using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Windows.Input;
using Underworld.Models;

namespace Underworld.ViewModels;

public class ThemeManagerViewModel : ViewModelBase, IDisposable
{
    public ObservableCollection<ThemeOptionViewModel> Themes { get; } = new();

    private ThemeOptionViewModel? _selectedTheme;
    public ThemeOptionViewModel? SelectedTheme
    {
        get => _selectedTheme;
        set
        {
            if (SetProperty(ref _selectedTheme, value))
            {
                (_applyThemeCommand as RelayCommand)?.RaiseCanExecuteChanged();
            }
        }
    }

    private readonly ICommand _applyThemeCommand;
    public ICommand ApplyThemeCommand => _applyThemeCommand;

    public ThemeManagerViewModel()
    {
        foreach (var theme in ThemeManager.AvailableThemes)
        {
            Themes.Add(new ThemeOptionViewModel(theme));
        }

        ThemeManager.ThemeChanged += OnThemeChanged;
        UpdateCurrentThemeState(ThemeManager.CurrentTheme);

        SelectedTheme = Themes.FirstOrDefault(t => t.IsMatch(ThemeManager.CurrentTheme.Id)) ?? Themes.FirstOrDefault();

        _applyThemeCommand = new RelayCommand(
            _ => ApplySelectedTheme(),
            _ => SelectedTheme != null && !SelectedTheme.IsCurrent
        );
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
        SelectedTheme = Themes.FirstOrDefault(t => t.IsMatch(e.Id)) ?? SelectedTheme;
        (_applyThemeCommand as RelayCommand)?.RaiseCanExecuteChanged();
    }

    private void UpdateCurrentThemeState(ThemeDefinition theme)
    {
        foreach (var option in Themes)
        {
            option.IsCurrent = option.IsMatch(theme.Id);
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
