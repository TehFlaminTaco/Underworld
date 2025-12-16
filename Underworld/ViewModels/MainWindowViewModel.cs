using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Windows.Input;
using Underworld.Models;
using System.Collections.Generic;
using System.ComponentModel;
using Avalonia.Controls;
using Avalonia.Platform.Storage;
using System.Threading.Tasks;
using System.Diagnostics;
using Underworld.Views;
using System.IO;
using Avalonia;
using Avalonia.Layout;
using Avalonia.Threading;

namespace Underworld.ViewModels;

/// <summary>
/// Main view model for the Underworld launcher application.
/// Manages executables, IWADs, PWADs, profiles, and game launching.
/// </summary>
public partial class MainWindowViewModel : ViewModelBase, IDisposable
{
    private readonly ExecutableManager _manager = new ExecutableManager();
    private readonly ConfigEntry<List<ExecutableItem>> _executablesConfig;
    private readonly ConfigEntry<List<Profile>> _profilesConfig;
    private readonly ConfigEntry<MiniLauncherOptions> _miniLauncherDefaultsConfig;

    private readonly ConfigEntry<string> _lastSelectedExecutablePathConfig;
    private readonly ConfigEntry<string> _lastSelectedIWADPathConfig;

    public MainWindowViewModel()
    {
        _executablesConfig = Config.Setup("executables", new List<ExecutableItem>());
        _profilesConfig = Config.Setup("profiles", new List<Profile>());
        _miniLauncherDefaultsConfig = Config.Setup("miniLauncherDefaults", new MiniLauncherOptions());
        _lastSelectedExecutablePathConfig = Config.Setup("lastSelectedExecutablePath", string.Empty);
        _lastSelectedIWADPathConfig = Config.Setup("lastSelectedIWADPath", string.Empty);

        var saved = _executablesConfig.Get();
        foreach (var e in saved)
        {
            _manager.Executables.Add(e);
            Executables.Add(e);
            e.PropertyChanged += (_, _) => SaveExecutablesConfig();
        }

        RemoveSelectedExecutableCommand = new RelayCommand(_ => RemoveSelectedExecutable(), _ => SelectedExecutable != null);
        AddExecutablesCommand = new RelayCommand(p => { _ = AddExecutablesFromWindowAsync(p); });
        ExportProfileCommand = new RelayCommand(p => { _ = ExportProfileAsync(p); }, _ => SelectedProfile != null);
        ImportProfileCommand = new RelayCommand(p => { _ = ImportProfileAsync(p); });
        PreferencesCommand = new RelayCommand(_ => OpenPreferences());
        SetDarkThemeCommand = new RelayCommand(_ =>
        {
            Console.WriteLine("SetDarkThemeCommand executed!");
            ThemeManager.SetTheme("dark");
            Console.WriteLine($"Current theme: {ThemeManager.CurrentTheme.Id}");
        });
        SetLightThemeCommand = new RelayCommand(_ =>
        {
            Console.WriteLine("SetLightThemeCommand executed!");
            ThemeManager.SetTheme("light");
            Console.WriteLine($"Current theme: {ThemeManager.CurrentTheme.Id}");
        });

        ReloadWadsFromDisk(preserveSelections: false);

        Executables.CollectionChanged += (_, args) =>
        {
            if (args.NewItems != null)
            {
                foreach (ExecutableItem item in args.NewItems)
                {
                    item.PropertyChanged += (_, _) => SaveExecutablesConfig();
                }
            }

            SaveExecutablesConfig();
        };

        var savedProfiles = _profilesConfig.Get();
        foreach (var p in savedProfiles)
        {
            Profiles.Add(p);
            p.PropertyChanged += (_, _) => { TrySaveProfiles(); };
        }

        Profiles.CollectionChanged += (_, args) =>
        {
            try
            {
                if (args.NewItems != null)
                {
                    foreach (Profile p in args.NewItems)
                    {
                        p.PropertyChanged += (_, _) => { TrySaveProfiles(); };
                    }
                }

                _profilesConfig.Set(Profiles.ToList());
            }
            catch
            {
                // ignore save errors
            }
        };

        WadDirectoryWatcher.WadFilesChanged += OnWadFilesChanged;
        WadDirectoryWatcher.EnsureWatching();

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

        UserPreferencesChanged += OnUserPreferencesChanged;
    }

    public ObservableCollection<ExecutableItem> Executables { get; } = new ObservableCollection<ExecutableItem>();

    public ObservableCollection<IWad> IWADs { get; } = new ObservableCollection<IWad>();

    public ObservableCollection<Profile> Profiles { get; } = new ObservableCollection<Profile>();

    public static List<SelectWadInfo> AllWads { get; } = new List<SelectWadInfo>();

    public ObservableCollection<SelectWadInfo> FilteredAvailableWads { get; } = new ObservableCollection<SelectWadInfo>();

    public ObservableCollection<SelectWadInfo> SelectedItemsAvailableWads { get; } = new ObservableCollection<SelectWadInfo>();

    public ObservableCollection<SelectWadInfo> SelectedWads { get; } = new ObservableCollection<SelectWadInfo>();

    public ObservableCollection<SelectWadInfo> FilteredSelectedWads { get; } = new ObservableCollection<SelectWadInfo>();

    public ObservableCollection<SelectWadInfo> SelectedItemsSelectedWads { get; } = new ObservableCollection<SelectWadInfo>();

    private bool _suppressSelectionChanged;
    private readonly List<string> _manualSelectedWadOrder = new();

    private void OnUserPreferencesChanged(object? sender, EventArgs e)
    {
        OnPropertyChanged(nameof(PreferredRunButtonLabel));
        OnPropertyChanged(nameof(CanRunPreferredLaunch));
    }

    private void OnWadFilesChanged(object? sender, WadDirectoryChangedEventArgs e)
    {
        var paths = e?.ChangedPaths ?? Array.Empty<string>();

        void Apply()
        {
            ApplyWadDirectoryChanges(paths);
        }

        if (Dispatcher.UIThread.CheckAccess())
        {
            Apply();
        }
        else
        {
            Dispatcher.UIThread.Post(Apply);
        }
    }


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
                NotifyPreferredRunStateChanged();
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
            if (SetProperty(ref _selectedIWad, value))
            {
                NotifyPreferredRunStateChanged();
            }
        }
    }

    public static string COMMAND_LINE_ARGUMENTS { get; private set;} = string.Empty;
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

    public static Profile? SELECTED_PROFILE {get; private set;}
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
                        Console.Error.WriteLine($"Failed to find executable for profile {value.PreferredExecutable}");
                    }else{
                        SelectedExecutable = executable;
                    }
                }

                if(!string.IsNullOrWhiteSpace(value.PreferredIWAD)){
                    var iwad = IWADs.FirstOrDefault(c=>c.Path == value.PreferredIWAD);
                    if(iwad is null){
                        Console.Error.WriteLine($"Failed to find IWAD for profile {value.PreferredIWAD}");
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
            UpdateAvailableWadsFilter();
            UpdateSelectedWadsFilter();
            MirrorProfileOrderIntoManualList();
            NotifyPreferredRunStateChanged();
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
    public ICommand RunGameCommand => new RelayCommand(_ => RunGame(), _ => CanRunGame());

    /// <summary>
    /// Represents an item inside the Run Game flyout menu.
    /// </summary>
    public abstract record RunMenuEntry
    {
        public sealed record RunMenuCommand(string Header, Action Execute, Func<bool>? CanExecute = null) : RunMenuEntry;
        public sealed record RunMenuSeparator : RunMenuEntry;
    }

    public string PreferredRunButtonLabel => UserPreferences.PreferredLaunchMethod switch
    {
        UserPreferences.LaunchPreference.Run => "Run Game",
        UserPreferences.LaunchPreference.LoadLastSave => "Continue Game",
        UserPreferences.LaunchPreference.ShowMiniLauncher => "Show Mini-Launcher",
        _ => "Run Game"
    };

    public bool CanRunPreferredLaunch => UserPreferences.PreferredLaunchMethod switch
    {
        UserPreferences.LaunchPreference.Run => CanRunGame(),
        UserPreferences.LaunchPreference.LoadLastSave => CanContinueGame(),
        UserPreferences.LaunchPreference.ShowMiniLauncher => true,
        _ => CanRunGame()
    };

    public void ExecutePreferredLaunch()
    {
        switch (UserPreferences.PreferredLaunchMethod)
        {
            case UserPreferences.LaunchPreference.Run:
                RunGame();
                break;
            case UserPreferences.LaunchPreference.LoadLastSave:
                ContinueMostRecentGame();
                break;
            case UserPreferences.LaunchPreference.ShowMiniLauncher:
                OpenMiniLauncher();
                break;
            default:
                RunGame();
                break;
        }
    }

    /// <summary>
    /// Gets the command to export the current profile to a JSON file.
    /// </summary>
    public ICommand ExportProfileCommand { get; }

    /// <summary>
    /// Gets the command to import a profile from a JSON file.
    /// </summary>
    public ICommand ImportProfileCommand { get; }

    /// <summary>
    /// Gets the command to open the preferences dialog.
    /// </summary>
    public ICommand PreferencesCommand { get; }

    /// <summary>
    /// Gets the command to set the dark theme.
    /// </summary>
    public ICommand SetDarkThemeCommand { get; }

    /// <summary>
    /// Gets the command to set the light theme.
    /// </summary>
    public ICommand SetLightThemeCommand { get; }

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
    /// Attempts to reimport all invalid profiles using the export/import pipeline.
    /// </summary>
    public ProfileReimportReport ReimportInvalidProfiles()
    {
        var report = new ProfileReimportReport();
        var snapshot = Profiles.ToList();

        foreach (var profile in snapshot)
        {
            if (IsProfileValid(profile))
            {
                report.AlreadyValid++;
                continue;
            }

            Console.WriteLine($"[ProfileReimport] Profile '{profile.Name}' is invalid. Attempting reimport...");

            var exportModel = ExportProfile.From(profile);
            var (convertedProfile, failReason) = exportModel.To();
            if (convertedProfile == null)
            {
                var reason = failReason ?? "Unable to resolve profile components.";
                Console.WriteLine($"[ProfileReimport] Failed to reimport '{profile.Name}': {reason}");
                report.StillInvalid.Add($"{profile.Name}: {reason}");
                continue;
            }

            convertedProfile.Name = profile.Name;
            convertedProfile.Locked = profile.Locked;
            convertedProfile.CommandLineArguments = profile.CommandLineArguments;

            ApplyProfileConversion(profile, convertedProfile);

            if (IsProfileValid(profile))
            {
                report.SuccessfulReimports++;
                report.ReimportedProfiles.Add(profile.Name);
                Console.WriteLine($"[ProfileReimport] Successfully reimported '{profile.Name}'.");

                if (ReferenceEquals(profile, SelectedProfile))
                {
                    SelectedProfile = profile;
                }
            }
            else
            {
                var reason = "Validation failed after reimport.";
                Console.WriteLine($"[ProfileReimport] Profile '{profile.Name}' remains invalid after reimport.");
                report.StillInvalid.Add($"{profile.Name}: {reason}");
            }
        }

        if (report.SuccessfulReimports > 0)
        {
            TrySaveProfiles();
        }

        return report;
    }

    private static UserPreferences _userPreferences = UserPreferences.Load();
    public static event EventHandler? UserPreferencesChanged;

    public static UserPreferences UserPreferences
    {
        get => _userPreferences;
        set
        {
            _userPreferences = value ?? throw new ArgumentNullException(nameof(value));
            UserPreferencesChanged?.Invoke(null, EventArgs.Empty);
        }
    }
    private void OpenPreferences()
    {
        Console.WriteLine("[Preferences] Preferences command invoked (stub)");
        var window = Application.Current?.ApplicationLifetime is Avalonia.Controls.ApplicationLifetimes.IClassicDesktopStyleApplicationLifetime desktop
            ? desktop.MainWindow
            : null;
        if(window is not MainWindow mainWindow)return;
        UserPreferences.ShowPreferencesWindow(mainWindow);
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

    private static bool IsProfileValid(Profile profile)
    {
        var iwadValid = string.IsNullOrWhiteSpace(profile.PreferredIWAD) || File.Exists(profile.PreferredIWAD);
        var wadsValid = profile.SelectedWads.All(w => string.IsNullOrWhiteSpace(w) || File.Exists(w));
        return iwadValid && wadsValid;
    }

    private static void ApplyProfileConversion(Profile target, Profile source)
    {
        target.PreferredExecutable = source.PreferredExecutable;
        target.PreferredIWAD = source.PreferredIWAD;

        target.SelectedWads.Clear();
        foreach (var wad in source.SelectedWads)
        {
            target.SelectedWads.Add(wad);
        }
    }

    public sealed class ProfileReimportReport
    {
        public int SuccessfulReimports { get; set; }
        public int AlreadyValid { get; set; }
        public List<string> ReimportedProfiles { get; } = new();
        public List<string> StillInvalid { get; } = new();
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

    public async Task ExportProfileAsync(object? parameter)
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

    public void MoveSelectedWad(SelectWadInfo wad, int insertionIndex)
    {
        if (wad == null)
            return;

        if (SelectedProfile?.Locked == true)
            return;

        var filtered = FilteredSelectedWads;
        var currentIndex = filtered.IndexOf(wad);
        if (currentIndex < 0)
            return;

        var boundedIndex = Math.Clamp(insertionIndex, 0, filtered.Count);
        var insertingAtEnd = boundedIndex == filtered.Count;
        var adjustedIndex = boundedIndex;
        if (boundedIndex > currentIndex)
            adjustedIndex--;

        if (adjustedIndex == currentIndex)
            return;

        var order = GetActiveLoadOrderList();
        var movingPath = wad.Path;
        string? beforePath = insertingAtEnd ? null : filtered[boundedIndex].Path;

        RemovePathIfPresent(order, movingPath);

        var newIndex = beforePath != null ? FindPathIndex(order, beforePath) : order.Count;
        if (newIndex < 0)
            newIndex = order.Count;

        order.Insert(newIndex, movingPath);

        if (SelectedProfile != null)
        {
            TrySaveProfiles();
            MirrorProfileOrderIntoManualList();
        }

        UpdateSelectedWadsFilter();
    }

    /// <summary>
    /// Handles changes to WAD selection status, moving WADs between available and selected collections
    /// and updating the selected profile if one is active.
    /// </summary>
    /// <param name="wadInfo">The WAD whose selection status changed.</param>
    public void OnWadSelectionChanged(SelectWadInfo wadInfo)
    {
        if (_suppressSelectionChanged)
            return;

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
                MirrorProfileOrderIntoManualList();
            }
            else
            {
                if (!ContainsPath(_manualSelectedWadOrder, wadInfo.Path))
                {
                    _manualSelectedWadOrder.Add(wadInfo.Path);
                }
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
                MirrorProfileOrderIntoManualList();
            }
            else
            {
                RemovePathIfPresent(_manualSelectedWadOrder, wadInfo.Path);
            }
        }
        ApplyLoadOrderToFilteredSelectedWads();
    }

    public void InsertWadIntoLoadOrder(SelectWadInfo wad, int insertionIndex)
    {
        if (wad == null)
            return;

        if (SelectedProfile?.Locked == true)
            return;

        var order = GetActiveLoadOrderList();

        if (wad.IsSelected)
        {
            MoveSelectedWad(wad, insertionIndex);
            return;
        }

        _suppressSelectionChanged = true;
        try
        {
            wad.IsSelected = true;
        }
        finally
        {
            _suppressSelectionChanged = false;
        }

        var boundedIndex = Math.Clamp(insertionIndex, 0, order.Count);
        if (!ContainsPath(order, wad.Path))
        {
            order.Insert(boundedIndex, wad.Path);
            if (SelectedProfile != null)
            {
                TrySaveProfiles();
                MirrorProfileOrderIntoManualList();
            }
        }

        UpdateSelectedWadsFilter();
        UpdateAvailableWadsFilter();
    }

    private IList<string> GetActiveLoadOrderList()
    {
        if (SelectedProfile != null)
            return SelectedProfile.SelectedWads;

        return _manualSelectedWadOrder;
    }

    private static int FindPathIndex(IList<string> order, string path)
    {
        for (int i = 0; i < order.Count; i++)
        {
            if (PathsEqual(order[i], path))
            {
                return i;
            }
        }

        return -1;
    }

    private static bool ContainsPath(IList<string> order, string path)
    {
        return FindPathIndex(order, path) >= 0;
    }

    private static void RemovePathIfPresent(IList<string> order, string path)
    {
        var index = FindPathIndex(order, path);
        if (index >= 0)
        {
            order.RemoveAt(index);
        }
    }

    private void MirrorProfileOrderIntoManualList()
    {
        if (SelectedProfile == null)
            return;

        _manualSelectedWadOrder.Clear();
        foreach (var path in SelectedProfile.SelectedWads)
        {
            _manualSelectedWadOrder.Add(path);
        }
    }

    private List<SelectWadInfo> OrderSelectedWads(IEnumerable<SelectWadInfo> wads)
    {
        var result = wads.ToList();
        var order = GetActiveLoadOrderList();
        if (order.Count == 0)
        {
            result.Sort((left, right) => string.Compare(left.DisplayName, right.DisplayName, StringComparison.OrdinalIgnoreCase));
            return result;
        }

        var ordering = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        for (int i = 0; i < order.Count; i++)
        {
            var path = order[i];
            if (!string.IsNullOrWhiteSpace(path) && !ordering.ContainsKey(path))
            {
                ordering[path] = i;
            }
        }

        result.Sort((left, right) =>
        {
            var leftRank = ordering.TryGetValue(left.Path, out var l) ? l : int.MaxValue;
            var rightRank = ordering.TryGetValue(right.Path, out var r) ? r : int.MaxValue;
            if (leftRank != rightRank)
            {
                return leftRank.CompareTo(rightRank);
            }

            return string.Compare(left.DisplayName, right.DisplayName, StringComparison.OrdinalIgnoreCase);
        });

        return result;
    }

    private void ApplyLoadOrderToFilteredSelectedWads()
    {
        var ordered = OrderSelectedWads(FilteredSelectedWads.ToList());
        if (ordered.Count != FilteredSelectedWads.Count)
        {
            FilteredSelectedWads.Clear();
            foreach (var wad in ordered)
            {
                FilteredSelectedWads.Add(wad);
            }
            return;
        }

        for (int i = 0; i < ordered.Count; i++)
        {
            if (!ReferenceEquals(ordered[i], FilteredSelectedWads[i]))
            {
                FilteredSelectedWads.Clear();
                foreach (var wad in ordered)
                {
                    FilteredSelectedWads.Add(wad);
                }
                break;
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

        var ordered = OrderSelectedWads(filtered);
        foreach (var wad in ordered)
        {
            FilteredSelectedWads.Add(wad);
        }
    }

    internal IReadOnlyList<SelectWadInfo> GetSelectedWadsInLoadOrder()
    {
        var selected = AllWads
            .Where(w => w.IsSelected)
            .ToDictionary(w => w.Path, w => w, StringComparer.OrdinalIgnoreCase);

        var ordered = new List<SelectWadInfo>();
        var loadOrder = GetActiveLoadOrderList();
        if (loadOrder.Count > 0)
        {
            foreach (var path in loadOrder)
            {
                if (selected.TryGetValue(path, out var wad))
                {
                    ordered.Add(wad);
                    selected.Remove(path);
                }
            }
        }

        if (selected.Count > 0)
        {
            foreach (var wad in AllWads)
            {
                if (selected.TryGetValue(wad.Path, out var info))
                {
                    ordered.Add(info);
                    selected.Remove(wad.Path);
                }
            }
        }

        return ordered;
    }

    public virtual void ReloadWadsFromDisk(bool preserveSelections = true)
    {
        var previousSelectedIwadPath = SelectedIWAD?.Path;
        IWad? restoredIwad = null;

        HashSet<string>? preserved = null;
        if (preserveSelections)
        {
            preserved = AllWads.Where(w => w.IsSelected)
                .Select(w => w.Path)
                .ToHashSet(StringComparer.OrdinalIgnoreCase);
        }

        foreach (var wad in AllWads)
        {
            DetachWadSelectionHandler(wad);
        }

        AllWads.Clear();
        IWADs.Clear();
        WadLists.UpdateGlobalDictionary();

        var wads = WadLists.GetAllWads();
        foreach (var wadPath in wads)
        {
            var info = WadLists.GetWadInfo(wadPath);
            if (info.IsPatch)
            {
                var selectInfo = info.Info;
                selectInfo.IsSelected = ShouldAutoSelectWad(selectInfo.Path, preserved);
                AttachWadSelectionHandler(selectInfo);
                AllWads.Add(selectInfo);
            }
            else
            {
                var newIwad = new IWad
                {
                    Path = info.Path,
                    DisplayName = info.Name
                };

                IWADs.Add(newIwad);
                if (previousSelectedIwadPath != null && PathsEqual(newIwad.Path, previousSelectedIwadPath))
                {
                    restoredIwad = newIwad;
                }
            }
        }

        UpdateAvailableWadsFilter();
        UpdateSelectedWadsFilter();

        if (restoredIwad != null)
        {
            SelectedIWAD = restoredIwad;
        }
        else if (previousSelectedIwadPath != null && IWADs.All(i => !PathsEqual(i.Path, previousSelectedIwadPath)))
        {
            SelectedIWAD = null;
        }
    }

    internal void ApplyWadDirectoryChanges(IReadOnlyCollection<string> changedPaths)
    {
        if (changedPaths.Count == 0)
        {
            ReloadWadsFromDisk(preserveSelections: true);
            return;
        }

        var distinctPaths = changedPaths
            .Where(p => !string.IsNullOrWhiteSpace(p))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        foreach (var path in distinctPaths)
        {
            var exists = File.Exists(path) || Directory.Exists(path);
            if (exists)
            {
                if (!WadLists.IsPotentialWadPath(path))
                {
                    continue;
                }

                var info = WadLists.RefreshWadInfo(path);
                if (info == null)
                {
                    continue;
                }

                _ = info.IsPatch
                    ? UpsertPatchWad(info)
                    : UpsertIWad(info);
            }
            else
            {
                WadLists.RemoveWadInfo(path);
                _ = RemoveWadByPath(path);
            }
        }

        UpdateAvailableWadsFilter();
        UpdateSelectedWadsFilter();
    }

    private bool UpsertPatchWad(WadInfo info)
    {
        var existing = FindPatchWad(info.Path);
        if (existing != null)
        {
            var updated = false;
            if (!string.Equals(existing.DisplayName, info.Name, StringComparison.Ordinal))
            {
                existing.DisplayName = info.Name;
                updated = true;
            }
            if (existing.HasMaps != info.HasMaps)
            {
                existing.HasMaps = info.HasMaps;
                updated = true;
            }
            return updated;
        }

        var selectInfo = info.Info;
        selectInfo.IsSelected = ShouldAutoSelectWad(selectInfo.Path);
        AttachWadSelectionHandler(selectInfo);
        AllWads.Add(selectInfo);
        return true;
    }

    private bool UpsertIWad(WadInfo info)
    {
        var existing = IWADs.FirstOrDefault(i => PathsEqual(i.Path, info.Path));
        if (existing != null)
        {
            if (!string.Equals(existing.DisplayName, info.Name, StringComparison.Ordinal))
            {
                existing.DisplayName = info.Name;
                return true;
            }
            return false;
        }

        IWADs.Add(new IWad
        {
            Path = info.Path,
            DisplayName = info.Name
        });
        return true;
    }

    private bool RemoveWadByPath(string path)
    {
        var removedPatch = RemovePatchWad(path);
        var removedIwad = RemoveIwad(path);
        return removedPatch || removedIwad;
    }

    private bool RemovePatchWad(string path)
    {
        var wad = FindPatchWad(path);
        if (wad == null)
        {
            return false;
        }

        DetachWadSelectionHandler(wad);
        AllWads.Remove(wad);
        return true;
    }

    private bool RemoveIwad(string path)
    {
        var iwad = IWADs.FirstOrDefault(i => PathsEqual(i.Path, path));
        if (iwad == null)
        {
            return false;
        }

        IWADs.Remove(iwad);
        if (SelectedIWAD != null && PathsEqual(SelectedIWAD.Path, path))
        {
            SelectedIWAD = null;
        }
        return true;
    }

    private static SelectWadInfo? FindPatchWad(string path)
    {
        return AllWads.FirstOrDefault(w => PathsEqual(w.Path, path));
    }

    private static bool PathsEqual(string? left, string? right)
    {
        return string.Equals(left, right, StringComparison.OrdinalIgnoreCase);
    }

    private static bool ShouldAutoSelectWad(string path, HashSet<string>? preserved = null)
    {
        if (preserved != null && preserved.Contains(path))
        {
            return true;
        }

        return SELECTED_PROFILE?.SelectedWads.Contains(path) ?? false;
    }

    private void AttachWadSelectionHandler(SelectWadInfo wad)
    {
        wad.PropertyChanged += SelectWadInfo_PropertyChanged;
    }

    private void DetachWadSelectionHandler(SelectWadInfo wad)
    {
        wad.PropertyChanged -= SelectWadInfo_PropertyChanged;
    }

    private void SelectWadInfo_PropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (sender is SelectWadInfo wad && e.PropertyName == nameof(SelectWadInfo.IsSelected))
        {
            OnWadSelectionChanged(wad);
        }
    }

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
    public virtual Task<bool> ShowConfirmDialogue(string title, string text, string okText = "OK", string cancelText = "Cancel")
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
    /// Shows a confirmation dialog with customizable title, message, and button text.
    /// </summary>
    /// <param name="title">The dialog title.</param>
    /// <param name="text">The message to display.</param>
    /// <param name="setDontShowAgain">Called when the user chooses to not show the dialog again.</param>
    /// <param name="okText">The text for the OK button.</param>
    /// <param name="cancelText">The text for the Cancel button.</param>
    /// <returns>True if OK was clicked, false if Cancel was clicked.</returns>
    public virtual Task<bool> ShowConfirmDialogue(string title, string text, Action setDontShowAgain, string okText = "OK", string cancelText = "Cancel")
    {
        TaskCompletionSource<bool> tcs = new();
        var okButton = new Button { Content = okText, Width = 80, Margin = new Thickness(0,0,8,0) };
        var cancelButton = new Button { Content = cancelText, Width = 80 };
        var checkbox = new CheckBox { Content = "Don't show this again (Can be renabled from Preferences)", Margin = new Thickness(0,0,0,8), IsChecked = false };

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
                    checkbox,
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
            if (checkbox.IsChecked == true)
            {
                setDontShowAgain();
            }
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
    /// Launches the game with the selected executable, IWAD, and WADs.
    /// Creates a save folder for the selected profile and builds appropriate command-line arguments.
    /// </summary>
    public async void RunGame()
    {
        await RunGameInternalAsync(null);
    }

    public async Task RunGameWithMiniLauncherAsync(MiniLauncherOptions options)
    {
        if (options == null)
            throw new ArgumentNullException(nameof(options));

        await RunGameInternalAsync(null, options);
    }

    public async void RunGameFromSave(string saveFilePath)
    {
        if (string.IsNullOrWhiteSpace(saveFilePath))
            return;

        if (!File.Exists(saveFilePath))
        {
            ShowFailDialogue("Selected save file could not be found.");
            return;
        }

        await RunGameInternalAsync(saveFilePath);
    }

    private async Task RunGameInternalAsync(string? saveFileToLoad, MiniLauncherOptions? customOptions = null)
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

        var commandLineArgs = BuildGameCommandLineArgs(saveFolder, saveFileToLoad, customOptions);
        if (commandLineArgs == null)
            return;

        LaunchGameProcess(commandLineArgs);
    }

    public void ContinueMostRecentGame()
    {
        var recentSave = GetRecentSaveFiles().FirstOrDefault();
        if (recentSave == null)
        {
            RunGame();
            return;
        }

        RunGameFromSave(recentSave.FullName);
    }

    internal MiniLauncherViewModel CreateMiniLauncherViewModel()
    {
        var initialOptions = GetMiniLauncherDefaultsSnapshot();
        return new MiniLauncherViewModel(
            RunGameWithMiniLauncherAsync,
            GetLevels,
            initialOptions,
            SaveMiniLauncherDefaults);
    }

    private MiniLauncherOptions GetMiniLauncherDefaultsSnapshot()
    {
        var source = SELECTED_PROFILE?.MiniLauncherDefaults ?? _miniLauncherDefaultsConfig.Get();
        return (source ?? new MiniLauncherOptions()).Clone();
    }

    private void SaveMiniLauncherDefaults(MiniLauncherOptions options)
    {
        var snapshot = (options ?? new MiniLauncherOptions()).Clone();

        if (SELECTED_PROFILE is not null)
        {
            SELECTED_PROFILE.MiniLauncherDefaults = snapshot;
            TrySaveProfiles();
        }
        else
        {
            _miniLauncherDefaultsConfig.Set(snapshot);
        }
    }

    private IEnumerable<LevelEntry> GetLevels()
    {
        if(SELECTED_IWAD is null)
            return Array.Empty<LevelEntry>();
        var wadInfo = WadLists.GetWadInfo(SELECTED_IWAD.Path);
        List<LevelEntry> maplumps = wadInfo?.MapIDs?.Select(c=>new LevelEntry { LumpName = c, DisplayName = c })?.ToList() ?? new();
        var mapNames = wadInfo?.MapNames?.ToDictionary() ?? new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        // For each Wad in Load Order, try and overwrite mapnames
        var selectedWads = GetSelectedWadsInLoadOrder();
        foreach(var wad in selectedWads){
            var info = WadLists.GetWadInfo(wad.Path);
            if(info?.MapNames is not null){
                foreach(var kvp in info.MapNames){
                    mapNames[kvp.Key] = kvp.Value;
                }
            }
        }
        // Build final list of map names
        for(int i = 0; i < maplumps.Count; i++){
            var lump = maplumps[i].LumpName;
            if(mapNames.TryGetValue(lump, out var name)){
                maplumps[i] = new LevelEntry { LumpName = lump, DisplayName = name };
            }
        }
        return maplumps;
    }

    public void OpenMiniLauncher()
    {
        var owner = GetMainWindow();
        var launcher = new MiniLauncherDialog(this);

        if (owner != null)
        {
            launcher.Show(owner);
        }
        else
        {
            launcher.Show();
        }
    }

    public IEnumerable<RunMenuEntry> BuildRunMenuEntries()
    {
        var recentSaves = GetRecentSaveFiles();

        yield return new RunMenuEntry.RunMenuCommand("Launch Game", () => RunGame(), () => CanRunGame());
        yield return new RunMenuEntry.RunMenuCommand("Open Mini-Launcher", () => OpenMiniLauncher());
        yield return new RunMenuEntry.RunMenuCommand(
            "Continue Game",
            () => ContinueMostRecentGame(),
            () => CanRunGame());

        if (recentSaves.Count == 0)
            yield break;

        yield return new RunMenuEntry.RunMenuSeparator();

        foreach (var save in recentSaves)
        {
            var header = $"{save.Name} ({save.LastWriteTime:g})";
            var savePath = save.FullName;
            yield return new RunMenuEntry.RunMenuCommand(header, () => RunGameFromSave(savePath));
        }
    }

    private IReadOnlyList<FileInfo> GetRecentSaveFiles()
    {
        var folderPath = GetCurrentSaveDirectoryPath();
        if (!Directory.Exists(folderPath))
        {
            return Array.Empty<FileInfo>();
        }

        try
        {
            return new DirectoryInfo(folderPath)
                .EnumerateFiles("*", SearchOption.TopDirectoryOnly)
                .OrderByDescending(f => f.LastWriteTimeUtc)
                .Take(10)
                .ToList();
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            Console.Error.WriteLine($"Failed to enumerate saves in '{folderPath}': {ex.Message}");
            return Array.Empty<FileInfo>();
        }
    }

    private bool CanContinueGame()
    {
        return CanRunGame() && GetRecentSaveFiles().Count > 0;
    }

    private void NotifyPreferredRunStateChanged()
    {
        OnPropertyChanged(nameof(CanRunPreferredLaunch));
    }

    private bool CanRunGame()
    {
        return SelectedExecutable != null && SelectedIWAD != null;
    }

    /// <summary>
    /// Ensures the ./saves directory exists, creating it if necessary.
    /// </summary>
    /// <returns>True if the directory exists or was created successfully, false otherwise.</returns>
    public bool EnsureSavesDirectoryExists()
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

    public string GetSaveFolder(string profileName)
    {
        var sanitized = SanitizeSaveFolderName(profileName);
        return $"./saves/{sanitized}";
    }

    private static string SanitizeSaveFolderName(string name)
    {
        var sanitized = name;
        foreach (var c in Path.GetInvalidFileNameChars())
        {
            sanitized = sanitized.Replace(c, '-');
        }

        return sanitized;
    }

    private string GetCurrentSaveFolderName()
    {
        return SanitizeSaveFolderName(SELECTED_PROFILE?.Name ?? "_unsorted");
    }

    private string GetCurrentSaveDirectoryPath()
    {
        return Path.Combine(".", "saves", GetCurrentSaveFolderName());
    }

    /// <summary>
    /// Determines the save folder name based on the selected profile.
    /// Prompts the user if no profile is selected.
    /// </summary>
    /// <returns>The save folder name, or null if the user cancelled.</returns>
    internal async Task<string?> DetermineSaveFolder()
    {
        if (SELECTED_PROFILE is not null)
        {
            var saveFolder = SanitizeSaveFolderName(SELECTED_PROFILE.Name);
            return saveFolder;
        }

        var proceed = true;
        if (UserPreferences.ShowNoProfileLaunchWarning)
            proceed = await ShowConfirmDialogue(
                "Run Game",
                "You have not selected a profile! Setting a profile ensures seperate tracking of saves and modlists. Are you sure you wish to proceed?",
                () => {
                    UserPreferences.ShowNoProfileLaunchWarning = false;
                    UserPreferences.Save();
                },
                "Yes",
                "Cancel");

        return proceed ? SanitizeSaveFolderName("_unsorted") : null;
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
    private string? BuildGameCommandLineArgs(string saveFolder, string? saveFileToLoad, MiniLauncherOptions? customOptions)
    {
        string args = $"-iwad \"{SELECTED_IWAD!.Path}\"";

        var selected = GetSelectedWadsInLoadOrder();
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

        if (!string.IsNullOrWhiteSpace(saveFileToLoad))
        {
            var relativeToFolder = Path.GetRelativePath(fullSavePath, saveFileToLoad);
            args += $" -loadgame \"{relativeToFolder}\"";
        }

        if (customOptions != null)
        {
            if (customOptions.Skill > 0)
            {
                args += $" -skill {customOptions.Skill}";
            }

            if (!string.IsNullOrWhiteSpace(customOptions.InitialLevel))
            {
                args += $" +map {customOptions.InitialLevel}";
            }

            if (customOptions.NoMonsters)
            {
                args += " -nomonsters";
            }

            if (customOptions.FastMonsters)
            {
                args += " -fast";
            }

            if (customOptions.RespawnMonsters)
            {
                args += " -respawn";
            }

            if (customOptions.EnableMultiplayer)
            {
                args += " -net";

                if (customOptions.HostGame)
                {
                    var hostSlots = customOptions.HostPlayerCount <= 0 ? 1 : customOptions.HostPlayerCount;
                    args += $" -host {hostSlots}";

                    if (!string.IsNullOrWhiteSpace(customOptions.HostPort))
                    {
                        args += $" -port {customOptions.HostPort}";
                    }
                }
                else if (!string.IsNullOrWhiteSpace(customOptions.IPAddress))
                {
                    if (!string.IsNullOrWhiteSpace(customOptions.Port))
                    {
                        args += $" -join {customOptions.IPAddress}:{customOptions.Port}";
                    }
                    else
                    {
                        args += $" -join {customOptions.IPAddress}";
                    }
                }
            }
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
            if (UserPreferences.ExitLauncherOnRun)
            {
                Environment.Exit(0);
            }
        }
        catch (Exception ex)
        {
            ShowFailDialogue($"An error occurred while trying to run the game: {ex.Message}");
        }
    }

    public void Dispose()
    {
        UserPreferencesChanged -= OnUserPreferencesChanged;
        WadDirectoryWatcher.WadFilesChanged -= OnWadFilesChanged;
        foreach (var wad in AllWads)
        {
            DetachWadSelectionHandler(wad);
        }
    }
}
