using Avalonia.Controls;
using Avalonia.Markup.Xaml;
using Scanner111.UI.ViewModels;

namespace Scanner111.UI.Views;

public partial class MainWindow : Window
{
    public MainWindow()
    {
        InitializeComponent();
    }

    private void InitializeComponent()
    {
        AvaloniaXamlLoader.Load(this);
    }
}