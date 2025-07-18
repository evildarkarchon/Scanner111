using System.Runtime.CompilerServices;
using Scanner111.Core.Models;

namespace Scanner111.Core.Pipeline;

public interface IScanPipeline : IAsyncDisposable
{
    /// <summary>
    /// Process a single crash log file
    /// </summary>
    Task<ScanResult> ProcessSingleAsync(string logPath, CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Process multiple crash logs with parallelism
    /// </summary>
    IAsyncEnumerable<ScanResult> ProcessBatchAsync(
        IEnumerable<string> logPaths,
        ScanOptions? options = null,
        IProgress<BatchProgress>? progress = null,
        CancellationToken cancellationToken = default);
}

public class ScanOptions
{
    public int MaxConcurrency { get; init; } = Environment.ProcessorCount;
    public bool PreserveOrder { get; init; } = false;
    public int? MaxDegreeOfParallelism { get; init; }
    public bool EnableCaching { get; init; } = true;
    public TimeSpan? Timeout { get; init; }
}

public class BatchProgress
{
    public int TotalFiles { get; init; }
    public int ProcessedFiles { get; init; }
    public int SuccessfulScans { get; init; }
    public int FailedScans { get; init; }
    public int IncompleteScans { get; init; }
    public string CurrentFile { get; init; } = string.Empty;
    public double ProgressPercentage => TotalFiles > 0 ? (ProcessedFiles * 100.0) / TotalFiles : 0;
    public TimeSpan ElapsedTime { get; init; }
    public TimeSpan? EstimatedTimeRemaining { get; init; }
}