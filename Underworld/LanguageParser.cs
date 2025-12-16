using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using CsvHelper;
using CsvHelper.Configuration.Attributes;
using Tmds.DBus.Protocol;
using Underworld.Models;

namespace Underworld;

public static class LanguageParser
{
    public static Dictionary<string, string>? ParseLanguage(VirtualFileSystem vfs)
    {
        var langCSV = vfs.GetFile("language.csv");
        var langLumps = vfs.GetAllLumps("LANGUAGE");

        if (langCSV is not null){
            using var reader = new StreamReader(new MemoryStream(langCSV));
            return ParseLanguageCSV(reader);
        }else if (langLumps.Count > 0){
            Dictionary<string, string> combined = new();
            foreach (var lump in langLumps)
            {
                using var reader = new StreamReader(new MemoryStream(lump));
                var parsed = ParseLanguageLump(reader);
                foreach (var kvp in parsed)
                {
                    combined[kvp.Key] = kvp.Value;
                }
            }
            return combined;
        }
        return null;
    }

    public static Dictionary<string, string> ParseLanguageCSV(StreamReader reader)
    {
        using var csv = new CsvReader(reader, CultureInfo.InvariantCulture);
        var records = csv.GetRecords<LanguageEntry>();
        var dict = new Dictionary<string, string>();
        foreach (var record in records)
        {
            dict[record.Identifier] = record.Default;
        }
        return dict;
    }

    public static Dictionary<string, string> ParseLanguageLump(StreamReader reader)
    {
        var allLangs = LangTableParser.Parse(reader);
        // Get one combined list of all languages, preferring "eng", then "enu", then default.
        var dict = new Dictionary<string, string>();
        if (allLangs.TryGetValue("default", out var defaultLang))
        {
            foreach (var kvp in defaultLang)
            {
                dict[kvp.Key] = kvp.Value;
            }
        }
        if (allLangs.TryGetValue("enu", out var enuLang))
        {
            foreach (var kvp in enuLang)    
            {
                dict[kvp.Key] = kvp.Value;
            }
        }
        if (allLangs.TryGetValue("eng", out var engLang))
        {
            foreach (var kvp in engLang)    
            {
                dict[kvp.Key] = kvp.Value;
            }
        }

        // Try to solve any $INDEX placeholders
        foreach(var key in dict.Keys.ToList())
        {
            var value = dict[key];
            if(!value.StartsWith("$"))
                continue;
            int translateAttempts = 0;
            while (value.StartsWith("$") && translateAttempts < 10)
            {
                translateAttempts++;
                var indexKey = value[1..];
                if (dict.TryGetValue(indexKey, out var indexedValue))
                {
                    value = indexedValue;
                }
                else if (WadLists.GLOBAL_LANGUAGE != null && WadLists.GLOBAL_LANGUAGE.TryGetValue(indexKey, out var globalIndexedValue))
                {
                    value = globalIndexedValue;
                }
                else
                {
                    break;
                }
            }
            dict[key] = value;
        }
        return dict;
    }

    private class LanguageEntry
    {
        [Name("default")]
        public string Default { get; set; } = string.Empty;
        [Name("Identifier")]
        public string Identifier { get; set; } = string.Empty;
    }
}


public static class LangTableParser
{
    /// <summary>
    /// Parses a custom C-style language/string table file.
    /// Returns: dict[language][key] = value
    /// Supports multiple language tags per header: [enu de default]
    /// Supports // line comments, semicolon-terminated entries, and adjacent string literal concatenation.
    /// </summary>
    public static Dictionary<string, Dictionary<string, string>> Parse(StreamReader reader)
    {
        if (reader == null) throw new ArgumentNullException(nameof(reader));

        var result = new Dictionary<string, Dictionary<string, string>>(StringComparer.OrdinalIgnoreCase);

        List<string> currentLangs = new();
        string? pendingKey = null;
        var pendingValue = new StringBuilder();

        while (!reader.EndOfStream)
        {
            var rawLine = reader.ReadLine();
            if (rawLine == null) break;

            var line = StripLineComment(rawLine).Trim();
            if (line.Length == 0) continue;

            // Language header: [enu de default]
            if (pendingKey == null && TryParseLangHeader(line, out var langsFromHeader))
            {
                currentLangs = langsFromHeader;
                EnsureLanguageDictionaries(result, currentLangs);
                continue;
            }

            if (currentLangs.Count == 0)
                throw new FormatException("Encountered definitions before any [lang] header.");

            // If we're not currently inside a value, look for KEY =
            if (pendingKey == null)
            {
                int eq = line.IndexOf('=');
                if (eq < 0)
                    continue; // tolerate junk/blank lines

                pendingKey = line[..eq].Trim();
                if (pendingKey.Length == 0)
                    throw new FormatException("Empty key before '='.");

                pendingValue.Clear();

                // Parse any string literals that might appear on same line after '='
                var afterEq = line[(eq + 1)..].Trim();
                ConsumeStringLiteralsAndMaybeFinish(afterEq, pendingValue, out bool finished);

                if (finished)
                {
                    Commit(result, currentLangs, pendingKey, pendingValue.ToString());
                    pendingKey = null;
                    pendingValue.Clear();
                }
            }
            else
            {
                // We are inside a value; this line may add more adjacent string literals and/or end with ';'
                ConsumeStringLiteralsAndMaybeFinish(line, pendingValue, out bool finished);

                if (finished)
                {
                    Commit(result, currentLangs, pendingKey, pendingValue.ToString());
                    pendingKey = null;
                    pendingValue.Clear();
                }
            }
        }

        if (pendingKey != null)
            throw new FormatException($"Unterminated value for key '{pendingKey}' (missing ';').");

        return result;
    }

    private static void EnsureLanguageDictionaries(
        Dictionary<string, Dictionary<string, string>> result,
        List<string> langs)
    {
        foreach (var lang in langs)
        {
            if (!result.ContainsKey(lang))
                result[lang] = new Dictionary<string, string>(StringComparer.Ordinal);
        }
    }

    private static void Commit(
        Dictionary<string, Dictionary<string, string>> result,
        List<string> langs,
        string key,
        string value)
    {
        // "Copy" into each language dictionary.
        foreach (var lang in langs)
        {
            // Language dict should already exist, but be defensive.
            if (!result.TryGetValue(lang, out var langDict))
            {
                langDict = new Dictionary<string, string>(StringComparer.Ordinal);
                result[lang] = langDict;
            }

            langDict[key] = value;
        }
    }

    private static bool TryParseLangHeader(string line, out List<string> langs)
    {
        langs = new List<string>();
        if (line.Length < 2 || line[0] != '[') return false;

        int close = line.IndexOf(']');
        if (close < 0) return false;

        var inner = line[1..close].Trim();
        if (inner.Length == 0) return false;

        var parts = inner.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries);
        foreach (var p in parts)
        {
            var tag = p.Trim();
            if (tag.Length > 0)
                langs.Add(tag);
        }

        return langs.Count > 0;
    }

    private static string StripLineComment(string line)
    {
        // Remove // comments, but do not treat // inside a string literal as a comment.
        // We'll scan and track whether we're inside a string.
        bool inString = false;
        bool escaped = false;

        for (int i = 0; i < line.Length - 1; i++)
        {
            char c = line[i];

            if (inString)
            {
                if (escaped)
                {
                    escaped = false;
                    continue;
                }

                if (c == '\\')
                {
                    escaped = true;
                    continue;
                }

                if (c == '"')
                {
                    inString = false;
                    continue;
                }
            }
            else
            {
                if (c == '"')
                {
                    inString = true;
                    continue;
                }

                if (c == '/' && line[i + 1] == '/')
                {
                    return line.Substring(0, i);
                }
            }
        }

        return line;
    }

    private static void ConsumeStringLiteralsAndMaybeFinish(
        string text,
        StringBuilder accum,
        out bool finished)
    {
        finished = false;
        int i = 0;

        while (i < text.Length)
        {
            // Skip whitespace
            while (i < text.Length && char.IsWhiteSpace(text[i])) i++;
            if (i >= text.Length) break;

            char c = text[i];

            if (c == ';')
            {
                finished = true;
                return;
            }

            if (c == '"')
            {
                i++; // skip opening quote
                var literal = new StringBuilder();

                bool escaped = false;
                while (i < text.Length)
                {
                    char ch = text[i++];

                    if (escaped)
                    {
                        literal.Append(UnescapeChar(ch, text, ref i));
                        escaped = false;
                        continue;
                    }

                    if (ch == '\\')
                    {
                        escaped = true;
                        continue;
                    }

                    if (ch == '"')
                        break;

                    literal.Append(ch);
                }

                // If we ended due to i == length without closing quote, tolerate and treat as error
                if (i > text.Length)
                    throw new FormatException("Unterminated string literal.");

                accum.Append(literal.ToString());
                continue;
            }

            // If there are non-string tokens, we mostly ignore them except for ';'
            // e.g. "KEY = "value";" or trailing stuff.
            i++;
        }

        // If we got here, we did not hit ';'
        finished = false;
    }

    private static char UnescapeChar(char esc, string text, ref int index)
    {
        // Supports common C escapes plus \uXXXX and \xHH (1-2 hex digits).
        return esc switch
        {
            'n' => '\n',
            'r' => '\r',
            't' => '\t',
            '\\' => '\\',
            '"' => '"',
            '0' => '\0',
            'a' => '\a',
            'b' => '\b',
            'f' => '\f',
            'v' => '\v',
            'u' => ParseHexFixed(text, ref index, 4),
            'x' => ParseHexVariable(text, ref index, 2),
            _ => esc
        };
    }

    private static char ParseHexFixed(string text, ref int index, int digits)
    {
        int value = 0;
        int taken = 0;

        while (taken < digits && index < text.Length)
        {
            int hex = HexVal(text[index]);
            if (hex < 0) break;
            value = (value << 4) | hex;
            index++;
            taken++;
        }

        if (taken == 0)
            return '\0';

        return (char)value;
    }

    private static char ParseHexVariable(string text, ref int index, int maxDigits)
    {
        int value = 0;
        int taken = 0;

        while (taken < maxDigits && index < text.Length)
        {
            int hex = HexVal(text[index]);
            if (hex < 0) break;
            value = (value << 4) | hex;
            index++;
            taken++;
        }

        if (taken == 0)
            return '\0';

        return (char)value;
    }

    private static int HexVal(char c)
    {
        if (c >= '0' && c <= '9') return c - '0';
        if (c >= 'a' && c <= 'f') return 10 + (c - 'a');
        if (c >= 'A' && c <= 'F') return 10 + (c - 'A');
        return -1;
    }
}

