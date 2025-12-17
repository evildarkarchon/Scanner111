using Scanner111.Common.Models.Reporting;

namespace Scanner111.Common.Models.ScanGame;

/// <summary>
/// Represents the complete result of a ScanGame orchestration operation.
/// </summary>
/// <remarks>
/// <para>
/// This record wraps <see cref="ScanGameReport"/> with additional metadata
/// about the scan execution, including duration, errors, and the generated report.
/// </para>
/// <para>
/// Use this result to check both the scan findings (<see cref="HasAnyIssues"/>)
/// and execution status (<see cref="CompletedSuccessfully"/>).
/// </para>
/// </remarks>
public record ScanGameResult
{
    /// <summary>
    /// Gets the aggregated scan report containing all scanner results.
    /// </summary>
    public ScanGameReport Report { get; init; } = new();

    /// <summary>
    /// Gets the generated markdown report fragment.
    /// </summary>
    /// <remarks>
    /// This is the formatted report ready for display or writing to a file.
    /// May be null if report generation was skipped or failed.
    /// </remarks>
    public ReportFragment? GeneratedReport { get; init; }

    /// <summary>
    /// Gets the total duration of the scan operation.
    /// </summary>
    public TimeSpan Duration { get; init; }

    /// <summary>
    /// Gets errors that occurred during individual scanner operations.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Scanner failures do not prevent other scanners from completing.
    /// All errors are captured here for diagnostic purposes.
    /// </para>
    /// <para>
    /// An empty list indicates all enabled scanners completed successfully.
    /// </para>
    /// </remarks>
    public IReadOnlyList<ScannerError> Errors { get; init; } = Array.Empty<ScannerError>();

    /// <summary>
    /// Gets a value indicating whether the scan completed without any scanner errors.
    /// </summary>
    /// <remarks>
    /// This indicates execution success, not whether issues were found.
    /// Use <see cref="HasAnyIssues"/> to check for scan findings.
    /// </remarks>
    public bool CompletedSuccessfully => Errors.Count == 0;

    /// <summary>
    /// Gets a value indicating whether any issues were found by the scanners.
    /// </summary>
    /// <remarks>
    /// This indicates findings from successful scans, not execution errors.
    /// Use <see cref="CompletedSuccessfully"/> to check for execution status.
    /// </remarks>
    public bool HasAnyIssues => Report.HasAnyIssues;
}

/// <summary>
/// Represents an error that occurred during a scanner operation.
/// </summary>
/// <param name="ScannerName">The name of the scanner that failed (e.g., "Unpacked", "Archives", "INI").</param>
/// <param name="ErrorMessage">The error message describing what went wrong.</param>
/// <param name="Exception">The underlying exception, if available.</param>
public record ScannerError(
    string ScannerName,
    string ErrorMessage,
    Exception? Exception = null);
