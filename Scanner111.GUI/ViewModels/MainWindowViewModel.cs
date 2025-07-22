using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Reactive;
using System.Reactive.Linq;
using System.Threading;
using System.Threading.Tasks;
using Avalonia.Controls;
using Avalonia.Threading;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using ReactiveUI;
using Scanner111.Core.Infrastructure;
using Scanner111.Core.Models;
using Scanner111.Core.Pipeline;
using Scanner111.GUI.Models;
using Scanner111.GUI.Services;
using Scanner111.GUI.Views;

namespace Scanner111.GUI.ViewModels;

/// <summary>
/// Represents the main ViewModel for the application's main window.
/// </summary>
/// <remarks>
/// This ViewModel is responsible for managing the main operations of the application,
/// including file and folder selection, initiating and canceling scanning, managing scan results, and handling progress updates.
/// It provides commands and properties to support the user interface.
/// Inherits from <see cref="ViewModelBase"/> to leverage reactive property functionality.
/// </remarks>
public class MainWindowViewModel : ViewModelBase
{
    private readonly ISettingsService _settingsService;
    private readonly GuiMessageHandlerService _messageHandlerService;
    private UserSettings _currentSettings;
    private bool _isScanning;
    private IMessageHandler? _messageHandler;
    private string _progressText = "";
    private double _progressValue;
    private bool _progressVisible;
    private IReportWriter? _reportWriter;
    private CancellationTokenSource? _scanCancellationTokenSource;

    private IScanPipeline? _scanPipeline;
    private string _selectedGamePath = "";
    private string _selectedLogPath = "";
    private ScanResultViewModel? _selectedResult;
    private string _selectedScanDirectory = "";
    private string _statusText = "Ready";

    public MainWindowViewModel(ISettingsService settingsService, GuiMessageHandlerService messageHandlerService)
    {
        _settingsService = settingsService ?? throw new ArgumentNullException(nameof(settingsService));
        _messageHandlerService = messageHandlerService ?? throw new ArgumentNullException(nameof(messageHandlerService));
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

        StatusText = "Ready - Select a crash log file to begin";

        // Load settings asynchronously
        _ = LoadSettingsAsync();
    }

    /// <summary>
    /// Gets or sets the current status message displayed in the application's user interface.
    /// The <c>StatusText</c> property is typically used to provide feedback or updates to the user
    /// about the current state of operations, such as scanning progress, errors, or user actions.
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
    /// Gets or sets a value indicating whether a scanning operation is currently in progress.
    /// The <c>IsScanning</c> property is used to control and reflect the state of the scanning process,
    /// ensuring UI elements react accordingly, such as disabling interactions or displaying progress indicators.
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
    /// Gets or sets the progress value representing the percentage of completion for the current operation.
    /// The <c>ProgressValue</c> property is typically used to update progress indicators in the user interface
    /// and ranges from 0.0 to 100.0, where 100.0 indicates the completion of the operation.
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
    /// Gets or sets the text indicating the current progress of an operation.
    /// The <c>ProgressText</c> property is typically used to provide descriptive feedback
    /// to the user about the ongoing progress, such as the number of files processed during a scan or export operation.
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
    /// Gets or sets a value indicating whether the progress indicator is visible in the user interface.
    /// The <c>ProgressVisible</c> property is used to show or hide visual elements
    /// related to progress during operations such as scanning or exporting.
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
    /// Gets or sets the file path of the currently selected log for analysis or processing.
    /// The <c>SelectedLogPath</c> property is used to specify the location of a log file,
    /// enabling the application to read or process its contents as part of the scanning workflow.
    /// Changes to this property may trigger updates in the UI or internal operations, such as
    /// populating scan results or adding log messages.
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
    /// Gets or sets the file system path to the selected game directory.
    /// The <c>SelectedGamePath</c> property is used to indicate the root folder
    /// of the game files required for various operations such as log scanning or processing.
    /// Its value can influence how specific tasks are executed, such as searching for logs
    /// or detecting specific configurations within the game directory.
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
    /// Gets or sets the directory path selected for scanning log files.
    /// The <c>SelectedScanDirectory</c> property is typically used to define the location
    /// from which files are collected for analysis or processing. Changes to this property
    /// may trigger updates to the application's state or initiate file loading processes.
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
    /// Gets or sets the currently selected scan result in the user interface.
    /// The <c>SelectedResult</c> property represents the item that the user has chosen
    /// from the list of available scan results and is used to facilitate actions
    /// like exporting detailed reports or displaying result-specific information.
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

    public ReactiveCommand<Unit, Unit> SelectLogFileCommand { get; }
    public ReactiveCommand<Unit, Unit> SelectGamePathCommand { get; }
    public ReactiveCommand<Unit, Unit> SelectScanDirectoryCommand { get; }
    public ReactiveCommand<Unit, Unit> ScanCommand { get; }
    public ReactiveCommand<Unit, Unit> CancelScanCommand { get; }
    public ReactiveCommand<Unit, Unit> ClearResultsCommand { get; }
    public ReactiveCommand<Unit, Unit> OpenSettingsCommand { get; }
    public ReactiveCommand<Unit, Unit> ExportSelectedReportCommand { get; }
    public ReactiveCommand<Unit, Unit> ExportAllReportsCommand { get; }

    // File picker delegates - set by the View
    public Func<string, string, Task<string>>? ShowFilePickerAsync { get; set; }
    public Func<string, Task<string>>? ShowFolderPickerAsync { get; set; }
    public Window? TopLevel { get; set; }

    /// <summary>
    /// Ensures that the scan pipeline is initialized for the application. If the pipeline has not yet been initialized,
    /// this method sets up the necessary components, including a message handler, caching, enhanced error handling,
    /// and logging configuration.
    /// </summary>
    /// <remarks>
    /// This method initializes necessary components for the scan pipeline:
    /// - A message handler (GUI-specific implementation).
    /// - Default analyzers added to the pipeline.
    /// - Optional caching and error handling mechanisms.
    /// - Logging system, such as console-based logging.
    /// Additionally, it initializes the report writer with a null logger, as console logging is not required for the GUI.
    /// </remarks>
    private void EnsurePipelineInitialized()
    {
        if (_scanPipeline != null) return;
        _messageHandler = _messageHandlerService;

        _scanPipeline = new ScanPipelineBuilder()
            .AddDefaultAnalyzers()
            .WithMessageHandler(_messageHandler)
            .WithCaching()
            .WithEnhancedErrorHandling()
            .WithLogging(builder => builder.AddConsole())
            .Build();

        // Initialize report writer with null logger (GUI doesn't need console logging)
        _reportWriter = new ReportWriter(NullLogger<ReportWriter>.Instance);
    }

    /// <summary>
    /// Opens a file picker to allow the user to select a crash log file. Once a file is selected, this method updates the
    /// selected log file path, displays the file name in the status text, logs the file selection, and saves the current
    /// paths to the settings.
    /// </summary>
    /// <remarks>
    /// This method displays a dialog for file selection. If a file is selected:
    /// - The file path is set to the SelectedLogPath property.
    /// - The file name is shown in the StatusText property.
    /// - A log message is added with the selected file path.
    /// - The selected paths are persisted to user settings.
    /// If an error occurs during file selection, an error message is displayed in the status text, and the error is logged.
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
    /// Prompts the user to select a directory for the game installation. Once a directory is selected, it updates the
    /// selected game path, logs the chosen path, and saves the updated path to the user settings.
    /// </summary>
    /// <remarks>
    /// This method uses a folder picker dialog to allow the user to select the game installation directory.
    /// If a valid path is chosen, the following steps are executed:
    /// - Updates the SelectedGamePath property with the chosen path.
    /// - Logs the selected game path for user reference.
    /// - Asynchronously saves the updated paths to the application settings.
    /// If an error occurs during path selection or saving, it logs the error message.
    /// </remarks>
    /// <returns>
    /// A task representing the asynchronous operation of selecting a game path, with no direct result.
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
    /// Prompts the user to select a directory for scanning crash logs and updates the application state with the selected path.
    /// </summary>
    /// <remarks>
    /// This method displays a folder picker dialog to the user, allowing them to select the directory to be scanned for crash logs.
    /// Once a valid directory is selected:
    /// - The selected directory path is stored in the <see cref="SelectedScanDirectory"/> property.
    /// - A log message is added to indicate the selected directory.
    /// - The application paths are saved to the settings asynchronously.
    /// If an error occurs during the directory selection process, a corresponding log message is added.
    /// </remarks>
    /// <returns>
    /// A task that represents the asynchronous operation of selecting a directory.
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
    /// Executes the scan process for analyzing crash log files. This method initializes the scan pipeline, processes
    /// the necessary configurations, and performs the scanning operation on the selected files, updating the user
    /// interface with progress and status information throughout the operation.
    /// </summary>
    /// <remarks>
    /// This method handles the following tasks:
    /// - Initializes the scan pipeline using the <c>EnsurePipelineInitialized</c> method if it has not already been set up.
    /// - Updates the scanning status, progress bar, and related UI elements during the scan lifecycle.
    /// - Collects the specified files marked for analysis and logs their count.
    /// - Executes log analysis on each file and accumulates results, updating the progress asynchronously.
    /// - Handles cancellation requests by the user during the scan process.
    /// - Manages error handling in case of scan failures due to unexpected exceptions.
    /// - Cleans up resources, such as cancellation tokens, used for coordinating the scanning operation.
    /// </remarks>
    /// <returns>
    /// A task representing the asynchronous operation of the scan process.
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
    /// Collects a list of files to be scanned based on selected paths and specific criteria. This method consolidates
    /// files from multiple sources including auto-copied logs, a manually selected log file, and logs located in a
    /// specified directory.
    /// </summary>
    /// <remarks>
    /// This method performs the following operations to gather files for scanning:
    /// - Automatically retrieves and copies XSE logs (e.g., F4SE and SKSE).
    /// - Adds a single, manually selected log file if specified and valid.
    /// - Scans a user-selected directory to find and include valid log files.
    /// The returned list ensures no duplicate entries and only includes valid files.
    /// </remarks>
    /// <returns>
    /// A task representing the asynchronous operation, containing a list of file paths to be scanned.
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
    /// Copies XSE crash logs (F4SE and SKSE) from commonly known directories to a specified crash logs directory.
    /// If the user has disabled this feature via settings, the operation is skipped. The method identifies potential
    /// source directories (e.g., Fallout4 and Skyrim-related SKSE/F4SE paths) and attempts to copy logs to the configured
    /// destination, logging the results of the process.
    /// </summary>
    /// <param name="filesToScan">
    /// A list of file paths that may be updated with paths to the copied XSE crash logs, depending on the process.
    /// </param>
    /// <returns>
    /// A task that represents the asynchronous operation for copying XSE logs. The task completes once the logs have been
    /// processed or skipped.
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
    /// Scans a specified directory for log files matching predefined criteria and adds them to a given collection of files to be scanned.
    /// The method identifies log files by extension (.log or .txt) and checks if their names contain keywords such as "crash", "dump", or "error".
    /// </summary>
    /// <param name="directory">The path of the directory to scan for log files.</param>
    /// <param name="filesToScan">A list to which the discovered log files will be added if they meet the criteria and do not already exist in the list.</param>
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
    /// Cancels the currently running scan operation, if any, by signaling the associated cancellation token.
    /// Updates the application status to reflect the cancellation state and logs an appropriate message.
    /// </summary>
    /// <remarks>
    /// This method invokes the cancellation token's cancel signal to stop an ongoing scan process.
    /// Post-cancellation actions include updating the status text with a "Cancelling scan..." message
    /// and adding a log entry to denote that a scan cancellation was requested by the user.
    /// </remarks>
    private void CancelScan()
    {
        _scanCancellationTokenSource?.Cancel();
        StatusText = "Cancelling scan...";
        AddLogMessage("Scan cancellation requested.");
    }

    /// <summary>
    /// Clears all scan results and log messages from the application. Resets the associated user interface elements,
    /// including status text and progress visibility.
    /// </summary>
    /// <remarks>
    /// This method performs the following actions:
    /// - Clears the collection of scan results.
    /// - Clears the collection of log messages.
    /// - Updates the status text to indicate that results have been cleared.
    /// - Hides the progress indicator.
    /// It ensures that the UI reflects a fresh state, ready for a new scanning operation.
    /// </remarks>
    private void ClearResults()
    {
        ScanResults.Clear();
        LogMessages.Clear();
        StatusText = "Results cleared";
        ProgressVisible = false;
    }

    /// <summary>
    /// Adds a log message to the log messages collection with a timestamp. This helps track user actions, errors,
    /// or other significant events as they occur in the application.
    /// </summary>
    /// <param name="message">The content of the log message to be added. This should describe the event or error being logged.</param>
    /// <remarks>
    /// The log message is prefixed with the current time in "HH:mm:ss" format. This method also ensures that
    /// the log messages collection does not exceed 100 entries by removing the oldest messages when necessary.
    /// Thread-safe updates to the UI are handled via a helper method.
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
    /// Executes the specified action on the UI thread. If the calling thread is already
    /// the UI thread, the action is executed directly; otherwise, it is dispatched
    /// to the UI thread for execution using Avalonia's dispatcher system.
    /// </summary>
    /// <param name="action">The action to be performed on the UI thread.</param>
    /// <remarks>
    /// This method ensures that UI-related actions are executed safely and consistently
    /// on the UI thread. It checks the current thread's access and dispatches the
    /// action if required, providing thread safety for UI updates in the application.
    /// </remarks>
    private void UpdateUi(Action action)
    {
        if (Dispatcher.UIThread.CheckAccess())
            action();
        else
            Dispatcher.UIThread.InvokeAsync(action);
    }

    /// <summary>
    /// Asynchronously loads and applies user settings to the application. This method retrieves the settings
    /// from a storage service and updates relevant properties such as default paths for logs, game files,
    /// and scan directories. If any settings are missing, existing values remain unchanged.
    /// </summary>
    /// <remarks>
    /// During the loading process, default paths from the user settings are applied to the following properties
    /// if they are currently empty:
    /// - SelectedLogPath: Set to the default log path if available.
    /// - SelectedGamePath: Set to the default game path if available.
    /// - SelectedScanDirectory: Set to the default scan directory if available.
    /// In case of an error during the load operation, the error message is logged for troubleshooting purposes.
    /// </remarks>
    /// <returns>
    /// A task representing the asynchronous operation. The task completes when the settings are successfully loaded
    /// or when an error occurs during the loading process.
    /// </returns>
    private async Task LoadSettingsAsync()
    {
        try
        {
            _currentSettings = await _settingsService.LoadUserSettingsAsync();

            // Apply default paths from settings if current paths are empty
            if (string.IsNullOrEmpty(SelectedLogPath) && !string.IsNullOrEmpty(_currentSettings.DefaultLogPath))
                SelectedLogPath = _currentSettings.DefaultLogPath;

            if (string.IsNullOrEmpty(SelectedGamePath) && !string.IsNullOrEmpty(_currentSettings.DefaultGamePath))
                SelectedGamePath = _currentSettings.DefaultGamePath;

            if (string.IsNullOrEmpty(SelectedScanDirectory) &&
                !string.IsNullOrEmpty(_currentSettings.DefaultScanDirectory))
                SelectedScanDirectory = _currentSettings.DefaultScanDirectory;
        }
        catch (Exception ex)
        {
            AddLogMessage($"Error loading settings: {ex.Message}");
        }
    }

    /// <summary>
    /// Saves the currently selected paths to the application settings. This includes paths for logs, game directories,
    /// and scan directories, if they are not empty. The settings are stored asynchronously to ensure persistence.
    /// </summary>
    /// <remarks>
    /// This method updates the recent paths in the application settings:
    /// - Adds the selected log path to the recent log files.
    /// - Adds the selected game path to the recent game paths.
    /// - Adds the selected scan directory to the recent scan directories.
    /// Once updated, it saves the user settings using the settings service.
    /// If an error occurs during the save process, an error message is added to the log.
    /// </remarks>
    /// <returns>
    /// A task that represents the asynchronous operation of saving the settings.
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
    /// Opens the settings window, allowing the user to modify configuration options.
    /// After the settings window is closed, the current settings are reloaded asynchronously.
    /// </summary>
    /// <remarks>
    /// This method creates and initializes the settings window, setting its DataContext to a new
    /// instance of <see cref="SettingsWindowViewModel"/>. The method also ensures that the
    /// settings window's "Close" action is properly configured. Upon closing the dialog, the
    /// application reloads settings using an asynchronous operation. Errors encountered during
    /// this process are logged for troubleshooting.
    /// </remarks>
    /// <returns>
    /// A task that represents the asynchronous operation of opening the settings window and reloading the settings.
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
    /// Attempts to automatically save the scan result using the report writer, if the auto-save setting is enabled.
    /// </summary>
    /// <param name="result">The scan result to be auto-saved, containing the processed data and analysis results.</param>
    /// <returns>A task representing the asynchronous operation of saving the report. If the report saving succeeds, the operation logs a success message; otherwise, logs an error message upon failure.</returns>
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
    }

    /// <summary>
    /// Exports the currently selected scan result as a report. This process utilizes the configured report writer
    /// to generate and save the report to the appropriate output path.
    /// </summary>
    /// <remarks>
    /// If a scan result is selected and the report writer is initialized, this method attempts to export the
    /// selected result as a report. It ensures error handling by catching exceptions during the export process
    /// and logs any errors or success messages.
    /// </remarks>
    /// <returns>
    /// A task representing the asynchronous operation of exporting the selected report.
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
    /// Exports all available scan results to reports using the configured report writer. This method processes each result
    /// in the current collection, attempts to generate and save a report, and updates the progress to reflect export activity.
    /// </summary>
    /// <remarks>
    /// This method checks if a report writer is properly initialized and if any scan results exist before proceeding.
    /// It iterates through the list of results and attempts to export each one individually, tracking the number of successfully
    /// exported and failed attempts. During the operation, progress visibility and textual updates are provided to the user.
    /// In case of an error, appropriate feedback is added to the operation log.
    /// </remarks>
    /// <returns>
    /// A task that performs batch exportation of reports for all scan results, including progress reporting and error logging.
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
}

/// <summary>
/// Handles and processes messages for the GUI layer in the application.
/// </summary>
/// <remarks>
/// This class is responsible for displaying various types of messages in the GUI, including informational, warning, error, success, debug, and critical messages.
/// It interacts with the GUI using the provided <see cref="MainWindowViewModel"/> instance and ensures messages are routed appropriately based on the specified target.
/// Implements the <see cref="IMessageHandler"/> interface to provide a contract for message handling within the application.
/// </remarks>
public class GuiMessageHandler : IMessageHandler
{
    private readonly MainWindowViewModel _viewModel;

    public GuiMessageHandler(MainWindowViewModel viewModel)
    {
        _viewModel = viewModel;
    }

    /// <summary>
    /// Displays an informational message to the user interface, if the specified target allows GUI messaging.
    /// The message is prefixed with an information icon for better visibility.
    /// </summary>
    /// <param name="message">The informational message to be displayed.</param>
    /// <param name="target">
    /// Specifies where the message should be directed. If set to <see cref="MessageTarget.CliOnly"/>, the method does nothing.
    /// Defaults to <see cref="MessageTarget.All"/>, which displays the message in the GUI.
    /// </param>
    public void ShowInfo(string message, MessageTarget target = MessageTarget.All)
    {
        if (target == MessageTarget.CliOnly) return;
        Dispatcher.UIThread.InvokeAsync(() => _viewModel.AddLogMessage($"ℹ️ INFO: {message}"));
    }

    /// <summary>
    /// Displays a warning message in the GUI by appending it to the log messages of the main view model.
    /// The warning is prefixed with a warning symbol for user clarity.
    /// </summary>
    /// <param name="message">The warning message to be displayed.</param>
    /// <param name="target">
    /// Specifies the target where the message should be delivered. By default, it is delivered to all targets.
    /// If the target is set to <see cref="MessageTarget.CliOnly"/>, the method does not perform any action.
    /// </param>
    public void ShowWarning(string message, MessageTarget target = MessageTarget.All)
    {
        if (target == MessageTarget.CliOnly) return;
        Dispatcher.UIThread.InvokeAsync(() => _viewModel.AddLogMessage($"⚠️ WARNING: {message}"));
    }

    /// <summary>
    /// Displays an error message in the GUI log. This method sends the specified error message
    /// to the user interface, indicating an issue that requires attention.
    /// </summary>
    /// <param name="message">The error message to be displayed in the GUI log.</param>
    /// <param name="target">
    /// Specifies the target destination for the message. By default, the message is sent
    /// to all message targets, but it can be limited to GUI-specific contexts.
    /// </param>
    public void ShowError(string message, MessageTarget target = MessageTarget.All)
    {
        if (target == MessageTarget.CliOnly) return;
        Dispatcher.UIThread.InvokeAsync(() => _viewModel.AddLogMessage($"❌ ERROR: {message}"));
    }

    /// <summary>
    /// Displays a success message in the GUI log.
    /// </summary>
    /// <param name="message">The success message to display.</param>
    /// <param name="target">Specifies the target audience for the message. Defaults to all targets.</param>
    /// <remarks>
    /// If the <paramref name="target"/> is set to <see cref="MessageTarget.CliOnly"/>, the message will not be displayed in the GUI log.
    /// The method utilizes the UI thread to ensure the message is added to the log in a thread-safe manner.
    /// </remarks>
    public void ShowSuccess(string message, MessageTarget target = MessageTarget.All)
    {
        if (target == MessageTarget.CliOnly) return;
        Dispatcher.UIThread.InvokeAsync(() => _viewModel.AddLogMessage($"✅ SUCCESS: {message}"));
    }

    /// <summary>
    /// Displays a debug message in the GUI, prefixed with a debug indicator.
    /// This method ensures the message is added to the logs only for non-CLI exclusive targets.
    /// </summary>
    /// <param name="message">The debug message to be displayed in the GUI.</param>
    /// <param name="target">Specifies the target audience for the message, with the default being all applicable targets.</param>
    public void ShowDebug(string message, MessageTarget target = MessageTarget.All)
    {
        if (target == MessageTarget.CliOnly) return;
        Dispatcher.UIThread.InvokeAsync(() => _viewModel.AddLogMessage($"🔍 DEBUG: {message}"));
    }

    /// <summary>
    /// Displays a critical message and logs it using the associated view model.
    /// This method ensures that critical messages are visually distinguished in the log,
    /// allowing users to quickly identify and address critical issues.
    /// </summary>
    /// <param name="message">The critical message to be displayed and logged.</param>
    /// <param name="target">
    /// Specifies the target for the message. If set to <see cref="MessageTarget.CliOnly"/>,
    /// the method does not perform any action. The default value is <see cref="MessageTarget.All"/>.
    /// </param>
    public void ShowCritical(string message, MessageTarget target = MessageTarget.All)
    {
        if (target == MessageTarget.CliOnly) return;
        Dispatcher.UIThread.InvokeAsync(() => _viewModel.AddLogMessage($"🚨 CRITICAL: {message}"));
    }

    /// <summary>
    /// Displays a message in the GUI log with additional optional details and a categorization by message type.
    /// The method prefixes the log entry with an appropriate symbol representing the message type (info, warning, error, etc.).
    /// </summary>
    /// <param name="message">The main message text to display in the log.</param>
    /// <param name="details">Optional additional details to provide more context for the message.</param>
    /// <param name="messageType">The category or type of the message (e.g., info, warning, error).</param>
    /// <param name="target">Specifies the intended target for the message (e.g., GUI only, CLI only, all).</param>
    public void ShowMessage(string message, string? details = null, MessageType messageType = MessageType.Info,
        MessageTarget target = MessageTarget.All)
    {
        if (target == MessageTarget.CliOnly) return;

        var prefix = messageType switch
        {
            MessageType.Info => "ℹ️ INFO",
            MessageType.Warning => "⚠️ WARNING",
            MessageType.Error => "❌ ERROR",
            MessageType.Success => "✅ SUCCESS",
            MessageType.Debug => "🔍 DEBUG",
            MessageType.Critical => "🚨 CRITICAL",
            _ => "INFO"
        };

        var fullMessage = details != null ? $"{message}\nDetails: {details}" : message;
        Dispatcher.UIThread.InvokeAsync(() => _viewModel.AddLogMessage($"{prefix}: {fullMessage}"));
    }

    /// <summary>
    /// Displays a progress indicator to the user with a given title and total number of items.
    /// Additionally, it initializes a progress tracking object that can be used to report updates
    /// during long-running operations.
    /// </summary>
    /// <param name="title">The title or description displayed with the progress indicator.</param>
    /// <param name="totalItems">The total number of items to track progress for.</param>
    /// <returns>An object implementing <see cref="IProgress{T}"/> for reporting progress updates.</returns>
    public IProgress<ProgressInfo> ShowProgress(string title, int totalItems)
    {
        Dispatcher.UIThread.InvokeAsync(() =>
        {
            _viewModel.ProgressText = title;
            _viewModel.ProgressVisible = true;
        });
        return new GuiProgress(_viewModel, totalItems);
    }

    /// <summary>
    /// Creates a progress context for managing and tracking the progress of an operation.
    /// </summary>
    /// <param name="title">The title or name of the progress operation being tracked.</param>
    /// <param name="totalItems">The total number of items or steps for the progress operation.</param>
    /// <returns>An instance of <see cref="IProgressContext"/> to monitor and interact with the progress.</returns>
    public IProgressContext CreateProgressContext(string title, int totalItems)
    {
        return new GuiProgressContext(_viewModel, title, totalItems);
    }
}

/// <summary>
/// Provides a GUI-based implementation of <see cref="IProgress{T}"/> to report progress updates
/// through the main window's ViewModel, enabling interaction with UI elements during long-running tasks.
/// </summary>
/// <remarks>
/// This class updates the progress-related properties of the <see cref="MainWindowViewModel"/> to reflect
/// the current state of an operation. Progress updates are marshaled to the UI thread using Avalonia's
/// <see cref="Dispatcher.UIThread"/> to ensure thread safety.
/// </remarks>
public class GuiProgress : IProgress<ProgressInfo>
{
    private readonly MainWindowViewModel _viewModel;

    public GuiProgress(MainWindowViewModel viewModel, int totalItems)
    {
        _viewModel = viewModel;
    }

    /// <summary>
    /// Reports progress updates to the associated <see cref="MainWindowViewModel"/> by updating the UI properties
    /// with the current status of the operation using a <see cref="ProgressInfo"/> instance.
    /// </summary>
    /// <param name="value">The progress information containing the current and total progress values, along with a status message.</param>
    public void Report(ProgressInfo value)
    {
        Dispatcher.UIThread.InvokeAsync(() =>
        {
            _viewModel.ProgressText = value.Message;
            _viewModel.ProgressValue = value.Percentage;
        });
    }
}

/// <summary>
/// Represents a progress tracking context specifically designed for GUI operations.
/// </summary>
/// <remarks>
/// This class is responsible for managing and updating progress information in the graphical user interface.
/// It interacts with the <see cref="MainWindowViewModel"/> to display progress-related data like messages, percentage completed,
/// and visibility status. It also provides methods to update, complete, and dispose of the progress context.
/// Implements <see cref="IProgressContext"/> to support progress reporting and disposable behavior.
/// </remarks>
public class GuiProgressContext : IProgressContext
{
    private readonly int _totalItems;
    private readonly MainWindowViewModel _viewModel;
    private bool _disposed;

    public GuiProgressContext(MainWindowViewModel viewModel, string title, int totalItems)
    {
        _viewModel = viewModel;
        _totalItems = totalItems;

        Dispatcher.UIThread.InvokeAsync(() =>
        {
            _viewModel.ProgressText = title;
            _viewModel.ProgressVisible = true;
            _viewModel.ProgressValue = 0;
        });
    }

    /// <summary>
    /// Updates the progress context with the current progress and message.
    /// </summary>
    /// <param name="current">The current progress value, typically representing the number of completed items.</param>
    /// <param name="message">A message describing the current progress state, displayed to the user.</param>
    /// <remarks>
    /// This method calculates the progress percentage based on the current and total items,
    /// and updates the associated view model's progress text and progress value properties on the UI thread.
    /// </remarks>
    public void Update(int current, string message)
    {
        if (_disposed) return;

        var percentage = _totalItems > 0 ? current * 100.0 / _totalItems : 0;
        Dispatcher.UIThread.InvokeAsync(() =>
        {
            _viewModel.ProgressText = message;
            _viewModel.ProgressValue = percentage;
        });
    }

    /// <summary>
    /// Marks the progress operation as complete and updates the associated GUI elements to reflect the completed status.
    /// </summary>
    /// <remarks>
    /// This method updates the progress value to 100 and sets the progress text to "Complete".
    /// It ensures the updates are executed on the UI thread to maintain thread-safety with the graphical user interface.
    /// If the context has been disposed, this method has no effect.
    /// </remarks>
    public void Complete()
    {
        if (_disposed) return;

        Dispatcher.UIThread.InvokeAsync(() =>
        {
            _viewModel.ProgressValue = 100;
            _viewModel.ProgressText = "Complete";
        });
    }

    /// <summary>
    /// Updates the user interface with the progress information provided.
    /// </summary>
    /// <param name="value">The progress information that includes the current progress, total progress, and a message describing the operation's state.</param>
    /// <remarks>
    /// This method is responsible for ensuring that the progress updates are displayed on the UI thread. It updates the associated
    /// <see cref="MainWindowViewModel"/> instance with the latest progress message and percentage. The method ensures thread-safety
    /// by leveraging Avalonia's <see cref="Dispatcher.UIThread"/> for UI updates. If the context has been disposed, the method
    /// does nothing.
    /// </remarks>
    public void Report(ProgressInfo value)
    {
        if (_disposed) return;

        Dispatcher.UIThread.InvokeAsync(() =>
        {
            _viewModel.ProgressText = value.Message;
            _viewModel.ProgressValue = value.Percentage;
        });
    }

    /// <summary>
    /// Releases the resources used by the <see cref="GuiProgressContext"/> and updates the associated
    /// <see cref="MainWindowViewModel"/> to reflect the end of any progress-related operations.
    /// </summary>
    /// <remarks>
    /// This method marks the context as disposed, ensures no further updates are made, and resets
    /// the visibility of progress indicators in the GUI. It schedules the visibility toggle on the UI thread
    /// to maintain thread safety when interacting with the view model's properties. Call this method
    /// when the progress operation is complete to clean up resources properly.
    /// </remarks>
    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;

        Dispatcher.UIThread.InvokeAsync(() => { _viewModel.ProgressVisible = false; });
        GC.SuppressFinalize(this);
    }
}