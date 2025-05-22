using System;
using System.IO;

namespace Scanner111.Services;

/// <summary>
/// Helper methods for working with YAML settings.
/// </summary>
public static class YamlSettings
{
    /// <summary>
    /// Gets or sets a setting in a YAML store.
    /// </summary>
    /// <typeparam name="T">The type of the setting.</typeparam>
    /// <param name="yamlStore">The YAML store containing the setting.</param>
    /// <param name="keyPath">The dot-notation path to the setting.</param>
    /// <param name="newValue">If not null, the new value to set. If null, the method acts as a getter.</param>
    /// <returns>The setting value, or default(T) if not found.</returns>
    public static T? Get<T>(YamlStore yamlStore, string keyPath, T? newValue = default)
    {
        var yamlCache = GlobalRegistry.Get<IYamlSettingsCache>(GlobalRegistry.Keys.YamlCache);
        if (yamlCache == null)
        {
            throw new InvalidOperationException("YAML cache service not registered. Ensure it's properly configured in DI.");
        }
        
        var setting = yamlCache.GetSetting<T>(yamlStore, keyPath, newValue);
        
        // Special handling for Path type
        if (typeof(T) == typeof(string) && setting is string stringValue && !string.IsNullOrEmpty(stringValue))
        {
            return (T)(object)Path.GetFullPath(stringValue);
        }
        
        return setting;
    }
    
    /// <summary>
    /// Gets a setting from the CLASSIC Settings YAML store.
    /// </summary>
    /// <typeparam name="T">The type of the setting.</typeparam>
    /// <param name="setting">The setting key.</param>
    /// <returns>The setting value, or default(T) if not found.</returns>
    public static T? GetClassicSetting<T>(string setting)
    {
        string settingsPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "CLASSIC Settings.yaml");
        
        // Create settings file if it doesn't exist
        if (!File.Exists(settingsPath))
        {
            string? defaultSettings = Get<string>(YamlStore.Main, "CLASSIC_Info.default_settings");
            if (string.IsNullOrEmpty(defaultSettings))
            {
                throw new InvalidOperationException("Invalid Default Settings in 'CLASSIC Main.yaml'");
            }
            
            // Ensure directory exists
            string? directory = Path.GetDirectoryName(settingsPath);
            if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
            {
                Directory.CreateDirectory(directory);
            }
            
            File.WriteAllText(settingsPath, defaultSettings);
        }
        
        return Get<T>(YamlStore.Settings, $"CLASSIC_Settings.{setting}");
    }
}