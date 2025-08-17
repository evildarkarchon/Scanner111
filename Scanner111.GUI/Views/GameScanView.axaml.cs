using Avalonia.Markup.Xaml;

namespace Scanner111.GUI.Views;

public partial class GameScanView : UserControl
{
    public GameScanView()
    {
        InitializeComponent();
    }

    private void InitializeComponent()
    {
        AvaloniaXamlLoader.Load(this);
    }
}