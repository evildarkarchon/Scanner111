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
    public int FilesProcessed { get; init; }
    public int TotalFiles { get; init; }
    public string? CurrentFile { get; init; }
    public ScanStatistics Statistics { get; init; } = null!;
}
