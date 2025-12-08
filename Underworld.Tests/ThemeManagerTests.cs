using System;
using Xunit;
using Avalonia;
using Avalonia.Media;
using Underworld.Models;

namespace Underworld.Tests;

/// <summary>
/// Tests for the ThemeManager class to verify theme switching functionality
/// </summary>
public class ThemeManagerTests
{
    private static readonly ConfigEntry<string> _testThemeConfig = Config.Setup("Theme", "Dark");

    public ThemeManagerTests()
    {
        // Reset theme to default for each test
        _testThemeConfig.Set("Dark");
    }

    [Fact]
    public void GetDarkThemeColors_ReturnsCorrectColorCount()
    {
        // Arrange & Act
        var darkColors = ThemeManager.DarkThemeColors;

        // Assert
        Assert.Equal(14, darkColors.Count); // Should have 14 color definitions
    }

    [Fact]
    public void GetLightThemeColors_ReturnsCorrectColorCount()
    {
        // Arrange & Act
        var lightColors = ThemeManager.LightThemeColors;

        // Assert
        Assert.Equal(14, lightColors.Count); // Should have 14 color definitions
    }

    [Fact]
    public void DarkThemeColors_ContainsExpectedKeys()
    {
        // Arrange
        var expectedKeys = new[]
        {
            "WindowBackground", "CardBackground", "ControlBackground",
            "CardBorder", "ControlBorder",
            "PrimaryText", "SecondaryText", "TertiaryText", "MutedText",
            "AccentOrange", "PrimaryButton", "DangerRed",
            "TitleBarHover", "TitleBarPressed"
        };

        // Act
        var darkColors = ThemeManager.DarkThemeColors;

        // Assert
        foreach (var key in expectedKeys)
        {
            Assert.True(darkColors.ContainsKey(key), $"Dark theme should contain key: {key}");
        }
    }

    [Fact]
    public void LightThemeColors_ContainsExpectedKeys()
    {
        // Arrange
        var expectedKeys = new[]
        {
            "WindowBackground", "CardBackground", "ControlBackground",
            "CardBorder", "ControlBorder",
            "PrimaryText", "SecondaryText", "TertiaryText", "MutedText",
            "AccentOrange", "PrimaryButton", "DangerRed",
            "TitleBarHover", "TitleBarPressed"
        };

        // Act
        var lightColors = ThemeManager.LightThemeColors;

        // Assert
        foreach (var key in expectedKeys)
        {
            Assert.True(lightColors.ContainsKey(key), $"Light theme should contain key: {key}");
        }
    }

    [Fact]
    public void DarkThemeColors_HasDarkWindowBackground()
    {
        // Arrange & Act
        var darkColors = ThemeManager.DarkThemeColors;
        var windowBg = darkColors["WindowBackground"];

        // Assert - dark theme should have a dark background
        Assert.True(windowBg.R < 50 && windowBg.G < 50 && windowBg.B < 50,
            $"Dark theme window background should be dark, but was: {windowBg}");
    }

    [Fact]
    public void LightThemeColors_HasLightWindowBackground()
    {
        // Arrange & Act
        var lightColors = ThemeManager.LightThemeColors;
        var windowBg = lightColors["WindowBackground"];

        // Assert - light theme should have a light background
        Assert.True(windowBg.R > 200 && windowBg.G > 200 && windowBg.B > 200,
            $"Light theme window background should be light, but was: {windowBg}");
    }

    [Fact]
    public void Initialize_LoadsDefaultTheme()
    {
        // Arrange
        _testThemeConfig.Set("Dark");

        // Act
        ThemeManager.Initialize();

        // Assert
        Assert.Equal(ThemeManager.Theme.Dark, ThemeManager.CurrentTheme);
    }

    [Fact]
    public void Initialize_LoadsSavedLightTheme()
    {
        // Arrange
        _testThemeConfig.Set("Light");

        // Act
        ThemeManager.Initialize();

        // Assert
        Assert.Equal(ThemeManager.Theme.Light, ThemeManager.CurrentTheme);
    }

    [Fact]
    public void Initialize_WithInvalidTheme_FallsBackToDark()
    {
        // Arrange
        _testThemeConfig.Set("InvalidTheme");

        // Act
        ThemeManager.Initialize();

        // Assert
        Assert.Equal(ThemeManager.Theme.Dark, ThemeManager.CurrentTheme);
    }

    [Fact]
    public void GetCurrentThemeColors_ReturnsDarkWhenDarkThemeSet()
    {
        // Arrange
        ThemeManager.SetTheme(ThemeManager.Theme.Dark);

        // Act
        var colors = ThemeManager.GetCurrentThemeColors();

        // Assert
        Assert.Equal(ThemeManager.DarkThemeColors["WindowBackground"], colors["WindowBackground"]);
    }

    [Fact]
    public void GetCurrentThemeColors_ReturnsLightWhenLightThemeSet()
    {
        // Arrange
        ThemeManager.SetTheme(ThemeManager.Theme.Light);

        // Act
        var colors = ThemeManager.GetCurrentThemeColors();

        // Assert
        Assert.Equal(ThemeManager.LightThemeColors["WindowBackground"], colors["WindowBackground"]);
    }

    [Fact]
    public void SetTheme_ChangesCurrentTheme()
    {
        // Arrange
        ThemeManager.SetTheme(ThemeManager.Theme.Dark);
        Assert.Equal(ThemeManager.Theme.Dark, ThemeManager.CurrentTheme);

        // Act
        ThemeManager.SetTheme(ThemeManager.Theme.Light);

        // Assert
        Assert.Equal(ThemeManager.Theme.Light, ThemeManager.CurrentTheme);
    }

    [Fact]
    public void SetTheme_PersistsToConfig()
    {
        // Arrange & Act
        ThemeManager.SetTheme(ThemeManager.Theme.Light);

        // Assert
        var savedTheme = _testThemeConfig.Get();
        Assert.Equal("Light", savedTheme);
    }

    [Fact]
    public void SetTheme_RaisesThemeChangedEvent()
    {
        // Arrange
        ThemeManager.SetTheme(ThemeManager.Theme.Dark);
        bool eventRaised = false;
        ThemeManager.Theme? receivedTheme = null;

        ThemeManager.ThemeChanged += (sender, theme) =>
        {
            eventRaised = true;
            receivedTheme = theme;
        };

        // Act
        ThemeManager.SetTheme(ThemeManager.Theme.Light);

        // Assert
        Assert.True(eventRaised);
        Assert.Equal(ThemeManager.Theme.Light, receivedTheme);
    }

    [Fact]
    public void SetTheme_DoesNotRaiseEventWhenSettingSameTheme()
    {
        // Arrange
        ThemeManager.SetTheme(ThemeManager.Theme.Dark);
        bool eventRaised = false;

        ThemeManager.ThemeChanged += (sender, theme) =>
        {
            eventRaised = true;
        };

        // Act
        ThemeManager.SetTheme(ThemeManager.Theme.Dark);

        // Assert
        Assert.False(eventRaised);
    }

    [Fact]
    public void ToggleTheme_SwitchesFromDarkToLight()
    {
        // Arrange
        ThemeManager.SetTheme(ThemeManager.Theme.Dark);

        // Act
        ThemeManager.ToggleTheme();

        // Assert
        Assert.Equal(ThemeManager.Theme.Light, ThemeManager.CurrentTheme);
    }

    [Fact]
    public void ToggleTheme_SwitchesFromLightToDark()
    {
        // Arrange
        ThemeManager.SetTheme(ThemeManager.Theme.Light);

        // Act
        ThemeManager.ToggleTheme();

        // Assert
        Assert.Equal(ThemeManager.Theme.Dark, ThemeManager.CurrentTheme);
    }

    [Fact]
    public void GetThemeColors_ReturnsCorrectThemeForDark()
    {
        // Arrange & Act
        var colors = ThemeManager.GetThemeColors(ThemeManager.Theme.Dark);

        // Assert
        Assert.Equal(ThemeManager.DarkThemeColors["WindowBackground"], colors["WindowBackground"]);
    }

    [Fact]
    public void GetThemeColors_ReturnsCorrectThemeForLight()
    {
        // Arrange & Act
        var colors = ThemeManager.GetThemeColors(ThemeManager.Theme.Light);

        // Assert
        Assert.Equal(ThemeManager.LightThemeColors["WindowBackground"], colors["WindowBackground"]);
    }

    [Theory]
    [InlineData("WindowBackground")]
    [InlineData("CardBackground")]
    [InlineData("PrimaryText")]
    [InlineData("AccentOrange")]
    public void DarkAndLightThemes_HaveDifferentColorsForKey(string colorKey)
    {
        // Arrange
        var darkColors = ThemeManager.DarkThemeColors;
        var lightColors = ThemeManager.LightThemeColors;

        // Act & Assert
        Assert.NotEqual(darkColors[colorKey], lightColors[colorKey]);
    }

    [Fact]
    public void DarkTheme_PrimaryTextIsLight()
    {
        // Arrange & Act
        var darkColors = ThemeManager.DarkThemeColors;
        var primaryText = darkColors["PrimaryText"];

        // Assert - in dark theme, text should be light colored
        Assert.True(primaryText.R > 200 || primaryText.G > 200 || primaryText.B > 200,
            $"Dark theme primary text should be light, but was: {primaryText}");
    }

    [Fact]
    public void LightTheme_PrimaryTextIsDark()
    {
        // Arrange & Act
        var lightColors = ThemeManager.LightThemeColors;
        var primaryText = lightColors["PrimaryText"];

        // Assert - in light theme, text should be dark colored
        Assert.True(primaryText.R < 100 && primaryText.G < 100 && primaryText.B < 100,
            $"Light theme primary text should be dark, but was: {primaryText}");
    }

    [Fact]
    public void ThemeColors_AreValidColors()
    {
        // Arrange
        var darkColors = ThemeManager.DarkThemeColors;
        var lightColors = ThemeManager.LightThemeColors;

        // Act & Assert - all colors should have valid ARGB values
        foreach (var (key, color) in darkColors)
        {
            Assert.True(color.A >= 0 && color.A <= 255, $"Dark theme {key} has invalid alpha");
            Assert.True(color.R >= 0 && color.R <= 255, $"Dark theme {key} has invalid red");
            Assert.True(color.G >= 0 && color.G <= 255, $"Dark theme {key} has invalid green");
            Assert.True(color.B >= 0 && color.B <= 255, $"Dark theme {key} has invalid blue");
        }

        foreach (var (key, color) in lightColors)
        {
            Assert.True(color.A >= 0 && color.A <= 255, $"Light theme {key} has invalid alpha");
            Assert.True(color.R >= 0 && color.R <= 255, $"Light theme {key} has invalid red");
            Assert.True(color.G >= 0 && color.G <= 255, $"Light theme {key} has invalid green");
            Assert.True(color.B >= 0 && color.B <= 255, $"Light theme {key} has invalid blue");
        }
    }

    [Fact]
    public void AccentOrange_IsDifferentInBothThemes()
    {
        // Arrange
        var darkOrange = ThemeManager.DarkThemeColors["AccentOrange"];
        var lightOrange = ThemeManager.LightThemeColors["AccentOrange"];

        // Assert - both should be orange-ish but different
        Assert.NotEqual(darkOrange, lightOrange);
        
        // Both should have more red than green/blue (orange characteristic)
        Assert.True(darkOrange.R > darkOrange.G && darkOrange.R > darkOrange.B);
        Assert.True(lightOrange.R > lightOrange.G && lightOrange.R > lightOrange.B);
    }
}
