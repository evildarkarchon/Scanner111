namespace Scanner111.Common.Models.Configuration;

/// <summary>
/// Represents the overall results of a crash log scanning operation.
/// </summary>
public record ScanResult
{
    /// <summary>
    /// Gets the statistics for the completed scan.
    /// </summary>
    public ScanStatistics Statistics { get; init; } = null!;

    /// <summary>
    /// Gets the list of log files that failed to process.
    /// </summary>
    public IReadOnlyList<string> FailedLogs { get; init; } = Array.Empty<string>();

    /// <summary>
    /// Gets the total duration of the scan operation.
    /// </summary>
    public TimeSpan ScanDuration { get; init; }

    /// <summary>
    /// Gets the list of files that were successfully processed.
    /// </summary>
    public IReadOnlyList<string> ProcessedFiles { get; init; } = Array.Empty<string>();

    /// <summary>
    /// Gets the list of error messages encountered during the scan.
    /// </summary>
    public IReadOnlyList<string> ErrorMessages { get; init; } = Array.Empty<string>();
}
