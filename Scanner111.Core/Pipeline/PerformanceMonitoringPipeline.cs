using System.Diagnostics;
using System.Runtime.CompilerServices;
using Microsoft.Extensions.Logging;
using Scanner111.Core.Models;

namespace Scanner111.Core.Pipeline;

/// <summary>
/// Decorator that adds performance monitoring to a scan pipeline
/// </summary>
public class PerformanceMonitoringPipeline : IScanPipeline
{
    private readonly IScanPipeline _innerPipeline;
    private readonly ILogger _logger;
    private readonly Dictionary<string, PerformanceMetrics> _metrics = new();

    public PerformanceMonitoringPipeline(IScanPipeline innerPipeline, ILogger logger)
    {
        _innerPipeline = innerPipeline;
        _logger = logger;
    }

    public async Task<ScanResult> ProcessSingleAsync(string logPath, CancellationToken cancellationToken = default)
    {
        var stopwatch = Stopwatch.StartNew();
        var metrics = new PerformanceMetrics { StartTime = DateTime.UtcNow };

        try
        {
            var result = await _innerPipeline.ProcessSingleAsync(logPath, cancellationToken);
            
            metrics.EndTime = DateTime.UtcNow;
            metrics.TotalDuration = stopwatch.Elapsed;
            metrics.Success = !result.Failed;
            
            RecordMetrics(logPath, metrics);
            
            return result;
        }
        catch (Exception ex)
        {
            metrics.EndTime = DateTime.UtcNow;
            metrics.TotalDuration = stopwatch.Elapsed;
            metrics.Success = false;
            metrics.Error = ex.Message;
            
            RecordMetrics(logPath, metrics);
            throw;
        }
    }

    public async IAsyncEnumerable<ScanResult> ProcessBatchAsync(
        IEnumerable<string> logPaths,
        ScanOptions? options = null,
        IProgress<BatchProgress>? progress = null,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        var batchStopwatch = Stopwatch.StartNew();
        var batchMetrics = new BatchPerformanceMetrics { StartTime = DateTime.UtcNow };
        var fileMetrics = new Dictionary<string, PerformanceMetrics>();

        await foreach (var result in _innerPipeline.ProcessBatchAsync(logPaths, options, progress, cancellationToken))
        {
            batchMetrics.FilesProcessed++;
            
            if (result.Failed)
                batchMetrics.FailedFiles++;
            else
                batchMetrics.SuccessfulFiles++;
            
            fileMetrics[result.LogPath] = new PerformanceMetrics
            {
                StartTime = DateTime.UtcNow.Subtract(result.ProcessingTime),
                EndTime = DateTime.UtcNow,
                TotalDuration = result.ProcessingTime,
                Success = !result.Failed
            };
            
            yield return result;
        }

        batchMetrics.EndTime = DateTime.UtcNow;
        batchMetrics.TotalDuration = batchStopwatch.Elapsed;
        batchMetrics.AverageFileTime = batchMetrics.FilesProcessed > 0
            ? TimeSpan.FromMilliseconds(batchMetrics.TotalDuration.TotalMilliseconds / batchMetrics.FilesProcessed)
            : TimeSpan.Zero;

        LogBatchMetrics(batchMetrics, fileMetrics);
    }

    private void RecordMetrics(string logPath, PerformanceMetrics metrics)
    {
        _metrics[logPath] = metrics;
        
        _logger.LogInformation(
            "Processed {LogPath} in {Duration:F2}s (Success: {Success})",
            logPath,
            metrics.TotalDuration.TotalSeconds,
            metrics.Success);
    }

    private void LogBatchMetrics(BatchPerformanceMetrics batchMetrics, Dictionary<string, PerformanceMetrics> fileMetrics)
    {
        _logger.LogInformation(
            "Batch processing completed: {TotalFiles} files in {Duration:F2}s " +
            "(Success: {Success}, Failed: {Failed}, Avg: {Average:F2}s/file)",
            batchMetrics.FilesProcessed,
            batchMetrics.TotalDuration.TotalSeconds,
            batchMetrics.SuccessfulFiles,
            batchMetrics.FailedFiles,
            batchMetrics.AverageFileTime.TotalSeconds);

        // Log slowest files
        var slowestFiles = fileMetrics
            .OrderByDescending(kvp => kvp.Value.TotalDuration)
            .Take(5);

        foreach (var (file, metrics) in slowestFiles)
        {
            _logger.LogDebug(
                "Slowest file: {File} took {Duration:F2}s",
                Path.GetFileName(file),
                metrics.TotalDuration.TotalSeconds);
        }
    }

    public async ValueTask DisposeAsync()
    {
        await _innerPipeline.DisposeAsync();
    }

    /// <summary>
    /// Get performance metrics for analysis
    /// </summary>
    public IReadOnlyDictionary<string, PerformanceMetrics> GetMetrics() => _metrics;
}

/// <summary>
/// Performance metrics for a single file
/// </summary>
public class PerformanceMetrics
{
    public DateTime StartTime { get; set; }
    public DateTime EndTime { get; set; }
    public TimeSpan TotalDuration { get; set; }
    public bool Success { get; set; }
    public string? Error { get; set; }
    public Dictionary<string, TimeSpan> AnalyzerDurations { get; init; } = new();
}

/// <summary>
/// Performance metrics for a batch operation
/// </summary>
public class BatchPerformanceMetrics
{
    public DateTime StartTime { get; set; }
    public DateTime EndTime { get; set; }
    public TimeSpan TotalDuration { get; set; }
    public int FilesProcessed { get; set; }
    public int SuccessfulFiles { get; set; }
    public int FailedFiles { get; set; }
    public TimeSpan AverageFileTime { get; set; }
}