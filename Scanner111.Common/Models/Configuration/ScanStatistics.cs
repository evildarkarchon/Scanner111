namespace Scanner111.Common.Models.Configuration;

/// <summary>
/// Tracks scan progress and results during crash log analysis.
/// </summary>
public record ScanStatistics
{
    /// <summary>
    /// Gets the number of successfully scanned logs.
    /// </summary>
    public int Scanned { get; init; }

    /// <summary>
    /// Gets the number of incomplete logs (missing required sections like PLUGINS).
    /// </summary>
    public int Incomplete { get; init; }

    /// <summary>
    /// Gets the number of logs that failed to process due to errors.
    /// </summary>
    public int Failed { get; init; }

    /// <summary>
    /// Gets the total number of log files to be processed.
    /// </summary>
    public int TotalFiles { get; init; }

    /// <summary>
    /// Gets the UTC timestamp when the scan started.
    /// </summary>
    public DateTime ScanStartTime { get; init; }

    /// <summary>
    /// Gets the elapsed time since the scan started.
    /// </summary>
    public TimeSpan ElapsedTime => DateTime.UtcNow - ScanStartTime;
}
