using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Scanner111.Core.Analyzers;
using Scanner111.Core.Infrastructure;
using Xunit;

namespace Scanner111.Tests.Infrastructure;

public class CacheManagerTests : IDisposable
{
    private readonly MemoryCache _memoryCache;
    private readonly CacheManager _cacheManager;
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

    [Fact]
    public void CacheAnalysisResult_StoresResult()
    {
        // Arrange
        var result = new GenericAnalysisResult
        {
            AnalyzerName = "TestAnalyzer",
            Success = true,
            ReportLines = new List<string> { "Test report" }
        };

        // Act
        _cacheManager.CacheAnalysisResult(_testFilePath, "TestAnalyzer", result);
        var cached = _cacheManager.GetCachedAnalysisResult(_testFilePath, "TestAnalyzer");

        // Assert
        Assert.NotNull(cached);
        Assert.Equal("TestAnalyzer", cached.AnalyzerName);
        Assert.True(cached.Success);
    }

    [Fact]
    public void GetCachedAnalysisResult_ReturnsNullForMissingCache()
    {
        // Act
        var result = _cacheManager.GetCachedAnalysisResult("nonexistent.log", "TestAnalyzer");

        // Assert
        Assert.Null(result);
    }

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

    [Fact]
    public void IsFileCacheValid_ReturnsFalseForNonexistentFile()
    {
        // Act
        var isValid = _cacheManager.IsFileCacheValid("nonexistent.log");

        // Assert
        Assert.False(isValid);
    }

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
        Assert.True(stats.HitRate > 0 && stats.HitRate <= 1);
        Assert.True(stats.MemoryUsage >= 0);
    }

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

    public void Dispose()
    {
        _cacheManager?.Dispose();
        _memoryCache?.Dispose();
        
        if (File.Exists(_testFilePath))
        {
            File.Delete(_testFilePath);
        }
    }
}