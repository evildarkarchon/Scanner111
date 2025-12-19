using Avalonia.Controls;
using Scanner111.ViewModels;

namespace Scanner111.Views
{
    public partial class MainWindow : Window
    {
        /// <summary>
        /// Parameterless constructor for XAML runtime loader (required for avares:// resource loading).
        /// </summary>
        public MainWindow()
        {
            InitializeComponent();
        }

        /// <summary>
        /// DI constructor used by the application.
        /// </summary>
        public MainWindow(MainWindowViewModel viewModel)
        {
            InitializeComponent();
            DataContext = viewModel;
        }
    }
}