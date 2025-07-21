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

public class MainWindowViewModel : ViewModelBase
{
    private readonly ISettingsService _settingsService;
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

    public MainWindowViewModel()
    {
        _settingsService = new SettingsService();
        _currentSettings = new UserSettings();

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

    private void EnsurePipelineInitialized()
    {
        if (_scanPipeline == null)
        {
            _messageHandler = new GuiMessageHandler(this);

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
    }

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

            // Look for XSE crash logs in common locations (F4SE and SKSE)
            var xsePaths = new[]
            {
                // F4SE paths
                Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), "My Games", "Fallout4",
                    "F4SE"),
                Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), "My Games", "Fallout4VR",
                    "F4SE"),
                // SKSE paths (including GOG version)
                Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), "My Games",
                    "Skyrim Special Edition", "SKSE"),
                Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), "My Games",
                    "Skyrim Special Edition GOG", "SKSE"),
                Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), "My Games", "Skyrim",
                    "SKSE"),
                // Also check game path if provided
                !string.IsNullOrEmpty(SelectedGamePath) ? Path.Combine(SelectedGamePath, "Data", "F4SE") : null,
                !string.IsNullOrEmpty(SelectedGamePath) ? Path.Combine(SelectedGamePath, "Data", "SKSE") : null
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

    private void CancelScan()
    {
        _scanCancellationTokenSource?.Cancel();
        StatusText = "Cancelling scan...";
        AddLogMessage("Scan cancellation requested.");
    }

    private void ClearResults()
    {
        ScanResults.Clear();
        LogMessages.Clear();
        StatusText = "Results cleared";
        ProgressVisible = false;
    }

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

    private void UpdateUi(Action action)
    {
        if (Dispatcher.UIThread.CheckAccess())
            action();
        else
            Dispatcher.UIThread.InvokeAsync(action);
    }

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

public class GuiMessageHandler : IMessageHandler
{
    private readonly MainWindowViewModel _viewModel;

    public GuiMessageHandler(MainWindowViewModel viewModel)
    {
        _viewModel = viewModel;
    }

    public void ShowInfo(string message, MessageTarget target = MessageTarget.All)
    {
        if (target == MessageTarget.CliOnly) return;
        Dispatcher.UIThread.InvokeAsync(() => _viewModel.AddLogMessage($"ℹ️ INFO: {message}"));
    }

    public void ShowWarning(string message, MessageTarget target = MessageTarget.All)
    {
        if (target == MessageTarget.CliOnly) return;
        Dispatcher.UIThread.InvokeAsync(() => _viewModel.AddLogMessage($"⚠️ WARNING: {message}"));
    }

    public void ShowError(string message, MessageTarget target = MessageTarget.All)
    {
        if (target == MessageTarget.CliOnly) return;
        Dispatcher.UIThread.InvokeAsync(() => _viewModel.AddLogMessage($"❌ ERROR: {message}"));
    }

    public void ShowSuccess(string message, MessageTarget target = MessageTarget.All)
    {
        if (target == MessageTarget.CliOnly) return;
        Dispatcher.UIThread.InvokeAsync(() => _viewModel.AddLogMessage($"✅ SUCCESS: {message}"));
    }

    public void ShowDebug(string message, MessageTarget target = MessageTarget.All)
    {
        if (target == MessageTarget.CliOnly) return;
        Dispatcher.UIThread.InvokeAsync(() => _viewModel.AddLogMessage($"🔍 DEBUG: {message}"));
    }

    public void ShowCritical(string message, MessageTarget target = MessageTarget.All)
    {
        if (target == MessageTarget.CliOnly) return;
        Dispatcher.UIThread.InvokeAsync(() => _viewModel.AddLogMessage($"🚨 CRITICAL: {message}"));
    }

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

    public IProgress<ProgressInfo> ShowProgress(string title, int totalItems)
    {
        Dispatcher.UIThread.InvokeAsync(() =>
        {
            _viewModel.ProgressText = title;
            _viewModel.ProgressVisible = true;
        });
        return new GuiProgress(_viewModel, totalItems);
    }

    public IProgressContext CreateProgressContext(string title, int totalItems)
    {
        return new GuiProgressContext(_viewModel, title, totalItems);
    }
}

public class GuiProgress : IProgress<ProgressInfo>
{
    private readonly int _totalItems;
    private readonly MainWindowViewModel _viewModel;

    public GuiProgress(MainWindowViewModel viewModel, int totalItems)
    {
        _viewModel = viewModel;
        _totalItems = totalItems;
    }

    public void Report(ProgressInfo value)
    {
        Dispatcher.UIThread.InvokeAsync(() =>
        {
            _viewModel.ProgressText = value.Message;
            _viewModel.ProgressValue = value.Percentage;
        });
    }
}

public class GuiProgressContext : IProgressContext
{
    private readonly string _title;
    private readonly int _totalItems;
    private readonly MainWindowViewModel _viewModel;
    private bool _disposed;

    public GuiProgressContext(MainWindowViewModel viewModel, string title, int totalItems)
    {
        _viewModel = viewModel;
        _title = title;
        _totalItems = totalItems;

        Dispatcher.UIThread.InvokeAsync(() =>
        {
            _viewModel.ProgressText = title;
            _viewModel.ProgressVisible = true;
            _viewModel.ProgressValue = 0;
        });
    }

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

    public void Complete()
    {
        if (_disposed) return;

        Dispatcher.UIThread.InvokeAsync(() =>
        {
            _viewModel.ProgressValue = 100;
            _viewModel.ProgressText = "Complete";
        });
    }

    public void Report(ProgressInfo value)
    {
        if (_disposed) return;

        Dispatcher.UIThread.InvokeAsync(() =>
        {
            _viewModel.ProgressText = value.Message;
            _viewModel.ProgressValue = value.Percentage;
        });
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;

        Dispatcher.UIThread.InvokeAsync(() => { _viewModel.ProgressVisible = false; });
    }
}