using Scanner111.Core.Models;
using Scanner111.Core.Services;

namespace Scanner111.GUI.Services;

/// <summary>
///     Design-time implementation of IUpdateService that provides mock functionality
/// </summary>
public class DesignTimeUpdateService : IUpdateService
{
    public Task<bool> IsLatestVersionAsync(bool quiet = false, CancellationToken cancellationToken = default)
    {
        // Return true for design-time to avoid network calls
        return Task.FromResult(true);
    }

    public Task<UpdateCheckResult> GetUpdateInfoAsync(CancellationToken cancellationToken = default)
    {
        return Task.FromResult(new UpdateCheckResult
        {
            CheckSuccessful = true,
            IsUpdateAvailable = false,
            CurrentVersion = new Version(1, 0, 0),
            ErrorMessage = null
        });
    }
}