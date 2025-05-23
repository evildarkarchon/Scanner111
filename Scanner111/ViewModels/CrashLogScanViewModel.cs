using System;
using System.Collections.ObjectModel;
using System.Threading.Tasks;
using System.Windows.Input;
using Microsoft.Extensions.Logging;
using ReactiveUI;
using Scanner111.Services.Interfaces;

namespace Scanner111.ViewModels;

/// <summary>
///     ViewModel for crash log scanning functionality.
/// </summary>
public class CrashLogScanViewModel : ViewModelBase
{
    private readonly ILogger<CrashLogScanViewModel> _logger;
    private readonly ICrashLogScanService _scanService;
    private int _failedCount;
    private int _incompleteCount;
    private bool _isScanning;
    private int _scannedCount;
    private ObservableCollection<string> _scanResults = [];
    private string _statusMessage = "Ready to scan";

    /// <summary>
    ///     Initializes a new instance of the crash log scan view model.
    /// </summary>
    /// <param name="logger">Logger for the view model.</param>
    /// <param name="scanService">Crash log scan service.</param>
    public CrashLogScanViewModel(ILogger<CrashLogScanViewModel> logger, ICrashLogScanService scanService)
    {
        _logger = logger;
        _scanService = scanService;

        // Initialize commands
        ScanCommand = ReactiveCommand.CreateFromTask(ScanCrashLogsAsync);
    }

    /// <summary>
    ///     Command to initiate crash log scanning.
    /// </summary>
    public ICommand ScanCommand { get; }

    /// <summary>
    ///     Whether a scan is currently in progress.
    /// </summary>
    public bool IsScanning
    {
        get => _isScanning;
        private set => this.RaiseAndSetIfChanged(ref _isScanning, value);
    }

    /// <summary>
    ///     Number of logs successfully scanned.
    /// </summary>
    public int ScannedCount
    {
        get => _scannedCount;
        private set => this.RaiseAndSetIfChanged(ref _scannedCount, value);
    }

    /// <summary>
    ///     Number of logs that failed to scan.
    /// </summary>
    public int FailedCount
    {
        get => _failedCount;
        private set => this.RaiseAndSetIfChanged(ref _failedCount, value);
    }

    /// <summary>
    ///     Number of incomplete logs.
    /// </summary>
    public int IncompleteCount
    {
        get => _incompleteCount;
        private set => this.RaiseAndSetIfChanged(ref _incompleteCount, value);
    }

    /// <summary>
    ///     Current status message.
    /// </summary>
    public string StatusMessage
    {
        get => _statusMessage;
        private set => this.RaiseAndSetIfChanged(ref _statusMessage, value);
    }

    /// <summary>
    ///     Collection of scan results.
    /// </summary>
    public ObservableCollection<string> ScanResults
    {
        get => _scanResults;
        private set => this.RaiseAndSetIfChanged(ref _scanResults, value);
    }

    /// <summary>
    ///     Scans all crash logs asynchronously.
    /// </summary>
    private async Task ScanCrashLogsAsync()
    {
        try
        {
            IsScanning = true;
            StatusMessage = "Initializing...";
            ScanResults.Clear();

            // Initialize the scan service
            await _scanService.InitializeAsync();

            StatusMessage = "Scanning crash logs...";

            // Process all crash logs
            var (results, statistics) = await _scanService.ProcessAllCrashLogsAsync();

            // Update UI with results
            ScannedCount = statistics.Scanned;
            FailedCount = statistics.Failed;
            IncompleteCount = statistics.Incomplete;

            // Add result summaries to the collection
            foreach (var result in results)
            {
                var status = result.ScanFailed ? "❌ Failed" : "✅ Success";
                ScanResults.Add($"{status}: {result.LogFileName}");
            }

            StatusMessage = $"Scan complete. Processed {statistics.Scanned} logs.";
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error scanning crash logs");
            StatusMessage = $"Error: {ex.Message}";
        }
        finally
        {
            IsScanning = false;
        }
    }
}