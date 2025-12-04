using Avalonia.Controls;
using Avalonia.Interactivity;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Avalonia.Platform.Storage;
using Underworld.ViewModels;
using Underworld.Models;
using Avalonia;

namespace Underworld.Views;

public partial class DataDirectoriesDialog: Window
{
    private DataDirectoriesViewModel _viewModel = null!;
    private readonly MainWindow _mainWindow;
    public DataDirectoriesDialog(MainWindow mainWin)
    {
        _mainWindow = mainWin;
        InitializeComponent();
        
        _viewModel = new DataDirectoriesViewModel();
        this.DataContext = _viewModel;
    }

    protected override void OnLoaded(RoutedEventArgs e)
    {
        base.OnLoaded(e);

        var addBtn = this.FindControl<Button>("AddButton");
        var closeBtn = this.FindControl<Button>("CloseButton");

        if (addBtn != null)
        {
            addBtn.Click += async (_, _) => await OnAddClicked();
        }

        if (closeBtn != null)
        {
            closeBtn.Click += (_, _) => this.Close();
        }

        this.Closed += (_, _) => {
            // Update WadLists and the main window's IWADs collection
            WadLists.GetNewWadInfos();
            // Todo: Update the IWADs collection in MainWindowViewModel
        };
    }

    private async Task OnAddClicked()
    {
        var topLevel = TopLevel.GetTopLevel(this);
        if (topLevel is not Window window)
            return;

        var provider = window.StorageProvider;
        if (provider == null || !provider.CanPickFolder)
            return;

        var options = new FolderPickerOpenOptions { Title = "Select data folder(s)", AllowMultiple = true };
        var folders = await provider.OpenFolderPickerAsync(options);
        if (folders == null || folders.Count == 0)
            return;

        var paths = new List<string>();
        foreach (var f in folders)
        {
            try
            {
                var uri = f.Path;
                paths.Add(uri.IsAbsoluteUri ? uri.LocalPath : uri.ToString());
            }
            finally
            {
                f.Dispose();
            }
        }

        var invalids = _viewModel.AddDirectories(paths);
        if (invalids.Count > 0)
        {
            await ShowInvalidSelectionsDialog(invalids);
        }
    }

    private async Task ShowInvalidSelectionsDialog(List<string> invalids)
    {
        var errorDlg = new Window
        {
            Title = "Invalid selections",
            Width = 500,
            SizeToContent = SizeToContent.Height,
            Content = new ScrollViewer
            {
                Content = new StackPanel
                {
                    Margin = new Avalonia.Thickness(10),
                    Children =
                    {
                        new TextBlock
                        {
                            Text = "The following selections are not valid:",
                            FontWeight = Avalonia.Media.FontWeight.Bold,
                            Margin = new Avalonia.Thickness(0, 0, 0, 8),
                            Foreground = new Avalonia.Media.SolidColorBrush(Avalonia.Media.Colors.White)
                        }
                    }
                }
            }
        };

        var stack = ((errorDlg.Content as ScrollViewer)!.Content as StackPanel)!;
        foreach (var s in invalids)
            stack.Children.Add(new TextBlock
            {
                Text = s,
                TextWrapping = Avalonia.Media.TextWrapping.Wrap,
                Foreground = new Avalonia.Media.SolidColorBrush(Avalonia.Media.Colors.White)
            });

        var ok = new Button
        {
            Content = "OK",
            HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Right,
            Margin = new Avalonia.Thickness(0, 8, 0, 0)
        };
        ok.Click += (_, _) => errorDlg.Close();
        stack.Children.Add(ok);

        await errorDlg.ShowDialog(this);
    }
}
