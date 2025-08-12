using System.Collections.Concurrent;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;
using Scanner111.Core.Analyzers;

namespace Scanner111.Core.Infrastructure;

/// <summary>
///     Cache manager for settings and analysis results
/// </summary>
public interface ICacheManager
{
    /// <summary>
    ///     Get or set YAML settings with caching
    /// </summary>
    T? GetOrSetYamlSetting<T>(string yamlFile, string keyPath, Func<T?> factory, TimeSpan? expiry = null);

    /// <summary>
    ///     Get or set YAML settings with caching (async version)
    /// </summary>
    Task<T?> GetOrSetYamlSettingAsync<T>(string yamlFile, string keyPath, Func<Task<T?>> factory, TimeSpan? expiry = null);

    /// <summary>
    ///     Cache analysis result for a file
    /// </summary>
    void CacheAnalysisResult(string filePath, string analyzerName, AnalysisResult result);

    /// <summary>
    ///     Get cached analysis result
    /// </summary>
    AnalysisResult? GetCachedAnalysisResult(string filePath, string analyzerName);

    /// <summary>
    ///     Check if file has been modified since last cache
    /// </summary>
    bool IsFileCacheValid(string filePath);

    /// <summary>
    ///     Clear all caches
    /// </summary>
    void ClearCache();

    /// <summary>
    ///     Get cache statistics
    /// </summary>
    CacheStatistics GetStatistics();
}

/// <summary>
///     Implementation of cache manager
/// </summary>
public class CacheManager(IMemoryCache memoryCache, ILogger<CacheManager> logger) : ICacheManager, IDisposable
{
    // Cache key prefixes
    private const string YamlPrefix = "yaml:";
    private const string AnalysisPrefix = "analysis:";
    private const string FileTimePrefix = "filetime:";
    private readonly ConcurrentDictionary<string, int> _cacheHits = new();
    private readonly ConcurrentDictionary<string, int> _cacheMisses = new();
    private readonly ConcurrentDictionary<string, DateTime> _fileModificationTimes = new();
    private readonly ILogger<CacheManager> _logger = logger;
    private readonly IMemoryCache _memoryCache = memoryCache;
    private readonly object _statsLock = new();
    private bool _disposed;

    /// <summary>
    /// Retrieves a YAML setting from the cache or sets it using the provided factory function if not cached.
    /// </summary>
    /// <typeparam name="T">The type of the value to retrieve or set.</typeparam>
    /// <param name="yamlFile">The name of the YAML file containing the setting.</param>
    /// <param name="keyPath">The key path within the YAML file to locate the setting.</param>
    /// <param name="factory">A function to generate the value if it is not found in the cache.</param>
    /// <param name="expiry">An optional time span defining the cache entry expiration. Defaults to 30 minutes.</param>
    /// <returns>The cached or newly generated setting value.</returns>
    public T? GetOrSetYamlSetting<T>(string yamlFile, string keyPath, Func<T?> factory, TimeSpan? expiry = null)
    {
        CheckDisposed();
        var cacheKey = $"{YamlPrefix}{yamlFile}:{keyPath}";

        if (_memoryCache.TryGetValue(cacheKey, out T? cached))
        {
            RecordCacheHit(cacheKey);
            _logger.LogTrace("Cache hit for YAML setting: {CacheKey}", cacheKey);
            return cached;
        }

        RecordCacheMiss(cacheKey);
        _logger.LogTrace("Cache miss for YAML setting: {CacheKey}", cacheKey);

        var value = factory();
        var options = new MemoryCacheEntryOptions
        {
            AbsoluteExpirationRelativeToNow = expiry ?? TimeSpan.FromMinutes(30),
            Priority = CacheItemPriority.Normal
        };

        _memoryCache.Set(cacheKey, value, options);
        return value;
    }

    public async Task<T?> GetOrSetYamlSettingAsync<T>(string yamlFile, string keyPath, Func<Task<T?>> factory, TimeSpan? expiry = null)
    {
        CheckDisposed();
        var cacheKey = $"{YamlPrefix}{yamlFile}:{keyPath}";

        if (_memoryCache.TryGetValue(cacheKey, out T? cached))
        {
            RecordCacheHit(cacheKey);
            _logger.LogTrace("Cache hit for YAML setting: {CacheKey}", cacheKey);
            return cached;
        }

        RecordCacheMiss(cacheKey);
        _logger.LogTrace("Cache miss for YAML setting: {CacheKey}", cacheKey);

        var value = await factory().ConfigureAwait(false);
        var options = new MemoryCacheEntryOptions
        {
            AbsoluteExpirationRelativeToNow = expiry ?? TimeSpan.FromMinutes(30),
            Priority = CacheItemPriority.Normal
        };

        _memoryCache.Set(cacheKey, value, options);
        return value;
    }

    /// <summary>
    /// Caches the analysis result for a specified file and analyzer,
    /// with expiration settings and file modification tracking.
    /// </summary>
    /// <param name="filePath">The path to the file being analyzed.</param>
    /// <param name="analyzerName">The name of the analyzer associated with the result.</param>
    /// <param name="result">The analysis result to be cached.</param>
    public void CacheAnalysisResult(string filePath, string analyzerName, AnalysisResult result)
    {
        CheckDisposed();
        try
        {
            var fileInfo = new FileInfo(filePath);
            if (!fileInfo.Exists) return;

            var cacheKey = GetAnalysisCacheKey(filePath, analyzerName);
            var fileTimeKey = $"{FileTimePrefix}{filePath}";

            // Cache the file modification time
            _fileModificationTimes[fileTimeKey] = fileInfo.LastWriteTimeUtc;

            // Cache the analysis result with file-based expiration
            var options = new MemoryCacheEntryOptions
            {
                AbsoluteExpirationRelativeToNow = TimeSpan.FromHours(1),
                Priority = CacheItemPriority.High,
                Size = EstimateResultSize(result)
            };

            _memoryCache.Set(cacheKey, result, options);
            _logger.LogTrace("Cached analysis result: {CacheKey}", cacheKey);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to cache analysis result for {FilePath}:{AnalyzerName}",
                filePath, analyzerName);
        }
    }

    /// <summary>
    /// Retrieves the cached analysis result for a specific file and analyzer, if available and valid.
    /// </summary>
    /// <param name="filePath">The path of the file analyzed.</param>
    /// <param name="analyzerName">The name of the analyzer used.</param>
    /// <returns>
    /// The cached <see cref="AnalysisResult"/> if the result exists and the file is unmodified;
    /// otherwise, null.
    /// </returns>
    public AnalysisResult? GetCachedAnalysisResult(string filePath, string analyzerName)
    {
        CheckDisposed();
        try
        {
            var cacheKey = GetAnalysisCacheKey(filePath, analyzerName);

            if (!IsFileCacheValid(filePath))
            {
                // File has been modified, invalidate cache
                _memoryCache.Remove(cacheKey);
                RecordCacheMiss(cacheKey);
                return null;
            }

            if (_memoryCache.TryGetValue(cacheKey, out AnalysisResult? cached))
            {
                RecordCacheHit(cacheKey);
                _logger.LogTrace("Cache hit for analysis result: {CacheKey}", cacheKey);
                return cached;
            }

            RecordCacheMiss(cacheKey);
            return null;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to get cached analysis result for {FilePath}:{AnalyzerName}",
                filePath, analyzerName);
            return null;
        }
    }

    /// <summary>
    /// Verifies whether the file is still valid in the cache by checking if its last modification time
    /// is less than or equal to the cached time.
    /// </summary>
    /// <param name="filePath">The file path of the file to validate in the cache.</param>
    /// <returns>
    /// Returns true if the file exists and its last modification time is less than or equal to the cached time;
    /// otherwise, returns false.
    /// </returns>
    public bool IsFileCacheValid(string filePath)
    {
        try
        {
            var fileInfo = new FileInfo(filePath);
            if (!fileInfo.Exists) return false;

            var fileTimeKey = $"{FileTimePrefix}{filePath}";
            if (!_fileModificationTimes.TryGetValue(fileTimeKey, out var cachedTime))
                return false;

            return fileInfo.LastWriteTimeUtc <= cachedTime;
        }
        catch
        {
            return false;
        }
    }

    /// <summary>
    /// Clears all items from the cache, resets hit and miss counters, and logs the operation.
    /// </summary>
    public void ClearCache()
    {
        CheckDisposed();
        if (_memoryCache is MemoryCache mc) mc.Compact(1.0); // Remove all entries

        _fileModificationTimes.Clear();
        _cacheHits.Clear();
        _cacheMisses.Clear();

        _logger.LogInformation("Cache cleared");
    }

    /// <summary>
    /// Retrieves cache statistics including total hits, misses, hit rate,
    /// number of cached files, and memory usage.
    /// </summary>
    /// <returns>
    /// A <see cref="CacheStatistics"/> instance containing detailed metrics
    /// about cache performance and usage.
    /// </returns>
    public CacheStatistics GetStatistics()
    {
        CheckDisposed();
        lock (_statsLock)
        {
            var totalHits = _cacheHits.Values.Sum();
            var totalMisses = _cacheMisses.Values.Sum();
            var totalRequests = totalHits + totalMisses;

            return new CacheStatistics
            {
                TotalHits = totalHits,
                TotalMisses = totalMisses,
                HitRate = totalRequests > 0 ? (double)totalHits / totalRequests : 0,
                CachedFiles = _fileModificationTimes.Count,
                MemoryUsage = GC.GetTotalMemory(false)
            };
        }
    }

    /// <summary>
    /// Releases all resources used by the current instance of the CacheManager class.
    /// </summary>
    /// <remarks>
    /// This method clears all internal caches, marks the current instance as disposed,
    /// and suppresses finalization to prevent unnecessary garbage collection overhead.
    /// </remarks>
    public void Dispose()
    {
        if (_disposed) return;

        _fileModificationTimes.Clear();
        _cacheHits.Clear();
        _cacheMisses.Clear();
        _disposed = true;
        GC.SuppressFinalize(this);
    }

    /// <summary>
    /// Records a cache hit in the internal statistics for the given cache key.
    /// </summary>
    /// <param name="cacheKey">The unique key identifying the cached item that was accessed.</param>
    private void RecordCacheHit(string cacheKey)
    {
        _cacheHits.AddOrUpdate(cacheKey, 1, (_, count) => count + 1);
    }

    /// <summary>
    /// Records a cache miss for the specified cache key.
    /// </summary>
    /// <param name="cacheKey">
    /// The key for which the cache miss is recorded.
    /// </param>
    private void RecordCacheMiss(string cacheKey)
    {
        _cacheMisses.AddOrUpdate(cacheKey, 1, (_, count) => count + 1);
    }

    /// <summary>
    /// Generates a cache key for storing analysis results based on the file path and analyzer name.
    /// </summary>
    /// <param name="filePath">The file path associated with the analysis result.</param>
    /// <param name="analyzerName">The name of the analyzer that produced the result.</param>
    /// <returns>A string representing the unique cache key for the analysis result.</returns>
    private static string GetAnalysisCacheKey(string filePath, string analyzerName)
    {
        return $"{AnalysisPrefix}{filePath}:{analyzerName}";
    }

    /// <summary>
    /// Estimates the memory size of the provided analysis result.
    /// </summary>
    /// <param name="result">The analysis result for which to estimate the memory size.</param>
    /// <returns>The estimated size in bytes of the analysis result.</returns>
    private static long EstimateResultSize(AnalysisResult result)
    {
        // Rough estimation of memory usage
        var baseSize = 1024; // Base object overhead
        var reportSize = result.ReportLines.Sum(line => line.Length * 2); // UTF-16
        var errorSize = result.Errors.Sum(error => error.Length * 2);
        return baseSize + reportSize + errorSize;
    }

    /// <summary>
    /// Verifies whether the current object has been disposed of and throws an exception if it has.
    /// </summary>
    /// <exception cref="ObjectDisposedException">
    /// Thrown if the object has already been disposed.
    /// </exception>
    private void CheckDisposed()
    {
        if (!_disposed) return;
        throw new ObjectDisposedException(nameof(CacheManager));
    }
}

/// <summary>
///     Cache performance statistics
/// </summary>
public record CacheStatistics
{
    public int TotalHits { get; init; }
    public int TotalMisses { get; init; }
    public double HitRate { get; init; }
    public int CachedFiles { get; init; }
    public long MemoryUsage { get; init; }
}