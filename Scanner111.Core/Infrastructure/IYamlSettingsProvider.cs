namespace Scanner111.Core.Infrastructure;

/// <summary>
///     Interface for accessing YAML settings with caching
/// </summary>
public interface IYamlSettingsProvider
{
    /// <summary>
    ///     Get a setting value from a YAML file
    /// </summary>
    [Obsolete("Use LoadYaml<T> method instead for strongly-typed YAML access")]
    T? GetSetting<T>(string yamlFile, string keyPath, T? defaultValue = default);

    /// <summary>
    ///     Set a setting value in memory cache
    /// </summary>
    [Obsolete("YAML data files are read-only. Use LoadYaml<T> method for reading YAML data")]
    void SetSetting<T>(string yamlFile, string keyPath, T value);

    /// <summary>
    ///     Load a complete YAML file as an object
    /// </summary>
    T? LoadYaml<T>(string yamlFile) where T : class;

    /// <summary>
    ///     Clear the settings cache
    /// </summary>
    void ClearCache();
}