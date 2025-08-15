using Avalonia.Controls;
using Avalonia.Interactivity;

namespace Scanner111.GUI.Views;

public partial class StatisticsWindow : Window
{
    public StatisticsWindow()
    {
        InitializeComponent();
    }

    private void CloseButton_Click(object? sender, RoutedEventArgs e)
    {
        Close();
    }
}