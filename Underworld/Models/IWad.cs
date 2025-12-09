using CommunityToolkit.Mvvm.ComponentModel;

namespace Underworld.Models;

public partial class IWad : ObservableObject
{

    [ObservableProperty]
    private string displayName = string.Empty;

    [ObservableProperty]
    private string path = string.Empty;

    public override string ToString() => DisplayName;
}