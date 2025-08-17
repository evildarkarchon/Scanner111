using Avalonia.Input;
using Avalonia.Platform.Storage;
using Microsoft.Extensions.DependencyInjection;
using Scanner111.Core.Infrastructure;
using Scanner111.Core.Services;
using Scanner111.GUI.Services;
using Scanner111.GUI.ViewModels;

namespace Scanner111.GUI.Views;

/// <summary>
///     Represents the main window of the application. This class serves as the primary user interface window
///     and manages the layout, appearance, and initialization of the application.
/// </summary>
/// <remarks>
///     The <c>MainWindow</c> class is designed to be the entry point for the desktop application.
///     It initializes the necessary components upon loading and serves as a container for user interface
///     elements defined in the corresponding XAML file (<c>MainWindow.axaml</c>).
///     The class is tightly integrated with the ViewModel layer, following the MVVM (Model-View-ViewModel)
///     architectural pattern to separate the user interface from business logic.
/// </remarks>
/// <example>
///     This class is initialized through dependency injection when the application starts.
/// </example>
public partial class MainWindow : Window
{
    private readonly MainWindowViewModel _viewModel;

    // Parameterless constructor for XAML runtime loader and design-time support
    public MainWindow()
    {
        // Create design-time services
        var serviceCollection = new ServiceCollection();
        serviceCollection.AddSingleton<ISettingsService, SettingsService>();
        serviceCollection.AddSingleton<GuiMessageHandlerService>();
        serviceCollection.AddSingleton<IUpdateService, DesignTimeUpdateService>();
        serviceCollection.AddSingleton<ICacheManager, NullCacheManager>();
        serviceCollection.AddSingleton<IUnsolvedLogsMover, NullUnsolvedLogsMover>();
        var serviceProvider = serviceCollection.BuildServiceProvider();

        var settingsService = new SettingsService();
        var messageHandler = new GuiMessageHandlerService();
        var updateService = new DesignTimeUpdateService();
        var cacheManager = new NullCacheManager();
        var unsolvedLogsMover = new NullUnsolvedLogsMover();

        _viewModel = new MainWindowViewModel(serviceProvider, settingsService, messageHandler, updateService,
            cacheManager,
            unsolvedLogsMover);
        InitializeComponent();

        // Set DataContext immediately so bindings work
        DataContext = _viewModel;

        // Wire up file picker events
        Loaded += MainWindow_Loaded;

        // Wire up drag-drop events
        AddHandler(DragDrop.DropEvent, OnDrop);
        AddHandler(DragDrop.DragOverEvent, OnDragOver);
    }

    // Constructor for dependency injection
    public MainWindow(MainWindowViewModel viewModel)
    {
        _viewModel = viewModel;
        InitializeComponent();

        // Set DataContext immediately so bindings work
        DataContext = _viewModel;

        // Wire up file picker events
        Loaded += MainWindow_Loaded;

        // Wire up drag-drop events
        AddHandler(DragDrop.DropEvent, OnDrop);
        AddHandler(DragDrop.DragOverEvent, OnDragOver);
    }

    /// <summary>
    ///     Handles the Loaded event of the MainWindow, setting up necessary file picker delegates for the application.
    /// </summary>
    /// <param name="sender">The source of the event, typically the MainWindow instance.</param>
    /// <param name="e">The event data associated with the Loaded event.</param>
    private void MainWindow_Loaded(object? sender, RoutedEventArgs e)
    {
        // Configure the ViewModel with file picker delegates
        _viewModel.ShowFilePickerAsync = ShowFilePickerAsync;
        _viewModel.ShowFolderPickerAsync = ShowFolderPickerAsync;
        _viewModel.TopLevel = this;
    }

    /// <summary>
    ///     Displays a file picker dialog to allow the user to select a file based on the specified title and filter.
    /// </summary>
    /// <param name="title">The title of the file picker dialog.</param>
    /// <param name="filter">The filter to specify the accepted file types in the file picker.</param>
    /// <returns>
    ///     A task representing the asynchronous operation. The task result contains the selected file's local path
    ///     as a string. If no file is selected, an empty string is returned.
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
    ///     Displays a folder picker dialog to the user, allowing the selection of a single folder.
    /// </summary>
    /// <param name="title">The title to be displayed on the folder picker dialog.</param>
    /// <returns>
    ///     A task that resolves to the path of the selected folder as a string. If no folder is selected, an empty string
    ///     is returned.
    /// </returns>
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

    private void OnDragOver(object? sender, DragEventArgs e)
    {
        // Only accept files
        e.DragEffects = e.Data.Contains(DataFormats.Files)
            ? DragDropEffects.Copy
            : DragDropEffects.None;
    }

    private void OnDrop(object? sender, DragEventArgs e)
    {
        if (!e.Data.Contains(DataFormats.Files))
            return;

        var files = e.Data.GetFiles()?.ToList();
        if (files == null || files.Count == 0)
            return;

        // Process the first dropped file
        var firstFile = files[0];
        if (firstFile is IStorageFile storageFile)
        {
            var path = storageFile.Path.LocalPath;

            // Determine if it's a log file or directory
            if (File.Exists(path))
            {
                // Check if it's a log file
                var extension = Path.GetExtension(path).ToLowerInvariant();
                if (extension == ".log" || extension == ".txt")
                {
                    _viewModel.SelectedLogPath = path;

                    // Automatically start scan if configured
                    if (!string.IsNullOrEmpty(_viewModel.SelectedLogPath)) _viewModel.ScanCommand.Execute().Subscribe();
                }
            }
            else if (Directory.Exists(path))
            {
                // It's a directory - set as scan directory
                _viewModel.SelectedScanDirectory = path;
            }
        }
    }
}