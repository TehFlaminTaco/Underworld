using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Avalonia.Data;
using Underworld.ViewModels;

namespace Underworld.Models;

// Serializable Profile for JSON support
[Serializable]
public class ExportProfile
{
    public string Name { get; set; } = String.Empty;
    public string PreferredExecutable { get; set; } = String.Empty;
    public string PreferredIWAD { get; set; } = String.Empty;

    public List<string> SelectedWADs { get; set; } = new();

    public static ExportProfile From(Profile profile)
    {
        return new()
        {
            Name = profile.Name,
            PreferredExecutable = Path.GetFileName(profile.PreferredExecutable),
            PreferredIWAD = Path.GetFileName(profile.PreferredIWAD),
            SelectedWADs = profile.SelectedWads.Select(wad => Path.GetFileName(wad)).ToList()
        };
    }

    public (Profile? result, string? failReason) To()
    {
        string? failReason = null;
        var profile = new Profile()
        {
            Name = Name
        };
        if (PreferredExecutable is not null)
        {
            var _executablesConfig = Config.Setup("executables", new List<ExecutableItem>());
            var executables = _executablesConfig.Get();
            var exe = executables.FirstOrDefault(c=>Path.GetFileName(c.Path) == PreferredExecutable);
            if(exe is null)
            {
                failReason = $"Preferred Executable for profile ({PreferredExecutable}) not found.";
            }
            else
            {
                PreferredExecutable = exe.Path;   
            }
        }

        if (PreferredIWAD is not null)
        {
            var allWads = WadLists.GetAllWads();
            var iwad = allWads.FirstOrDefault(c=>Path.GetFileName(c) == PreferredIWAD);
            if(iwad is null)
            {
                failReason = $"Preferred IWAD for profile ({PreferredIWAD}) not found.";
                return (null, failReason);
            }
            profile.PreferredIWAD = iwad;
        }

        foreach(var wad in SelectedWADs)
        {
            var pwad = MainWindowViewModel.AllWads.FirstOrDefault(c=>Path.GetFileName(c.Path) == wad);
            if(pwad is null)
            {
                failReason = $"Required WAD for profile ({wad}) not found.";
                return (null, failReason);
            }
            profile.SelectedWads.Add(pwad.Path);
        }

        return (profile, failReason);        
    }

    public override string ToString() => Name;
}