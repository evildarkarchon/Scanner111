using Scanner111.Core.Analysis;
using Scanner111.Core.Reporting;

namespace Scanner111.CLI.Services;

/// <summary>
/// Service for exporting analysis results.
/// </summary>
public interface IExportService
{
    /// <summary>
    /// Exports analysis results to a file.
    /// </summary>
    /// <param name="results">The analysis results to export.</param>
    /// <param name="fileName">The base file name (without extension).</param>
    /// <param name="format">The export format.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The path to the exported file.</returns>
    Task<string> ExportResultsAsync(
        IEnumerable<AnalysisResult> results,
        string fileName,
        ReportFormat format,
        CancellationToken cancellationToken = default);
}