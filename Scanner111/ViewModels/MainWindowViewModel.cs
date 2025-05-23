using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using System.Windows.Input;
using Avalonia.Platform.Storage;
using Microsoft.Extensions.Logging;
using ReactiveUI;
using Scanner111.Services;

namespace Scanner111.ViewModels;

public partial class MainWindowViewModel : ViewModelBase
{
    private string _outputText = string.Empty;
    private bool _isTaskRunning;
    private bool _isTaskIndeterminate = true;
    private double _taskProgress;
    private string _statusText = "Ready";
    private bool _isPapyrusMonitoringEnabled;
    
    // Settings tab properties
    private string _iniFilesPath = string.Empty;
    private string _modsFolderPath = string.Empty;
    private string _customScanPath = string.Empty;
    private string _pastebinUrl = string.Empty;
    private List<string> _gameOptions = new() { "Fallout 4", "Fallout 4 VR", "Skyrim Special Edition", "Skyrim VR" };
    private string _selectedGame = "Fallout 4";
    private bool _isFcxModeEnabled;
    private bool _isSimplifyLogsEnabled = true;
    private bool _isUpdateCheckEnabled = true;
    private bool _isVrModeEnabled;
    private bool _isDebugModeEnabled;
    private bool _isBackupBeforeChangesEnabled = true;

    private readonly DialogService _dialogService;
    
    // Services would be injected here
    public MainWindowViewModel(DialogService dialogService = null)
    {
        _dialogService = dialogService;
        
        // Initialize commands for Main tab
        ScanCrashLogsCommand = ReactiveCommand.CreateFromTask(ScanCrashLogsAsync);
        ScanGameFilesCommand = ReactiveCommand.CreateFromTask(ScanGameFilesAsync);
        DownloadFromPastebinCommand = ReactiveCommand.CreateFromTask(DownloadFromPastebinAsync);
        CheckUpdatesCommand = ReactiveCommand.CreateFromTask(CheckUpdatesAsync);
        TogglePapyrusMonitoringCommand = ReactiveCommand.Create(TogglePapyrusMonitoring);
        
        // Initialize commands for Settings tab
        BrowseIniPathCommand = ReactiveCommand.CreateFromTask(BrowseIniPathAsync);
        BrowseModsPathCommand = ReactiveCommand.CreateFromTask(BrowseModsPathAsync);
        BrowseCustomScanPathCommand = ReactiveCommand.CreateFromTask(BrowseCustomScanPathAsync);
        ClearBackupFilesCommand = ReactiveCommand.CreateFromTask(ClearBackupFilesAsync);
        
        // Initialize commands for Articles tab
        OpenArticleCommand = ReactiveCommand.Create<string>(OpenArticle);
        
        // Initialize commands for Backups tab
        BackupFilesCommand = ReactiveCommand.CreateFromTask<string>(BackupFilesAsync);
        RestoreFilesCommand = ReactiveCommand.CreateFromTask<string>(RestoreFilesAsync);
        RemoveFilesCommand = ReactiveCommand.CreateFromTask<string>(RemoveFilesAsync);
        OpenBackupsFolderCommand = ReactiveCommand.Create(OpenBackupsFolder);
        
        // Initialize commands for menu/toolbar actions
        ShowAboutCommand = ReactiveCommand.CreateFromTask(ShowAboutAsync);
        ShowHelpCommand = ReactiveCommand.CreateFromTask(ShowHelpAsync);
    }

    // Main tab properties
    public string OutputText
    {
        get => _outputText;
        set => this.RaiseAndSetIfChanged(ref _outputText, value);
    }

    public bool IsTaskRunning
    {
        get => _isTaskRunning;
        set => this.RaiseAndSetIfChanged(ref _isTaskRunning, value);
    }

    public bool IsTaskIndeterminate
    {
        get => _isTaskIndeterminate;
        set => this.RaiseAndSetIfChanged(ref _isTaskIndeterminate, value);
    }

    public double TaskProgress
    {
        get => _taskProgress;
        set => this.RaiseAndSetIfChanged(ref _taskProgress, value);
    }

    public string StatusText
    {
        get => _statusText;
        set => this.RaiseAndSetIfChanged(ref _statusText, value);
    }

    public bool IsPapyrusMonitoringEnabled
    {
        get => _isPapyrusMonitoringEnabled;
        set => this.RaiseAndSetIfChanged(ref _isPapyrusMonitoringEnabled, value);
    }

    // Settings tab properties
    public string IniFilesPath
    {
        get => _iniFilesPath;
        set => this.RaiseAndSetIfChanged(ref _iniFilesPath, value);
    }

    public string ModsFolderPath
    {
        get => _modsFolderPath;
        set => this.RaiseAndSetIfChanged(ref _modsFolderPath, value);
    }

    public string CustomScanPath
    {
        get => _customScanPath;
        set => this.RaiseAndSetIfChanged(ref _customScanPath, value);
    }

    public string PastebinUrl
    {
        get => _pastebinUrl;
        set => this.RaiseAndSetIfChanged(ref _pastebinUrl, value);
    }

    public List<string> GameOptions
    {
        get => _gameOptions;
        set => this.RaiseAndSetIfChanged(ref _gameOptions, value);
    }

    public string SelectedGame
    {
        get => _selectedGame;
        set => this.RaiseAndSetIfChanged(ref _selectedGame, value);
    }

    public bool IsFcxModeEnabled
    {
        get => _isFcxModeEnabled;
        set => this.RaiseAndSetIfChanged(ref _isFcxModeEnabled, value);
    }

    public bool IsSimplifyLogsEnabled
    {
        get => _isSimplifyLogsEnabled;
        set => this.RaiseAndSetIfChanged(ref _isSimplifyLogsEnabled, value);
    }

    public bool IsUpdateCheckEnabled
    {
        get => _isUpdateCheckEnabled;
        set => this.RaiseAndSetIfChanged(ref _isUpdateCheckEnabled, value);
    }

    public bool IsVrModeEnabled
    {
        get => _isVrModeEnabled;
        set => this.RaiseAndSetIfChanged(ref _isVrModeEnabled, value);
    }

    public bool IsDebugModeEnabled
    {
        get => _isDebugModeEnabled;
        set => this.RaiseAndSetIfChanged(ref _isDebugModeEnabled, value);
    }

    public bool IsBackupBeforeChangesEnabled
    {
        get => _isBackupBeforeChangesEnabled;
        set => this.RaiseAndSetIfChanged(ref _isBackupBeforeChangesEnabled, value);
    }

    // Main tab commands
    public ICommand ScanCrashLogsCommand { get; }
    public ICommand ScanGameFilesCommand { get; }
    public ICommand DownloadFromPastebinCommand { get; }
    public ICommand CheckUpdatesCommand { get; }
    public ICommand TogglePapyrusMonitoringCommand { get; }

    // Settings tab commands
    public ICommand BrowseIniPathCommand { get; }
    public ICommand BrowseModsPathCommand { get; }
    public ICommand BrowseCustomScanPathCommand { get; }
    public ICommand ClearBackupFilesCommand { get; }

    // Articles tab commands
    public ICommand OpenArticleCommand { get; }

    // Backups tab commands
    public ICommand BackupFilesCommand { get; }
    public ICommand RestoreFilesCommand { get; }
    public ICommand RemoveFilesCommand { get; }
    public ICommand OpenBackupsFolderCommand { get; }

    // Menu/toolbar commands
    public ICommand ShowAboutCommand { get; }
    public ICommand ShowHelpCommand { get; }

    // Main tab command implementations
    private async Task ScanCrashLogsAsync()
    {
        await RunTaskAsync("Scanning crash logs...", async (progress) =>
        {
            // Example implementation - this would be replaced with actual scan logic
            AppendToOutput("Starting crash log scan...");
            await Task.Delay(500); // Simulate work
            progress.Report(30);
            
            AppendToOutput("Analyzing crash data...");
            await Task.Delay(500); // Simulate work
            progress.Report(60);
            
            AppendToOutput("Generating report...");
            await Task.Delay(500); // Simulate work
            progress.Report(90);
            
            AppendToOutput("Scan complete.\n");
            AppendToOutput("Found 0 critical issues.\n");
            
            return "Crash log scan completed";
        });
    }

    private async Task ScanGameFilesAsync()
    {
        await RunTaskAsync("Scanning game files...", async (progress) =>
        {
            // Example implementation - this would be replaced with actual scan logic
            AppendToOutput("Starting game file scan...");
            await Task.Delay(500); // Simulate work
            progress.Report(30);
            
            AppendToOutput("Checking mod files...");
            await Task.Delay(500); // Simulate work
            progress.Report(60);
            
            AppendToOutput("Analyzing game configuration...");
            await Task.Delay(500); // Simulate work
            progress.Report(90);
            
            AppendToOutput("Scan complete.\n");
            AppendToOutput("Found 0 issues with game files.\n");
            
            return "Game file scan completed";
        });
    }

    private async Task DownloadFromPastebinAsync()
    {
        if (string.IsNullOrWhiteSpace(PastebinUrl))
        {
            AppendToOutput("Error: Please enter a Pastebin URL or ID before downloading.");
            StatusText = "No Pastebin URL provided";
            return;
        }

        await RunTaskAsync("Downloading from Pastebin...", async (progress) =>
        {
            // Format the URL if needed (handle both full URLs and just the paste ID)
            string url = PastebinUrl.Trim();
            if (!url.StartsWith("http"))
            {
                // If user only entered the ID, construct the full URL
                if (!url.StartsWith("raw/"))
                {
                    url = "https://pastebin.com/raw/" + url;
                }
                else
                {
                    url = "https://pastebin.com/" + url;
                }
            }
            
            AppendToOutput($"Downloading content from: {url}");
            
            // In a real implementation, we would use HttpClient to download the content
            // For now, just simulate the process
            await Task.Delay(1000); // Simulate download
            progress.Report(50);
            
            AppendToOutput("Processing downloaded content...");
            await Task.Delay(500); // Simulate processing
            progress.Report(90);
            
            // Clear the URL field after successful download
            PastebinUrl = string.Empty;
            
            AppendToOutput("Download and processing completed successfully.\n");
            
            return "Pastebin download completed";
        });
    }

    private async Task CheckUpdatesAsync()
    {
        await RunTaskAsync("Checking for updates...", async (progress) =>
        {
            AppendToOutput("Checking for updates...");
            await Task.Delay(1000); // Simulate work
            progress.Report(100);
            
            AppendToOutput("You are running the latest version.\n");
            
            return "Update check completed";
        });
    }

    private void TogglePapyrusMonitoring()
    {
        if (IsPapyrusMonitoringEnabled)
        {
            AppendToOutput("Papyrus log monitoring started.\n");
            StatusText = "Monitoring Papyrus logs";
        }
        else
        {
            AppendToOutput("Papyrus log monitoring stopped.\n");
            StatusText = "Ready";
        }
    }

    // Settings tab command implementations
    private async Task<string> BrowseFolderAsync(string title)
    {
        try
        {
            // This is a placeholder for the actual folder browsing implementation
            // In a real implementation, we would use Avalonia's FolderBrowserDialog
            AppendToOutput($"Opening folder browser for: {title}");
            await Task.Delay(500); // Simulate dialog opening
            
            // Simulate a folder selection
            string selectedPath = $"C:\\Users\\Username\\Documents\\{title}";
            AppendToOutput($"Selected folder: {selectedPath}");
            
            return selectedPath;
        }
        catch (Exception ex)
        {
            AppendToOutput($"Error browsing for folder: {ex.Message}");
            return string.Empty;
        }
    }

    private async Task BrowseIniPathAsync()
    {
        string path = await BrowseFolderAsync("INI Files");
        if (!string.IsNullOrEmpty(path))
        {
            IniFilesPath = path;
        }
    }

    private async Task BrowseModsPathAsync()
    {
        string path = await BrowseFolderAsync("Mods Folder");
        if (!string.IsNullOrEmpty(path))
        {
            ModsFolderPath = path;
        }
    }

    private async Task BrowseCustomScanPathAsync()
    {
        string path = await BrowseFolderAsync("Custom Scan Path");
        if (!string.IsNullOrEmpty(path))
        {
            CustomScanPath = path;
        }
    }

    private async Task ClearBackupFilesAsync()
    {
        await RunTaskAsync("Clearing backup files...", async (progress) =>
        {
            AppendToOutput("Clearing backup files...");
            await Task.Delay(1000); // Simulate work
            progress.Report(100);
            
            AppendToOutput("All backup files have been removed.\n");
            
            return "Backup files cleared";
        });
    }

    // Articles tab command implementations
    private void OpenArticle(string articleId)
    {
        // Dictionary mapping article IDs to URLs
        var articleUrls = new Dictionary<string, string>
        {
            // General Guides
            { "CrashLogGuide", "https://example.com/crash-log-guide" },
            { "GameSetupGuide", "https://example.com/game-setup-guide" },
            { "ModTroubleshooting", "https://example.com/mod-troubleshooting" },
            
            // Performance Guides
            { "PerformanceOptimization", "https://example.com/performance-optimization" },
            { "EnbSetup", "https://example.com/enb-setup-guide" },
            { "GraphicsMods", "https://example.com/graphics-mods-guide" },
            
            // Mod Management
            { "Mo2Guide", "https://example.com/mod-organizer-2-guide" },
            { "VortexGuide", "https://example.com/vortex-guide" },
            { "LoadOrderGuide", "https://example.com/load-order-optimization" },
            
            // Troubleshooting
            { "CrashFixes", "https://example.com/common-crash-fixes" },
            { "PluginConflicts", "https://example.com/plugin-conflict-resolution" },
            { "SaveGameIssues", "https://example.com/save-game-troubleshooting" },
            
            // Community Resources
            { "DiscordCommunity", "https://discord.gg/exampleserver" },
            { "RedditCommunity", "https://reddit.com/r/examplesubreddit" },
            { "NexusForums", "https://forums.nexusmods.com/example" }
        };

        try
        {
            if (articleUrls.TryGetValue(articleId, out string url))
            {
                AppendToOutput($"Opening article: {articleId}");
                OpenUrl(url);
                StatusText = $"Opened {articleId} in browser";
            }
            else
            {
                AppendToOutput($"Article not found: {articleId}");
                StatusText = "Article link not found";
            }
        }
        catch (Exception ex)
        {
            AppendToOutput($"Error opening article: {ex.Message}");
            StatusText = "Error opening article";
        }
    }

    private void OpenUrl(string url)
    {
        try
        {
            // This is a cross-platform way to open URLs
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                // On Windows, use Process.Start with the URL
                url = url.Replace("&", "^&");
                Process.Start(new ProcessStartInfo("cmd", $"/c start {url}") { CreateNoWindow = true });
            }
            else if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
            {
                // On Linux, use xdg-open
                Process.Start("xdg-open", url);
            }
            else if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
            {
                // On macOS, use open
                Process.Start("open", url);
            }
            else
            {
                // If the platform is not recognized, throw an exception
                throw new PlatformNotSupportedException("Platform not supported for opening URLs");
            }
        }
        catch (Exception ex)
        {
            // Log the exception and rethrow
            AppendToOutput($"Error opening URL: {ex.Message}");
            throw;
        }
    }

    // Backups tab command implementations
    private async Task BackupFilesAsync(string componentType)
    {
        await RunTaskAsync($"Backing up {componentType} files...", async (progress) =>
        {
            AppendToOutput($"Backing up {componentType} files...");
            await Task.Delay(500); // Simulate work
            progress.Report(50);
            
            // In a real implementation, this would copy files to the backup folder
            AppendToOutput($"Files identified for backup: 5");
            await Task.Delay(500); // Simulate work
            progress.Report(75);
            
            AppendToOutput($"All {componentType} files have been backed up successfully.\n");
            
            return $"{componentType} files backed up";
        });
    }
    
    private async Task RestoreFilesAsync(string componentType)
    {
        await RunTaskAsync($"Restoring {componentType} files...", async (progress) =>
        {
            AppendToOutput($"Restoring {componentType} files from backup...");
            await Task.Delay(500); // Simulate work
            progress.Report(50);
            
            // In a real implementation, this would copy files from the backup folder
            AppendToOutput($"Files identified for restore: 5");
            await Task.Delay(500); // Simulate work
            progress.Report(75);
            
            AppendToOutput($"All {componentType} files have been restored successfully.\n");
            
            return $"{componentType} files restored";
        });
    }
    
    private async Task RemoveFilesAsync(string componentType)
    {
        await RunTaskAsync($"Removing {componentType} files...", async (progress) =>
        {
            AppendToOutput($"Removing {componentType} files...");
            await Task.Delay(500); // Simulate work
            progress.Report(50);
            
            // In a real implementation, this would delete files
            AppendToOutput($"Files identified for removal: 5");
            await Task.Delay(500); // Simulate work
            progress.Report(75);
            
            AppendToOutput($"All {componentType} files have been removed successfully.\n");
            
            return $"{componentType} files removed";
        });
    }
    
    private void OpenBackupsFolder()
    {
        try
        {
            // In a real implementation, this would use the actual backups folder path
            string backupsFolderPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), "CLASSIC Backup");
            
            AppendToOutput($"Opening backups folder: {backupsFolderPath}");
            
            // Create the directory if it doesn't exist
            if (!Directory.Exists(backupsFolderPath))
            {
                Directory.CreateDirectory(backupsFolderPath);
            }
            
            // Open the folder in the default file explorer
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                Process.Start("explorer.exe", backupsFolderPath);
            }
            else if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
            {
                Process.Start("xdg-open", backupsFolderPath);
            }
            else if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
            {
                Process.Start("open", backupsFolderPath);
            }
            else
            {
                throw new PlatformNotSupportedException("Platform not supported for opening folders");
            }
            
            StatusText = "Opened backups folder";
        }
        catch (Exception ex)
        {
            AppendToOutput($"Error opening backups folder: {ex.Message}");
            StatusText = "Error opening backups folder";
        }
    }

    // Dialog command implementations
    private async Task ShowAboutAsync()
    {
        if (_dialogService != null)
        {
            await _dialogService.ShowAboutDialogAsync();
        }
        else
        {
            AppendToOutput("Dialog service not available.");
        }
    }

    private async Task ShowHelpAsync()
    {
        if (_dialogService != null)
        {
            string helpContent = @"Scanner111 Help

Scanner111 is a utility tool for Bethesda games that helps diagnose and fix common issues.

Main Features:
- Scan crash logs to identify the cause of crashes
- Scan game files for potential issues
- Download and process logs from Pastebin
- Manage game components like XSE, Reshade, Vulkan, and ENB files

For more detailed help on specific features, please visit the Articles tab where you can find links to comprehensive guides.";

            await _dialogService.ShowHelpAsync("Scanner111 Help", helpContent);
        }
        else
        {
            AppendToOutput("Dialog service not available.");
        }
    }

    // Helper methods
    private void AppendToOutput(string text)
    {
        OutputText += text + Environment.NewLine;
    }

    private async Task RunTaskAsync(string statusMessage, Func<IProgress<double>, Task<string>> task)
    {
        try
        {
            // Setup
            StatusText = statusMessage;
            IsTaskRunning = true;
            IsTaskIndeterminate = false;
            TaskProgress = 0;

            // Create progress reporting
            var progress = new Progress<double>(value => TaskProgress = value);

            // Run the task
            string result = await task(progress);

            // Update status
            StatusText = result;
        }
        catch (Exception ex)
        {
            AppendToOutput($"Error: {ex.Message}");
            StatusText = "Error occurred";
        }
        finally
        {
            // Cleanup
            IsTaskRunning = false;
        }
    }
}
