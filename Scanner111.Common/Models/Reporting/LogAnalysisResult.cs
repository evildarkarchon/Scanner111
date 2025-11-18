namespace Scanner111.Common.Models.Reporting;

using Scanner111.Common.Models.Analysis;

/// <summary>
/// Represents the complete analysis result for a single crash log file.
/// </summary>
public record LogAnalysisResult
{
    /// <summary>
    /// Gets the name of the crash log file.
    /// </summary>
    public string LogFileName { get; init; } = string.Empty;

    /// <summary>
    /// Gets the crash header information.
    /// </summary>
    public CrashHeader Header { get; init; } = null!;

    /// <summary>
    /// Gets the parsed segments from the crash log.
    /// </summary>
    public IReadOnlyList<LogSegment> Segments { get; init; } = Array.Empty<LogSegment>();

    /// <summary>
    /// Gets the generated report fragment for this crash log.
    /// </summary>
    public ReportFragment Report { get; init; } = null!;

    /// <summary>
    /// Gets a value indicating whether the log is complete.
    /// A log is considered complete if it has a PLUGINS section.
    /// </summary>
    public bool IsComplete { get; init; }

    /// <summary>
    /// Gets the list of warnings generated during analysis.
    /// </summary>
    public IReadOnlyList<string> Warnings { get; init; } = Array.Empty<string>();
}
