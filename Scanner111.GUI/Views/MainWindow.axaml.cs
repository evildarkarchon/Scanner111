using System.Threading.Tasks;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Platform.Storage;
using Scanner111.GUI.ViewModels;

namespace Scanner111.GUI.Views;

public partial class MainWindow : Window
{
    public MainWindow()
    {
        InitializeComponent();

        // Wire up file picker events
        Loaded += MainWindow_Loaded;
    }

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