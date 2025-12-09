using Avalonia.Controls;
using Avalonia.Interactivity;
using System;
using Underworld.ViewModels;

namespace Underworld.Views;

public partial class ThemeManagerDialog : Window
{
    private readonly ThemeManagerViewModel _viewModel;

    public ThemeManagerDialog()
    {
        InitializeComponent();
        _viewModel = new ThemeManagerViewModel();
        DataContext = _viewModel;

        Closed += (_, _) => _viewModel.Dispose();
    }

    private void OnCloseClicked(object? sender, RoutedEventArgs e)
    {
        Close();
    }

    private void OnThemeDoubleTapped(object? sender, RoutedEventArgs e)
    {
        _viewModel.ApplySelectedTheme();
    }
}
