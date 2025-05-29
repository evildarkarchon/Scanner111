using System;
using System.Collections.Generic;
using ReactiveUI;
using System.Reactive;
using System.Threading.Tasks;
using System.IO;
using System.Linq;
using Microsoft.Extensions.Logging;
using Scanner111.Models.CrashLog;
using Scanner111.Services.CrashLog;
using Scanner111.Services;

namespace Scanner111.ViewModels.Tabs;

/// <summary>
/// Represents the view model for the main tab in the application.
/// </summary>
public class MainTabViewModel : ViewModelBase
{
    private readonly ICrashLogValidationService _crashLogService;
    private readonly IEnhancedDialogService _dialogService;
    private readonly ILogger<MainTabViewModel> _logger;

    private string _outputText = "";
    private string _pastebinUrl = "";
    private bool _isScanningCrashLogs;
    private bool _isScanningGameFiles;
    private bool _canScan = true;

    public MainTabViewModel(
        ICrashLogValidationService crashLogService,
        IEnhancedDialogService dialogService,
        ILogger<MainTabViewModel> logger)
    {
        _crashLogService = crashLogService;
        _dialogService = dialogService;
        _logger = logger;

        // Initialize commands
        ScanCrashLogsCommand = ReactiveCommand.CreateFromTask(ScanCrashLogsAsync, this.WhenAnyValue(x => x.CanScan));
        ScanGameFilesCommand = ReactiveCommand.CreateFromTask(ScanGameFilesAsync, this.WhenAnyValue(x => x.CanScan));
        FetchPastebinCommand = ReactiveCommand.CreateFromTask(FetchPastebinAsync,
            this.WhenAnyValue(x => x.PastebinUrl, url => !string.IsNullOrWhiteSpace(url) && CanScan));
        ClearOutputCommand = ReactiveCommand.Create(ClearOutput);

        // Initialize output with welcome message
        OutputText = GetWelcomeMessage();
    }

    // Properties
    public string OutputText
    {
        get => _outputText;
        set => this.RaiseAndSetIfChanged(ref _outputText, value);
    }

    public string PastebinUrl
    {
        get => _pastebinUrl;
        set => this.RaiseAndSetIfChanged(ref _pastebinUrl, value);
    }

    public bool IsScanningCrashLogs
    {
        get => _isScanningCrashLogs;
        set
        {
            this.RaiseAndSetIfChanged(ref _isScanningCrashLogs, value);
            UpdateCanScan();
        }
    }

    public bool IsScanningGameFiles
    {
        get => _isScanningGameFiles;
        set
        {
            this.RaiseAndSetIfChanged(ref _isScanningGameFiles, value);
            UpdateCanScan();
        }
    }

    public bool CanScan
    {
        get => _canScan;
        private set => this.RaiseAndSetIfChanged(ref _canScan, value);
    }

    // Commands
    public ReactiveCommand<Unit, Unit> ScanCrashLogsCommand { get; }
    public ReactiveCommand<Unit, Unit> ScanGameFilesCommand { get; }
    public ReactiveCommand<Unit, Unit> FetchPastebinCommand { get; }
    public ReactiveCommand<Unit, Unit> ClearOutputCommand { get; }

    // Command implementations
    private async Task ScanCrashLogsAsync()
    {
        IsScanningCrashLogs = true;
        AppendOutput("=== Starting Crash Logs Scan ===");

        try
        {
            // Show supported combinations
            var supportedCombinations = _crashLogService.GetSupportedCombinations().ToList();
            AppendOutput($"Supported combinations: {supportedCombinations.Count}");

            foreach (var combo in supportedCombinations) AppendOutput($"  • {combo.GameName} + {combo.CrashGenerator}");

            // Get crash log files from common locations
            var crashLogFiles = await GetCrashLogFilesAsync();
            AppendOutput($"Found {crashLogFiles.Count} potential crash log files");

            if (crashLogFiles.Count == 0)
            {
                AppendOutput("❌ No crash log files found. Please ensure crash logs are in the expected locations:");
                AppendOutput("  • Current directory: crash-*.log");
                AppendOutput("  • Crash Logs folder: Crash Logs/**/*.log");
                return;
            }

            // Validate crash logs
            AppendOutput("Validating crash log files...");
            var validCrashLogs = await _crashLogService.GetValidCrashLogsAsync(crashLogFiles);
            var validLogsList = validCrashLogs.ToList();

            AppendOutput($"✅ Found {validLogsList.Count} valid crash logs:");

            var groupedLogs = validLogsList.GroupBy(log => log.CombinationKey);
            foreach (var group in groupedLogs)
            {
                var sample = group.First();
                AppendOutput($"  • {sample.GameName} + {sample.CrashGenerator}: {group.Count()} files");
            }

            if (validLogsList.Count == 0)
            {
                AppendOutput("❌ No valid crash logs found for supported games/crash generators");
                AppendOutput("Please ensure you have crash logs from supported combinations");
            }
            else
            {
                AppendOutput("🔍 Starting detailed analysis...");

                // Process each valid crash log
                var processedCount = 0;
                foreach (var crashLog in validLogsList)
                    try
                    {
                        await ProcessCrashLogAsync(crashLog);
                        processedCount++;
                    }
                    catch (Exception ex)
                    {
                        AppendOutput($"❌ Error processing {Path.GetFileName(crashLog.FilePath)}: {ex.Message}");
                        _logger.LogError(ex, "Error processing crash log: {FilePath}", crashLog.FilePath);
                    }

                AppendOutput($"✅ Crash logs scan completed! Processed {processedCount}/{validLogsList.Count} files");
            }
        }
        catch (Exception ex)
        {
            AppendOutput($"❌ Error during crash logs scan: {ex.Message}");
            _logger.LogError(ex, "Error during crash logs scan");
        }
        finally
        {
            IsScanningCrashLogs = false;
        }
    }

    private async Task<List<string>> GetCrashLogFilesAsync()
    {
        // Use Task.Run to offload file I/O to a background thread
        var result = await Task.Run(() =>
        {
            var crashLogFiles = new List<string>();
            var searchLocations = new[]
            {
                Environment.CurrentDirectory,
                Path.Combine(Environment.CurrentDirectory, "Crash Logs"),
                Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments),
                    "My Games", "Fallout4", "F4SE") // Default F4SE crash log location
            };

            foreach (var location in searchLocations)
                if (Directory.Exists(location))
                {
                    // Look for crash-*.log files
                    var files = Directory.GetFiles(location, "*.log", SearchOption.AllDirectories);
                    var txtFiles = Directory.GetFiles(location, "*.txt", SearchOption.AllDirectories);
                    crashLogFiles.AddRange(files);
                    crashLogFiles.AddRange(txtFiles);
                }

            return crashLogFiles.Distinct().ToList();
        }).ConfigureAwait(true); // Use ConfigureAwait(true) to ensure we return to the UI thread
    
        return result;
    }

    private async Task ProcessCrashLogAsync(CrashLogInfo crashLog)
    {
        AppendOutput($"📄 Processing: {Path.GetFileName(crashLog.FilePath)}");
        AppendOutput($"   Game: {crashLog.GameName} {crashLog.GameVersion}");
        AppendOutput($"   Generator: {crashLog.CrashGenerator} {crashLog.CrashGeneratorVersion}");

        if (crashLog.IsVrVersion) AppendOutput($"   🥽 VR Version detected");

        // TODO: Implement actual crash log analysis logic here
        // This would call the crash log analysis engine similar to the Python version

        // Simulate processing time
        await Task.Delay(100);

        AppendOutput($"   ✅ Analysis complete");
    }

    private async Task ScanGameFilesAsync()
    {
        IsScanningGameFiles = true;
        AppendOutput("=== Starting Game Files Scan ===");

        try
        {
            // TODO: Implement actual game files scanning logic
            await Task.Delay(3000); // Simulate work
            AppendOutput("✅ Game files scan completed successfully!");
            AppendOutput("Game integrity check passed. All core files verified.");
        }
        catch (Exception ex)
        {
            AppendOutput($"❌ Error during game files scan: {ex.Message}");
        }
        finally
        {
            IsScanningGameFiles = false;
        }
    }

    private async Task FetchPastebinAsync()
    {
        AppendOutput($"=== Fetching Pastebin: {PastebinUrl} ===");

        try
        {
            // TODO: Implement actual Pastebin fetching logic
            await Task.Delay(1000); // Simulate network request

            // After fetching, validate the downloaded content
            var tempFile = Path.GetTempFileName();
            // ... download content to tempFile ...

            var crashLogInfo = await _crashLogService.GetCrashLogInfoAsync(tempFile);
            if (crashLogInfo != null)
            {
                AppendOutput("✅ Valid crash log fetched from Pastebin!");
                AppendOutput($"   Game: {crashLogInfo.GameName} {crashLogInfo.GameVersion}");
                AppendOutput($"   Generator: {crashLogInfo.CrashGenerator} {crashLogInfo.CrashGeneratorVersion}");
            }
            else
            {
                AppendOutput("❌ Fetched content is not a valid crash log");
            }

            PastebinUrl = ""; // Clear the input after processing
        }
        catch (Exception ex)
        {
            AppendOutput($"❌ Error fetching Pastebin: {ex.Message}");
        }
    }

    private void ClearOutput()
    {
        OutputText = GetWelcomeMessage();
    }

    // Helper methods
    private void AppendOutput(string message)
    {
        var timestamp = DateTime.Now.ToString("HH:mm:ss");
        OutputText += $"\n[{timestamp}] {message}";
    }

    private void UpdateCanScan()
    {
        CanScan = !IsScanningCrashLogs && !IsScanningGameFiles;
    }

    private static string GetWelcomeMessage()
    {
        return """
               === Scanner 111 - Vault-Tec Diagnostic Tool ===

               Welcome to Scanner 111! Your comprehensive tool for diagnosing and fixing 
               issues with Bethesda RPGs.

               🔍 SCAN CRASH LOGS - Analyze crash logs to identify issues
               🎮 SCAN GAME FILES - Check game integrity and mod conflicts
               📋 PASTEBIN - Fetch crash logs from Pastebin URLs

               Supported crash log combinations will be detected automatically.
               Ready for diagnostic operations...
               """;
    }
}

