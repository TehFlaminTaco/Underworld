using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Underworld.ViewModels;

namespace Underworld.Models;

/// <summary>
/// Serializable representation of a Profile for JSON import/export.
/// Stores only filenames instead of full paths for portability across systems.
/// </summary>
[Serializable]
public class ExportProfile
{
    /// <summary>
    /// Gets or sets the profile name.
    /// </summary>
    public string Name { get; set; } = string.Empty;
    
    /// <summary>
    /// Gets or sets the preferred executable filename (not full path).
    /// </summary>
    public string PreferredExecutable { get; set; } = string.Empty;
    
    /// <summary>
    /// Gets or sets the preferred IWAD filename (not full path).
    /// </summary>
    public string PreferredIWAD { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the list of selected WAD filenames (not full paths).
    /// </summary>
    public List<string> SelectedWADs { get; set; } = new();

    /// <summary>
    /// Creates an ExportProfile from a Profile by converting full paths to filenames.
    /// </summary>
    /// <param name="profile">The profile to convert</param>
    /// <returns>An ExportProfile suitable for serialization</returns>
    public static ExportProfile From(Profile profile)
    {
        return new ExportProfile
        {
            Name = profile.Name,
            PreferredExecutable = ExtractFilename(profile.PreferredExecutable),
            PreferredIWAD = ExtractFilename(profile.PreferredIWAD),
            SelectedWADs = profile.SelectedWads
                .Select(ExtractFilename)
                .ToList()
        };
    }

    /// <summary>
    /// Converts this ExportProfile back to a Profile by resolving filenames to full paths.
    /// </summary>
    /// <returns>
    /// A tuple containing the converted Profile (or null if conversion failed) 
    /// and an optional failure reason string.
    /// </returns>
    public (Profile? result, string? failReason) To()
    {
        var profile = new Profile { Name = Name };
        string? warning = null;
        
        var executableResult = ResolveExecutable();
        if (!executableResult.Success)
        {
            return (null, executableResult.ErrorMessage);
        }
        if (!string.IsNullOrEmpty(executableResult.Path))
        {
            profile.PreferredExecutable = executableResult.Path;
        }
        warning = executableResult.WarningMessage;

        var iwadResult = ResolveIWAD();
        if (!iwadResult.Success)
        {
            return (null, iwadResult.ErrorMessage);
        }
        if (!string.IsNullOrEmpty(iwadResult.Path))
        {
            profile.PreferredIWAD = iwadResult.Path;
        }

        var wadsResult = ResolveWADs();
        if (!wadsResult.Success)
        {
            return (null, wadsResult.ErrorMessage);
        }
        foreach (var wadPath in wadsResult.Paths!)
        {
            profile.SelectedWads.Add(wadPath);
        }

        return (profile, warning);
    }

    /// <summary>
    /// Resolves the preferred executable filename to a full path.
    /// </summary>
    private (bool Success, string? Path, string? ErrorMessage, string? WarningMessage) ResolveExecutable()
    {
        if (string.IsNullOrEmpty(PreferredExecutable))
        {
            return (true, string.Empty, null, null);
        }

        var executablesConfig = Config.Setup("executables", new List<ExecutableItem>());
        var executables = executablesConfig.Get();
        var executable = executables.FirstOrDefault(e => 
            Path.GetFileName(e.Path) == PreferredExecutable);

        if (executable == null)
        {
            var warning = $"Preferred Executable for profile ({PreferredExecutable}) not found.";
            return (true, string.Empty, null, warning);
        }

        return (true, executable.Path, null, null);
    }

    /// <summary>
    /// Resolves the preferred IWAD filename to a full path.
    /// </summary>
    private (bool Success, string? Path, string? ErrorMessage) ResolveIWAD()
    {
        if (string.IsNullOrEmpty(PreferredIWAD))
        {
            return (true, null, null);
        }

        var allWads = WadLists.GetAllWads();
        var iwad = allWads.FirstOrDefault(w => 
            Path.GetFileName(w) == PreferredIWAD);

        if (iwad == null)
        {
            var error = $"Preferred IWAD for profile ({PreferredIWAD}) not found.";
            return (false, null, error);
        }

        return (true, iwad, null);
    }

    /// <summary>
    /// Resolves the selected WAD filenames to full paths.
    /// </summary>
    private (bool Success, List<string>? Paths, string? ErrorMessage) ResolveWADs()
    {
        var resolvedPaths = new List<string>();

        foreach (var wadFilename in SelectedWADs)
        {
            var wad = MainWindowViewModel.AllWads.FirstOrDefault(w => 
                Path.GetFileName(w.Path) == wadFilename);

            if (wad == null)
            {
                var error = $"Required WAD for profile ({wadFilename}) not found.";
                return (false, null, error);
            }

            resolvedPaths.Add(wad.Path);
        }

        return (true, resolvedPaths, null);
    }

    /// <summary>
    /// Extracts the filename from a full path, or returns empty string if null.
    /// </summary>
    private static string ExtractFilename(string? path)
    {
        return string.IsNullOrEmpty(path) ? string.Empty : Path.GetFileName(path);
    }

    public override string ToString() => Name;
}