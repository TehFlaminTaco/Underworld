using System.Collections.Generic;
using System.Text.RegularExpressions;
using Underworld.Models;

namespace Underworld;

public static class MapInfoParser
{
    private static readonly Regex IncludeRegex = new(@"^\s*include\s+([^\r\n]+?)\s*$", RegexOptions.IgnoreCase | RegexOptions.Compiled | RegexOptions.Multiline);
    private static string GetWADInfoString(VirtualFileSystem vfs, byte[] data, int recurseDepth = 0)
    {
        var mapinfoText = System.Text.Encoding.UTF8.GetString(data);
        if (recurseDepth < 10)
        {
            // I couldn't actually find a single WAD that uses includes, but they SHOULD be supported.
            Match match;
            while((match = IncludeRegex.Match(mapinfoText)).Success)
            {
                string includePath = match.Groups[1].Value.Trim().Trim('\"', '\'');
                if (vfs.TryGetFile(includePath, out var includeData))
                {
                    var includeText = GetWADInfoString(vfs, includeData, recurseDepth + 1);
                    mapinfoText = mapinfoText.Replace(match.Value, includeText);
                }
                else
                {
                    // Remove the include line if the file is not found
                    mapinfoText = mapinfoText.Replace(match.Value, string.Empty);
                }
            }
        }
        return mapinfoText;
    }

    public static readonly Regex MapEntryRegex = new(@"^map\s+(\w{1,8})\s+(lookup)?\s*(""[^""]*""|\w+)\s*[^{]*?\{", RegexOptions.IgnoreCase | RegexOptions.Compiled | RegexOptions.Multiline);
    public static Dictionary<string, string>? ParseMapInfo(VirtualFileSystem vfs, byte[] mapinfoLump)
    {
        Dictionary<string, string>? languageEntries = null;
        string mapinfoText = GetWADInfoString(vfs, mapinfoLump);
        // God help us all, parse the MAPINFO text for map names
        // Actually, Parsing is for Chumps. Let's crack the Regex out.
        var mapNames = new Dictionary<string, string>();
        var matches = MapEntryRegex.Matches(mapinfoText);
        foreach(Match match in matches)
        {
            var lumpName = match.Groups[1].Value.ToUpperInvariant();
            var wasLookup = match.Groups[2].Success;
            var displayNameRaw = match.Groups[3].Value.Trim('"', ' ');
            if(wasLookup)
            {
                languageEntries ??= LanguageParser.ParseLanguage(vfs);
                displayNameRaw = "$" + displayNameRaw;
                int translateAttempts = 0;
                while(displayNameRaw.StartsWith("$") && translateAttempts < 10)
                {
                    translateAttempts++;
                    var lookupKey = displayNameRaw.Substring(1);
                    if (languageEntries?.TryGetValue(lookupKey, out var lookedUpValue)??false)
                    {
                        displayNameRaw = lookedUpValue;
                    }
                    else if (WadLists.GLOBAL_LANGUAGE.TryGetValue(lookupKey, out var globalLookedUpValue))
                    {
                        displayNameRaw = globalLookedUpValue;
                    }
                    else
                    {
                        break;
                    }
                }
            }
            mapNames[lumpName] = displayNameRaw;
        }
        return mapNames;
    }

    public static readonly Regex UMapEntryRegex = new(@"MAP\s+(\w{1,8})\s*\{[^}]*levelname\s*=\s*(""[^""]+""|\w*?)\s", RegexOptions.IgnoreCase | RegexOptions.Compiled | RegexOptions.Multiline);
    public static Dictionary<string, string>? ParseUMapInfo(VirtualFileSystem vfs, byte[] umapinfoLump)
    {
        // Similar to MAPINFO but slightly different syntax.
        // No Includes either, thankfully.
        string umapinfoText = System.Text.Encoding.UTF8.GetString(umapinfoLump);
        var mapNames = new Dictionary<string, string>();

        var matches = UMapEntryRegex.Matches(umapinfoText);
        foreach(Match match in matches)
        {
            var lumpName = match.Groups[1].Value.ToUpperInvariant();
            var displayNameRaw = match.Groups[2].Value.Trim('"', ' ');
            mapNames[lumpName] = displayNameRaw;
        }
        return mapNames;
    }

    public static readonly Regex EMapEntryRegex = new(@"\s*\[(\w{1,8})\][^[]*?levelname\s*=\s*(.*?)\s*(?:\r?\n|$)", RegexOptions.IgnoreCase | RegexOptions.Compiled | RegexOptions.Multiline);
    public static Dictionary<string, string>? ParseEMapInfo(VirtualFileSystem vfs, byte[] emapinfoLump)
    {
        // Most different of them all
        // Looks like:
        // [MAP01]
        // levelname = Evil Hell Lab
        // Sometimes with leading whitespace
        string emapinfoText = System.Text.Encoding.UTF8.GetString(emapinfoLump);
        var mapNames = new Dictionary<string, string>();

        var matches = EMapEntryRegex.Matches(emapinfoText);
        foreach(Match match in matches)
        {
            var lumpName = match.Groups[1].Value.ToUpperInvariant();
            var displayNameRaw = match.Groups[2].Value.Trim();
            mapNames[lumpName] = displayNameRaw;
        }
        return mapNames;
    }
}