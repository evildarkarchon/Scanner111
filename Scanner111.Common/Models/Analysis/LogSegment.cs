namespace Scanner111.Common.Models.Analysis;

/// <summary>
/// Represents a parsed section of a crash log file.
/// Crash logs are divided into segments such as SYSTEM SPECS, MODULES, PLUGINS, etc.
/// </summary>
public record LogSegment
{
    /// <summary>
    /// Gets the name of the segment (e.g., "SYSTEM SPECS", "MODULES", "PLUGINS").
    /// </summary>
    public string Name { get; init; } = string.Empty;

    /// <summary>
    /// Gets the lines of text in this segment.
    /// </summary>
    public IReadOnlyList<string> Lines { get; init; } = Array.Empty<string>();

    /// <summary>
    /// Gets the starting index of this segment in the original log content.
    /// </summary>
    public int StartIndex { get; init; }

    /// <summary>
    /// Gets the ending index of this segment in the original log content.
    /// </summary>
    public int EndIndex { get; init; }
}
