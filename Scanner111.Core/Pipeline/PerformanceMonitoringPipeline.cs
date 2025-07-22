using System.Diagnostics;
using System.Runtime.CompilerServices;
using Microsoft.Extensions.Logging;
using Scanner111.Core.Models;

namespace Scanner111.Core.Pipeline;

/// <summary>
/// Implements a performance monitoring decorator for an existing scan pipeline.
/// Tracks and logs performance metrics for single and batch scan processing operations.
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

    /// <summary>
    /// Processes a single log file asynchronously, while tracking performance metrics.
    /// </summary>
    /// <param name="logPath">
    /// The path to the log file that needs to be processed.
    /// </param>
    /// <param name="cancellationToken">
    /// An optional token to cancel the operation.
    /// </param>
    /// <returns>
    /// A task that represents the asynchronous operation, containing the result of the scan as a <see cref="ScanResult"/> object.
    /// </returns>
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

    /// <summary>
    /// Processes a batch of log files asynchronously, while monitoring performance metrics for the entire batch and individual files.
    /// </summary>
    /// <param name="logPaths">
    /// The collection of paths to the log files that need to be processed.
    /// </param>
    /// <param name="options">
    /// Optional settings for controlling the scan process.
    /// </param>
    /// <param name="progress">
    /// Optional progress reporter for batch processing updates.
    /// </param>
    /// <param name="cancellationToken">
    /// An optional token to cancel the batch processing operation.
    /// </param>
    /// <returns>
    /// An asynchronous stream of <see cref="ScanResult"/> containing the result of processing each log file.
    /// </returns>
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

    /// <summary>
    /// Asynchronously disposes of resources used by the performance monitoring pipeline and its inner pipeline.
    /// </summary>
    /// <returns>
    /// A task that represents the asynchronous dispose operation.
    /// </returns>
    public async ValueTask DisposeAsync()
    {
        await _innerPipeline.DisposeAsync();
        GC.SuppressFinalize(this);
    }

    /// <summary>
    /// Records performance metrics for a specified log file.
    /// </summary>
    /// <param name="logPath">
    /// The path to the log file for which performance metrics are being recorded.
    /// </param>
    /// <param name="metrics">
    /// The performance metrics associated with the processing of the log file.
    /// </param>
    private void RecordMetrics(string logPath, PerformanceMetrics metrics)
    {
        _metrics[logPath] = metrics;

        _logger.LogInformation(
            "Processed {LogPath} in {Duration:F2}s (Success: {Success})",
            logPath,
            metrics.TotalDuration.TotalSeconds,
            metrics.Success);
    }

    /// <summary>
    /// Logs performance metrics for a batch processing operation, including per-file metrics and overall batch summary.
    /// </summary>
    /// <param name="batchMetrics">
    /// Metrics summarizing the performance of the batch operation, including timing and success rates.
    /// </param>
    /// <param name="fileMetrics">
    /// A dictionary containing performance metrics for individual files processed in the batch.
    /// </param>
    private void LogBatchMetrics(BatchPerformanceMetrics batchMetrics,
        Dictionary<string, PerformanceMetrics> fileMetrics)
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
            _logger.LogDebug(
                "Slowest file: {File} took {Duration:F2}s",
                Path.GetFileName(file),
                metrics.TotalDuration.TotalSeconds);
    }

    /// <summary>
    /// Retrieves the performance metrics collected during scanning operations.
    /// </summary>
    /// <returns>
    /// An immutable dictionary where the keys represent file identifiers, and the values are the corresponding <see cref="PerformanceMetrics"/> objects containing performance data.
    /// </returns>
    public IReadOnlyDictionary<string, PerformanceMetrics> GetMetrics()
    {
        return _metrics;
    }
}

/// <summary>
/// Represents detailed performance metrics for processing a single file within the scan pipeline.
/// Captures key timing details, success state, error information, and performance breakdowns by analysis stage.
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
/// Represents performance metrics collected during a batch processing operation.
/// Tracks the start and end times, duration, count of processed files, successes, failures,
/// and average processing time per file.
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