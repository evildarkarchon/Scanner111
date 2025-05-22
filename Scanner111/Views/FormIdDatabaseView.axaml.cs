using System;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Markup.Xaml;
using Scanner111.ViewModels;

namespace Scanner111.Views;

public partial class FormIdDatabaseView : Window
{
    public FormIdDatabaseView()
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

    private void CloseButton_Click(object sender, RoutedEventArgs e)
    {
        Close();
    }

    protected override void OnOpened(EventArgs e)
    {
        base.OnOpened(e);

        // Display initial status message when the window is opened
        if (DataContext is FormIdDatabaseViewModel viewModel) viewModel.ShowDatabaseDetailsCommand.Execute(null);
    }
}