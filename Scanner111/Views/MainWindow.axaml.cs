using Avalonia.Controls;
using Scanner111.ViewModels;
using Scanner111.Services;

namespace Scanner111.Views;

public partial class MainWindow : Window
{
    public MainWindow()
    {
        InitializeComponent();
        // DataContext will be set via DI in App.axaml.cs
    }

    // Method to set up the view model and services after DI
    public void Initialize(MainWindowViewModel viewModel)
    {
        DataContext = viewModel;

        // Set up the dialog service with the window reference
        if (viewModel.DialogService is DialogService dialogService) dialogService.SetParentWindow(this);

        if (viewModel.DialogService is EnhancedDialogService enhancedDialogService)
            enhancedDialogService.SetParentWindow(this);
    }
}