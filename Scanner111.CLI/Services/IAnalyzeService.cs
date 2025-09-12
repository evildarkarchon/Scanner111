using Scanner111.Core.Reporting;

namespace Scanner111.CLI.Services;

/// <summary>
/// Service for analyzing crash log files.
/// </summary>
public interface IAnalyzeService
{
    /// <summary>
    /// Analyzes a crash log file with the specified analyzers.
    /// </summary>
    /// <param name="logFile">Path to the log file.</param>
    /// <param name="analyzerNames">Names of analyzers to run.</param>
    /// <param name="outputFile">Optional output file path.</param>
    /// <param name="format">Report format.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    Task AnalyzeFileAsync(
        string logFile,
        IEnumerable<string> analyzerNames,
        string? outputFile,
        ReportFormat format,
        CancellationToken cancellationToken = default);
}