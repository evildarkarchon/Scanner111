namespace Scanner111.Common.Models.Update;

/// <summary>
/// Result of an update check operation.
/// </summary>
public sealed class UpdateCheckResult
{
    /// <summary>
    /// Gets a value indicating whether the check was successful.
    /// </summary>
    public bool Success { get; init; }

    /// <summary>
    /// Gets a value indicating whether an update is available.
    /// </summary>
    public bool IsUpdateAvailable { get; init; }

    /// <summary>
    /// Gets the current application version.
    /// </summary>
    public string? CurrentVersion { get; init; }

    /// <summary>
    /// Gets the latest available release information.
    /// </summary>
    public ReleaseInfo? LatestRelease { get; init; }

    /// <summary>
    /// Gets the error message if the check failed.
    /// </summary>
    public string? ErrorMessage { get; init; }

    /// <summary>
    /// Creates a successful result indicating an update is available.
    /// </summary>
    /// <param name="currentVersion">The current application version.</param>
    /// <param name="latestRelease">Information about the latest release.</param>
    /// <returns>A successful update check result.</returns>
    public static UpdateCheckResult CreateUpdateAvailable(
        string currentVersion,
        ReleaseInfo latestRelease) => new()
    {
        Success = true,
        IsUpdateAvailable = true,
        CurrentVersion = currentVersion,
        LatestRelease = latestRelease
    };

    /// <summary>
    /// Creates a successful result indicating the application is up to date.
    /// </summary>
    /// <param name="currentVersion">The current application version.</param>
    /// <param name="latestRelease">Optional information about the latest release.</param>
    /// <returns>A successful update check result.</returns>
    public static UpdateCheckResult CreateUpToDate(
        string currentVersion,
        ReleaseInfo? latestRelease = null) => new()
    {
        Success = true,
        IsUpdateAvailable = false,
        CurrentVersion = currentVersion,
        LatestRelease = latestRelease
    };

    /// <summary>
    /// Creates a failure result.
    /// </summary>
    /// <param name="errorMessage">The error message describing the failure.</param>
    /// <param name="currentVersion">Optional current application version.</param>
    /// <returns>A failed update check result.</returns>
    public static UpdateCheckResult CreateFailure(
        string errorMessage,
        string? currentVersion = null) => new()
    {
        Success = false,
        IsUpdateAvailable = false,
        CurrentVersion = currentVersion,
        ErrorMessage = errorMessage
    };
}
