using Scanner111.Common.Models.Configuration;

namespace Scanner111.Common.Services.Settings;

/// <summary>
/// Service for reading and writing user settings that persist across sessions.
/// Settings are stored in JSON format in the application data directory.
/// </summary>
public interface IUserSettingsService
{
    /// <summary>
    /// Loads the user settings from storage.
    /// </summary>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>The loaded settings, or default settings if no settings file exists.</returns>
    Task<UserSettings> LoadAsync(CancellationToken ct = default);

    /// <summary>
    /// Saves the user settings to storage.
    /// </summary>
    /// <param name="settings">The settings to save.</param>
    /// <param name="ct">Cancellation token.</param>
    Task SaveAsync(UserSettings settings, CancellationToken ct = default);

    /// <summary>
    /// Gets the current settings, loading from storage if not already cached.
    /// </summary>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>The current user settings.</returns>
    Task<UserSettings> GetCurrentAsync(CancellationToken ct = default);

    /// <summary>
    /// Updates a single setting by applying a transform function.
    /// </summary>
    /// <param name="transform">Function that transforms the current settings.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>The updated settings.</returns>
    Task<UserSettings> UpdateAsync(Func<UserSettings, UserSettings> transform, CancellationToken ct = default);

    /// <summary>
    /// Gets the custom scan path setting.
    /// </summary>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>The custom scan path, or null if not set.</returns>
    Task<string?> GetCustomScanPathAsync(CancellationToken ct = default);

    /// <summary>
    /// Sets the custom scan path setting.
    /// </summary>
    /// <param name="path">The path to set, or null to clear.</param>
    /// <param name="ct">Cancellation token.</param>
    Task SetCustomScanPathAsync(string? path, CancellationToken ct = default);

    /// <summary>
    /// Gets the mods folder path setting.
    /// </summary>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>The mods folder path, or null if not set.</returns>
    Task<string?> GetModsFolderPathAsync(CancellationToken ct = default);

    /// <summary>
    /// Sets the mods folder path setting.
    /// </summary>
    /// <param name="path">The path to set, or null to clear.</param>
    /// <param name="ct">Cancellation token.</param>
    Task SetModsFolderPathAsync(string? path, CancellationToken ct = default);

    /// <summary>
    /// Gets the INI folder path setting.
    /// </summary>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>The INI folder path, or null if not set.</returns>
    Task<string?> GetIniFolderPathAsync(CancellationToken ct = default);

    /// <summary>
    /// Sets the INI folder path setting.
    /// </summary>
    /// <param name="path">The path to set, or null to clear.</param>
    /// <param name="ct">Cancellation token.</param>
    Task SetIniFolderPathAsync(string? path, CancellationToken ct = default);

    /// <summary>
    /// Gets the game root path setting.
    /// </summary>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>The game root path, or null if not set.</returns>
    Task<string?> GetGameRootPathAsync(CancellationToken ct = default);

    /// <summary>
    /// Sets the game root path setting.
    /// </summary>
    /// <param name="path">The path to set, or null to clear.</param>
    /// <param name="ct">Cancellation token.</param>
    Task SetGameRootPathAsync(string? path, CancellationToken ct = default);

    /// <summary>
    /// Gets the documents path setting.
    /// </summary>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>The documents path, or null if not set.</returns>
    Task<string?> GetDocumentsPathAsync(CancellationToken ct = default);

    /// <summary>
    /// Sets the documents path setting.
    /// </summary>
    /// <param name="path">The path to set, or null to clear.</param>
    /// <param name="ct">Cancellation token.</param>
    Task SetDocumentsPathAsync(string? path, CancellationToken ct = default);

    /// <summary>
    /// Gets the currently selected game name.
    /// </summary>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>The selected game name, or "Fallout4" as default.</returns>
    Task<string> GetSelectedGameAsync(CancellationToken ct = default);

    /// <summary>
    /// Sets the currently selected game name.
    /// </summary>
    /// <param name="gameName">The game name to select.</param>
    /// <param name="ct">Cancellation token.</param>
    Task SetSelectedGameAsync(string gameName, CancellationToken ct = default);

    /// <summary>
    /// Gets the VR mode setting.
    /// </summary>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>True if VR mode is enabled.</returns>
    Task<bool> GetIsVrModeAsync(CancellationToken ct = default);

    /// <summary>
    /// Sets the VR mode setting.
    /// </summary>
    /// <param name="isVrMode">True to enable VR mode.</param>
    /// <param name="ct">Cancellation token.</param>
    Task SetIsVrModeAsync(bool isVrMode, CancellationToken ct = default);

    /// <summary>
    /// Clears the cached settings, forcing a reload from storage on next access.
    /// </summary>
    void ClearCache();
}
