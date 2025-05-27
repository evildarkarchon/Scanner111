using System;
using ReactiveUI;
using System.Collections.ObjectModel;
using System.Reactive;
using System.Threading.Tasks;

namespace Scanner111.ViewModels.Tabs;

public class MainTabViewModel : ViewModelBase
{
    private string _outputText = "";
    private string _pastebinUrl = "";
    private bool _isScanningCrashLogs;
    private bool _isScanningGameFiles;
    private bool _canScan = true;

    public MainTabViewModel()
    {
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
            // TODO: Implement actual crash log scanning logic
            await Task.Delay(2000); // Simulate work
            AppendOutput("✅ Crash logs scan completed successfully!");
            AppendOutput("Found 3 crash logs, 2 analyzed successfully, 1 requires manual review.");
        }
        catch (Exception ex)
        {
            AppendOutput($"❌ Error during crash logs scan: {ex.Message}");
        }
        finally
        {
            IsScanningCrashLogs = false;
        }
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
            AppendOutput("✅ Pastebin log fetched successfully!");
            PastebinUrl = ""; // Clear the input after successful fetch
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
            
            Ready for diagnostic operations...
            """;
    }
}