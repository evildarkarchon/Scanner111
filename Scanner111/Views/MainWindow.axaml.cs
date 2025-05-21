using Avalonia.Controls;
using Avalonia.Interactivity;
using Microsoft.Extensions.DependencyInjection;
using Scanner111.ViewModels;

namespace Scanner111.Views
{
    public partial class MainWindow : Window
    {
        public MainWindow()
        {
            InitializeComponent();
        }

        private void OpenFormIdDatabaseManager_Click(object sender, RoutedEventArgs e)
        {
            // Get the service provider from the application
            var app = Avalonia.Application.Current as App;
            if (app?._serviceProvider == null)
            {
                return;
            }

            // Create a new FormIdDatabaseView with its ViewModel from DI
            var formIdDatabaseViewModel = app._serviceProvider.GetRequiredService<FormIdDatabaseViewModel>();
            var formIdDatabaseView = new FormIdDatabaseView
            {
                DataContext = formIdDatabaseViewModel
            };

            // Show the dialog
            formIdDatabaseView.ShowDialog(this);
        }
    }
}