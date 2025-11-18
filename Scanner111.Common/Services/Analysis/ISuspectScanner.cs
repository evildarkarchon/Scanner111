using Scanner111.Common.Models.Analysis;

namespace Scanner111.Common.Services.Analysis;

/// <summary>
/// Provides suspect detection functionality for crash logs.
/// </summary>
public interface ISuspectScanner
{
    /// <summary>
    /// Scans a crash log for known suspect patterns asynchronously.
    /// </summary>
    /// <param name="header">The crash header containing the main error.</param>
    /// <param name="segments">The parsed log segments.</param>
    /// <param name="patterns">The suspect patterns to match against.</param>
    /// <param name="cancellationToken">A token to cancel the operation.</param>
    /// <returns>A <see cref="SuspectScanResult"/> containing detected suspects.</returns>
    Task<SuspectScanResult> ScanAsync(
        CrashHeader header,
        IReadOnlyList<LogSegment> segments,
        SuspectPatterns patterns,
        CancellationToken cancellationToken = default);
}

/// <summary>
/// Result of scanning for suspect patterns.
/// </summary>
public record SuspectScanResult
{
    /// <summary>
    /// Gets the list of matched error patterns.
    /// </summary>
    public IReadOnlyList<string> ErrorMatches { get; init; } = Array.Empty<string>();

    /// <summary>
    /// Gets the list of matched stack trace patterns.
    /// </summary>
    public IReadOnlyList<string> StackMatches { get; init; } = Array.Empty<string>();

    /// <summary>
    /// Gets the list of recommended actions based on detected patterns.
    /// </summary>
    public IReadOnlyList<string> Recommendations { get; init; } = Array.Empty<string>();
}
