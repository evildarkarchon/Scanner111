using System;

namespace Scanner111.Core.Configuration;

/// <summary>
///     Synchronous wrapper for AsyncYamlSettingsCore.
///     Provides a sync interface to the async YAML settings core for compatibility with sync code paths.
/// </summary>
[Obsolete("Use IAsyncYamlSettingsCore directly. This synchronous wrapper is only for porting assistance and will be removed in a future version.", false)]
public class YamlSettingsCache : IYamlSettingsCache
{
    private readonly IAsyncYamlSettingsCore _asyncCore;

    public YamlSettingsCache(IAsyncYamlSettingsCore asyncCore)
    {
        _asyncCore = asyncCore ?? throw new ArgumentNullException(nameof(asyncCore));
    }

    /// <inheritdoc />
    [Obsolete("Use IAsyncYamlSettingsCore.GetPathForStoreAsync instead. This synchronous method is only for porting assistance.", false)]
    public string GetPathForStore(YamlStore yamlStore)
    {
        return Task.Run(async () => await _asyncCore.GetPathForStoreAsync(yamlStore).ConfigureAwait(false))
            .GetAwaiter()
            .GetResult();
    }

    /// <inheritdoc />
    [Obsolete("Use IAsyncYamlSettingsCore.LoadYamlAsync instead. This synchronous method is only for porting assistance.", false)]
    public Dictionary<string, object?> LoadYaml(string yamlPath)
    {
        return Task.Run(async () => await _asyncCore.LoadYamlAsync(yamlPath).ConfigureAwait(false))
            .GetAwaiter()
            .GetResult();
    }

    /// <inheritdoc />
    [Obsolete("Use IAsyncYamlSettingsCore.GetSettingAsync instead. This synchronous method is only for porting assistance.", false)]
    public T? GetSetting<T>(YamlStore yamlStore, string keyPath, T? newValue = default)
    {
        return Task.Run(async () =>
                await _asyncCore.GetSettingAsync(yamlStore, keyPath, newValue).ConfigureAwait(false))
            .GetAwaiter()
            .GetResult();
    }

    /// <inheritdoc />
    [Obsolete("Use IAsyncYamlSettingsCore.LoadMultipleStoresAsync instead. This synchronous method is only for porting assistance.", false)]
    public Dictionary<YamlStore, Dictionary<string, object?>> LoadMultipleStores(IEnumerable<YamlStore> stores)
    {
        return Task.Run(async () =>
                await _asyncCore.LoadMultipleStoresAsync(stores).ConfigureAwait(false))
            .GetAwaiter()
            .GetResult();
    }

    /// <inheritdoc />
    [Obsolete("Use IAsyncYamlSettingsCore.BatchGetSettingsAsync instead. This synchronous method is only for porting assistance.", false)]
    public List<object?> BatchGetSettings(IEnumerable<(YamlStore store, string keyPath)> requests)
    {
        return Task.Run(async () =>
                await _asyncCore.BatchGetSettingsAsync(requests).ConfigureAwait(false))
            .GetAwaiter()
            .GetResult();
    }

    /// <inheritdoc />
    [Obsolete("Use IAsyncYamlSettingsCore.PrefetchAllSettingsAsync instead. This synchronous method is only for porting assistance.", false)]
    public void PrefetchAllSettings()
    {
        Task.Run(async () => await _asyncCore.PrefetchAllSettingsAsync().ConfigureAwait(false))
            .GetAwaiter()
            .GetResult();
    }

    /// <inheritdoc />
    public void ClearCache()
    {
        _asyncCore.ClearCache();
    }

    /// <inheritdoc />
    public IReadOnlyDictionary<string, long> GetMetrics()
    {
        return _asyncCore.GetMetrics();
    }
}