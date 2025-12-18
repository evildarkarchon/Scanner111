using Scanner111.Common.Models.Analysis;
using Scanner111.Common.Models.Reporting;

namespace Scanner111.Common.Services.Analysis;

/// <summary>
/// Scans crash log call stacks for named records (game objects, record types, mod files).
/// Named records help identify which game data is involved in a crash.
/// </summary>
public interface IRecordScanner
{
    /// <summary>
    /// Gets or sets the configuration for record scanning.
    /// </summary>
    RecordScannerConfiguration Configuration { get; set; }

    /// <summary>
    /// Scans a call stack segment for named records.
    /// </summary>
    /// <param name="callStackSegment">The call stack segment to scan.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The scan result containing matched records and counts.</returns>
    Task<RecordScanResult> ScanAsync(
        LogSegment? callStackSegment,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Scans all segments for named records, automatically finding the call stack.
    /// </summary>
    /// <param name="segments">All segments from the crash log.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The scan result containing matched records and counts.</returns>
    Task<RecordScanResult> ScanFromSegmentsAsync(
        IReadOnlyList<LogSegment> segments,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Creates a report fragment for the scan result.
    /// </summary>
    /// <param name="result">The record scan result.</param>
    /// <returns>A report fragment describing the found records.</returns>
    ReportFragment CreateReportFragment(RecordScanResult result);
}
