using System;
using System.Collections.Generic;
using Avalonia;
using Avalonia.Media;

namespace Underworld.Models;

/// <summary>
/// Manages application themes (Dark and Light) with hardcoded color definitions.
/// Provides theme switching functionality and persists user theme preference.
/// </summary>
public static class ThemeManager
{
    public enum Theme
    {
        Dark,
        Light
    }

    private static Theme _currentTheme = Theme.Dark;
    private static readonly ConfigEntry<string> _themeConfig = Config.Setup("Theme", "Dark");

    /// <summary>
    /// Event raised when the theme changes
    /// </summary>
    public static event EventHandler<Theme>? ThemeChanged;

    /// <summary>
    /// Gets the current active theme
    /// </summary>
    public static Theme CurrentTheme => _currentTheme;

    /// <summary>
    /// Gets all available color definitions for the dark theme
    /// </summary
    public static Dictionary<string, Color> DarkThemeColors => new()
    {
        // Main backgrounds
        ["WindowBackground"] = Color.Parse("#1A1D22"),
        ["CardBackground"] = Color.Parse("#252931"),
        ["ControlBackground"] = Color.Parse("#3F3F46"),
        
        // Borders
        ["CardBorder"] = Color.Parse("#333842"),
        ["ControlBorder"] = Color.Parse("#555555"),
        
        // Text colors
        ["PrimaryText"] = Colors.White,
        ["SecondaryText"] = Color.Parse("#BFBFBF"),
        ["TertiaryText"] = Color.Parse("#808080"),
        ["MutedText"] = Color.Parse("#B0B6C0"),
        
        // Accent colors
        ["AccentOrange"] = Color.Parse("#FFB347"),
        ["PrimaryButton"] = Color.Parse("#FF4B2B"),
        ["PrimaryButtonForeground"] = Colors.White,
        ["DangerRed"] = Color.Parse("#E81123"),
        ["ScrollBarTrack"] = Color.Parse("#1F232A"),
        ["ScrollBarThumb"] = Color.Parse("#4B4F5A"),
        ["SelectionBackground"] = Color.Parse("#FFB347"),
        ["SelectionForeground"] = Color.Parse("#1A1D22"),
        
        // Title bar
        ["TitleBarHover"] = Color.Parse("#333842"),
        ["TitleBarPressed"] = Color.Parse("#252931"),
    };

    /// <summary>
    /// Gets all available color definitions for the light theme
    /// </summary>
    public static Dictionary<string, Color> LightThemeColors => new()
    {
        // Main backgrounds
        ["WindowBackground"] = Color.Parse("#F5F5F5"),
        ["CardBackground"] = Color.Parse("#FFFFFF"),
        ["ControlBackground"] = Color.Parse("#FAFAFA"),
        
        // Borders
        ["CardBorder"] = Color.Parse("#E0E0E0"),
        ["ControlBorder"] = Color.Parse("#CCCCCC"),
        
        // Text colors
        ["PrimaryText"] = Color.Parse("#1A1D22"),
        ["SecondaryText"] = Color.Parse("#404040"),
        ["TertiaryText"] = Color.Parse("#707070"),
        ["MutedText"] = Color.Parse("#505050"),
        
        // Accent colors
        ["AccentOrange"] = Color.Parse("#FF8C00"),
        ["PrimaryButton"] = Color.Parse("#D04A1E"),
        ["PrimaryButtonForeground"] = Colors.White,
        ["DangerRed"] = Color.Parse("#C50F1F"),
        ["ScrollBarTrack"] = Color.Parse("#E8E8E8"),
        ["ScrollBarThumb"] = Color.Parse("#C5C5C5"),
        ["SelectionBackground"] = Color.Parse("#FF8C00"),
        ["SelectionForeground"] = Color.Parse("#1A1D22"),
        
        // Title bar
        ["TitleBarHover"] = Color.Parse("#E5E5E5"),
        ["TitleBarPressed"] = Color.Parse("#D0D0D0"),
    };

    /// <summary>
    /// Initializes the theme system by loading the saved theme preference
    /// </summary>
    public static void Initialize()
    {
        var savedTheme = _themeConfig.Get();
        _currentTheme = Enum.TryParse<Theme>(savedTheme, true, out var theme) ? theme : Theme.Dark;
    }

    /// <summary>
    /// Gets the color definitions for the current theme
    /// </summary>
    public static Dictionary<string, Color> GetCurrentThemeColors()
    {
        return _currentTheme == Theme.Dark ? DarkThemeColors : LightThemeColors;
    }

    /// <summary>
    /// Gets the color definitions for a specific theme
    /// </summary>
    public static Dictionary<string, Color> GetThemeColors(Theme theme)
    {
        return theme == Theme.Dark ? DarkThemeColors : LightThemeColors;
    }

    /// <summary>
    /// Switches to the specified theme and updates application resources
    /// </summary>
    public static void SetTheme(Theme theme)
    {
        Console.WriteLine($"ThemeManager.SetTheme called with: {theme}");
        Console.WriteLine($"Current theme before change: {_currentTheme}");
        
        if (_currentTheme == theme)
        {
            Console.WriteLine("Theme unchanged, skipping update");
            return;
        }

        _currentTheme = theme;
        _themeConfig.Set(theme.ToString());
        Console.WriteLine($"Theme set to: {_currentTheme}");

        // Update application resources
        Console.WriteLine("Calling UpdateApplicationResources...");
        UpdateApplicationResources();
        Console.WriteLine("UpdateApplicationResources completed");

        // Notify listeners
        ThemeChanged?.Invoke(null, theme);
        Console.WriteLine("ThemeChanged event fired");
    }

    /// <summary>
    /// Toggles between dark and light themes
    /// </summary>
    public static void ToggleTheme()
    {
        SetTheme(_currentTheme == Theme.Dark ? Theme.Light : Theme.Dark);
    }

    /// <summary>
    /// Updates the Avalonia application resources with current theme colors
    /// </summary>
    private static void UpdateApplicationResources()
    {
        Console.WriteLine("UpdateApplicationResources started");
        
        if (Application.Current == null)
        {
            Console.WriteLine("ERROR: Application.Current is null!");
            return;
        }

        var colors = GetCurrentThemeColors();
        var resources = Application.Current.Resources;
        Console.WriteLine($"Updating {colors.Count} theme colors...");
        
        foreach (var (key, color) in colors)
        {
            var colorKey = $"{key}Color";
            var brushKey = $"{key}Brush";
            
            Console.WriteLine($"  Updating {key}: {color}");
            
            // Update color resource - this will update any DynamicResource bindings
            resources[colorKey] = color;
            
            // Update brush resource with new brush instance
            resources[brushKey] = new SolidColorBrush(color);
        }
        
        Console.WriteLine("UpdateApplicationResources completed successfully");
    }

    /// <summary>
    /// Applies the current theme to application resources (called during startup)
    /// </summary>
    public static void ApplyTheme()
    {
        UpdateApplicationResources();
    }
}
