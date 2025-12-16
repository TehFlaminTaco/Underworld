using System;
using System.Threading.Tasks;
using Avalonia.Controls;
using Underworld.ViewModels;

namespace Underworld.Views;

public partial class MiniLauncherDialog : Window
{
    private readonly MiniLauncherViewModel _viewModel;

    public MiniLauncherDialog()
        : this(new MiniLauncherViewModel(_ => Task.CompletedTask))
    {
    }

    public MiniLauncherDialog(MainWindowViewModel mainWindowViewModel)
        : this(mainWindowViewModel?.CreateMiniLauncherViewModel() ?? throw new ArgumentNullException(nameof(mainWindowViewModel)))
    {
    }

    private MiniLauncherDialog(MiniLauncherViewModel viewModel)
    {
        InitializeComponent();
        _viewModel = viewModel ?? throw new ArgumentNullException(nameof(viewModel));
        _viewModel.CloseRequested += (_, _) => Close();
        DataContext = _viewModel;
    }

    protected override void OnClosing(WindowClosingEventArgs e)
    {
        _viewModel.PersistState();
        base.OnClosing(e);
    }
}
