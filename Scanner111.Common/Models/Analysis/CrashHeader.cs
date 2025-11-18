namespace Scanner111.Common.Models.Analysis;

/// <summary>
/// Represents the header information extracted from a crash log.
/// This includes metadata about the game, crash generator, and primary error.
/// </summary>
public record CrashHeader
{
    /// <summary>
    /// Gets the version of the game (e.g., "1.10.163.0" for Fallout 4).
    /// </summary>
    public string GameVersion { get; init; } = string.Empty;

    /// <summary>
    /// Gets the version of the crash log generator (e.g., "Buffout 4 v1.26.2").
    /// </summary>
    public string CrashGeneratorVersion { get; init; } = string.Empty;

    /// <summary>
    /// Gets the main error message from the crash log.
    /// </summary>
    public string MainError { get; init; } = string.Empty;

    /// <summary>
    /// Gets the timestamp when the crash occurred, if available.
    /// </summary>
    public DateTime? CrashTimestamp { get; init; }
}
