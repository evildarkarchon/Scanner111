namespace Scanner111.Core.Configuration;

/// <summary>
///     Defines the contract for async-first YAML settings management with caching and concurrency control.
/// </summary>
public interface IAsyncYamlSettingsCore : IAsyncDisposable
{
    /// <summary>
    ///     Gets the file path for a given YAML configuration store.
    /// </summary>
    /// <param name="yamlStore">The identifier for the configuration type.</param>
    /// <param name="cancellationToken">Cancellation token for the operation.</param>
    /// <returns>The resolved file path corresponding to the provided YAML store.</returns>
    Task<string> GetPathForStoreAsync(YamlStore yamlStore, CancellationToken cancellationToken = default);

    /// <summary>
    ///     Loads the content of a YAML file with caching.
    ///     Static files are cached permanently, dynamic files use TTL-based cache invalidation.
    /// </summary>
    /// <param name="yamlPath">The path to the YAML file to be loaded.</param>
    /// <param name="cancellationToken">Cancellation token for the operation.</param>
    /// <returns>The content of the YAML file as a dictionary.</returns>
    Task<Dictionary<string, object?>> LoadYamlAsync(string yamlPath, CancellationToken cancellationToken = default);

    /// <summary>
    ///     Retrieves or updates a setting from a YAML store.
    /// </summary>
    /// <typeparam name="T">The expected type of the setting value.</typeparam>
    /// <param name="yamlStore">The YAML store from which the setting is retrieved or updated.</param>
    /// <param name="keyPath">The dot-delimited path specifying the location of the setting.</param>
    /// <param name="newValue">The new value to update the setting with (null for read-only).</param>
    /// <param name="cancellationToken">Cancellation token for the operation.</param>
    /// <returns>The existing or updated setting value if successful, otherwise default(T).</returns>
    /// <exception cref="InvalidOperationException">If attempting to modify a static YAML store.</exception>
    Task<T?> GetSettingAsync<T>(YamlStore yamlStore, string keyPath, T? newValue = default,
        CancellationToken cancellationToken = default);

    /// <summary>
    ///     Load multiple YAML stores concurrently.
    /// </summary>
    /// <param name="stores">List of YAML stores to load.</param>
    /// <param name="cancellationToken">Cancellation token for the operation.</param>
    /// <returns>Dictionary mapping YAML stores to their loaded data.</returns>
    Task<Dictionary<YamlStore, Dictionary<string, object?>>> LoadMultipleStoresAsync(
        IEnumerable<YamlStore> stores, CancellationToken cancellationToken = default);

    /// <summary>
    ///     Batch get multiple settings concurrently.
    /// </summary>
    /// <param name="requests">List of setting requests as tuples of (yamlStore, keyPath).</param>
    /// <param name="cancellationToken">Cancellation token for the operation.</param>
    /// <returns>List of setting values in the same order as requests.</returns>
    Task<List<object?>> BatchGetSettingsAsync(
        IEnumerable<(YamlStore store, string keyPath)> requests,
        CancellationToken cancellationToken = default);

    /// <summary>
    ///     Prefetch all common settings at startup for better performance.
    ///     Loads all commonly used YAML stores concurrently to warm up the cache.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token for the operation.</param>
    Task PrefetchAllSettingsAsync(CancellationToken cancellationToken = default);

    /// <summary>
    ///     Clear all cached data.
    /// </summary>
    void ClearCache();

    /// <summary>
    ///     Get performance metrics for monitoring.
    /// </summary>
    /// <returns>Dictionary containing performance metrics like cache hits, misses, etc.</returns>
    IReadOnlyDictionary<string, long> GetMetrics();
}