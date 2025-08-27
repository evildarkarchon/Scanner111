using System.Collections.Generic;
using System.Threading.Tasks;
using Scanner111.Core.Analysis;

namespace Scanner111.Core.Reporting;

/// <summary>
/// Defines the contract for composing analysis results into reports.
/// </summary>
public interface IReportComposer
{
    /// <summary>
    /// Composes a report from multiple analysis results.
    /// </summary>
    /// <param name="results">The analysis results to compose.</param>
    /// <param name="options">Options for report composition.</param>
    /// <returns>The composed report as a string.</returns>
    Task<string> ComposeReportAsync(
        IEnumerable<AnalysisResult> results,
        ReportOptions? options = null);
    
    /// <summary>
    /// Composes a report from report fragments.
    /// </summary>
    /// <param name="fragments">The fragments to compose.</param>
    /// <param name="options">Options for report composition.</param>
    /// <returns>The composed report as a string.</returns>
    Task<string> ComposeFromFragmentsAsync(
        IEnumerable<ReportFragment> fragments,
        ReportOptions? options = null);
}

/// <summary>
/// Options for report composition.
/// </summary>
public sealed class ReportOptions
{
    /// <summary>
    /// Gets or sets whether to include timing information.
    /// </summary>
    public bool IncludeTimingInfo { get; set; } = true;
    
    /// <summary>
    /// Gets or sets whether to include metadata.
    /// </summary>
    public bool IncludeMetadata { get; set; } = false;
    
    /// <summary>
    /// Gets or sets whether to include skipped analyzers.
    /// </summary>
    public bool IncludeSkipped { get; set; } = false;
    
    /// <summary>
    /// Gets or sets the report format.
    /// </summary>
    public ReportFormat Format { get; set; } = ReportFormat.Markdown;
    
    /// <summary>
    /// Gets or sets the title for the report.
    /// </summary>
    public string? Title { get; set; }
    
    /// <summary>
    /// Gets or sets whether to sort fragments by order.
    /// </summary>
    public bool SortByOrder { get; set; } = true;
    
    /// <summary>
    /// Gets or sets the visibility level for conditional fragments.
    /// </summary>
    public FragmentVisibility MinimumVisibility { get; set; } = FragmentVisibility.Always;
}

/// <summary>
/// Defines the output format for reports.
/// </summary>
public enum ReportFormat
{
    /// <summary>
    /// Markdown format.
    /// </summary>
    Markdown,
    
    /// <summary>
    /// Plain text format.
    /// </summary>
    PlainText,
    
    /// <summary>
    /// HTML format.
    /// </summary>
    Html,
    
    /// <summary>
    /// JSON format.
    /// </summary>
    Json
}