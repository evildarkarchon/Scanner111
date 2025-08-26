namespace Scanner111.Core.Configuration;

/// <summary>
/// Synchronous interface for YAML settings access.
/// </summary>
public interface IYamlSettingsCache
{
    /// <summary>
    /// Gets the file path for a given YAML configuration store.
    /// </summary>
    string GetPathForStore(YamlStore yamlStore);
    
    /// <summary>
    /// Loads the content of a YAML file with caching.
    /// </summary>
    Dictionary<string, object?> LoadYaml(string yamlPath);
    
    /// <summary>
    /// Retrieves or updates a setting from a YAML store.
    /// </summary>
    T? GetSetting<T>(YamlStore yamlStore, string keyPath, T? newValue = default);
    
    /// <summary>
    /// Load multiple YAML stores.
    /// </summary>
    Dictionary<YamlStore, Dictionary<string, object?>> LoadMultipleStores(IEnumerable<YamlStore> stores);
    
    /// <summary>
    /// Batch get multiple settings.
    /// </summary>
    List<object?> BatchGetSettings(IEnumerable<(YamlStore store, string keyPath)> requests);
    
    /// <summary>
    /// Prefetch all common settings for better performance.
    /// </summary>
    void PrefetchAllSettings();
    
    /// <summary>
    /// Clear all cached data.
    /// </summary>
    void ClearCache();
    
    /// <summary>
    /// Get performance metrics.
    /// </summary>
    IReadOnlyDictionary<string, long> GetMetrics();
}