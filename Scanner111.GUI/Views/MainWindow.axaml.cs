using System.Threading.Tasks;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Platform.Storage;
using Scanner111.GUI.ViewModels;

namespace Scanner111.GUI.Views;

/// <summary>
/// Represents the main window of the application. This class serves as the primary user interface window
/// and manages the layout, appearance, and initialization of the application.
/// </summary>
/// <remarks>
/// The <c>MainWindow</c> class is designed to be the entry point for the desktop application.
/// It initializes the necessary components upon loading and serves as a container for user interface
/// elements defined in the corresponding XAML file (<c>MainWindow.axaml</c>).
/// The class is tightly integrated with the ViewModel layer, following the MVVM (Model-View-ViewModel)
/// architectural pattern to separate the user interface from business logic.
/// </remarks>
/// <example>
/// This class is automatically initialized and displayed when the application starts.
/// It is not designed to be instantiated manually.
/// </example>
public partial class MainWindow : Window
{
    public MainWindow()
    {
        InitializeComponent();

        // Wire up file picker events
        Loaded += MainWindow_Loaded;
    }

    /// <summary>
    /// Handles the Loaded event of the MainWindow, initializing the ViewModel
    /// and setting up necessary file picker delegates for the application.
    /// </summary>
    /// <param name="sender">The source of the event, typically the MainWindow instance.</param>
    /// <param name="e">The event data associated with the Loaded event.</param>
    private void MainWindow_Loaded(object? sender, RoutedEventArgs e)
    {
        // Create ViewModel on UI thread after window is loaded
        var viewModel = new MainWindowViewModel
        {
            // Set up file picker delegates
            ShowFilePickerAsync = ShowFilePickerAsync,
            ShowFolderPickerAsync = ShowFolderPickerAsync,
            TopLevel = this
        };

        // Set DataContext
        DataContext = viewModel;
    }

    /// <summary>
    /// Displays a file picker dialog to allow the user to select a file based on the specified title and filter.
    /// </summary>
    /// <param name="title">The title of the file picker dialog.</param>
    /// <param name="filter">The filter to specify the accepted file types in the file picker.</param>
    /// <returns>
    /// A task representing the asynchronous operation. The task result contains the selected file's local path
    /// as a string. If no file is selected, an empty string is returned.
    /// </returns>
    private async Task<string> ShowFilePickerAsync(string title, string filter)
    {
        var topLevel = GetTopLevel(this);
        if (topLevel == null) return string.Empty;

        var files = await topLevel.StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
        {
            Title = title,
            AllowMultiple = false,
            FileTypeFilter =
            [
                new FilePickerFileType("Log Files")
                {
                    Patterns = ["*.log", "*.txt"]
                },
                new FilePickerFileType("All Files")
                {
                    Patterns = ["*.*"]
                }
            ]
        });

        return files.Count > 0 ? files[0].Path.LocalPath : string.Empty;
    }

    /// <summary>
    /// Displays a folder picker dialog to the user, allowing the selection of a single folder.
    /// </summary>
    /// <param name="title">The title to be displayed on the folder picker dialog.</param>
    /// <returns>A task that resolves to the path of the selected folder as a string. If no folder is selected, an empty string is returned.</returns>
    private async Task<string> ShowFolderPickerAsync(string title)
    {
        var topLevel = GetTopLevel(this);
        if (topLevel == null) return string.Empty;

        var folders = await topLevel.StorageProvider.OpenFolderPickerAsync(new FolderPickerOpenOptions
        {
            Title = title,
            AllowMultiple = false
        });

        return folders.Count > 0 ? folders[0].Path.LocalPath : string.Empty;
    }
}