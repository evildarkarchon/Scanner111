using Scanner111.Common.Models.Orchestration;

namespace Scanner111.Common.Services.Orchestration;

/// <summary>
/// Collects and organizes crash logs based on analysis results.
/// Supports moving unsolved logs to backup directories for later review.
/// </summary>
public interface ILogCollector
{
    /// <summary>
    /// Gets or sets the configuration for log collection.
    /// </summary>
    LogCollectorConfiguration Configuration { get; set; }

    /// <summary>
    /// Moves an unsolved crash log and its associated report to the backup directory.
    /// </summary>
    /// <param name="crashLogPath">The path to the crash log file.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The result of the move operation.</returns>
    Task<LogCollectionResult> MoveUnsolvedLogAsync(
        string crashLogPath,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Moves multiple unsolved crash logs and their associated reports to the backup directory.
    /// </summary>
    /// <param name="crashLogPaths">The paths to the crash log files.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The combined result of all move operations.</returns>
    Task<LogCollectionResult> MoveUnsolvedLogsAsync(
        IEnumerable<string> crashLogPaths,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets the backup path for a given crash log.
    /// </summary>
    /// <param name="crashLogPath">The path to the crash log file.</param>
    /// <returns>The full path where the log would be moved.</returns>
    string GetBackupPath(string crashLogPath);

    /// <summary>
    /// Gets the associated report path for a crash log.
    /// </summary>
    /// <param name="crashLogPath">The path to the crash log file.</param>
    /// <returns>The expected path of the autoscan report file.</returns>
    string GetReportPath(string crashLogPath);
}
