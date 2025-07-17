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
    /// Generated report lines
    /// </summary>
    public List<string> Report { get; init; } = new();
    
    /// <summary>
    /// True if the scan failed due to an error
    /// </summary>
    public bool Failed { get; init; }
    
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