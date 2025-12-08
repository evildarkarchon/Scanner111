using ReactiveUI;
using ReactiveUI.Fody.Helpers;
using Scanner111.Common.Models.Configuration;
using Scanner111.Common.Services.Orchestration;
using Scanner111.Models;
using Scanner111.Services;
using System;
using System.Collections.ObjectModel;
using System.Reactive;
using System.Threading.Tasks;

namespace Scanner111.ViewModels;

public class HomePageViewModel : ViewModelBase
{
    private readonly IScanExecutor _scanExecutor;
    private readonly IScanResultsService _scanResultsService;

    [Reactive] public string StagingModsPath { get; set; } = string.Empty;
    [Reactive] public string CustomScanPath { get; set; } = "D:/Crash Logs";
    [Reactive] public string PastebinUrl { get; set; } = string.Empty;
    [Reactive] public string StatusText { get; set; } = "Ready";
    [Reactive] public bool IsScanning { get; set; }
    [Reactive] public double Progress { get; set; }

    /// <summary>
    /// Collection of scan results (backed by shared service).
    /// </summary>
    public ObservableCollection<LogAnalysisResultDisplay> ScanResults => _scanResultsService.Results;

    public ReactiveCommand<Unit, Unit> ScanCrashLogsCommand { get; }
    public ReactiveCommand<Unit, Unit> ScanGameFilesCommand { get; }
    public ReactiveCommand<Unit, Unit> StartPapyrusMonitorCommand { get; }
    public ReactiveCommand<Unit, Unit> OpenCrashLogsCommand { get; }
    public ReactiveCommand<Unit, Unit> BrowseStagingCommand { get; }
    public ReactiveCommand<Unit, Unit> BrowseCustomScanCommand { get; }
    public ReactiveCommand<Unit, Unit> FetchPastebinCommand { get; }

    public HomePageViewModel(IScanExecutor scanExecutor, IScanResultsService scanResultsService)
    {
        _scanExecutor = scanExecutor;
        _scanResultsService = scanResultsService;

        var canScan = this.WhenAnyValue(x => x.IsScanning, scanning => !scanning);

        ScanCrashLogsCommand = ReactiveCommand.CreateFromTask(ExecuteScanAsync, canScan);
        ScanGameFilesCommand = ReactiveCommand.Create(() => { StatusText = "Scan Game Files not yet implemented."; });
        StartPapyrusMonitorCommand = ReactiveCommand.Create(() =>
        {
            StatusText = "Papyrus Monitor not yet implemented.";
        });
        OpenCrashLogsCommand = ReactiveCommand.Create(OpenCrashLogsFolder);

        BrowseStagingCommand = ReactiveCommand.Create(() =>
        {
            /* TODO: Open Folder Dialog */
        });
        BrowseCustomScanCommand = ReactiveCommand.Create(() =>
        {
            /* TODO: Open Folder Dialog */
        });
        FetchPastebinCommand = ReactiveCommand.Create(() => { StatusText = "Fetch Pastebin not yet implemented."; });
    }

    private async Task ExecuteScanAsync()
    {
        if (string.IsNullOrWhiteSpace(CustomScanPath))
        {
            StatusText = "Error: Please specify a scan path.";
            return;
        }

        IsScanning = true;
        StatusText = "Scanning...";
        _scanResultsService.Clear();
        Progress = 0;

        var config = new ScanConfig
        {
            ScanPath = CustomScanPath,
            FcxMode = false, // TODO: Get from settings
            ShowFormIdValues = false // TODO: Get from settings
        };

        var progressReporter = new Progress<ScanProgress>(p =>
        {
            if (p.TotalFiles > 0)
            {
                Progress = (double)p.FilesProcessed / p.TotalFiles * 100;
            }

            StatusText = $"Processing: {p.CurrentFile} ({p.FilesProcessed}/{p.TotalFiles})";
        });

        try
        {
            var result = await _scanExecutor.ExecuteScanAsync(config, progressReporter);

            foreach (var processedFile in result.ProcessedFiles)
            {
                // TODO: Load actual markdown content from the generated AUTOSCAN file
                ScanResults.Add(new LogAnalysisResultDisplay
                {
                    FileName = System.IO.Path.GetFileName(processedFile),
                    Status = "Completed",
                    Content = $"# {System.IO.Path.GetFileName(processedFile)}\n\nAnalysis completed."
                });
            }

            foreach (var failedLog in result.FailedLogs)
            {
                ScanResults.Add(new LogAnalysisResultDisplay
                {
                    FileName = System.IO.Path.GetFileName(failedLog),
                    Status = "Failed",
                    Content = $"# {System.IO.Path.GetFileName(failedLog)}\n\nFailed to process this log."
                });
            }

            StatusText =
                $"Complete - Scanned: {result.Statistics.Scanned}, Failed: {result.Statistics.Failed} in {result.ScanDuration.TotalSeconds:F2}s";
        }
        catch (Exception ex)
        {
            StatusText = $"Error: {ex.Message}";
        }
        finally
        {
            IsScanning = false;
        }
    }

    private void OpenCrashLogsFolder()
    {
        if (!string.IsNullOrWhiteSpace(CustomScanPath) && System.IO.Directory.Exists(CustomScanPath))
        {
            System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
            {
                FileName = CustomScanPath,
                UseShellExecute = true
            });
            StatusText = "Opened crash logs folder.";
        }
        else
        {
            StatusText = "Error: Folder does not exist.";
        }
    }
}
