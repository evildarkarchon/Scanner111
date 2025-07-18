using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Platform.Storage;
using Scanner111.GUI.ViewModels;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

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
        var viewModel = new MainWindowViewModel();
        
        // Set up file picker delegates
        viewModel.ShowFilePickerAsync = ShowFilePickerAsync;
        viewModel.ShowFolderPickerAsync = ShowFolderPickerAsync;
        viewModel.TopLevel = this;
        
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
            FileTypeFilter = new[]
            {
                new FilePickerFileType("Log Files")
                {
                    Patterns = new[] { "*.log", "*.txt" }
                },
                new FilePickerFileType("All Files")
                {
                    Patterns = new[] { "*.*" }
                }
            }
        });

        return files.FirstOrDefault()?.Path.LocalPath ?? string.Empty;
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

        return folders.FirstOrDefault()?.Path.LocalPath ?? string.Empty;
    }
}