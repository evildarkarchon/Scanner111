using System;
using System.IO;

namespace Scanner111.Services;

/// <summary>
/// Helper methods for working with YAML settings.
/// </summary>
public static class YamlSettings
{
    private static IYamlSettingsCache? _yamlSettingsCache;

    /// <summary>
    /// Initializes the YamlSettings class with the YAML settings cache service.
    /// This should be called at application startup.
    /// </summary>
    /// <param name="yamlSettingsCache">The YAML settings cache service.</param>
    public static void Initialize(IYamlSettingsCache yamlSettingsCache)
    {
        _yamlSettingsCache = yamlSettingsCache;
    }

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
        if (_yamlSettingsCache == null)
        {
            try
            {
                // Try to use the singleton YamlSettingsCache
                _yamlSettingsCache = YamlSettingsCache.Instance;
            }
            catch
            {
                // Fall back to service provider if the singleton isn't initialized
                if (App.ServiceProvider != null)
                {
                    _yamlSettingsCache = App.ServiceProvider.GetService(typeof(IYamlSettingsCache)) as IYamlSettingsCache;
                }
                
                if (_yamlSettingsCache == null)
                {
                    throw new InvalidOperationException("YAML cache service not registered. Ensure it's properly configured in DI and YamlSettings.Initialize has been called.");
                }
            }
        }
        
        var setting = _yamlSettingsCache.GetSetting(yamlStore, keyPath, newValue);
        
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
        var settingsPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "CLASSIC Settings.yaml");
        
        // Create settings file if it doesn't exist
        if (!File.Exists(settingsPath))
        {
            var defaultSettings = Get<string>(YamlStore.Main, "CLASSIC_Info.default_settings");
            if (string.IsNullOrEmpty(defaultSettings))
            {
                throw new InvalidOperationException("Invalid Default Settings in 'CLASSIC Main.yaml'");
            }
            
            // Ensure directory exists
            var directory = Path.GetDirectoryName(settingsPath);
            if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
            {
                Directory.CreateDirectory(directory);
            }
            
            File.WriteAllText(settingsPath, defaultSettings);
        }
        
        return Get<T>(YamlStore.Settings, $"CLASSIC_Settings.{setting}");
    }
}