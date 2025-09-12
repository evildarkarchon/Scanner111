using Scanner111.Core.Configuration;

namespace Scanner111.CLI.Configuration;

/// <summary>
/// Interface for CLI-specific settings that extends the core application settings.
/// </summary>
public interface ICliSettings : IApplicationSettings
{
    /// <summary>
    /// Gets CLI-specific settings.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The CLI-specific settings.</returns>
    Task<CliSpecificSettings> GetCliSettingsAsync(CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Updates CLI-specific settings.
    /// </summary>
    /// <param name="settings">The CLI-specific settings to update.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    Task UpdateCliSettingsAsync(CliSpecificSettings settings, CancellationToken cancellationToken = default);
}