using ReactiveUI;
using Scanner111.Core.Models;
using Scanner111.Core.Pipeline;
using Scanner111.Core.Infrastructure;
using Scanner111.GUI.Models;
using Microsoft.Extensions.Logging;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Reactive;
using System.Reactive.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.IO;
using System;
using System.Linq;
using Avalonia.Threading;

namespace Scanner111.GUI.ViewModels;

public partial class MainWindowViewModel : ViewModelBase
{
    private string _statusText = "Ready";
    private bool _isScanning = false;
    private double _progressValue = 0;
    private string _progressText = "";
    private bool _progressVisible = false;
    private string _selectedLogPath = "";
    private string _selectedGamePath = "";
    private string _selectedScanDirectory = "";
    private ScanResultViewModel? _selectedResult;
    private CancellationTokenSource? _scanCancellationTokenSource;

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

    private IScanPipeline? _scanPipeline;
    private IMessageHandler? _messageHandler;

    public MainWindowViewModel()
    {
        // Initialize commands first - defer pipeline creation to avoid threading issues
        SelectLogFileCommand = ReactiveCommand.CreateFromTask(SelectLogFile);
        SelectGamePathCommand = ReactiveCommand.CreateFromTask(SelectGamePath);
        SelectScanDirectoryCommand = ReactiveCommand.CreateFromTask(SelectScanDirectory);
        ScanCommand = ReactiveCommand.CreateFromTask(ExecuteScan);
        CancelScanCommand = ReactiveCommand.Create(CancelScan);
        ClearResultsCommand = ReactiveCommand.Create(ClearResults);

        StatusText = "Ready - Select a crash log file to begin";
    }

    private void EnsurePipelineInitialized()
    {
        if (_scanPipeline == null)
        {
            _messageHandler = new GuiMessageHandler(this);
            
            _scanPipeline = new ScanPipelineBuilder()
                .AddDefaultAnalyzers()
                .WithMessageHandler(_messageHandler)
                .WithCaching(true)
                .WithEnhancedErrorHandling(true)
                .WithLogging(builder => builder.AddConsole())
                .Build();
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
                var result = await _scanPipeline!.ProcessSingleAsync(filesToScan[0], _scanCancellationTokenSource.Token);
                if (result != null)
                {
                    ScanResults.Add(new ScanResultViewModel(result));
                    AddLogMessage($"Scan status: {result.Status}, Found {result.AnalysisResults.Count} analysis results");
                }
            }
            else
            {
                // Batch scan
                var resultCount = 0;
                await foreach (var result in _scanPipeline!.ProcessBatchAsync(filesToScan))
                {
                    if (result != null)
                    {
                        ScanResults.Add(new ScanResultViewModel(result));
                        resultCount++;
                        ProgressValue = (double)resultCount / filesToScan.Count * 100;
                        ProgressText = $"Processed {resultCount}/{filesToScan.Count} files";
                        AddLogMessage($"Processed {Path.GetFileName(result.LogPath)}: {result.Status}, {result.AnalysisResults.Count} analysis results");
                    }
                }
            }

            ProgressValue = 100;
            ProgressText = $"Scan complete - {ScanResults.Count} results";
            StatusText = $"Scan completed - {ScanResults.Count} results from {filesToScan.Count} files";
            AddLogMessage($"Scan completed successfully. Found {ScanResults.Count} total results from {filesToScan.Count} files.");
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

        // 1. Auto-copy F4SE logs (unconditional)
        await CopyF4SELogsAsync(filesToScan);

        // 2. Add single selected file if specified
        if (!string.IsNullOrEmpty(SelectedLogPath) && File.Exists(SelectedLogPath))
        {
            if (!filesToScan.Contains(SelectedLogPath))
            {
                filesToScan.Add(SelectedLogPath);
                AddLogMessage($"Added selected file: {Path.GetFileName(SelectedLogPath)}");
            }
        }

        // 3. Add files from selected directory if specified
        if (!string.IsNullOrEmpty(SelectedScanDirectory) && Directory.Exists(SelectedScanDirectory))
        {
            await ScanDirectoryForLogs(SelectedScanDirectory, filesToScan);
        }

        return filesToScan;
    }

    private async Task CopyF4SELogsAsync(List<string> filesToScan)
    {
        try
        {
            // Look for F4SE crash logs in common locations
            var f4sePaths = new[]
            {
                Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), "My Games", "Fallout4", "F4SE"),
                Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), "My Games", "Fallout4VR", "F4SE"),
                // Also check game path if provided
                !string.IsNullOrEmpty(SelectedGamePath) ? Path.Combine(SelectedGamePath, "Data", "F4SE") : null
            }.Where(path => path != null && Directory.Exists(path)).ToArray();

            var copiedCount = 0;
            foreach (var f4sePath in f4sePaths)
            {
                if (Directory.Exists(f4sePath))
                {
                    var crashLogs = Directory.GetFiles(f4sePath!, "*.log", SearchOption.TopDirectoryOnly)
                        .Where(f => Path.GetFileName(f).StartsWith("crash-", StringComparison.OrdinalIgnoreCase))
                        .OrderByDescending(File.GetLastWriteTime)
                        .ToArray();

                    foreach (var logFile in crashLogs)
                    {
                        // Copy to current directory for scanning
                        var targetPath = Path.Combine(Directory.GetCurrentDirectory(), Path.GetFileName(logFile));
                        if (!File.Exists(targetPath) || File.GetLastWriteTime(logFile) > File.GetLastWriteTime(targetPath))
                        {
                            await Task.Run(() => File.Copy(logFile, targetPath, true));
                            AddLogMessage($"Copied F4SE log: {Path.GetFileName(logFile)}");
                            copiedCount++;
                        }
                        
                        if (!filesToScan.Contains(targetPath))
                        {
                            filesToScan.Add(targetPath);
                        }
                    }
                }
            }

            if (copiedCount > 0)
            {
                AddLogMessage($"Auto-copied {copiedCount} F4SE crash logs");
            }
            else if (f4sePaths.Length == 0)
            {
                AddLogMessage("No F4SE directories found for auto-copy");
            }
            else
            {
                AddLogMessage("No new F4SE crash logs to copy");
            }
        }
        catch (Exception ex)
        {
            AddLogMessage($"Error during F4SE auto-copy: {ex.Message}");
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
            {
                if (!filesToScan.Contains(logFile))
                {
                    filesToScan.Add(logFile);
                    addedCount++;
                }
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
        UpdateUI(() => {
            LogMessages.Add($"[{timestamp}] {message}");
            
            // Keep only last 100 messages
            while (LogMessages.Count > 100)
            {
                LogMessages.RemoveAt(0);
            }
        });
    }

    private void UpdateUI(Action action)
    {
        if (Dispatcher.UIThread.CheckAccess())
        {
            action();
        }
        else
        {
            Dispatcher.UIThread.InvokeAsync(action);
        }
    }

    // File picker delegates - set by the View
    public Func<string, string, Task<string>>? ShowFilePickerAsync { get; set; }
    public Func<string, Task<string>>? ShowFolderPickerAsync { get; set; }
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
        Dispatcher.UIThread.InvokeAsync(() => _viewModel.AddLogMessage($"INFO: {message}"));
    }

    public void ShowWarning(string message)
    {
        Dispatcher.UIThread.InvokeAsync(() => _viewModel.AddLogMessage($"WARNING: {message}"));
    }

    public void ShowError(string message)
    {
        Dispatcher.UIThread.InvokeAsync(() => _viewModel.AddLogMessage($"ERROR: {message}"));
    }

    public IProgress<ProgressInfo> ShowProgress(string title, int totalItems)
    {
        Dispatcher.UIThread.InvokeAsync(() => {
            _viewModel.ProgressText = title;
            _viewModel.ProgressVisible = true;
        });
        return new GuiProgress(_viewModel, totalItems);
    }
}

public class GuiProgress : IProgress<ProgressInfo>
{
    private readonly MainWindowViewModel _viewModel;
    private readonly int _totalItems;

    public GuiProgress(MainWindowViewModel viewModel, int totalItems)
    {
        _viewModel = viewModel;
        _totalItems = totalItems;
    }

    public void Report(ProgressInfo value)
    {
        Dispatcher.UIThread.InvokeAsync(() => {
            _viewModel.ProgressText = value.Message;
            _viewModel.ProgressValue = value.Percentage;
        });
    }
}
