using Avalonia.Controls;
using Avalonia.Interactivity;
using Scanner111.ViewModels.Tabs;

namespace Scanner111.Views;

public partial class MainTabView : UserControl
{
    public MainTabView()
    {
        InitializeComponent();
    }

    private void ClearPastebinUrl_Click(object? sender, RoutedEventArgs e)
    {
        if (DataContext is MainTabViewModel viewModel)
        {
            viewModel.PastebinUrl = "";
        }
    }
}