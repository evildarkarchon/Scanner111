using Scanner111.Common.Models.Papyrus;

namespace Scanner111.Common.Services.Papyrus;

/// <summary>
/// Provides background monitoring of Papyrus log files.
/// </summary>
public interface IPapyrusMonitorService : IDisposable
{
    /// <summary>
    /// Event raised when statistics are updated.
    /// </summary>
    /// <remarks>
    /// Raised only when statistics change from the previous read.
    /// Only includes data written after monitoring started.
    /// </remarks>
    event Action<PapyrusStats>? StatsUpdated;

    /// <summary>
    /// Event raised when an error occurs during monitoring.
    /// </summary>
    event Action<string>? ErrorOccurred;

    /// <summary>
    /// Gets whether monitoring is currently active.
    /// </summary>
    bool IsMonitoring { get; }

    /// <summary>
    /// Starts monitoring the Papyrus log file.
    /// </summary>
    /// <param name="logPath">The path to the Papyrus.0.log file.</param>
    /// <param name="pollIntervalMs">Polling interval in milliseconds (default: 1000).</param>
    /// <remarks>
    /// Only tracks new entries written after this method is called.
    /// </remarks>
    void StartMonitoring(string logPath, int pollIntervalMs = 1000);

    /// <summary>
    /// Stops monitoring.
    /// </summary>
    void StopMonitoring();
}
