using Scanner111.Common.Models.Update;

namespace Scanner111.Common.Services.Updates;

/// <summary>
/// Service for checking GitHub releases for application updates.
/// </summary>
public interface IUpdateService
{
    /// <summary>
    /// Checks for updates from GitHub releases.
    /// </summary>
    /// <param name="includePrerelease">Whether to include prerelease versions.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Result containing update information.</returns>
    Task<UpdateCheckResult> CheckForUpdatesAsync(
        bool includePrerelease = false,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets the current application version.
    /// </summary>
    /// <returns>The current version string.</returns>
    string GetCurrentVersion();
}
