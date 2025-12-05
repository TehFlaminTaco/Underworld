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

/// <summary>
/// Main view model for the Underworld launcher application.
/// Manages executables, IWADs, PWADs, profiles, and game launching.
/// </summary>
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
        ExportProfileCommand = new RelayCommand(p => { _ = ExportProfileAsync(p); }, _ => SelectedProfile != null);
        ImportProfileCommand = new RelayCommand(p => { _ = ImportProfileAsync(p); });

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

    /// <summary>
    /// Gets the collection of available game executables.
    /// </summary>
    public ObservableCollection<ExecutableItem> Executables { get; } = new ObservableCollection<ExecutableItem>();

    /// <summary>
    /// Gets the collection of available IWADs (main game data files).
    /// </summary>
    public ObservableCollection<IWad> IWADs { get; } = new ObservableCollection<IWad>();

    /// <summary>
    /// Gets the collection of saved game profiles.
    /// </summary>
    public ObservableCollection<Profile> Profiles { get; } = new ObservableCollection<Profile>();

    /// <summary>
    /// Gets the master list of all discovered WAD files.
    /// </summary>
    public static List<SelectWadInfo> AllWads { get; } = new List<SelectWadInfo>();

    /// <summary>
    /// Gets the filtered collection of available (unselected) WADs.
    /// </summary>
    public ObservableCollection<SelectWadInfo> FilteredAvailableWads { get; } = new ObservableCollection<SelectWadInfo>();

    /// <summary>
    /// Gets the collection of items selected in the available WADs list.
    /// </summary>
    public ObservableCollection<SelectWadInfo> SelectedItemsAvailableWads { get; } = new ObservableCollection<SelectWadInfo>();

    /// <summary>
    /// Gets the collection of selected WADs.
    /// </summary>
    public ObservableCollection<SelectWadInfo> SelectedWads { get; } = new ObservableCollection<SelectWadInfo>();

    /// <summary>
    /// Gets the filtered collection of selected WADs.
    /// </summary>
    public ObservableCollection<SelectWadInfo> FilteredSelectedWads { get; } = new ObservableCollection<SelectWadInfo>();

    /// <summary>
    /// Gets the collection of items selected in the selected WADs list.
    /// </summary>
    public ObservableCollection<SelectWadInfo> SelectedItemsSelectedWads { get; } = new ObservableCollection<SelectWadInfo>();


    private static ExecutableItem? SELECTED_EXECUTABLE;
    private ExecutableItem? _selectedExecutable;

    /// <summary>
    /// Gets or sets the currently selected game executable.
    /// Updates the last selected path in config and the current profile's preferred executable.
    /// </summary>
    public ExecutableItem? SelectedExecutable
    {
        get => _selectedExecutable;
        set
        {
            SELECTED_EXECUTABLE = value;
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

    private static IWad? SELECTED_IWAD;
    private IWad? _selectedIWad;

    /// <summary>
    /// Gets or sets the currently selected IWAD.
    /// Updates the last selected path in config and the current profile's preferred IWAD.
    /// </summary>
    public IWad? SelectedIWAD
    {
        get => _selectedIWad;
        set
        {
            SELECTED_IWAD = value;
            if(SelectedProfile is not null){
                SelectedProfile.PreferredIWAD = value?.Path ?? string.Empty;
            }
            _lastSelectedIWADPathConfig.Set(value?.Path ?? string.Empty);
            SetProperty(ref _selectedIWad, value);
        }
    }

    private static string COMMAND_LINE_ARGUMENTS = string.Empty;
    private string _commandLineArguments = string.Empty;

    /// <summary>
    /// Gets or sets the command line arguments for the game.
    /// These are saved per profile and can be referenced when launching the game.
    /// </summary>
    public string CommandLineArguments
    {
        get => _commandLineArguments;
        set
        {
            COMMAND_LINE_ARGUMENTS = value;
            if (SelectedProfile is not null)
            {
                SelectedProfile.CommandLineArguments = value;
            }
            SetProperty(ref _commandLineArguments, value);
        }
    }

    private static Profile? SELECTED_PROFILE;
    private Profile? _selectedProfile;

    /// <summary>
    /// Gets or sets the currently selected profile.
    /// When set, applies the profile's WAD selection, preferred executable, and preferred IWAD.
    /// </summary>
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
            _selectedProfile = null; // Unset for the duration.
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

                // Restore command line arguments
                _commandLineArguments = value.CommandLineArguments;
                COMMAND_LINE_ARGUMENTS = value.CommandLineArguments;
                OnPropertyChanged(nameof(CommandLineArguments));
            }
            SELECTED_PROFILE = value;
            SetProperty(ref _selectedProfile, value);
            OnPropertyChanged(nameof(CurrentProfileLocked));
        }
    }

    /// <summary>
    /// Gets the command to remove the currently selected executable.
    /// </summary>
    public ICommand RemoveSelectedExecutableCommand { get; }

    /// <summary>
    /// Gets the command to add new executables via file picker.
    /// </summary>
    public ICommand AddExecutablesCommand { get; }

    /// <summary>
    /// Gets the command to launch the game.
    /// Enabled only when both an executable and IWAD are selected.
    /// </summary>
    public ICommand RunGameCommand => new RelayCommand(_ => RunGame(), _ => SelectedExecutable != null && SelectedIWAD != null);

    /// <summary>
    /// Gets the command to export the current profile to a JSON file.
    /// </summary>
    public ICommand ExportProfileCommand { get; }

    /// <summary>
    /// Gets the command to import a profile from a JSON file.
    /// </summary>
    public ICommand ImportProfileCommand { get; }

    /// <summary>
    /// Gets or sets whether the current profile is locked.
    /// A locked profile prevents accidental modification.
    /// </summary>
    public bool CurrentProfileLocked
    {
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
    /// Validates and adds executable file paths to the manager and observable collection.
    /// </summary>
    /// <param name="paths">The file paths to validate and add.</param>
    /// <returns>A list of error messages for invalid paths.</returns>
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

    private async Task ExportProfileAsync(object? parameter)
    {
        if (parameter is not Window win)
            return;

        if (SelectedProfile == null)
            return;

        try
        {
            var provider = win.StorageProvider;
            if (provider == null || !provider.CanSave)
                return;

            var options = new FilePickerSaveOptions
            {
                Title = "Export Profile",
                SuggestedFileName = $"{SelectedProfile.Name}.uw-profile.json",
                DefaultExtension = "json",
                FileTypeChoices = new[]
                {
                    new FilePickerFileType("Underworld Profile")
                    {
                        Patterns = new[] { "*.uw-profile.json" }
                    }
                }
            };

            var file = await provider.SaveFilePickerAsync(options);
            if (file == null)
                return;

            var exportProfile = ExportProfile.From(SelectedProfile);
            var json = System.Text.Json.JsonSerializer.Serialize(exportProfile, new System.Text.Json.JsonSerializerOptions { WriteIndented = true });
            
            await using var stream = await file.OpenWriteAsync();
            await using var writer = new System.IO.StreamWriter(stream);
            await writer.WriteAsync(json);
        }
        catch (Exception ex)
        {
            ShowFailDialogue($"Failed to export profile: {ex.Message}");
        }
    }

    private async Task ImportProfileAsync(object? parameter)
    {
        if (parameter is not Window win)
            return;

        try
        {
            var provider = win.StorageProvider;
            if (provider == null || !provider.CanOpen)
                return;

            var options = new FilePickerOpenOptions
            {
                Title = "Import Profile",
                AllowMultiple = false,
                FileTypeFilter = new[]
                {
                    new FilePickerFileType("Underworld Profile")
                    {
                        Patterns = new[] { "*.uw-profile.json" }
                    }
                }
            };

            var files = await provider.OpenFilePickerAsync(options);
            if (files == null || files.Count == 0)
                return;

            var file = files[0];
            try
            {
                await using var stream = await file.OpenReadAsync();
                using var reader = new System.IO.StreamReader(stream);
                var json = await reader.ReadToEndAsync();

                var exportProfile = System.Text.Json.JsonSerializer.Deserialize<ExportProfile>(json);
                if (exportProfile == null)
                {
                    ShowFailDialogue("Failed to parse profile file.");
                    return;
                }

                var (profile, failReason) = exportProfile.To();
                if (profile == null)
                {
                    ShowFailDialogue($"Failed to import profile: {failReason}");
                    return;
                }

                // Set imported profiles as locked by default
                profile.Locked = true;

                // Check if profile with same name already exists
                if (Profiles.Any(p => p.Name == profile.Name))
                {
                    var overwrite = await ShowConfirmDialogue(
                        "Profile Exists",
                        $"A profile named '{profile.Name}' already exists. Overwrite?",
                        "Overwrite",
                        "Cancel"
                    );

                    if (!overwrite)
                        return;

                    var existing = Profiles.First(p => p.Name == profile.Name);
                    Profiles.Remove(existing);
                }

                Profiles.Add(profile);
                SelectedProfile = profile;
            }
            finally
            {
                file.Dispose();
            }
        }
        catch (Exception ex)
        {
            ShowFailDialogue($"Failed to import profile: {ex.Message}");
        }
    }

    /// <summary>
    /// Persists the current list of executables to configuration.
    /// Silently ignores save errors.
    /// </summary>
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

    /// <summary>
    /// Attempts to persist the current list of profiles to configuration.
    /// Silently ignores save errors.
    /// </summary>
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

    /// <summary>
    /// Removes the currently selected executable from the manager and collection.
    /// </summary>
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

    /// <summary>
    /// Marks the provided WADs as selected and updates the filtered collections.
    /// This avoids relying on synced SelectedItems collections.
    /// If the current profile is locked, this operation is blocked.
    /// </summary>
    /// <param name="items">The WADs to mark as selected.</param>
    public void AddWadsFromItems(IEnumerable<SelectWadInfo> items)
    {
        Console.WriteLine($"\n=== AddWadsFromItems() called (VM hash {this.GetHashCode()}) ===");
        
        // Block if profile is locked
        if (SelectedProfile?.Locked == true)
        {
            Console.WriteLine("AddWadsFromItems blocked: Profile is locked");
            return;
        }
        
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

    /// <summary>
    /// Marks the provided WADs as unselected and updates the filtered collections.
    /// If the current profile is locked, this operation is blocked.
    /// </summary>
    /// <param name="items">The WADs to mark as unselected.</param>
    public void RemoveWadsFromItems(IEnumerable<SelectWadInfo> items)
    {
        // Block if profile is locked
        if (SelectedProfile?.Locked == true)
        {
            Console.WriteLine("RemoveWadsFromItems blocked: Profile is locked");
            return;
        }
        
        var list = items?.ToList() ?? new List<SelectWadInfo>();
        foreach (var wadInfo in list)
        {
            wadInfo.IsSelected = false;
        }
        UpdateAvailableWadsFilter();
        UpdateSelectedWadsFilter();
    }

    /// <summary>
    /// Handles changes to WAD selection status, moving WADs between available and selected collections
    /// and updating the selected profile if one is active.
    /// </summary>
    /// <param name="wadInfo">The WAD whose selection status changed.</param>
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

    /// <summary>
    /// Updates the filtered list of available (unselected) WADs based on the filter text.
    /// If filterText is not provided, attempts to retrieve it from the WADFilterTextBox1 control.
    /// </summary>
    /// <param name="filterText">Optional filter text to search for in WAD names and filenames.</param>
    public void UpdateAvailableWadsFilter(string? filterText = null)
    {
        if (filterText == null)
        {
            Console.WriteLine("UpdateAvailableWadsFilter: filterText is null, trying to get from TextBox");
            filterText = GetFilterTextFromControl("WADFilterTextBox1");
        }
        FilteredAvailableWads.Clear();
        var availableWads = AllWads.Where(w => !w.IsSelected);
        var filtered = string.IsNullOrWhiteSpace(filterText)
            ? availableWads
            : availableWads.Where(w => w.DisplayName.IndexOf(filterText!, StringComparison.OrdinalIgnoreCase) >= 0 ||
                                        w.Filename.IndexOf(filterText!, StringComparison.OrdinalIgnoreCase) >= 0);

        foreach (var wad in filtered.ToList())
        {
            FilteredAvailableWads.Add(wad);
        }
    }

    /// <summary>
    /// Updates the filtered list of selected WADs based on the filter text.
    /// If filterText is not provided, attempts to retrieve it from the WADFilterTextBox2 control.
    /// </summary>
    /// <param name="filterText">Optional filter text to search for in WAD names and filenames.</param>
    public void UpdateSelectedWadsFilter(string? filterText = null)
    {
        if (filterText == null)
        {
            filterText = GetFilterTextFromControl("WADFilterTextBox2");
        }
        FilteredSelectedWads.Clear();
        var selectedWads = AllWads.Where(w => w.IsSelected);
        var filtered = string.IsNullOrWhiteSpace(filterText)
            ? selectedWads
            : selectedWads.Where(w => w.DisplayName.IndexOf(filterText!, StringComparison.OrdinalIgnoreCase) >= 0 ||
                                       w.Filename.IndexOf(filterText!, StringComparison.OrdinalIgnoreCase) >= 0);

        foreach (var wad in filtered)
        {
            FilteredSelectedWads.Add(wad);
        }
    }

    /// <summary>
    /// Shows an error dialog with the specified failure reason.
    /// </summary>
    /// <param name="failReason">The error message to display.</param>
    private void ShowFailDialogue(string failReason)
    {
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

        var mainWindow = GetMainWindow();
        if (mainWindow != null)
        {
            dlg.ShowDialog(mainWindow);
        }
    }

    /// <summary>
    /// Shows a confirmation dialog with customizable title, message, and button text.
    /// </summary>
    /// <param name="title">The dialog title.</param>
    /// <param name="text">The message to display.</param>
    /// <param name="okText">The text for the OK button.</param>
    /// <param name="cancelText">The text for the Cancel button.</param>
    /// <returns>True if OK was clicked, false if Cancel was clicked.</returns>
    public Task<bool> ShowConfirmDialogue(string title, string text, string okText = "OK", string cancelText = "Cancel")
    {
        TaskCompletionSource<bool> tcs = new();
        var okButton = new Button { Content = okText, Width = 80, Margin = new Thickness(0,0,8,0) };
        var cancelButton = new Button { Content = cancelText, Width = 80 };

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

    /// <summary>
    /// Gets the main application window.
    /// </summary>
    /// <returns>The main window, or null if not available.</returns>
    private Window? GetMainWindow()
    {
        return Avalonia.Application.Current?.ApplicationLifetime is Avalonia.Controls.ApplicationLifetimes.IClassicDesktopStyleApplicationLifetime desktop
            ? desktop.MainWindow
            : null;
    }

    /// <summary>
    /// Retrieves filter text from a named TextBox control in the main window.
    /// </summary>
    /// <param name="controlName">The name of the TextBox control.</param>
    /// <returns>The text from the control, or null if not found.</returns>
    private string? GetFilterTextFromControl(string controlName)
    {
        var mainWindow = GetMainWindow();
        if (mainWindow != null)
        {
            Console.WriteLine("Main window found");
            var filterBox = mainWindow.FindControl<TextBox>(controlName);
            if (filterBox != null)
            {
                Console.WriteLine("Filter TextBox found");
                return filterBox.Text;
            }
        }
        return null;
    }

    /// <summary>
    /// Launches the game with the selected executable, IWAD, and WADs.
    /// Creates a save folder for the selected profile and builds appropriate command-line arguments.
    /// </summary>
    public async void RunGame()
    {
        if (SELECTED_EXECUTABLE == null)
        {
            ShowFailDialogue("No executable selected.");
            return;
        }
        if (SELECTED_IWAD == null)
        {
            ShowFailDialogue("No IWAD selected.");
            return;
        }

        if (!EnsureSavesDirectoryExists())
            return;

        var saveFolder = await DetermineSaveFolder();
        if (saveFolder == null)
            return;

        if (!CreateSaveFolderIfNeeded(saveFolder))
            return;

        var commandLineArgs = BuildGameCommandLineArgs(saveFolder);
        if (commandLineArgs == null)
            return;

        LaunchGameProcess(commandLineArgs);
    }

    /// <summary>
    /// Ensures the ./saves directory exists, creating it if necessary.
    /// </summary>
    /// <returns>True if the directory exists or was created successfully, false otherwise.</returns>
    private bool EnsureSavesDirectoryExists()
    {
        if (Directory.Exists("./saves"))
            return true;

        try
        {
            Directory.CreateDirectory("./saves");
            return true;
        }
        catch
        {
            ShowFailDialogue("Failed to create new saves folder!");
            return false;
        }
    }

    /// <summary>
    /// Determines the save folder name based on the selected profile.
    /// Prompts the user if no profile is selected.
    /// </summary>
    /// <returns>The save folder name, or null if the user cancelled.</returns>
    private async Task<string?> DetermineSaveFolder()
    {
        if (SELECTED_PROFILE is not null)
        {
            var saveFolder = SELECTED_PROFILE.Name;
            foreach (var c in Path.GetInvalidFileNameChars())
                saveFolder = saveFolder.Replace(c, '-');
            return saveFolder;
        }

        var proceed = await ShowConfirmDialogue(
            "Run Game",
            "You have not selected a profile! Setting a profile ensures seperate tracking of saves and modlists. Are you sure you wish to proceed?",
            "Yes",
            "Cancel");

        return proceed ? "_unsorted" : null;
    }

    /// <summary>
    /// Creates the save folder if it doesn't exist.
    /// </summary>
    /// <param name=\"saveFolder\">The save folder name (not full path).</param>
    /// <returns>True if the folder exists or was created successfully, false otherwise.</returns>
    private bool CreateSaveFolderIfNeeded(string saveFolder)
    {
        var folderPath = $"./saves/{saveFolder}";
        if (Directory.Exists(folderPath))
            return true;

        try
        {
            Directory.CreateDirectory(folderPath);
            return true;
        }
        catch
        {
            ShowFailDialogue($"Failed to create new save folder: {folderPath}!");
            return false;
        }
    }

    /// <summary>
    /// Builds the command-line arguments for launching the game.
    /// </summary>
    /// <param name=\"saveFolder\">The save folder name (not full path).</param>
    /// <returns>The complete command-line arguments string, or null if no WADs are selected.</returns>
    private string? BuildGameCommandLineArgs(string saveFolder)
    {
        string args = $"-iwad \"{SELECTED_IWAD!.Path}\"";

        var selected = AllWads.Where(w => w.IsSelected).ToList();
        if (selected.Count > 0)
        {
            args += $" -file {string.Join(" ", selected.Select(w => $"\"{w.Path}\""))}";
        }

        var fullSavePath = Path.GetFullPath($"./saves/{saveFolder}");
        args += $" -savedir \"{fullSavePath}\"";

        // Add custom command line arguments if present
        if (!string.IsNullOrWhiteSpace(COMMAND_LINE_ARGUMENTS))
        {
            args += $" {COMMAND_LINE_ARGUMENTS}";
        }

        return args;
    }

    /// <summary>
    /// Launches the game process with the specified command-line arguments.
    /// </summary>
    /// <param name=\"commandLineArgs\">The command-line arguments to pass to the executable.</param>
    private void LaunchGameProcess(string commandLineArgs)
    {
        try
        {
            Console.WriteLine($"Running game: {SELECTED_EXECUTABLE!.Path} {commandLineArgs}");

            var cmd = new ProcessStartInfo
            {
                FileName = SELECTED_EXECUTABLE.Path,
                Arguments = commandLineArgs,
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true
            };

            var proc = Process.Start(cmd);
            if (proc == null)
            {            
                ShowFailDialogue("Failed to start the game process.");
                return;
            }

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
