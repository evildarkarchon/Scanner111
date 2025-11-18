using Scanner111.Common.Models.Analysis;

namespace Scanner111.Common.Services.Parsing;

/// <summary>
/// Provides log parsing functionality for crash logs.
/// </summary>
public interface ILogParser
{
    /// <summary>
    /// Parses a crash log asynchronously and extracts header and segments.
    /// </summary>
    /// <param name="logContent">The full content of the crash log.</param>
    /// <param name="cancellationToken">A token to cancel the operation.</param>
    /// <returns>A <see cref="LogParseResult"/> containing the parsed data.</returns>
    Task<LogParseResult> ParseAsync(string logContent, CancellationToken cancellationToken = default);

    /// <summary>
    /// Parses the header section of a crash log.
    /// </summary>
    /// <param name="logContent">The full content of the crash log.</param>
    /// <returns>A <see cref="CrashHeader"/> if parsing succeeds; otherwise, null.</returns>
    CrashHeader? ParseHeader(string logContent);

    /// <summary>
    /// Extracts all segments from a crash log.
    /// </summary>
    /// <param name="logContent">The full content of the crash log.</param>
    /// <returns>A list of <see cref="LogSegment"/> objects representing the parsed segments.</returns>
    IReadOnlyList<LogSegment> ExtractSegments(string logContent);
}

/// <summary>
/// Represents the result of parsing a crash log.
/// </summary>
public record LogParseResult
{
    /// <summary>
    /// Gets the parsed crash header.
    /// </summary>
    public CrashHeader Header { get; init; } = null!;

    /// <summary>
    /// Gets the extracted log segments.
    /// </summary>
    public IReadOnlyList<LogSegment> Segments { get; init; } = Array.Empty<LogSegment>();

    /// <summary>
    /// Gets a value indicating whether the log was successfully parsed.
    /// </summary>
    public bool IsValid { get; init; }

    /// <summary>
    /// Gets the error message if parsing failed.
    /// </summary>
    public string? ErrorMessage { get; init; }
}
