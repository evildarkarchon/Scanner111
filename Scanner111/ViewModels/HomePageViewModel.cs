using ReactiveUI;
using ReactiveUI.Fody.Helpers;
using Scanner111.Common.Models.Configuration;
using Scanner111.Common.Services.Orchestration;
using Scanner111.Models;
using Scanner111.Services;
using System;
using System.Collections.ObjectModel;
using System.IO;
using System.Reactive;
using System.Threading.Tasks;

namespace Scanner111.ViewModels;

public class HomePageViewModel : ViewModelBase
{
    private readonly IScanExecutor _scanExecutor;
    private readonly IScanResultsService _scanResultsService;
    private readonly IDialogService _dialogService;
    private readonly ISettingsService _settingsService;

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

    public HomePageViewModel(
        IScanExecutor scanExecutor,
        IScanResultsService scanResultsService,
        IDialogService dialogService,
        ISettingsService settingsService)
    {
        _scanExecutor = scanExecutor;
        _scanResultsService = scanResultsService;
        _dialogService = dialogService;
        _settingsService = settingsService;

        var canScan = this.WhenAnyValue(x => x.IsScanning, scanning => !scanning);

        ScanCrashLogsCommand = ReactiveCommand.CreateFromTask(ExecuteScanAsync, canScan);
        ScanGameFilesCommand = ReactiveCommand.Create(() => { StatusText = "Scan Game Files not yet implemented."; });
        StartPapyrusMonitorCommand = ReactiveCommand.Create(() =>
        {
            StatusText = "Papyrus Monitor not yet implemented.";
        });
        OpenCrashLogsCommand = ReactiveCommand.Create(OpenCrashLogsFolder);

        BrowseStagingCommand = ReactiveCommand.CreateFromTask(BrowseStagingFolderAsync);
        BrowseCustomScanCommand = ReactiveCommand.CreateFromTask(BrowseCustomScanFolderAsync);
        FetchPastebinCommand = ReactiveCommand.Create(() => { StatusText = "Fetch Pastebin not yet implemented."; });
    }

    private async Task BrowseStagingFolderAsync()
    {
        var folder = await _dialogService.ShowFolderPickerAsync(
            "Select Staging Mods Folder",
            StagingModsPath);

        if (!string.IsNullOrEmpty(folder))
        {
            StagingModsPath = folder;
            StatusText = $"Staging folder set to: {folder}";
        }
    }

    private async Task BrowseCustomScanFolderAsync()
    {
        var folder = await _dialogService.ShowFolderPickerAsync(
            "Select Crash Logs Folder",
            CustomScanPath);

        if (!string.IsNullOrEmpty(folder))
        {
            CustomScanPath = folder;
            StatusText = $"Scan folder set to: {folder}";
        }
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
            FcxMode = _settingsService.FcxMode,
            ShowFormIdValues = _settingsService.ShowFormIdValues,
            MaxConcurrent = _settingsService.MaxConcurrent
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
                // Try to load actual AUTOSCAN content
                var autoscanPath = GetAutoscanPath(processedFile);
                var content = await LoadAutoscanContentAsync(autoscanPath, processedFile);

                ScanResults.Add(new LogAnalysisResultDisplay
                {
                    FileName = Path.GetFileName(processedFile),
                    Status = "Completed",
                    Content = content
                });
            }

            foreach (var failedLog in result.FailedLogs)
            {
                ScanResults.Add(new LogAnalysisResultDisplay
                {
                    FileName = Path.GetFileName(failedLog),
                    Status = "Failed",
                    Content = $"# {Path.GetFileName(failedLog)}\n\nFailed to process this log."
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

    private static string GetAutoscanPath(string logFilePath)
    {
        // AUTOSCAN files are typically named like: crash-name-AUTOSCAN.md
        var directory = Path.GetDirectoryName(logFilePath) ?? string.Empty;
        var fileName = Path.GetFileNameWithoutExtension(logFilePath);
        return Path.Combine(directory, $"{fileName}-AUTOSCAN.md");
    }

    private static async Task<string> LoadAutoscanContentAsync(string autoscanPath, string originalLogPath)
    {
        if (File.Exists(autoscanPath))
        {
            try
            {
                return await File.ReadAllTextAsync(autoscanPath);
            }
            catch
            {
                // Fall through to default content
            }
        }

        // Default content if AUTOSCAN file doesn't exist
        return
            $"# {Path.GetFileName(originalLogPath)}\n\nAnalysis completed. AUTOSCAN file not found at:\n`{autoscanPath}`";
    }

    private void OpenCrashLogsFolder()
    {
        if (!string.IsNullOrWhiteSpace(CustomScanPath) && Directory.Exists(CustomScanPath))
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
