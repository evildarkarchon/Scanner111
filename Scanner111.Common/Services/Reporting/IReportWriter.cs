using Scanner111.Common.Models.Reporting;

namespace Scanner111.Common.Services.Reporting;

/// <summary>
/// Interface for writing crash log analysis reports.
/// </summary>
public interface IReportWriter
{
    /// <summary>
    /// Writes a report fragment to a markdown file corresponding to the crash log.
    /// </summary>
    /// <param name="crashLogPath">The path to the original crash log file.</param>
    /// <param name="report">The report fragment to write.</param>
    /// <param name="cancellationToken">Cancellation token for the async operation.</param>
    /// <returns>A task representing the async write operation.</returns>
    Task WriteReportAsync(
        string crashLogPath,
        ReportFragment report,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Checks if a report file already exists for a given crash log.
    /// </summary>
    /// <param name="crashLogPath">The path to the crash log file.</param>
    /// <param name="cancellationToken">Cancellation token for the async operation.</param>
    /// <returns>True if the report file exists, false otherwise.</returns>
    Task<bool> ReportExistsAsync(
        string crashLogPath,
        CancellationToken cancellationToken = default);
}
