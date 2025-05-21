using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Scanner111.Models;

namespace Scanner111.Services
{
    /// <summary>
    /// Interface for the Papyrus log monitoring service.
    /// Handles monitoring and analyzing Papyrus log files.
    /// </summary>
    public interface IPapyrusLogMonitoringService
    {
        /// <summary>
        /// Analyzes the Papyrus log file and extracts statistics
        /// </summary>
        /// <returns>PapyrusLogAnalysis object with analysis results</returns>
        Task<PapyrusLogAnalysis> AnalyzePapyrusLogAsync();

        /// <summary>
        /// Starts asynchronous monitoring of the Papyrus log file
        /// </summary>
        /// <param name="callback">Action to call when log file changes</param>
        /// <param name="cancellationToken">Token to cancel monitoring</param>
        /// <returns>Task representing the monitoring operation</returns>
        Task StartMonitoringAsync(Action<PapyrusLogAnalysis> callback, CancellationToken cancellationToken);

        /// <summary>
        /// Stops the monitoring of the Papyrus log file
        /// </summary>
        void StopMonitoring();

        /// <summary>
        /// Gets the current path to the Papyrus log file
        /// </summary>
        /// <returns>Path to the Papyrus log file, or null if not configured</returns>
        string? GetPapyrusLogPath();
    }
}
