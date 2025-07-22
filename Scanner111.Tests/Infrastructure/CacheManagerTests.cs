using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging.Abstractions;
using Scanner111.Core.Analyzers;
using Scanner111.Core.Infrastructure;

namespace Scanner111.Tests.Infrastructure;

/// <summary>
/// Contains unit tests for the <see cref="CacheManager"/> class, ensuring correct behavior of its caching mechanisms
/// and interactions with the underlying memory cache.
/// </summary>
public class CacheManagerTests : IDisposable
{
    private readonly CacheManager _cacheManager;
    private readonly MemoryCache _memoryCache;
    private readonly string _testFilePath;

    public CacheManagerTests()
    {
        _memoryCache = new MemoryCache(new MemoryCacheOptions());
        var logger = NullLogger<CacheManager>.Instance;
        _cacheManager = new CacheManager(_memoryCache, logger);

        // Create a temporary test file
        _testFilePath = Path.GetTempFileName();
        File.WriteAllText(_testFilePath, "test content");
    }

    /// <summary>
    /// Performs application-defined tasks associated with freeing, releasing, or resetting unmanaged resources.
    /// </summary>
    /// <remarks>
    /// This method is responsible for releasing all resources utilized by the implementing class,
    /// ensuring proper cleanup of system resources and memory. It may involve clearing cache data,
    /// deleting temporary files, and suppressing finalization to reduce garbage collection overhead.
    /// </remarks>
    public void Dispose()
    {
        _cacheManager.Dispose();
        _memoryCache.Dispose();

        if (File.Exists(_testFilePath)) File.Delete(_testFilePath);
        GC.SuppressFinalize(this);
    }

    /// <summary>
    /// Ensures the value returned by the factory function is cached and reused for subsequent calls with the same YAML file
    /// and key path combination.
    /// </summary>
    /// <remarks>
    /// This method is designed to provide efficient access to YAML settings by caching the result of the factory function.
    /// If the requested key and YAML file combination already exists in the cache, the cached value is returned.
    /// Otherwise, the factory function is invoked to generate the value, which is then added to the cache.
    /// This prevents redundant costly operations for the same input and improves overall system performance.
    /// </remarks>
    [Fact]
    public void GetOrSetYamlSetting_CachesValue()
    {
        // Arrange
        var factoryCalled = 0;

        string Factory()
        {
            factoryCalled++;
            return "test-value";
        }

        // Act
        var value1 = _cacheManager.GetOrSetYamlSetting("test", "key", Factory);
        var value2 = _cacheManager.GetOrSetYamlSetting("test", "key", Factory);

        // Assert
        Assert.Equal("test-value", value1);
        Assert.Equal("test-value", value2);
        Assert.Equal(1, factoryCalled); // Factory should only be called once
    }

    /// <summary>
    /// Validates that the <see cref="CacheManager.GetOrSetYamlSetting{T}"/> method respects the specified expiration
    /// time by ensuring the cached value is re-evaluated and updated after the expiry period has elapsed.
    /// </summary>
    /// <remarks>
    /// This test verifies the proper functionality of cache expiration by utilizing a factory method to generate values.
    /// It ensures that the factory method is invoked again for the same key-path combination if the cache entry for
    /// that key-path has expired. Additionally, it ensures that the new value is properly stored and can be retrieved
    /// after the factory method is re-invoked.
    /// </remarks>
    [Fact]
    public void GetOrSetYamlSetting_RespectsExpiry()
    {
        // Arrange
        var factoryCalled = 0;

        string Factory()
        {
            factoryCalled++;
            return $"test-value-{factoryCalled}";
        }

        // Act
        var value1 = _cacheManager.GetOrSetYamlSetting("test", "key", Factory, TimeSpan.FromMilliseconds(1));
        Thread.Sleep(10); // Wait for expiry
        var value2 = _cacheManager.GetOrSetYamlSetting("test", "key", Factory, TimeSpan.FromMilliseconds(1));

        // Assert
        Assert.Equal("test-value-1", value1);
        Assert.Equal("test-value-2", value2);
        Assert.Equal(2, factoryCalled); // Factory should be called twice due to expiry
    }

    /// <summary>
    /// Verifies that the result of an analysis is correctly cached and retrieved from the cache
    /// when using the <see cref="CacheManager"/> class.
    /// </summary>
    /// <remarks>
    /// This test ensures that an analysis result, once cached, can be retrieved from the cache
    /// without alteration. It validates that properties such as analyzer name or success status
    /// remain consistent between the original result and the retrieved instance.
    /// </remarks>
    [Fact]
    public void CacheAnalysisResult_StoresResult()
    {
        // Arrange
        var result = new GenericAnalysisResult
        {
            AnalyzerName = "TestAnalyzer",
            Success = true,
            ReportLines = ["Test report"]
        };

        // Act
        _cacheManager.CacheAnalysisResult(_testFilePath, "TestAnalyzer", result);
        var cached = _cacheManager.GetCachedAnalysisResult(_testFilePath, "TestAnalyzer");

        // Assert
        Assert.NotNull(cached);
        Assert.Equal("TestAnalyzer", cached.AnalyzerName);
        Assert.True(cached.Success);
    }

    /// <summary>
    /// Verifies that the <see cref="CacheManager.GetCachedAnalysisResult(string, string)"/> method
    /// returns null when attempting to retrieve a cached analysis result for a nonexistent cache entry.
    /// </summary>
    /// <remarks>
    /// This test ensures that the caching mechanism in the <see cref="CacheManager"/> behaves correctly
    /// when a requested file-analyzer combination is not found in the cache. It validates that
    /// the method does not throw an exception or return an invalid value in this scenario.
    /// </remarks>
    [Fact]
    public void GetCachedAnalysisResult_ReturnsNullForMissingCache()
    {
        // Act
        var result = _cacheManager.GetCachedAnalysisResult("nonexistent.log", "TestAnalyzer");

        // Assert
        Assert.Null(result);
    }

    /// <summary>
    /// Verifies that the cache for a file is considered valid when the file has not been modified since
    /// the associated analysis result was cached.
    /// </summary>
    /// <remarks>
    /// This test ensures that the <see cref="CacheManager.IsFileCacheValid"/> method correctly identifies
    /// unmodified files and returns <c>true</c> in such cases. It validates the caching mechanism works as intended
    /// for files that remain unchanged, preserving cached results for improved performance.
    /// </remarks>
    [Fact]
    public void IsFileCacheValid_ReturnsTrueForUnmodifiedFile()
    {
        // Arrange
        var result = new GenericAnalysisResult
        {
            AnalyzerName = "TestAnalyzer",
            Success = true
        };
        _cacheManager.CacheAnalysisResult(_testFilePath, "TestAnalyzer", result);

        // Act
        var isValid = _cacheManager.IsFileCacheValid(_testFilePath);

        // Assert
        Assert.True(isValid);
    }

    /// <summary>
    /// Verifies that the <see cref="CacheManager.IsFileCacheValid(string)"/> method returns <c>false</c>
    /// for a file whose content has been modified after being cached.
    /// </summary>
    /// <remarks>
    /// This test ensures that the caching mechanism correctly identifies a file as invalidated
    /// when its contents are altered after caching. It aims to confirm that the implementation
    /// properly compares the file's last modification timestamp with the cached metadata,
    /// detecting any discrepancies and invalidating the cache as expected.
    /// </remarks>
    [Fact]
    public void IsFileCacheValid_ReturnsFalseForModifiedFile()
    {
        // Arrange
        var result = new GenericAnalysisResult
        {
            AnalyzerName = "TestAnalyzer",
            Success = true
        };
        _cacheManager.CacheAnalysisResult(_testFilePath, "TestAnalyzer", result);

        // Act - Modify the file
        Thread.Sleep(10); // Ensure time difference
        File.WriteAllText(_testFilePath, "modified content");
        var isValid = _cacheManager.IsFileCacheValid(_testFilePath);

        // Assert
        Assert.False(isValid);
    }

    /// <summary>
    /// Validates that the cache is considered invalid when the specified file does not exist.
    /// </summary>
    /// <remarks>
    /// This test ensures that the <see cref="CacheManager.IsFileCacheValid(string)"/> method correctly identifies
    /// a non-existent file as invalid in the cache and returns false. This behavior is crucial to maintain
    /// cache reliability and avoid incorrect assumptions about missing files.
    /// </remarks>
    [Fact]
    public void IsFileCacheValid_ReturnsFalseForNonexistentFile()
    {
        // Act
        var isValid = _cacheManager.IsFileCacheValid("nonexistent.log");

        // Assert
        Assert.False(isValid);
    }

    /// <summary>
    /// Verifies that invoking <see cref="CacheManager.GetCachedAnalysisResult(string, string)"/> returns null
    /// when the underlying file has been modified after its analysis result was cached.
    /// </summary>
    /// <remarks>
    /// This test ensures that the caching mechanism invalidates previously stored results when the cache key,
    /// in this case, the file's last modified timestamp, changes. It involves modifying a file after caching
    /// an analysis result and checking that the method no longer returns the cached data.
    /// </remarks>
    [Fact]
    public void GetCachedAnalysisResult_InvalidatesCacheForModifiedFile()
    {
        // Arrange
        var result = new GenericAnalysisResult
        {
            AnalyzerName = "TestAnalyzer",
            Success = true
        };
        _cacheManager.CacheAnalysisResult(_testFilePath, "TestAnalyzer", result);

        // Act - Modify the file and try to get cached result
        Thread.Sleep(10);
        File.WriteAllText(_testFilePath, "modified content");
        var cached = _cacheManager.GetCachedAnalysisResult(_testFilePath, "TestAnalyzer");

        // Assert
        Assert.Null(cached); // Should return null because file was modified
    }

    /// <summary>
    /// Validates that invoking the <see cref="CacheManager.ClearCache"/> method removes all items currently stored in the cache.
    /// </summary>
    /// <remarks>
    /// This test ensures that upon clearing the cache, all previously cached analysis results and configuration settings
    /// are removed. Subsequent attempts to fetch these values will either return null or trigger the provided factory functions.
    /// This is an essential verification for scenarios that require a reset of cache state, such as application updates or
    /// configuration changes.
    /// </remarks>
    [Fact]
    public void ClearCache_RemovesAllCachedItems()
    {
        // Arrange
        var result = new GenericAnalysisResult
        {
            AnalyzerName = "TestAnalyzer",
            Success = true
        };
        _cacheManager.CacheAnalysisResult(_testFilePath, "TestAnalyzer", result);
        _cacheManager.GetOrSetYamlSetting("test", "key", () => "value");

        // Act
        _cacheManager.ClearCache();

        // Assert
        var cachedResult = _cacheManager.GetCachedAnalysisResult(_testFilePath, "TestAnalyzer");
        var cachedYaml = _cacheManager.GetOrSetYamlSetting("test", "key", () => "new-value");

        Assert.Null(cachedResult);
        Assert.Equal("new-value", cachedYaml); // Should call factory again
    }

    /// <summary>
    /// Verifies that the <see cref="CacheManager.GetStatistics"/> method returns accurate metrics
    /// reflecting the cache's performance, including total hits, total misses, hit rate, and memory usage.
    /// </summary>
    /// <remarks>
    /// This test ensures that the statistics reported by the cache correctly account for cache usage events
    /// such as hits and misses, providing a clear representation of the cache's operational efficiency.
    /// The test involves creating scenarios with cache hits and misses and validating that the resulting
    /// metrics align with the expected values.
    /// </remarks>
    [Fact]
    public void GetStatistics_ReturnsCorrectMetrics()
    {
        // Arrange - Create cache hits and misses
        _cacheManager.GetOrSetYamlSetting("test", "key1", () => "value1");
        _cacheManager.GetOrSetYamlSetting("test", "key1", () => "value1"); // Hit
        _cacheManager.GetOrSetYamlSetting("test", "key2", () => "value2");
        _cacheManager.GetCachedAnalysisResult("nonexistent.log", "TestAnalyzer"); // Miss

        // Act
        var stats = _cacheManager.GetStatistics();

        // Assert
        Assert.True(stats.TotalHits > 0);
        Assert.True(stats.TotalMisses > 0);
        Assert.True(stats.HitRate is > 0 and <= 1);
        Assert.True(stats.MemoryUsage >= 0);
    }

    /// <summary>
    /// Validates that the <see cref="NullCacheManager"/> does not cache any values and always invokes the provided factory
    /// methods to retrieve fresh results every time.
    /// </summary>
    /// <remarks>
    /// This test ensures that the behavior of the <see cref="NullCacheManager"/> is consistent with its design of being
    /// a no-op caching mechanism. It verifies that values are not stored or retrieved from any cache and that
    /// the factory function is called on every invocation.
    /// </remarks>
    [Fact]
    public void NullCacheManager_NeverCaches()
    {
        // Arrange
        var nullCache = new NullCacheManager();
        var factoryCalled = 0;

        string Factory()
        {
            factoryCalled++;
            return $"value-{factoryCalled}";
        }

        // Act
        var value1 = nullCache.GetOrSetYamlSetting("test", "key", Factory);
        var value2 = nullCache.GetOrSetYamlSetting("test", "key", Factory);

        // Assert
        Assert.Equal("value-1", value1);
        Assert.Equal("value-2", value2);
        Assert.Equal(2, factoryCalled); // Factory should be called both times
    }

    /// <summary>
    /// Validates that the <see cref="NullCacheManager"/> class always returns null
    /// for analysis results, regardless of caching attempts.
    /// </summary>
    /// <remarks>
    /// This unit test ensures that the <see cref="NullCacheManager"/> implementation behaves as expected
    /// by explicitly verifying that any attempt to retrieve a cached analysis result will always return null.
    /// This behavior confirms the non-caching nature of the <see cref="NullCacheManager"/>.
    /// </remarks>
    [Fact]
    public void NullCacheManager_AlwaysReturnsNullForAnalysisResults()
    {
        // Arrange
        var nullCache = new NullCacheManager();
        var result = new GenericAnalysisResult
        {
            AnalyzerName = "TestAnalyzer",
            Success = true
        };

        // Act
        nullCache.CacheAnalysisResult(_testFilePath, "TestAnalyzer", result);
        var cached = nullCache.GetCachedAnalysisResult(_testFilePath, "TestAnalyzer");

        // Assert
        Assert.Null(cached);
    }

    /// <summary>
    /// Verifies that the <see cref="NullCacheManager"/> implementation of <c>IsFileCacheValid</c>
    /// always returns <c>false</c>, indicating that file cache is never considered valid.
    /// </summary>
    /// <remarks>
    /// This test ensures that the behavior of the <see cref="NullCacheManager"/> class is consistent
    /// with its intended design of bypassing any file cache validation, always treating the cache as invalid.
    /// </remarks>
    [Fact]
    public void NullCacheManager_IsFileCacheValidAlwaysReturnsFalse()
    {
        // Arrange
        var nullCache = new NullCacheManager();

        // Act
        var isValid = nullCache.IsFileCacheValid(_testFilePath);

        // Assert
        Assert.False(isValid);
    }

    /// <summary>
    /// Verifies that calling <see cref="NullCacheManager.GetStatistics"/> returns a <see cref="CacheStatistics"/> object
    /// where all metric values are set to zero.
    /// </summary>
    /// <remarks>
    /// This test ensures that the <see cref="NullCacheManager"/> implementation adheres to its intended behavior
    /// of not caching any values and reports no activity or resource usage through the <see cref="GetStatistics"/> method.
    /// </remarks>
    [Fact]
    public void NullCacheManager_GetStatisticsReturnsZeros()
    {
        // Arrange
        var nullCache = new NullCacheManager();

        // Act
        var stats = nullCache.GetStatistics();

        // Assert
        Assert.Equal(0, stats.TotalHits);
        Assert.Equal(0, stats.TotalMisses);
        Assert.Equal(0, stats.HitRate);
        Assert.Equal(0, stats.CachedFiles);
        Assert.Equal(0, stats.MemoryUsage);
    }
}