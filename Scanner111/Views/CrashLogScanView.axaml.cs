using Avalonia;
using Avalonia.Controls;
using Avalonia.Markup.Xaml;

namespace Scanner111.Views;

public partial class CrashLogScanView : Window
{
    public CrashLogScanView()
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
