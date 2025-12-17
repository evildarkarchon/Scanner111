namespace Scanner111.Common.Models.ScanGame;

/// <summary>
/// Represents aggregate progress information during a ScanGame operation.
/// </summary>
/// <param name="CurrentOperation">Description of the current operation.</param>
/// <param name="OverallPercentComplete">Overall progress percentage (0-100).</param>
/// <param name="ScannersCompleted">Number of scanner operations completed.</param>
/// <param name="TotalScanners">Total number of scanner operations to run.</param>
public record ScanGameProgress(
    string CurrentOperation,
    int OverallPercentComplete,
    int ScannersCompleted,
    int TotalScanners)
{
    /// <summary>
    /// Creates a progress instance for the starting state.
    /// </summary>
    /// <param name="totalScanners">Total number of scanners to run.</param>
    /// <returns>A new progress instance indicating the scan is starting.</returns>
    public static ScanGameProgress Starting(int totalScanners) =>
        new("Starting scan...", 0, 0, totalScanners);

    /// <summary>
    /// Creates a progress instance for the completion state.
    /// </summary>
    /// <param name="totalScanners">Total number of scanners that were run.</param>
    /// <returns>A new progress instance indicating the scan is complete.</returns>
    public static ScanGameProgress Completed(int totalScanners) =>
        new("Scan complete", 100, totalScanners, totalScanners);
}
