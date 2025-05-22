using Scanner111.Models;

namespace Scanner111.Services;

/// <summary>
///     Interface for YAML settings cache service to enable easier testing and dependency injection
/// </summary>
public interface IYamlSettingsCacheService
{
    /// <summary>
    ///     Gets a setting from the YAML cache
    /// </summary>
    /// <typeparam name="T">Type of the setting to retrieve</typeparam>
    /// <param name="yamlType">The YAML file type</param>
    /// <param name="path">Path to the setting in the YAML file</param>
    /// <param name="defaultValue">Default value to return if setting is not found</param>
    /// <returns>The setting value, or default for the type if not found</returns>
    T? GetSetting<T>(Yaml yamlType, string path, T? defaultValue = default);

    /// <summary>
    ///     Gets a setting from the cache (simplified version)
    /// </summary>
    /// <typeparam name="T">Type of setting to retrieve</typeparam>
    /// <param name="section">YAML section</param>
    /// <param name="key">Setting key</param>
    /// <returns>The setting value or default for type if not found</returns>
    T? GetSetting<T>(Yaml section, string key);

    /// <summary>
    ///     Sets a setting in the YAML cache
    /// </summary>
    /// <typeparam name="T">Type of the setting to set</typeparam>
    /// <param name="yamlType">The YAML file type</param>
    /// <param name="path">Path to the setting in the YAML file</param>
    /// <param name="value">The value to set</param>
    void SetSetting<T>(Yaml yamlType, string path, T value);
}