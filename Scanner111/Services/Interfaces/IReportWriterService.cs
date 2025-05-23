using System.Collections.Generic;
using System.Threading.Tasks;
using Scanner111.Models;

namespace Scanner111.Services.Interfaces;

/// <summary>
///     Service for writing scan results to files.
/// </summary>
public interface IReportWriterService
{
    /// <summary>
    ///     Writes a crash log scan report to file.
    /// </summary>
    /// <param name="logFileName">Name of the crash log file.</param>
    /// <param name="report">The scan report lines.</param>
    /// <param name="scanFailed">Whether the scan failed.</param>
    /// <returns>A task representing the write operation.</returns>
    Task WriteReportToFileAsync(string logFileName, List<string> report, bool scanFailed);

    /// <summary>
    ///     Writes a combined results report.
    /// </summary>
    /// <param name="results">All scan results.</param>
    /// <param name="statistics">Scan statistics.</param>
    /// <returns>A task representing the write operation.</returns>
    Task WriteCombinedResultsAsync(List<CrashLogProcessResult> results, ScanStatistics statistics);
}