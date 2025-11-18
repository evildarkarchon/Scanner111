namespace Scanner111.Common.Models.Configuration;

/// <summary>
/// Configuration for crash log scanning operations.
/// </summary>
public record ScanConfig
{
    /// <summary>
    /// Gets a value indicating whether FCX mode is enabled.
    /// FCX mode (Fallout Crash Xtra) provides additional diagnostic information.
    /// </summary>
    public bool FcxMode { get; init; }

    /// <summary>
    /// Gets a value indicating whether FormID values should be displayed in reports.
    /// </summary>
    public bool ShowFormIdValues { get; init; }

    /// <summary>
    /// Gets a value indicating whether unsolved logs should be moved to a separate directory.
    /// </summary>
    public bool MoveUnsolvedLogs { get; init; }

    /// <summary>
    /// Gets a value indicating whether logs should be simplified by removing unnecessary sections.
    /// </summary>
    public bool SimplifyLogs { get; init; }

    /// <summary>
    /// Gets custom paths for game directories and resources.
    /// </summary>
    public IReadOnlyDictionary<string, string> CustomPaths { get; init; } =
        new Dictionary<string, string>();

    /// <summary>
    /// Gets the maximum number of concurrent log processing tasks.
    /// Default is 50 to match the original CLASSIC behavior.
    /// </summary>
    public int MaxConcurrent { get; init; } = 50;

    /// <summary>
    /// Gets a value indicating whether the FormID database exists and can be used for lookups.
    /// </summary>
    public bool FormIdDatabaseExists { get; init; }

    /// <summary>
    /// Gets the list of items to remove during log simplification.
    /// </summary>
    public IReadOnlyList<string> RemoveList { get; init; } = Array.Empty<string>();
}
