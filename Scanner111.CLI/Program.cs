using CommandLine;
using Scanner111.Core.Infrastructure;
using Scanner111.Core.Pipeline;
using Microsoft.Extensions.Logging;
using System.Text;
using Scanner111.Core.Models;

// Ensure UTF-8 encoding for Windows console
if (OperatingSystem.IsWindows())
{
    Console.OutputEncoding = Encoding.UTF8;
    Console.InputEncoding = Encoding.UTF8;
}

// Parse command line arguments
var parser = new Parser(with => with.HelpWriter = Console.Error);
var result = parser.ParseArguments<ScanOptions, DemoOptions, ConfigOptions, AboutOptions>(args);

return result.MapResult(
    (ScanOptions opts) => RunScan(opts),
    (DemoOptions opts) => RunDemo(opts),
    (ConfigOptions opts) => RunConfig(opts),
    (AboutOptions opts) => RunAbout(opts),
    errs => 1);

int RunScan(ScanOptions options)
{
    try
    {
        // Initialize CLI message handler
        var messageHandler = new CliMessageHandler(!options.DisableColors);
        MessageHandler.Initialize(messageHandler);
        
        // Apply settings from command line options
        ApplyCommandLineSettings(options);
        
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
        
        // Collect files to scan
        var filesToScan = CollectFilesToScan(options);
        
        if (filesToScan.Count == 0)
        {
            MessageHandler.MsgError("No crash log files found to analyze");
            MessageHandler.MsgInfo("Supported file patterns: crash-*.log, crash-*.txt, *dump*.log, *dump*.txt");
            return 1;
        }
        
        MessageHandler.MsgSuccess($"Starting analysis of {filesToScan.Count} files...");
        
        // Process files
        var progressContext = options.DisableProgress 
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
                        scanResults.Add(result);
                        ProcessScanResult(result, options);
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

int RunDemo(DemoOptions options)
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

int RunConfig(ConfigOptions options)
{
    var messageHandler = new CliMessageHandler();
    MessageHandler.Initialize(messageHandler);
    
    if (options.List)
    {
        MessageHandler.MsgInfo("Current Scanner111 Configuration:");
        MessageHandler.MsgInfo("================================");
        MessageHandler.MsgInfo($"FCX Mode: {GlobalRegistry.GetValueType<bool>("FCXMode", false)}");
        MessageHandler.MsgInfo($"Show FormID Values: {GlobalRegistry.GetValueType<bool>("ShowFormIDValues", false)}");
        MessageHandler.MsgInfo($"Simplify Logs: {GlobalRegistry.GetValueType<bool>("SimplifyLogs", false)}");
        MessageHandler.MsgInfo($"Move Unsolved Logs: {GlobalRegistry.GetValueType<bool>("MoveUnsolvedLogs", false)}");
        MessageHandler.MsgInfo($"Audio Notifications: {GlobalRegistry.GetValueType<bool>("AudioNotifications", false)}");
        MessageHandler.MsgInfo($"VR Mode: {GlobalRegistry.GetValueType<bool>("VRMode", false)}");
    }
    
    if (!string.IsNullOrEmpty(options.Set))
    {
        var parts = options.Set.Split('=');
        if (parts.Length == 2)
        {
            var key = parts[0].Trim();
            var value = parts[1].Trim();
            
            // TODO: Implement configuration setting
            MessageHandler.MsgSuccess($"Set {key} = {value}");
        }
        else
        {
            MessageHandler.MsgError("Invalid set format. Use: --set \"key=value\"");
            return 1;
        }
    }
    
    return 0;
}

int RunAbout(AboutOptions options)
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

void ApplyCommandLineSettings(ScanOptions options)
{
    // Apply command line overrides to settings
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
}

List<string> CollectFilesToScan(ScanOptions options)
{
    var filesToScan = new List<string>();
    
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

void ProcessScanResult(ScanResult result, ScanOptions options)
{
    if (options.OutputFormat == "json")
    {
        // TODO: Implement JSON output
        return;
    }
    
    if (options.OutputFormat == "summary")
    {
        // Just collect for summary at the end
        return;
    }
    
    // Detailed output (default)
    MessageHandler.MsgInfo($"\nScan result for {Path.GetFileName(result.LogPath)}: {result.Status}");
    
    if (result.AnalysisResults.Any(r => r.HasFindings))
    {
        MessageHandler.MsgWarning($"Found {result.AnalysisResults.Count(r => r.HasFindings)} issues in {Path.GetFileName(result.LogPath)}");
        
        if (options.Verbose || options.OutputFormat == "detailed")
        {
            Console.WriteLine("\n" + result.ReportText);
        }
    }
    else
    {
        MessageHandler.MsgSuccess($"No issues found in {Path.GetFileName(result.LogPath)}");
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
    
    [Option("disable-progress", HelpText = "Disable progress bars in CLI mode")]
    public bool DisableProgress { get; set; }
    
    [Option("disable-colors", HelpText = "Disable colored output")]
    public bool DisableColors { get; set; }
    
    [Option('o', "output-format", Default = "detailed", HelpText = "Output format: detailed, summary, or json")]
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
}

[Verb("about", HelpText = "Show version and about information")]
public class AboutOptions
{
}