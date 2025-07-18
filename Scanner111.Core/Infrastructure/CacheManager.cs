using System.Collections.Concurrent;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;
using Scanner111.Core.Analyzers;

namespace Scanner111.Core.Infrastructure;

/// <summary>
/// Cache manager for settings and analysis results
/// </summary>
public interface ICacheManager
{
    /// <summary>
    /// Get or set YAML settings with caching
    /// </summary>
    T? GetOrSetYamlSetting<T>(string yamlFile, string keyPath, Func<T?> factory, TimeSpan? expiry = null);
    
    /// <summary>
    /// Cache analysis result for a file
    /// </summary>
    void CacheAnalysisResult(string filePath, string analyzerName, AnalysisResult result);
    
    /// <summary>
    /// Get cached analysis result
    /// </summary>
    AnalysisResult? GetCachedAnalysisResult(string filePath, string analyzerName);
    
    /// <summary>
    /// Check if file has been modified since last cache
    /// </summary>
    bool IsFileCacheValid(string filePath);
    
    /// <summary>
    /// Clear all caches
    /// </summary>
    void ClearCache();
    
    /// <summary>
    /// Get cache statistics
    /// </summary>
    CacheStatistics GetStatistics();
}

/// <summary>
/// Implementation of cache manager
/// </summary>
public class CacheManager : ICacheManager, IDisposable
{
    private readonly IMemoryCache _memoryCache;
    private readonly ILogger<CacheManager> _logger;
    private readonly ConcurrentDictionary<string, DateTime> _fileModificationTimes = new();
    private readonly ConcurrentDictionary<string, int> _cacheHits = new();
    private readonly ConcurrentDictionary<string, int> _cacheMisses = new();
    private readonly object _statsLock = new();
    private bool _disposed;

    // Cache key prefixes
    private const string YAML_PREFIX = "yaml:";
    private const string ANALYSIS_PREFIX = "analysis:";
    private const string FILE_TIME_PREFIX = "filetime:";

    public CacheManager(IMemoryCache memoryCache, ILogger<CacheManager> logger)
    {
        _memoryCache = memoryCache;
        _logger = logger;
    }

    public T? GetOrSetYamlSetting<T>(string yamlFile, string keyPath, Func<T?> factory, TimeSpan? expiry = null)
    {
        var cacheKey = $"{YAML_PREFIX}{yamlFile}:{keyPath}";
        
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

    public void CacheAnalysisResult(string filePath, string analyzerName, AnalysisResult result)
    {
        try
        {
            var fileInfo = new FileInfo(filePath);
            if (!fileInfo.Exists) return;

            var cacheKey = GetAnalysisCacheKey(filePath, analyzerName);
            var fileTimeKey = $"{FILE_TIME_PREFIX}{filePath}";
            
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

    public AnalysisResult? GetCachedAnalysisResult(string filePath, string analyzerName)
    {
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

    public bool IsFileCacheValid(string filePath)
    {
        try
        {
            var fileInfo = new FileInfo(filePath);
            if (!fileInfo.Exists) return false;

            var fileTimeKey = $"{FILE_TIME_PREFIX}{filePath}";
            if (!_fileModificationTimes.TryGetValue(fileTimeKey, out var cachedTime))
                return false;

            return fileInfo.LastWriteTimeUtc <= cachedTime;
        }
        catch
        {
            return false;
        }
    }

    public void ClearCache()
    {
        if (_memoryCache is MemoryCache mc)
        {
            mc.Compact(1.0); // Remove all entries
        }
        
        _fileModificationTimes.Clear();
        _cacheHits.Clear();
        _cacheMisses.Clear();
        
        _logger.LogInformation("Cache cleared");
    }

    public CacheStatistics GetStatistics()
    {
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

    private void RecordCacheHit(string cacheKey)
    {
        _cacheHits.AddOrUpdate(cacheKey, 1, (key, count) => count + 1);
    }

    private void RecordCacheMiss(string cacheKey)
    {
        _cacheMisses.AddOrUpdate(cacheKey, 1, (key, count) => count + 1);
    }

    private static string GetAnalysisCacheKey(string filePath, string analyzerName)
        => $"{ANALYSIS_PREFIX}{filePath}:{analyzerName}";

    private static long EstimateResultSize(AnalysisResult result)
    {
        // Rough estimation of memory usage
        var baseSize = 1024; // Base object overhead
        var reportSize = result.ReportLines.Sum(line => line.Length * 2); // UTF-16
        var errorSize = result.Errors.Sum(error => error.Length * 2);
        return baseSize + reportSize + errorSize;
    }

    public void Dispose()
    {
        if (_disposed) return;
        
        _fileModificationTimes.Clear();
        _cacheHits.Clear();
        _cacheMisses.Clear();
        _disposed = true;
    }
}

/// <summary>
/// Cache performance statistics
/// </summary>
public record CacheStatistics
{
    public int TotalHits { get; init; }
    public int TotalMisses { get; init; }
    public double HitRate { get; init; }
    public int CachedFiles { get; init; }
    public long MemoryUsage { get; init; }
}