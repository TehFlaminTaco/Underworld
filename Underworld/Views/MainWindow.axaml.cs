using Avalonia.Controls;
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


namespace Underworld.Views;

public partial class MainWindow : Window
{
    private ViewModels.MainWindowViewModel? _vm;

    public MainWindow()
    {
        // Wire the view model BEFORE InitializeComponent so bindings can resolve
        _vm = new ViewModels.MainWindowViewModel();
        this.DataContext = _vm;
        
        InitializeComponent();

        // Setup selection tracking on window load
        this.Loaded += MainWindow_Loaded;
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
        (App.Current?.ApplicationLifetime as Avalonia.Controls.ApplicationLifetimes.IClassicDesktopStyleApplicationLifetime)?.Shutdown();
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
        if (item == null || !File.Exists(item.Path))
            return;

        try
        {
            var dir = Path.GetDirectoryName(item.Path) ?? item.Path;
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                Process.Start(new ProcessStartInfo { FileName = "explorer.exe", Arguments = $"/select,\"{item.Path}\"", UseShellExecute = true });
            }
            else if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
            {
                Process.Start(new ProcessStartInfo { FileName = "open", Arguments = $"-R \"{item.Path}\"", UseShellExecute = false });
            }
            else
            {
                // Linux/other: open the directory
                Process.Start(new ProcessStartInfo { FileName = "xdg-open", Arguments = $"\"{dir}\"", UseShellExecute = false });
            }
        }
        catch
        {
            // Silently fail if we can't open the location
        }
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
            // After the dialog closes, refresh IWADs in the ViewModel
            WadLists.GetNewWadInfos();
            if (this.DataContext is ViewModels.MainWindowViewModel vm)
            {
                vm.IWADs.Clear();
                var wads = WadLists.GetAllWads();
                foreach (var wad in wads)
                {
                    var wadInfo = WadLists.GetWadInfo(wad);
                    if(wadInfo.IsPatch) continue;
                    vm.IWADs.Add(new(){
                        Path = wadInfo.Path,
                        DisplayName = wadInfo.Name
                    });
                }
            }
            // Clear the cache manually.
            OnClearCacheClicked(sender, e);
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
            MainWindowViewModel.AllWads.Clear();
            vm.FilteredAvailableWads.Clear();
            vm.FilteredSelectedWads.Clear();

            var wads = WadLists.GetAllWads();
            foreach (var wad in wads)
            {
                var wadInfo = WadLists.GetWadInfo(wad);
                if(!wadInfo.IsPatch) continue;
                var info = wadInfo.Info;
                MainWindowViewModel.AllWads.Add(info);
                vm.FilteredAvailableWads.Add(info);
                // Listen for IsSelected changes to move between collections
                info.PropertyChanged += (_, args) =>
                {
                    if (args.PropertyName == nameof(SelectWadInfo.IsSelected))
                    {
                        vm.OnWadSelectionChanged(info);
                    }
                };
            }
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
