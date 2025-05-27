using Avalonia.Controls;
using Scanner111.ViewModels;
using Scanner111.Services;

namespace Scanner111.Views;

public partial class MainWindow : Window
{
    public MainWindow()
    {
        InitializeComponent();
        var viewModel = new MainWindowViewModel();
        DataContext = viewModel;

        // Set up the dialog service with the window reference
        if (viewModel.DialogService is DialogService dialogService) dialogService.SetParentWindow(this);
    }
}