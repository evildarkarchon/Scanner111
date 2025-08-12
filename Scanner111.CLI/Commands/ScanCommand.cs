using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Scanner111.CLI.Services;
using Scanner111.Core.Infrastructure;
using Scanner111.Core.Models;
using Scanner111.Core.Pipeline;
using Scanner111.Core.Services;
using Scanner111.Core.ModManagers;
using CliScanOptions = Scanner111.CLI.Models.ScanOptions;

namespace Scanner111.CLI.Commands;

public class ScanCommand : ICommand<CliScanOptions>
{
    private readonly IFileScanService _fileScanService;
    private readonly IScanResultProcessor _scanResultProcessor;
    private readonly ICliSettingsService _settingsService;
    private readonly IMessageHandler _messageHandler;
    private readonly IModManagerService? _modManagerService;

    public ScanCommand(
        ICliSettingsService settingsService,
        IFileScanService fileScanService,
        IScanResultProcessor scanResultProcessor,
        IMessageHandler messageHandler,
        IModManagerService? modManagerService = null)
    {
        _settingsService = settingsService ?? throw new ArgumentNullException(nameof(settingsService));
        _fileScanService = fileScanService ?? throw new ArgumentNullException(nameof(fileScanService));
        _scanResultProcessor = scanResultProcessor ?? throw new ArgumentNullException(nameof(scanResultProcessor));
        _messageHandler = messageHandler ?? throw new ArgumentNullException(nameof(messageHandler));
        _modManagerService = modManagerService;
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

            // Note: MessageHandler is already injected via constructor

            // Apply command line overrides to settings
            ApplyCommandLineSettings(options, settings);

            // Update settings with recent path if scanning specific directory
            if (!string.IsNullOrEmpty(options.ScanDir))
            {
                settings.AddRecentScanDirectory(options.ScanDir);
                await _settingsService.SaveSettingsAsync(settings);
            }

            _messageHandler.ShowInfo("Initializing Scanner111...");

            // Build pipeline
            var pipeline = BuildScanPipeline(options, _messageHandler);

            // Get report writer service from the pipeline's service provider
            var reportWriter = GetReportWriterFromPipeline();

            // Collect files to scan
            var scanData = await _fileScanService.CollectFilesToScanAsync(options, settings);

            if (scanData.FilesToScan.Count == 0)
            {
                _messageHandler.ShowError("No crash log files found to analyze");
                _messageHandler.ShowInfo("Supported file patterns: crash-*.log, crash-*.txt, *dump*.log, *dump*.txt");
                return 1;
            }

            _messageHandler.ShowSuccess($"Starting analysis of {scanData.FilesToScan.Count} files...");

            // Process files
            var scanResults = await ProcessFilesAsync(pipeline, scanData, options, reportWriter, settings);

            // Summary
            _messageHandler.ShowSuccess($"Analysis complete! Processed {scanData.FilesToScan.Count} files.");

            if (options.OutputFormat == "summary") PrintSummary(scanResults);

            return 0;
        }
        catch (Exception ex)
        {
            _messageHandler.ShowCritical($"Fatal error during scan: {ex.Message}");
            if (options.Verbose) _messageHandler.ShowDebug($"Stack trace: {ex.StackTrace}");
            return 1;
        }
    }

    private IScanPipeline BuildScanPipeline(CliScanOptions options, IMessageHandler messageHandler)
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
            await foreach (var result in pipeline.ProcessBatchAsync(scanData.FilesToScan).ConfigureAwait(false))
            {
                processedCount++;
                progressContext?.Update(processedCount, $"Processed {Path.GetFileName(result?.LogPath)}");

                if (result != null)
                {
                    // Set XSE flag if this file was copied from XSE directory
                    result.WasCopiedFromXse = scanData.XseCopiedFiles.Contains(result.LogPath);

                    scanResults.Add(result);
                    await _scanResultProcessor.ProcessScanResultAsync(result, options, reportWriter,
                        scanData.XseCopiedFiles, settings).ConfigureAwait(false);
                }
            }

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

        // Apply mod manager settings
        if (!string.IsNullOrEmpty(options.MO2Path)) 
        {
            settings.MO2InstallPath = options.MO2Path;
            if (settings.ModManagerSettings == null)
                settings.ModManagerSettings = new ModManagerSettings();
            settings.ModManagerSettings.MO2InstallPath = options.MO2Path;
        }

        if (!string.IsNullOrEmpty(options.MO2Profile))
        {
            settings.MO2DefaultProfile = options.MO2Profile;
            if (settings.ModManagerSettings == null)
                settings.ModManagerSettings = new ModManagerSettings();
            settings.ModManagerSettings.MO2DefaultProfile = options.MO2Profile;
        }

        if (!string.IsNullOrEmpty(options.VortexPath))
        {
            settings.VortexDataPath = options.VortexPath;
            if (settings.ModManagerSettings == null)
                settings.ModManagerSettings = new ModManagerSettings();
            settings.ModManagerSettings.VortexDataPath = options.VortexPath;
        }

        if (options.SkipModManagers)
        {
            settings.AutoDetectModManagers = false;
            if (settings.ModManagerSettings == null)
                settings.ModManagerSettings = new ModManagerSettings();
            settings.ModManagerSettings.SkipModManagerIntegration = true;
        }

        if (!string.IsNullOrEmpty(options.PreferredModManager))
        {
            settings.DefaultModManager = options.PreferredModManager;
            if (settings.ModManagerSettings == null)
                settings.ModManagerSettings = new ModManagerSettings();
            settings.ModManagerSettings.DefaultManager = options.PreferredModManager;
            
            // Set preference in the mod manager service if available
            if (_modManagerService != null && Enum.TryParse<ModManagerType>(options.PreferredModManager, true, out var managerType))
            {
                _modManagerService.SetPreferredManager(managerType);
            }
        }

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
        _messageHandler.ShowInfo("\n=== SCAN SUMMARY ===");
        _messageHandler.ShowInfo($"Total files scanned: {results.Count}");
        _messageHandler.ShowInfo($"Files with issues: {results.Count(r => r.AnalysisResults.Any(ar => ar.HasFindings))}");
        _messageHandler.ShowInfo($"Clean files: {results.Count(r => !r.AnalysisResults.Any(ar => ar.HasFindings))}");

        var filesWithIssues = results.Where(r => r.AnalysisResults.Any(ar => ar.HasFindings)).ToList();
        if (filesWithIssues.Count == 0) return;
        {
            _messageHandler.ShowInfo("\nFiles with issues:");
            foreach (var result in filesWithIssues)
            {
                var issueCount = result.AnalysisResults.Count(ar => ar.HasFindings);
                _messageHandler.ShowWarning($"  - {Path.GetFileName(result.LogPath)}: {issueCount} issues");
            }
        }
    }
}