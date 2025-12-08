using System.Collections.Concurrent;
using Scanner111.Common.Models.Analysis;
using Scanner111.Common.Models.Configuration;

namespace Scanner111.Common.Services.Orchestration;

/// <summary>
/// Executes scan operations on multiple crash logs concurrently.
/// </summary>
public class ScanExecutor : IScanExecutor
{
    private readonly ILogOrchestrator _orchestrator;

    /// <summary>
    /// Initializes a new instance of the <see cref="ScanExecutor"/> class.
    /// </summary>
    /// <param name="orchestrator">The log orchestrator service.</param>
    public ScanExecutor(ILogOrchestrator orchestrator)
    {
        _orchestrator = orchestrator ?? throw new ArgumentNullException(nameof(orchestrator));
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

        // Discover files
        var logFiles = Directory.GetFiles(config.ScanPath, "crash-*.log", SearchOption.TopDirectoryOnly);
        
        var totalFiles = logFiles.Length;
        var startTime = DateTime.UtcNow;
        
        var processedCount = 0;
        var failedLogs = new ConcurrentBag<string>();
        var processedFiles = new ConcurrentBag<string>();
        var errorMessages = new ConcurrentBag<string>();

        // Set up concurrency control
        var semaphore = new SemaphoreSlim(config.MaxConcurrent, config.MaxConcurrent);
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

        await Task.WhenAll(tasks);

        return new ScanResult
        {
            Statistics = new ScanStatistics
            {
                Scanned = processedFiles.Count,
                Failed = failedLogs.Count,
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
        await semaphore.WaitAsync(ct);
        try
        {
            var result = await _orchestrator.ProcessLogAsync(logFile, config, ct);
            processedFiles.Add(logFile);
            
            // Check for warnings or issues that might count as "failure" or just track valid scans
            // Here we assume if ProcessLogAsync returns, it's "Scanned".
            // If it threw exception, it would be caught below.
            // However, LogOrchestrator catches parsing errors and returns valid object with Warnings.
        }
        catch (Exception ex)
        {
            failedLogs.Add(logFile);
            errorMessages.Add($"Error processing {Path.GetFileName(logFile)}: {ex.Message}");
        }
        finally
        {
            semaphore.Release();
            onProcessed();
        }
    }
}
