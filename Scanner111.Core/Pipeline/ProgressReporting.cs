using System.Collections.Concurrent;

namespace Scanner111.Core.Pipeline;

/// <summary>
/// Represents detailed progress reporting for scanning operations, capable of capturing file and analyzer states during the process.
/// </summary>
public class DetailedProgress : IProgress<DetailedProgressInfo>
{
    private readonly ConcurrentDictionary<string, AnalyzerProgress> _analyzerProgress = new();
    private readonly IProgress<DetailedProgressInfo>? _innerProgress;
    private DetailedProgressInfo _currentProgress = new();

    public DetailedProgress(IProgress<DetailedProgressInfo>? innerProgress = null)
    {
        _innerProgress = innerProgress;
    }

    /// Reports progress information.
    /// <param name="value">The current progress information to report.</param>
    public void Report(DetailedProgressInfo value)
    {
        _currentProgress = value;
        _innerProgress?.Report(value);
    }

    /// Reports the start of processing for a specific file.
    /// <param name="filePath">The path of the file being processed.</param>
    public void ReportFileStart(string filePath)
    {
        var progress = _currentProgress with
        {
            CurrentFile = filePath,
            CurrentFileStatus = FileProcessingStatus.InProgress,
            LastUpdateTime = DateTime.UtcNow
        };
        Report(progress);
    }

    /// Reports the completion status of a file processing operation.
    /// <param name="filePath">The path of the file that was processed.</param>
    /// <param name="success">A boolean indicating whether the file processing was successful.</param>
    public void ReportFileComplete(string filePath, bool success)
    {
        var progress = _currentProgress with
        {
            CurrentFile = filePath,
            CurrentFileStatus = success ? FileProcessingStatus.Completed : FileProcessingStatus.Failed,
            ProcessedFiles = _currentProgress.ProcessedFiles + 1,
            LastUpdateTime = DateTime.UtcNow
        };

        if (success)
            progress = progress with { SuccessfulFiles = _currentProgress.SuccessfulFiles + 1 };
        else
            progress = progress with { FailedFiles = _currentProgress.FailedFiles + 1 };

        Report(progress);
    }

    /// Starts tracking the progress of an analyzer for a specific file.
    /// <param name="analyzerName">The name of the analyzer to track.</param>
    /// <param name="filePath">The path of the file being analyzed.</param>
    public void ReportAnalyzerStart(string analyzerName, string filePath)
    {
        _analyzerProgress[GetKey(analyzerName, filePath)] = new AnalyzerProgress
        {
            AnalyzerName = analyzerName,
            FileName = Path.GetFileName(filePath),
            Status = AnalyzerStatus.Running,
            StartTime = DateTime.UtcNow
        };

        UpdateAnalyzerProgress();
    }

    /// Completes the progress reporting for an analyzer by updating its status,
    /// end time, and duration.
    /// <param name="analyzerName">The name of the analyzer whose progress is being completed.</param>
    /// <param name="filePath">The file that was being analyzed by the specified analyzer.</param>
    /// <param name="success">Indicates whether the analyzer finished successfully or encountered an error.</param>
    public void ReportAnalyzerComplete(string analyzerName, string filePath, bool success)
    {
        var key = GetKey(analyzerName, filePath);
        if (_analyzerProgress.TryGetValue(key, out var progress))
        {
            progress.Status = success ? AnalyzerStatus.Completed : AnalyzerStatus.Failed;
            progress.EndTime = DateTime.UtcNow;
            progress.Duration = progress.EndTime - progress.StartTime;
        }

        UpdateAnalyzerProgress();
    }

    /// Updates the progress information of currently running analyzers for detailed progress tracking.
    /// Simultaneously calculates and reports the current state of active analyzers.
    /// This method updates the list of active analyzers, sorting them by their start time,
    /// and includes the latest updates for progress reporting.
    private void UpdateAnalyzerProgress()
    {
        var analyzerProgresses = _analyzerProgress.Values
            .Where(p => p.Status == AnalyzerStatus.Running)
            .OrderBy(p => p.StartTime)
            .ToList();

        var progress = _currentProgress with
        {
            ActiveAnalyzers = analyzerProgresses,
            LastUpdateTime = DateTime.UtcNow
        };

        Report(progress);
    }

    /// <summary>
    /// Combines the analyzer name and file path into a unique key.
    /// </summary>
    /// <param name="analyzerName">The name of the analyzer.</param>
    /// <param name="filePath">The path of the file being analyzed.</param>
    /// <returns>A string key that uniquely identifies the analyzer and file combination.</returns>
    private static string GetKey(string analyzerName, string filePath)
    {
        return $"{analyzerName}:{filePath}";
    }
}

/// <summary>
/// Represents detailed information about the progress of a scanning operation, including file processing status,
/// analyzer progress, timing, and performance metrics.
/// </summary>
public record DetailedProgressInfo
{
    // Overall progress
    public int TotalFiles { get; init; }
    public int ProcessedFiles { get; init; }
    public int SuccessfulFiles { get; init; }
    public int FailedFiles { get; init; }
    public double ProgressPercentage => TotalFiles > 0 ? ProcessedFiles * 100.0 / TotalFiles : 0;

    // Current file progress
    public string CurrentFile { get; init; } = string.Empty;
    public FileProcessingStatus CurrentFileStatus { get; init; }

    // Analyzer progress
    public IReadOnlyList<AnalyzerProgress> ActiveAnalyzers { get; init; } = Array.Empty<AnalyzerProgress>();

    // Timing
    public DateTime StartTime { get; init; }
    public DateTime LastUpdateTime { get; init; }
    public TimeSpan ElapsedTime => LastUpdateTime - StartTime;
    public TimeSpan? EstimatedTimeRemaining { get; init; }

    // Performance metrics
    public double FilesPerSecond => ElapsedTime.TotalSeconds > 0 ? ProcessedFiles / ElapsedTime.TotalSeconds : 0;
    public double AverageFileTime => ProcessedFiles > 0 ? ElapsedTime.TotalSeconds / ProcessedFiles : 0;

    // Memory usage
    public long MemoryUsageMb { get; init; }
}

/// <summary>
/// Represents the progress of an individual analyzer, capturing details such as the analyzer's name, associated file, status,
/// start time, end time, and duration of execution.
/// </summary>
public class AnalyzerProgress
{
    public string AnalyzerName { get; init; } = string.Empty;
    public string FileName { get; init; } = string.Empty;
    public AnalyzerStatus Status { get; set; }
    public DateTime StartTime { get; init; }
    public DateTime? EndTime { get; set; }
    public TimeSpan? Duration { get; set; }
}

/// <summary>
/// Represents the status of a file during its processing lifecycle.
/// </summary>
public enum FileProcessingStatus
{
    Pending,
    InProgress,
    Completed,
    Failed,
    Cancelled
}

/// <summary>
/// Represents the execution status of an analyzer in a scanning or processing pipeline.
/// </summary>
public enum AnalyzerStatus
{
    Pending,
    Running,
    Completed,
    Failed,
    Skipped
}