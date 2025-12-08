using Scanner111.Common.Models.Analysis;

namespace Scanner111.Common.Services.Analysis;

/// <summary>
/// Analyzes FormIDs in crash logs by looking them up in a database.
/// </summary>
public interface IFormIdAnalyzer
{
    /// <summary>
    /// Analyzes segments for FormIDs and looks them up.
    /// </summary>
    /// <param name="segments">The parsed log segments.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>Analysis result containing detected form IDs and their names.</returns>
    Task<FormIdAnalysisResult> AnalyzeAsync(
        IReadOnlyList<LogSegment> segments,
        CancellationToken ct = default);

    /// <summary>
    /// Looks up specific FormIDs in the database.
    /// </summary>
    /// <param name="formIds">The list of FormIDs to look up.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>A dictionary mapping FormID to its record name.</returns>
    Task<IReadOnlyDictionary<string, string>> LookupFormIdsAsync(
        IReadOnlyList<string> formIds,
        CancellationToken ct = default);
}

/// <summary>
/// Result of FormID analysis.
/// </summary>
public record FormIdAnalysisResult
{
    /// <summary>
    /// Gets the dictionary of FormID to Record Name mappings found.
    /// </summary>
    public IReadOnlyDictionary<string, string> DetectedRecords { get; init; } = new Dictionary<string, string>();
}
