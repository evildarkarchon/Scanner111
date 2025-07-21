using Microsoft.Extensions.Logging;
using Xunit;
using Scanner111.Core.Analyzers;
using Scanner111.Core.Infrastructure;
using Scanner111.Core.Pipeline;
using Scanner111.Core.Models;
using Scanner111.Tests.TestHelpers;
using System.Collections.Concurrent;
using System.Diagnostics;

namespace Scanner111.Tests.Integration;

/// <summary>
/// Tests for concurrency safety and resource management
/// Based on issues identified in async-io-audit-report.md
/// </summary>
public class ConcurrencyAndResourceTests : IDisposable
{
    private readonly List<string> _tempFiles = new();
    private readonly List<IDisposable> _disposables = new();

    [Fact]
    public async Task ReportWriter_ConcurrentWritesToSameFile_ShouldNotCorruptData()
    {
        // Arrange
        var logger = new TestLogger<ReportWriter>();
        var reportWriter = new ReportWriter(logger);

        var outputPath = CreateTempFile();
        var tasks = new List<Task<bool>>();
        var expectedContent = new ConcurrentBag<string>();

        // Create multiple scan results that will write to the same file
        for (int i = 0; i < 10; i++)
        {
            var scanResult = new ScanResult
            {
                LogPath = $"test_{i}.log",
                Status = ScanStatus.Completed
            };
            scanResult.AddAnalysisResult(new GenericAnalysisResult
            {
                AnalyzerName = $"TestAnalyzer_{i}",
                Success = true
            });

            expectedContent.Add(scanResult.ReportText);
            tasks.Add(reportWriter.WriteReportAsync(scanResult, outputPath));
        }

        // Act - Write concurrently to the same file
        var results = await Task.WhenAll(tasks);

        // Assert
        Assert.All(results, result => Assert.True(result)); // All writes should succeed
        Assert.True(File.Exists(outputPath));
        
        // File should exist and contain valid content (no corruption)
        var fileContent = await File.ReadAllTextAsync(outputPath);
        Assert.NotEmpty(fileContent);
        
        // The file should contain content from one of the reports (last writer wins)
        Assert.True(expectedContent.Any(content => fileContent.Contains("TestAnalyzer_")));
    }

    [Fact]
    public async Task ScanPipeline_ResourceDisposal_ShouldNotLeakSemaphore()
    {
        // Arrange
        var initialHandleCount = GetCurrentProcessHandleCount();
        var pipelines = new List<ScanPipeline>();

        // Act - Create and dispose multiple pipelines
        for (int i = 0; i < 20; i++)
        {
            var pipeline = CreateTestPipeline();
            pipelines.Add(pipeline);
            
            // Use the pipeline briefly
            var testLogPath = CreateTempCrashLog($"test_{i}.log", "Sample crash log content");
            var result = await pipeline.ProcessSingleAsync(testLogPath);
            Assert.NotNull(result);
        }

        // Dispose all pipelines
        foreach (var pipeline in pipelines)
        {
            await pipeline.DisposeAsync();
        }

        // Force garbage collection
        GC.Collect();
        GC.WaitForPendingFinalizers();
        GC.Collect();

        // Wait a bit for handles to be released
        await Task.Delay(100);

        var finalHandleCount = GetCurrentProcessHandleCount();

        // Assert - Handle count should not have grown significantly
        var handleIncrease = finalHandleCount - initialHandleCount;
        Assert.True(handleIncrease < 50, $"Handle count increased by {handleIncrease}, indicating potential resource leak");
    }

    [Fact]
    public async Task EnhancedScanPipeline_ResourceDisposal_ShouldNotLeakSemaphore()
    {
        // Arrange
        var initialHandleCount = GetCurrentProcessHandleCount();
        var pipelines = new List<EnhancedScanPipeline>();

        // Act - Create and dispose multiple enhanced pipelines
        for (int i = 0; i < 15; i++)
        {
            var pipeline = CreateTestEnhancedPipeline();
            pipelines.Add(pipeline);
            
            // Use the pipeline briefly
            var testLogPath = CreateTempCrashLog($"enhanced_test_{i}.log", "Sample enhanced crash log content");
            var result = await pipeline.ProcessSingleAsync(testLogPath);
            Assert.NotNull(result);
        }

        // Dispose all pipelines
        foreach (var pipeline in pipelines)
        {
            await pipeline.DisposeAsync();
        }

        // Force garbage collection
        GC.Collect();
        GC.WaitForPendingFinalizers();
        GC.Collect();

        await Task.Delay(100);

        var finalHandleCount = GetCurrentProcessHandleCount();

        // Assert
        var handleIncrease = finalHandleCount - initialHandleCount;
        Assert.True(handleIncrease < 50, $"Handle count increased by {handleIncrease}, indicating potential resource leak");
    }

    [Fact]
    public async Task ScanPipeline_ConcurrentBatchProcessing_ShouldHandleFileDeduplication()
    {
        // Arrange
        var pipeline = CreateTestPipeline();

        var logPath = CreateTempCrashLog("duplicate_test.log", "Duplicate test content");
        
        // Create multiple lists with the same file path
        var duplicatedPaths = new List<IEnumerable<string>>
        {
            Enumerable.Repeat(logPath, 5).ToList(),
            Enumerable.Repeat(logPath, 3).ToList(),
            Enumerable.Repeat(logPath, 7).ToList()
        };

        var allResults = new ConcurrentBag<ScanResult>();

        // Act - Process the same file from multiple batch operations concurrently
        var tasks = duplicatedPaths.Select(async paths =>
        {
            var results = new List<ScanResult>();
            await foreach (var result in pipeline.ProcessBatchAsync(paths))
            {
                results.Add(result);
                allResults.Add(result);
            }
            return results;
        });

        var batchResults = await Task.WhenAll(tasks);

        // Assert
        // Each batch should process the file once due to deduplication
        foreach (var results in batchResults)
        {
            Assert.Single(results);
            Assert.Equal(logPath, results[0].LogPath);
        }

        // Total results should be 3 (one per batch), not 15 (5+3+7)
        Assert.Equal(3, allResults.Count);
    }

    [Fact]
    public async Task ApplicationSettings_ConcurrentRecentItemsModification_ShouldBeThreadSafe()
    {
        // Arrange
        var settings = new ApplicationSettings();
        var tasks = new List<Task>();
        var addedPaths = new ConcurrentBag<string>();

        // Act - Add recent items from multiple threads simultaneously
        for (int i = 0; i < 50; i++)
        {
            var path = $"test_path_{i}.log";
            addedPaths.Add(path);
            
            tasks.Add(Task.Run(() =>
            {
                settings.AddRecentLogFile(path);
                settings.AddRecentGamePath($"game_path_{i}");
                settings.AddRecentScanDirectory($"scan_dir_{i}");
            }));
        }

        await Task.WhenAll(tasks);

        // Assert
        // Recent items lists should be in a consistent state
        Assert.True(settings.RecentLogFiles.Count <= settings.MaxRecentItems);
        Assert.True(settings.RecentGamePaths.Count <= settings.MaxRecentItems);
        Assert.True(settings.RecentScanDirectories.Count <= settings.MaxRecentItems);
        
        // No null or empty entries should exist
        Assert.All(settings.RecentLogFiles, path => Assert.False(string.IsNullOrEmpty(path)));
        Assert.All(settings.RecentGamePaths, path => Assert.False(string.IsNullOrEmpty(path)));
        Assert.All(settings.RecentScanDirectories, path => Assert.False(string.IsNullOrEmpty(path)));
        
        // Most recent items should be at the beginning
        if (settings.RecentLogFiles.Count > 1)
        {
            Assert.Contains("test_path_", settings.RecentLogFiles[0]);
        }
    }

    [Fact]
    public void YamlSettingsCache_ConcurrentInitialization_ShouldBeThreadSafe()
    {
        // Arrange
        YamlSettingsCache.Reset(); // Ensure clean state
        var initializationTasks = new List<Task>();
        var providers = new List<IYamlSettingsProvider>();

        // Create multiple providers
        for (int i = 0; i < 10; i++)
        {
            providers.Add(new TestYamlSettingsProvider());
        }

        // Act - Initialize from multiple threads simultaneously
        foreach (var provider in providers)
        {
            initializationTasks.Add(Task.Run(() => YamlSettingsCache.Initialize(provider)));
        }

        Task.WaitAll(initializationTasks.ToArray());

        // Assert - Cache should be initialized and functional
        Assert.NotNull(YamlSettingsCache.YamlSettings<string>("test", "test.key", "default"));
        
        // Should not throw exceptions when accessed concurrently
        var accessTasks = new List<Task>();
        for (int i = 0; i < 20; i++)
        {
            accessTasks.Add(Task.Run(() =>
            {
                var value = YamlSettingsCache.YamlSettings<string>("test", "test.key", "default");
                Assert.NotNull(value);
            }));
        }

        // Should complete without exceptions
        Task.WaitAll(accessTasks.ToArray());
    }

    [Fact]
    public async Task CacheManager_ConcurrentOperations_ShouldNotCorruptData()
    {
        // Arrange
        var cacheManager = new TestCacheManager();
        var tasks = new List<Task>();
        var keys = new List<string>();

        // Act - Perform concurrent cache operations
        for (int i = 0; i < 100; i++)
        {
            var key = $"test_key_{i}";
            var analyzerName = $"analyzer_{i % 5}"; // Use fewer analyzer names to create contention
            var result = new GenericAnalysisResult
            {
                AnalyzerName = analyzerName,
                Success = true
            };

            keys.Add(key);

            tasks.Add(Task.Run(() =>
            {
                // Mix of operations to create contention
                cacheManager.CacheAnalysisResult(key, analyzerName, result);
                var cached = cacheManager.GetCachedAnalysisResult(key, analyzerName);
                cacheManager.IsFileCacheValid(key);
            }));
        }

        await Task.WhenAll(tasks);

        // Assert
        var stats = cacheManager.GetStatistics();
        Assert.True(stats.CachedFiles >= 0);
        Assert.True(stats.HitRate >= 0.0 && stats.HitRate <= 1.0);

        // Verify some cached items exist
        var foundItems = 0;
        foreach (var key in keys.Take(10))
        {
            for (int i = 0; i < 5; i++)
            {
                if (cacheManager.GetCachedAnalysisResult(key, $"analyzer_{i}") != null)
                {
                    foundItems++;
                }
            }
        }
        Assert.True(foundItems > 0, "No cached items found after concurrent operations");
    }

    [Fact]
    public async Task ReportWriter_HighVolumeWrites_ShouldNotExhaustResources()
    {
        // Arrange
        var logger = new TestLogger<ReportWriter>();
        var reportWriter = new ReportWriter(logger);
        var initialMemory = GC.GetTotalMemory(true);
        var tasks = new List<Task<bool>>();

        // Act - Perform many write operations
        for (int i = 0; i < 200; i++)
        {
            var outputPath = CreateTempFile($"high_volume_{i}.md");
            var scanResult = new ScanResult
            {
                LogPath = $"test_{i}.log",
                Status = ScanStatus.Completed
            };
            
            // Add some content to make the files non-trivial
            for (int j = 0; j < 5; j++)
            {
                scanResult.AddAnalysisResult(new GenericAnalysisResult
                {
                    AnalyzerName = $"Analyzer_{j}",
                    Success = true
                });
            }

            tasks.Add(reportWriter.WriteReportAsync(scanResult, outputPath));
        }

        var results = await Task.WhenAll(tasks);

        // Force garbage collection
        GC.Collect();
        GC.WaitForPendingFinalizers();
        GC.Collect();

        var finalMemory = GC.GetTotalMemory(true);

        // Assert
        Assert.All(results, result => Assert.True(result));
        
        // Memory should not have grown excessively (allow for some growth due to test infrastructure)
        var memoryIncrease = finalMemory - initialMemory;
        Assert.True(memoryIncrease < 50 * 1024 * 1024, $"Memory increased by {memoryIncrease / 1024 / 1024}MB, indicating potential memory leak");
    }

    private ScanPipeline CreateTestPipeline()
    {
        var logger = new TestLogger<ScanPipeline>();
        var messageHandler = new TestMessageHandler();
        var settingsProvider = new TestYamlSettingsProvider();
        var analyzers = new List<TestSimpleAnalyzer>
        {
            new TestSimpleAnalyzer("TestAnalyzer1", 1),
            new TestSimpleAnalyzer("TestAnalyzer2", 2)
        };

        return new ScanPipeline(analyzers, logger, messageHandler, settingsProvider);
    }

    private EnhancedScanPipeline CreateTestEnhancedPipeline()
    {
        var logger = new TestLogger<EnhancedScanPipeline>();
        var messageHandler = new TestMessageHandler();
        var settingsProvider = new TestYamlSettingsProvider();
        var cacheManager = new TestCacheManager();
        var resilientExecutor = new ResilientExecutor(new TestErrorHandlingPolicy(), new TestLogger<ResilientExecutor>());
        var analyzers = new List<TestSimpleAnalyzer>
        {
            new TestSimpleAnalyzer("EnhancedAnalyzer1", 1),
            new TestSimpleAnalyzer("EnhancedAnalyzer2", 2)
        };

        return new EnhancedScanPipeline(analyzers, logger, messageHandler, settingsProvider, cacheManager, resilientExecutor);
    }

    private string CreateTempCrashLog(string fileName, string content)
    {
        var tempPath = Path.Combine(Path.GetTempPath(), fileName);
        File.WriteAllText(tempPath, content);
        _tempFiles.Add(tempPath);
        return tempPath;
    }

    private string CreateTempFile(string? fileName = null)
    {
        var tempPath = fileName != null 
            ? Path.Combine(Path.GetTempPath(), fileName)
            : Path.GetTempFileName();
        _tempFiles.Add(tempPath);
        return tempPath;
    }

    private static int GetCurrentProcessHandleCount()
    {
        try
        {
            using var process = Process.GetCurrentProcess();
            return process.HandleCount;
        }
        catch
        {
            return 0; // Return 0 if we can't get handle count (some platforms don't support it)
        }
    }

    public void Dispose()
    {
        foreach (var disposable in _disposables)
        {
            try
            {
                disposable?.Dispose();
            }
            catch
            {
                // Ignore disposal errors
            }
        }

        foreach (var tempFile in _tempFiles)
        {
            try
            {
                if (File.Exists(tempFile))
                {
                    File.Delete(tempFile);
                }
            }
            catch
            {
                // Ignore cleanup errors
            }
        }
    }
}

/// <summary>
/// Simple test analyzer for concurrency testing
/// </summary>
internal class TestSimpleAnalyzer : IAnalyzer
{
    public string Name { get; }
    public int Priority { get; }
    public bool CanRunInParallel => true;

    public TestSimpleAnalyzer(string name, int priority)
    {
        Name = name;
        Priority = priority;
    }

    public async Task<AnalysisResult> AnalyzeAsync(CrashLog crashLog, CancellationToken cancellationToken = default)
    {
        // Simulate some work with a small delay
        await Task.Delay(5, cancellationToken);

        return new GenericAnalysisResult
        {
            AnalyzerName = Name,
            Success = true
        };
    }
}