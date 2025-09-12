namespace Scanner111.CLI.Services;

/// <summary>
/// Service for managing configuration settings.
/// </summary>
public interface IConfigurationService
{
    /// <summary>
    /// Lists all configuration settings.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    Task ListSettingsAsync(CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Gets a specific configuration value.
    /// </summary>
    /// <param name="key">The configuration key.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    Task GetSettingAsync(string key, CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Sets a configuration value.
    /// </summary>
    /// <param name="key">The configuration key.</param>
    /// <param name="value">The value to set.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    Task SetSettingAsync(string key, string? value, CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Resets all settings to defaults.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    Task ResetSettingsAsync(CancellationToken cancellationToken = default);
}