namespace Scanner111.Core.Infrastructure;

/// <summary>
///     Interface for accessing YAML settings with caching
/// </summary>
public interface IYamlSettingsProvider
{
    /// <summary>
    ///     Load a complete YAML file as an object
    /// </summary>
    T? LoadYaml<T>(string yamlFile) where T : class;

    /// <summary>
    ///     Load a complete YAML file as an object (async version)
    /// </summary>
    Task<T?> LoadYamlAsync<T>(string yamlFile) where T : class;

    /// <summary>
    ///     Clear the settings cache
    /// </summary>
    void ClearCache();
}