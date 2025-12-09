using System;
using System.Collections.ObjectModel;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Windows.Input;
using System.IO;
using Underworld.Models;

namespace Underworld.ViewModels;

public class ThemeManagerViewModel : ViewModelBase, IDisposable
{
    public ObservableCollection<ThemeOptionViewModel> Themes { get; } = new();

    private ThemeOptionViewModel? _selectedTheme;
    private bool _suppressAutoApply;
    public ThemeOptionViewModel? SelectedTheme
    {
        get => _selectedTheme;
        set
        {
            if (SetProperty(ref _selectedTheme, value))
            {
                (_applyThemeCommand as RelayCommand)?.RaiseCanExecuteChanged();

                if (!_suppressAutoApply && value != null && !value.IsCurrent)
                {
                    ApplySelectedTheme();
                }
            }
        }
    }

    private readonly ICommand _applyThemeCommand;
    private readonly ICommand _openThemesFolderCommand;
    private readonly ICommand _refreshThemesCommand;
    public ICommand ApplyThemeCommand => _applyThemeCommand;
    public ICommand OpenThemesFolderCommand => _openThemesFolderCommand;
    public ICommand RefreshThemesCommand => _refreshThemesCommand;

    public ThemeManagerViewModel()
    {
        LoadThemeOptions(ThemeManager.AvailableThemes);

        ThemeManager.ThemeChanged += OnThemeChanged;
        UpdateCurrentThemeState(ThemeManager.CurrentTheme);

        SetSelectedTheme(Themes.FirstOrDefault(t => t.IsMatch(ThemeManager.CurrentTheme.Id)) ?? Themes.FirstOrDefault(), suppressAutoApply: true);

        _applyThemeCommand = new RelayCommand(
            _ => ApplySelectedTheme(),
            _ => SelectedTheme != null && !SelectedTheme.IsCurrent
        );

        _openThemesFolderCommand = new RelayCommand(_ => OpenThemesFolder());
        _refreshThemesCommand = new RelayCommand(_ => RefreshThemes());
    }

    public bool ApplySelectedTheme()
    {
        if (SelectedTheme == null)
        {
            return false;
        }

        ThemeManager.SetTheme(SelectedTheme.ThemeId);
        return true;
    }

    private void OnThemeChanged(object? sender, ThemeDefinition e)
    {
        UpdateCurrentThemeState(e);
        SetSelectedTheme(Themes.FirstOrDefault(t => t.IsMatch(e.Id)) ?? SelectedTheme, suppressAutoApply: true);
        (_applyThemeCommand as RelayCommand)?.RaiseCanExecuteChanged();
    }

    private void UpdateCurrentThemeState(ThemeDefinition theme)
    {
        foreach (var option in Themes)
        {
            option.IsCurrent = option.IsMatch(theme.Id);
        }
    }

    private void LoadThemeOptions(IEnumerable<ThemeDefinition> definitions)
    {
        Themes.Clear();
        foreach (var theme in definitions)
        {
            Themes.Add(new ThemeOptionViewModel(theme));
        }
    }

    private void OpenThemesFolder()
    {
        try
        {
            var folderPath = ThemeManager.GetThemesDirectoryPath();
            if (!Directory.Exists(folderPath))
            {
                Directory.CreateDirectory(folderPath);
            }

            var startInfo = new ProcessStartInfo
            {
                FileName = folderPath,
                UseShellExecute = true
            };

            Process.Start(startInfo);
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"[ThemeManager] Failed to open themes folder: {ex}");
        }
    }

    private void RefreshThemes()
    {
        try
        {
            var previousSelectionId = SelectedTheme?.ThemeId;
            var refreshedThemes = ThemeManager.ReloadThemes();
            LoadThemeOptions(refreshedThemes);
            UpdateCurrentThemeState(ThemeManager.CurrentTheme);

            var fallbackId = previousSelectionId ?? ThemeManager.CurrentTheme.Id;
            SetSelectedTheme(Themes.FirstOrDefault(t => t.IsMatch(fallbackId))
                ?? Themes.FirstOrDefault(t => t.IsMatch(ThemeManager.CurrentTheme.Id))
                ?? Themes.FirstOrDefault(), suppressAutoApply: true);
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"[ThemeManager] Failed to refresh themes: {ex}");
        }
    }

    private void SetSelectedTheme(ThemeOptionViewModel? theme, bool suppressAutoApply)
    {
        var previousState = _suppressAutoApply;
        _suppressAutoApply = suppressAutoApply;
        try
        {
            SelectedTheme = theme;
        }
        finally
        {
            _suppressAutoApply = previousState;
        }
    }

    public void Dispose()
    {
        ThemeManager.ThemeChanged -= OnThemeChanged;
    }
}

public class ThemeOptionViewModel : ViewModelBase
{
    public ThemeOptionViewModel(ThemeDefinition theme)
    {
        Theme = theme;
    }

    public ThemeDefinition Theme { get; }
    public string ThemeId => Theme.Id;
    public string Name => Theme.Name;
    public string Description => Theme.Description;

    private bool _isCurrent;
    public bool IsCurrent
    {
        get => _isCurrent;
        set => SetProperty(ref _isCurrent, value);
    }

    public bool IsMatch(string themeId) => Theme.Id.Equals(themeId, StringComparison.OrdinalIgnoreCase);
}
