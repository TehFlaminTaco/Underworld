using System;
using Underworld.Models;
using Xunit;

#nullable enable

namespace Underworld.Tests;

/// <summary>
/// Tests for the JSON-driven ThemeManager implementation.
/// </summary>
public class ThemeManagerTests
{
    private static readonly ConfigEntry<string> _testThemeConfig = Config.Setup("Theme", "dark");

    public ThemeManagerTests()
    {
        _testThemeConfig.Set("dark");
        ThemeManager.Initialize();
        ThemeManager.SetTheme("dark");
    }

    [Fact]
    public void AvailableThemes_LoadedFromDisk()
    {
        var themes = ThemeManager.AvailableThemes;

        Assert.True(themes.Count >= 2);
        Assert.Contains(themes, t => t.Id.Equals("dark", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(themes, t => t.Id.Equals("light", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void GetThemeColors_ReturnsColorSetForTheme()
    {
        var colors = ThemeManager.GetThemeColors("dark");

        Assert.True(colors.ContainsKey("WindowBackground"));
        Assert.True(colors["WindowBackground"].A > 0);
    }

    [Fact]
    public void AvailableThemes_ExposeMetadata()
    {
        var dark = ThemeManager.FindTheme("dark");

        Assert.NotNull(dark);
        Assert.Equal("Dark", dark!.Name);
        Assert.False(string.IsNullOrWhiteSpace(dark.Description));
    }

    [Fact]
    public void Initialize_RespectsSavedTheme()
    {
        _testThemeConfig.Set("light");

        ThemeManager.Initialize();

        Assert.Equal("light", ThemeManager.CurrentTheme.Id);
    }

    [Fact]
    public void Initialize_InvalidTheme_FallsBackToDefault()
    {
        _testThemeConfig.Set("not-a-theme");

        ThemeManager.Initialize();

        Assert.Equal("dark", ThemeManager.CurrentTheme.Id);
    }

    [Fact]
    public void SetTheme_ChangesCurrentTheme()
    {
        ThemeManager.SetTheme("light");

        Assert.Equal("light", ThemeManager.CurrentTheme.Id);
    }

    [Fact]
    public void SetTheme_PersistsPreference()
    {
        ThemeManager.SetTheme("light");

        Assert.Equal("light", _testThemeConfig.Get());
    }

    [Fact]
    public void SetTheme_RaisesThemeChangedEvent()
    {
        ThemeManager.SetTheme("dark");
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

            Assert.Equal(1, eventCount);
            Assert.Equal("light", lastTheme?.Id);
        }
        finally
        {
            ThemeManager.ThemeChanged -= handler;
        }
    }

    [Fact]
    public void ToggleTheme_CyclesThroughAvailableThemes()
    {
        ThemeManager.SetTheme("dark");

        ThemeManager.ToggleTheme();
        Assert.Equal("light", ThemeManager.CurrentTheme.Id);

        ThemeManager.ToggleTheme();
        Assert.Equal("dark", ThemeManager.CurrentTheme.Id);
    }

    [Fact]
    public void GetCurrentThemeColors_ReflectsActiveTheme()
    {
        ThemeManager.SetTheme("dark");
        var darkBg = ThemeManager.GetCurrentThemeColors()["WindowBackground"];

        ThemeManager.SetTheme("light");
        var lightBg = ThemeManager.GetCurrentThemeColors()["WindowBackground"];

        Assert.NotEqual(darkBg, lightBg);
    }

    [Fact]
    public void FindTheme_UsesAliasesCaseInsensitive()
    {
        var theme = ThemeManager.FindTheme("LIGHT");

        Assert.NotNull(theme);
        Assert.Equal("light", theme!.Id);
    }

    [Theory]
    [InlineData("WindowBackground")]
    [InlineData("PrimaryText")]
    [InlineData("AccentOrange")]
    public void Themes_ExposeDifferentColorValues(string key)
    {
        var dark = ThemeManager.GetThemeColors("dark");
        var light = ThemeManager.GetThemeColors("light");

        Assert.NotEqual(dark[key], light[key]);
    }

    [Fact]
    public void ThemeColors_AreValidArgb()
    {
        foreach (var theme in ThemeManager.AvailableThemes)
        {
            foreach (var (_, color) in theme.Colors)
            {
                Assert.InRange(color.A, 0, 255);
                Assert.InRange(color.R, 0, 255);
                Assert.InRange(color.G, 0, 255);
                Assert.InRange(color.B, 0, 255);
            }
        }
    }
}
