using Avalonia.Controls;
using Avalonia.Markup.Xaml;

namespace Scanner111.UI.Views;

public partial class CrashLogDetailView : UserControl
{
    public CrashLogDetailView()
    {
        InitializeComponent();
    }

    private void InitializeComponent()
    {
        AvaloniaXamlLoader.Load(this);
    }
}