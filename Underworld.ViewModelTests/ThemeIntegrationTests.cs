using System;
using Xunit;
using Underworld.Models;
using Underworld.ViewModels;

#nullable enable

namespace Underworld.ViewModelTests;

/// <summary>
/// Integration tests for theme functionality in ViewModels
/// </summary>
public class ThemeIntegrationTests
{
    private static readonly ConfigEntry<string> _testThemeConfig = Config.Setup("Theme", "dark");

    public ThemeIntegrationTests()
    {
        _testThemeConfig.Set("dark");
        ThemeManager.Initialize();
        ThemeManager.SetTheme("dark");
    }

    [Fact]
    public void ThemeManager_SetTheme_PersistsAcrossInitialization()
    {
        ThemeManager.SetTheme("light");

        ThemeManager.Initialize();

        Assert.Equal("light", ThemeManager.CurrentTheme.Id);
    }

    [Fact]
    public void ThemeManager_ThemeChangedEvent_FiresOnThemeSwitch()
    {
        var eventCount = 0;
        ThemeDefinition? lastTheme = null;

        EventHandler<ThemeDefinition>? handler = (_, theme) =>
        {
            eventCount++;
            lastTheme = theme;
        };

        try
        {
            ThemeManager.ThemeChanged += handler;

            ThemeManager.SetTheme("light");
            ThemeManager.SetTheme("dark");

            Assert.Equal(2, eventCount);
            Assert.Equal("dark", lastTheme?.Id);
        }
        finally
        {
            ThemeManager.ThemeChanged -= handler;
        }
    }

    [Fact]
    public void ThemeManager_GetCurrentThemeColors_ReturnsDifferentColorsForDifferentThemes()
    {
        ThemeManager.SetTheme("dark");
        var darkWindowBg = ThemeManager.GetCurrentThemeColors()["WindowBackground"];

        ThemeManager.SetTheme("light");
        var lightWindowBg = ThemeManager.GetCurrentThemeColors()["WindowBackground"];

        Assert.NotEqual(darkWindowBg, lightWindowBg);
    }

    [Fact]
    public void MainWindowViewModel_CanBeCreatedWithDarkTheme()
    {
        ThemeManager.SetTheme("dark");

        var viewModel = new MainWindowViewModel();

        Assert.NotNull(viewModel);
    }

    [Fact]
    public void MainWindowViewModel_CanBeCreatedWithLightTheme()
    {
        ThemeManager.SetTheme("light");

        var viewModel = new MainWindowViewModel();

        Assert.NotNull(viewModel);
    }

    [Fact]
    public void ThemeManagerViewModel_PopulatesFromAvailableThemes()
    {
        using var dialogVm = new ThemeManagerViewModel();

        Assert.Equal(ThemeManager.AvailableThemes.Count, dialogVm.Themes.Count);
        Assert.Equal(ThemeManager.CurrentTheme.Id, dialogVm.SelectedTheme?.ThemeId);
    }
}
