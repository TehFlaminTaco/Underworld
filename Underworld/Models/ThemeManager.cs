using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Text.Json;
using Avalonia;
using Avalonia.Media;

namespace Underworld.Models;

/// <summary>
/// Manages application themes by loading JSON definitions from the executable's /themes directory.
/// Provides theme switching functionality and persists user theme preference.
/// </summary>
public static class ThemeManager
{
    private const string ThemesFolderName = "themes";
    private const string ThemeFilePattern = "*.uw-theme.json";

    private static readonly ConfigEntry<string> _themeConfig = Config.Setup("Theme", string.Empty);
    private static readonly Dictionary<string, ThemeDefinition> _themesById = new(StringComparer.OrdinalIgnoreCase);
    private static readonly List<ThemeDefinition> _orderedThemes = new();
    private static ReadOnlyCollection<ThemeDefinition>? _cachedReadOnlyThemes;
    private static readonly JsonSerializerOptions _serializerOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        ReadCommentHandling = JsonCommentHandling.Skip,
        AllowTrailingCommas = true
    };
    private static readonly object _syncRoot = new();
    private static bool _themesLoaded;
    private static string _currentThemeId = string.Empty;

    /// <summary>
    /// Event raised when the theme changes.
    /// </summary>
    public static event EventHandler<ThemeDefinition>? ThemeChanged;

    /// <summary>
    /// Gets the theme that is currently active.
    /// </summary>
    public static ThemeDefinition CurrentTheme
    {
        get
        {
            EnsureThemesLoaded();

            if (string.IsNullOrWhiteSpace(_currentThemeId) || !_themesById.ContainsKey(_currentThemeId))
            {
                _currentThemeId = _orderedThemes.First().Id;
            }

            return _themesById[_currentThemeId];
        }
    }

    /// <summary>
    /// Provides the list of themes that were discovered from disk.
    /// </summary>
    public static IReadOnlyList<ThemeDefinition> AvailableThemes
    {
        get
        {
            EnsureThemesLoaded();
            return _cachedReadOnlyThemes ??= new ReadOnlyCollection<ThemeDefinition>(_orderedThemes);
        }
    }

    /// <summary>
    /// Gets the absolute path to the application's themes directory.
    /// </summary>
    public static string GetThemesDirectoryPath() => Path.Combine(AppContext.BaseDirectory, ThemesFolderName);

    /// <summary>
    /// Reloads theme definitions from disk and reapplies the current theme.
    /// </summary>
    public static IReadOnlyList<ThemeDefinition> ReloadThemes()
    {
        lock (_syncRoot)
        {
            LoadThemesFromDisk();
            _themesLoaded = true;
        }

        var themeChanged = false;
        if (string.IsNullOrWhiteSpace(_currentThemeId) || !_themesById.ContainsKey(_currentThemeId))
        {
            _currentThemeId = _orderedThemes.First().Id;
            _themeConfig.Set(_currentThemeId);
            themeChanged = true;
        }

        UpdateApplicationResources();

        if (themeChanged)
        {
            ThemeChanged?.Invoke(null, _themesById[_currentThemeId]);
        }

        return AvailableThemes;
    }

    /// <summary>
    /// Initializes the theme system by loading the saved theme preference.
    /// </summary>
    public static void Initialize()
    {
        EnsureThemesLoaded();

        var savedTheme = _themeConfig.Get();
        var resolvedId = ResolveThemeId(savedTheme) ?? _orderedThemes.First().Id;
        _currentThemeId = resolvedId;
    }

    /// <summary>
    /// Gets the color definitions for the current theme.
    /// </summary>
    public static IReadOnlyDictionary<string, Color> GetCurrentThemeColors() => CurrentTheme.Colors;

    /// <summary>
    /// Gets the color definitions for a specific theme id or alias.
    /// </summary>
    public static IReadOnlyDictionary<string, Color> GetThemeColors(string themeId)
    {
        EnsureThemesLoaded();

        var resolvedId = ResolveThemeId(themeId)
            ?? throw new InvalidOperationException($"Theme '{themeId}' was not found.");

        return _themesById[resolvedId].Colors;
    }

    /// <summary>
    /// Attempts to find a theme definition using its id, display name, or any alias.
    /// Returns null when the theme cannot be resolved.
    /// </summary>
    public static ThemeDefinition? FindTheme(string? idOrAlias)
    {
        EnsureThemesLoaded();

        var resolvedId = ResolveThemeId(idOrAlias);
        return resolvedId == null ? null : _themesById[resolvedId];
    }

    /// <summary>
    /// Switches to the specified theme and updates application resources.
    /// </summary>
    public static void SetTheme(string themeId)
    {
        EnsureThemesLoaded();

        var resolvedId = ResolveThemeId(themeId);
        if (resolvedId == null)
        {
            Console.WriteLine($"[ThemeManager] Theme '{themeId}' could not be resolved. Request ignored.");
            return;
        }

        if (string.Equals(resolvedId, _currentThemeId, StringComparison.OrdinalIgnoreCase))
        {
            Console.WriteLine("[ThemeManager] Theme unchanged, skipping update.");
            return;
        }

        _currentThemeId = resolvedId;
        _themeConfig.Set(resolvedId);
        Console.WriteLine($"[ThemeManager] Theme set to '{resolvedId}'.");

        UpdateApplicationResources();

        ThemeChanged?.Invoke(null, _themesById[resolvedId]);
        Console.WriteLine("[ThemeManager] ThemeChanged event fired.");
    }

    /// <summary>
    /// Switches to the specified theme and updates application resources.
    /// </summary>
    public static void SetTheme(ThemeDefinition theme) => SetTheme(theme.Id);

    /// <summary>
    /// Toggles to the next available theme.
    /// </summary>
    public static void ToggleTheme()
    {
        EnsureThemesLoaded();

        if (_orderedThemes.Count <= 1)
        {
            return;
        }

        var currentIndex = _orderedThemes.FindIndex(t => t.Id.Equals(_currentThemeId, StringComparison.OrdinalIgnoreCase));
        if (currentIndex < 0)
        {
            currentIndex = 0;
        }

        var nextTheme = _orderedThemes[(currentIndex + 1) % _orderedThemes.Count];
        SetTheme(nextTheme.Id);
    }

    /// <summary>
    /// Applies the current theme to application resources (called during startup)
    /// </summary>
    public static void ApplyTheme()
    {
        EnsureThemesLoaded();
        UpdateApplicationResources();
    }

    private static void UpdateApplicationResources()
    {
        Console.WriteLine("[ThemeManager] UpdateApplicationResources started");

        if (Application.Current == null)
        {
            Console.WriteLine("[ThemeManager] ERROR: Application.Current is null!");
            return;
        }

        var colors = CurrentTheme.Colors;
        var resources = Application.Current.Resources;
        Console.WriteLine($"[ThemeManager] Updating {colors.Count} theme colors...");

        foreach (var (key, color) in colors)
        {
            var colorKey = $"{key}Color";
            var brushKey = $"{key}Brush";

            resources[colorKey] = color;
            resources[brushKey] = new SolidColorBrush(color);
        }

        Console.WriteLine("[ThemeManager] UpdateApplicationResources completed successfully");
    }

    private static void EnsureThemesLoaded()
    {
        if (_themesLoaded)
        {
            return;
        }

        lock (_syncRoot)
        {
            if (_themesLoaded)
            {
                return;
            }

            LoadThemesFromDisk();
            _themesLoaded = true;
        }
    }

    private static void LoadThemesFromDisk()
    {
        var directory = GetThemesDirectoryPath();
        Console.WriteLine($"[ThemeManager] Loading theme definitions from '{directory}'");

        if (!Directory.Exists(directory))
        {
            throw new InvalidOperationException($"Theme directory '{directory}' does not exist.");
        }

        var themeFiles = Directory.EnumerateFiles(directory, ThemeFilePattern, SearchOption.TopDirectoryOnly).ToList();
        if (themeFiles.Count == 0)
        {
            throw new InvalidOperationException($"No theme JSON files found in '{directory}'.");
        }

        _themesById.Clear();
        _orderedThemes.Clear();

        foreach (var file in themeFiles)
        {
            var json = File.ReadAllText(file);
            var model = JsonSerializer.Deserialize<ThemeFileModel>(json, _serializerOptions)
                ?? throw new InvalidOperationException($"Unable to deserialize theme file '{Path.GetFileName(file)}'.");

            if (string.IsNullOrWhiteSpace(model.Id))
            {
                throw new InvalidOperationException($"Theme file '{Path.GetFileName(file)}' is missing an 'id' property.");
            }

            if (model.Colors == null || model.Colors.Count == 0)
            {
                throw new InvalidOperationException($"Theme '{model.Id}' must define at least one color.");
            }

            var parsedColors = new Dictionary<string, Color>(StringComparer.OrdinalIgnoreCase);
            foreach (var (key, value) in model.Colors)
            {
                if (string.IsNullOrWhiteSpace(value))
                {
                    throw new InvalidOperationException($"Theme '{model.Id}' color '{key}' is empty.");
                }

                parsedColors[key] = Color.Parse(value);
            }

            var definition = new ThemeDefinition(
                model.Id.Trim(),
                model.Name?.Trim() ?? model.Id.Trim(),
                model.Description?.Trim() ?? string.Empty,
                parsedColors,
                model.SortOrder,
                model.Aliases ?? Array.Empty<string>());

            _themesById[definition.Id] = definition;
        }

        _orderedThemes.AddRange(_themesById.Values
            .OrderBy(t => t.SortOrder)
            .ThenBy(t => t.Name, StringComparer.OrdinalIgnoreCase));

        _cachedReadOnlyThemes = new ReadOnlyCollection<ThemeDefinition>(_orderedThemes);

        if (string.IsNullOrWhiteSpace(_currentThemeId) || !_themesById.ContainsKey(_currentThemeId))
        {
            _currentThemeId = _orderedThemes.First().Id;
        }
    }

    private static string? ResolveThemeId(string? idOrAlias)
    {
        if (string.IsNullOrWhiteSpace(idOrAlias))
        {
            return null;
        }

        foreach (var theme in _orderedThemes)
        {
            if (theme.Matches(idOrAlias))
            {
                return theme.Id;
            }
        }

        return null;
    }

    private sealed class ThemeFileModel
    {
        public string? Id { get; set; }
        public string? Name { get; set; }
        public string? Description { get; set; }
        public int SortOrder { get; set; }
        public Dictionary<string, string>? Colors { get; set; }
        public string[]? Aliases { get; set; }
    }
}

public sealed record ThemeDefinition(
    string Id,
    string Name,
    string Description,
    IReadOnlyDictionary<string, Color> Colors,
    int SortOrder,
    IReadOnlyList<string> Aliases)
{
    public bool Matches(string candidate)
    {
        if (string.IsNullOrWhiteSpace(candidate))
        {
            return false;
        }

        if (string.Equals(Id, candidate, StringComparison.OrdinalIgnoreCase) ||
            string.Equals(Name, candidate, StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        return Aliases.Any(alias => string.Equals(alias, candidate, StringComparison.OrdinalIgnoreCase));
    }
}
