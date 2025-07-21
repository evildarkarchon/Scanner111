namespace Scanner111.Core.Infrastructure;

/// <summary>
/// Backward compatibility adapter for the static YamlSettingsCache API
/// Uses the new singleton YamlSettingsService internally
/// </summary>
public static class YamlSettingsCache
{
    private static volatile IYamlSettingsProvider? _instance;
    private static readonly object _initLock = new object();
    
    /// <summary>
    /// Initialize the static cache with a service instance
    /// Called by DI container during startup or for testing
    /// </summary>
    internal static void Initialize(IYamlSettingsProvider yamlSettingsProvider)
    {
        lock (_initLock)
        {
            _instance = yamlSettingsProvider;
        }
    }
    
    /// <summary>
    /// Reset the static cache (for testing)
    /// </summary>
    internal static void Reset()
    {
        lock (_initLock)
        {
            _instance = null;
        }
    }
    
    /// <summary>
    /// Get a setting value from a YAML file with caching
    /// </summary>
    /// <typeparam name="T">Type of value to return</typeparam>
    /// <param name="yamlFile">YAML filename (without extension)</param>
    /// <param name="keyPath">Dot-separated path to the setting (e.g., "CLASSIC_Settings.Show FormID Values")</param>
    /// <param name="defaultValue">Default value if not found</param>
    /// <returns>The setting value or default</returns>
    public static T? YamlSettings<T>(string yamlFile, string keyPath, T? defaultValue = default)
    {
        if (_instance == null)
            throw new InvalidOperationException("YamlSettingsCache not initialized. Ensure services are configured properly.");
        
        return _instance.GetSetting(yamlFile, keyPath, defaultValue);
    }
    
    /// <summary>
    /// Set a setting value in memory cache
    /// </summary>
    /// <typeparam name="T">Type of value to set</typeparam>
    /// <param name="yamlFile">YAML filename (without extension)</param>
    /// <param name="keyPath">Dot-separated path to the setting</param>
    /// <param name="value">Value to set</param>
    public static void SetYamlSetting<T>(string yamlFile, string keyPath, T value)
    {
        if (_instance == null)
            throw new InvalidOperationException("YamlSettingsCache not initialized. Ensure services are configured properly.");
        
        _instance.SetSetting(yamlFile, keyPath, value);
    }
    
    /// <summary>
    /// Clear the settings cache
    /// </summary>
    public static void ClearCache()
    {
        _instance?.ClearCache();
    }
}