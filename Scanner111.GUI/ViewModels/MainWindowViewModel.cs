using System.Reactive.Linq;
using Avalonia.Threading;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Scanner111.Core.Analyzers;
using Scanner111.Core.Infrastructure;
using Scanner111.Core.Models;
using Scanner111.Core.ModManagers;
using Scanner111.Core.Pipeline;
using Scanner111.Core.Services;
using Scanner111.GUI.Models;
using Scanner111.GUI.Services;
using Scanner111.GUI.Views;

namespace Scanner111.GUI.ViewModels;

/// <summary>
///     Represents the main ViewModel for the application's main window.
/// </summary>
/// <remarks>
///     This ViewModel is responsible for managing the main operations of the application,
///     including file and folder selection, initiating and canceling scanning, managing scan results, and handling
///     progress updates.
///     It provides commands and properties to support the user interface.
///     Inherits from <see cref="ViewModelBase" /> to leverage reactive property functionality.
/// </remarks>
public class MainWindowViewModel : ViewModelBase
{
    private readonly IAudioNotificationService? _audioNotificationService;
    private readonly ICacheManager _cacheManager;
    private readonly GuiMessageHandlerService _messageHandlerService;
    private readonly IModManagerService? _modManagerService;
    private readonly IRecentItemsService? _recentItemsService;
    private readonly ISettingsService _settingsService;
    private readonly IStatisticsService? _statisticsService;
    private readonly IThemeService? _themeService;
    private readonly IUnsolvedLogsMover? _unsolvedLogsMover;
    private readonly IUpdateService _updateService;
    private UserSettings _currentSettings;
    private ObservableCollection<ModInfo> _detectedMods = new();
    private FcxResultViewModel? _fcxResult;
    private bool _isScanning;
    private IMessageHandler? _messageHandler;
    private bool _modManagerDetected;
    private string _modManagerStatus = "No mod manager detected";
    private string _progressText = "";
    private double _progressValue;
    private bool _progressVisible;
    private ObservableCollection<RecentItem> _recentFiles = new();
    private IReportWriter? _reportWriter;
    private CancellationTokenSource? _scanCancellationTokenSource;

    private IScanPipeline? _scanPipeline;
    private string _selectedGamePath = "";
    private string _selectedLogPath = "";
    private ScanResultViewModel? _selectedResult;
    private string _selectedScanDirectory = "";
    private string _statusText = "Ready";

    public MainWindowViewModel(
        ISettingsService settingsService,
        GuiMessageHandlerService messageHandlerService,
        IUpdateService updateService,
        ICacheManager cacheManager,
        IUnsolvedLogsMover unsolvedLogsMover,
        IModManagerService? modManagerService = null,
        IRecentItemsService? recentItemsService = null,
        IAudioNotificationService? audioNotificationService = null,
        IStatisticsService? statisticsService = null,
        IThemeService? themeService = null)
    {
        _settingsService = Guard.NotNull(settingsService, nameof(settingsService));
        _messageHandlerService = Guard.NotNull(messageHandlerService, nameof(messageHandlerService));
        _updateService = Guard.NotNull(updateService, nameof(updateService));
        _cacheManager = Guard.NotNull(cacheManager, nameof(cacheManager));
        _unsolvedLogsMover = Guard.NotNull(unsolvedLogsMover, nameof(unsolvedLogsMover));
        _modManagerService = modManagerService;
        _recentItemsService = recentItemsService;
        _audioNotificationService = audioNotificationService;
        _statisticsService = statisticsService;
        _themeService = themeService;
        _currentSettings = new UserSettings();

        // Set this view model in the message handler service
        _messageHandlerService.SetViewModel(this);

        // Initialize commands first - defer pipeline creation to avoid threading issues
        SelectLogFileCommand = ReactiveCommand.CreateFromTask(SelectLogFile);
        SelectGamePathCommand = ReactiveCommand.CreateFromTask(SelectGamePath);
        SelectScanDirectoryCommand = ReactiveCommand.CreateFromTask(SelectScanDirectory);
        ScanCommand = ReactiveCommand.CreateFromTask(ExecuteScan);
        CancelScanCommand = ReactiveCommand.Create(CancelScan);
        ClearResultsCommand = ReactiveCommand.Create(ClearResults);
        OpenSettingsCommand = ReactiveCommand.CreateFromTask(OpenSettings);
        ExportSelectedReportCommand = ReactiveCommand.CreateFromTask(ExportSelectedReport,
            this.WhenAnyValue(x => x.SelectedResult).Select(x => x != null));
        ExportAllReportsCommand = ReactiveCommand.CreateFromTask(ExportAllReports,
            this.WhenAnyValue(x => x.ScanResults.Count).Select(x => x > 0));

        // FCX Commands
        RunFcxScanCommand = ReactiveCommand.CreateFromTask(RunFcxScan);
        BackupGameFilesCommand = ReactiveCommand.CreateFromTask(BackupGameFiles);
        ValidateGameInstallCommand = ReactiveCommand.CreateFromTask(ValidateGameInstall);

        // Mod Manager Commands
        RefreshModManagersCommand = ReactiveCommand.CreateFromTask(DetectModManagersAsync);

        // Recent Files Commands
        OpenRecentFileCommand = ReactiveCommand.CreateFromTask<string>(OpenRecentFile);
        ClearRecentFilesCommand = ReactiveCommand.Create(ClearRecentFiles);

        // View Commands
        ShowStatisticsCommand = ReactiveCommand.CreateFromTask(ShowStatistics);
        ShowPapyrusMonitorCommand = ReactiveCommand.CreateFromTask(ShowPapyrusMonitor);
        ShowHelpCommand = ReactiveCommand.CreateFromTask(ShowHelp);
        ShowKeyboardShortcutsCommand = ReactiveCommand.CreateFromTask(ShowKeyboardShortcuts);
        ShowAboutCommand = ReactiveCommand.CreateFromTask(ShowAbout);
        SetThemeCommand = ReactiveCommand.CreateFromTask<string>(SetTheme);
        ExitCommand = ReactiveCommand.Create(Exit);

        StatusText = "Ready - Select a crash log file to begin";

        // Load settings asynchronously and perform update check
        _ = InitializeAsync();
    }

    /// <summary>
    ///     Gets or sets the current status message displayed in the application's user interface.
    ///     The <c>StatusText</c> property is typically used to provide feedback or updates to the user
    ///     about the current state of operations, such as scanning progress, errors, or user actions.
    /// </summary>
    public string StatusText
    {
        get => _statusText;
        set
        {
            if (Dispatcher.UIThread.CheckAccess())
                this.RaiseAndSetIfChanged(ref _statusText, value);
            else
                Dispatcher.UIThread.InvokeAsync(() => this.RaiseAndSetIfChanged(ref _statusText, value));
        }
    }

    /// <summary>
    ///     Gets or sets a value indicating whether a scanning operation is currently in progress.
    ///     The <c>IsScanning</c> property is used to control and reflect the state of the scanning process,
    ///     ensuring UI elements react accordingly, such as disabling interactions or displaying progress indicators.
    /// </summary>
    public bool IsScanning
    {
        get => _isScanning;
        set
        {
            if (Dispatcher.UIThread.CheckAccess())
                this.RaiseAndSetIfChanged(ref _isScanning, value);
            else
                Dispatcher.UIThread.InvokeAsync(() => this.RaiseAndSetIfChanged(ref _isScanning, value));
        }
    }

    /// <summary>
    ///     Gets or sets the progress value representing the percentage of completion for the current operation.
    ///     The <c>ProgressValue</c> property is typically used to update progress indicators in the user interface
    ///     and ranges from 0.0 to 100.0, where 100.0 indicates the completion of the operation.
    /// </summary>
    public double ProgressValue
    {
        get => _progressValue;
        set
        {
            if (Dispatcher.UIThread.CheckAccess())
                this.RaiseAndSetIfChanged(ref _progressValue, value);
            else
                Dispatcher.UIThread.InvokeAsync(() => this.RaiseAndSetIfChanged(ref _progressValue, value));
        }
    }

    /// <summary>
    ///     Gets or sets the text indicating the current progress of an operation.
    ///     The <c>ProgressText</c> property is typically used to provide descriptive feedback
    ///     to the user about the ongoing progress, such as the number of files processed during a scan or export operation.
    /// </summary>
    public string ProgressText
    {
        get => _progressText;
        set
        {
            if (Dispatcher.UIThread.CheckAccess())
                this.RaiseAndSetIfChanged(ref _progressText, value);
            else
                Dispatcher.UIThread.InvokeAsync(() => this.RaiseAndSetIfChanged(ref _progressText, value));
        }
    }

    /// <summary>
    ///     Gets or sets a value indicating whether the progress indicator is visible in the user interface.
    ///     The <c>ProgressVisible</c> property is used to show or hide visual elements
    ///     related to progress during operations such as scanning or exporting.
    /// </summary>
    public bool ProgressVisible
    {
        get => _progressVisible;
        set
        {
            if (Dispatcher.UIThread.CheckAccess())
                this.RaiseAndSetIfChanged(ref _progressVisible, value);
            else
                Dispatcher.UIThread.InvokeAsync(() => this.RaiseAndSetIfChanged(ref _progressVisible, value));
        }
    }

    /// <summary>
    ///     Gets or sets the file path of the currently selected log for analysis or processing.
    ///     The <c>SelectedLogPath</c> property is used to specify the location of a log file,
    ///     enabling the application to read or process its contents as part of the scanning workflow.
    ///     Changes to this property may trigger updates in the UI or internal operations, such as
    ///     populating scan results or adding log messages.
    /// </summary>
    public string SelectedLogPath
    {
        get => _selectedLogPath;
        set
        {
            if (Dispatcher.UIThread.CheckAccess())
                this.RaiseAndSetIfChanged(ref _selectedLogPath, value);
            else
                Dispatcher.UIThread.InvokeAsync(() => this.RaiseAndSetIfChanged(ref _selectedLogPath, value));
        }
    }

    /// <summary>
    ///     Gets or sets the file system path to the selected game directory.
    ///     The <c>SelectedGamePath</c> property is used to indicate the root folder
    ///     of the game files required for various operations such as log scanning or processing.
    ///     Its value can influence how specific tasks are executed, such as searching for logs
    ///     or detecting specific configurations within the game directory.
    /// </summary>
    public string SelectedGamePath
    {
        get => _selectedGamePath;
        set
        {
            if (Dispatcher.UIThread.CheckAccess())
                this.RaiseAndSetIfChanged(ref _selectedGamePath, value);
            else
                Dispatcher.UIThread.InvokeAsync(() => this.RaiseAndSetIfChanged(ref _selectedGamePath, value));
        }
    }

    /// <summary>
    ///     Gets or sets the directory path selected for scanning log files.
    ///     The <c>SelectedScanDirectory</c> property is typically used to define the location
    ///     from which files are collected for analysis or processing. Changes to this property
    ///     may trigger updates to the application's state or initiate file loading processes.
    /// </summary>
    public string SelectedScanDirectory
    {
        get => _selectedScanDirectory;
        set
        {
            if (Dispatcher.UIThread.CheckAccess())
                this.RaiseAndSetIfChanged(ref _selectedScanDirectory, value);
            else
                Dispatcher.UIThread.InvokeAsync(() => this.RaiseAndSetIfChanged(ref _selectedScanDirectory, value));
        }
    }

    /// <summary>
    ///     Gets or sets the currently selected scan result in the user interface.
    ///     The <c>SelectedResult</c> property represents the item that the user has chosen
    ///     from the list of available scan results and is used to facilitate actions
    ///     like exporting detailed reports or displaying result-specific information.
    /// </summary>
    public ScanResultViewModel? SelectedResult
    {
        get => _selectedResult;
        set
        {
            if (Dispatcher.UIThread.CheckAccess())
                this.RaiseAndSetIfChanged(ref _selectedResult, value);
            else
                Dispatcher.UIThread.InvokeAsync(() => this.RaiseAndSetIfChanged(ref _selectedResult, value));
        }
    }

    public ObservableCollection<ScanResultViewModel> ScanResults { get; } = new();
    public ObservableCollection<string> LogMessages { get; } = new();

    /// <summary>
    ///     Gets the collection of detected mods from the active mod manager.
    /// </summary>
    public ObservableCollection<ModInfo> DetectedMods
    {
        get => _detectedMods;
        private set => this.RaiseAndSetIfChanged(ref _detectedMods, value);
    }

    public ObservableCollection<RecentItem> RecentFiles
    {
        get => _recentFiles;
        private set => this.RaiseAndSetIfChanged(ref _recentFiles, value);
    }

    public bool HasRecentFiles => RecentFiles?.Count > 0;

    /// <summary>
    ///     Gets or sets the mod manager status text displayed in the UI.
    /// </summary>
    public string ModManagerStatus
    {
        get => _modManagerStatus;
        set
        {
            if (Dispatcher.UIThread.CheckAccess())
                this.RaiseAndSetIfChanged(ref _modManagerStatus, value);
            else
                Dispatcher.UIThread.InvokeAsync(() => this.RaiseAndSetIfChanged(ref _modManagerStatus, value));
        }
    }

    /// <summary>
    ///     Gets or sets whether a mod manager has been detected.
    /// </summary>
    public bool ModManagerDetected
    {
        get => _modManagerDetected;
        set
        {
            if (Dispatcher.UIThread.CheckAccess())
                this.RaiseAndSetIfChanged(ref _modManagerDetected, value);
            else
                Dispatcher.UIThread.InvokeAsync(() => this.RaiseAndSetIfChanged(ref _modManagerDetected, value));
        }
    }

    /// <summary>
    ///     Gets or sets the FCX scan result view model.
    /// </summary>
    public FcxResultViewModel? FcxResult
    {
        get => _fcxResult;
        set
        {
            if (Dispatcher.UIThread.CheckAccess())
                this.RaiseAndSetIfChanged(ref _fcxResult, value);
            else
                Dispatcher.UIThread.InvokeAsync(() => this.RaiseAndSetIfChanged(ref _fcxResult, value));
        }
    }

    public ReactiveCommand<Unit, Unit> SelectLogFileCommand { get; }
    public ReactiveCommand<Unit, Unit> SelectGamePathCommand { get; }
    public ReactiveCommand<Unit, Unit> SelectScanDirectoryCommand { get; }
    public ReactiveCommand<Unit, Unit> ScanCommand { get; }
    public ReactiveCommand<Unit, Unit> CancelScanCommand { get; }
    public ReactiveCommand<Unit, Unit> ClearResultsCommand { get; }
    public ReactiveCommand<Unit, Unit> OpenSettingsCommand { get; }
    public ReactiveCommand<Unit, Unit> ExportSelectedReportCommand { get; }
    public ReactiveCommand<Unit, Unit> ExportAllReportsCommand { get; }
    public ReactiveCommand<Unit, Unit> RunFcxScanCommand { get; }
    public ReactiveCommand<Unit, Unit> BackupGameFilesCommand { get; }
    public ReactiveCommand<Unit, Unit> ValidateGameInstallCommand { get; }
    public ReactiveCommand<Unit, Unit> RefreshModManagersCommand { get; }

    // Recent Files Commands
    public ReactiveCommand<string, Unit> OpenRecentFileCommand { get; }
    public ReactiveCommand<Unit, Unit> ClearRecentFilesCommand { get; }

    // View Commands  
    public ReactiveCommand<Unit, Unit> ShowStatisticsCommand { get; }
    public ReactiveCommand<Unit, Unit> ShowPapyrusMonitorCommand { get; }
    public ReactiveCommand<Unit, Unit> ShowHelpCommand { get; }
    public ReactiveCommand<Unit, Unit> ShowKeyboardShortcutsCommand { get; }
    public ReactiveCommand<Unit, Unit> ShowAboutCommand { get; }
    public ReactiveCommand<string, Unit> SetThemeCommand { get; }
    public ReactiveCommand<Unit, Unit> ExitCommand { get; }

    // File picker delegates - set by the View
    public Func<string, string, Task<string>>? ShowFilePickerAsync { get; set; }
    public Func<string, Task<string>>? ShowFolderPickerAsync { get; set; }
    public Window? TopLevel { get; set; }

    /// <summary>
    ///     Ensures that the scan pipeline is initialized for the application. If the pipeline has not yet been initialized,
    ///     this method sets up the necessary components, including a message handler, caching, enhanced error handling,
    ///     and logging configuration.
    /// </summary>
    /// <remarks>
    ///     This method initializes necessary components for the scan pipeline:
    ///     - A message handler (GUI-specific implementation).
    ///     - Default analyzers added to the pipeline.
    ///     - Optional caching and error handling mechanisms.
    ///     - Logging system, such as console-based logging.
    ///     Additionally, it initializes the report writer with a null logger, as console logging is not required for the GUI.
    /// </remarks>
    private void EnsurePipelineInitialized()
    {
        if (_scanPipeline != null) return;
        _messageHandler = _messageHandlerService;

        var pipelineBuilder = new ScanPipelineBuilder()
            .AddDefaultAnalyzers()
            .WithMessageHandler(_messageHandler)
            .WithCaching()
            .WithEnhancedErrorHandling()
            .WithLogging(builder => builder.AddConsole());

        // Enable FCX mode if configured in settings
        if (_currentSettings.FcxMode) pipelineBuilder.WithFcxMode();

        _scanPipeline = pipelineBuilder.Build();

        // Initialize report writer with null logger (GUI doesn't need console logging)
        _reportWriter = new ReportWriter(NullLogger<ReportWriter>.Instance);
    }

    /// <summary>
    ///     Resets the scan pipeline, forcing it to be recreated with current settings.
    ///     This should be called when FCX mode or other pipeline-affecting settings change.
    /// </summary>
    private async Task ResetPipelineAsync()
    {
        if (_scanPipeline != null)
        {
            await _scanPipeline.DisposeAsync();
            _scanPipeline = null;
        }
    }

    /// <summary>
    ///     Opens a file picker to allow the user to select a crash log file. Once a file is selected, this method updates the
    ///     selected log file path, displays the file name in the status text, logs the file selection, and saves the current
    ///     paths to the settings.
    /// </summary>
    /// <remarks>
    ///     This method displays a dialog for file selection. If a file is selected:
    ///     - The file path is set to the SelectedLogPath property.
    ///     - The file name is shown in the StatusText property.
    ///     - A log message is added with the selected file path.
    ///     - The selected paths are persisted to user settings.
    ///     If an error occurs during file selection, an error message is displayed in the status text, and the error is
    ///     logged.
    /// </remarks>
    /// <returns>Returns a task that represents the asynchronous operation of selecting a file.</returns>
    private async Task SelectLogFile()
    {
        try
        {
            if (ShowFilePickerAsync != null)
            {
                var result = await ShowFilePickerAsync("Select Crash Log", "*.log");
                if (!string.IsNullOrEmpty(result))
                {
                    SelectedLogPath = result;
                    StatusText = $"Selected: {Path.GetFileName(result)}";
                    AddLogMessage($"Selected crash log: {result}");
                    _recentItemsService?.AddRecentLogFile(result);
                    UpdateRecentFiles();
                    _ = SaveCurrentPathsToSettings();
                }
            }
        }
        catch (Exception ex)
        {
            StatusText = "Error selecting file";
            AddLogMessage($"Error: {ex.Message}");
        }
    }

    /// <summary>
    ///     Prompts the user to select a directory for the game installation. Once a directory is selected, it updates the
    ///     selected game path, logs the chosen path, and saves the updated path to the user settings.
    /// </summary>
    /// <remarks>
    ///     This method uses a folder picker dialog to allow the user to select the game installation directory.
    ///     If a valid path is chosen, the following steps are executed:
    ///     - Updates the SelectedGamePath property with the chosen path.
    ///     - Logs the selected game path for user reference.
    ///     - Asynchronously saves the updated paths to the application settings.
    ///     If an error occurs during path selection or saving, it logs the error message.
    /// </remarks>
    /// <returns>
    ///     A task representing the asynchronous operation of selecting a game path, with no direct result.
    /// </returns>
    private async Task SelectGamePath()
    {
        try
        {
            if (ShowFolderPickerAsync != null)
            {
                var result = await ShowFolderPickerAsync("Select Game Installation Directory");
                if (!string.IsNullOrEmpty(result))
                {
                    SelectedGamePath = result;
                    AddLogMessage($"Selected game path: {result}");
                    _ = SaveCurrentPathsToSettings();
                }
            }
        }
        catch (Exception ex)
        {
            AddLogMessage($"Error selecting game path: {ex.Message}");
        }
    }

    /// <summary>
    ///     Prompts the user to select a directory for scanning crash logs and updates the application state with the selected
    ///     path.
    /// </summary>
    /// <remarks>
    ///     This method displays a folder picker dialog to the user, allowing them to select the directory to be scanned for
    ///     crash logs.
    ///     Once a valid directory is selected:
    ///     - The selected directory path is stored in the <see cref="SelectedScanDirectory" /> property.
    ///     - A log message is added to indicate the selected directory.
    ///     - The application paths are saved to the settings asynchronously.
    ///     If an error occurs during the directory selection process, a corresponding log message is added.
    /// </remarks>
    /// <returns>
    ///     A task that represents the asynchronous operation of selecting a directory.
    /// </returns>
    private async Task SelectScanDirectory()
    {
        try
        {
            if (ShowFolderPickerAsync != null)
            {
                var result = await ShowFolderPickerAsync("Select Directory to Scan for Crash Logs");
                if (!string.IsNullOrEmpty(result))
                {
                    SelectedScanDirectory = result;
                    AddLogMessage($"Selected scan directory: {result}");
                    _ = SaveCurrentPathsToSettings();
                }
            }
        }
        catch (Exception ex)
        {
            AddLogMessage($"Error selecting scan directory: {ex.Message}");
        }
    }

    /// <summary>
    ///     Executes the scan process for analyzing crash log files. This method initializes the scan pipeline, processes
    ///     the necessary configurations, and performs the scanning operation on the selected files, updating the user
    ///     interface with progress and status information throughout the operation.
    /// </summary>
    /// <remarks>
    ///     This method handles the following tasks:
    ///     - Initializes the scan pipeline using the <c>EnsurePipelineInitialized</c> method if it has not already been set
    ///     up.
    ///     - Updates the scanning status, progress bar, and related UI elements during the scan lifecycle.
    ///     - Collects the specified files marked for analysis and logs their count.
    ///     - Executes log analysis on each file and accumulates results, updating the progress asynchronously.
    ///     - Handles cancellation requests by the user during the scan process.
    ///     - Manages error handling in case of scan failures due to unexpected exceptions.
    ///     - Cleans up resources, such as cancellation tokens, used for coordinating the scanning operation.
    /// </remarks>
    /// <returns>
    ///     A task representing the asynchronous operation of the scan process.
    /// </returns>
    private async Task ExecuteScan()
    {
        try
        {
            EnsurePipelineInitialized();

            IsScanning = true;
            ProgressVisible = true;
            ProgressValue = 0;
            ProgressText = "Initializing scan...";
            StatusText = "Scanning crash logs...";

            _scanCancellationTokenSource = new CancellationTokenSource();
            ScanResults.Clear();

            AddLogMessage("Starting crash log analysis...");

            // Collect all files to scan
            var filesToScan = await CollectFilesToScan();

            if (filesToScan.Count == 0)
            {
                StatusText = "No crash log files found to scan";
                AddLogMessage("No valid crash log files found. Please select a file or directory.");
                return;
            }

            AddLogMessage($"Found {filesToScan.Count} crash log files to scan");

            if (filesToScan.Count == 1)
            {
                // Single file scan
                var result =
                    await _scanPipeline!.ProcessSingleAsync(filesToScan[0], _scanCancellationTokenSource.Token);
                ScanResults.Add(new ScanResultViewModel(result));
                AddLogMessage(
                    $"Scan status: {result.Status}, Found {result.AnalysisResults.Count} analysis results");

                // Auto-save if enabled
                await AutoSaveResult(result);
            }
            else
            {
                // Batch scan
                var resultCount = 0;
                await foreach (var result in _scanPipeline!.ProcessBatchAsync(filesToScan))
                {
                    ScanResults.Add(new ScanResultViewModel(result));
                    resultCount++;
                    ProgressValue = (double)resultCount / filesToScan.Count * 100;
                    ProgressText = $"Processed {resultCount}/{filesToScan.Count} files";
                    AddLogMessage(
                        $"Processed {Path.GetFileName(result.LogPath)}: {result.Status}, {result.AnalysisResults.Count} analysis results");

                    // Auto-save if enabled
                    await AutoSaveResult(result);
                }
            }

            ProgressValue = 100;
            ProgressText = $"Scan complete - {ScanResults.Count} results";
            StatusText = $"Scan completed - {ScanResults.Count} results from {filesToScan.Count} files";
            AddLogMessage(
                $"Scan completed successfully. Found {ScanResults.Count} total results from {filesToScan.Count} files.");
        }
        catch (OperationCanceledException)
        {
            StatusText = "Scan cancelled";
            AddLogMessage("Scan was cancelled by user.");
        }
        catch (Exception ex)
        {
            StatusText = "Scan failed";
            AddLogMessage($"Scan failed: {ex.Message}");
        }
        finally
        {
            IsScanning = false;
            ProgressVisible = false;
            _scanCancellationTokenSource?.Dispose();
            _scanCancellationTokenSource = null;
        }
    }

    /// <summary>
    ///     Collects a list of files to be scanned based on selected paths and specific criteria. This method consolidates
    ///     files from multiple sources including auto-copied logs, a manually selected log file, and logs located in a
    ///     specified directory.
    /// </summary>
    /// <remarks>
    ///     This method performs the following operations to gather files for scanning:
    ///     - Automatically retrieves and copies XSE logs (e.g., F4SE and SKSE).
    ///     - Adds a single, manually selected log file if specified and valid.
    ///     - Scans a user-selected directory to find and include valid log files.
    ///     The returned list ensures no duplicate entries and only includes valid files.
    /// </remarks>
    /// <returns>
    ///     A task representing the asynchronous operation, containing a list of file paths to be scanned.
    /// </returns>
    private async Task<List<string>> CollectFilesToScan()
    {
        var filesToScan = new List<string>();

        // 1. Auto-copy XSE logs (F4SE and SKSE)
        await CopyXseLogsAsync(filesToScan);

        // 2. Add single selected file if specified
        if (!string.IsNullOrEmpty(SelectedLogPath) && File.Exists(SelectedLogPath))
            if (!filesToScan.Contains(SelectedLogPath))
            {
                filesToScan.Add(SelectedLogPath);
                AddLogMessage($"Added selected file: {Path.GetFileName(SelectedLogPath)}");
            }

        // 3. Add files from selected directory if specified
        if (!string.IsNullOrEmpty(SelectedScanDirectory) && Directory.Exists(SelectedScanDirectory))
            await ScanDirectoryForLogs(SelectedScanDirectory, filesToScan);

        return filesToScan;
    }

    /// <summary>
    ///     Copies XSE crash logs (F4SE and SKSE) from commonly known directories to a specified crash logs directory.
    ///     If the user has disabled this feature via settings, the operation is skipped. The method identifies potential
    ///     source directories (e.g., Fallout4 and Skyrim-related SKSE/F4SE paths) and attempts to copy logs to the configured
    ///     destination, logging the results of the process.
    /// </summary>
    /// <param name="filesToScan">
    ///     A list of file paths that may be updated with paths to the copied XSE crash logs, depending on the process.
    /// </param>
    /// <returns>
    ///     A task that represents the asynchronous operation for copying XSE logs. The task completes once the logs have been
    ///     processed or skipped.
    /// </returns>
    private async Task CopyXseLogsAsync(List<string> filesToScan)
    {
        // Skip XSE copy if user disabled it
        if (_currentSettings.SkipXseCopy) return;

        try
        {
            // Get crash logs directory from settings or use default
            var crashLogsBaseDir = !string.IsNullOrEmpty(_currentSettings.CrashLogsDirectory)
                ? _currentSettings.CrashLogsDirectory
                : CrashLogDirectoryManager.GetDefaultCrashLogsDirectory();

            // Look for F4SE crash logs in common locations (prioritizing Fallout 4 only)
            var xsePaths = new[]
            {
                // F4SE paths only
                Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), "My Games", "Fallout4",
                    "F4SE"),
                Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), "My Games", "Fallout4VR",
                    "F4SE"),
                // Also check game path if provided
                !string.IsNullOrEmpty(SelectedGamePath) ? Path.Combine(SelectedGamePath, "Data", "F4SE") : null
            }.Where(path => path != null && Directory.Exists(path)).ToArray();

            var copiedCount = 0;
            foreach (var xsePath in xsePaths)
                if (Directory.Exists(xsePath))
                {
                    var crashLogs = Directory.GetFiles(xsePath, "*.log", SearchOption.TopDirectoryOnly)
                        .Where(f => Path.GetFileName(f).StartsWith("crash-", StringComparison.OrdinalIgnoreCase))
                        .OrderByDescending(File.GetLastWriteTime)
                        .ToArray();

                    foreach (var logFile in crashLogs)
                    {
                        // Detect game type and copy to appropriate subdirectory
                        var gameType = CrashLogDirectoryManager.DetectGameType(SelectedGamePath, logFile);
                        var targetPath = await Task.Run(() =>
                            CrashLogDirectoryManager.CopyCrashLog(logFile, crashLogsBaseDir, gameType));

                        var xseType = xsePath.Contains("F4SE") ? "F4SE" : "SKSE";
                        AddLogMessage(
                            $"Copied {xseType} {gameType} crash log: {Path.GetFileName(logFile)} -> {Path.GetDirectoryName(targetPath)}");
                        copiedCount++;

                        if (!filesToScan.Contains(targetPath)) filesToScan.Add(targetPath);
                    }
                }

            if (copiedCount > 0)
                AddLogMessage($"Auto-copied {copiedCount} XSE crash logs to {crashLogsBaseDir}");
            else if (xsePaths.Length == 0)
                AddLogMessage("No XSE directories found for auto-copy");
            else
                AddLogMessage("No new XSE crash logs to copy");
        }
        catch (Exception ex)
        {
            AddLogMessage($"Error during XSE auto-copy: {ex.Message}");
        }
    }

    /// <summary>
    ///     Scans a specified directory for log files matching predefined criteria and adds them to a given collection of files
    ///     to be scanned.
    ///     The method identifies log files by extension (.log or .txt) and checks if their names contain keywords such as
    ///     "crash", "dump", or "error".
    /// </summary>
    /// <param name="directory">The path of the directory to scan for log files.</param>
    /// <param name="filesToScan">
    ///     A list to which the discovered log files will be added if they meet the criteria and do not
    ///     already exist in the list.
    /// </param>
    private async Task ScanDirectoryForLogs(string directory, List<string> filesToScan)
    {
        try
        {
            var logFiles = await Task.Run(() =>
                Directory.GetFiles(directory, "*.*", SearchOption.TopDirectoryOnly)
                    .Where(f =>
                    {
                        var ext = Path.GetExtension(f).ToLower();
                        return ext == ".log" || ext == ".txt";
                    })
                    .Where(f =>
                    {
                        var name = Path.GetFileName(f).ToLower();
                        return name.Contains("crash") || name.Contains("dump") || name.Contains("error");
                    })
                    .OrderByDescending(File.GetLastWriteTime)
                    .ToArray());

            var addedCount = 0;
            foreach (var logFile in logFiles)
                if (!filesToScan.Contains(logFile))
                {
                    filesToScan.Add(logFile);
                    addedCount++;
                }

            AddLogMessage($"Added {addedCount} crash logs from directory: {Path.GetFileName(directory)}");
        }
        catch (Exception ex)
        {
            AddLogMessage($"Error scanning directory {directory}: {ex.Message}");
        }
    }

    /// <summary>
    ///     Cancels the currently running scan operation, if any, by signaling the associated cancellation token.
    ///     Updates the application status to reflect the cancellation state and logs an appropriate message.
    /// </summary>
    /// <remarks>
    ///     This method invokes the cancellation token's cancel signal to stop an ongoing scan process.
    ///     Post-cancellation actions include updating the status text with a "Cancelling scan..." message
    ///     and adding a log entry to denote that a scan cancellation was requested by the user.
    /// </remarks>
    private void CancelScan()
    {
        _scanCancellationTokenSource?.Cancel();
        StatusText = "Cancelling scan...";
        AddLogMessage("Scan cancellation requested.");
    }

    /// <summary>
    ///     Clears all scan results and log messages from the application. Resets the associated user interface elements,
    ///     including status text and progress visibility.
    /// </summary>
    /// <remarks>
    ///     This method performs the following actions:
    ///     - Clears the collection of scan results.
    ///     - Clears the collection of log messages.
    ///     - Updates the status text to indicate that results have been cleared.
    ///     - Hides the progress indicator.
    ///     It ensures that the UI reflects a fresh state, ready for a new scanning operation.
    /// </remarks>
    private void ClearResults()
    {
        ScanResults.Clear();
        LogMessages.Clear();
        StatusText = "Results cleared";
        ProgressVisible = false;
    }

    /// <summary>
    ///     Adds a log message to the log messages collection with a timestamp. This helps track user actions, errors,
    ///     or other significant events as they occur in the application.
    /// </summary>
    /// <param name="message">The content of the log message to be added. This should describe the event or error being logged.</param>
    /// <remarks>
    ///     The log message is prefixed with the current time in "HH:mm:ss" format. This method also ensures that
    ///     the log messages collection does not exceed 100 entries by removing the oldest messages when necessary.
    ///     Thread-safe updates to the UI are handled via a helper method.
    /// </remarks>
    public void AddLogMessage(string message)
    {
        var timestamp = DateTime.Now.ToString("HH:mm:ss");
        UpdateUi(() =>
        {
            LogMessages.Add($"[{timestamp}] {message}");

            // Keep only last 100 messages
            while (LogMessages.Count > 100) LogMessages.RemoveAt(0);
        });
    }

    /// <summary>
    ///     Executes the specified action on the UI thread. If the calling thread is already
    ///     the UI thread, the action is executed directly; otherwise, it is dispatched
    ///     to the UI thread for execution using Avalonia's dispatcher system.
    /// </summary>
    /// <param name="action">The action to be performed on the UI thread.</param>
    /// <remarks>
    ///     This method ensures that UI-related actions are executed safely and consistently
    ///     on the UI thread. It checks the current thread's access and dispatches the
    ///     action if required, providing thread safety for UI updates in the application.
    /// </remarks>
    private void UpdateUi(Action action)
    {
        if (Dispatcher.UIThread.CheckAccess())
            action();
        else
            Dispatcher.UIThread.InvokeAsync(action);
    }

    /// <summary>
    ///     Initializes the ViewModel by loading settings and performing startup tasks like update checking.
    /// </summary>
    /// <returns>A task representing the asynchronous initialization.</returns>
    private async Task InitializeAsync()
    {
        await LoadSettingsAsync();
        await PerformStartupUpdateCheckAsync();
        await DetectModManagersAsync();
        UpdateRecentFiles();
    }

    /// <summary>
    ///     Detects installed mod managers and updates the UI accordingly.
    /// </summary>
    private async Task DetectModManagersAsync()
    {
        // Check if mod manager support is disabled in settings
        if (!_currentSettings.AutoDetectModManagers)
        {
            ModManagerStatus = "Mod manager integration disabled";
            ModManagerDetected = false;
            AddLogMessage("Mod manager integration is disabled in settings");
            return;
        }

        if (_modManagerService == null)
        {
            ModManagerStatus = "Mod manager support not available";
            ModManagerDetected = false;
            return;
        }

        try
        {
            AddLogMessage("Detecting installed mod managers...");

            var managers = await _modManagerService.GetAvailableManagersAsync();
            if (!managers.Any())
            {
                ModManagerStatus = "No mod managers detected";
                ModManagerDetected = false;
                AddLogMessage("No mod managers found on this system");
                return;
            }

            var activeManager = await _modManagerService.GetActiveManagerAsync();
            if (activeManager != null)
            {
                ModManagerStatus = $"{activeManager.Name} detected";
                ModManagerDetected = true;

                // Load mods if FCX mode is enabled
                if (_currentSettings.FcxMode)
                {
                    var mods = await _modManagerService.GetAllModsAsync();
                    await Dispatcher.UIThread.InvokeAsync(() =>
                    {
                        DetectedMods.Clear();
                        foreach (var mod in mods.Where(m => m.IsEnabled)) DetectedMods.Add(mod);
                    });

                    AddLogMessage(
                        $"Loaded {mods.Count()} mods from {activeManager.Name} ({mods.Count(m => m.IsEnabled)} enabled)");
                }
            }
        }
        catch (Exception ex)
        {
            ModManagerStatus = "Error detecting mod managers";
            ModManagerDetected = false;
            AddLogMessage($"Error detecting mod managers: {ex.Message}");
        }
    }

    /// <summary>
    ///     Performs an update check during startup, respecting user configuration settings.
    /// </summary>
    /// <returns>A task representing the asynchronous update check operation.</returns>
    private async Task PerformStartupUpdateCheckAsync()
    {
        try
        {
            // Check if update checking is enabled in settings
            if (_currentSettings.EnableUpdateCheck)
            {
                AddLogMessage("Checking for application updates...");
                await _updateService.IsLatestVersionAsync();
            }
        }
        catch (UpdateCheckException ex)
        {
            // Handle update check specific exceptions (e.g., update available)
            AddLogMessage($"Update check completed: {ex.Message}");
        }
        catch (Exception ex)
        {
            // Log other errors but don't fail the application
            AddLogMessage($"Update check failed: {ex.Message}");
        }
    }

    /// <summary>
    ///     Asynchronously loads and applies user settings to the application. This method retrieves the settings
    ///     from a storage service and updates relevant properties such as default paths for logs, game files,
    ///     and scan directories. If any settings are missing, existing values remain unchanged.
    /// </summary>
    /// <remarks>
    ///     During the loading process, default paths from the user settings are applied to the following properties
    ///     if they are currently empty:
    ///     - SelectedLogPath: Set to the default log path if available.
    ///     - SelectedGamePath: Set to the default game path if available.
    ///     - SelectedScanDirectory: Set to the default scan directory if available.
    ///     In case of an error during the load operation, the error message is logged for troubleshooting purposes.
    /// </remarks>
    /// <returns>
    ///     A task representing the asynchronous operation. The task completes when the settings are successfully loaded
    ///     or when an error occurs during the loading process.
    /// </returns>
    private async Task LoadSettingsAsync()
    {
        try
        {
            var previousFcxMode = _currentSettings.FcxMode;
            _currentSettings = await _settingsService.LoadUserSettingsAsync();

            // Apply default paths from settings if current paths are empty
            if (string.IsNullOrEmpty(SelectedLogPath) && !string.IsNullOrEmpty(_currentSettings.DefaultLogPath))
                SelectedLogPath = _currentSettings.DefaultLogPath;

            if (string.IsNullOrEmpty(SelectedGamePath) && !string.IsNullOrEmpty(_currentSettings.DefaultGamePath))
                SelectedGamePath = _currentSettings.DefaultGamePath;

            if (string.IsNullOrEmpty(SelectedScanDirectory) &&
                !string.IsNullOrEmpty(_currentSettings.DefaultScanDirectory))
                SelectedScanDirectory = _currentSettings.DefaultScanDirectory;

            // Reset pipeline if FCX mode has changed
            if (previousFcxMode != _currentSettings.FcxMode)
            {
                await ResetPipelineAsync();
                AddLogMessage(
                    $"FCX mode {(_currentSettings.FcxMode ? "enabled" : "disabled")} - scan pipeline updated");
            }
        }
        catch (Exception ex)
        {
            AddLogMessage($"Error loading settings: {ex.Message}");
        }
    }

    /// <summary>
    ///     Saves the currently selected paths to the application settings. This includes paths for logs, game directories,
    ///     and scan directories, if they are not empty. The settings are stored asynchronously to ensure persistence.
    /// </summary>
    /// <remarks>
    ///     This method updates the recent paths in the application settings:
    ///     - Adds the selected log path to the recent log files.
    ///     - Adds the selected game path to the recent game paths.
    ///     - Adds the selected scan directory to the recent scan directories.
    ///     Once updated, it saves the user settings using the settings service.
    ///     If an error occurs during the save process, an error message is added to the log.
    /// </remarks>
    /// <returns>
    ///     A task that represents the asynchronous operation of saving the settings.
    /// </returns>
    private async Task SaveCurrentPathsToSettings()
    {
        try
        {
            // Update recent paths
            if (!string.IsNullOrEmpty(SelectedLogPath))
                _currentSettings.AddRecentLogFile(SelectedLogPath);

            if (!string.IsNullOrEmpty(SelectedGamePath))
                _currentSettings.AddRecentGamePath(SelectedGamePath);

            if (!string.IsNullOrEmpty(SelectedScanDirectory))
                _currentSettings.AddRecentScanDirectory(SelectedScanDirectory);

            await _settingsService.SaveUserSettingsAsync(_currentSettings);
        }
        catch (Exception ex)
        {
            AddLogMessage($"Error saving settings: {ex.Message}");
        }
    }

    /// <summary>
    ///     Opens the settings window, allowing the user to modify configuration options.
    ///     After the settings window is closed, the current settings are reloaded asynchronously.
    /// </summary>
    /// <remarks>
    ///     This method creates and initializes the settings window, setting its DataContext to a new
    ///     instance of <see cref="SettingsWindowViewModel" />. The method also ensures that the
    ///     settings window's "Close" action is properly configured. Upon closing the dialog, the
    ///     application reloads settings using an asynchronous operation. Errors encountered during
    ///     this process are logged for troubleshooting.
    /// </remarks>
    /// <returns>
    ///     A task that represents the asynchronous operation of opening the settings window and reloading the settings.
    /// </returns>
    private async Task OpenSettings()
    {
        try
        {
            var settingsWindow = new SettingsWindow
            {
                DataContext = new SettingsWindowViewModel(_settingsService)
            };

            if (settingsWindow.DataContext is SettingsWindowViewModel viewModel)
                viewModel.CloseWindow = () => settingsWindow.Close();

            // Show as dialog and reload settings after closing
            if (TopLevel != null)
            {
                await settingsWindow.ShowDialog(TopLevel);
                await LoadSettingsAsync();
            }
        }
        catch (Exception ex)
        {
            AddLogMessage($"Error opening settings: {ex.Message}");
        }
    }

    /// <summary>
    ///     Attempts to automatically save the scan result using the report writer, if the auto-save setting is enabled.
    /// </summary>
    /// <param name="result">The scan result to be auto-saved, containing the processed data and analysis results.</param>
    /// <returns>
    ///     A task representing the asynchronous operation of saving the report. If the report saving succeeds, the
    ///     operation logs a success message; otherwise, logs an error message upon failure.
    /// </returns>
    private async Task AutoSaveResult(ScanResult result)
    {
        if (_reportWriter == null || !_currentSettings.AutoSaveResults) return;

        try
        {
            var success = await _reportWriter.WriteReportAsync(result);
            if (success) AddLogMessage($"Report auto-saved: {Path.GetFileName(result.OutputPath)}");
        }
        catch (Exception ex)
        {
            AddLogMessage($"Auto-save failed for {Path.GetFileName(result.LogPath)}: {ex.Message}");
        }

        // Move unsolved logs if enabled and scan failed
        if (_currentSettings.MoveUnsolvedLogs &&
            (result.Failed || result.Status == ScanStatus.Failed || result.HasErrors))
            try
            {
                if (_unsolvedLogsMover != null)
                {
                    var moved = await _unsolvedLogsMover.MoveUnsolvedLogAsync(result.LogPath);
                    if (moved) AddLogMessage($"Moved unsolved log: {Path.GetFileName(result.LogPath)}");
                }
            }
            catch (Exception ex)
            {
                AddLogMessage($"Failed to move unsolved log: {ex.Message}");
            }
    }

    /// <summary>
    ///     Exports the currently selected scan result as a report. This process utilizes the configured report writer
    ///     to generate and save the report to the appropriate output path.
    /// </summary>
    /// <remarks>
    ///     If a scan result is selected and the report writer is initialized, this method attempts to export the
    ///     selected result as a report. It ensures error handling by catching exceptions during the export process
    ///     and logs any errors or success messages.
    /// </remarks>
    /// <returns>
    ///     A task representing the asynchronous operation of exporting the selected report.
    /// </returns>
    private async Task ExportSelectedReport()
    {
        if (SelectedResult?.ScanResult == null || _reportWriter == null) return;

        try
        {
            var success = await _reportWriter.WriteReportAsync(SelectedResult.ScanResult);
            AddLogMessage(success
                ? $"Report exported to: {SelectedResult.ScanResult.OutputPath}"
                : "Failed to export report");
        }
        catch (Exception ex)
        {
            AddLogMessage($"Error exporting report: {ex.Message}");
        }
    }

    /// <summary>
    ///     Exports all available scan results to reports using the configured report writer. This method processes each result
    ///     in the current collection, attempts to generate and save a report, and updates the progress to reflect export
    ///     activity.
    /// </summary>
    /// <remarks>
    ///     This method checks if a report writer is properly initialized and if any scan results exist before proceeding.
    ///     It iterates through the list of results and attempts to export each one individually, tracking the number of
    ///     successfully
    ///     exported and failed attempts. During the operation, progress visibility and textual updates are provided to the
    ///     user.
    ///     In case of an error, appropriate feedback is added to the operation log.
    /// </remarks>
    /// <returns>
    ///     A task that performs batch exportation of reports for all scan results, including progress reporting and error
    ///     logging.
    /// </returns>
    private async Task ExportAllReports()
    {
        if (_reportWriter == null || ScanResults.Count == 0) return;

        try
        {
            var exportedCount = 0;
            var failedCount = 0;

            ProgressVisible = true;
            ProgressText = "Exporting reports...";
            ProgressValue = 0;

            for (var i = 0; i < ScanResults.Count; i++)
            {
                var result = ScanResults[i];
                try
                {
                    var success = await _reportWriter.WriteReportAsync(result.ScanResult);
                    if (success)
                        exportedCount++;
                    else
                        failedCount++;
                }
                catch
                {
                    failedCount++;
                }

                ProgressValue = (i + 1) * 100.0 / ScanResults.Count;
                ProgressText = $"Exported {exportedCount}/{ScanResults.Count} reports...";
            }

            ProgressVisible = false;
            AddLogMessage($"Batch export complete: {exportedCount} exported, {failedCount} failed");
        }
        catch (Exception ex)
        {
            ProgressVisible = false;
            AddLogMessage($"Error during batch export: {ex.Message}");
        }
    }

    /// <summary>
    ///     Runs an FCX (File Integrity Check) scan on the game installation.
    /// </summary>
    /// <returns>A task representing the asynchronous FCX scan operation.</returns>
    private async Task RunFcxScan()
    {
        try
        {
            // Check if FCX mode is enabled
            if (!_currentSettings.FcxMode)
            {
                AddLogMessage("FCX mode is not enabled. Please enable it in settings.");
                return;
            }

            // Check if game path is set
            if (string.IsNullOrEmpty(SelectedGamePath))
            {
                AddLogMessage("Please select a game installation path first.");
                return;
            }

            StatusText = "Running FCX integrity checks...";
            ProgressVisible = true;
            ProgressValue = 0;
            ProgressText = "Initializing FCX scan...";
            IsScanning = true;

            _scanCancellationTokenSource = new CancellationTokenSource();

            // Create a FileIntegrityAnalyzer for FCX scanning
            var hashService = new HashValidationService(NullLogger<HashValidationService>.Instance);
            var yamlSettings = new YamlSettingsService(_cacheManager, NullLogger<YamlSettingsService>.Instance);

            // Create an adapter for settings service
            var appSettingsService = new GuiApplicationSettingsAdapter(_settingsService);

            var fileIntegrityAnalyzer = new FileIntegrityAnalyzer(
                hashService,
                appSettingsService,
                yamlSettings,
                _messageHandler ?? _messageHandlerService);

            // Create a synthetic crash log with the game path
            var crashLog = new CrashLog
            {
                FilePath = "FCX_SCAN",
                GamePath = SelectedGamePath
            };

            ProgressText = "Checking game files integrity...";
            ProgressValue = 25;

            // Run the file integrity analysis
            var analysisResult = await fileIntegrityAnalyzer.AnalyzeAsync(crashLog, _scanCancellationTokenSource.Token);

            if (analysisResult is FcxScanResult fcxResult)
            {
                // Update progress
                ProgressValue = 75;
                ProgressText = "Processing results...";

                // Set the FCX result for display
                FcxResult = new FcxResultViewModel(fcxResult);

                // Update status based on results
                switch (fcxResult.GameStatus)
                {
                    case GameIntegrityStatus.Good:
                        StatusText = "FCX scan completed - All checks passed";
                        AddLogMessage("✅ FCX scan completed: Game integrity is good");
                        break;
                    case GameIntegrityStatus.Warning:
                        StatusText = "FCX scan completed - Minor issues found";
                        AddLogMessage($"⚠️ FCX scan completed: {fcxResult.VersionWarnings.Count} warnings found");
                        foreach (var warning in fcxResult.VersionWarnings) AddLogMessage($"   • {warning}");
                        break;
                    case GameIntegrityStatus.Critical:
                        StatusText = "FCX scan completed - Critical issues found";
                        AddLogMessage("❌ FCX scan completed: Critical issues detected");
                        foreach (var fix in fcxResult.RecommendedFixes) AddLogMessage($"   • Recommended: {fix}");
                        break;
                    case GameIntegrityStatus.Invalid:
                        StatusText = "FCX scan failed - Invalid game installation";
                        AddLogMessage("❌ FCX scan failed: Game installation not found or invalid");
                        break;
                }

                // Log file check details
                var failedChecks = fcxResult.FileChecks.Where(fc => !fc.IsValid).ToList();
                if (failedChecks.Any())
                {
                    AddLogMessage($"Failed file checks: {failedChecks.Count}");
                    foreach (var check in failedChecks.Take(5)) // Show first 5 failures
                        AddLogMessage($"   • {Path.GetFileName(check.FilePath)}: {check.ErrorMessage}");
                }
            }
            else
            {
                StatusText = "FCX scan completed with unexpected result";
                AddLogMessage("FCX scan completed but result format was unexpected");
            }

            ProgressValue = 100;
            await Task.Delay(500); // Brief pause to show completion
        }
        catch (OperationCanceledException)
        {
            StatusText = "FCX scan cancelled";
            AddLogMessage("FCX scan was cancelled by user.");
        }
        catch (Exception ex)
        {
            StatusText = "FCX scan failed";
            AddLogMessage($"Error during FCX scan: {ex.Message}");
        }
        finally
        {
            IsScanning = false;
            ProgressVisible = false;
            _scanCancellationTokenSource?.Dispose();
            _scanCancellationTokenSource = null;
        }
    }

    /// <summary>
    ///     Backs up critical game files.
    /// </summary>
    /// <returns>A task representing the asynchronous backup operation.</returns>
    private async Task BackupGameFiles()
    {
        try
        {
            if (string.IsNullOrEmpty(SelectedGamePath))
            {
                AddLogMessage("Please select a game installation path first.");
                return;
            }

            StatusText = "Backing up game files...";
            ProgressVisible = true;
            ProgressValue = 0;
            ProgressText = "Preparing backup...";
            IsScanning = true;

            _scanCancellationTokenSource = new CancellationTokenSource();

            // Create backup service
            var appSettingsService = new GuiApplicationSettingsAdapter(_settingsService);
            var backupService = new BackupService(NullLogger<BackupService>.Instance, appSettingsService);

            // Create progress reporter
            var progress = new Progress<BackupProgress>(p =>
            {
                ProgressValue = p.PercentComplete;
                ProgressText = $"Backing up {Path.GetFileName(p.CurrentFile)}... ({p.ProcessedFiles}/{p.TotalFiles})";
            });

            AddLogMessage("Starting backup of critical game files...");

            // Perform full backup of critical files
            var result = await backupService.CreateFullBackupAsync(
                SelectedGamePath,
                progress,
                _scanCancellationTokenSource.Token);

            if (result.Success)
            {
                StatusText = "Backup completed successfully";
                AddLogMessage("✅ Backup completed successfully!");
                AddLogMessage($"   • Location: {result.BackupPath}");
                AddLogMessage($"   • Files backed up: {result.BackedUpFiles.Count}");
                AddLogMessage(
                    $"   • Total size: {result.TotalSize:N0} bytes ({result.TotalSize / 1024.0 / 1024.0:F1} MB)");

                // Show some of the backed up files
                var importantFiles = result.BackedUpFiles
                    .Where(f => f.EndsWith(".exe") || f.EndsWith(".dll") || f.EndsWith(".esm"))
                    .Take(5)
                    .ToList();

                if (importantFiles.Any())
                {
                    AddLogMessage("   • Important files included:");
                    foreach (var file in importantFiles) AddLogMessage($"     - {file}");
                }

                // Offer to open backup folder
                if (ShowFolderPickerAsync != null)
                {
                    var backupDir = Path.GetDirectoryName(result.BackupPath);
                    AddLogMessage($"\nBackup saved to: {backupDir}");
                }
            }
            else
            {
                StatusText = "Backup failed";
                AddLogMessage($"❌ Backup failed: {result.ErrorMessage}");

                if (result.BackedUpFiles.Count > 0)
                    AddLogMessage($"   • Partially backed up {result.BackedUpFiles.Count} files before failure");
            }

            ProgressValue = 100;
            await Task.Delay(500); // Brief pause to show completion
        }
        catch (OperationCanceledException)
        {
            StatusText = "Backup cancelled";
            AddLogMessage("Backup was cancelled by user.");
        }
        catch (Exception ex)
        {
            StatusText = "Backup failed";
            AddLogMessage($"Error during backup: {ex.Message}");
        }
        finally
        {
            IsScanning = false;
            ProgressVisible = false;
            _scanCancellationTokenSource?.Dispose();
            _scanCancellationTokenSource = null;
        }
    }

    /// <summary>
    ///     Validates the game installation for missing or corrupted files.
    /// </summary>
    /// <returns>A task representing the asynchronous validation operation.</returns>
    private async Task ValidateGameInstall()
    {
        try
        {
            if (string.IsNullOrEmpty(SelectedGamePath))
            {
                AddLogMessage("Please select a game installation path first.");
                return;
            }

            StatusText = "Validating game installation...";
            ProgressVisible = true;
            ProgressValue = 0;
            ProgressText = "Preparing validation...";
            IsScanning = true;

            _scanCancellationTokenSource = new CancellationTokenSource();

            var hashService = new HashValidationService(NullLogger<HashValidationService>.Instance);

            // Define critical game files to validate
            var criticalFiles = new Dictionary<string, string>
            {
                ["Fallout4.exe"] = "Main game executable",
                ["Fallout4.cdx"] = "Game content index",
                ["Fallout4 - Animations.ba2"] = "Animation archive",
                ["Fallout4 - Interface.ba2"] = "Interface archive",
                ["Fallout4 - Materials.ba2"] = "Materials archive",
                ["Fallout4 - Meshes.ba2"] = "Meshes archive",
                ["Fallout4 - MeshesExtra.ba2"] = "Extra meshes archive",
                ["Fallout4 - Misc.ba2"] = "Miscellaneous archive",
                ["Fallout4 - Shaders.ba2"] = "Shaders archive",
                ["Fallout4 - Sounds.ba2"] = "Sounds archive",
                ["Fallout4 - Startup.ba2"] = "Startup archive",
                ["Fallout4 - Textures1.ba2"] = "Textures archive 1",
                ["Fallout4 - Textures2.ba2"] = "Textures archive 2",
                ["Fallout4 - Textures3.ba2"] = "Textures archive 3"
            };

            var validationResults = new List<string>();
            var missingFiles = new List<string>();
            var corruptedFiles = new List<string>();
            var totalFiles = criticalFiles.Count;
            var processedFiles = 0;

            AddLogMessage($"Validating {totalFiles} critical game files...");

            foreach (var (fileName, description) in criticalFiles)
            {
                if (_scanCancellationTokenSource.Token.IsCancellationRequested)
                    break;

                var filePath = Path.Combine(SelectedGamePath, fileName);
                processedFiles++;

                ProgressValue = processedFiles * 100.0 / totalFiles;
                ProgressText = $"Checking {fileName}...";

                if (!File.Exists(filePath))
                {
                    missingFiles.Add($"{fileName} ({description})");
                    validationResults.Add($"❌ Missing: {fileName}");
                }
                else
                {
                    try
                    {
                        // Check file size and basic integrity
                        var fileInfo = new FileInfo(filePath);

                        // Minimum size checks for BA2 archives
                        if (fileName.EndsWith(".ba2", StringComparison.OrdinalIgnoreCase))
                        {
                            if (fileInfo.Length < 1024 * 1024) // Less than 1MB is suspicious for BA2
                            {
                                corruptedFiles.Add($"{fileName} ({description}) - File too small");
                                validationResults.Add($"⚠️ Suspicious: {fileName} - Size: {fileInfo.Length:N0} bytes");
                            }
                            else
                            {
                                validationResults.Add($"✅ Valid: {fileName} - Size: {fileInfo.Length:N0} bytes");
                            }
                        }
                        else if (fileName == "Fallout4.exe")
                        {
                            // Calculate hash for the executable
                            var hash = await hashService.CalculateFileHashAsync(filePath,
                                _scanCancellationTokenSource.Token);
                            var shortHash = hash.Length > 8 ? hash.Substring(0, 8) : hash;
                            validationResults.Add($"✅ Valid: {fileName} - Hash: {shortHash}...");
                        }
                        else
                        {
                            validationResults.Add($"✅ Valid: {fileName}");
                        }
                    }
                    catch (Exception ex)
                    {
                        corruptedFiles.Add($"{fileName} ({description}) - {ex.Message}");
                        validationResults.Add($"❌ Error: {fileName} - {ex.Message}");
                    }
                }

                // Small delay to show progress
                await Task.Delay(50, _scanCancellationTokenSource.Token);
            }

            // Report results
            AddLogMessage("\nValidation Results:");
            AddLogMessage($"Total files checked: {processedFiles}");
            AddLogMessage($"✅ Valid files: {processedFiles - missingFiles.Count - corruptedFiles.Count}");

            if (missingFiles.Count > 0)
            {
                AddLogMessage($"❌ Missing files: {missingFiles.Count}");
                foreach (var file in missingFiles.Take(5)) AddLogMessage($"   • {file}");
                if (missingFiles.Count > 5) AddLogMessage($"   ... and {missingFiles.Count - 5} more");
            }

            if (corruptedFiles.Count > 0)
            {
                AddLogMessage($"⚠️ Suspicious files: {corruptedFiles.Count}");
                foreach (var file in corruptedFiles.Take(5)) AddLogMessage($"   • {file}");
            }

            // Overall status
            if (missingFiles.Count == 0 && corruptedFiles.Count == 0)
            {
                StatusText = "Game validation passed - All files valid";
                AddLogMessage("\n✅ Game installation is valid!");
            }
            else if (missingFiles.Count > 0)
            {
                StatusText = $"Game validation failed - {missingFiles.Count} files missing";
                AddLogMessage("\n❌ Game installation has issues. Consider verifying game files through Steam/GOG.");
            }
            else
            {
                StatusText = "Game validation completed with warnings";
                AddLogMessage("\n⚠️ Game installation may have issues. Consider verifying game files.");
            }

            ProgressValue = 100;
            await Task.Delay(500); // Brief pause to show completion
        }
        catch (OperationCanceledException)
        {
            StatusText = "Validation cancelled";
            AddLogMessage("Game validation was cancelled by user.");
        }
        catch (Exception ex)
        {
            StatusText = "Validation failed";
            AddLogMessage($"Error during validation: {ex.Message}");
        }
        finally
        {
            IsScanning = false;
            ProgressVisible = false;
            _scanCancellationTokenSource?.Dispose();
            _scanCancellationTokenSource = null;
        }
    }

    // Recent Files Command Implementations
    private async Task OpenRecentFile(string path)
    {
        if (string.IsNullOrEmpty(path)) return;

        if (File.Exists(path))
        {
            SelectedLogPath = path;
            _recentItemsService?.AddRecentLogFile(path);
            UpdateRecentFiles();

            // Automatically start scan
            if (!string.IsNullOrEmpty(SelectedLogPath)) await ExecuteScan();
        }
        else
        {
            AddLogMessage($"Recent file not found: {path}");
            _recentItemsService?.RemoveRecentItem(RecentItemType.LogFile, path);
            UpdateRecentFiles();
        }
    }

    private void ClearRecentFiles()
    {
        _recentItemsService?.ClearRecentLogFiles();
        UpdateRecentFiles();
        AddLogMessage("Recent files cleared");
    }

    private void UpdateRecentFiles()
    {
        if (_recentItemsService != null)
        {
            var recentItems = _recentItemsService.GetRecentLogFiles();
            RecentFiles = new ObservableCollection<RecentItem>(recentItems);
            this.RaisePropertyChanged(nameof(HasRecentFiles));
        }
    }

    // View Command Implementations
    private async Task ShowStatistics()
    {
        if (_statisticsService != null && TopLevel != null)
        {
            var summary = await _statisticsService.GetSummaryAsync();
            var statsWindow = new StatisticsWindow
            {
                DataContext = new StatisticsViewModel(_statisticsService)
            };
            await statsWindow.ShowDialog(TopLevel);
        }
    }

    private async Task ShowPapyrusMonitor()
    {
        if (TopLevel != null)
        {
            // For now, create services manually since we don't have access to DI container here
            // In a future refactor, we could pass IServiceProvider to MainWindowViewModel
            var yamlSettings = new YamlSettingsService(_cacheManager, NullLogger<YamlSettingsService>.Instance);
            var papyrusService = new PapyrusMonitorService(
                new GuiApplicationSettingsAdapter(_settingsService),
                yamlSettings);
            var viewModel = new PapyrusMonitorViewModel(
                papyrusService,
                new GuiApplicationSettingsAdapter(_settingsService),
                _messageHandlerService,
                _audioNotificationService);

            var papyrusWindow = new PapyrusMonitorWindow
            {
                DataContext = viewModel
            };
            await papyrusWindow.ShowDialog(TopLevel);
        }
    }

    private async Task ShowHelp()
    {
        if (TopLevel != null)
        {
            var helpWindow = new HelpWindow();
            await helpWindow.ShowDialog(TopLevel);
        }
    }

    private async Task ShowKeyboardShortcuts()
    {
        if (TopLevel != null)
        {
            var shortcutsWindow = new KeyboardShortcutsWindow();
            await shortcutsWindow.ShowDialog(TopLevel);
        }
    }

    private async Task ShowAbout()
    {
        if (TopLevel != null)
        {
            var aboutWindow = new AboutWindow();
            await aboutWindow.ShowDialog(TopLevel);
        }
    }

    private async Task SetTheme(string themeName)
    {
        _themeService?.SetTheme(themeName);
        AddLogMessage($"Theme changed to: {themeName}");
        await Task.CompletedTask;
    }

    private void Exit()
    {
        if (TopLevel is Window window) window.Close();
    }
}