using Scanner111.Common.Models.GamePath;
using Scanner111.Common.Models.ScanGame;

namespace Scanner111.Common.Models.GameIntegrity;

/// <summary>
/// Represents the result of a game integrity check.
/// </summary>
public record GameIntegrityResult
{
    /// <summary>
    /// Gets the game type that was checked.
    /// </summary>
    public GameType GameType { get; init; }

    /// <summary>
    /// Gets the path to the game executable.
    /// </summary>
    public string? ExecutablePath { get; init; }

    /// <summary>
    /// Gets whether the executable was found.
    /// </summary>
    public bool ExecutableFound { get; init; }

    /// <summary>
    /// Gets the executable version check result.
    /// </summary>
    public ExecutableVersionStatus VersionStatus { get; init; } = ExecutableVersionStatus.NotChecked;

    /// <summary>
    /// Gets the computed hash of the game executable.
    /// </summary>
    public string? ComputedHash { get; init; }

    /// <summary>
    /// Gets the game version (old/new) that matched, if any.
    /// </summary>
    public string? MatchedVersion { get; init; }

    /// <summary>
    /// Gets the installation location check result.
    /// </summary>
    public InstallationLocationStatus LocationStatus { get; init; } = InstallationLocationStatus.NotChecked;

    /// <summary>
    /// Gets whether a Steam INI file was detected (indicates outdated installation).
    /// </summary>
    public bool SteamIniDetected { get; init; }

    /// <summary>
    /// Gets the list of integrity issues found.
    /// </summary>
    public IReadOnlyList<GameIntegrityIssue> Issues { get; init; } = Array.Empty<GameIntegrityIssue>();

    /// <summary>
    /// Gets a value indicating whether any issues were found.
    /// </summary>
    public bool HasIssues =>
        VersionStatus == ExecutableVersionStatus.Outdated ||
        VersionStatus == ExecutableVersionStatus.Unknown ||
        VersionStatus == ExecutableVersionStatus.NotFound ||
        VersionStatus == ExecutableVersionStatus.HashError ||
        LocationStatus == InstallationLocationStatus.RestrictedLocation ||
        SteamIniDetected;
}

/// <summary>
/// Represents the status of the game executable version check.
/// </summary>
public enum ExecutableVersionStatus
{
    /// <summary>
    /// Version check was not performed.
    /// </summary>
    NotChecked,

    /// <summary>
    /// Executable hash matches the latest known version.
    /// </summary>
    LatestVersion,

    /// <summary>
    /// Executable hash matches an older known version.
    /// </summary>
    Outdated,

    /// <summary>
    /// Executable hash does not match any known version.
    /// </summary>
    Unknown,

    /// <summary>
    /// Executable file was not found.
    /// </summary>
    NotFound,

    /// <summary>
    /// Could not compute hash (permission error, etc.).
    /// </summary>
    HashError
}

/// <summary>
/// Represents the status of the game installation location.
/// </summary>
public enum InstallationLocationStatus
{
    /// <summary>
    /// Location check was not performed.
    /// </summary>
    NotChecked,

    /// <summary>
    /// Game is installed in a recommended location.
    /// </summary>
    RecommendedLocation,

    /// <summary>
    /// Game is installed in Program Files (may cause permission issues).
    /// </summary>
    RestrictedLocation,

    /// <summary>
    /// Game path was not provided.
    /// </summary>
    PathNotProvided
}

/// <summary>
/// Represents an integrity issue with the game installation.
/// </summary>
/// <param name="Type">The type of integrity issue.</param>
/// <param name="Message">The issue message.</param>
/// <param name="Severity">The severity level.</param>
/// <param name="Recommendation">Optional recommendation to fix the issue.</param>
public record GameIntegrityIssue(
    GameIntegrityIssueType Type,
    string Message,
    ConfigIssueSeverity Severity,
    string? Recommendation = null);

/// <summary>
/// Types of game integrity issues.
/// </summary>
public enum GameIntegrityIssueType
{
    /// <summary>
    /// Game executable not found.
    /// </summary>
    ExecutableNotFound,

    /// <summary>
    /// Game executable version is outdated.
    /// </summary>
    OutdatedVersion,

    /// <summary>
    /// Game executable version is unknown.
    /// </summary>
    UnknownVersion,

    /// <summary>
    /// Game installed in restricted location (Program Files).
    /// </summary>
    RestrictedInstallLocation,

    /// <summary>
    /// Steam INI file detected (indicates outdated Steam installation).
    /// </summary>
    SteamIniPresent,

    /// <summary>
    /// Could not read executable file.
    /// </summary>
    ExecutableReadError
}
