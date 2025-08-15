using Scanner111.CLI.Services;
using Scanner111.Core.Infrastructure;
using Scanner111.Core.Models;
using Scanner111.Core.ModManagers;
using Scanner111.Core.Pipeline;
using Scanner111.Core.Services;
using CliScanOptions = Scanner111.CLI.Models.ScanOptions;
using ScanStatistics = Scanner111.Core.Services.ScanStatistics;

namespace Scanner111.CLI.Commands;

public class ScanCommand : ICommand<CliScanOptions>
{
    private readonly IAudioNotificationService? _audioService;
    private readonly IFileScanService _fileScanService;
    private readonly IMessageHandler _messageHandler;
    private readonly IModManagerService? _modManagerService;
    private readonly IRecentItemsService? _recentItemsService;
    private readonly IScanResultProcessor _scanResultProcessor;
    private readonly ICliSettingsService _settingsService;
    private readonly IStatisticsService? _statisticsService;

    public ScanCommand(
        ICliSettingsService settingsService,
        IFileScanService fileScanService,
        IScanResultProcessor scanResultProcessor,
        IMessageHandler messageHandler,
        IModManagerService? modManagerService = null,
        IStatisticsService? statisticsService = null,
        IAudioNotificationService? audioService = null,
        IRecentItemsService? recentItemsService = null)
    {
        _settingsService = Guard.NotNull(settingsService, nameof(settingsService));
        _fileScanService = Guard.NotNull(fileScanService, nameof(fileScanService));
        _scanResultProcessor = Guard.NotNull(scanResultProcessor, nameof(scanResultProcessor));
        _messageHandler = Guard.NotNull(messageHandler, nameof(messageHandler));
        _modManagerService = modManagerService;
        _statisticsService = statisticsService;
        _audioService = audioService;
        _recentItemsService = recentItemsService;
    }

    /// Executes the scan command asynchronously, handling crash log file scanning based on the given options.
    /// <param name="options">
    ///     The options provided through the command line, specifying scan parameters like file path, directory, or override
    ///     settings.
    /// </param>
    /// <returns>
    ///     An integer status code indicating the result of the operation. Returns 0 on success, and 1 if a fatal error occurs.
    /// </returns>
    public async Task<int> ExecuteAsync(CliScanOptions options)
    {
        var stopwatch = Stopwatch.StartNew();
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

            stopwatch.Stop();

            // Record statistics
            await RecordStatisticsAsync(scanResults, scanData, stopwatch.Elapsed, options);

            // Track recent files
            await TrackRecentFilesAsync(scanData.FilesToScan, options);

            // Summary
            _messageHandler.ShowSuccess($"Analysis complete! Processed {scanData.FilesToScan.Count} files.");

            if (options.OutputFormat == "summary") PrintSummary(scanResults);

            // Play completion sound
            await PlayCompletionSoundAsync(scanResults);

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
        if (options.FcxMode == true) pipelineBuilder.WithFcxMode();

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
            if (_modManagerService != null &&
                Enum.TryParse<ModManagerType>(options.PreferredModManager, true, out var managerType))
                _modManagerService.SetPreferredManager(managerType);
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
        _messageHandler.ShowInfo(
            $"Files with issues: {results.Count(r => r.AnalysisResults.Any(ar => ar.HasFindings))}");
        _messageHandler.ShowInfo($"Clean files: {results.Count(r => !r.AnalysisResults.Any(ar => ar.HasFindings))}");

        var filesWithIssues = results.Where(r => r.AnalysisResults.Any(ar => ar.HasFindings)).ToList();
        if (filesWithIssues.Count == 0) return;

        _messageHandler.ShowInfo("\nFiles with issues:");
        foreach (var result in filesWithIssues)
        {
            var issueCount = result.AnalysisResults.Count(ar => ar.HasFindings);
            _messageHandler.ShowWarning($"  - {Path.GetFileName(result.LogPath)}: {issueCount} issues");
        }
    }

    private async Task RecordStatisticsAsync(List<ScanResult> results, FileScanData scanData, TimeSpan processingTime,
        CliScanOptions options)
    {
        if (_statisticsService == null) return;

        try
        {
            foreach (var result in results)
            {
                var issuesByType = new Dictionary<string, int>();
                var criticalCount = 0;
                var warningCount = 0;
                var infoCount = 0;
                string? primaryIssueType = null;
                var maxIssueCount = 0;

                foreach (var analysis in result.AnalysisResults)
                {
                    if (!analysis.HasFindings) continue;

                    var analyzerName = analysis.AnalyzerName;
                    var issueCount = analysis.HasFindings ? 1 : 0;

                    if (issuesByType.ContainsKey(analyzerName))
                        issuesByType[analyzerName] += issueCount;
                    else
                        issuesByType[analyzerName] = issueCount;

                    if (issueCount > maxIssueCount)
                    {
                        maxIssueCount = issueCount;
                        primaryIssueType = analyzerName;
                    }

                    // Since AnalysisResult doesn't have severity levels, count all findings as warnings
                    if (analysis.HasFindings)
                        warningCount++;
                }

                var statistics = new ScanStatistics
                {
                    Timestamp = DateTime.Now,
                    LogFilePath = result.LogPath,
                    GameType = DetectGameType(result.LogPath),
                    TotalIssuesFound = criticalCount + warningCount + infoCount,
                    CriticalIssues = criticalCount,
                    WarningIssues = warningCount,
                    InfoIssues = infoCount,
                    ProcessingTime = processingTime / results.Count, // Average time per file
                    WasSolved = criticalCount == 0,
                    PrimaryIssueType = primaryIssueType,
                    IssuesByType = issuesByType
                };

                await _statisticsService.RecordScanAsync(statistics);
            }
        }
        catch (Exception ex)
        {
            _messageHandler.ShowDebug($"Failed to record statistics: {ex.Message}");
        }
    }

    private async Task TrackRecentFilesAsync(List<string> files, CliScanOptions options)
    {
        if (_recentItemsService == null) return;

        try
        {
            foreach (var file in files) _recentItemsService.AddRecentLogFile(file);

            if (!string.IsNullOrEmpty(options.GamePath))
                _recentItemsService.AddRecentGamePath(options.GamePath);

            if (!string.IsNullOrEmpty(options.ScanDir))
                _recentItemsService.AddRecentScanDirectory(options.ScanDir);
        }
        catch (Exception ex)
        {
            _messageHandler.ShowDebug($"Failed to track recent files: {ex.Message}");
        }
    }

    private async Task PlayCompletionSoundAsync(List<ScanResult> results)
    {
        if (_audioService == null || !_audioService.IsEnabled) return;

        try
        {
            // Since AnalysisResult doesn't have severity levels, check for errors in report lines
            var hasCriticalIssues = results.Any(r => r.AnalysisResults.Any(ar => ar.Errors.Count > 0));
            var hasAnyIssues = results.Any(r => r.AnalysisResults.Any(ar => ar.HasFindings));

            if (hasCriticalIssues)
                await _audioService.PlayCriticalIssueAsync();
            else if (hasAnyIssues)
                await _audioService.PlayErrorFoundAsync();
            else
                await _audioService.PlayScanCompleteAsync();
        }
        catch (Exception ex)
        {
            _messageHandler.ShowDebug($"Failed to play audio notification: {ex.Message}");
        }
    }

    private string DetectGameType(string logPath)
    {
        var content = File.ReadAllText(logPath).ToLowerInvariant();
        if (content.Contains("fallout4") || content.Contains("f4se"))
            return "Fallout4";
        if (content.Contains("skyrim") || content.Contains("skse"))
            return "Skyrim";
        return "Unknown";
    }
}