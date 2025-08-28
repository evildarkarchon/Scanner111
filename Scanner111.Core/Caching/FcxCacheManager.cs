using System.Collections.Concurrent;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using Microsoft.Extensions.Logging;

namespace Scanner111.Core.Caching;

/// <summary>
///     Interface for FCX cache management with advanced invalidation strategies.
/// </summary>
public interface IFcxCacheManager
{
    /// <summary>
    ///     Gets a cached value or computes it if not present.
    /// </summary>
    Task<T> GetOrAddAsync<T>(
        string key,
        Func<Task<T>> factory,
        CacheEntryOptions? options = null,
        CancellationToken cancellationToken = default) where T : class;

    /// <summary>
    ///     Tries to get a cached value.
    /// </summary>
    Task<(bool Found, T? Value)> TryGetAsync<T>(string key) where T : class;

    /// <summary>
    ///     Sets a value in the cache.
    /// </summary>
    Task SetAsync<T>(string key, T value, CacheEntryOptions? options = null) where T : class;

    /// <summary>
    ///     Removes a value from the cache.
    /// </summary>
    Task<bool> RemoveAsync(string key);

    /// <summary>
    ///     Clears all cached values.
    /// </summary>
    Task ClearAsync();

    /// <summary>
    ///     Invalidates cache entries matching a pattern.
    /// </summary>
    Task InvalidateAsync(string pattern);

    /// <summary>
    ///     Gets cache statistics.
    /// </summary>
    CacheStatistics GetStatistics();

    /// <summary>
    ///     Warms the cache with precomputed values.
    /// </summary>
    Task WarmCacheAsync(IEnumerable<(string Key, object Value)> entries);
}

/// <summary>
///     Advanced cache manager for FCX mode with versioning and multiple invalidation strategies.
/// </summary>
public sealed class FcxCacheManager : IFcxCacheManager, IDisposable
{
    private readonly ILogger<FcxCacheManager> _logger;
    private readonly ConcurrentDictionary<string, CacheEntry> _cache;
    private readonly ConcurrentDictionary<string, SemaphoreSlim> _keyLocks;
    private readonly ReaderWriterLockSlim _globalLock;
    private readonly Timer _cleanupTimer;
    private readonly CacheStatistics _statistics;
    private readonly int _maxCacheSize;
    private readonly TimeSpan _defaultExpiration;
    private int _version;
    private bool _disposed;

    public FcxCacheManager(
        ILogger<FcxCacheManager> logger,
        int maxCacheSize = 1000,
        TimeSpan? defaultExpiration = null)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _cache = new ConcurrentDictionary<string, CacheEntry>(StringComparer.OrdinalIgnoreCase);
        _keyLocks = new ConcurrentDictionary<string, SemaphoreSlim>(StringComparer.OrdinalIgnoreCase);
        _globalLock = new ReaderWriterLockSlim();
        _statistics = new CacheStatistics();
        _maxCacheSize = maxCacheSize;
        _defaultExpiration = defaultExpiration ?? TimeSpan.FromMinutes(5);
        _version = 0;

        // Setup cleanup timer to run every minute
        _cleanupTimer = new Timer(
            CleanupExpiredEntries,
            null,
            TimeSpan.FromMinutes(1),
            TimeSpan.FromMinutes(1));

        _logger.LogDebug("FcxCacheManager initialized with max size: {MaxSize}, default expiration: {Expiration}",
            maxCacheSize, _defaultExpiration);
    }

    /// <inheritdoc />
    public async Task<T> GetOrAddAsync<T>(
        string key,
        Func<Task<T>> factory,
        CacheEntryOptions? options = null,
        CancellationToken cancellationToken = default) where T : class
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(key);
        ArgumentNullException.ThrowIfNull(factory);
        ThrowIfDisposed();

        // Try to get from cache first (fast path)
        _globalLock.EnterReadLock();
        try
        {
            if (_cache.TryGetValue(key, out var entry) && !entry.IsExpired && entry.Version == _version)
            {
                _statistics.RecordHit();
                _logger.LogTrace("Cache hit for key: {Key}", key);
                return (T)entry.Value;
            }
        }
        finally
        {
            _globalLock.ExitReadLock();
        }

        // Cache miss - need to compute value
        _statistics.RecordMiss();
        
        // Get or create a lock for this specific key to prevent concurrent factory calls
        var keyLock = _keyLocks.GetOrAdd(key, _ => new SemaphoreSlim(1, 1));
        
        await keyLock.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            // Double-check after acquiring lock
            _globalLock.EnterReadLock();
            try
            {
                if (_cache.TryGetValue(key, out var entry) && !entry.IsExpired && entry.Version == _version)
                {
                    _statistics.RecordHit();
                    return (T)entry.Value;
                }
            }
            finally
            {
                _globalLock.ExitReadLock();
            }

            // Compute the value
            var stopwatch = Stopwatch.StartNew();
            var value = await factory().ConfigureAwait(false);
            stopwatch.Stop();

            _logger.LogDebug("Computed value for key {Key} in {ElapsedMs}ms", key, stopwatch.ElapsedMilliseconds);

            // Store in cache
            await SetInternalAsync(key, value, options ?? new CacheEntryOptions()).ConfigureAwait(false);

            return value;
        }
        finally
        {
            keyLock.Release();
            
            // Clean up the key lock if no longer needed
            if (keyLock.CurrentCount == 1)
            {
                _keyLocks.TryRemove(key, out _);
                keyLock.Dispose();
            }
        }
    }

    /// <inheritdoc />
    public Task<(bool Found, T? Value)> TryGetAsync<T>(string key) where T : class
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(key);
        ThrowIfDisposed();

        _globalLock.EnterReadLock();
        try
        {
            if (_cache.TryGetValue(key, out var entry) && !entry.IsExpired && entry.Version == _version)
            {
                _statistics.RecordHit();
                return Task.FromResult((true, (T?)entry.Value));
            }

            _statistics.RecordMiss();
            return Task.FromResult((false, (T?)null));
        }
        finally
        {
            _globalLock.ExitReadLock();
        }
    }

    /// <inheritdoc />
    public Task SetAsync<T>(string key, T value, CacheEntryOptions? options = null) where T : class
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(key);
        ArgumentNullException.ThrowIfNull(value);
        ThrowIfDisposed();

        return SetInternalAsync(key, value, options ?? new CacheEntryOptions());
    }

    /// <inheritdoc />
    public Task<bool> RemoveAsync(string key)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(key);
        ThrowIfDisposed();

        _globalLock.EnterWriteLock();
        try
        {
            var removed = _cache.TryRemove(key, out _);
            if (removed)
            {
                _statistics.RecordEviction();
                _logger.LogDebug("Removed cache entry for key: {Key}", key);
            }
            return Task.FromResult(removed);
        }
        finally
        {
            _globalLock.ExitWriteLock();
        }
    }

    /// <inheritdoc />
    public Task ClearAsync()
    {
        ThrowIfDisposed();

        _globalLock.EnterWriteLock();
        try
        {
            var count = _cache.Count;
            _cache.Clear();
            Interlocked.Increment(ref _version);
            _statistics.RecordClear(count);
            _logger.LogInformation("Cleared {Count} cache entries", count);
            return Task.CompletedTask;
        }
        finally
        {
            _globalLock.ExitWriteLock();
        }
    }

    /// <inheritdoc />
    public Task InvalidateAsync(string pattern)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(pattern);
        ThrowIfDisposed();

        _globalLock.EnterWriteLock();
        try
        {
            var keysToRemove = _cache.Keys
                .Where(k => k.Contains(pattern, StringComparison.OrdinalIgnoreCase))
                .ToList();

            foreach (var key in keysToRemove)
            {
                _cache.TryRemove(key, out _);
                _statistics.RecordEviction();
            }

            if (keysToRemove.Count > 0)
            {
                _logger.LogInformation("Invalidated {Count} cache entries matching pattern: {Pattern}",
                    keysToRemove.Count, pattern);
            }

            return Task.CompletedTask;
        }
        finally
        {
            _globalLock.ExitWriteLock();
        }
    }

    /// <inheritdoc />
    public CacheStatistics GetStatistics()
    {
        ThrowIfDisposed();
        return _statistics.Clone();
    }

    /// <inheritdoc />
    public async Task WarmCacheAsync(IEnumerable<(string Key, object Value)> entries)
    {
        ArgumentNullException.ThrowIfNull(entries);
        ThrowIfDisposed();

        var tasks = new List<Task>();
        foreach (var (key, value) in entries)
        {
            tasks.Add(SetAsync(key, value));
        }

        await Task.WhenAll(tasks).ConfigureAwait(false);
        _logger.LogInformation("Warmed cache with {Count} entries", tasks.Count);
    }

    public void Dispose()
    {
        if (_disposed)
            return;

        _cleanupTimer?.Dispose();
        _globalLock?.Dispose();

        foreach (var keyLock in _keyLocks.Values)
        {
            keyLock?.Dispose();
        }
        _keyLocks.Clear();

        _cache.Clear();
        _disposed = true;

        _logger.LogDebug("FcxCacheManager disposed");
    }

    private async Task SetInternalAsync<T>(string key, T value, CacheEntryOptions options) where T : class
    {
        await Task.Yield(); // Ensure async context

        _globalLock.EnterWriteLock();
        try
        {
            // Check cache size and evict if necessary
            if (_cache.Count >= _maxCacheSize)
            {
                EvictLeastRecentlyUsed();
            }

            var expiration = options.AbsoluteExpiration ?? 
                            (options.SlidingExpiration.HasValue 
                                ? DateTime.UtcNow.Add(options.SlidingExpiration.Value)
                                : DateTime.UtcNow.Add(_defaultExpiration));

            var entry = new CacheEntry
            {
                Key = key,
                Value = value,
                CreatedAt = DateTime.UtcNow,
                LastAccessedAt = DateTime.UtcNow,
                ExpiresAt = expiration,
                Version = _version,
                Priority = options.Priority
            };

            _cache[key] = entry;
            _logger.LogTrace("Set cache entry for key: {Key}, expires: {ExpiresAt}", key, expiration);
        }
        finally
        {
            _globalLock.ExitWriteLock();
        }
    }

    private void EvictLeastRecentlyUsed()
    {
        // Find entries to evict (LRU with priority consideration)
        var entriesToEvict = _cache.Values
            .Where(e => e.Priority != CachePriority.High)
            .OrderBy(e => e.Priority)
            .ThenBy(e => e.LastAccessedAt)
            .Take(_maxCacheSize / 10) // Evict 10% of cache
            .Select(e => e.Key)
            .ToList();

        foreach (var key in entriesToEvict)
        {
            _cache.TryRemove(key, out _);
            _statistics.RecordEviction();
        }

        if (entriesToEvict.Count > 0)
        {
            _logger.LogDebug("Evicted {Count} LRU cache entries", entriesToEvict.Count);
        }
    }

    private void CleanupExpiredEntries(object? state)
    {
        if (_disposed)
            return;

        _globalLock.EnterWriteLock();
        try
        {
            var expiredKeys = _cache
                .Where(kvp => kvp.Value.IsExpired)
                .Select(kvp => kvp.Key)
                .ToList();

            foreach (var key in expiredKeys)
            {
                _cache.TryRemove(key, out _);
                _statistics.RecordEviction();
            }

            if (expiredKeys.Count > 0)
            {
                _logger.LogDebug("Cleaned up {Count} expired cache entries", expiredKeys.Count);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error during cache cleanup");
        }
        finally
        {
            _globalLock.ExitWriteLock();
        }
    }

    private void ThrowIfDisposed()
    {
        if (_disposed)
            throw new ObjectDisposedException(nameof(FcxCacheManager));
    }

    private sealed class CacheEntry
    {
        public required string Key { get; init; }
        public required object Value { get; init; }
        public required DateTime CreatedAt { get; init; }
        public required DateTime LastAccessedAt { get; set; }
        public required DateTime ExpiresAt { get; init; }
        public required int Version { get; init; }
        public CachePriority Priority { get; init; } = CachePriority.Normal;

        public bool IsExpired => DateTime.UtcNow > ExpiresAt;
    }
}

/// <summary>
///     Options for cache entries.
/// </summary>
public sealed class CacheEntryOptions
{
    /// <summary>
    ///     Absolute expiration time.
    /// </summary>
    public DateTime? AbsoluteExpiration { get; init; }

    /// <summary>
    ///     Sliding expiration duration.
    /// </summary>
    public TimeSpan? SlidingExpiration { get; init; }

    /// <summary>
    ///     Priority for eviction.
    /// </summary>
    public CachePriority Priority { get; init; } = CachePriority.Normal;

    /// <summary>
    ///     Tags for grouping cache entries.
    /// </summary>
    public HashSet<string> Tags { get; } = new();
}

/// <summary>
///     Cache entry priority for eviction.
/// </summary>
public enum CachePriority
{
    Low,
    Normal,
    High
}

/// <summary>
///     Statistics for cache operations.
/// </summary>
public sealed class CacheStatistics
{
    private long _hits;
    private long _misses;
    private long _evictions;
    private long _clears;

    /// <summary>
    ///     Total cache hits.
    /// </summary>
    public long Hits => Interlocked.Read(ref _hits);

    /// <summary>
    ///     Total cache misses.
    /// </summary>
    public long Misses => Interlocked.Read(ref _misses);

    /// <summary>
    ///     Total evictions.
    /// </summary>
    public long Evictions => Interlocked.Read(ref _evictions);

    /// <summary>
    ///     Total clear operations.
    /// </summary>
    public long Clears => Interlocked.Read(ref _clears);

    /// <summary>
    ///     Cache hit ratio (0-1).
    /// </summary>
    public double HitRatio
    {
        get
        {
            var totalRequests = Hits + Misses;
            return totalRequests > 0 ? (double)Hits / totalRequests : 0;
        }
    }

    internal void RecordHit() => Interlocked.Increment(ref _hits);
    internal void RecordMiss() => Interlocked.Increment(ref _misses);
    internal void RecordEviction() => Interlocked.Increment(ref _evictions);
    internal void RecordClear(int count)
    {
        Interlocked.Increment(ref _clears);
        Interlocked.Add(ref _evictions, count);
    }

    internal CacheStatistics Clone()
    {
        return new CacheStatistics
        {
            _hits = Hits,
            _misses = Misses,
            _evictions = Evictions,
            _clears = Clears
        };
    }
}