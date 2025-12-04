using System;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Windows.Input;
using Underworld.Models;

namespace Underworld.ViewModels;

public partial class DataDirectoriesViewModel : ViewModelBase
{
    private readonly ConfigEntry<System.Collections.Generic.List<string>> _dataDirectoriesConfig;

    public ObservableCollection<DataDirectory> DataDirectories { get; } = new();

    private DataDirectory? _selectedDirectory;
    public DataDirectory? SelectedDirectory
    {
        get => _selectedDirectory;
        set
        {
            if (SetProperty(ref _selectedDirectory, value))
            {
                (RemoveDirectoryCommand as RelayCommand)?.RaiseCanExecuteChanged();
            }
        }
    }

    public ICommand RemoveDirectoryCommand { get; }

    public DataDirectoriesViewModel()
    {
        _dataDirectoriesConfig = Config.Setup("dataDirectories", new System.Collections.Generic.List<string>());

        RemoveDirectoryCommand = new RelayCommand(
            _ => RemoveDirectory(),
            _ => SelectedDirectory != null && !SelectedDirectory.IsFromEnvironment
        );

        LoadDirectories();
    }

    public void LoadDirectories()
    {
        DataDirectories.Clear();

        // Add environment variables
        var envVar = Environment.GetEnvironmentVariable("DOOMWADDIR");
        if (!string.IsNullOrEmpty(envVar) && Directory.Exists(envVar))
        {
            DataDirectories.Add(new DataDirectory(envVar, "DOOMWADDIR", true));
        }

        var envPath = Environment.GetEnvironmentVariable("DOOMWADPATH");
        if (!string.IsNullOrEmpty(envPath))
        {
            var separator = envPath.Contains(';') ? ';' : ':';
            var paths = envPath.Split(separator);
            foreach (var p in paths)
            {
                var trimmed = p.Trim();
                if (Directory.Exists(trimmed) && !DataDirectories.Any(d => d.Path == trimmed))
                {
                    DataDirectories.Add(new DataDirectory(trimmed, "DOOMWADPATH", true));
                }
            }
        }

        // Add persisted user directories
        var persisted = _dataDirectoriesConfig.Get();
        foreach (var dir in persisted)
        {
            if (Directory.Exists(dir) && !DataDirectories.Any(d => d.Path == dir))
            {
                DataDirectories.Add(new DataDirectory(dir, "User", false));
            }
        }
    }

    public System.Collections.Generic.List<string> AddDirectories(System.Collections.Generic.IEnumerable<string> paths)
    {
        var invalids = new System.Collections.Generic.List<string>();
        var added = new System.Collections.Generic.List<string>();

        foreach (var path in paths)
        {
            try
            {
                if (!Directory.Exists(path))
                {
                    invalids.Add($"{path}: directory does not exist");
                    continue;
                }

                if (DataDirectories.Any(d => d.Path == path))
                {
                    invalids.Add($"{path}: already in list");
                    continue;
                }

                DataDirectories.Add(new DataDirectory(path, "User", false));
                added.Add(path);
            }
            catch (Exception ex)
            {
                invalids.Add($"{path}: {ex.Message}");
            }
        }

        // Persist only the user-added directories
        var persistedDirs = DataDirectories
            .Where(d => !d.IsFromEnvironment)
            .Select(d => d.Path)
            .ToList();
        _dataDirectoriesConfig.Set(persistedDirs);

        return invalids;
    }

    private void RemoveDirectory()
    {
        if (SelectedDirectory == null || SelectedDirectory.IsFromEnvironment)
            return;

        DataDirectories.Remove(SelectedDirectory);
        SelectedDirectory = null;

        // Update persisted directories
        var persistedDirs = DataDirectories
            .Where(d => !d.IsFromEnvironment)
            .Select(d => d.Path)
            .ToList();
        _dataDirectoriesConfig.Set(persistedDirs);
    }

    // Public method for testing
    public void RemoveSelectedDirectory()
    {
        RemoveDirectory();
    }
}
