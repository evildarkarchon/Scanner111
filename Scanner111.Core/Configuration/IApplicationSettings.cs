using Scanner111.Core.Models;

namespace Scanner111.Core.Configuration;

/// <summary>
/// Interface for application-wide settings management.
/// </summary>
public interface IApplicationSettings
{
    /// <summary>
    /// Loads the settings from storage.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The loaded settings.</returns>
    Task<ApplicationSettings> LoadAsync(CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Saves the settings to storage.
    /// </summary>
    /// <param name="settings">The settings to save.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    Task SaveAsync(ApplicationSettings settings, CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Resets settings to defaults.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    Task ResetAsync(CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Gets a specific setting value.
    /// </summary>
    /// <typeparam name="T">The type of the setting value.</typeparam>
    /// <param name="key">The setting key.</param>
    /// <param name="defaultValue">The default value if the setting doesn't exist.</param>
    /// <returns>The setting value.</returns>
    T GetSetting<T>(string key, T defaultValue = default!);
    
    /// <summary>
    /// Sets a specific setting value.
    /// </summary>
    /// <typeparam name="T">The type of the setting value.</typeparam>
    /// <param name="key">The setting key.</param>
    /// <param name="value">The value to set.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    Task SetSettingAsync<T>(string key, T value, CancellationToken cancellationToken = default);
}