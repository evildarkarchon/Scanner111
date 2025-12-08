using Avalonia.Controls;
using Avalonia.ReactiveUI;
using Scanner111.ViewModels;

namespace Scanner111.Views;

public partial class SettingsWindow : ReactiveWindow<SettingsViewModel>
{
    public SettingsWindow()
    {
        InitializeComponent();
    }
}
