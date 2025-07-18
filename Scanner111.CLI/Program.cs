using Scanner111.Core.Infrastructure;
using Scanner111.Core.Pipeline;
using Microsoft.Extensions.Logging;
using System.CommandLine;

// Initialize CLI message handler
var messageHandler = new CliMessageHandler();
MessageHandler.Initialize(messageHandler);

// Create root command
var rootCommand = new RootCommand("Scanner111 - CLASSIC Crash Log Analyzer CLI");

// Scan command
var scanCommand = new Command("scan", "Scan crash log files for issues");
var logFileOption = new Option<FileInfo?>("--log", "Path to crash log file") { IsRequired = false };
var gamePathOption = new Option<DirectoryInfo?>("--game-path", "Path to game installation directory") { IsRequired = false };
var scanDirOption = new Option<DirectoryInfo?>("--scan-dir", "Directory to scan for crash logs") { IsRequired = false };
var verboseOption = new Option<bool>("--verbose", "Enable verbose output") { IsRequired = false };

scanCommand.AddOption(logFileOption);
scanCommand.AddOption(gamePathOption);
scanCommand.AddOption(scanDirOption);
scanCommand.AddOption(verboseOption);

scanCommand.SetHandler(async (logFile, gamePath, scanDir, verbose) =>
{
    try
    {
        MessageHandler.MsgInfo("Initializing Scanner111...");
        
        // Build pipeline
        var pipelineBuilder = new ScanPipelineBuilder()
            .AddDefaultAnalyzers()
            .WithMessageHandler(messageHandler)
            .WithCaching(true)
            .WithEnhancedErrorHandling(true);
            
        if (verbose)
        {
            pipelineBuilder.WithLogging(builder => builder.AddConsole().SetMinimumLevel(LogLevel.Debug));
        }
        else
        {
            pipelineBuilder.WithLogging(builder => builder.AddConsole().SetMinimumLevel(LogLevel.Information));
        }
        
        var pipeline = pipelineBuilder.Build();
        
        // Collect files to scan
        var filesToScan = new List<string>();
        
        if (logFile != null && logFile.Exists)
        {
            filesToScan.Add(logFile.FullName);
            MessageHandler.MsgInfo($"Added log file: {logFile.Name}");
        }
        
        if (scanDir != null && scanDir.Exists)
        {
            var logs = scanDir.GetFiles("*.log", SearchOption.TopDirectoryOnly)
                .Concat(scanDir.GetFiles("*.txt", SearchOption.TopDirectoryOnly))
                .Where(f => f.Name.ToLower().Contains("crash") || f.Name.ToLower().Contains("dump"))
                .Select(f => f.FullName);
                
            filesToScan.AddRange(logs);
            MessageHandler.MsgInfo($"Found {logs.Count()} crash logs in scan directory");
        }
        
        if (filesToScan.Count == 0)
        {
            MessageHandler.MsgError("No crash log files found to analyze");
            return;
        }
        
        MessageHandler.MsgSuccess($"Starting analysis of {filesToScan.Count} files...");
        
        // Process files
        using var progress = MessageHandler.CreateProgressContext("Analyzing crash logs", filesToScan.Count);
        
        int processedCount = 0;
        await foreach (var result in pipeline.ProcessBatchAsync(filesToScan))
        {
            processedCount++;
            progress.Update(processedCount, $"Processed {Path.GetFileName(result?.LogPath)}");
            
            if (result != null)
            {
                MessageHandler.MsgInfo($"Scan result for {Path.GetFileName(result.LogPath)}: {result.Status}");
                
                if (result.AnalysisResults.Any(r => r.HasFindings))
                {
                    MessageHandler.MsgWarning($"Found {result.AnalysisResults.Count(r => r.HasFindings)} issues in {Path.GetFileName(result.LogPath)}");
                    
                    if (verbose)
                    {
                        Console.WriteLine("\n" + result.ReportText);
                    }
                }
                else
                {
                    MessageHandler.MsgSuccess($"No issues found in {Path.GetFileName(result.LogPath)}");
                }
            }
        }
        
        progress.Complete();
        MessageHandler.MsgSuccess($"Analysis complete! Processed {processedCount} files.");
    }
    catch (Exception ex)
    {
        MessageHandler.MsgCritical($"Fatal error during scan: {ex.Message}");
        if (verbose)
        {
            MessageHandler.MsgDebug($"Stack trace: {ex.StackTrace}");
        }
    }
}, logFileOption, gamePathOption, scanDirOption, verboseOption);

// Demo command to show message types
var demoCommand = new Command("demo", "Demonstrate message handler features");
demoCommand.SetHandler(() =>
{
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
});

rootCommand.AddCommand(scanCommand);
rootCommand.AddCommand(demoCommand);

// Run the application
return await rootCommand.InvokeAsync(args);
