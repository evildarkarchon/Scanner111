namespace Scanner111.Core.Infrastructure;

/// <summary>
///     Backward compatibility adapter for the static YamlSettingsCache API
///     Uses the new singleton YamlSettingsService internally
/// </summary>
public static class YamlSettingsCache
{
    private static volatile IYamlSettingsProvider? _instance;
    private static readonly object InitLock = new();

    /// <summary>
    ///     Initialize the static cache with a service instance
    ///     Called by DI container during startup or for testing
    /// </summary>
    internal static void Initialize(IYamlSettingsProvider yamlSettingsProvider)
    {
        lock (InitLock)
        {
            _instance = yamlSettingsProvider;
        }
    }

    /// <summary>
    ///     Reset the static cache (for testing)
    /// </summary>
    internal static void Reset()
    {
        lock (InitLock)
        {
            _instance = null;
        }
    }

    /// <summary>
    /// Provides functionality for caching and retrieving setting values from YAML files.
    /// </summary>
    /// <typeparam name="T">Type of the value being retrieved from the settings.</typeparam>
    /// <param name="yamlFile">The name of the YAML file (without file extension) to load settings from.</param>
    /// <param name="keyPath">The dot-separated key path used to identify the specific setting in the YAML file.</param>
    /// <param name="defaultValue">The default value to return if the key is not found in the YAML file.</param>
    /// <returns>The value of the requested setting if found; otherwise, the provided default value.</returns>
    public static T? YamlSettings<T>(string yamlFile, string keyPath, T? defaultValue = default)
    {
        if (_instance == null)
            throw new InvalidOperationException(
                "YamlSettingsCache not initialized. Ensure services are configured properly.");

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
            throw new InvalidOperationException(
                "YamlSettingsCache not initialized. Ensure services are configured properly.");

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