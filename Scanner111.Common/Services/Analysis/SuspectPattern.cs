namespace Scanner111.Common.Services.Analysis;

/// <summary>
/// Represents a pattern to detect known crash suspects.
/// </summary>
public record SuspectPattern
{
    /// <summary>
    /// Gets the regex pattern to match against error messages or stack traces.
    /// </summary>
    public string Pattern { get; init; } = string.Empty;

    /// <summary>
    /// Gets the category of this pattern ("error" for main error, "stack" for call stack).
    /// </summary>
    public string Category { get; init; } = string.Empty;

    /// <summary>
    /// Gets the message to display when this pattern matches.
    /// </summary>
    public string Message { get; init; } = string.Empty;

    /// <summary>
    /// Gets the list of recommended actions to resolve the issue.
    /// </summary>
    public IReadOnlyList<string> Recommendations { get; init; } = Array.Empty<string>();
}

/// <summary>
/// Collection of suspect patterns for error and stack trace matching.
/// </summary>
public record SuspectPatterns
{
    /// <summary>
    /// Gets the patterns to match against main error messages.
    /// </summary>
    public IReadOnlyList<SuspectPattern> ErrorPatterns { get; init; } = Array.Empty<SuspectPattern>();

    /// <summary>
    /// Gets the patterns to match against call stack signatures.
    /// </summary>
    public IReadOnlyList<SuspectPattern> StackSignatures { get; init; } = Array.Empty<SuspectPattern>();
}
