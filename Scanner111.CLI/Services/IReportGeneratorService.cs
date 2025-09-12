using Scanner111.Core.Analysis;
using Scanner111.Core.Reporting;

namespace Scanner111.CLI.Services;

/// <summary>
/// Service for generating reports from analysis results.
/// </summary>
public interface IReportGeneratorService
{
    /// <summary>
    /// Generates a report from analysis results.
    /// </summary>
    /// <param name="results">The analysis results.</param>
    /// <param name="format">The report format.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The generated report as a string.</returns>
    Task<string> GenerateReportAsync(
        IEnumerable<AnalysisResult> results,
        ReportFormat format,
        CancellationToken cancellationToken = default);
}