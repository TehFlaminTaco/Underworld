using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;

namespace Underworld.Models;

public partial class Profile : ObservableObject
{

    [ObservableProperty]
    private string name = string.Empty;

    [ObservableProperty]
    public bool locked = false;

    [ObservableProperty]
    public string preferredExecutable = string.Empty;

    [ObservableProperty]
    public string preferredIWAD = string.Empty;

    [ObservableProperty]
    public ObservableCollection<string> selectedWads = new ObservableCollection<string>();

    [ObservableProperty]
    public string commandLineArguments = string.Empty;

    public override string ToString() => Name;
}