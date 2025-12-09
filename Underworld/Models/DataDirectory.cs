using CommunityToolkit.Mvvm.ComponentModel;

namespace Underworld.Models;

public partial class DataDirectory : ObservableObject
{
    [ObservableProperty]
    private string path = string.Empty;

    [ObservableProperty]
    private string source = string.Empty;  // "User", "DOOMWADDIR", "DOOMWADPATH", etc.

    [ObservableProperty]
    private bool isFromEnvironment = false;  // True if from env var, unselectable

    public DataDirectory()
    {
    }

    public DataDirectory(string path, string source, bool isFromEnvironment = false)
    {
        Path = path;
        Source = source;
        IsFromEnvironment = isFromEnvironment;
    }

    public override string ToString() => this.Path;
}
