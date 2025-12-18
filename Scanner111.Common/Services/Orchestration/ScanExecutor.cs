using System.Collections.Concurrent;
using System.Diagnostics;
using Microsoft.Extensions.Logging;
using Scanner111.Common.Models.Analysis;
using Scanner111.Common.Models.Configuration;

namespace Scanner111.Common.Services.Orchestration;

/// <summary>
/// Executes scan operations on multiple crash logs concurrently.
/// </summary>
public class ScanExecutor : IScanExecutor
{
    private readonly ILogger<ScanExecutor> _logger;
    private readonly Func<ILogOrchestrator> _orchestratorFactory;

    /// <summary>
    /// Initializes a new instance of the <see cref="ScanExecutor"/> class.
    /// </summary>
    /// <param name="logger">The logger instance.</param>
    /// <param name="orchestratorFactory">Factory to create log orchestrator instances.</param>
    public ScanExecutor(
        ILogger<ScanExecutor> logger,
        Func<ILogOrchestrator> orchestratorFactory)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _orchestratorFactory = orchestratorFactory ?? throw new ArgumentNullException(nameof(orchestratorFactory));
    }

    /// <inheritdoc/>
    public async Task<ScanResult> ExecuteScanAsync(
        ScanConfig config,
        IProgress<ScanProgress>? progress = null,
        CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(config.ScanPath))
        {
            throw new ArgumentException("Scan path must be specified in configuration.", nameof(config));
        }

        if (!Directory.Exists(config.ScanPath))
        {
            throw new DirectoryNotFoundException($"Scan path not found: {config.ScanPath}");
        }

        _logger.LogInformation("Starting batch scan in '{ScanPath}'", config.ScanPath);

        // Discover files
        var logFiles = Directory.GetFiles(config.ScanPath, "crash-*.log", SearchOption.TopDirectoryOnly);

        var totalFiles = logFiles.Length;
        _logger.LogInformation("Discovered {FileCount} crash log files", totalFiles);

        if (totalFiles == 0)
        {
            _logger.LogWarning("No crash log files found in '{ScanPath}'", config.ScanPath);
        }

        var startTime = DateTime.UtcNow;
        var stopwatch = Stopwatch.StartNew();

        var processedCount = 0;
        var failedLogs = new ConcurrentBag<string>();
        var processedFiles = new ConcurrentBag<string>();
        var errorMessages = new ConcurrentBag<string>();

        // Set up concurrency control
        using var semaphore = new SemaphoreSlim(config.MaxConcurrent, config.MaxConcurrent);
        _logger.LogDebug("Concurrency limit set to {MaxConcurrent}", config.MaxConcurrent);

        var tasks = new List<Task>();

        foreach (var logFile in logFiles)
        {
            tasks.Add(ProcessLogWithSemaphoreAsync(
                logFile,
                config,
                semaphore,
                failedLogs,
                processedFiles,
                errorMessages,
                () =>
                {
                    var count = Interlocked.Increment(ref processedCount);
                    progress?.Report(new ScanProgress
                    {
                        FilesProcessed = count,
                        TotalFiles = totalFiles,
                        CurrentFile = Path.GetFileName(logFile),
                        Statistics = new ScanStatistics
                        {
                            Scanned = processedFiles.Count,
                            Failed = failedLogs.Count,
                            TotalFiles = totalFiles,
                            ScanStartTime = startTime
                        }
                    });
                },
                ct));
        }

        await Task.WhenAll(tasks).ConfigureAwait(false);

        stopwatch.Stop();
        var failedCount = failedLogs.Count;

        if (failedCount > 0)
        {
            _logger.LogWarning("Batch scan completed: {ProcessedCount} processed, {FailedCount} failed in {Duration:F2}s",
                processedFiles.Count, failedCount, stopwatch.Elapsed.TotalSeconds);
        }
        else
        {
            _logger.LogInformation("Batch scan completed: {ProcessedCount} files processed in {Duration:F2}s",
                processedFiles.Count, stopwatch.Elapsed.TotalSeconds);
        }

        return new ScanResult
        {
            Statistics = new ScanStatistics
            {
                Scanned = processedFiles.Count,
                Failed = failedCount,
                TotalFiles = totalFiles,
                ScanStartTime = startTime
            },
            FailedLogs = failedLogs.ToList(),
            ProcessedFiles = processedFiles.ToList(),
            ErrorMessages = errorMessages.ToList(),
            ScanDuration = DateTime.UtcNow - startTime
        };
    }

    private async Task ProcessLogWithSemaphoreAsync(
        string logFile,
        ScanConfig config,
        SemaphoreSlim semaphore,
        ConcurrentBag<string> failedLogs,
        ConcurrentBag<string> processedFiles,
        ConcurrentBag<string> errorMessages,
        Action onProcessed,
        CancellationToken ct)
    {
        await semaphore.WaitAsync(ct).ConfigureAwait(false);
        var fileName = Path.GetFileName(logFile);

        try
        {
            var orchestrator = _orchestratorFactory();
            var result = await orchestrator.ProcessLogAsync(logFile, config, ct).ConfigureAwait(false);
            processedFiles.Add(logFile);

            // Check for warnings or issues that might count as "failure" or just track valid scans
            // Here we assume if ProcessLogAsync returns, it's "Scanned".
            // If it threw exception, it would be caught below.
            // However, LogOrchestrator catches parsing errors and returns valid object with Warnings.
        }
        catch (Exception ex)
        {
            failedLogs.Add(logFile);
            errorMessages.Add($"Error processing {fileName}: {ex.Message}");
            _logger.LogError(ex, "Error processing crash log '{FileName}'", fileName);
        }
        finally
        {
            semaphore.Release();
            onProcessed();
        }
    }
}
