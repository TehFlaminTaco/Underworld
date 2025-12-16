using CommunityToolkit.Mvvm.ComponentModel;

namespace Underworld.Models;

public partial class LevelEntry : ObservableObject
{
    [ObservableProperty]
    private string lumpName = string.Empty;

    [ObservableProperty]
    private string displayName = string.Empty;
}