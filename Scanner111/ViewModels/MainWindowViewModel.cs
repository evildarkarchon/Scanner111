using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Input;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Platform.Storage;
using Microsoft.Extensions.DependencyInjection;
using ReactiveUI;
using Scanner111.Models;
using Scanner111.Services;
using Scanner111.Views;

namespace Scanner111.ViewModels;

public class MainWindowViewModel : ViewModelBase
{
    private readonly AppSettings _appSettings;
    private readonly CrashLogFormattingService? _formattingService;
    private readonly IGameDirectoryService? _gameDirectoryService;
    private readonly ScanLogService? _scanLogService;

    private string _directoryStatusColor = "Gray";

    private string _directoryStatusMessage = string.Empty;

    private string _docsDirectory = string.Empty;

    // Game directory properties
    private string _gameDirectory = string.Empty;

    // Store a reference to the main window to use for dialogs
    private Window? _mainWindow;

    private ObservableCollection<LogIssue> _scanResults = new();

    private string _statusMessage = string.Empty;

    // Constructor for design-time, if needed, or for DI to inject services
    public MainWindowViewModel()
    {
        // This parameterless constructor can be used by the designer.
        // If AppSettings is critical even for design, you might initialize a default/mock instance here.
        // For runtime, the DI container will use the constructor that takes AppSettings.
        _appSettings =
            new AppSettings(); // Example: Provide a default for the designer or if DI fails            // Set up no-op commands for design-time
        ScanCrashLogsCommand = ReactiveCommand.Create(() => { });
        ReformatCrashLogsCommand = ReactiveCommand.Create(() => { });
        OpenSettingsCommand = ReactiveCommand.Create(() => { });
        OpenPapyrusMonitoringCommand = ReactiveCommand.Create(() => { });
        BrowseGameDirectoryCommand = ReactiveCommand.Create(() => { });
        BrowseDocsDirectoryCommand = ReactiveCommand.Create(() => { });
        DetectDirectoriesCommand = ReactiveCommand.Create(() => { });
    }

    public MainWindowViewModel(
        AppSettings appSettings,
        ScanLogService scanLogService,
        CrashLogFormattingService formattingService,
        IGameDirectoryService gameDirectoryService)
    {
        _appSettings = appSettings ?? throw new ArgumentNullException(nameof(appSettings));
        _scanLogService = scanLogService ?? throw new ArgumentNullException(nameof(scanLogService));
        _formattingService = formattingService ?? throw new ArgumentNullException(nameof(formattingService));
        _gameDirectoryService =
            gameDirectoryService ?? throw new ArgumentNullException(nameof(gameDirectoryService));

        // Initialize directory properties from the service
        _gameDirectory = _gameDirectoryService.GamePath ?? string.Empty;
        _docsDirectory = _gameDirectoryService.DocsPath ?? string.Empty;

        if (!string.IsNullOrEmpty(_gameDirectory) && !string.IsNullOrEmpty(_docsDirectory))
        {
            _directoryStatusMessage = "Directories loaded from settings";
            _directoryStatusColor = "Green";
        }
        else
        {
            _directoryStatusMessage = "Directories not set or detected";
            _directoryStatusColor = "Orange";
        }

        // Initialize commands
        ScanCrashLogsCommand = ReactiveCommand.CreateFromTask(ScanCrashLogsAsync);
        ReformatCrashLogsCommand = ReactiveCommand.CreateFromTask(ReformatCrashLogsAsync);
        OpenSettingsCommand = ReactiveCommand.Create(OpenSettings);
        OpenPapyrusMonitoringCommand = ReactiveCommand.Create(OpenPapyrusMonitoring);
        BrowseGameDirectoryCommand = ReactiveCommand.CreateFromTask(BrowseGameDirectoryAsync);
        BrowseDocsDirectoryCommand = ReactiveCommand.CreateFromTask(BrowseDocsDirectoryAsync);
        DetectDirectoriesCommand = ReactiveCommand.CreateFromTask(DetectDirectoriesAsync);
    } // Example property using a setting
#if DEBUG
    public string Greeting => "Welcome to Avalonia, from MainWindowViewModel!";
#endif

    public string StatusMessage
    {
        get => _statusMessage;
        set => this.RaiseAndSetIfChanged(ref _statusMessage, value);
    }

    public ObservableCollection<LogIssue> ScanResults
    {
        get => _scanResults;
        set => this.RaiseAndSetIfChanged(ref _scanResults, value);
    }

    // Settings properties for UI binding
    public bool SimplifyLogs
    {
        get => _appSettings.SimplifyLogs;
        set
        {
            if (_appSettings.SimplifyLogs != value)
            {
                _appSettings.SimplifyLogs = value;
                this.RaisePropertyChanged();
            }
        }
    }

    public bool PreserveOriginalFiles
    {
        get => _appSettings.PreserveOriginalFiles;
        set
        {
            if (_appSettings.PreserveOriginalFiles != value)
            {
                _appSettings.PreserveOriginalFiles = value;
                this.RaisePropertyChanged();
            }
        }
    }

    public bool AutoDetectCrashLogs
    {
        get => _appSettings.AutoDetectCrashLogs;
        set
        {
            if (_appSettings.AutoDetectCrashLogs != value)
            {
                _appSettings.AutoDetectCrashLogs = value;
                this.RaisePropertyChanged();
            }
        }
    } // Commands

    public ICommand ScanCrashLogsCommand { get; }
    public ICommand ReformatCrashLogsCommand { get; }
    public ICommand OpenSettingsCommand { get; }
    public ICommand OpenPapyrusMonitoringCommand { get; }

    public string GameDirectory
    {
        get => _gameDirectory;
        set => this.RaiseAndSetIfChanged(ref _gameDirectory, value);
    }

    public string DocsDirectory
    {
        get => _docsDirectory;
        set => this.RaiseAndSetIfChanged(ref _docsDirectory, value);
    }

    public string DirectoryStatusMessage
    {
        get => _directoryStatusMessage;
        set => this.RaiseAndSetIfChanged(ref _directoryStatusMessage, value);
    }

    public string DirectoryStatusColor
    {
        get => _directoryStatusColor;
        set => this.RaiseAndSetIfChanged(ref _directoryStatusColor, value);
    }

    // Command for automatic directory detection
    public ICommand DetectDirectoriesCommand { get; }

    public string GamePathDisplay =>
        string.IsNullOrEmpty(_appSettings.GamePath) ? "Not set" : _appSettings.GamePath;

    public string DocsPathDisplay =>
        string.IsNullOrEmpty(_appSettings.DocsPath) ? "Not set" : _appSettings.DocsPath;

    // Commands for browsing directories
    public ICommand BrowseGameDirectoryCommand { get; }
    public ICommand BrowseDocsDirectoryCommand { get; }

    /// <summary>
    ///     Opens a file dialog to select crash log files and then scans them
    /// </summary>
    private async Task ScanCrashLogsAsync()
    {
        if (_scanLogService == null)
        {
            StatusMessage = "Error: ScanLogService is not available";
            return;
        }

        try
        {
            var files = await SelectCrashLogFilesAsync();
            if (files == null || !files.Any()) return;

            StatusMessage = "Scanning crash logs...";

            // Validate files if auto-detection is enabled
            if (_appSettings.AutoDetectCrashLogs && _formattingService != null)
            {
                var validFiles = new List<string>();
                foreach (var file in files)
                    if (await _formattingService.IsCrashLogAsync(file))
                        validFiles.Add(file);

                if (validFiles.Count < files.Count())
                {
                    StatusMessage =
                        $"Warning: {files.Count() - validFiles.Count} files didn't appear to be crash logs and were skipped.";
                    files = validFiles;
                }

                if (!files.Any())
                {
                    StatusMessage = "No valid crash log files were found. Please select crash log files.";
                    return;
                }
            }

            // Create backups if enabled
            if (_appSettings.PreserveOriginalFiles) CreateBackups(files);

            // First reformat the logs
            var processedCount = await _scanLogService.PreprocessCrashLogsAsync(files);

            // Then scan them
            var results = await _scanLogService.ScanMultipleLogFilesAsync(files);

            ScanResults.Clear();
            foreach (var result in results) ScanResults.Add(result);

            // Show errors if any occurred during processing
            if (_appSettings.LastProcessingErrors.Any())
                StatusMessage =
                    $"Scan complete with some issues. Processed {processedCount} files. Found {ScanResults.Count} issues. {_appSettings.LastProcessingErrors.Count} errors occurred during processing.";
            else
                StatusMessage =
                    $"Scan complete. Processed {processedCount} files. Found {ScanResults.Count} issues.";
        }
        catch (Exception ex)
        {
            StatusMessage = $"Error scanning logs: {ex.Message}";
        }
    }

    /// <summary>
    ///     Opens a file dialog to select crash log files and reformats them
    /// </summary>
    private async Task ReformatCrashLogsAsync()
    {
        if (_formattingService == null)
        {
            StatusMessage = "Error: FormattingService is not available";
            return;
        }

        try
        {
            var files = await SelectCrashLogFilesAsync();
            if (files == null || !files.Any()) return;

            StatusMessage = "Reformatting crash logs...";

            // Validate files if auto-detection is enabled
            if (_appSettings.AutoDetectCrashLogs)
            {
                var validFiles = new List<string>();
                foreach (var file in files)
                    if (await _formattingService.IsCrashLogAsync(file))
                        validFiles.Add(file);

                if (validFiles.Count < files.Count())
                {
                    StatusMessage =
                        $"Warning: {files.Count() - validFiles.Count} files didn't appear to be crash logs and were skipped.";
                    files = validFiles;
                }

                if (!files.Any())
                {
                    StatusMessage = "No valid crash log files were found. Please select crash log files.";
                    return;
                }
            }

            // Create backups if enabled
            if (_appSettings.PreserveOriginalFiles) CreateBackups(files);

            var processedCount = await _formattingService.ReformatCrashLogsAsync(
                files,
                _appSettings.SimplifyRemoveStrings
            );

            // Show errors if any occurred during processing
            if (_appSettings.LastProcessingErrors.Any())
                StatusMessage =
                    $"Reformat complete with some issues. Processed {processedCount} files. {_appSettings.LastProcessingErrors.Count} errors occurred during processing.";
            else
                StatusMessage = $"Reformat complete. Processed {processedCount} files.";
        }
        catch (Exception ex)
        {
            StatusMessage = $"Error reformatting logs: {ex.Message}";
        }
    }

    /// <summary>
    ///     Creates backup copies of all files before modifying them
    /// </summary>
    private void CreateBackups(IEnumerable<string> files)
    {
        foreach (var file in files)
            try
            {
                var backupPath = file + ".bak";
                if (!File.Exists(backupPath)) File.Copy(file, backupPath, false);
            }
            catch (Exception ex)
            {
                _appSettings.LastProcessingErrors.Add($"Failed to create backup of {file}: {ex.Message}");
            }
    }

    /// <summary>
    ///     Set the main window reference for file dialogs
    /// </summary>
    public void SetMainWindow(Window mainWindow)
    {
        _mainWindow = mainWindow;
    }

    /// <summary>
    ///     Opens a file dialog to select multiple crash log files
    /// </summary>
    private async Task<IEnumerable<string>> SelectCrashLogFilesAsync()
    {
        try
        {
            // Since we may not have direct access to the TopLevel from the VM
            // we'll need to have the window reference set from the view
            if (_mainWindow == null)
            {
                StatusMessage = "Error: Main window reference is not set";
                return Array.Empty<string>();
            }

            // Create file types filter options
            var logFileTypes = new FilePickerFileType("Log Files")
            {
                Patterns = new[] { "*.log", "*.txt" },
                MimeTypes = new[] { "text/plain" }
            };

            var allFileTypes = new FilePickerFileType("All Files")
            {
                Patterns = new[] { "*.*" }
            };

            // Configure open file dialog options
            var options = new FilePickerOpenOptions
            {
                Title = "Select Crash Log Files",
                AllowMultiple = true,
                FileTypeFilter = new[] { logFileTypes, allFileTypes }
            };

            // Show the dialog using the StorageProvider from the window
            var result = await _mainWindow.StorageProvider.OpenFilePickerAsync(options);

            // Convert IStorageFile results to file paths
            return result?.Select(file => file.Path.LocalPath) ?? Array.Empty<string>();
        }
        catch (Exception ex)
        {
            StatusMessage = $"Error selecting files: {ex.Message}";
            return Array.Empty<string>();
        }
    }

    /// <summary>
    ///     Opens the settings dialog/window
    /// </summary>
    private void OpenSettings()
    {
        // Not yet implemented
        StatusMessage = "Settings dialog not yet implemented";
    }

    /// <summary>
    ///     Opens the Papyrus log monitoring window
    /// </summary>
    private void OpenPapyrusMonitoring()
    {
        // Get the service provider from the application
        var app = Application.Current as App;
        if (app?.ServiceProvider == null || _mainWindow == null)
        {
            StatusMessage = "Could not open Papyrus monitoring window";
            return;
        }

        // Create a new PapyrusMonitoringView with its ViewModel from DI
        var papyrusMonitoringViewModel = app.ServiceProvider.GetRequiredService<PapyrusMonitoringViewModel>();
        var papyrusMonitoringView = new PapyrusMonitoringView
        {
            DataContext = papyrusMonitoringViewModel
        };

        // Show the dialog
        papyrusMonitoringView.ShowDialog(_mainWindow);

        StatusMessage = "Opened Papyrus monitoring window";
    }

    /// <summary>
    ///     Opens a folder dialog to select the game installation directory
    /// </summary>
    private async Task BrowseGameDirectoryAsync()
    {
        if (_gameDirectoryService == null)
        {
            StatusMessage = "Error: GameDirectoryService is not available";
            return;
        }

        if (_mainWindow == null)
        {
            StatusMessage = "Error: Main window reference not set";
            return;
        }

        var topLevel = TopLevel.GetTopLevel(_mainWindow);
        if (topLevel == null)
        {
            StatusMessage = "Error: Cannot get top level window";
            return;
        }

        var folderDialog = await topLevel.StorageProvider.OpenFolderPickerAsync(new FolderPickerOpenOptions
        {
            Title = "Select Game Installation Directory",
            AllowMultiple = false
        });

        if (folderDialog.Count > 0)
        {
            var selectedPath = folderDialog[0].Path.LocalPath;
            StatusMessage = $"Setting game directory to: {selectedPath}";

            var success = await _gameDirectoryService.SetGamePathManuallyAsync(selectedPath);
            if (success)
            {
                StatusMessage = $"Game directory successfully set to: {selectedPath}";
                this.RaisePropertyChanged(nameof(GamePathDisplay));
            }
            else
            {
                StatusMessage = $"Error: Could not set game directory to {selectedPath}";
            }
        }
    }

    /// <summary>
    ///     Opens a folder dialog to select the game documents directory
    /// </summary>
    private async Task BrowseDocsDirectoryAsync()
    {
        if (_gameDirectoryService == null)
        {
            StatusMessage = "Error: GameDirectoryService is not available";
            return;
        }

        if (_mainWindow == null)
        {
            StatusMessage = "Error: Main window reference not set";
            return;
        }

        var topLevel = TopLevel.GetTopLevel(_mainWindow);
        if (topLevel == null)
        {
            StatusMessage = "Error: Cannot get top level window";
            return;
        }

        var folderDialog = await topLevel.StorageProvider.OpenFolderPickerAsync(new FolderPickerOpenOptions
        {
            Title = "Select Game Documents Directory",
            AllowMultiple = false
        });

        if (folderDialog.Count > 0)
        {
            var selectedPath = folderDialog[0].Path.LocalPath;
            StatusMessage = $"Setting documents directory to: {selectedPath}";

            var success = await _gameDirectoryService.SetDocsPathManuallyAsync(selectedPath);
            if (success)
            {
                StatusMessage = $"Documents directory successfully set to: {selectedPath}";
                this.RaisePropertyChanged(nameof(DocsPathDisplay));
            }
            else
            {
                StatusMessage = $"Error: Could not set documents directory to {selectedPath}";
            }
        }
    }

    /// <summary>
    ///     Automatically detect game and docs directories
    /// </summary>
    private async Task DetectDirectoriesAsync()
    {
        if (_gameDirectoryService == null)
        {
            StatusMessage = "Error: GameDirectoryService is not available";
            DirectoryStatusMessage = "Directory detection failed";
            DirectoryStatusColor = "Red";
            return;
        }

        StatusMessage = "Detecting game and docs directories...";
        DirectoryStatusMessage = "Detection in progress...";
        DirectoryStatusColor = "Gray";

        try
        {
            // First try to detect game directory
            var gamePathResult = await _gameDirectoryService.FindGamePathAsync();
            var gamePathFound = !string.IsNullOrEmpty(_gameDirectoryService.GamePath);
            if (gamePathFound)
            {
                GameDirectory = _gameDirectoryService.GamePath ?? string.Empty;
                StatusMessage = $"Game directory detected: {GameDirectory}";
            }
            else
            {
                StatusMessage = "Could not detect game directory";
            }

            // Then try to detect docs directory
            var docsPathResult = await _gameDirectoryService.FindDocsPathAsync();
            var docsPathFound = !string.IsNullOrEmpty(_gameDirectoryService.DocsPath);
            if (docsPathFound)
            {
                DocsDirectory = _gameDirectoryService.DocsPath ?? string.Empty;
                StatusMessage = $"Docs directory detected: {DocsDirectory}";
            }
            else
            {
                StatusMessage = "Could not detect docs directory";
            }

            // Update status based on detection results
            if (gamePathFound && docsPathFound)
            {
                DirectoryStatusMessage = "Directories successfully detected";
                DirectoryStatusColor = "Green";
                StatusMessage = "Game and docs directories detected successfully";
            }
            else if (gamePathFound)
            {
                DirectoryStatusMessage = "Game directory detected, docs directory not found";
                DirectoryStatusColor = "Orange";
                StatusMessage = "Game directory detected, but docs directory could not be found";
            }
            else if (docsPathFound)
            {
                DirectoryStatusMessage = "Docs directory detected, game directory not found";
                DirectoryStatusColor = "Orange";
                StatusMessage = "Docs directory detected, but game directory could not be found";
            }
            else
            {
                DirectoryStatusMessage = "Could not detect directories automatically";
                DirectoryStatusColor = "Red";
                StatusMessage = "Could not detect game or docs directories automatically";
            }
        }
        catch (Exception ex)
        {
            DirectoryStatusMessage = "Error during directory detection";
            DirectoryStatusColor = "Red";
            StatusMessage = $"Error detecting directories: {ex.Message}";
        }
    }
}