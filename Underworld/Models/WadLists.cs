using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using Underworld.Models;
using Underworld.ViewModels;

namespace Underworld.Models;

public static class WadLists
{
    public static ConfigEntry<Dictionary<string, WadInfo>> WadInfoCache =
        Config.Setup("wadInfoCache", new Dictionary<string, WadInfo>());
    public static List<string> GetDataDirectories()
    {
        var data = new DataDirectoriesViewModel();
        data.LoadDirectories();
        return data.DataDirectories.Select(d => d.Path).Distinct().ToList();
    }
    public static void ClearWadCache()
    {
        WadInfoCache.Set(new Dictionary<string, WadInfo>());
        GetNewWadInfos();
    }

    public static string[] ValidWADFiles = [
        ".wad", ".pk3", ".zip" // TODO: Pk7. It's a dumb extension though. 
    ];

    public static List<string> GetAllWads()
    {
        var dataDirs = GetDataDirectories();
        // A file is considered a wad if it has a valid extension
        // A folder is considered a wad if it contains a `zscript.*` file.
        // This is an improper way to detect folders, but there is no better way currently.

        List<string> wadPaths = new();

        foreach (var dir in dataDirs)
        {
            if (!Directory.Exists(dir))
                continue;

            try
            {
                var files = Directory.GetFiles(dir, "*", SearchOption.TopDirectoryOnly);
                foreach (var file in files)
                {
                    var ext = Path.GetExtension(file).ToLowerInvariant();
                    if (ValidWADFiles.Contains(ext))
                    {
                        wadPaths.Add(file);
                    }
                }

                var directories = Directory.GetDirectories(dir, "*", SearchOption.TopDirectoryOnly);
                foreach (var folder in directories)
                {
                    var zscriptFiles = Directory.GetFiles(folder, "zscript.*", SearchOption.TopDirectoryOnly);
                    if (zscriptFiles.Length > 0)
                    {
                        wadPaths.Add(folder);
                    }
                }
            }
            catch
            {
                // Ignore directories we can't access
                Console.Error.WriteLine($"Could not access directory: {dir}");
            }
        }

        return wadPaths.Distinct().ToList();
    }

    public static void GetNewWadInfos()
    {
        var wads = GetAllWads();
        foreach (var wad in wads)
            GetWadInfo(wad);
    }

    private static Dictionary<string, string>? _iwadNamesCache = null;
    private static readonly Regex IWADNameRegex = new Regex(@"\bIWADName\s*=\s*""([^""]+)""", RegexOptions.Compiled);
    private static readonly Regex NameRegex = new Regex(@"\bName\s*=\s*""([^""]+)""", RegexOptions.Compiled);
    public static Dictionary<string, string> GetIWADNames() {
        if (_iwadNamesCache != null)
            return _iwadNamesCache;


        // Check all datadirectories for an archive called game_support.pk3
        // And grab the iwadwadinfo.txt out of it.
        var iwadNames = new Dictionary<string, string>();
        var dataDirs = GetDataDirectories();
        string? gameSupportPK3 = null;
        foreach (var dir in dataDirs){
            try
            {
                var pk3Path = Path.Combine(dir, "game_support.pk3");
                if (File.Exists(pk3Path))
                {
                    gameSupportPK3 = pk3Path;
                    break;
                }
            }
            catch
            {
                // Ignore errors reading the archive
                Console.Error.WriteLine($"Could not access directory: {dir}");
                return new();
            }
        }
        if (gameSupportPK3 == null){
            Console.Error.WriteLine("No game_support.pk3 found in data directories.");
            return new(); // No game_support.pk3 found. Return empty dictionary.
            // IMPORTANT, don't cache this version so we can update it with new added data directories.
        }
        try
        {
            using var archive = System.IO.Compression.ZipFile.OpenRead(gameSupportPK3);
            var iwadInfoEntry = archive.Entries.FirstOrDefault(e => e.FullName.Equals("iwadinfo.txt", StringComparison.OrdinalIgnoreCase));
            if (iwadInfoEntry != null)
            {
                using var reader = new StreamReader(iwadInfoEntry.Open());
                // Wad entries look like:
                /*
                IWad
                {
                    Name = "Freedoom: Phase 2"
                    Autoname = "doom.freedoom.phase2"
                    Game = "Doom"
                    Config = "Doom"
                    IWADName = "freedoom2.wad"
                    Mapinfo = "mapinfo/doom2.txt"
                    MustContain = "MAP01", "FREEDOOM"
                    BannerColors = "32 54 43", "c6 dc d1"
                    SkipBexStringsIfLanguage
                }
                */
                // For each of these, extract the IWADName and Name fields.
                string? currentIWADName = null;
                string? currentName = null;
                while (!reader.EndOfStream){
                    var line = reader.ReadLine()?.Trim();
                    if (line == null)
                        continue;
                    if (line.StartsWith("IWad")){
                        // Find the next line starting with {
                        while(!reader.EndOfStream){
                            line = reader.ReadLine()?.Trim();
                            if (line == "{")
                                break;
                        }
                        // Iterate through each line until we find }
                        // if it's a Name = "value" or IWADName = "value", store it.
                        while(!reader.EndOfStream){
                            line = reader.ReadLine()?.Trim();
                            if (line == null)
                                continue;
                            if (line == "}")
                                break;
                            var iwadMatch = IWADNameRegex.Match(line);
                            if (iwadMatch.Success){
                                currentIWADName = iwadMatch.Groups[1].Value;
                            }
                            var nameMatch = NameRegex.Match(line);
                            if (nameMatch.Success){
                                currentName = nameMatch.Groups[1].Value;
                            }
                        }
                        if (currentIWADName != null && currentName != null){
                            iwadNames[currentIWADName.ToLowerInvariant()] = currentName;
                        }else{
                            Console.Error.WriteLine("Malformed IWAD entry in iwadinfo.txt");
                            Console.Error.WriteLine($"IWADName: {currentIWADName}, Name: {currentName}");
                        }
                        currentIWADName = null;
                        currentName = null;
                    }
                }
            }else{
                Console.Error.WriteLine($"No iwadinfo.txt found in {gameSupportPK3}");
                return new(); // No iwadinfo.txt found. Return empty dictionary.
            }
            // Assuming all went well, cache the result
            _iwadNamesCache = iwadNames;
            return iwadNames;
        }
        catch
        {
            // Ignore errors reading the archive
            Console.Error.WriteLine("Could not read game_support.pk3 for IWAD names.");
        }
        return new();
    }

    public static WadInfo GetWadInfo(string path)
    {
        var cache = WadInfoCache.Get();
        if (cache.ContainsKey(path))
        {
            return cache[path];
        }

        bool IsPatch = true;
        bool HasMaps = false;

        // Depending on the file type, we may be able to determine if it's an IWAD or PWAD
        var ext = Path.GetExtension(path).ToLowerInvariant();
        switch (ext) {
            case "":
                // Folder - assume PWAD
                IsPatch = true;
                // Check if there's a /maps/ folder
                var mapsDir = System.IO.Path.Combine(path, "maps");;
                if (Directory.Exists(mapsDir))
                {
                    // Check if any file/folder called MAP01 or E1M1 (Optionally .wad) exists
                    var mapEntries = Directory.GetFileSystemEntries(mapsDir, "*", SearchOption.TopDirectoryOnly);
                    foreach (var entry in mapEntries)
                    {
                        var mapName = Path.GetFileNameWithoutExtension(entry).ToUpperInvariant();
                        if (mapName == "MAP01" || mapName == "E1M1")
                        {
                            HasMaps = true;
                            break;
                        }
                    }
                }
                break;
            case ".wad":
                // WAD file - read header
                try
                {
                    using var fs = new FileStream(path, FileMode.Open, FileAccess.Read);
                    using var br = new BinaryReader(fs);
                    var id = new string(br.ReadChars(4));
                    if (id == "IWAD"){
                        IsPatch = false;
                        HasMaps = true; // IWADs always have maps. Hopefully.
                    }else{
                        // Search for a lump named "MAP01" to "MAP99" or "E1M1" to "E4M9"
                        var numLumps = br.ReadInt32(); // numlumps
                        var dirOffset = br.ReadInt32(); // infotableofs
                        fs.Seek(dirOffset, SeekOrigin.Begin);
                        for (int i = 0; i < numLumps; i++){
                            var lumpOffset = br.ReadInt32();
                            var lumpSize = br.ReadInt32();
                            var lumpNameChars = br.ReadChars(8);
                            var lumpName = new string(lumpNameChars).TrimEnd('\0').ToUpperInvariant();
                            if (lumpName == "MAP01" || lumpName == "E1M1"){
                                HasMaps = true;
                                break;
                            }
                        }
                    }
                }
                catch
                {
                    // Ignore errors reading the file
                    Console.Error.WriteLine($"Could not read WAD file: {path}");
                }
                break;
            case ".pk3":
            case ".zip":
            case ".pk7":
                // No such thing as an IWAD in these formats, right?
                IsPatch = true;
                // PK3/ZIP/PK7 - check for maps inside the archive
                // Use the same "Is there a /maps/ folder" logic
                try
                {
                    using var archive = System.IO.Compression.ZipFile.OpenRead(path);
                    var mapsEntry = archive.Entries.FirstOrDefault(e => e.FullName.StartsWith("maps/") || e.FullName.StartsWith("Maps/"));
                    if (mapsEntry != null)
                    {
                        // Check for children in mapEntry named MAP01 or E1M1 inside the maps folder
                        var mapEntries = archive.Entries.Where(e => e.FullName.StartsWith(mapsEntry.FullName));
                        foreach (var entry in mapEntries)
                        {
                            var mapName = Path.GetFileNameWithoutExtension(entry.Name).ToUpperInvariant();
                            if (mapName == "MAP01" || mapName == "E1M1")
                            {
                                HasMaps = true;
                                break;
                            }
                        }
                    }
                }
                catch
                {
                    // Ignore errors reading the archive
                    Console.Error.WriteLine($"Could not read archive file: {path}");
                }
                break;
            default:
                // Unknown file type - assume PWAD and hope for the best.
                IsPatch = true;
                break;
        }


        var name = Path.GetFileName(path);
        if (!IsPatch){
            var iwadNames = GetIWADNames();
            var key = Path.GetFileName(path).ToLowerInvariant();
            Console.WriteLine($"Getting IWAD name for {key}");
            if (iwadNames.ContainsKey(key)){
                name = iwadNames[key];
                Console.WriteLine($"Found IWAD name for {key}: {name}");
            }else{
                Console.WriteLine($"No IWAD name found for {path}, using filename.");
            }
        }

        var info = new WadInfo
        {
            Path = path,
            Name = name,
            IsPatch = IsPatch,
            HasMaps = HasMaps
        };

        cache[path] = info;
        WadInfoCache.Set(cache);

        return info;
    }

    public static IEnumerable<WadInfo> IWADs => WadInfoCache.Get().Values.Where(c=>!c.IsPatch);

}

// Stores information about a wad. Serializable to be stored in config.
public class WadInfo
{
    public string Path { get; set; } = string.Empty; // Full path to the wad file
    public string Name { get; set; } = string.Empty; // Name of the wad

    public bool IsPatch { get; set; } // Whether this wad is an IWAD or a PWAD
    public bool HasMaps { get; set; } // Whether this wad contains maps

    [System.Text.Json.Serialization.JsonIgnore]
    public SelectWadInfo Info => new(){
        Path = this.Path,
        DisplayName = this.Name,
        HasMaps = this.HasMaps
    };
}
