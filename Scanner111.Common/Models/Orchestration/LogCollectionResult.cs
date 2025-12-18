namespace Scanner111.Common.Models.Orchestration;

/// <summary>
/// Represents the result of a log collection operation.
/// </summary>
public record LogCollectionResult
{
    /// <summary>
    /// Gets the list of crash log files that were moved.
    /// </summary>
    public IReadOnlyList<string> MovedCrashLogs { get; init; } = Array.Empty<string>();

    /// <summary>
    /// Gets the list of autoscan report files that were moved.
    /// </summary>
    public IReadOnlyList<string> MovedReports { get; init; } = Array.Empty<string>();

    /// <summary>
    /// Gets the list of files that failed to move, with error details.
    /// </summary>
    public IReadOnlyList<LogCollectionError> Errors { get; init; } = Array.Empty<LogCollectionError>();

    /// <summary>
    /// Gets a value indicating whether any files were moved.
    /// </summary>
    public bool HasMovedFiles => MovedCrashLogs.Count > 0 || MovedReports.Count > 0;

    /// <summary>
    /// Gets a value indicating whether any errors occurred.
    /// </summary>
    public bool HasErrors => Errors.Count > 0;

    /// <summary>
    /// Gets the total number of files moved (crash logs + reports).
    /// </summary>
    public int TotalMoved => MovedCrashLogs.Count + MovedReports.Count;

    /// <summary>
    /// Returns an empty result indicating no files were moved.
    /// </summary>
    public static LogCollectionResult Empty { get; } = new();
}

/// <summary>
/// Represents an error that occurred during log collection.
/// </summary>
public record LogCollectionError(
    string FilePath,
    string ErrorMessage
);

/// <summary>
/// Configuration for the log collector.
/// </summary>
public record LogCollectorConfiguration
{
    /// <summary>
    /// Gets the base backup directory where logs will be moved.
    /// </summary>
    public string BackupBasePath { get; init; } = string.Empty;

    /// <summary>
    /// Gets the subdirectory name for unsolved logs (relative to BackupBasePath).
    /// </summary>
    public string UnsolvedLogsSubdirectory { get; init; } = "Unsolved Logs";

    /// <summary>
    /// Gets the report file suffix (e.g., "-AUTOSCAN.md").
    /// </summary>
    public string ReportFileSuffix { get; init; } = "-AUTOSCAN.md";

    /// <summary>
    /// Gets a value indicating whether to create the backup directory if it doesn't exist.
    /// </summary>
    public bool CreateDirectoryIfNotExists { get; init; } = true;

    /// <summary>
    /// Gets a value indicating whether to overwrite existing files in the backup directory.
    /// </summary>
    public bool OverwriteExisting { get; init; } = false;

    /// <summary>
    /// Returns an empty configuration.
    /// </summary>
    public static LogCollectorConfiguration Empty { get; } = new();
}
