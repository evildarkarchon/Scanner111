using System.Collections.Generic;

namespace Scanner111.Services;

/// <summary>
///     Interface for the YAML settings cache service.
/// </summary>
public interface IYamlSettingsCache
{
    /// <summary>
    ///     Gets the path for a given YAML store.
    /// </summary>
    /// <param name="yamlStore">The YAML store to get the path for.</param>
    /// <returns>The file path for the specified YAML store.</returns>
    string GetPathForStore(YamlStore yamlStore);

    /// <summary>
    ///     Loads a YAML file and returns its contents.
    /// </summary>
    /// <param name="yamlPath">The path to the YAML file.</param>
    /// <returns>The YAML file contents as a dictionary.</returns>
    Dictionary<string, object> LoadYaml(string yamlPath);

    /// <summary>
    ///     Gets or sets a setting in a YAML store.
    /// </summary>
    /// <typeparam name="T">The type of the setting.</typeparam>
    /// <param name="yamlStore">The YAML store containing the setting.</param>
    /// <param name="keyPath">The dot-notation path to the setting.</param>
    /// <param name="newValue">If not null, the new value to set. If null, the method acts as a getter.</param>
    /// <returns>The setting value, or default(T) if not found.</returns>
    T? GetSetting<T>(YamlStore yamlStore, string keyPath, T? newValue = default);
}