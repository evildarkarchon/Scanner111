using Avalonia;
using Avalonia.Controls;
using Avalonia.Markup.Xaml;
using Scanner111.ViewModels;

namespace Scanner111.Views;

public partial class HelpDialog : Window
{
    public HelpDialog()
    {
        InitializeComponent();
#if DEBUG
        this.AttachDevTools();
#endif
    }

    public HelpDialog(string title, string content) : this()
    {
        DataContext = new HelpDialogViewModel(this, title, content);
    }

    private void InitializeComponent()
    {
        AvaloniaXamlLoader.Load(this);
    }
}