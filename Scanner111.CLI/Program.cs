using CommandLine;
using Scanner111.Core.Infrastructure;
using Scanner111.Core.Pipeline;
using Microsoft.Extensions.Logging;
using System.Text;
using Scanner111.Core.Models;
using Scanner111.CLI.Services;
using Scanner111.CLI.Models;
using Microsoft.Extensions.DependencyInjection;

// Helper functions
static IReportWriter GetReportWriterFromPipeline(ScanPipelineBuilder pipelineBuilder)
{
    // Create a temporary service provider to get the report writer
    var services = new Microsoft.Extensions.DependencyInjection.ServiceCollection();
    services.AddLogging(builder => builder.AddConsole());
    services.AddSingleton<IReportWriter, ReportWriter>();
    
    var serviceProvider = services.BuildServiceProvider();
    return serviceProvider.GetRequiredService<IReportWriter>();
}

// Ensure UTF-8 encoding for Windows console
if (OperatingSystem.IsWindows())
{
    Console.OutputEncoding = Encoding.UTF8;
    Console.InputEncoding = Encoding.UTF8;
}

// Initialize settings service
var settingsService = new CliSettingsService();
var settings = await settingsService.LoadSettingsAsync();

// Apply settings to GlobalRegistry
ApplySettingsToRegistry(settings);

// Parse command line arguments
var parser = new Parser(with => with.HelpWriter = Console.Error);
var result = parser.ParseArguments<ScanOptions, DemoOptions, ConfigOptions, AboutOptions>(args);

return await result.MapResult(
    async (ScanOptions opts) => await RunScan(opts, settingsService, settings),
    async (DemoOptions opts) => await RunDemo(opts),
    async (ConfigOptions opts) => await RunConfig(opts, settingsService),
    async (AboutOptions opts) => await RunAbout(opts),
    async errs => await Task.FromResult(1));

async Task<int> RunScan(ScanOptions options, CliSettingsService settingsService, CliSettings settings)
{
    try
    {
        // Initialize CLI message handler with settings
        var useColors = !options.DisableColors && !settings.DisableColors;
        var messageHandler = new CliMessageHandler(useColors);
        MessageHandler.Initialize(messageHandler);
        
        // Apply command line options (these override settings)
        ApplyCommandLineSettings(options, settings);
        
        // Update settings with recent path if scanning specific directory
        if (!string.IsNullOrEmpty(options.ScanDir))
        {
            settings.AddRecentPath(options.ScanDir);
            await settingsService.SaveSettingsAsync(settings);
        }
        
        MessageHandler.MsgInfo("Initializing Scanner111...");
        
        // Build pipeline
        var pipelineBuilder = new ScanPipelineBuilder()
            .AddDefaultAnalyzers()
            .WithMessageHandler(messageHandler)
            .WithCaching(true)
            .WithEnhancedErrorHandling(true);
            
        if (options.Verbose)
        {
            pipelineBuilder.WithLogging(builder => builder.AddConsole().SetMinimumLevel(LogLevel.Debug));
        }
        else
        {
            pipelineBuilder.WithLogging(builder => builder.AddConsole().SetMinimumLevel(LogLevel.Information));
        }
        
        var pipeline = pipelineBuilder.Build();
        
        // Get report writer service from the pipeline's service provider
        var reportWriter = GetReportWriterFromPipeline(pipelineBuilder);
        
        // Collect files to scan and track which ones were copied from XSE
        var xseCopiedFiles = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var filesToScan = CollectFilesToScan(options, xseCopiedFiles);
        
        if (filesToScan.Count == 0)
        {
            MessageHandler.MsgError("No crash log files found to analyze");
            MessageHandler.MsgInfo("Supported file patterns: crash-*.log, crash-*.txt, *dump*.log, *dump*.txt");
            return 1;
        }
        
        MessageHandler.MsgSuccess($"Starting analysis of {filesToScan.Count} files...");
        
        // Process files
        var progressContext = (options.DisableProgress || settings.DisableProgress)
            ? null 
            : MessageHandler.CreateProgressContext("Analyzing crash logs", filesToScan.Count);
        
        var scanResults = new List<ScanResult>();
        int processedCount = 0;
        
        using (progressContext)
        {
            Task.Run(async () =>
            {
                await foreach (var result in pipeline.ProcessBatchAsync(filesToScan))
                {
                    processedCount++;
                    progressContext?.Update(processedCount, $"Processed {Path.GetFileName(result?.LogPath)}");
                    
                    if (result != null)
                    {
                        // Set XSE flag if this file was copied from XSE directory
                        result.WasCopiedFromXSE = xseCopiedFiles.Contains(result.LogPath);
                        
                        scanResults.Add(result);
                        await ProcessScanResult(result, options, reportWriter, xseCopiedFiles);
                    }
                }
            }).Wait();
            
            progressContext?.Complete();
        }
        
        // Summary
        MessageHandler.MsgSuccess($"Analysis complete! Processed {filesToScan.Count} files.");
        
        if (options.OutputFormat == "summary")
        {
            PrintSummary(scanResults);
        }
        
        return 0;
    }
    catch (Exception ex)
    {
        MessageHandler.MsgCritical($"Fatal error during scan: {ex.Message}");
        if (options.Verbose)
        {
            MessageHandler.MsgDebug($"Stack trace: {ex.StackTrace}");
        }
        return 1;
    }
}

async Task<int> RunDemo(DemoOptions options)
{
    // Initialize CLI message handler
    var messageHandler = new CliMessageHandler();
    MessageHandler.Initialize(messageHandler);
    
    MessageHandler.MsgInfo("This is an info message");
    MessageHandler.MsgWarning("This is a warning message");
    MessageHandler.MsgError("This is an error message");
    MessageHandler.MsgSuccess("This is a success message");
    MessageHandler.MsgDebug("This is a debug message");
    MessageHandler.MsgCritical("This is a critical message");
    
    // Demo progress
    using var progress = MessageHandler.CreateProgressContext("Demo Progress", 5);
    for (int i = 1; i <= 5; i++)
    {
        progress.Update(i, $"Step {i} of 5");
        Thread.Sleep(500); // Simulate work
    }
    progress.Complete();
    
    MessageHandler.MsgSuccess("Demo complete!");
    return 0;
}

async Task<int> RunConfig(ConfigOptions options, CliSettingsService settingsService)
{
    var messageHandler = new CliMessageHandler();
    MessageHandler.Initialize(messageHandler);
    
    if (options.ShowPath)
    {
        var settingsPath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "Scanner111", "cli-settings.json");
        MessageHandler.MsgInfo($"CLI Settings file: {settingsPath}");
        MessageHandler.MsgInfo($"File exists: {File.Exists(settingsPath)}");
    }
    
    if (options.Reset)
    {
        MessageHandler.MsgInfo("Resetting configuration to defaults...");
        var defaultSettings = settingsService.GetDefaultSettings();
        await settingsService.SaveSettingsAsync(defaultSettings);
        MessageHandler.MsgSuccess("Configuration reset to defaults.");
        return 0;
    }
    
    if (options.List)
    {
        MessageHandler.MsgInfo("Current Scanner111 Configuration:");
        MessageHandler.MsgInfo("================================");
        MessageHandler.MsgInfo($"FCX Mode: {GlobalRegistry.GetValueType<bool>("FCXMode", false)}");
        MessageHandler.MsgInfo($"Show FormID Values: {GlobalRegistry.GetValueType<bool>("ShowFormIDValues", false)}");
        MessageHandler.MsgInfo($"Simplify Logs: {GlobalRegistry.GetValueType<bool>("SimplifyLogs", false)}");
        MessageHandler.MsgInfo($"Move Unsolved Logs: {GlobalRegistry.GetValueType<bool>("MoveUnsolvedLogs", false)}");
        MessageHandler.MsgInfo($"Crash Logs Directory: {GlobalRegistry.Get<string>("CrashLogsDirectory") ?? ""}");
        MessageHandler.MsgInfo($"Audio Notifications: {GlobalRegistry.GetValueType<bool>("AudioNotifications", false)}");
        MessageHandler.MsgInfo($"VR Mode: {GlobalRegistry.GetValueType<bool>("VRMode", false)}");
        MessageHandler.MsgInfo($"Disable Colors: {GlobalRegistry.GetValueType<bool>("DisableColors", false)}");
        MessageHandler.MsgInfo($"Disable Progress: {GlobalRegistry.GetValueType<bool>("DisableProgress", false)}");
        MessageHandler.MsgInfo($"Default Output Format: {GlobalRegistry.Get<string>("DefaultOutputFormat") ?? "detailed"}");
        MessageHandler.MsgInfo($"Default Game Path: {GlobalRegistry.Get<string>("DefaultGamePath") ?? "(not set)"}");
        MessageHandler.MsgInfo($"Default Scan Directory: {GlobalRegistry.Get<string>("DefaultScanDirectory") ?? "(not set)"}");
    }
    
    if (!string.IsNullOrEmpty(options.Set))
    {
        var parts = options.Set.Split('=');
        if (parts.Length == 2)
        {
            var key = parts[0].Trim();
            var value = parts[1].Trim();
            
            try
            {
                // Save to settings file
                await settingsService.SaveSettingAsync(key, value);
                
                // Update GlobalRegistry for current session
                GlobalRegistry.Set(key, value);
                
                MessageHandler.MsgSuccess($"Set {key} = {value}");
                MessageHandler.MsgInfo("Setting saved to configuration file.");
            }
            catch (ArgumentException ex)
            {
                MessageHandler.MsgError(ex.Message);
                return 1;
            }
        }
        else
        {
            MessageHandler.MsgError("Invalid set format. Use: --set \"key=value\"");
            MessageHandler.MsgInfo("Available settings:");
            MessageHandler.MsgInfo("  FcxMode, ShowFormIdValues, SimplifyLogs, MoveUnsolvedLogs");
            MessageHandler.MsgInfo("  AudioNotifications, VrMode, DisableColors, DisableProgress");
            MessageHandler.MsgInfo("  DefaultOutputFormat, DefaultGamePath, DefaultScanDirectory, CrashLogsDirectory");
            return 1;
        }
    }
    
    return 0;
}

async Task<int> RunAbout(AboutOptions options)
{
    var messageHandler = new CliMessageHandler();
    MessageHandler.Initialize(messageHandler);
    
    var assembly = System.Reflection.Assembly.GetExecutingAssembly();
    var version = assembly.GetName().Version ?? new Version(1, 0, 0);
    
    MessageHandler.MsgInfo($"Scanner111 - CLASSIC Crash Log Analyzer");
    MessageHandler.MsgInfo($"Version: {version}");
    MessageHandler.MsgInfo($"Compatible with Bethesda games crash logs");
    MessageHandler.MsgInfo($"Based on CLASSIC Python implementation");
    
    return 0;
}

void ApplySettingsToRegistry(CliSettings settings)
{
    // Load settings into GlobalRegistry at startup
    GlobalRegistry.Set("FCXMode", settings.FcxMode);
    GlobalRegistry.Set("ShowFormIDValues", settings.ShowFormIdValues);
    GlobalRegistry.Set("SimplifyLogs", settings.SimplifyLogs);
    GlobalRegistry.Set("MoveUnsolvedLogs", settings.MoveUnsolvedLogs);
    GlobalRegistry.Set("AudioNotifications", settings.AudioNotifications);
    GlobalRegistry.Set("VRMode", settings.VrMode);
    GlobalRegistry.Set("DefaultGamePath", settings.DefaultGamePath);
    GlobalRegistry.Set("DefaultScanDirectory", settings.DefaultScanDirectory);
    GlobalRegistry.Set("DefaultOutputFormat", settings.DefaultOutputFormat);
    GlobalRegistry.Set("DisableColors", settings.DisableColors);
    GlobalRegistry.Set("DisableProgress", settings.DisableProgress);
    GlobalRegistry.Set("VerboseLogging", settings.VerboseLogging);
    GlobalRegistry.Set("MaxConcurrentScans", settings.MaxConcurrentScans);
    GlobalRegistry.Set("CacheEnabled", settings.CacheEnabled);
    GlobalRegistry.Set("CrashLogsDirectory", settings.CrashLogsDirectory);
}

void ApplyCommandLineSettings(ScanOptions options, CliSettings settings)
{
    // Apply command line overrides (these take precedence over saved settings)
    if (options.FcxMode.HasValue)
    {
        GlobalRegistry.Set("FCXMode", options.FcxMode.Value);
    }
    
    if (options.ShowFidValues.HasValue)
    {
        GlobalRegistry.Set("ShowFormIDValues", options.ShowFidValues.Value);
    }
    
    if (options.SimplifyLogs.HasValue)
    {
        GlobalRegistry.Set("SimplifyLogs", options.SimplifyLogs.Value);
    }
    
    if (options.MoveUnsolved.HasValue)
    {
        GlobalRegistry.Set("MoveUnsolvedLogs", options.MoveUnsolved.Value);
    }
    
    if (!string.IsNullOrEmpty(options.CrashLogsDirectory))
    {
        GlobalRegistry.Set("CrashLogsDirectory", options.CrashLogsDirectory);
    }
    
    // Apply defaults from settings if not specified on command line
    if (string.IsNullOrEmpty(options.GamePath) && !string.IsNullOrEmpty(settings.DefaultGamePath))
    {
        options.GamePath = settings.DefaultGamePath;
    }
    
    if (string.IsNullOrEmpty(options.ScanDir) && !string.IsNullOrEmpty(settings.DefaultScanDirectory))
    {
        options.ScanDir = settings.DefaultScanDirectory;
    }
    
    if (options.OutputFormat == "detailed" && settings.DefaultOutputFormat != "detailed")
    {
        options.OutputFormat = settings.DefaultOutputFormat;
    }
}

List<string> CollectFilesToScan(ScanOptions options, HashSet<string> xseCopiedFiles)
{
    var filesToScan = new List<string>();
    
    // Auto-copy XSE crash logs first (F4SE and SKSE, similar to GUI functionality)
    CopyXSELogs(filesToScan, options, xseCopiedFiles);
    
    // Add specific log file if provided
    if (!string.IsNullOrEmpty(options.LogFile) && File.Exists(options.LogFile))
    {
        filesToScan.Add(options.LogFile);
        MessageHandler.MsgInfo($"Added log file: {Path.GetFileName(options.LogFile)}");
    }
    
    // Scan directory if provided
    if (!string.IsNullOrEmpty(options.ScanDir) && Directory.Exists(options.ScanDir))
    {
        var directory = new DirectoryInfo(options.ScanDir);
        var logs = directory.GetFiles("*.log", SearchOption.TopDirectoryOnly)
            .Concat(directory.GetFiles("*.txt", SearchOption.TopDirectoryOnly))
            .Where(f => f.Name.ToLower().Contains("crash") || f.Name.ToLower().Contains("dump"))
            .Select(f => f.FullName)
            .ToList();
            
        filesToScan.AddRange(logs);
        MessageHandler.MsgInfo($"Found {logs.Count} crash logs in scan directory");
    }
    
    // If no specific files provided, scan current directory
    if (filesToScan.Count == 0 && string.IsNullOrEmpty(options.LogFile) && string.IsNullOrEmpty(options.ScanDir))
    {
        var currentDir = new DirectoryInfo(Directory.GetCurrentDirectory());
        var logs = currentDir.GetFiles("crash-*.log", SearchOption.TopDirectoryOnly)
            .Concat(currentDir.GetFiles("crash-*.txt", SearchOption.TopDirectoryOnly))
            .Select(f => f.FullName)
            .ToList();
            
        filesToScan.AddRange(logs);
        if (logs.Any())
        {
            MessageHandler.MsgInfo($"Found {logs.Count} crash logs in current directory");
        }
    }
    
    return filesToScan.Distinct().ToList();
}

void CopyXSELogs(List<string> filesToScan, ScanOptions options, HashSet<string> xseCopiedFiles)
{
    // Skip XSE copy if user disabled it
    if (options.SkipXSECopy)
    {
        return;
    }
    
    try
    {
        // Get crash logs directory from settings or use default
        var crashLogsBaseDir = GlobalRegistry.Get<string>("CrashLogsDirectory");
        if (string.IsNullOrEmpty(crashLogsBaseDir))
        {
            crashLogsBaseDir = CrashLogDirectoryManager.GetDefaultCrashLogsDirectory();
        }

        // Look for XSE crash logs in common locations (F4SE and SKSE)
        var xsePaths = new[]
        {
            // F4SE paths
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), "My Games", "Fallout4", "F4SE"),
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), "My Games", "Fallout4VR", "F4SE"),
            // SKSE paths (including GOG version)
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), "My Games", "Skyrim Special Edition", "SKSE"),
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), "My Games", "Skyrim Special Edition GOG", "SKSE"),
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), "My Games", "Skyrim", "SKSE"),
            // Also check game path if provided
            !string.IsNullOrEmpty(options.GamePath) ? Path.Combine(options.GamePath, "Data", "F4SE") : null,
            !string.IsNullOrEmpty(options.GamePath) ? Path.Combine(options.GamePath, "Data", "SKSE") : null
        }.Where(path => path != null && Directory.Exists(path)).ToArray();

        var copiedCount = 0;
        foreach (var xsePath in xsePaths)
        {
            if (Directory.Exists(xsePath))
            {
                var crashLogs = Directory.GetFiles(xsePath!, "*.log", SearchOption.TopDirectoryOnly)
                    .Where(f => Path.GetFileName(f).StartsWith("crash-", StringComparison.OrdinalIgnoreCase))
                    .OrderByDescending(File.GetLastWriteTime)
                    .ToArray();

                foreach (var logFile in crashLogs)
                {
                    // Detect game type and copy to appropriate subdirectory
                    var gameType = CrashLogDirectoryManager.DetectGameType(options.GamePath, logFile);
                    var targetPath = CrashLogDirectoryManager.CopyCrashLog(logFile, crashLogsBaseDir, gameType, overwrite: true);
                    
                    var xseType = xsePath.Contains("F4SE") ? "F4SE" : "SKSE";
                    MessageHandler.MsgDebug($"Copied {xseType} {gameType} crash log: {Path.GetFileName(logFile)} -> {Path.GetDirectoryName(targetPath)}");
                    copiedCount++;
                    
                    if (!filesToScan.Contains(targetPath))
                    {
                        filesToScan.Add(targetPath);
                        xseCopiedFiles.Add(targetPath);
                    }
                }
            }
        }

        if (copiedCount > 0)
        {
            MessageHandler.MsgSuccess($"Auto-copied {copiedCount} XSE crash logs");
        }
        else if (xsePaths.Length == 0)
        {
            MessageHandler.MsgDebug("No XSE directories found for auto-copy");
        }
        else
        {
            MessageHandler.MsgDebug("No new XSE crash logs to copy");
        }
    }
    catch (Exception ex)
    {
        MessageHandler.MsgWarning($"Error during XSE auto-copy: {ex.Message}");
    }
}

async Task ProcessScanResult(ScanResult result, ScanOptions options, IReportWriter reportWriter, HashSet<string> xseCopiedFiles)
{
    
    if (options.OutputFormat == "summary")
    {
        // Just collect for summary at the end
        return;
    }
    
    // Show scan status without printing full report (reports are auto-saved to files)
    if (result.AnalysisResults.Any(r => r.HasFindings))
    {
        MessageHandler.MsgWarning($"Found {result.AnalysisResults.Count(r => r.HasFindings)} issues in {Path.GetFileName(result.LogPath)}");
    }
    else
    {
        MessageHandler.MsgSuccess($"No issues found in {Path.GetFileName(result.LogPath)}");
    }
    
    // Auto-save report to file (unless disabled by setting)
    var autoSaveReports = GlobalRegistry.GetValueType<bool>("AutoSaveResults", true);
    if (autoSaveReports && !string.IsNullOrEmpty(result.ReportText))
    {
        try
        {
            string outputPath;
            if (result.WasCopiedFromXSE)
            {
                // For XSE copied files, use the default OutputPath (alongside the copied log)
                outputPath = result.OutputPath;
            }
            else
            {
                // For non-XSE files (directly scanned), write report alongside the source log
                outputPath = Path.ChangeExtension(result.LogPath, null) + "-AUTOSCAN.md";
            }
            
            var success = await reportWriter.WriteReportAsync(result, outputPath);
            if (!success)
            {
                MessageHandler.MsgWarning($"Failed to save report for {Path.GetFileName(result.LogPath)}");
            }
        }
        catch (Exception ex)
        {
            MessageHandler.MsgError($"Error saving report: {ex.Message}");
        }
    }
}

void PrintSummary(List<ScanResult> results)
{
    MessageHandler.MsgInfo("\n=== SCAN SUMMARY ===");
    MessageHandler.MsgInfo($"Total files scanned: {results.Count}");
    MessageHandler.MsgInfo($"Files with issues: {results.Count(r => r.AnalysisResults.Any(ar => ar.HasFindings))}");
    MessageHandler.MsgInfo($"Clean files: {results.Count(r => !r.AnalysisResults.Any(ar => ar.HasFindings))}");
    
    var filesWithIssues = results.Where(r => r.AnalysisResults.Any(ar => ar.HasFindings)).ToList();
    if (filesWithIssues.Any())
    {
        MessageHandler.MsgInfo("\nFiles with issues:");
        foreach (var result in filesWithIssues)
        {
            var issueCount = result.AnalysisResults.Count(ar => ar.HasFindings);
            MessageHandler.MsgWarning($"  - {Path.GetFileName(result.LogPath)}: {issueCount} issues");
        }
    }
}

// Command line option classes
[Verb("scan", isDefault: true, HelpText = "Scan crash log files for issues")]
public class ScanOptions
{
    [Option('l', "log", HelpText = "Path to specific crash log file")]
    public string? LogFile { get; set; }
    
    [Option('d', "scan-dir", HelpText = "Directory to scan for crash logs")]
    public string? ScanDir { get; set; }
    
    [Option('g', "game-path", HelpText = "Path to game installation directory")]
    public string? GamePath { get; set; }
    
    [Option('v', "verbose", HelpText = "Enable verbose output")]
    public bool Verbose { get; set; }
    
    [Option("fcx-mode", HelpText = "Enable FCX mode for enhanced file checks")]
    public bool? FcxMode { get; set; }
    
    [Option("show-fid-values", HelpText = "Show FormID values (slower scans)")]
    public bool? ShowFidValues { get; set; }
    
    [Option("simplify-logs", HelpText = "Simplify logs (Warning: May remove important information)")]
    public bool? SimplifyLogs { get; set; }
    
    [Option("move-unsolved", HelpText = "Move unsolved logs to separate folder")]
    public bool? MoveUnsolved { get; set; }
    
    [Option("crash-logs-dir", HelpText = "Directory to store copied crash logs (with game subfolders)")]
    public string? CrashLogsDirectory { get; set; }
    
    [Option("skip-xse-copy", HelpText = "Skip automatic XSE (F4SE/SKSE) crash log copying")]
    public bool SkipXSECopy { get; set; }
    
    [Option("disable-progress", HelpText = "Disable progress bars in CLI mode")]
    public bool DisableProgress { get; set; }
    
    [Option("disable-colors", HelpText = "Disable colored output")]
    public bool DisableColors { get; set; }
    
    [Option('o', "output-format", Default = "detailed", HelpText = "Output format: detailed or summary")]
    public string OutputFormat { get; set; } = "detailed";
}

[Verb("demo", HelpText = "Demonstrate message handler features")]
public class DemoOptions
{
}

[Verb("config", HelpText = "Manage Scanner111 configuration")]
public class ConfigOptions
{
    [Option('l', "list", HelpText = "List current configuration")]
    public bool List { get; set; }
    
    [Option('s', "set", HelpText = "Set configuration value (format: key=value)")]
    public string? Set { get; set; }
    
    [Option('r', "reset", HelpText = "Reset configuration to defaults")]
    public bool Reset { get; set; }
    
    [Option("show-path", HelpText = "Show configuration file path")]
    public bool ShowPath { get; set; }
}


[Verb("about", HelpText = "Show version and about information")]
public class AboutOptions
{
}