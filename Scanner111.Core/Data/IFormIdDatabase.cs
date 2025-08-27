using System;
using System.Threading;
using System.Threading.Tasks;

namespace Scanner111.Core.Data;

/// <summary>
/// Defines the contract for FormID database access with async support.
/// </summary>
public interface IFormIdDatabase : IAsyncDisposable
{
    /// <summary>
    /// Gets whether the database has been initialized and is available.
    /// </summary>
    bool IsAvailable { get; }
    
    /// <summary>
    /// Gets the number of cached entries.
    /// </summary>
    int CachedEntryCount { get; }
    
    /// <summary>
    /// Gets the maximum number of concurrent connections allowed.
    /// </summary>
    int MaxConnections { get; }
    
    /// <summary>
    /// Initializes the database connections and prepares for queries.
    /// </summary>
    /// <param name="cancellationToken">Token to cancel the initialization.</param>
    /// <returns>A task representing the initialization operation.</returns>
    Task InitializeAsync(CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Retrieves a FormID entry from the database or cache.
    /// Thread-safe and supports concurrent access.
    /// </summary>
    /// <param name="formId">The FormID to look up (without prefix).</param>
    /// <param name="plugin">The plugin name associated with the FormID.</param>
    /// <param name="cancellationToken">Token to cancel the operation.</param>
    /// <returns>The FormID description if found, null otherwise.</returns>
    Task<string?> GetEntryAsync(string formId, string plugin, CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Attempts to get multiple entries in parallel for better performance.
    /// </summary>
    /// <param name="queries">Array of FormID and plugin pairs to query.</param>
    /// <param name="cancellationToken">Token to cancel the operation.</param>
    /// <returns>Array of results corresponding to each query.</returns>
    Task<string?[]> GetEntriesAsync((string formId, string plugin)[] queries, CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Clears the in-memory cache of FormID entries.
    /// </summary>
    void ClearCache();
    
    /// <summary>
    /// Gets statistics about database usage for monitoring.
    /// </summary>
    /// <returns>Database usage statistics.</returns>
    FormIdDatabaseStatistics GetStatistics();
}

/// <summary>
/// Statistics about FormID database usage.
/// </summary>
public sealed record FormIdDatabaseStatistics
{
    /// <summary>
    /// Gets the total number of queries executed.
    /// </summary>
    public long TotalQueries { get; init; }
    
    /// <summary>
    /// Gets the number of cache hits.
    /// </summary>
    public long CacheHits { get; init; }
    
    /// <summary>
    /// Gets the number of cache misses.
    /// </summary>
    public long CacheMisses { get; init; }
    
    /// <summary>
    /// Gets the cache hit ratio (0.0 to 1.0).
    /// </summary>
    public double CacheHitRatio => TotalQueries > 0 ? (double)CacheHits / TotalQueries : 0;
    
    /// <summary>
    /// Gets the number of database errors encountered.
    /// </summary>
    public long DatabaseErrors { get; init; }
    
    /// <summary>
    /// Gets the average query time in milliseconds.
    /// </summary>
    public double AverageQueryTimeMs { get; init; }
    
    /// <summary>
    /// Gets the number of active connections.
    /// </summary>
    public int ActiveConnections { get; init; }
}