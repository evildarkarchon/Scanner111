using System;

namespace Scanner111.Core.Configuration;

/// <summary>
///     Synchronous interface for YAML settings access.
/// </summary>
[Obsolete("Use IAsyncYamlSettingsCore instead. This synchronous interface is only for porting assistance and will be removed in a future version.", false)]
public interface IYamlSettingsCache
{
    /// <summary>
    ///     Gets the file path for a given YAML configuration store.
    /// </summary>
    [Obsolete("Use IAsyncYamlSettingsCore.GetPathForStoreAsync instead. This synchronous method is only for porting assistance.", false)]
    string GetPathForStore(YamlStore yamlStore);

    /// <summary>
    ///     Loads the content of a YAML file with caching.
    /// </summary>
    [Obsolete("Use IAsyncYamlSettingsCore.LoadYamlAsync instead. This synchronous method is only for porting assistance.", false)]
    Dictionary<string, object?> LoadYaml(string yamlPath);

    /// <summary>
    ///     Retrieves or updates a setting from a YAML store.
    /// </summary>
    [Obsolete("Use IAsyncYamlSettingsCore.GetSettingAsync instead. This synchronous method is only for porting assistance.", false)]
    T? GetSetting<T>(YamlStore yamlStore, string keyPath, T? newValue = default);

    /// <summary>
    ///     Load multiple YAML stores.
    /// </summary>
    [Obsolete("Use IAsyncYamlSettingsCore.LoadMultipleStoresAsync instead. This synchronous method is only for porting assistance.", false)]
    Dictionary<YamlStore, Dictionary<string, object?>> LoadMultipleStores(IEnumerable<YamlStore> stores);

    /// <summary>
    ///     Batch get multiple settings.
    /// </summary>
    [Obsolete("Use IAsyncYamlSettingsCore.BatchGetSettingsAsync instead. This synchronous method is only for porting assistance.", false)]
    List<object?> BatchGetSettings(IEnumerable<(YamlStore store, string keyPath)> requests);

    /// <summary>
    ///     Prefetch all common settings for better performance.
    /// </summary>
    [Obsolete("Use IAsyncYamlSettingsCore.PrefetchAllSettingsAsync instead. This synchronous method is only for porting assistance.", false)]
    void PrefetchAllSettings();

    /// <summary>
    ///     Clear all cached data.
    /// </summary>
    void ClearCache();

    /// <summary>
    ///     Get performance metrics.
    /// </summary>
    IReadOnlyDictionary<string, long> GetMetrics();
}