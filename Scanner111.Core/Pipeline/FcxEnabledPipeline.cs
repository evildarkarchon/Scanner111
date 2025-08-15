using System.Runtime.CompilerServices;
using Microsoft.Extensions.Logging;
using Scanner111.Core.Analyzers;
using Scanner111.Core.Infrastructure;
using Scanner111.Core.Models;

namespace Scanner111.Core.Pipeline;

/// <summary>
///     Pipeline decorator that adds FCX (File Integrity Check) capabilities
///     to the standard scan pipeline when FCX mode is enabled
/// </summary>
public class FcxEnabledPipeline : IScanPipeline
{
    private readonly FileIntegrityAnalyzer _fileIntegrityAnalyzer;
    private readonly IHashValidationService _hashService;
    private readonly IScanPipeline _innerPipeline;
    private readonly ILogger<FcxEnabledPipeline> _logger;
    private readonly IMessageHandler _messageHandler;
    private readonly IApplicationSettingsService _settingsService;
    private readonly IYamlSettingsProvider _yamlSettings;

    public FcxEnabledPipeline(
        IScanPipeline innerPipeline,
        IApplicationSettingsService settingsService,
        IHashValidationService hashService,
        ILogger<FcxEnabledPipeline> logger,
        IMessageHandler messageHandler,
        IYamlSettingsProvider yamlSettings)
    {
        _innerPipeline = Guard.NotNull(innerPipeline, nameof(innerPipeline));
        _settingsService = Guard.NotNull(settingsService, nameof(settingsService));
        _hashService = Guard.NotNull(hashService, nameof(hashService));
        _logger = Guard.NotNull(logger, nameof(logger));
        _messageHandler = Guard.NotNull(messageHandler, nameof(messageHandler));
        _yamlSettings = Guard.NotNull(yamlSettings, nameof(yamlSettings));

        // Create a dedicated FileIntegrityAnalyzer for FCX pre-checks
        _fileIntegrityAnalyzer = new FileIntegrityAnalyzer(
            _hashService,
            _settingsService,
            _yamlSettings,
            _messageHandler);
    }

    /// <summary>
    ///     Process a single crash log file with optional FCX pre-checks
    /// </summary>
    public async Task<ScanResult> ProcessSingleAsync(string logPath, CancellationToken cancellationToken = default)
    {
        var settings = await _settingsService.LoadSettingsAsync();

        if (!settings.FcxMode)
            // FCX mode disabled, pass through to inner pipeline
            return await _innerPipeline.ProcessSingleAsync(logPath, cancellationToken);

        _logger.LogInformation("FCX mode enabled - running file integrity checks before crash log analysis");

        // Create a synthetic crash log for FCX analysis
        var crashLog = new CrashLog
        {
            FilePath = logPath,
            GamePath = settings.DefaultGamePath
        };

        // Run FCX checks
        var fcxResult = await RunFcxChecksAsync(crashLog, cancellationToken);

        // Process the crash log normally
        var scanResult = await _innerPipeline.ProcessSingleAsync(logPath, cancellationToken);

        // Merge FCX results into the scan result
        if (fcxResult != null) scanResult = MergeFcxResults(scanResult, fcxResult);

        return scanResult;
    }

    /// <summary>
    ///     Process multiple crash logs with FCX pre-checks if enabled
    /// </summary>
    public async IAsyncEnumerable<ScanResult> ProcessBatchAsync(
        IEnumerable<string> logPaths,
        ScanOptions? options = null,
        IProgress<BatchProgress>? progress = null,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        var settings = await _settingsService.LoadSettingsAsync();

        if (!settings.FcxMode)
        {
            // FCX mode disabled, pass through to inner pipeline
            await foreach (var result in _innerPipeline.ProcessBatchAsync(logPaths, options, progress,
                               cancellationToken)) yield return result;
            yield break;
        }

        _logger.LogInformation("FCX mode enabled - running file integrity checks before batch processing");

        // Run FCX checks once for the batch
        var fcxResult = await RunFcxChecksForBatchAsync(cancellationToken);

        // If FCX found critical issues, yield them as a special result
        if (fcxResult != null && fcxResult.GameStatus == GameIntegrityStatus.Critical)
            yield return CreateFcxOnlyScanResult(fcxResult);

        // Continue with normal pipeline processing
        await foreach (var result in _innerPipeline.ProcessBatchAsync(logPaths, options, progress, cancellationToken))
            // Add FCX summary to each result if there were issues
            if (fcxResult != null && fcxResult.HasFindings)
            {
                var mergedResult = MergeFcxResults(result, fcxResult);
                yield return mergedResult;
            }
            else
            {
                yield return result;
            }
    }

    /// <summary>
    ///     Dispose of resources
    /// </summary>
    public async ValueTask DisposeAsync()
    {
        await _innerPipeline.DisposeAsync();
    }

    /// <summary>
    ///     Run FCX checks for a single crash log
    /// </summary>
    private async Task<FcxScanResult?> RunFcxChecksAsync(CrashLog crashLog, CancellationToken cancellationToken)
    {
        try
        {
            _messageHandler.ShowInfo("Running FCX file integrity checks...");

            var result = await _fileIntegrityAnalyzer.AnalyzeAsync(crashLog, cancellationToken);

            if (result is FcxScanResult fcxResult)
            {
                LogFcxResults(fcxResult);
                return fcxResult;
            }

            return null;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error running FCX checks");
            _messageHandler.ShowWarning($"FCX check failed: {ex.Message}");
            return null;
        }
    }

    /// <summary>
    ///     Run FCX checks once for a batch operation
    /// </summary>
    private async Task<FcxScanResult?> RunFcxChecksForBatchAsync(CancellationToken cancellationToken)
    {
        try
        {
            _messageHandler.ShowInfo("Running FCX file integrity checks for batch...");

            // Create a synthetic crash log for FCX analysis
            var settings = await _settingsService.LoadSettingsAsync();
            var crashLog = new CrashLog
            {
                GamePath = settings.DefaultGamePath
            };

            var result = await _fileIntegrityAnalyzer.AnalyzeAsync(crashLog, cancellationToken);

            if (result is FcxScanResult fcxResult)
            {
                LogFcxResults(fcxResult);
                return fcxResult;
            }

            return null;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error running FCX batch checks");
            _messageHandler.ShowWarning($"FCX batch check failed: {ex.Message}");
            return null;
        }
    }

    /// <summary>
    ///     Create a scan result that only contains FCX findings
    /// </summary>
    private ScanResult CreateFcxOnlyScanResult(FcxScanResult fcxResult)
    {
        return new ScanResult
        {
            LogPath = "FCX_CHECK",
            Status = ScanStatus.CompletedWithErrors,
            CrashLog = null,
            AnalysisResults = { fcxResult },
            ProcessingTime = TimeSpan.Zero
        };
    }

    /// <summary>
    ///     Merge FCX results into an existing scan result
    /// </summary>
    private ScanResult MergeFcxResults(ScanResult scanResult, FcxScanResult fcxResult)
    {
        // Create a copy to avoid modifying the original
        var mergedResult = new ScanResult
        {
            LogPath = scanResult.LogPath,
            Status = scanResult.Status,
            CrashLog = scanResult.CrashLog,
            ProcessingTime = scanResult.ProcessingTime
        };

        // Copy existing analysis results
        foreach (var analysis in scanResult.AnalysisResults) mergedResult.AnalysisResults.Add(analysis);

        // Copy existing error messages
        foreach (var error in scanResult.ErrorMessages) mergedResult.ErrorMessages.Add(error);

        // Add FCX result to the beginning of the analysis list
        mergedResult.AnalysisResults.Insert(0, fcxResult);

        // If FCX found critical issues, add a warning
        if (fcxResult.GameStatus == GameIntegrityStatus.Critical)
            mergedResult.ErrorMessages.Insert(0,
                "⚠️ FCX detected critical game integrity issues that may affect crash analysis accuracy");

        return mergedResult;
    }

    /// <summary>
    ///     Log FCX results for debugging
    /// </summary>
    private void LogFcxResults(FcxScanResult fcxResult)
    {
        _logger.LogInformation("FCX scan completed - Status: {Status}", fcxResult.GameStatus);

        if (fcxResult.HasFindings)
            _logger.LogWarning("FCX found {IssueCount} issues",
                fcxResult.FileChecks.Count(fc => !fc.IsValid));

        switch (fcxResult.GameStatus)
        {
            case GameIntegrityStatus.Critical:
                _messageHandler.ShowError("FCX: Critical game integrity issues detected!");
                break;
            case GameIntegrityStatus.Warning:
                _messageHandler.ShowWarning("FCX: Minor game integrity issues detected.");
                break;
            case GameIntegrityStatus.Good:
                _messageHandler.ShowSuccess("FCX: Game integrity checks passed.");
                break;
        }
    }
}