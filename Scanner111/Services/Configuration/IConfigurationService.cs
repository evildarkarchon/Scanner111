using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace Scanner111.Services.Configuration;

/// <summary>
/// Defines the types of YAML configuration stores available
/// </summary>
public enum YamlStore
{
    /// <summary>CLASSIC Data/databases/CLASSIC Main.yaml</summary>
    Main,

    /// <summary>CLASSIC Settings.yaml</summary>
    Settings,

    /// <summary>CLASSIC Ignore.yaml</summary>
    Ignore,

    /// <summary>CLASSIC Data/databases/CLASSIC Fallout4.yaml</summary>
    Game,

    /// <summary>CLASSIC Data/CLASSIC Fallout4 Local.yaml</summary>
    GameLocal,

    /// <summary>tests/test_settings.yaml</summary>
    Test
}

/// <summary>
/// Interface for YAML-based configuration management with caching
/// </summary>
public interface IConfigurationService
{
    /// <summary>
    /// Gets a configuration value of the specified type
    /// </summary>
    /// <typeparam name="T">The type of the configuration value</typeparam>
    /// <param name="store">The YAML store to read from</param>
    /// <param name="keyPath">Dot-separated path to the configuration key</param>
    /// <returns>The configuration value or default(T) if not found</returns>
    T? GetValue<T>(YamlStore store, string keyPath);

    /// <summary>
    /// Gets a configuration value of the specified type asynchronously
    /// </summary>
    /// <typeparam name="T">The type of the configuration value</typeparam>
    /// <param name="store">The YAML store to read from</param>
    /// <param name="keyPath">Dot-separated path to the configuration key</param>
    /// <returns>The configuration value or default(T) if not found</returns>
    Task<T?> GetValueAsync<T>(YamlStore store, string keyPath);

    /// <summary>
    /// Sets a configuration value
    /// </summary>
    /// <typeparam name="T">The type of the configuration value</typeparam>
    /// <param name="store">The YAML store to write to</param>
    /// <param name="keyPath">Dot-separated path to the configuration key</param>
    /// <param name="value">The value to set</param>
    /// <returns>True if the value was set successfully</returns>
    Task<bool> SetValueAsync<T>(YamlStore store, string keyPath, T value);

    /// <summary>
    /// Gets a setting from the Settings store with a default value
    /// </summary>
    /// <typeparam name="T">The type of the setting value</typeparam>
    /// <param name="keyPath">Dot-separated path to the setting key</param>
    /// <param name="defaultValue">Default value if setting doesn't exist</param>
    /// <returns>The setting value or the default value</returns>
    T GetSetting<T>(string keyPath, T defaultValue = default!);

    /// <summary>
    /// Sets a setting in the Settings store
    /// </summary>
    /// <typeparam name="T">The type of the setting value</typeparam>
    /// <param name="keyPath">Dot-separated path to the setting key</param>
    /// <param name="value">The value to set</param>
    /// <returns>True if the setting was set successfully</returns>
    Task<bool> SetSettingAsync<T>(string keyPath, T value);

    /// <summary>
    /// Clears the cache for all or specific YAML stores
    /// </summary>
    /// <param name="store">Specific store to clear, or null to clear all</param>
    void ClearCache(YamlStore? store = null);

    /// <summary>
    /// Preloads static YAML files into cache
    /// </summary>
    Task PreloadStaticFilesAsync();

    /// <summary>
    /// Checks if a YAML store file exists
    /// </summary>
    /// <param name="store">The YAML store to check</param>
    /// <returns>True if the file exists</returns>
    bool StoreExists(YamlStore store);

    /// <summary>
    /// Creates default configuration files if they don't exist
    /// </summary>
    Task EnsureDefaultFilesAsync();
}