using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Windows.Input;
using Underworld.Models;
using System.Collections.Generic;
using Avalonia.Controls;
using Avalonia.Platform.Storage;
using System.Threading.Tasks;
using System.Diagnostics;
using Underworld.Views;
using System.IO;
using Avalonia;
using Avalonia.Layout;

namespace Underworld.ViewModels;

public partial class MainWindowViewModel : ViewModelBase
{
    private readonly ExecutableManager _manager = new ExecutableManager();
    private readonly ConfigEntry<List<ExecutableItem>> _executablesConfig;
    private readonly ConfigEntry<List<Profile>> _profilesConfig;

    private readonly ConfigEntry<string> _lastSelectedExecutablePathConfig;
    private readonly ConfigEntry<string> _lastSelectedIWADPathConfig;

    public MainWindowViewModel()
    {
        _executablesConfig = Config.Setup("executables", new List<ExecutableItem>());
        _profilesConfig = Config.Setup("profiles", new List<Profile>());
        _lastSelectedExecutablePathConfig = Config.Setup("lastSelectedExecutablePath", string.Empty);
        _lastSelectedIWADPathConfig = Config.Setup("lastSelectedIWADPath", string.Empty);

        // load persisted executables (if any)
        var saved = _executablesConfig.Get();
        foreach (var e in saved)
        {
            _manager.Executables.Add(e);
            Executables.Add(e);
            // Listen for property changes on this item
            e.PropertyChanged += (_, _) => SaveExecutablesConfig();
        }

        RemoveSelectedExecutableCommand = new RelayCommand(_ => RemoveSelectedExecutable(), _ => SelectedExecutable != null);
        AddExecutablesCommand = new RelayCommand(p => { _ = AddExecutablesFromWindowAsync(p); });

        AllWads.Clear();

        // Load WAD list once at startup
        var wads = WadLists.GetAllWads();
        foreach (var wad in wads)
        {
            var wadInfo = WadLists.GetWadInfo(wad);
            if(!wadInfo.IsPatch){
                IWADs.Add(new(){
                    Path = wad,
                    DisplayName = wadInfo.Name
                });
            }else{
                var info = wadInfo.Info;
                AllWads.Add(info);
                FilteredAvailableWads.Add(info);
                // Listen for IsSelected changes to move between collections
                info.PropertyChanged += (_, args) =>
                {
                    if (args.PropertyName == nameof(SelectWadInfo.IsSelected))
                    {
                        OnWadSelectionChanged(info);
                    }
                };
            }
        }

        // Persist on collection changes (add/remove)
        Executables.CollectionChanged += (_, args) =>
        {
            if (args.NewItems != null)
            {
                // Listen for property changes on newly added items
                foreach (ExecutableItem item in args.NewItems)
                {
                    item.PropertyChanged += (_, _) => SaveExecutablesConfig();
                }
            }
            SaveExecutablesConfig();
        };

        // load persisted profiles (if any)
        var savedProfiles = _profilesConfig.Get();
        foreach (var p in savedProfiles)
        {
            Profiles.Add(p);
            p.PropertyChanged += (_, _) => 
            {
                TrySaveProfiles();
            };
        }

        // Persist profiles on collection changes
        Profiles.CollectionChanged += (_, args) =>
        {
            try
            {   
                if(args.NewItems != null){
                    foreach (Profile p in args.NewItems)
                    {
                        p.PropertyChanged += (_, _) => 
                        {
                            TrySaveProfiles();
                        };
                    }
                }
                _profilesConfig.Set(Profiles.ToList());
            }
            catch
            {
                // ignore save errors
            }
        };
        
        // Ensure we have selected the appropriate last-used items
        if (_lastSelectedExecutablePathConfig.HasSet())
        {
            var lastPath = _lastSelectedExecutablePathConfig.Get();
            SelectedExecutable = Executables.FirstOrDefault(e => e.Path == lastPath);
        }
        if (_lastSelectedIWADPathConfig.HasSet())
        {
            var lastPath = _lastSelectedIWADPathConfig.Get();
            SelectedIWAD = IWADs.FirstOrDefault(i => i.Path == lastPath);
        }
    }

    public ObservableCollection<ExecutableItem> Executables { get; } = new ObservableCollection<ExecutableItem>();

    public ObservableCollection<IWad> IWADs { get; } = new ObservableCollection<IWad>();

    public ObservableCollection<Profile> Profiles { get; } = new ObservableCollection<Profile>();

    // Available WADs (IsSelected = false)
    public static List<SelectWadInfo> AllWads { get; } = new List<SelectWadInfo>();
    public ObservableCollection<SelectWadInfo> FilteredAvailableWads { get; } = new ObservableCollection<SelectWadInfo>();
    public ObservableCollection<SelectWadInfo> SelectedItemsAvailableWads { get; } = new ObservableCollection<SelectWadInfo>();

    // Selected WADs (IsSelected = true)
    public ObservableCollection<SelectWadInfo> SelectedWads { get; } = new ObservableCollection<SelectWadInfo>();
    public ObservableCollection<SelectWadInfo> FilteredSelectedWads { get; } = new ObservableCollection<SelectWadInfo>();
    public ObservableCollection<SelectWadInfo> SelectedItemsSelectedWads { get; } = new ObservableCollection<SelectWadInfo>();

    private ExecutableItem? _selectedExecutable;
    public ExecutableItem? SelectedExecutable
    {
        get => _selectedExecutable;
        set
        {
            _lastSelectedExecutablePathConfig.Set(value?.Path ?? string.Empty);
            if(SelectedProfile is not null){
                SelectedProfile.PreferredExecutable = value?.Path ?? string.Empty;
            }
            if (SetProperty(ref _selectedExecutable, value))
            {
                (RemoveSelectedExecutableCommand as RelayCommand)?.RaiseCanExecuteChanged();
            }
        }
    }

    private IWad? _selectedIWad;
    public IWad? SelectedIWAD
    {
        get => _selectedIWad;
        set
        {
            if(SelectedProfile is not null){
                SelectedProfile.PreferredIWAD = value?.Path ?? string.Empty;
            }
            _lastSelectedIWADPathConfig.Set(value?.Path ?? string.Empty);
            SetProperty(ref _selectedIWad, value);
        }
    }

    private static Profile? SELECTED_PROFILE; // Dirty hack. b/c I cannot be arsed trying to sync this.

    private Profile? _selectedProfile;
    public Profile? SelectedProfile
    {
        get => _selectedProfile;
        set {
            // Enable the Checkbox if value is true.
            var mainWindow = Avalonia.Application.Current?.ApplicationLifetime is Avalonia.Controls.ApplicationLifetimes.IClassicDesktopStyleApplicationLifetime desktop
                ? desktop.MainWindow
                : null;
            if (mainWindow != null){
                var checkbox = mainWindow.Find<CheckBox>("ProfileLockedCheckbox");
                if(checkbox is not null)
                    checkbox.IsEnabled = value is not null;
            }
            if (value != null){
                // Set the appropriate settings from the profile.
                foreach(var wad in AllWads)
                    wad.IsSelected = value.SelectedWads.Contains(wad.Path);
                // Try and set the preferred executable
                if(!string.IsNullOrWhiteSpace(value.PreferredExecutable)){
                    var executable = Executables.FirstOrDefault(c=>c.Path == value.PreferredExecutable);
                    if(executable is null){
                        Console.Error.WriteLine($"Failed to find executable for profile ${value.PreferredExecutable}");
                    }else{
                        SelectedExecutable = executable;
                    }
                }

                if(!string.IsNullOrWhiteSpace(value.PreferredIWAD)){
                    var iwad = IWADs.FirstOrDefault(c=>c.Path == value.PreferredIWAD);
                    if(iwad is null){
                        Console.Error.WriteLine($"Failed to find executable for profile ${value.PreferredIWAD}");
                    }else{
                        SelectedIWAD = iwad;
                    }
                }
            }
            SELECTED_PROFILE = value;
            SetProperty(ref _selectedProfile, value);
        }
    }

    public ICommand RemoveSelectedExecutableCommand { get; }
    public ICommand AddExecutablesCommand { get; }
    public ICommand RunGameCommand => new RelayCommand(_ => RunGame(), _ => SelectedExecutable != null && SelectedIWAD != null);

    public bool CurrentProfileLocked {
        get => SelectedProfile?.Locked ?? false;
        set {
            if (SelectedProfile != null)
            {
                SelectedProfile.Locked = value;
                OnPropertyChanged(nameof(CurrentProfileLocked));
            }
        }
    }

    /// <summary>
    /// Add a set of file paths: validate them and add valid entries to the manager/observable collection.
    /// Returns list of invalid reasons for the caller to present to the user.
    /// </summary>
    public List<string> AddExecutables(IEnumerable<string> paths)
    {
        var (valid, invalids) = _manager.ValidatePaths(paths);
        _manager.AddValid(valid);

        foreach (var v in valid)
        {
            if (!Executables.Any(x => x.Path == v.Path))
                Executables.Add(v);
        }

        return invalids;
    }

    private async Task AddExecutablesFromWindowAsync(object? parameter)
    {
        // Expect the window as parameter so we can use StorageProvider
        if (parameter is not Window win)
            return;

        try
        {
            var provider = win.StorageProvider;
            if (provider == null || !provider.CanOpen)
                return;

            var options = new FilePickerOpenOptions { Title = "Select executable(s)", AllowMultiple = true };
            var files = await provider.OpenFilePickerAsync(options);
            if (files == null)
                return;

            var paths = new List<string>();
            foreach (var f in files)
            {
                try
                {
                    var uri = f.Path;
                    paths.Add(uri.IsAbsoluteUri ? uri.LocalPath : uri.ToString());
                }
                finally
                {
                    f.Dispose();
                }
            }

            var invalids = AddExecutables(paths);

            if (invalids.Count > 0)
            {
                // Show a simple modal dialog from the UI window
                var dlg = new Window
                {
                    Title = "Invalid selections",
                    Width = 500,
                    Height = 300,
                    Content = new ScrollViewer
                    {
                        Content = new StackPanel
                        {
                            Margin = new Avalonia.Thickness(10),
                            Children =
                            {
                                new TextBlock { Text = "The following selections are not valid executables:", FontWeight = Avalonia.Media.FontWeight.Bold, Margin = new Avalonia.Thickness(0,0,0,8) }
                            }
                        }
                    }
                };

                var stack = ((dlg.Content as ScrollViewer)!.Content as StackPanel)!;
                foreach (var s in invalids)
                    stack.Children.Add(new TextBlock { Text = s, TextWrapping = Avalonia.Media.TextWrapping.Wrap });

                var ok = new Button { Content = "OK", HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Right, Margin = new Avalonia.Thickness(0,8,0,0) };
                ok.Click += (_, _) => dlg.Close();
                stack.Children.Add(ok);

                await dlg.ShowDialog(win);
            }
        }
        catch
        {
            // ignore UI errors
        }
    }

    private void SaveExecutablesConfig()
    {
        try
        {
            _executablesConfig.Set(Executables.ToList());
        }
        catch
        {
            // ignore save errors
        }
    }

    private void TrySaveProfiles()
    {
        try
        {
            _profilesConfig.Set(Profiles.ToList());
        }
        catch
        {
            // ignore save errors
        }
    }

    private void RemoveSelectedExecutable()
    {
        if (SelectedExecutable == null)
            return;

        if (_manager.Remove(SelectedExecutable))
        {
            Executables.Remove(SelectedExecutable);
            SelectedExecutable = null;
        }
    }

    // Add wads directly from a provided selection (avoids relying on a synced SelectedItems collection)
    public void AddWadsFromItems(IEnumerable<SelectWadInfo> items)
    {
        Console.WriteLine($"\n=== AddWadsFromItems() called (VM hash {this.GetHashCode()}) ===");
        var list = items?.ToList() ?? new List<SelectWadInfo>();
        Console.WriteLine($"Items passed: {list.Count}");
        foreach (var wadInfo in list)
        {
            Console.WriteLine($"Setting IsSelected=true for: {wadInfo.DisplayName}");
            wadInfo.IsSelected = true;
        }
        UpdateAvailableWadsFilter();
        UpdateSelectedWadsFilter();
        Console.WriteLine("=== AddWadsFromItems() done ===\n");
    }

    public void RemoveWadsFromItems(IEnumerable<SelectWadInfo> items)
    {
        var list = items?.ToList() ?? new List<SelectWadInfo>();
        foreach (var wadInfo in list)
        {
            wadInfo.IsSelected = false;
        }
        UpdateAvailableWadsFilter();
        UpdateSelectedWadsFilter();
    }

    public void OnWadSelectionChanged(SelectWadInfo wadInfo)
    {
        Console.WriteLine($"Wad selection changed: {wadInfo.DisplayName}, IsSelected={wadInfo.IsSelected}");
        if (wadInfo.IsSelected)
        {
            // Move from Available to Selected
            FilteredAvailableWads.Remove(wadInfo);
            FilteredSelectedWads.Add(wadInfo);
            if(SelectedProfile is not null){
                Console.WriteLine("<== ADDING WAD TO PROFILE ==>");
                if(!SelectedProfile.SelectedWads.Contains(wadInfo.Path))
                    SelectedProfile.SelectedWads.Add(wadInfo.Path);
                TrySaveProfiles();
            }
        }
        else
        {
            // Move from Selected to Available
            FilteredSelectedWads.Remove(wadInfo);
            FilteredAvailableWads.Add(wadInfo);
            if(SelectedProfile is not null){
                Console.WriteLine("<== REMOVING WAD FROM PROFILE ==>");
                if(SelectedProfile.SelectedWads.Contains(wadInfo.Path)){
                    Console.WriteLine("FOUND");
                    SelectedProfile.SelectedWads.Remove(wadInfo.Path);
                }
                TrySaveProfiles();
            }
        }
    }

    public void UpdateAvailableWadsFilter(string? filterText = null)
    {
        if (filterText == null) // Find the FilterText from the search box if not provided
        {
            Console.WriteLine("UpdateAvailableWadsFilter: filterText is null, trying to get from TextBox");
            // How the hell do I get the TextBox value from here? Ugh.
            // Step 1. Get the main window
            var mainWindow = Avalonia.Application.Current?.ApplicationLifetime is Avalonia.Controls.ApplicationLifetimes.IClassicDesktopStyleApplicationLifetime desktop
                ? desktop.MainWindow
                : null;
            if (mainWindow != null){
                Console.WriteLine("Main window found");
                // Step 2. Find the WADFilterTextBox1
                var filterBox = mainWindow.FindControl<TextBox>("WADFilterTextBox1");
                if (filterBox != null){
                    Console.WriteLine("Filter TextBox found");
                    filterText = filterBox.Text;
                }
            }
        }
        FilteredAvailableWads.Clear();
        var availableWads = AllWads.Where(w => !w.IsSelected);
        var filtered = string.IsNullOrWhiteSpace(filterText)
            ? availableWads
            : availableWads.Where(w => w.DisplayName.IndexOf(filterText, StringComparison.OrdinalIgnoreCase) >= 0 ||
                                        w.Filename.IndexOf(filterText, StringComparison.OrdinalIgnoreCase) >= 0);

        foreach (var wad in filtered)
        {
            FilteredAvailableWads.Add(wad);
        }
    }

    public void UpdateSelectedWadsFilter(string? filterText = null)
    {
        if (filterText == null) // Find the FilterText from the search box if not provided
        {
            // Step 1. Get the main window
            var mainWindow = Avalonia.Application.Current?.ApplicationLifetime is Avalonia.Controls.ApplicationLifetimes.IClassicDesktopStyleApplicationLifetime desktop
                ? desktop.MainWindow
                : null;
            if (mainWindow != null){
                // Step 2. Find the WADFilterTextBox2
                var filterBox = mainWindow.FindControl<TextBox>("WADFilterTextBox2");
                if (filterBox != null){
                    filterText = filterBox.Text;
                }
            }
        }
        FilteredSelectedWads.Clear();
        var selectedWads = AllWads.Where(w => w.IsSelected);
        var filtered = string.IsNullOrWhiteSpace(filterText)
            ? selectedWads
            : selectedWads.Where(w => w.DisplayName.IndexOf(filterText, StringComparison.OrdinalIgnoreCase) >= 0 ||
                                       w.Filename.IndexOf(filterText, StringComparison.OrdinalIgnoreCase) >= 0);

        foreach (var wad in filtered)
        {
            FilteredSelectedWads.Add(wad);
        }
    }

    private void ShowFailDialogue(string failReason){
        // Show a simple modal dialog from the UI window
        var dlg = new Window
        {
            Title = "Cannot run game",
            Width = 400,
            SizeToContent = SizeToContent.Height,
            Content = new StackPanel
            {
                Margin = new Avalonia.Thickness(10),
                Children =
                {
                    new TextBlock { Text = "The game cannot be started for the following reason:", FontWeight = Avalonia.Media.FontWeight.Bold, Margin = new Avalonia.Thickness(0,0,0,8) },
                    new TextBlock { Text = failReason, TextWrapping = Avalonia.Media.TextWrapping.Wrap, Margin = new Avalonia.Thickness(0,0,0,8) },
                    new Button { Content = "OK", HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Right }
                }
            }
        };

        var ok = (dlg.Content as StackPanel)!.Children.OfType<Button>().First();
        ok.Click += (_, _) => dlg.Close();

        // Find the main window to show dialog over
        var mainWindow = Avalonia.Application.Current?.ApplicationLifetime is Avalonia.Controls.ApplicationLifetimes.IClassicDesktopStyleApplicationLifetime desktop
            ? desktop.MainWindow
            : null;
        if (mainWindow != null)
        {
            dlg.ShowDialog(mainWindow);
        }
    }

    public Task<bool> ShowConfirmDialogue(string title, string text){
        TaskCompletionSource<bool> tcs = new();
        var okButton = new Button { Content = "OK", Width = 80, Margin = new Thickness(0,0,8,0) };
        var cancelButton = new Button { Content = "Cancel", Width = 80 };

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
                            okButton,
                            cancelButton
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
        cancelButton.Click += (_, _) =>
        {
            tcs.SetResult(false);
            dlg.Close();
        };
        dlg.Show();

        return tcs.Task;
    }

    public async void RunGame()
    {
        if (SelectedExecutable == null)
        {
            ShowFailDialogue("No executable selected.");
            return;
        }
        if (SelectedIWAD == null)
        {
            ShowFailDialogue("No IWAD selected.");
            return;
        }

        // Check that a ./saves folder exists, create it otherwise.
        if(!Directory.Exists("./saves")){
            try {
                Directory.CreateDirectory("./saves");
            }catch{
                ShowFailDialogue("Failed to create new saves folder!");
                return;
            }
        }

        var saveFolder = "_unsorted";
        if(SELECTED_PROFILE is not null){
            saveFolder = SELECTED_PROFILE.Name;
            foreach(var c in Path.GetInvalidFileNameChars())
                saveFolder = saveFolder.Replace(c, '-');
        }else{
            if (!await ShowConfirmDialogue("Run Game", "You have not selected a profile! Setting a profile ensures seperate tracking of saves and modlists. Are you sure you wish to proceed?")){
                return;
            }
        }

        if (!Directory.Exists($"./saves/{saveFolder}")){
            try {
                Directory.CreateDirectory($"./saves/{saveFolder}");
            }catch{
                ShowFailDialogue($"Failed to create new save folder: ./saves/{saveFolder}!");
                return;
            }
        }

        try
        {
            // Build command line arguments
            string args = $"-iwad \"{SelectedIWAD.Path}\"";
            var selected = AllWads.Where(w => w.IsSelected).ToList();
            if(selected.Count > 0){
                args += $" -file {string.Join(" ", selected.Select(w => $"\"{w.Path}\""))}";
            }else{
                // No WADs selected, warn and abort
                ShowFailDialogue("No WADs selected to load.");
                return;
            }
            saveFolder = Path.GetFullPath($"./saves/{saveFolder}");
            args += $" -savedir \"{saveFolder}\"";
            Console.WriteLine($"Running game: {SelectedExecutable.Path} {args}");
            // TODO: Custom savedir with profiles.
            var cmd = new ProcessStartInfo
            {
                FileName = SelectedExecutable.Path,
                Arguments = string.Join(" ", args),
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true
            };
            var proc = Process.Start(cmd);
            if(proc == null){
                ShowFailDialogue("Failed to start the game process.");
                return;
            }
            // Redirect output to console for now
            proc.OutputDataReceived += (sender, e) => { if (e.Data != null) Console.WriteLine(e.Data); };
            proc.BeginOutputReadLine();
            proc.ErrorDataReceived += (sender, e) => { if (e.Data != null) Console.WriteLine($"ERR: {e.Data}"); };
            proc.BeginErrorReadLine();

        }
        catch (Exception ex)
        {
            ShowFailDialogue($"An error occurred while trying to run the game: {ex.Message}");
        }

    }
}
