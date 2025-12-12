using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using Avalonia.Interactivity;
using System;
using System.Collections.ObjectModel;
using System.Threading.Tasks;
using System.IO;
using System.Runtime.InteropServices;
using System.Collections.Generic;
using System.Diagnostics;
using Avalonia;
using Avalonia.Media;
using Avalonia.Layout;
using System.Linq;
using Avalonia.Platform.Storage;
using Avalonia.Threading;
using Avalonia.VisualTree;
using Underworld.Models;
using Underworld.ViewModels;
using System.Text;


namespace Underworld.Views;

public partial class MainWindow : Window
{
    private ViewModels.MainWindowViewModel? _vm;
    private const string WadDragFormat = "application/x-underworld-wad";
    private SelectWadInfo? _draggedSelectedWad;
    private DataGridRow? _currentInsertionRow;
    private bool _insertBeforeCurrentRow;

    private sealed class WadDragPayload
    {
        public SelectWadInfo Wad { get; init; } = null!;
        public bool FromSelectedList { get; init; }
    }

    // Stays true once the user initiates a close so a second close attempt force-exits without prompting.
    bool ReadyToDie = false;
    public MainWindow()
    {
        // Wire the view model BEFORE InitializeComponent so bindings can resolve
        _vm = new ViewModels.MainWindowViewModel();
        this.DataContext = _vm;
        
        InitializeComponent();

        // Setup selection tracking on window load
        this.Loaded += MainWindow_Loaded;
        this.Closing += (_, e) => {
            if (ReadyToDie) return; // Second attempt is treated as "force close".
            Console.WriteLine("=== MainWindow Closing event called ===");
            e.Cancel = true;
            _ = AskToCloseAsync();
        };
        this.Closed += (_, _) => _vm?.Dispose();
    }

    public async Task AskToCloseAsync()
    {
        ReadyToDie = true;
        var vm = this.DataContext as MainWindowViewModel;
        if (vm == null){
            (Application.Current?.ApplicationLifetime as Avalonia.Controls.ApplicationLifetimes.IClassicDesktopStyleApplicationLifetime)?.Shutdown();
            return;
        }
        if (!MainWindowViewModel.UserPreferences.ShowNoProfileExitWarning){
            (Application.Current?.ApplicationLifetime as Avalonia.Controls.ApplicationLifetimes.IClassicDesktopStyleApplicationLifetime)?.Shutdown();
            return;
        }
        if (MainWindowViewModel.SELECTED_PROFILE != null)
        {
            (Application.Current?.ApplicationLifetime as Avalonia.Controls.ApplicationLifetimes.IClassicDesktopStyleApplicationLifetime)?.Shutdown();
            return;
        }
        // Check if the user has any wads selected, or a arguments added
        // If they do, they might want to save it to a profile.
        bool shouldShowDialog = MainWindowViewModel.AllWads.Any(c=>c.IsSelected) || !string.IsNullOrWhiteSpace(MainWindowViewModel.COMMAND_LINE_ARGUMENTS);
        if (shouldShowDialog)
        {
            bool result = await vm.ShowConfirmDialogue(
                "Exit Underworld",
                "You have selected WADs or command-line arguments that are not saved to a profile. Are you sure you want to exit without saving?",
                () => { MainWindowViewModel.UserPreferences.ShowNoProfileExitWarning = false; MainWindowViewModel.UserPreferences.Save(); },
                "Exit",
                "Cancel"
            );
            if (result)
            {
                (Application.Current?.ApplicationLifetime as Avalonia.Controls.ApplicationLifetimes.IClassicDesktopStyleApplicationLifetime)?.Shutdown();
            }
            else
            {
                ReadyToDie = false;
            }
        }
        else
        {
            (Application.Current?.ApplicationLifetime as Avalonia.Controls.ApplicationLifetimes.IClassicDesktopStyleApplicationLifetime)?.Shutdown();
        }
    }

    private void MainWindow_Loaded(object? sender, RoutedEventArgs e)
    {
        // Check if data directories are configured
        CheckDataDirectoriesOnStartup();

        // Sync DataGrid selections with ViewModel collections
        var wadGrid1 = this.FindControl<DataGrid>("WADsDataGrid1");
        if (wadGrid1 != null)
        {
            // Use KeyUp to capture selection changes after the DataGrid updates
            wadGrid1.KeyUp += (s, e) =>
            {
                SyncAvailableWadsSelection(wadGrid1);
            };
            
            // Also sync on pointer release
            wadGrid1.PointerReleased += (s, e) =>
            {
                SyncAvailableWadsSelection(wadGrid1);
            };

            // Also listen for SelectionChanged - more reliable for updating SelectedItems
            wadGrid1.SelectionChanged += (s, e) =>
            {
                // Post to the UI thread to ensure the DataGrid has finished updating its SelectedItems
                Dispatcher.UIThread.Post(() => SyncAvailableWadsSelection(wadGrid1));
            };

            wadGrid1.AddHandler(InputElement.PointerPressedEvent, OnAvailableWadsGridPointerPressed, RoutingStrategies.Tunnel | RoutingStrategies.Bubble, handledEventsToo: true);
            wadGrid1.AddHandler(DragDrop.DragOverEvent, OnAvailableWadsDragOver, RoutingStrategies.Tunnel | RoutingStrategies.Bubble, handledEventsToo: true);
            wadGrid1.AddHandler(DragDrop.DropEvent, OnAvailableWadsDrop, RoutingStrategies.Tunnel | RoutingStrategies.Bubble, handledEventsToo: true);
        }
        else
        {
            Console.WriteLine("WADsDataGrid1 not found!");
        }

        var wadGrid2 = this.FindControl<DataGrid>("WADsDataGrid2");
        if (wadGrid2 != null)
        {
            // Use KeyUp to capture selection changes after the DataGrid updates
            wadGrid2.KeyUp += (s, e) =>
            {
                SyncSelectedWadsSelection(wadGrid2);
            };
            
            // Also sync on pointer release
            wadGrid2.PointerReleased += (s, e) =>
            {
                SyncSelectedWadsSelection(wadGrid2);
            };

            // Also listen for SelectionChanged - more reliable for updating SelectedItems
            wadGrid2.SelectionChanged += (s, e) =>
            {
                Dispatcher.UIThread.Post(() => SyncSelectedWadsSelection(wadGrid2));
            };

            wadGrid2.AddHandler(InputElement.PointerPressedEvent, OnSelectedWadsGridPointerPressed, RoutingStrategies.Tunnel | RoutingStrategies.Bubble, handledEventsToo: true);
            wadGrid2.AddHandler(DragDrop.DragOverEvent, OnSelectedWadsDragOver, RoutingStrategies.Tunnel | RoutingStrategies.Bubble, handledEventsToo: true);
            wadGrid2.AddHandler(DragDrop.DropEvent, OnSelectedWadsDrop, RoutingStrategies.Tunnel | RoutingStrategies.Bubble, handledEventsToo: true);
            wadGrid2.AddHandler(DragDrop.DragLeaveEvent, OnSelectedWadsDragLeave, RoutingStrategies.Tunnel | RoutingStrategies.Bubble, handledEventsToo: true);
        }
        else
        {
            Console.WriteLine("WADsDataGrid2 not found!");
        }
    }

    private void SyncAvailableWadsSelection(DataGrid wadGrid1)
    {
        if (_vm == null || wadGrid1 == null) return;
        
        _vm.SelectedItemsAvailableWads.Clear();
        Console.WriteLine($"[Sync1] _vm instance hash: {_vm.GetHashCode()} | VM.SelectedItemsAvailableWads hash: {_vm.SelectedItemsAvailableWads.GetHashCode()}");
        
        // Get selected items from DataGrid
        Console.WriteLine($"[Sync1] SelectedItems: {wadGrid1.SelectedItems}");
        if (wadGrid1.SelectedItems != null)
        {
            Console.WriteLine($"[Sync1] SelectedItems count: {wadGrid1.SelectedItems.Count}");
            var selected = wadGrid1.SelectedItems as System.Collections.IList;
            if (selected != null)
            {
                foreach (var item in selected)
                {
                    Console.WriteLine($"[Sync1] Item: {item?.GetType().Name} = {item}");
                    if (item is SelectWadInfo wad)
                    {
                        _vm.SelectedItemsAvailableWads.Add(wad);
                    }else{
                        Console.WriteLine($"[Sync1] Item is not SelectWadInfo"); 
                    }
                }
            }
        }
        
        Console.WriteLine($"[Sync1] Final selection count: {_vm.SelectedItemsAvailableWads.Count}");
    }

    private void SyncSelectedWadsSelection(DataGrid wadGrid2)
    {
        if (_vm == null || wadGrid2 == null) return;
        
        _vm.SelectedItemsSelectedWads.Clear();
        
        Console.WriteLine($"[Sync2] SelectedItems: {wadGrid2.SelectedItems}");
        if (wadGrid2.SelectedItems != null)
        {
            Console.WriteLine($"[Sync2] SelectedItems count: {wadGrid2.SelectedItems.Count}");
            var selected = wadGrid2.SelectedItems as System.Collections.IList;
            if (selected != null)
            {
                foreach (var item in selected)
                {
                    Console.WriteLine($"[Sync2] Item: {item?.GetType().Name} = {item}");
                    if (item is SelectWadInfo wad)
                    {
                        _vm.SelectedItemsSelectedWads.Add(wad);
                    }
                }
            }
        }
        
        Console.WriteLine($"[Sync2] Final selection count: {_vm.SelectedItemsSelectedWads.Count}");
    }

    private void OnNewClicked(object? sender, RoutedEventArgs e)
    {
        // Placeholder for New action
        Console.WriteLine("New clicked");
    }

    private void OnOpenClicked(object? sender, RoutedEventArgs e)
    {
        // Placeholder for Open action
        Console.WriteLine("Open clicked");
    }

    private void OnSaveClicked(object? sender, RoutedEventArgs e)
    {
        // Placeholder for Save action
        Console.WriteLine("Save clicked");
    }

    // Custom titlebar event handlers
    private void OnTitleBarPointerPressed(object? sender, Avalonia.Input.PointerPressedEventArgs e)
    {
        // Don't handle if the event came from the Menu or MenuItem
        var source = e.Source;
        while (source != null)
        {
            if (source is Menu || source is MenuItem)
            {
                Console.WriteLine($"Click came from {source.GetType().Name}, ignoring for title bar drag");
                return;
            }
            if (source is Avalonia.Visual visual)
                source = visual.GetVisualParent();
            else
                break;
        }
        
        if (e.GetCurrentPoint(this).Properties.IsLeftButtonPressed)
        {
            Console.WriteLine("Starting title bar drag");
            BeginMoveDrag(e);
        }
    }

    private void OnTitleBarDoubleTapped(object? sender, RoutedEventArgs e)
    {
        // Don't handle if the event came from the Menu or its children
        var source = e.Source;
        while (source != null)
        {
            if (source is Menu || source is MenuItem)
            {
                Console.WriteLine("Double-click came from Menu/MenuItem, ignoring");
                return;
            }
            if (source is Avalonia.Visual visual)
                source = visual.GetVisualParent();
            else
                break;
        }
        
        Console.WriteLine("Toggling window state");
        WindowState = WindowState == WindowState.Maximized ? WindowState.Normal : WindowState.Maximized;
    }

    private void OnMinimizeClicked(object? sender, RoutedEventArgs e)
    {
        WindowState = WindowState.Minimized;
    }

    private void OnMaximizeClicked(object? sender, RoutedEventArgs e)
    {
        WindowState = WindowState == WindowState.Maximized ? WindowState.Normal : WindowState.Maximized;
    }

    private void OnCloseClicked(object? sender, RoutedEventArgs e)
    {
        Close();
    }

    private void OnExitClicked(object? sender, RoutedEventArgs e)
    {
        Console.WriteLine("=== OnExitClicked CALLED ===");
        _ = AskToCloseAsync();
    }

    private void OnMenuPointerPressed(object? sender, Avalonia.Input.PointerPressedEventArgs e)
    {
        // Stop the event from propagating to the title bar
        e.Handled = true;
    }

    private void OnAboutClicked(object? sender, RoutedEventArgs e)
    {
        Console.WriteLine("=== OnAboutClicked CALLED - Menu events ARE working ===");
        ShowPopup("About", "Underworld\n\nUnderworld is a DooM launcher created by TehFlaminTaco. AI was utilized in the creation of this program and art assets.\n\nLicensed CC BY-SA 4.0");
    }

    private async void OnReimportProfilesClicked(object? sender, RoutedEventArgs e)
    {
        if (_vm == null)
        {
            return;
        }

        var report = _vm.ReimportInvalidProfiles();
        var sb = new StringBuilder();

        if (report.SuccessfulReimports == 0 && report.StillInvalid.Count == 0)
        {
            sb.AppendLine("All profiles are already valid. No reimports were required.");
        }
        else
        {
            sb.AppendLine($"Successful reimports: {report.SuccessfulReimports}");
            sb.AppendLine($"Already valid: {report.AlreadyValid}");
        }

        if (report.StillInvalid.Count > 0)
        {
            sb.AppendLine();
            sb.AppendLine("Still invalid after reimport:");
            foreach (var entry in report.StillInvalid)
            {
                sb.AppendLine($"• {entry}");
            }
        }

        await ShowPopup("Reimport Profiles", sb.ToString());
    }

    private async void OnManageThemeClicked(object? sender, RoutedEventArgs e)
    {
        var dialog = new ThemeManagerDialog();
        await dialog.ShowDialog(this);
    }

    private void OnRunGameClicked(object? sender, RoutedEventArgs e)
    {
        if (_vm != null)
        {
            _vm.RunGameCommand.Execute(null);
        }
    }

    public void OnAvailableWADsFilterTextChanged(object? sender, TextChangedEventArgs e)
    {
        var textBox = sender as TextBox;
        var filterText = textBox?.Text ?? string.Empty;

        if (this.DataContext is ViewModels.MainWindowViewModel vm)
        {
            vm.UpdateAvailableWadsFilter(filterText);
        }
    }

    public void OnClearAvailableWADsFilterClicked(object? sender, RoutedEventArgs e)
    {
        var textBox = this.FindControl<TextBox>("WADFilterTextBox1");
        if (textBox != null)
        {
            textBox.Text = string.Empty;
        }
    }

    public void OnSelectedWADsFilterTextChanged(object? sender, TextChangedEventArgs e)
    {
        var textBox = sender as TextBox;
        var filterText = textBox?.Text ?? string.Empty;

        if (this.DataContext is ViewModels.MainWindowViewModel vm)
        {
            vm.UpdateSelectedWadsFilter(filterText);
        }
    }

    public void OnClearSelectedWADsFilterClicked(object? sender, RoutedEventArgs e)
    {
        var textBox = this.FindControl<TextBox>("WADFilterTextBox2");
        if (textBox != null)
        {
            textBox.Text = string.Empty;
        }
    }

    // Executable detection moved to ExecutableManager; remove duplicate implementation here.

    private async Task ShowInvalidSelectionsDialog(List<string> invalids)
    {
        // Build a simple modal dialog window listing invalid selections
        var dlg = new Window
        {
            Title = "Invalid selections",
            Width = 500,
            Height = 300,
            Content = new ScrollViewer
            {
                Content = new StackPanel
                {
                    Margin = new Thickness(10),
                    Children =
                    {
                        new TextBlock { Text = "The following selections are not valid executables:", FontWeight = FontWeight.Bold, Margin = new Thickness(0,0,0,8) }
                    }
                }
            }
        };

        var stack = ((dlg.Content as ScrollViewer)!.Content as StackPanel)!;
        foreach (var s in invalids)
        {
            stack.Children.Add(new TextBlock { Text = s, TextWrapping = TextWrapping.Wrap });
        }

        var ok = new Button { Content = "OK", HorizontalAlignment = HorizontalAlignment.Right, Margin = new Thickness(0,8,0,0) };
        ok.Click += (_, _) => dlg.Close();
        stack.Children.Add(ok);

        await dlg.ShowDialog(this);
    }

    private static void TryOpenPathInFileManager(string? targetPath, bool selectFile)
    {
        if (string.IsNullOrWhiteSpace(targetPath))
            return;

        try
        {
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                if (selectFile && File.Exists(targetPath))
                {
                    Process.Start(new ProcessStartInfo
                    {
                        FileName = "explorer.exe",
                        Arguments = $"/select,\"{targetPath}\"",
                        UseShellExecute = true
                    });
                }
                else
                {
                    var folder = GetExistingDirectoryOrParent(targetPath);
                    Process.Start(new ProcessStartInfo
                    {
                        FileName = "explorer.exe",
                        Arguments = $"\"{folder}\"",
                        UseShellExecute = true
                    });
                }
            }
            else if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
            {
                var args = selectFile && File.Exists(targetPath)
                    ? $"-R \"{targetPath}\""
                    : $"\"{GetExistingDirectoryOrParent(targetPath)}\"";

                Process.Start(new ProcessStartInfo
                {
                    FileName = "open",
                    Arguments = args,
                    UseShellExecute = false
                });
            }
            else
            {
                var folder = GetExistingDirectoryOrParent(targetPath);
                Process.Start(new ProcessStartInfo
                {
                    FileName = "xdg-open",
                    Arguments = $"\"{folder}\"",
                    UseShellExecute = false
                });
            }
        }
        catch
        {
            // Ignore failures when launching external file manager
        }
    }

    private static string GetExistingDirectoryOrParent(string path)
    {
        if (Directory.Exists(path))
            return path;

        var directory = Path.GetDirectoryName(path);
        if (!string.IsNullOrEmpty(directory) && Directory.Exists(directory))
            return directory;

        return path;
    }

    // Removal is handled by the ViewModel command bound in XAML.

    private void OnContextRenameClicked(object? sender, RoutedEventArgs e)
    {
        var item = (this.FindControl<ListBox>("ExecutablesList")?.SelectedItem as ExecutableItem);
        if (item == null)
            return;

        _ = ShowRenameDialog(item);
    }

    private void OnContextOpenLocationClicked(object? sender, RoutedEventArgs e)
    {
        var item = (this.FindControl<ListBox>("ExecutablesList")?.SelectedItem as ExecutableItem);
        if (item == null)
            return;

        TryOpenPathInFileManager(item.Path, selectFile: true);
    }

    private void OnContextDeleteClicked(object? sender, RoutedEventArgs e)
    {
        var item = (this.FindControl<ListBox>("ExecutablesList")?.SelectedItem as ExecutableItem);
        if (item == null)
            return;

        if (this.DataContext is ViewModels.MainWindowViewModel vm)
        {
            vm.SelectedExecutable = item;
            vm.RemoveSelectedExecutableCommand.Execute(null);
        }
    }

    private async Task ShowRenameDialog(ExecutableItem item)
    {
        var originalName = item.DisplayName;
        var input = new TextBox
        {
            Text = item.DisplayName,
            Foreground = new Avalonia.Media.SolidColorBrush(Avalonia.Media.Colors.White),
            Background = new Avalonia.Media.SolidColorBrush(Avalonia.Media.Colors.DarkGray),
            Padding = new Thickness(5)
        };

        var okBtn = new Button { Content = "OK", Width = 80 };
        var cancelBtn = new Button { Content = "Cancel", Width = 80 };

        var dlg = new Window
        {
            Title = "Change Display Name",
            Width = 400,
            SizeToContent = SizeToContent.Height,
            Content = new StackPanel
            {
                Margin = new Thickness(10),
                Spacing = 8,
                Children =
                {
                    new TextBlock 
                    { 
                        Text = "Enter new display name:", 
                        Foreground = new Avalonia.Media.SolidColorBrush(Avalonia.Media.Colors.White) 
                    },
                    input,
                    new StackPanel
                    {
                        Orientation = Orientation.Horizontal,
                        Spacing = 8,
                        HorizontalAlignment = HorizontalAlignment.Right,
                        Children = { okBtn, cancelBtn }
                    }
                }
            }
        };

        // Focus the textbox and select all text when dialog opens
        dlg.Opened += (_, _) =>
        {
            input.Focus();
            input.SelectAll();
        };

        okBtn.Click += (_, _) =>
        {
            if (!string.IsNullOrWhiteSpace(input.Text))
            {
                item.DisplayName = input.Text.Trim();
            }
            dlg.Close();
        };

        cancelBtn.Click += (_, _) => dlg.Close();

        input.KeyDown += (_, e) =>
        {
            if (e.Key == Avalonia.Input.Key.Return && !string.IsNullOrWhiteSpace(input.Text))
            {
                item.DisplayName = input.Text.Trim();
                dlg.Close();
            }
            else if (e.Key == Avalonia.Input.Key.Escape)
            {
                dlg.Close();
            }
        };

        await dlg.ShowDialog(this);
    }

    private async void OnManageDataFoldersClicked(object? sender, RoutedEventArgs e)
    {
        var dialog = new DataDirectoriesDialog(this);
        dialog.Closed += (_, _) =>
        {
            WadDirectoryWatcher.RefreshWatchers();
            WadDirectoryWatcher.RequestFullRescan();
        };
        await dialog.ShowDialog(this);
    }

    private void OnClearCacheClicked(object? sender, RoutedEventArgs e)
    {
        // Clear internal WAD caches
        WadLists.ClearWadCache();

        // Refresh the ViewModel's WAD lists if present
        if (this.DataContext is ViewModels.MainWindowViewModel vm)
        {
            vm.ReloadWadsFromDisk();
        }
    }

    private void OnAvailableWADsDoubleClicked(object? sender, RoutedEventArgs e)
    {
        OnAddWadsClicked(sender, e);
    }

    private void OnSelectedWADsDoubleClicked(object? sender, RoutedEventArgs e)
    {
        OnRemoveWadsClicked(sender, e);
    }

    private async void OnAvailableWadsGridPointerPressed(object? sender, PointerPressedEventArgs e)
    {
        if (sender is not DataGrid grid)
            return;

        if (this.DataContext is not ViewModels.MainWindowViewModel vm)
            return;

        if (vm.SelectedProfile?.Locked == true)
            return;

        var point = e.GetCurrentPoint(grid);
        if (!point.Properties.IsLeftButtonPressed)
            return;

        var row = (e.Source as Visual)?.FindAncestorOfType<DataGridRow>();
        if (row?.DataContext is not SelectWadInfo wad)
            return;

        _draggedSelectedWad = null;
        #pragma warning disable CS0618
        var data = new DataObject();
        data.Set(WadDragFormat, new WadDragPayload
        {
            Wad = wad,
            FromSelectedList = false
        });
        await DragDrop.DoDragDrop(e, data, DragDropEffects.Move);
        #pragma warning restore CS0618
    }

    private void OnAvailableWadsDragOver(object? sender, DragEventArgs e)
    {
        if (!TryGetDragPayload(e, out var payload) || !payload.FromSelectedList)
        {
            e.DragEffects = DragDropEffects.None;
            return;
        }

        if (this.DataContext is not ViewModels.MainWindowViewModel vm || vm.SelectedProfile?.Locked == true)
        {
            e.DragEffects = DragDropEffects.None;
            return;
        }

        e.DragEffects = DragDropEffects.Move;
        e.Handled = true;
    }

    private void OnAvailableWadsDrop(object? sender, DragEventArgs e)
    {
        if (!TryGetDragPayload(e, out var payload) || !payload.FromSelectedList)
            return;

        if (this.DataContext is not ViewModels.MainWindowViewModel vm || vm.SelectedProfile?.Locked == true)
            return;

        if (!payload.Wad.IsSelected)
            return;

        vm.RemoveWadsFromItems(new List<SelectWadInfo> { payload.Wad });
        ClearInsertionVisuals();
        e.Handled = true;
    }

    private async void OnSelectedWadsGridPointerPressed(object? sender, PointerPressedEventArgs e)
    {
        if (sender is not DataGrid grid)
            return;

        if (this.DataContext is not ViewModels.MainWindowViewModel vm)
            return;

        if (vm.SelectedProfile?.Locked == true)
            return;

        var point = e.GetCurrentPoint(grid);
        if (!point.Properties.IsLeftButtonPressed)
            return;

        var row = (e.Source as Visual)?.FindAncestorOfType<DataGridRow>();
        if (row?.DataContext is not SelectWadInfo wad)
            return;

        _draggedSelectedWad = wad;
        #pragma warning disable CS0618
        var data = new DataObject();
        data.Set(WadDragFormat, new WadDragPayload
        {
            Wad = wad,
            FromSelectedList = true
        });
        await DragDrop.DoDragDrop(e, data, DragDropEffects.Move);
        #pragma warning restore CS0618
        ClearInsertionVisuals();
        _draggedSelectedWad = null;
    }

    private void OnSelectedWadsDragOver(object? sender, DragEventArgs e)
    {
        if (sender is not DataGrid grid)
            return;

        if (!TryGetDragPayload(e, out _))
        {
            e.DragEffects = DragDropEffects.None;
            return;
        }

        if (this.DataContext is not ViewModels.MainWindowViewModel vm || vm.SelectedProfile?.Locked == true)
        {
            e.DragEffects = DragDropEffects.None;
            return;
        }

        e.DragEffects = DragDropEffects.Move;
        e.Handled = true;

        var row = ResolveRowFromDragEvent(grid, e);
        if (row != null && row.DataContext is SelectWadInfo)
        {
            var relative = e.GetPosition(row);
            var insertBefore = relative.Y <= row.Bounds.Height / 2;
            ShowInsertionVisual(row, insertBefore);
        }
        else
        {
            ClearInsertionVisuals();
        }
    }

    private void OnSelectedWadsDrop(object? sender, DragEventArgs e)
    {
        if (sender is not DataGrid grid)
            return;

        if (!TryGetDragPayload(e, out var payload))
            return;

        if (this.DataContext is not ViewModels.MainWindowViewModel vm || vm.SelectedProfile?.Locked == true)
            return;

        var insertionIndex = CalculateInsertionIndex(vm, grid, e);
        if (payload.FromSelectedList)
        {
            if (_draggedSelectedWad == null)
                _draggedSelectedWad = payload.Wad;

            vm.MoveSelectedWad(_draggedSelectedWad, insertionIndex);
        }
        else
        {
            vm.InsertWadIntoLoadOrder(payload.Wad, insertionIndex);
        }

        ClearInsertionVisuals();
        _draggedSelectedWad = null;
        e.Handled = true;
    }

    private void OnSelectedWadsDragLeave(object? sender, DragEventArgs e)
    {
        ClearInsertionVisuals();
    }

    private bool TryGetDragPayload(DragEventArgs e, out WadDragPayload payload)
    {
        #pragma warning disable CS0618
        if (e.Data?.Get(WadDragFormat) is WadDragPayload info)
        #pragma warning restore CS0618
        {
            payload = info;
            return true;
        }

        payload = null!;
        return false;
    }

    private int CalculateInsertionIndex(ViewModels.MainWindowViewModel vm, DataGrid grid, DragEventArgs e)
    {
        if (_currentInsertionRow?.DataContext is SelectWadInfo target)
        {
            var index = vm.FilteredSelectedWads.IndexOf(target);
            if (index >= 0)
            {
                return _insertBeforeCurrentRow ? index : index + 1;
            }
        }

        var fallbackRow = ResolveRowFromDragEvent(grid, e);
        if (fallbackRow?.DataContext is SelectWadInfo fallbackTarget)
        {
            var fallbackIndex = vm.FilteredSelectedWads.IndexOf(fallbackTarget);
            if (fallbackIndex >= 0)
            {
                var relative = e.GetPosition(fallbackRow);
                var insertBefore = relative.Y <= fallbackRow.Bounds.Height / 2;
                return insertBefore ? fallbackIndex : fallbackIndex + 1;
            }
        }

        return vm.FilteredSelectedWads.Count;
    }

    private DataGridRow? ResolveRowFromDragEvent(DataGrid grid, DragEventArgs e)
    {
        var directRow = (e.Source as Visual)?.FindAncestorOfType<DataGridRow>();
        if (directRow != null)
        {
            return directRow;
        }

        var pointInGrid = e.GetPosition(grid);
        if (grid.InputHitTest(pointInGrid) is Visual hitVisual)
        {
            var hitRow = hitVisual.FindAncestorOfType<DataGridRow>();
            if (hitRow != null)
            {
                return hitRow;
            }
        }

        var rowsPresenter = grid.GetVisualDescendants()
            .OfType<DataGridRowsPresenter>()
            .FirstOrDefault();

        if (rowsPresenter == null || rowsPresenter.Children.Count == 0)
        {
            return null;
        }

        var pointInPresenter = e.GetPosition(rowsPresenter);
        foreach (var child in rowsPresenter.Children)
        {
            if (child is DataGridRow candidate)
            {
                if (pointInPresenter.Y <= candidate.Bounds.Bottom)
                {
                    return candidate;
                }
            }
        }

        return rowsPresenter.Children
            .OfType<DataGridRow>()
            .LastOrDefault();
    }

    private void ShowInsertionVisual(DataGridRow row, bool insertBefore)
    {
        if (_currentInsertionRow != row)
        {
            ClearInsertionVisuals();
            _currentInsertionRow = row;
        }

        _insertBeforeCurrentRow = insertBefore;
        row.Classes.Set("insertion-before", insertBefore);
        row.Classes.Set("insertion-after", !insertBefore);
    }

    private void ClearInsertionVisuals()
    {
        if (_currentInsertionRow != null)
        {
            _currentInsertionRow.Classes.Set("insertion-before", false);
            _currentInsertionRow.Classes.Set("insertion-after", false);
            _currentInsertionRow = null;
        }
    }

    private void OnAddWadsClicked(object? sender, RoutedEventArgs e)
    {
        // Block if profile is locked
        if (this.DataContext is ViewModels.MainWindowViewModel vm && vm.SelectedProfile?.Locked == true)
        {
            _ = ShowPopup("Profile Locked", "Cannot modify WAD selection while profile is locked.");
            return;
        }

        // Force-sync selection from the DataGrid into the ViewModel before executing the command
        var wadGrid1 = this.FindControl<DataGrid>("WADsDataGrid1");
        if (wadGrid1 != null)
        {
            // Try to sync the existing VM collection for diagnostics
            SyncAvailableWadsSelection(wadGrid1);

            // Also directly obtain the DataGrid.SelectedItems and pass them to the ViewModel
            var selected = wadGrid1.SelectedItems as System.Collections.IList;
            if (selected != null)
            {
                var list = new List<SelectWadInfo>();
                foreach (var it in selected)
                {
                    if (it is SelectWadInfo s)
                        list.Add(s);
                }

                if (this.DataContext is ViewModels.MainWindowViewModel viewModel)
                {
                    // Call the new method that accepts items directly to avoid timing races
                    viewModel.AddWadsFromItems(list);
                    return; // we've handled the add directly
                }
            }
        }
    }

    private void OnRemoveWadsClicked(object? sender, RoutedEventArgs e)
    {
        // Block if profile is locked
        if (this.DataContext is ViewModels.MainWindowViewModel vm && vm.SelectedProfile?.Locked == true)
        {
            _ = ShowPopup("Profile Locked", "Cannot modify WAD selection while profile is locked.");
            return;
        }

        // Force-sync selection from the DataGrid into the ViewModel before executing the command
        var wadGrid2 = this.FindControl<DataGrid>("WADsDataGrid2");
        if (wadGrid2 != null)
        {
            // Try to sync the existing VM collection for diagnostics
            SyncSelectedWadsSelection(wadGrid2);

            // Also directly obtain the DataGrid.SelectedItems and pass them to the ViewModel
            var selected = wadGrid2.SelectedItems as System.Collections.IList;
            if (selected != null)
            {
                var list = new List<SelectWadInfo>();
                foreach (var it in selected)
                {
                    if (it is SelectWadInfo s)
                        list.Add(s);
                }

                if (this.DataContext is ViewModels.MainWindowViewModel viewModel)
                {
                    // Call the new method that accepts items directly to avoid timing
                    viewModel.RemoveWadsFromItems(list);
                    return; // we've handled the remove directly
                }
            }
        }
    }

    private void OnContextAddAvailableWadClicked(object? sender, RoutedEventArgs e)
    {
        if (sender is not MenuItem { DataContext: SelectWadInfo wad })
            return;

        if (this.DataContext is not ViewModels.MainWindowViewModel vm)
            return;

        if (vm.SelectedProfile?.Locked == true)
        {
            _ = ShowPopup("Profile Locked", "Cannot modify WAD selection while profile is locked.");
            return;
        }

        vm.AddWadsFromItems(new List<SelectWadInfo> { wad });
    }

    private void OnContextRemoveSelectedWadClicked(object? sender, RoutedEventArgs e)
    {
        if (sender is not MenuItem { DataContext: SelectWadInfo wad })
            return;

        if (this.DataContext is not ViewModels.MainWindowViewModel vm)
            return;

        if (vm.SelectedProfile?.Locked == true)
        {
            _ = ShowPopup("Profile Locked", "Cannot modify WAD selection while profile is locked.");
            return;
        }

        vm.RemoveWadsFromItems(new List<SelectWadInfo> { wad });
    }

    private void OnContextOpenWadLocationClicked(object? sender, RoutedEventArgs e)
    {
        if (sender is not MenuItem menuItem)
            return;

        string? path = menuItem.DataContext switch
        {
            SelectWadInfo wad => wad.Path,
            IWad iw => iw.Path,
            _ => null
        };

        TryOpenPathInFileManager(path, selectFile: true);
    }

    private void OnNewProfileClicked(object? sender, RoutedEventArgs e)
    {
        var textBox = new TextBox { Width = 360, Margin = new Thickness(0,0,0,8) };
        var okButton = new Button { Content = "OK", Width = 80, Margin = new Thickness(0,0,8,0) };
        var cancelButton = new Button { Content = "Cancel", Width = 80 };

        // Basic text edit dialog to name a new profile
        var dlg = new Window
        {
            Title = "Name Profile",
            Width = 400,
            SizeToContent = SizeToContent.Height,
            Content = new StackPanel
            {
                Margin = new Thickness(10),
                Children =
                {
                    new TextBlock { Text = "Enter a name for the new profile:", Margin = new Thickness(0,0,0,8) },
                    textBox,
                    new StackPanel
                    {
                        Orientation = Orientation.Horizontal,
                        HorizontalAlignment = HorizontalAlignment.Right,
                        Children =
                        {
                            okButton,
                            cancelButton
                        }
                    }
                }
            }
        };
        okButton.Click += (_, _) =>
        {
            var name = (textBox.Text ?? string.Empty).Trim();
            if (!string.IsNullOrEmpty(name))
            {
                if (this.DataContext is ViewModels.MainWindowViewModel vm)
                {
                    var profile = new Profile {
                        Name = name,
                        PreferredExecutable = vm.SelectedExecutable?.Path ?? string.Empty,
                        PreferredIWAD = vm.SelectedIWAD?.Path ?? string.Empty
                    };
                    foreach(var wad in MainWindowViewModel.AllWads.Where(w => w.IsSelected))
                    {
                        profile.SelectedWads.Add(wad.Path);
                    }
                    vm.Profiles.Add(profile);
                    vm.SelectedProfile = profile;
                }
                dlg.Close();
            }
        };
        cancelButton.Click += (_, _) =>
        {
            dlg.Close();
        };
        dlg.Show();
    }

    private void OnRemoveProfileClicked(object? sender, RoutedEventArgs e)
    {
        if (this.DataContext is ViewModels.MainWindowViewModel vm)
        {
            var profile = vm.SelectedProfile;
            if (profile != null && !profile.Locked)
            {
                vm.Profiles.Remove(profile);
                vm.SelectedProfile = null;
            }
        }
    }

    private void OnRenameProfileMenuClicked(object? sender, RoutedEventArgs e)
    {
        if (sender is not MenuItem { DataContext: Profile profile })
            return;

        if (this.DataContext is ViewModels.MainWindowViewModel vm)
        {
            vm.SelectedProfile = profile;
            ShowRenameProfileDialog(profile);
        }
    }

    private void OnContextRemoveProfileClicked(object? sender, RoutedEventArgs e)
    {
        if (sender is not MenuItem { DataContext: Profile profile })
            return;

        if (this.DataContext is ViewModels.MainWindowViewModel vm)
        {
            vm.SelectedProfile = profile;
            OnRemoveProfileClicked(sender, e);
        }
    }

    private void OnExportProfileClicked(object? sender, RoutedEventArgs e)
    {
        if (sender is not MenuItem { DataContext: Profile profile })
            return;

        if (this.DataContext is ViewModels.MainWindowViewModel vm)
        {
            vm.SelectedProfile = profile;
            _ = vm.ExportProfileAsync(this);
        }
    }

    private void OnMoveProfileToTopClicked(object? sender, RoutedEventArgs e)
    {
        if (sender is not MenuItem { DataContext: Profile profile })
            return;

        if (this.DataContext is ViewModels.MainWindowViewModel vm)
        {
            MoveProfile(vm, profile, 0);
        }
    }

    private void OnMoveProfileToBottomClicked(object? sender, RoutedEventArgs e)
    {
        if (sender is not MenuItem { DataContext: Profile profile })
            return;

        if (this.DataContext is ViewModels.MainWindowViewModel vm && vm.Profiles.Count > 0)
        {
            MoveProfile(vm, profile, vm.Profiles.Count - 1);
        }
    }

    private void MoveProfile(ViewModels.MainWindowViewModel vm, Profile profile, int targetIndex)
    {
        var currentIndex = vm.Profiles.IndexOf(profile);
        if (currentIndex < 0 || targetIndex < 0 || targetIndex >= vm.Profiles.Count || currentIndex == targetIndex)
            return;

        vm.Profiles.Move(currentIndex, targetIndex);
        vm.SelectedProfile = profile;
    }

    private void ShowRenameProfileDialog(Profile profile)
    {
        var textBox = new TextBox { Width = 360, Margin = new Thickness(0,0,0,8), Text = profile.Name };
        var okButton = new Button { Content = "OK", Width = 80, Margin = new Thickness(0,0,8,0) };
        var cancelButton = new Button { Content = "Cancel", Width = 80 };

        var dlg = new Window
        {
            Title = "Rename Profile",
            Width = 400,
            SizeToContent = SizeToContent.Height,
            Content = new StackPanel
            {
                Margin = new Thickness(10),
                Children =
                {
                    new TextBlock { Text = "Enter a new name for the profile:", Margin = new Thickness(0,0,0,8) },
                    textBox,
                    new StackPanel
                    {
                        Orientation = Orientation.Horizontal,
                        HorizontalAlignment = HorizontalAlignment.Right,
                        Children =
                        {
                            okButton,
                            cancelButton
                        }
                    }
                }
            }
        };

        okButton.Click += (_, _) =>
        {
            var newName = (textBox.Text ?? string.Empty).Trim();
            if (!string.IsNullOrEmpty(newName))
            {
                // Get the VM
                if (this.DataContext is not ViewModels.MainWindowViewModel vm)
                {
                    dlg.Close();
                    return;
                }
                // Check for a save folder with the old Profile Name, and rename it.
                if(vm.EnsureSavesDirectoryExists()){
                    var oldPath = vm.GetSaveFolder(profile.Name);
                    var newPath = vm.GetSaveFolder(newName);
                    // If Folder exists, and names differ, rename
                    if(Directory.Exists(oldPath) && !oldPath.Equals(newPath, StringComparison.OrdinalIgnoreCase)){
                        try{
                            Directory.Move(oldPath, newPath);
                        }catch(Exception ex){
                            Console.WriteLine($"Error renaming save folder from '{oldPath}' to '{newPath}': {ex.Message}");
                        }
                    }
                }
                profile.Name = newName;
                dlg.Close();
            }
        };

        cancelButton.Click += (_, _) => dlg.Close();

        dlg.Show();
    }

    private async void CheckDataDirectoriesOnStartup()
    {
        // Load data directories configuration to check if any exist
        var dataDirectoriesConfig = Config.Setup("dataDirectories", new System.Collections.Generic.List<string>());
        var dirs = dataDirectoriesConfig.Get();
        
        // Check if there are no user-configured directories and no environment variables
        var envVar = Environment.GetEnvironmentVariable("DOOMWADDIR");
        var envPath = Environment.GetEnvironmentVariable("DOOMWADPATH");
        
        bool hasDataDirectories = (dirs != null && dirs.Count > 0) || 
                                  (!string.IsNullOrEmpty(envVar) && Directory.Exists(envVar)) ||
                                  (!string.IsNullOrEmpty(envPath));
        
        if (!hasDataDirectories)
        {
            var result = await ShowConfirmDialog(
                "No Data Folders Configured",
                "No data folders have been configured. Would you like to set them up now?\n\n" +
                "Data folders tell Underworld where to find your WAD files (DOOM.WAD, mods, etc.).",
                "Yes, Configure Now",
                "Skip");
            
            if (result)
            {
                // Open the data folders dialog
                OnManageDataFoldersClicked(this, new RoutedEventArgs());
            }
        }
    }

    private Task<bool> ShowConfirmDialog(string title, string text, string yesText, string noText)
    {
        TaskCompletionSource<bool> tcs = new();
        var yesButton = new Button { Content = yesText, MinWidth = 120, Margin = new Thickness(0,0,8,0) };
        var noButton = new Button { Content = noText, MinWidth = 80 };

        var dlg = new Window
        {
            Title = title,
            Width = 500,
            SizeToContent = SizeToContent.Height,
            Content = new StackPanel
            {
                Margin = new Thickness(10),
                Children =
                {
                    new TextBlock { Text = text, Margin = new Thickness(0,0,0,12), TextWrapping = TextWrapping.Wrap },
                    new StackPanel
                    {
                        Orientation = Orientation.Horizontal,
                        HorizontalAlignment = HorizontalAlignment.Right,
                        Children =
                        {
                            yesButton,
                            noButton
                        }
                    }
                }
            }
        };
        
        yesButton.Click += (_, _) =>
        {
            tcs.SetResult(true);
            dlg.Close();
        };
        
        noButton.Click += (_, _) =>
        {
            tcs.SetResult(false);
            dlg.Close();
        };
        
        dlg.Show();

        return tcs.Task;
    }

    public Task<bool> ShowPopup(string title, string text){
        TaskCompletionSource<bool> tcs = new();
        var okButton = new Button { Content = "OK", Width = 80, Margin = new Thickness(0,0,8,0) };

        // Basic text edit dialog to name a new profile
        var dlg = new Window
        {
            Title = title,
            Width = 400,
            SizeToContent = SizeToContent.WidthAndHeight,
            Content = new StackPanel
            {
                Margin = new Thickness(10),
                Children =
                {
                    new TextBlock { Text = text, Margin = new Thickness(0,0,0,8) },
                    new StackPanel
                    {
                        Orientation = Orientation.Horizontal,
                        HorizontalAlignment = HorizontalAlignment.Right,
                        Children =
                        {
                            okButton
                        }
                    }
                }
            }
        };
        okButton.Click += (_, _) =>
        {
            tcs.SetResult(true);
            dlg.Close();
        };
        dlg.Show();

        return tcs.Task;
    }
}
