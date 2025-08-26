namespace Scanner111.Core.Configuration;

/// <summary>
/// Synchronous wrapper for AsyncYamlSettingsCore.
/// Provides a sync interface to the async YAML settings core for compatibility with sync code paths.
/// </summary>
public class YamlSettingsCache : IYamlSettingsCache
{
    private readonly IAsyncYamlSettingsCore _asyncCore;
    
    public YamlSettingsCache(IAsyncYamlSettingsCore asyncCore)
    {
        _asyncCore = asyncCore ?? throw new ArgumentNullException(nameof(asyncCore));
    }
    
    /// <inheritdoc/>
    public string GetPathForStore(YamlStore yamlStore)
    {
        return Task.Run(async () => await _asyncCore.GetPathForStoreAsync(yamlStore).ConfigureAwait(false))
            .GetAwaiter()
            .GetResult();
    }
    
    /// <inheritdoc/>
    public Dictionary<string, object?> LoadYaml(string yamlPath)
    {
        return Task.Run(async () => await _asyncCore.LoadYamlAsync(yamlPath).ConfigureAwait(false))
            .GetAwaiter()
            .GetResult();
    }
    
    /// <inheritdoc/>
    public T? GetSetting<T>(YamlStore yamlStore, string keyPath, T? newValue = default)
    {
        return Task.Run(async () => 
                await _asyncCore.GetSettingAsync(yamlStore, keyPath, newValue).ConfigureAwait(false))
            .GetAwaiter()
            .GetResult();
    }
    
    /// <inheritdoc/>
    public Dictionary<YamlStore, Dictionary<string, object?>> LoadMultipleStores(IEnumerable<YamlStore> stores)
    {
        return Task.Run(async () => 
                await _asyncCore.LoadMultipleStoresAsync(stores).ConfigureAwait(false))
            .GetAwaiter()
            .GetResult();
    }
    
    /// <inheritdoc/>
    public List<object?> BatchGetSettings(IEnumerable<(YamlStore store, string keyPath)> requests)
    {
        return Task.Run(async () => 
                await _asyncCore.BatchGetSettingsAsync(requests).ConfigureAwait(false))
            .GetAwaiter()
            .GetResult();
    }
    
    /// <inheritdoc/>
    public void PrefetchAllSettings()
    {
        Task.Run(async () => await _asyncCore.PrefetchAllSettingsAsync().ConfigureAwait(false))
            .GetAwaiter()
            .GetResult();
    }
    
    /// <inheritdoc/>
    public void ClearCache()
    {
        _asyncCore.ClearCache();
    }
    
    /// <inheritdoc/>
    public IReadOnlyDictionary<string, long> GetMetrics()
    {
        return _asyncCore.GetMetrics();
    }
}