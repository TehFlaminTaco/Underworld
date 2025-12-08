using System;
using Xunit;
using Underworld.Models;
using Underworld.ViewModels;

namespace Underworld.ViewModelTests;

/// <summary>
/// Integration tests for theme functionality in ViewModels
/// </summary>
public class ThemeIntegrationTests
{
    private static readonly ConfigEntry<string> _testThemeConfig = Config.Setup("Theme", "Dark");

    public ThemeIntegrationTests()
    {
        // Reset theme to default for each test
        _testThemeConfig.Set("Dark");
        ThemeManager.Initialize();
    }

    [Fact]
    public void ThemeManager_InitializeAndToggle_WorksCorrectly()
    {
        // Arrange
        ThemeManager.Initialize();
        Assert.Equal(ThemeManager.Theme.Dark, ThemeManager.CurrentTheme);

        // Act - Toggle to Light
        ThemeManager.ToggleTheme();

        // Assert
        Assert.Equal(ThemeManager.Theme.Light, ThemeManager.CurrentTheme);

        // Act - Toggle back to Dark
        ThemeManager.ToggleTheme();

        // Assert
        Assert.Equal(ThemeManager.Theme.Dark, ThemeManager.CurrentTheme);
    }

    [Fact]
    public void ThemeManager_SetTheme_PersistsAcrossInitialization()
    {
        // Arrange & Act
        ThemeManager.SetTheme(ThemeManager.Theme.Light);
        
        // Re-initialize (simulates app restart)
        ThemeManager.Initialize();

        // Assert
        Assert.Equal(ThemeManager.Theme.Light, ThemeManager.CurrentTheme);
    }

    [Fact]
    public void ThemeManager_ThemeChangedEvent_FiresOnThemeSwitch()
    {
        // Arrange
        int eventCount = 0;
        ThemeManager.Theme? lastTheme = null;

        ThemeManager.ThemeChanged += (sender, theme) =>
        {
            eventCount++;
            lastTheme = theme;
        };

        // Act - Switch to light
        ThemeManager.SetTheme(ThemeManager.Theme.Light);

        // Assert
        Assert.Equal(1, eventCount);
        Assert.Equal(ThemeManager.Theme.Light, lastTheme);

        // Act - Switch back to dark
        ThemeManager.SetTheme(ThemeManager.Theme.Dark);

        // Assert
        Assert.Equal(2, eventCount);
        Assert.Equal(ThemeManager.Theme.Dark, lastTheme);
    }

    [Fact]
    public void ThemeManager_GetCurrentThemeColors_ReturnsDifferentColorsForDifferentThemes()
    {
        // Arrange
        ThemeManager.SetTheme(ThemeManager.Theme.Dark);
        var darkColors = ThemeManager.GetCurrentThemeColors();
        var darkWindowBg = darkColors["WindowBackground"];

        // Act
        ThemeManager.SetTheme(ThemeManager.Theme.Light);
        var lightColors = ThemeManager.GetCurrentThemeColors();
        var lightWindowBg = lightColors["WindowBackground"];

        // Assert
        Assert.NotEqual(darkWindowBg, lightWindowBg);
    }

    [Fact]
    public void ThemeManager_MultipleToggle_MaintainsCorrectState()
    {
        // Arrange
        ThemeManager.SetTheme(ThemeManager.Theme.Dark);

        // Act & Assert - Multiple toggles
        for (int i = 0; i < 10; i++)
        {
            var expectedTheme = (i % 2 == 0) ? ThemeManager.Theme.Light : ThemeManager.Theme.Dark;
            ThemeManager.ToggleTheme();
            Assert.Equal(expectedTheme, ThemeManager.CurrentTheme);
        }
    }

    [Fact]
    public void MainWindowViewModel_CanBeCreatedWithDarkTheme()
    {
        // Arrange
        ThemeManager.SetTheme(ThemeManager.Theme.Dark);

        // Act
        var viewModel = new MainWindowViewModel();

        // Assert - Should not throw exception
        Assert.NotNull(viewModel);
    }

    [Fact]
    public void MainWindowViewModel_CanBeCreatedWithLightTheme()
    {
        // Arrange
        ThemeManager.SetTheme(ThemeManager.Theme.Light);

        // Act
        var viewModel = new MainWindowViewModel();

        // Assert - Should not throw exception
        Assert.NotNull(viewModel);
    }

    [Fact]
    public void ThemeManager_DarkThemeColors_AllHaveValidValues()
    {
        // Arrange
        var darkTheme = ThemeManager.DarkThemeColors;

        // Assert
        foreach (var (key, color) in darkTheme)
        {
            Assert.True(color.A > 0, $"Color {key} should have alpha > 0");
            // RGB values should be between 0-255 (automatically enforced by Color struct)
        }
    }

    [Fact]
    public void ThemeManager_LightThemeColors_AllHaveValidValues()
    {
        // Arrange
        var lightTheme = ThemeManager.LightThemeColors;

        // Assert
        foreach (var (key, color) in lightTheme)
        {
            Assert.True(color.A > 0, $"Color {key} should have alpha > 0");
            // RGB values should be between 0-255 (automatically enforced by Color struct)
        }
    }
}
