using System.Collections.Concurrent;
using System.Diagnostics;
using Scanner111.Core.Analyzers;
using Scanner111.Core.Infrastructure;
using Scanner111.Core.Models;
using Scanner111.Core.Pipeline;
using Scanner111.Tests.TestHelpers;

namespace Scanner111.Tests.Integration;

/// <summary>
///     Tests for concurrency safety and resource management
///     Based on issues identified in async-io-audit-report.md
/// </summary>
public class ConcurrencyAndResourceTests : IDisposable
{
    private readonly List<object> _disposables = new();
    private readonly List<string> _tempFiles = new();

    public void Dispose()
    {
        // Handle async disposables synchronously in Dispose
        var asyncDisposeTasks = new List<Task>();

        foreach (var disposable in _disposables)
            try
            {
                switch (disposable)
                {
                    case IAsyncDisposable asyncDisposable:
                        asyncDisposeTasks.Add(asyncDisposable.DisposeAsync().AsTask());
                        break;
                    case IDisposable syncDisposable:
                        syncDisposable.Dispose();
                        break;
                }
            }
            catch
            {
                // Ignore disposal errors
            }

        // Wait for async disposals to complete
        if (asyncDisposeTasks.Count > 0)
            try
            {
                Task.WaitAll(asyncDisposeTasks.ToArray(), TimeSpan.FromSeconds(5));
            }
            catch
            {
                // Ignore disposal errors
            }

        foreach (var tempFile in _tempFiles)
            try
            {
                if (File.Exists(tempFile)) File.Delete(tempFile);
            }
            catch
            {
                // Ignore cleanup errors
            }

        GC.SuppressFinalize(this);
    }

    [Fact]
    public async Task ReportWriter_ConcurrentWritesToSameFile_ShouldNotCorruptData()
    {
        // Arrange
        var logger = new TestLogger<ReportWriter>();
        var reportWriter = new ReportWriter(logger);
        _disposables.Add(logger);
        _disposables.Add(reportWriter);

        var outputPath = CreateTempFile();
        var tasks = new List<Task<bool>>();
        var expectedContent = new ConcurrentBag<string>();

        // Create multiple scan results that will write to the same file
        for (var i = 0; i < 10; i++)
        {
            var crashLog = new CrashLog
            {
                FilePath = $"test_{i}.log",
                OriginalLines = [$"Sample crash log content {i}"],
                CallStack = [$"  Call stack entry {i}"]
            };

            var scanResult = new ScanResult
            {
                LogPath = $"test_{i}.log",
                Status = ScanStatus.Completed,
                CrashLog = crashLog
            };

            // Add analysis results with actual content
            scanResult.AddAnalysisResult(new GenericAnalysisResult
            {
                AnalyzerName = $"TestAnalyzer_{i}",
                Success = true,
                ReportLines = [$"Test analysis result for item {i}\n"]
            });

            // Verify the scan result has report content before using it
            Assert.NotEmpty(scanResult.ReportText);
            expectedContent.Add(scanResult.ReportText);

            tasks.Add(reportWriter.WriteReportAsync(scanResult, outputPath));
        }

        // Act - Write concurrently to the same file
        var results = await Task.WhenAll(tasks);

        // Assert
        Assert.All(results, Assert.True); // All writes should succeed
        Assert.True(File.Exists(outputPath));

        // File should exist and contain valid content (no corruption)
        var fileContent = await File.ReadAllTextAsync(outputPath);

        // Debug information if the file is still empty
        if (string.IsNullOrEmpty(fileContent))
        {
            var fileInfo = new FileInfo(outputPath);
            Assert.Fail(
                $"File content is empty. File exists: {File.Exists(outputPath)}, " +
                $"File size: {fileInfo.Length} bytes, " +
                $"Expected content samples: {string.Join(", ", expectedContent.Take(3).Select(c => c.Substring(0, Math.Min(50, c.Length))))}");
        }

        Assert.NotEmpty(fileContent);

        // The file should contain content from one of the reports (last writer wins)
        Assert.Contains("TestAnalyzer_", fileContent);
    }

    [Fact]
    public async Task ScanPipeline_ResourceDisposal_ShouldNotLeakSemaphore()
    {
        // Arrange
        var initialHandleCount = GetCurrentProcessHandleCount();
        var pipelines = new List<ScanPipeline>();

        // Act - Create and dispose multiple pipelines
        for (var i = 0; i < 20; i++)
        {
            var pipeline = CreateTestPipeline();
            pipelines.Add(pipeline);
            _disposables.Add(pipeline); // Track for disposal

            // Use the pipeline briefly
            var testLogPath = CreateTempCrashLog($"test_{i}.log", "Sample crash log content");
            var result = await pipeline.ProcessSingleAsync(testLogPath);
            Assert.NotNull(result);
        }

        // Dispose all pipelines
        foreach (var pipeline in pipelines) await pipeline.DisposeAsync();

        // Force garbage collection
        GC.Collect();
        GC.WaitForPendingFinalizers();
        GC.Collect();

        // Wait a bit for handles to be released
        await Task.Delay(100);

        var finalHandleCount = GetCurrentProcessHandleCount();

        // Assert - Handle count should not have grown significantly
        var handleIncrease = finalHandleCount - initialHandleCount;
        Assert.True(handleIncrease < 50,
            $"Handle count increased by {handleIncrease}, indicating potential resource leak");
    }

    [Fact]
    public async Task EnhancedScanPipeline_ResourceDisposal_ShouldNotLeakSemaphore()
    {
        // Arrange
        var initialHandleCount = GetCurrentProcessHandleCount();
        var pipelines = new List<EnhancedScanPipeline>();

        // Act - Create and dispose multiple enhanced pipelines
        for (var i = 0; i < 15; i++)
        {
            var pipeline = CreateTestEnhancedPipeline();
            pipelines.Add(pipeline);
            _disposables.Add(pipeline); // Track for disposal

            // Use the pipeline briefly
            var testLogPath = CreateTempCrashLog($"enhanced_test_{i}.log", "Sample enhanced crash log content");
            var result = await pipeline.ProcessSingleAsync(testLogPath);
            Assert.NotNull(result);
        }

        // Dispose all pipelines
        foreach (var pipeline in pipelines) await pipeline.DisposeAsync();

        // Force garbage collection
        GC.Collect();
        GC.WaitForPendingFinalizers();
        GC.Collect();

        await Task.Delay(100);

        var finalHandleCount = GetCurrentProcessHandleCount();

        // Assert
        var handleIncrease = finalHandleCount - initialHandleCount;
        Assert.True(handleIncrease < 50,
            $"Handle count increased by {handleIncrease}, indicating potential resource leak");
    }

    [Fact]
    public async Task ScanPipeline_ConcurrentBatchProcessing_ShouldHandleFileDeduplication()
    {
        // Arrange
        var pipeline = CreateTestPipeline();
        _disposables.Add(pipeline); // Track for disposal

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

        // Act - Add recent items from multiple threads simultaneously
        for (var i = 0; i < 50; i++)
        {
            var localIndex = i; // Capture loop variable
            var path = $"test_path_{localIndex}.log";

            tasks.Add(Task.Run(() =>
            {
                settings.AddRecentLogFile(path);
                settings.AddRecentGamePath($"game_path_{localIndex}");
                settings.AddRecentScanDirectory($"scan_dir_{localIndex}");
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
        if (settings.RecentLogFiles.Count > 1) Assert.Contains("test_path_", settings.RecentLogFiles[0]);
    }

    [Fact]
    public async Task YamlSettingsCache_ConcurrentInitialization_ShouldBeThreadSafe()
    {
        // Arrange
        YamlSettingsCache.Reset(); // Ensure clean state
        var initializationTasks = new List<Task>();
        var providers = new List<IYamlSettingsProvider>();

        // Create multiple providers
        for (var i = 0; i < 10; i++)
            providers.Add(new TestYamlSettingsProvider());

        // Act - Initialize from multiple threads simultaneously
        foreach (var provider in providers)
        {
            var localProvider = provider; // Capture the provider
            initializationTasks.Add(Task.Run(() => YamlSettingsCache.Initialize(localProvider)));
        }

        await Task.WhenAll(initializationTasks);

        // Assert - Cache should be initialized and functional
        Assert.NotNull(YamlSettingsCache.YamlSettings<string>("test", "test.key", "default"));

        // Should not throw exceptions when accessed concurrently
        var accessTasks = new List<Task>();
        for (var i = 0; i < 20; i++)
            accessTasks.Add(Task.Run(() =>
            {
                var value = YamlSettingsCache.YamlSettings<string>("test", "test.key", "default");
                Assert.NotNull(value);
            }));

        // Should complete without exceptions
        await Task.WhenAll(accessTasks);
    }

    [Fact]
    public async Task CacheManager_ConcurrentOperations_ShouldNotCorruptData()
    {
        // Arrange
        var cacheManager = new TestCacheManager();
        _disposables.Add(cacheManager); // Track for disposal

        var tasks = new List<Task>();
        var keys = new List<string>();

        // Generate keys first to avoid modification in concurrent context
        for (var i = 0; i < 100; i++) keys.Add($"test_key_{i}");

        // Act - Perform concurrent cache operations
        for (var i = 0; i < 100; i++)
        {
            var localIndex = i; // Capture loop variable
            var key = keys[localIndex];
            var analyzerName = $"analyzer_{localIndex % 5}"; // Use fewer analyzer names to create contention
            var result = new GenericAnalysisResult
            {
                AnalyzerName = analyzerName,
                Success = true,
                ReportLines = [$"Test result {localIndex}\n"]
            };

            tasks.Add(Task.Run(() =>
            {
                // Mix of operations to create contention
                cacheManager.CacheAnalysisResult(key, analyzerName, result);
                cacheManager.GetCachedAnalysisResult(key, analyzerName);
                cacheManager.IsFileCacheValid(key);
            }));
        }

        await Task.WhenAll(tasks);

        // Assert
        var stats = cacheManager.GetStatistics();
        Assert.True(stats.CachedFiles >= 0);
        Assert.True(stats.HitRate is >= 0.0 and <= 1.0);

        // Verify some cached items exist
        var foundItems = 0;
        foreach (var key in keys.Take(10))
            for (var i = 0; i < 5; i++)
                if (cacheManager.GetCachedAnalysisResult(key, $"analyzer_{i}") != null)
                    foundItems++;

        Assert.True(foundItems > 0, "No cached items found after concurrent operations");
    }

    [Fact]
    public async Task ReportWriter_HighVolumeWrites_ShouldNotExhaustResources()
    {
        // Arrange
        var logger = new TestLogger<ReportWriter>();
        var reportWriter = new ReportWriter(logger);
        _disposables.Add(reportWriter); // Track for disposal
        var initialMemory = GC.GetTotalMemory(true);
        var tasks = new List<Task<bool>>();

        // Act - Perform many write operations
        for (var i = 0; i < 200; i++)
        {
            var outputPath = CreateTempFile($"high_volume_{i}.md");
            var scanResult = new ScanResult
            {
                LogPath = $"test_{i}.log",
                Status = ScanStatus.Completed,
                CrashLog = new CrashLog
                {
                    FilePath = $"test_{i}.log",
                    OriginalLines = [$"Test crash log content {i}"]
                }
            };

            // Add some content to make the files non-trivial
            for (var j = 0; j < 5; j++)
                scanResult.AddAnalysisResult(new GenericAnalysisResult
                {
                    AnalyzerName = $"Analyzer_{j}",
                    Success = true,
                    ReportLines = [$"Test analysis result {i}_{j}\n"]
                });

            tasks.Add(reportWriter.WriteReportAsync(scanResult, outputPath));
        }

        var results = await Task.WhenAll(tasks);

        // Force garbage collection
        GC.Collect();
        GC.WaitForPendingFinalizers();
        GC.Collect();

        var finalMemory = GC.GetTotalMemory(true);

        // Assert
        Assert.All(results, Assert.True);

        // Memory should not have grown excessively (allow for some growth due to test infrastructure)
        var memoryIncrease = finalMemory - initialMemory;
        Assert.True(memoryIncrease < 50 * 1024 * 1024,
            $"Memory increased by {memoryIncrease / 1024 / 1024}MB, indicating potential memory leak");
    }

    private ScanPipeline CreateTestPipeline()
    {
        var logger = new TestLogger<ScanPipeline>();
        var messageHandler = new TestMessageHandler();
        var settingsProvider = new TestYamlSettingsProvider();
        var analyzers = new List<TestSimpleAnalyzer>
        {
            new("TestAnalyzer1", 1),
            new("TestAnalyzer2", 2)
        };

        var pipeline = new ScanPipeline(analyzers, logger, messageHandler, settingsProvider);

        // Track disposable dependencies (use object collection for mixed types)
        _disposables.Add(logger);
        _disposables.Add(messageHandler);
        _disposables.Add(settingsProvider);

        return pipeline;
    }

    private EnhancedScanPipeline CreateTestEnhancedPipeline()
    {
        var logger = new TestLogger<EnhancedScanPipeline>();
        var messageHandler = new TestMessageHandler();
        var settingsProvider = new TestYamlSettingsProvider();
        var cacheManager = new TestCacheManager();
        var resilientExecutor =
            new ResilientExecutor(new TestErrorHandlingPolicy(), new TestLogger<ResilientExecutor>());
        var analyzers = new List<TestSimpleAnalyzer>
        {
            new("EnhancedAnalyzer1", 1),
            new("EnhancedAnalyzer2", 2)
        };

        var pipeline = new EnhancedScanPipeline(analyzers, logger, messageHandler, settingsProvider, cacheManager,
            resilientExecutor);

        // Track disposable dependencies (use object collection for mixed types)
        _disposables.Add(logger);
        _disposables.Add(messageHandler);
        _disposables.Add(settingsProvider);
        _disposables.Add(cacheManager);
        _disposables.Add(resilientExecutor);

        return pipeline;
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
}

/// <summary>
///     Simple test analyzer for concurrency testing
/// </summary>
internal class TestSimpleAnalyzer : IAnalyzer
{
    public TestSimpleAnalyzer(string name, int priority)
    {
        Name = name;
        Priority = priority;
    }

    public string Name { get; }
    public int Priority { get; }
    public bool CanRunInParallel => true;

    public async Task<AnalysisResult> AnalyzeAsync(CrashLog crashLog, CancellationToken cancellationToken = default)
    {
        // Simulate some work with a small delay
        await Task.Delay(5, cancellationToken).ConfigureAwait(false);

        return new GenericAnalysisResult
        {
            AnalyzerName = Name,
            Success = true,
            ReportLines = [$"Analysis completed by {Name}\n"]
        };
    }
}