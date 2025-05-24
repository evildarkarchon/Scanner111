using Avalonia;
using Avalonia.Controls;
using Avalonia.Markup.Xaml;
using Scanner111.ViewModels;

namespace Scanner111.Views;

public class ConfirmationDialog : Window
{
    public ConfirmationDialog()
    {
        InitializeComponent();
#if DEBUG
        this.AttachDevTools();
#endif
    }

    public ConfirmationDialog(string title, string message, string yesText = "Yes", string noText = "No") : this()
    {
        DataContext = new ConfirmationDialogViewModel(this, title, message, yesText, noText);
    }

    private void InitializeComponent()
    {
        AvaloniaXamlLoader.Load(this);
    }
}