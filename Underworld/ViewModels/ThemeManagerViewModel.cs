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
        Themes.Add(new ThemeOptionViewModel("Dark", "High-contrast palette optimized for low-light environments.", ThemeManager.Theme.Dark));
        Themes.Add(new ThemeOptionViewModel("Light", "Bright palette that pairs well with daylight viewing.", ThemeManager.Theme.Light));

        ThemeManager.ThemeChanged += OnThemeChanged;
        UpdateCurrentThemeState(ThemeManager.CurrentTheme);

        SelectedTheme = Themes.FirstOrDefault(t => t.Theme == ThemeManager.CurrentTheme) ?? Themes.FirstOrDefault();

        _applyThemeCommand = new RelayCommand(
            _ => ApplySelectedTheme(),
            _ => SelectedTheme != null && SelectedTheme.Theme != ThemeManager.CurrentTheme
        );
    }

    public bool ApplySelectedTheme()
    {
        if (SelectedTheme == null)
        {
            return false;
        }

        ThemeManager.SetTheme(SelectedTheme.Theme);
        return true;
    }

    private void OnThemeChanged(object? sender, ThemeManager.Theme e)
    {
        UpdateCurrentThemeState(e);
        SelectedTheme = Themes.FirstOrDefault(t => t.Theme == e) ?? SelectedTheme;
        (_applyThemeCommand as RelayCommand)?.RaiseCanExecuteChanged();
    }

    private void UpdateCurrentThemeState(ThemeManager.Theme theme)
    {
        foreach (var option in Themes)
        {
            option.IsCurrent = option.Theme == theme;
        }
    }

    public void Dispose()
    {
        ThemeManager.ThemeChanged -= OnThemeChanged;
    }
}

public class ThemeOptionViewModel : ViewModelBase
{
    public ThemeOptionViewModel(string name, string description, ThemeManager.Theme theme)
    {
        Name = name;
        Description = description;
        Theme = theme;
    }

    public string Name { get; }
    public string Description { get; }
    public ThemeManager.Theme Theme { get; }

    private bool _isCurrent;
    public bool IsCurrent
    {
        get => _isCurrent;
        set => SetProperty(ref _isCurrent, value);
    }
}
