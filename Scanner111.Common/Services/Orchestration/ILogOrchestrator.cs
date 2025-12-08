using Scanner111.Common.Models.Analysis;
using Scanner111.Common.Models.Reporting;
using Scanner111.Common.Models.Configuration;

namespace Scanner111.Common.Services.Orchestration;

/// <summary>
/// Interface for orchestrating the analysis of a single crash log.
/// </summary>
public interface ILogOrchestrator
{
    /// <summary>
    /// Processes a single crash log file through the analysis pipeline.
    /// </summary>
    /// <param name="logFilePath">Path to the crash log file.</param>
    /// <param name="config">Scan configuration.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>Result of the analysis.</returns>
    Task<LogAnalysisResult> ProcessLogAsync(
        string logFilePath,
        ScanConfig config,
        CancellationToken ct = default);
}
