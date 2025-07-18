using Scanner111.Core.Analyzers;

namespace Scanner111.Core.Models;

/// <summary>
/// Result of analyzing a crash log
/// </summary>
public class ScanResult
{
    /// <summary>
    /// Path to the original crash log file
    /// </summary>
    public required string LogPath { get; init; }
    
    /// <summary>
    /// Status of the scan
    /// </summary>
    public ScanStatus Status { get; set; }
    
    /// <summary>
    /// The parsed crash log
    /// </summary>
    public CrashLog? CrashLog { get; set; }
    
    /// <summary>
    /// Analysis results from each analyzer
    /// </summary>
    public List<AnalysisResult> AnalysisResults { get; init; } = new();
    
    /// <summary>
    /// Generated report lines
    /// </summary>
    public List<string> Report { get; init; } = new();
    
    /// <summary>
    /// Errors encountered during the scan
    /// </summary>
    public List<string> ErrorMessages { get; init; } = new();
    
    /// <summary>
    /// True if the scan failed due to an error
    /// </summary>
    public bool Failed => Status == ScanStatus.Failed;
    
    /// <summary>
    /// True if there were any errors
    /// </summary>
    public bool HasErrors => ErrorMessages.Any();
    
    /// <summary>
    /// Time taken to process
    /// </summary>
    public TimeSpan ProcessingTime { get; set; }
    
    /// <summary>
    /// Statistics about the scan operation
    /// </summary>
    public ScanStatistics Statistics { get; init; } = new();
    
    /// <summary>
    /// Full report text as a single string
    /// </summary>
    public string ReportText => string.Join("", Report);
    
    /// <summary>
    /// Suggested output path for the AUTOSCAN report
    /// </summary>
    public string OutputPath => Path.ChangeExtension(LogPath, null) + "-AUTOSCAN.md";
    
    /// <summary>
    /// Add an analysis result to this scan result
    /// </summary>
    public void AddAnalysisResult(AnalysisResult result)
    {
        AnalysisResults.Add(result);
        if (result.ReportLines.Any())
        {
            Report.AddRange(result.ReportLines);
        }
    }
    
    /// <summary>
    /// Add an error message
    /// </summary>
    public void AddError(string error)
    {
        ErrorMessages.Add(error);
    }
}

/// <summary>
/// Status of a scan operation
/// </summary>
public enum ScanStatus
{
    /// <summary>
    /// Scan is pending
    /// </summary>
    Pending,
    
    /// <summary>
    /// Scan is in progress
    /// </summary>
    InProgress,
    
    /// <summary>
    /// Scan completed successfully
    /// </summary>
    Completed,
    
    /// <summary>
    /// Scan completed with some errors
    /// </summary>
    CompletedWithErrors,
    
    /// <summary>
    /// Scan failed
    /// </summary>
    Failed,
    
    /// <summary>
    /// Scan was cancelled
    /// </summary>
    Cancelled
}

/// <summary>
/// Statistics dictionary for scan operations
/// </summary>
public class ScanStatistics : Dictionary<string, int>
{
    /// <summary>
    /// Initialize with default counters
    /// </summary>
    public ScanStatistics()
    {
        this["scanned"] = 0;
        this["incomplete"] = 0;
        this["failed"] = 0;
    }
    
    /// <summary>
    /// Number of files scanned
    /// </summary>
    public int Scanned
    {
        get => this["scanned"];
        set => this["scanned"] = value;
    }
    
    /// <summary>
    /// Number of incomplete crash logs
    /// </summary>
    public int Incomplete
    {
        get => this["incomplete"];
        set => this["incomplete"] = value;
    }
    
    /// <summary>
    /// Number of failed scans
    /// </summary>
    public int Failed
    {
        get => this["failed"];
        set => this["failed"] = value;
    }
    
    /// <summary>
    /// Increment a counter by 1
    /// </summary>
    public void Increment(string key)
    {
        if (ContainsKey(key))
            this[key]++;
        else
            this[key] = 1;
    }
}