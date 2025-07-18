using System.Collections.Concurrent;

namespace Scanner111.Core.Pipeline;

/// <summary>
/// Detailed progress information for scan operations
/// </summary>
public class DetailedProgress : IProgress<DetailedProgressInfo>
{
    private readonly IProgress<DetailedProgressInfo>? _innerProgress;
    private readonly ConcurrentDictionary<string, AnalyzerProgress> _analyzerProgress = new();
    private DetailedProgressInfo _currentProgress = new();

    public DetailedProgress(IProgress<DetailedProgressInfo>? innerProgress = null)
    {
        _innerProgress = innerProgress;
    }

    public void Report(DetailedProgressInfo value)
    {
        _currentProgress = value;
        _innerProgress?.Report(value);
    }

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

    private static string GetKey(string analyzerName, string filePath) 
        => $"{analyzerName}:{filePath}";
}

/// <summary>
/// Detailed progress information
/// </summary>
public record DetailedProgressInfo
{
    // Overall progress
    public int TotalFiles { get; init; }
    public int ProcessedFiles { get; init; }
    public int SuccessfulFiles { get; init; }
    public int FailedFiles { get; init; }
    public double ProgressPercentage => TotalFiles > 0 ? (ProcessedFiles * 100.0) / TotalFiles : 0;
    
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
    public long MemoryUsageMB { get; init; }
}

/// <summary>
/// Progress for individual analyzer
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
/// Status of file processing
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
/// Status of analyzer execution
/// </summary>
public enum AnalyzerStatus
{
    Pending,
    Running,
    Completed,
    Failed,
    Skipped
}