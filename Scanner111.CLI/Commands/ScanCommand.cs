using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Scanner111.CLI.Services;
using Scanner111.Core.Infrastructure;
using Scanner111.Core.Models;
using Scanner111.Core.Pipeline;
using CliScanOptions = Scanner111.CLI.Models.ScanOptions;

namespace Scanner111.CLI.Commands;

public class ScanCommand : ICommand<CliScanOptions>
{
    private readonly IFileScanService _fileScanService;
    private readonly IScanResultProcessor _scanResultProcessor;
    private readonly ICliSettingsService _settingsService;

    public ScanCommand(
        ICliSettingsService settingsService,
        IFileScanService fileScanService,
        IScanResultProcessor scanResultProcessor)
    {
        _settingsService = settingsService;
        _fileScanService = fileScanService;
        _scanResultProcessor = scanResultProcessor;
    }

    /// Executes the scan command asynchronously, handling crash log file scanning based on the given options.
    /// <param name="options">
    /// The options provided through the command line, specifying scan parameters like file path, directory, or override settings.
    /// </param>
    /// <returns>
    /// An integer status code indicating the result of the operation. Returns 0 on success, and 1 if a fatal error occurs.
    /// </returns>
    public async Task<int> ExecuteAsync(CliScanOptions options)
    {
        try
        {
            // Load settings
            var settings = await _settingsService.LoadSettingsAsync();

            // Initialize CLI message handler with settings
            var useColors = !options.DisableColors && !settings.DisableColors;
            var messageHandler = new CliMessageHandler(useColors);
            MessageHandler.Initialize(messageHandler);

            // Apply command line overrides to settings
            ApplyCommandLineSettings(options, settings);

            // Update settings with recent path if scanning specific directory
            if (!string.IsNullOrEmpty(options.ScanDir))
            {
                settings.AddRecentScanDirectory(options.ScanDir);
                await _settingsService.SaveSettingsAsync(settings);
            }

            MessageHandler.MsgInfo("Initializing Scanner111...");

            // Build pipeline
            var pipeline = BuildScanPipeline(options, messageHandler);

            // Get report writer service from the pipeline's service provider
            var reportWriter = GetReportWriterFromPipeline();

            // Collect files to scan
            var scanData = await _fileScanService.CollectFilesToScanAsync(options, settings);

            if (scanData.FilesToScan.Count == 0)
            {
                MessageHandler.MsgError("No crash log files found to analyze");
                MessageHandler.MsgInfo("Supported file patterns: crash-*.log, crash-*.txt, *dump*.log, *dump*.txt");
                return 1;
            }

            MessageHandler.MsgSuccess($"Starting analysis of {scanData.FilesToScan.Count} files...");

            // Process files
            var scanResults = await ProcessFilesAsync(pipeline, scanData, options, reportWriter, settings);

            // Summary
            MessageHandler.MsgSuccess($"Analysis complete! Processed {scanData.FilesToScan.Count} files.");

            if (options.OutputFormat == "summary") PrintSummary(scanResults);

            return 0;
        }
        catch (Exception ex)
        {
            MessageHandler.MsgCritical($"Fatal error during scan: {ex.Message}");
            if (options.Verbose) MessageHandler.MsgDebug($"Stack trace: {ex.StackTrace}");
            return 1;
        }
    }

    private IScanPipeline BuildScanPipeline(CliScanOptions options, CliMessageHandler messageHandler)
    {
        var pipelineBuilder = new ScanPipelineBuilder()
            .AddDefaultAnalyzers()
            .WithMessageHandler(messageHandler)
            .WithCaching()
            .WithEnhancedErrorHandling();
        
        // Enable FCX mode if specified
        if (options.FcxMode == true)
        {
            pipelineBuilder.WithFcxMode(true);
        }

        if (options.Verbose)
            pipelineBuilder.WithLogging(builder => builder.AddConsole().SetMinimumLevel(LogLevel.Debug));
        else
            pipelineBuilder.WithLogging(builder => builder.AddConsole().SetMinimumLevel(LogLevel.Information));

        return pipelineBuilder.Build();
    }

    private IReportWriter GetReportWriterFromPipeline()
    {
        // Create a temporary service provider to get the report writer
        var services = new ServiceCollection();
        services.AddLogging(builder => builder.AddConsole());
        services.AddSingleton<IReportWriter, ReportWriter>();

        var serviceProvider = services.BuildServiceProvider();
        return serviceProvider.GetRequiredService<IReportWriter>();
    }

    private async Task<List<ScanResult>> ProcessFilesAsync(
        IScanPipeline pipeline,
        FileScanData scanData,
        CliScanOptions options,
        IReportWriter reportWriter,
        ApplicationSettings settings)
    {
        var progressContext = options.DisableProgress || settings.DisableProgress
            ? null
            : MessageHandler.CreateProgressContext("Analyzing crash logs", scanData.FilesToScan.Count);

        var scanResults = new List<ScanResult>();
        var processedCount = 0;

        using (progressContext)
        {
            await Task.Run(async () =>
            {
                await foreach (var result in pipeline.ProcessBatchAsync(scanData.FilesToScan))
                {
                    processedCount++;
                    progressContext?.Update(processedCount, $"Processed {Path.GetFileName(result?.LogPath)}");

                    if (result != null)
                    {
                        // Set XSE flag if this file was copied from XSE directory
                        result.WasCopiedFromXse = scanData.XseCopiedFiles.Contains(result.LogPath);

                        scanResults.Add(result);
                        await _scanResultProcessor.ProcessScanResultAsync(result, options, reportWriter,
                            scanData.XseCopiedFiles, settings);
                    }
                }
            });

            progressContext?.Complete();
        }

        return scanResults;
    }

    private void ApplyCommandLineSettings(CliScanOptions options, ApplicationSettings settings)
    {
        // Apply command line overrides (these take precedence over saved settings)
        if (options.FcxMode.HasValue) settings.FcxMode = options.FcxMode.Value;

        if (options.ShowFidValues.HasValue) settings.ShowFormIdValues = options.ShowFidValues.Value;

        if (options.SimplifyLogs.HasValue) settings.SimplifyLogs = options.SimplifyLogs.Value;

        if (options.MoveUnsolved.HasValue) settings.MoveUnsolvedLogs = options.MoveUnsolved.Value;

        if (!string.IsNullOrEmpty(options.CrashLogsDirectory)) settings.CrashLogsDirectory = options.CrashLogsDirectory;

        // Apply defaults from settings if not specified on command line
        if (string.IsNullOrEmpty(options.GamePath) && !string.IsNullOrEmpty(settings.DefaultGamePath))
            options.GamePath = settings.DefaultGamePath;

        if (string.IsNullOrEmpty(options.ScanDir) && !string.IsNullOrEmpty(settings.DefaultScanDirectory))
            options.ScanDir = settings.DefaultScanDirectory;

        if (options.OutputFormat == "detailed" && settings.DefaultOutputFormat != "detailed")
            options.OutputFormat = settings.DefaultOutputFormat;
    }

    private void PrintSummary(List<ScanResult> results)
    {
        MessageHandler.MsgInfo("\n=== SCAN SUMMARY ===");
        MessageHandler.MsgInfo($"Total files scanned: {results.Count}");
        MessageHandler.MsgInfo($"Files with issues: {results.Count(r => r.AnalysisResults.Any(ar => ar.HasFindings))}");
        MessageHandler.MsgInfo($"Clean files: {results.Count(r => !r.AnalysisResults.Any(ar => ar.HasFindings))}");

        var filesWithIssues = results.Where(r => r.AnalysisResults.Any(ar => ar.HasFindings)).ToList();
        if (filesWithIssues.Count == 0) return;
        {
            MessageHandler.MsgInfo("\nFiles with issues:");
            foreach (var result in filesWithIssues)
            {
                var issueCount = result.AnalysisResults.Count(ar => ar.HasFindings);
                MessageHandler.MsgWarning($"  - {Path.GetFileName(result.LogPath)}: {issueCount} issues");
            }
        }
    }
}