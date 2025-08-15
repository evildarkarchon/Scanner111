using Scanner111.Core.Models;

namespace Scanner111.Core.Services;

/// <summary>
///     Service for checking application updates from various sources
/// </summary>
public interface IUpdateService
{
    /// <summary>
    ///     Checks if the current version is the latest available version
    /// </summary>
    /// <param name="quiet">If true, suppresses status messages</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>True if current version is latest, false if update is available</returns>
    Task<bool> IsLatestVersionAsync(bool quiet = false, CancellationToken cancellationToken = default);

    /// <summary>
    ///     Gets detailed update information including available versions
    /// </summary>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Update information result</returns>
    Task<UpdateCheckResult> GetUpdateInfoAsync(CancellationToken cancellationToken = default);
}