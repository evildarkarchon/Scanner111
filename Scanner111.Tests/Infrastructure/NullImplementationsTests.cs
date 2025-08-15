using Scanner111.Core.Analyzers;
using Scanner111.Core.Infrastructure;

namespace Scanner111.Tests.Infrastructure;

public class NullImplementationsTests
{
    [Fact]
    public void NullCacheManager_GetOrSetYamlSetting_AlwaysCallsFactory()
    {
        // Arrange
        var cache = new NullCacheManager();
        var factoryCallCount = 0;

        string Factory()
        {
            factoryCallCount++;
            return "test value";
        }

        // Act
        var result1 = cache.GetOrSetYamlSetting("test.yaml", "test.key", Factory);
        var result2 = cache.GetOrSetYamlSetting("test.yaml", "test.key", Factory);
        var result3 = cache.GetOrSetYamlSetting("test.yaml", "test.key", Factory, TimeSpan.FromMinutes(5));

        // Assert
        result1.Should().Be("test value");
        result2.Should().Be("test value");
        result3.Should().Be("test value");
        factoryCallCount.Should().Be(3); // Factory called every time since no caching
    }

    [Fact]
    public void NullCacheManager_CacheAnalysisResult_DoesNothing()
    {
        // Arrange
        var cache = new NullCacheManager();
        var result = new GenericAnalysisResult
        {
            AnalyzerName = "TestAnalyzer",
            HasFindings = true
        };

        // Act & Assert - Should not throw
        cache.CacheAnalysisResult("test.log", "TestAnalyzer", result);
    }

    [Fact]
    public void NullCacheManager_GetCachedAnalysisResult_AlwaysReturnsNull()
    {
        // Arrange
        var cache = new NullCacheManager();
        var result = new GenericAnalysisResult
        {
            AnalyzerName = "TestAnalyzer",
            HasFindings = true
        };

        // Cache a result
        cache.CacheAnalysisResult("test.log", "TestAnalyzer", result);

        // Act
        var cachedResult = cache.GetCachedAnalysisResult("test.log", "TestAnalyzer");

        // Assert
        cachedResult.Should().BeNull();
    }

    [Fact]
    public void NullCacheManager_IsFileCacheValid_AlwaysReturnsFalse()
    {
        // Arrange
        var cache = new NullCacheManager();

        // Act
        var isValid1 = cache.IsFileCacheValid("existing.log");
        var isValid2 = cache.IsFileCacheValid("nonexistent.log");
        var isValid3 = cache.IsFileCacheValid("");

        // Assert
        isValid1.Should().BeFalse();
        isValid2.Should().BeFalse();
        isValid3.Should().BeFalse();
    }

    [Fact]
    public void NullCacheManager_ClearCache_DoesNothing()
    {
        // Arrange
        var cache = new NullCacheManager();

        // Act & Assert - Should not throw
        cache.ClearCache();
    }

    [Fact]
    public void NullCacheManager_GetStatistics_ReturnsZeroStats()
    {
        // Arrange
        var cache = new NullCacheManager();

        // Act
        var stats = cache.GetStatistics();

        // Assert
        stats.TotalHits.Should().Be(0);
        stats.TotalMisses.Should().Be(0);
        stats.HitRate.Should().Be(0);
        stats.CachedFiles.Should().Be(0);
        stats.MemoryUsage.Should().Be(0);
    }

    [Fact]
    public void NoRetryErrorPolicy_HandleError_ReturnsCorrectAction_ForOperationCanceledException()
    {
        // Arrange
        var policy = new NoRetryErrorPolicy();
        var exception = new OperationCanceledException();

        // Act
        var result = policy.HandleError(exception, "test context", 1);

        // Assert
        result.Action.Should().Be(ErrorAction.Fail);
        result.Message.Should().Be("Operation was cancelled");
        result.LogLevel.Should().Be(LogLevel.Information);
    }

    [Fact]
    public void NoRetryErrorPolicy_HandleError_ReturnsSkipAction_ForOtherExceptions()
    {
        // Arrange
        var policy = new NoRetryErrorPolicy();
        var exception = new InvalidOperationException("Test error");

        // Act
        var result = policy.HandleError(exception, "test context", 1);

        // Assert
        result.Action.Should().Be(ErrorAction.Skip);
        result.Message.Should().Be("Error in test context: Test error");
        result.LogLevel.Should().Be(LogLevel.Error);
    }

    [Theory]
    [InlineData(typeof(ArgumentException))]
    [InlineData(typeof(NullReferenceException))]
    [InlineData(typeof(IOException))]
    public void NoRetryErrorPolicy_HandleError_ReturnsSkipAction_ForVariousExceptions(Type exceptionType)
    {
        // Arrange
        var policy = new NoRetryErrorPolicy();
        var exception = (Exception)Activator.CreateInstance(exceptionType, "Test error");

        // Act
        var result = policy.HandleError(exception, "test context", 1);

        // Assert
        result.Action.Should().Be(ErrorAction.Skip);
        result.Message.Should().Contain("Test error");
        result.LogLevel.Should().Be(LogLevel.Error);
    }

    [Theory]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(5)]
    [InlineData(10)]
    public void NoRetryErrorPolicy_HandleError_IgnoresAttemptNumber(int attemptNumber)
    {
        // Arrange
        var policy = new NoRetryErrorPolicy();
        var exception = new Exception("Test");

        // Act
        var result = policy.HandleError(exception, "context", attemptNumber);

        // Assert
        result.Action.Should().Be(ErrorAction.Skip);
    }

    [Fact]
    public void NoRetryErrorPolicy_ShouldRetry_AlwaysReturnsFalse()
    {
        // Arrange
        var policy = new NoRetryErrorPolicy();

        // Act & Assert
        policy.ShouldRetry(new Exception(), 1).Should().BeFalse();
        policy.ShouldRetry(new IOException(), 2).Should().BeFalse();
        policy.ShouldRetry(new TimeoutException(), 3).Should().BeFalse();
        policy.ShouldRetry(new OperationCanceledException(), 1).Should().BeFalse();
    }

    [Fact]
    public void NullProgress_Report_DoesNothing()
    {
        // Arrange
        var progress = new NullProgress<int>();

        // Act & Assert - Should not throw
        progress.Report(0);
        progress.Report(50);
        progress.Report(100);
    }

    [Fact]
    public void NullProgress_Report_HandlesVariousTypes()
    {
        // Arrange & Act & Assert - Should not throw
        var intProgress = new NullProgress<int>();
        intProgress.Report(42);

        var stringProgress = new NullProgress<string>();
        stringProgress.Report("test");
        stringProgress.Report(null);

        var progressInfoProgress = new NullProgress<ProgressInfo>();
        progressInfoProgress.Report(new ProgressInfo { Current = 10, Total = 100, Message = "Test" });

        var customProgress = new NullProgress<(int count, string message)>();
        customProgress.Report((5, "Processing"));
    }

    [Fact]
    public void NullProgress_CanBeUsedAsIProgress()
    {
        // Arrange
        IProgress<string> progress = new NullProgress<string>();

        // Act & Assert - Should work as IProgress interface
        progress.Report("test message");
    }

    [Fact]
    public void AllNullImplementations_AreThreadSafe()
    {
        // Arrange
        var cache = new NullCacheManager();
        var policy = new NoRetryErrorPolicy();
        var progress = new NullProgress<int>();
        var exception = new Exception("Test");

        // Act - Concurrent access
        var tasks = new Task[10];
        for (var i = 0; i < tasks.Length; i++)
        {
            var index = i;
            tasks[i] = Task.Run(() =>
            {
                cache.GetOrSetYamlSetting("test", "key", () => index);
                cache.CacheAnalysisResult($"file{index}", "analyzer",
                    new GenericAnalysisResult { AnalyzerName = "analyzer" });
                cache.GetCachedAnalysisResult($"file{index}", "analyzer");
                cache.IsFileCacheValid($"file{index}");
                cache.GetStatistics();

                policy.HandleError(exception, $"context{index}", index);
                policy.ShouldRetry(exception, index);

                progress.Report(index);
            });
        }

        // Assert - Should complete without exceptions
        Task.WaitAll(tasks);
    }
}