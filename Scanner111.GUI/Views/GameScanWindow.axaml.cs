using Avalonia.Markup.Xaml;

namespace Scanner111.GUI.Views;

public partial class GameScanWindow : Window
{
    public GameScanWindow()
    {
        InitializeComponent();
#if DEBUG
        this.AttachDevTools();
#endif
    }

    private void InitializeComponent()
    {
        AvaloniaXamlLoader.Load(this);
    }
}