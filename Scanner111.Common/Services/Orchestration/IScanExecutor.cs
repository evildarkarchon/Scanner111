using Scanner111.Common.Models.Configuration;

namespace Scanner111.Common.Services.Orchestration;

/// <summary>
/// Interface for executing a scan operation on multiple files.
/// </summary>
public interface IScanExecutor
{
    /// <summary>
    /// Executes a scan based on the provided configuration.
    /// </summary>
    /// <param name="config">Scan configuration.</param>
    /// <param name="progress">Optional progress reporter.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>Result of the scan operation.</returns>
    Task<ScanResult> ExecuteScanAsync(
        ScanConfig config,
        IProgress<ScanProgress>? progress = null,
        CancellationToken ct = default);
}

/// <summary>
/// Progress update for a scan operation.
/// </summary>
public record ScanProgress
{
    /// <summary>Gets the number of files processed so far.</summary>
    public int FilesProcessed { get; init; }

    /// <summary>Gets the total number of files to process.</summary>
    public int TotalFiles { get; init; }

    /// <summary>Gets the path of the file currently being processed.</summary>
    public string? CurrentFile { get; init; }

    /// <summary>Gets the cumulative statistics for the scan operation.</summary>
    public ScanStatistics Statistics { get; init; } = null!;
}
