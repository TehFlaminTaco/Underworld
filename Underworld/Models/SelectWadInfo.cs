using CommunityToolkit.Mvvm.ComponentModel;

namespace Underworld.Models;

public partial class SelectWadInfo : ObservableObject
{

    [ObservableProperty]
    private bool isSelected = false;

    [ObservableProperty]
    private string displayName = string.Empty;

    [ObservableProperty]
    private string path = string.Empty;

    public string Filename => System.IO.Path.GetFileName(Path);

    [ObservableProperty]
    private bool hasMaps = false;

    public override string ToString() => DisplayName;
}