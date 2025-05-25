using System;
using System.IO;

namespace Scanner111.ClassicLib;

/// <summary>
/// Provides access to YAML settings across the application.
/// </summary>
public class YamlSettings
{
    private readonly YamlSettingsCache _cache;

    /// <summary>
    /// Initializes a new instance of the <see cref="YamlSettings"/> class.
    /// </summary>
    /// <param name="cache">The YAML settings cache.</param>
    public YamlSettings(YamlSettingsCache cache)
    {
        _cache = cache ?? throw new ArgumentNullException(nameof(cache));
    }

    /// <summary>
    /// Manages YAML settings by retrieving or updating a specific setting in the YAML store.
    /// </summary>
    /// <typeparam name="T">The expected type of the setting value.</typeparam>
    /// <param name="yamlStore">The YAML store from which the setting is retrieved or updated.</param>
    /// <param name="keyPath">The dot-delimited path specifying the location of the setting within the YAML structure.</param>
    /// <param name="newValue">The new value to update the setting with. If null or default, the method operates as a read.</param>
    /// <returns>The value of the setting retrieved from the YAML store.</returns>
    public T? GetSetting<T>(YamlStoreType yamlStore, string keyPath, T? newValue = default)
    {
        var setting = _cache.GetSetting(yamlStore, keyPath, newValue);

        // Special handling for Path type
        if (typeof(T) != typeof(string) || setting == null || string.IsNullOrEmpty(setting.ToString()))
            return setting;
        try
        {
            return (T)(object)Path.GetFullPath(setting.ToString()!);
        }
        catch
        {
            return default;
        }
    }
}