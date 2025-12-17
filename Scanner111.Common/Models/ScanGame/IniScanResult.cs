namespace Scanner111.Common.Models.ScanGame;

/// <summary>
/// Represents the result of scanning INI configuration files in a game directory.
/// </summary>
public record IniScanResult
{
    /// <summary>
    /// Gets the total number of INI/CONF files scanned.
    /// </summary>
    public int TotalFilesScanned { get; init; }

    /// <summary>
    /// Gets the list of configuration issues detected (problematic mod settings).
    /// </summary>
    public IReadOnlyList<ConfigIssue> ConfigIssues { get; init; } = Array.Empty<ConfigIssue>();

    /// <summary>
    /// Gets the list of console command warnings (sStartingConsoleCommand settings).
    /// </summary>
    public IReadOnlyList<ConsoleCommandIssue> ConsoleCommandIssues { get; init; } = Array.Empty<ConsoleCommandIssue>();

    /// <summary>
    /// Gets the list of VSync setting detections.
    /// </summary>
    public IReadOnlyList<VSyncIssue> VSyncIssues { get; init; } = Array.Empty<VSyncIssue>();

    /// <summary>
    /// Gets the list of duplicate file detections.
    /// </summary>
    public IReadOnlyList<DuplicateFileIssue> DuplicateFileIssues { get; init; } = Array.Empty<DuplicateFileIssue>();

    /// <summary>
    /// Gets a value indicating whether any issues were found.
    /// </summary>
    public bool HasIssues =>
        ConfigIssues.Count > 0 ||
        ConsoleCommandIssues.Count > 0 ||
        VSyncIssues.Count > 0 ||
        DuplicateFileIssues.Count > 0;
}

/// <summary>
/// Represents the severity of a configuration issue.
/// </summary>
public enum ConfigIssueSeverity
{
    /// <summary>
    /// Informational notice.
    /// </summary>
    Info,

    /// <summary>
    /// Warning that may affect performance or stability.
    /// </summary>
    Warning,

    /// <summary>
    /// Error that likely causes problems.
    /// </summary>
    Error
}

/// <summary>
/// Represents a detected configuration issue with a recommendation.
/// </summary>
/// <param name="FilePath">The full path to the INI file.</param>
/// <param name="FileName">The filename of the INI file.</param>
/// <param name="Section">The INI section name (null for non-sectioned files).</param>
/// <param name="Setting">The setting/key name.</param>
/// <param name="CurrentValue">The current value in the file.</param>
/// <param name="RecommendedValue">The recommended value to fix the issue.</param>
/// <param name="Description">Human-readable description of the issue.</param>
/// <param name="Severity">The issue severity level.</param>
public record ConfigIssue(
    string FilePath,
    string FileName,
    string? Section,
    string Setting,
    string CurrentValue,
    string RecommendedValue,
    string Description,
    ConfigIssueSeverity Severity = ConfigIssueSeverity.Warning);

/// <summary>
/// Represents a console command setting that may slow startup.
/// </summary>
/// <param name="FilePath">The full path to the INI file.</param>
/// <param name="FileName">The filename of the INI file.</param>
/// <param name="CommandValue">The value of the sStartingConsoleCommand setting.</param>
public record ConsoleCommandIssue(
    string FilePath,
    string FileName,
    string CommandValue);

/// <summary>
/// Represents a VSync setting detection.
/// </summary>
/// <param name="FilePath">The full path to the config file.</param>
/// <param name="FileName">The filename of the config file.</param>
/// <param name="Section">The section containing the VSync setting.</param>
/// <param name="Setting">The VSync setting name.</param>
/// <param name="IsEnabled">Whether VSync is currently enabled.</param>
public record VSyncIssue(
    string FilePath,
    string FileName,
    string Section,
    string Setting,
    bool IsEnabled);

/// <summary>
/// Represents a duplicate configuration file detection.
/// </summary>
/// <param name="OriginalPath">The path to the original file.</param>
/// <param name="DuplicatePath">The path to the duplicate file.</param>
/// <param name="FileName">The filename (same for both).</param>
/// <param name="SimilarityType">How the duplication was detected.</param>
public record DuplicateFileIssue(
    string OriginalPath,
    string DuplicatePath,
    string FileName,
    DuplicateSimilarityType SimilarityType);

/// <summary>
/// Indicates how duplicate files were determined to be similar.
/// </summary>
public enum DuplicateSimilarityType
{
    /// <summary>
    /// Files have identical content hash.
    /// </summary>
    ExactMatch,

    /// <summary>
    /// Files have similar content (90%+ similarity).
    /// </summary>
    HighSimilarity,

    /// <summary>
    /// Files have same size and modification time.
    /// </summary>
    MetadataMatch,

    /// <summary>
    /// INI files have identical sections and values.
    /// </summary>
    IniStructureMatch
}
