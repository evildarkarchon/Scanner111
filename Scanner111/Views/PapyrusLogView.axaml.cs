using Avalonia.Controls;
using Avalonia.Markup.Xaml;

namespace Scanner111.Views;

/// <summary>
///     Interaction logic for PapyrusLogView.xaml
/// </summary>
public partial class PapyrusLogView : UserControl
{
    public PapyrusLogView()
    {
        InitializeComponent();
    }

    private void InitializeComponent()
    {
        AvaloniaXamlLoader.Load(this);
    }
}