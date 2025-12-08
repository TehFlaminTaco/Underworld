using System;
using Avalonia.Media;
using Underworld.Models;
using Xunit;
using Xunit.Abstractions;

namespace Underworld.Tests;

/// <summary>
/// Visual output tests for theme colors - helpful for documentation and debugging
/// </summary>
public class ThemeColorDisplayTests
{
    private readonly ITestOutputHelper _output;

    public ThemeColorDisplayTests(ITestOutputHelper output)
    {
        _output = output;
    }

    [Fact]
    public void DisplayDarkThemeColors()
    {
        _output.WriteLine("=== DARK THEME COLOR PALETTE ===");
        _output.WriteLine("");

        var darkColors = ThemeManager.DarkThemeColors;
        
        foreach (var (key, color) in darkColors)
        {
            _output.WriteLine($"{key,-20} : {ColorToHex(color),-10} RGB({color.R},{color.G},{color.B})");
        }
    }

    [Fact]
    public void DisplayLightThemeColors()
    {
        _output.WriteLine("=== LIGHT THEME COLOR PALETTE ===");
        _output.WriteLine("");

        var lightColors = ThemeManager.LightThemeColors;
        
        foreach (var (key, color) in lightColors)
        {
            _output.WriteLine($"{key,-20} : {ColorToHex(color),-10} RGB({color.R},{color.G},{color.B})");
        }
    }

    [Fact]
    public void DisplayColorDifferences()
    {
        _output.WriteLine("=== COLOR DIFFERENCES BETWEEN THEMES ===");
        _output.WriteLine("");

        var darkColors = ThemeManager.DarkThemeColors;
        var lightColors = ThemeManager.LightThemeColors;

        _output.WriteLine($"{"Color Key",-20} {"Dark Theme",-15} {"Light Theme",-15} {"Different",-10}");
        _output.WriteLine(new string('-', 65));

        foreach (var key in darkColors.Keys)
        {
            var darkColor = darkColors[key];
            var lightColor = lightColors[key];
            var isDifferent = !darkColor.Equals(lightColor);

            _output.WriteLine($"{key,-20} {ColorToHex(darkColor),-15} {ColorToHex(lightColor),-15} {(isDifferent ? "YES" : "NO"),-10}");
        }
    }

    [Fact]
    public void DisplayContrastRatios()
    {
        _output.WriteLine("=== CONTRAST ANALYSIS ===");
        _output.WriteLine("");

        _output.WriteLine("Dark Theme:");
        var darkBg = ThemeManager.DarkThemeColors["WindowBackground"];
        var darkText = ThemeManager.DarkThemeColors["PrimaryText"];
        _output.WriteLine($"  Background: {ColorToHex(darkBg)} (Luminance: {GetRelativeLuminance(darkBg):F3})");
        _output.WriteLine($"  Text: {ColorToHex(darkText)} (Luminance: {GetRelativeLuminance(darkText):F3})");
        _output.WriteLine($"  Contrast Ratio: {CalculateContrastRatio(darkBg, darkText):F2}:1");
        _output.WriteLine("");

        _output.WriteLine("Light Theme:");
        var lightBg = ThemeManager.LightThemeColors["WindowBackground"];
        var lightText = ThemeManager.LightThemeColors["PrimaryText"];
        _output.WriteLine($"  Background: {ColorToHex(lightBg)} (Luminance: {GetRelativeLuminance(lightBg):F3})");
        _output.WriteLine($"  Text: {ColorToHex(lightText)} (Luminance: {GetRelativeLuminance(lightText):F3})");
        _output.WriteLine($"  Contrast Ratio: {CalculateContrastRatio(lightBg, lightText):F2}:1");
        _output.WriteLine("");
        _output.WriteLine("Note: WCAG AA requires 4.5:1 for normal text, 3:1 for large text");
    }

    private string ColorToHex(Color color)
    {
        if (color.Equals(Colors.White))
            return "#FFFFFF";
        
        return $"#{color.R:X2}{color.G:X2}{color.B:X2}";
    }

    private double GetRelativeLuminance(Color color)
    {
        // Convert RGB to relative luminance (WCAG formula)
        double r = color.R / 255.0;
        double g = color.G / 255.0;
        double b = color.B / 255.0;

        r = r <= 0.03928 ? r / 12.92 : Math.Pow((r + 0.055) / 1.055, 2.4);
        g = g <= 0.03928 ? g / 12.92 : Math.Pow((g + 0.055) / 1.055, 2.4);
        b = b <= 0.03928 ? b / 12.92 : Math.Pow((b + 0.055) / 1.055, 2.4);

        return 0.2126 * r + 0.7152 * g + 0.0722 * b;
    }

    private double CalculateContrastRatio(Color bg, Color fg)
    {
        double l1 = GetRelativeLuminance(bg);
        double l2 = GetRelativeLuminance(fg);

        double lighter = Math.Max(l1, l2);
        double darker = Math.Min(l1, l2);

        return (lighter + 0.05) / (darker + 0.05);
    }
}
