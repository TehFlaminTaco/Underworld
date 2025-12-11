using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Text.RegularExpressions;
using Underworld.ViewModels;

namespace Underworld.Models;

/// <summary>
/// Manages discovery and caching of WAD files across configured data directories.
/// </summary>
public static class WadLists
{
    private const string ZScriptPattern = "zscript.*";
    
    /// <summary>
    /// Valid file extensions for WAD files.
    /// </summary>
    public static readonly string[]                                                              ValidWADFiles = { ".wad", ".pk3", ".zip", ".pk7" };

    /// <summary>
    /// Determines whether a filesystem path could refer to a trackable WAD asset.
    /// </summary>
    public static bool IsPotentialWadPath(string path)
    {
        if (string.IsNullOrWhiteSpace(path))
            return false;

        var extension = Path.GetExtension(path);
        if (string.IsNullOrEmpty(extension))
        {
            // Folder-based mods have no extension; treat them as potential candidates.
            return true;
        }

        return ValidWADFiles.Contains(extension.ToLowerInvariant());
    }

    /// <summary>
    /// Persistent cache of WAD file information.
    /// </summary>
    public static ConfigEntry<Dictionary<string, WadInfo>> WadInfoCache =
        Config.Setup("wadInfoCache", new Dictionary<string, WadInfo>());

    /// <summary>
    /// Gets all configured data directories from the DataDirectoriesViewModel.
    /// </summary>
    /// <returns>List of unique directory paths</returns>
    public static List<string> GetDataDirectories()
    {
        var data = new DataDirectoriesViewModel();
        data.LoadDirectories();
        return data.DataDirectories.Select(d => d.Path).Distinct().ToList();
    }

    /// <summary>
    /// Clears the WAD cache and repopulates it with current data directories.
    /// </summary>
    public static void ClearWadCache()
    {
        WadInfoCache.Set(new Dictionary<string, WadInfo>());
        GetNewWadInfos();
    }

    /// <summary>
    /// Discovers all WAD files in configured data directories.
    /// Includes files with valid extensions and directories containing ZScript files.
    /// </summary>
    /// <returns>List of unique WAD file/directory paths</returns>
    public static List<string> GetAllWads()
    {
        var dataDirs = GetDataDirectories();
        var wadPaths = new List<string>();

        foreach (var dir in dataDirs)
        {
            if (!Directory.Exists(dir))
                continue;

            try
            {
                DiscoverWadsInDirectory(dir, wadPaths);
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine($"Could not access directory '{dir}': {ex.Message}");
            }
        }

        return wadPaths.Distinct().ToList();
    }

    /// <summary>
    /// Populates the WAD info cache with all discovered WADs.
    /// </summary>
    public static void GetNewWadInfos()
    {
        var wads = GetAllWads();
        foreach (var wad in wads)
        {
            GetWadInfo(wad);
        }
    }

    /// <summary>
    /// Discovers WAD files and directories in a single directory.
    /// </summary>
    private static void DiscoverWadsInDirectory(string directory, List<string> wadPaths)
    {
        // Find WAD files by extension
        var files = Directory.GetFiles(directory, "*", SearchOption.TopDirectoryOnly);
        foreach (var file in files)
        {
            if (IsValidWadFile(file))
            {
                wadPaths.Add(file);
            }
        }

        // Find directories containing ZScript files (folder-based mods)
        var directories = Directory.GetDirectories(directory, "*", SearchOption.TopDirectoryOnly);
        foreach (var folder in directories)
        {
            if (IsZScriptDirectory(folder))
            {
                wadPaths.Add(folder);
            }
        }
    }

    /// <summary>
    /// Checks if a file has a valid WAD extension.
    /// </summary>
    private static bool IsValidWadFile(string path)
    {
        var extension = Path.GetExtension(path).ToLowerInvariant();
        return ValidWADFiles.Contains(extension);
    }

    /// <summary>
    /// Checks if a directory contains ZScript files, indicating it's a folder-based mod.
    /// </summary>
    private static bool IsZScriptDirectory(string directory)
    {
        try
        {
            var zscriptFiles = Directory.GetFiles(directory, ZScriptPattern, SearchOption.TopDirectoryOnly);
            return zscriptFiles.Length > 0;
        }
        catch
        {
            return false;
        }
    }

    private const string GameSupportArchive = "game_support.pk3";
    private const string IWadInfoFile = "iwadinfo.txt";
    
    private static Dictionary<string, string>? _iwadNamesCache = null;
    private static readonly Regex IWADNameRegex = new(@"\bIWADName\s*=\s*""([^""]+)""", RegexOptions.Compiled);
    private static readonly Regex NameRegex = new(@"\bName\s*=\s*""([^""]+)""", RegexOptions.Compiled);

    /// <summary>
    /// Gets a dictionary mapping IWAD filenames to their display names.
    /// Data is parsed from game_support.pk3/iwadinfo.txt if found in data directories.
    /// Results are cached after first successful read.
    /// </summary>
    /// <returns>Dictionary of IWAD filename (lowercase) to display name mappings</returns>
    public static Dictionary<string, string> GetIWADNames()
    {
        if (_iwadNamesCache != null)
            return _iwadNamesCache;

        var gameSupportPath = FindGameSupportArchive();
        if (gameSupportPath == null)
        {
            Console.Error.WriteLine($"No {GameSupportArchive} found in data directories.");
            return new Dictionary<string, string>();
        }

        try
        {
            var iwadNames = ParseIWadInfoFromArchive(gameSupportPath);
            _iwadNamesCache = iwadNames;
            return iwadNames;
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"Could not read {GameSupportArchive} for IWAD names: {ex.Message}");
            return new Dictionary<string, string>();
        }
    }

    /// <summary>
    /// Searches data directories for the game_support.pk3 archive.
    /// </summary>
    /// <returns>Full path to game_support.pk3 or null if not found</returns>
    private static string? FindGameSupportArchive()
    {
        var dataDirs = GetDataDirectories();
        
        foreach (var dir in dataDirs)
        {
            try
            {
                var pk3Path = Path.Combine(dir, GameSupportArchive);
                if (File.Exists(pk3Path))
                {
                    return pk3Path;
                }
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine($"Could not access directory '{dir}': {ex.Message}");
            }
        }

        return null;
    }

    /// <summary>
    /// Parses iwadinfo.txt from the game_support.pk3 archive.
    /// </summary>
    private static Dictionary<string, string> ParseIWadInfoFromArchive(string archivePath)
    {
        using var archive = ZipFile.OpenRead(archivePath);
        var iwadInfoEntry = archive.Entries
            .FirstOrDefault(e => e.FullName.Equals(IWadInfoFile, StringComparison.OrdinalIgnoreCase));

        if (iwadInfoEntry == null)
        {
            Console.Error.WriteLine($"No {IWadInfoFile} found in {archivePath}");
            return new Dictionary<string, string>();
        }

        using var reader = new StreamReader(iwadInfoEntry.Open());
        return ParseIWadInfoContent(reader);
    }

    /// <summary>
    /// Parses IWAD entries from iwadinfo.txt content.
    /// Each entry contains IWADName and Name properties that map filename to display name.
    /// </summary>
    private static Dictionary<string, string> ParseIWadInfoContent(StreamReader reader)
    {
        var iwadNames = new Dictionary<string, string>();

        while (!reader.EndOfStream)
        {
            var line = reader.ReadLine()?.Trim();
            if (line != null && line.StartsWith("IWad", StringComparison.OrdinalIgnoreCase))
            {
                var iwadEntry = ParseSingleIWadEntry(reader);
                if (iwadEntry.HasValue)
                {
                    iwadNames[iwadEntry.Value.FileName.ToLowerInvariant()] = iwadEntry.Value.DisplayName;
                }
            }
        }

        return iwadNames;
    }

    /// <summary>
    /// Parses a single IWAD entry block from iwadinfo.txt.
    /// </summary>
    private static (string FileName, string DisplayName)? ParseSingleIWadEntry(StreamReader reader)
    {
        // Skip to opening brace
        while (!reader.EndOfStream)
        {
            var line = reader.ReadLine()?.Trim();
            if (line == "{")
                break;
        }

        string? iwadFileName = null;
        string? displayName = null;

        // Read properties until closing brace
        while (!reader.EndOfStream)
        {
            var line = reader.ReadLine()?.Trim();
            if (string.IsNullOrEmpty(line))
                continue;
                
            if (line == "}")
                break;

            var iwadMatch = IWADNameRegex.Match(line);
            if (iwadMatch.Success)
            {
                iwadFileName = iwadMatch.Groups[1].Value;
            }

            var nameMatch = NameRegex.Match(line);
            if (nameMatch.Success)
            {
                displayName = nameMatch.Groups[1].Value;
            }
        }

        if (iwadFileName != null && displayName != null)
        {
            return (iwadFileName, displayName);
        }

        Console.Error.WriteLine($"Malformed IWAD entry - IWADName: {iwadFileName}, Name: {displayName}");
        return null;
    }

    /// <summary>
    /// Gets metadata information about a WAD file or directory.
    /// Results are cached for performance.
    /// </summary>
    /// <param name="path">Path to the WAD file or directory</param>
    /// <returns>WadInfo containing metadata about the WAD</returns>
    public static WadInfo GetWadInfo(string path)
    {
        var cache = WadInfoCache.Get();
        
        if (cache.ContainsKey(path))
        {
            return cache[path];
        }

        var info = AnalyzeWadFile(path);
        
        cache[path] = info;
        WadInfoCache.Set(cache);

        return info;
    }

    /// <summary>
    /// Forces the cache entry for a specific path to be refreshed if the file currently exists.
    /// Returns null when the path is no longer available or cannot be analyzed.
    /// </summary>
    public static WadInfo? RefreshWadInfo(string path)
    {
        if (!File.Exists(path) && !Directory.Exists(path))
        {
            RemoveWadInfo(path);
            return null;
        }

        var info = AnalyzeWadFile(path);
        var cache = WadInfoCache.Get();
        cache[path] = info;
        WadInfoCache.Set(cache);
        return info;
    }

    /// <summary>
    /// Removes a cached WAD entry when the file has been deleted.
    /// </summary>
    public static bool RemoveWadInfo(string path)
    {
        var cache = WadInfoCache.Get();
        var removed = cache.Remove(path);
        if (removed)
        {
            WadInfoCache.Set(cache);
        }
        return removed;
    }

    /// <summary>
    /// Analyzes a WAD file or directory to determine its properties.
    /// </summary>
    private static WadInfo AnalyzeWadFile(string path)
    {
        var extension = Path.GetExtension(path).ToLowerInvariant();
        
        var (isPatch, hasMaps) = extension switch
        {
            "" => AnalyzeDirectory(path),
            ".wad" => AnalyzeWadArchive(path),
            ".pk3" or ".zip" or ".pk7" => AnalyzeZipArchive(path),
            _ => (true, false) // Unknown type - assume PWAD
        };

        var displayName = DetermineDisplayName(path, isPatch);

        return new WadInfo
        {
            Path = path,
            Name = displayName,
            IsPatch = isPatch,
            HasMaps = hasMaps
        };
    }

    /// <summary>
    /// Analyzes a directory (folder-based mod) for map content.
    /// </summary>
    private static (bool IsPatch, bool HasMaps) AnalyzeDirectory(string path)
    {
        var mapsDir = Path.Combine(path, "maps");
        if (!Directory.Exists(mapsDir))
        {
            return (true, false);
        }

        try
        {
            var mapEntries = Directory.GetFileSystemEntries(mapsDir, "*", SearchOption.TopDirectoryOnly);
            bool hasMaps = mapEntries.Any(entry => IsStartMap(Path.GetFileNameWithoutExtension(entry)));
            return (true, hasMaps);
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"Could not read maps directory in '{path}': {ex.Message}");
            return (true, false);
        }
    }

    /// <summary>
    /// Analyzes a .wad file by reading its header and directory.
    /// </summary>
    private static (bool IsPatch, bool HasMaps) AnalyzeWadArchive(string path)
    {
        try
        {
            using var fs = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read);
            using var br = new BinaryReader(fs);
            
            var header = new string(br.ReadChars(4));
            
            if (header == "IWAD")
            {
                return (false, true); // IWADs always have maps
            }
            
            // PWAD - check for map lumps
            var numLumps = br.ReadInt32();
            var dirOffset = br.ReadInt32();
            
            bool hasMaps = SearchWadDirectory(br, fs, dirOffset, numLumps);
            return (true, hasMaps);
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"Could not read WAD file '{path}': {ex.Message}");
            return (true, false);
        }
    }

    /// <summary>
    /// Searches a WAD directory for map markers.
    /// </summary>
    private static bool SearchWadDirectory(BinaryReader reader, FileStream stream, int dirOffset, int numLumps)
    {
        stream.Seek(dirOffset, SeekOrigin.Begin);
        
        for (int i = 0; i < numLumps; i++)
        {
            reader.ReadInt32(); // lumpOffset
            reader.ReadInt32(); // lumpSize
            var lumpName = new string(reader.ReadChars(8)).TrimEnd('\0').ToUpperInvariant();
            
            if (IsStartMap(lumpName))
            {
                return true;
            }
        }
        
        return false;
    }

    /// <summary>
    /// Analyzes a ZIP/PK3/PK7 archive for map content.
    /// </summary>
    private static (bool IsPatch, bool HasMaps) AnalyzeZipArchive(string path)
    {
        try
        {
            using var archive = ZipFile.OpenRead(path);
            
            var mapEntries = archive.Entries
                .Where(e => e.FullName.StartsWith("maps/", StringComparison.OrdinalIgnoreCase));
            
            bool hasMaps = mapEntries.Any(entry => 
                IsStartMap(Path.GetFileNameWithoutExtension(entry.Name)));
            
            return (true, hasMaps);
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"Could not read archive file '{path}': {ex.Message}");
            return (true, false);
        }
    }

    /// <summary>
    /// Checks if a map name represents a starting map (MAP01 or E1M1).
    /// </summary>
    private static bool IsStartMap(string mapName)
    {
        var upperName = mapName.ToUpperInvariant();
        return upperName == "MAP01" || upperName == "E1M1";
    }

    /// <summary>
    /// Determines the display name for a WAD.
    /// IWADs use names from iwadinfo.txt if available, PWADs use filename.
    /// </summary>
    private static string DetermineDisplayName(string path, bool isPatch)
    {
        var filename = Path.GetFileName(path);
        
        if (isPatch)
        {
            return filename;
        }

        // IWAD - try to get friendly name
        var iwadNames = GetIWADNames();
        var key = filename.ToLowerInvariant();
        
        if (iwadNames.ContainsKey(key))
        {
            Console.WriteLine($"Found IWAD name for '{key}': {iwadNames[key]}");
            return iwadNames[key];
        }

        Console.WriteLine($"No IWAD name found for '{path}', using filename.");
        return filename;
    }

    /// <summary>
    /// Gets all cached IWADs (non-patch WADs).
    /// </summary>
    public static IEnumerable<WadInfo> IWADs => WadInfoCache.Get().Values.Where(w => !w.IsPatch);
}

/// <summary>
/// Contains metadata about a WAD file or directory.
/// Serializable for storage in configuration.
/// </summary>
public class WadInfo
{
    /// <summary>
    /// Gets or sets the full path to the WAD file or directory.
    /// </summary>
    public string Path { get; set; } = string.Empty;
    
    /// <summary>
    /// Gets or sets the display name of the WAD.
    /// For IWADs, this may be a friendly name from iwadinfo.txt.
    /// </summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets whether this is a patch WAD (PWAD) vs an IWAD.
    /// IWADs are standalone game files, PWADs are modifications.
    /// </summary>
    public bool IsPatch { get; set; }
    
    /// <summary>
    /// Gets or sets whether this WAD contains map data.
    /// </summary>
    public bool HasMaps { get; set; }

    /// <summary>
    /// Gets a SelectWadInfo instance for UI binding.
    /// </summary>
    [System.Text.Json.Serialization.JsonIgnore]
    public SelectWadInfo Info => new()
    {
        Path = this.Path,
        DisplayName = this.Name,
        HasMaps = this.HasMaps
    };
}
