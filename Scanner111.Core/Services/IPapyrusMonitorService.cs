using Scanner111.Core.Models;

namespace Scanner111.Core.Services;

/// <summary>
///     Service for monitoring and analyzing Papyrus log files
/// </summary>
public interface IPapyrusMonitorService : IDisposable
{
    /// <summary>
    ///     Gets whether the service is currently monitoring
    /// </summary>
    bool IsMonitoring { get; }

    /// <summary>
    ///     Gets the current statistics (null if not monitoring)
    /// </summary>
    PapyrusStats? CurrentStats { get; }

    /// <summary>
    ///     Gets the path being monitored
    /// </summary>
    string? MonitoredPath { get; }

    /// <summary>
    ///     Gets or sets the monitoring interval in milliseconds
    /// </summary>
    int MonitoringInterval { get; set; }

    /// <summary>
    ///     Event raised when Papyrus statistics are updated
    /// </summary>
    event EventHandler<PapyrusStatsUpdatedEventArgs>? StatsUpdated;

    /// <summary>
    ///     Event raised when an error occurs during monitoring
    /// </summary>
    event EventHandler<ErrorEventArgs>? Error;

    /// <summary>
    ///     Starts monitoring the Papyrus log file
    /// </summary>
    /// <param name="logPath">Path to the Papyrus.0.log file</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Task representing the monitoring operation</returns>
    Task StartMonitoringAsync(string logPath, CancellationToken cancellationToken = default);

    /// <summary>
    ///     Starts monitoring with auto-detection of log path
    /// </summary>
    /// <param name="gameType">The game type to detect log path for</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Task representing the monitoring operation</returns>
    Task StartMonitoringAsync(GameType gameType, CancellationToken cancellationToken = default);

    /// <summary>
    ///     Stops monitoring the Papyrus log file
    /// </summary>
    Task StopMonitoringAsync();

    /// <summary>
    ///     Analyzes a Papyrus log file once without continuous monitoring
    /// </summary>
    /// <param name="logPath">Path to the Papyrus.0.log file</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>The statistics extracted from the log</returns>
    Task<PapyrusStats> AnalyzeLogAsync(string logPath, CancellationToken cancellationToken = default);

    /// <summary>
    ///     Gets historical statistics for the current monitoring session
    /// </summary>
    /// <returns>List of historical statistics</returns>
    IReadOnlyList<PapyrusStats> GetHistoricalStats();

    /// <summary>
    ///     Clears historical statistics
    /// </summary>
    void ClearHistory();

    /// <summary>
    ///     Attempts to auto-detect the Papyrus log path for a game
    /// </summary>
    /// <param name="gameType">The game type</param>
    /// <returns>The detected path or null if not found</returns>
    Task<string?> DetectLogPathAsync(GameType gameType);

    /// <summary>
    ///     Exports statistics to a file
    /// </summary>
    /// <param name="filePath">Path to export file</param>
    /// <param name="format">Export format (csv or json)</param>
    /// <param name="cancellationToken">Cancellation token</param>
    Task ExportStatsAsync(string filePath, string format = "csv", CancellationToken cancellationToken = default);
}