using Avalonia;
using Avalonia.Controls;
using Avalonia.Markup.Xaml;
using Scanner111.ViewModels;

namespace Scanner111.Views;

public class AboutDialog : Window
{
    public AboutDialog()
    {
        InitializeComponent();
#if DEBUG
        this.AttachDevTools();
#endif
        DataContext = new AboutDialogViewModel(this);
    }

    private void InitializeComponent()
    {
        AvaloniaXamlLoader.Load(this);
    }
}