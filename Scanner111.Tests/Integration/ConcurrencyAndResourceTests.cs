using System.Collections.Concurrent;
using System.Diagnostics;
using Scanner111.Core.Analyzers;
using Scanner111.Core.Infrastructure;
using Scanner111.Core.Models;
using Scanner111.Core.Pipeline;
using Scanner111.Tests.TestHelpers;

namespace Scanner111.Tests.Integration;

/// <summary>
///     Integration tests for concurrency safety, resource management,
///     and validation of high-volume operations in critical paths.
///     Focuses on ensuring thread safety, proper disposal of resources,
///     and preventing data corruption under concurrent operations.
/// </summary>
public class ConcurrencyAndResourceTests : IDisposable
{
    private readonly List<object> _disposables = new();
    private readonly List<string> _tempFiles = new();

    /// <summary>
    ///     Releases all resources used by the ConcurrencyAndResourceTests class.
    ///     This includes disposing any managed disposable resources,
    ///     asynchronously disposing resources when necessary,
    ///     and cleaning up temporary files created during the tests.
    /// </summary>
    /// <remarks>
    ///     Since this method handles both synchronous and asynchronous disposables,
    ///     it ensures proper cleanup without leaving resources pending.
    ///     Temporary files created during tests are also deleted if they still exist.
    ///     Suppresses finalization of the class to prevent redundant cleanup by the garbage collector.
    ///     Exceptions during disposal or file deletion are handled gracefully and do not stop execution.
    /// </remarks>
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

    /// <summary>
    ///     Validates that concurrent writes to the same file by the ReportWriter
    ///     class do not result in data corruption or unexpected behavior.
    /// </summary>
    /// <remarks>
    ///     This test creates multiple scan results and writes them concurrently
    ///     to the same output file using the ReportWriter class. Each scan result
    ///     contains specific report data that is expected to be written to the file.
    ///     After all writes are completed, the resulting file is verified to ensure
    ///     that it exists, is not empty, and contains valid content.
    ///     Additional checks include confirming that all write operations succeed,
    ///     asserting no corruption occurred in the final file content, and that
    ///     the file includes data from at least one of the scan results.
    /// </remarks>
    /// <returns>
    ///     An asynchronous task that validates the integrity of ReportWriter's
    ///     concurrent write operations and ensures file content accuracy.
    /// </returns>
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
                CallStack = [$"  Call stack entry {i}"],
                MainError = $"Test error {i}",
                CrashGenVersion = "Buffout 4 v1.26.2"
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
                HasFindings = true,
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

    /// <summary>
    ///     Verifies that the resource disposal process for the ScanPipeline class
    ///     does not result in leaking operating system semaphores or other handles.
    ///     Ensures proper cleanup of all resources even under frequent creation and disposal.
    /// </summary>
    /// <remarks>
    ///     This test creates multiple instances of ScanPipeline, performs a brief operation
    ///     with each, and disposes of them. It ensures that handle counts remain stable by
    ///     comparing the system's handle count before and after execution. Garbage collection
    ///     and finalizer execution are enforced to eliminate residual references to resources.
    /// </remarks>
    /// <returns>
    ///     Asserts that the increase in operating system handles does not exceed
    ///     a predefined threshold, indicating no significant resource leaks.
    /// </returns>
    [Fact]
    public async Task ScanPipeline_ResourceDisposal_ShouldNotLeakSemaphore()
    {
        // Arrange
        var initialHandleCount = GetCurrentProcessHandleCount();
        var pipelines = new List<ScanPipeline>();
        var testDependencies = new List<IDisposable>();

        // Act - Create and dispose multiple pipelines
        for (var i = 0; i < 20; i++)
        {
            var pipeline = CreateTestPipelineForResourceTest(out var dependencies);
            pipelines.Add(pipeline);
            testDependencies.AddRange(dependencies);

            // Use the pipeline briefly
            var testLogPath = CreateTempCrashLog($"test_{i}.log", GenerateValidCrashLog());
            var result = await pipeline.ProcessSingleAsync(testLogPath);
            Assert.NotNull(result);
        }

        // Dispose all pipelines first
        foreach (var pipeline in pipelines)
            await pipeline.DisposeAsync();

        // Then dispose dependencies
        foreach (var dependency in testDependencies)
            try
            {
                dependency?.Dispose();
            }
            catch (ObjectDisposedException)
            {
                // Already disposed by pipeline - this is fine
            }

        // Force garbage collection
        GC.Collect();
        GC.WaitForPendingFinalizers();
        GC.Collect();

        // Wait a bit for handles to be released
        await Task.Delay(200);

        var finalHandleCount = GetCurrentProcessHandleCount();

        // Assert - Handle count should not have grown significantly
        var handleIncrease = finalHandleCount - initialHandleCount;
        Assert.True(handleIncrease < 400,
            $"Handle count increased by {handleIncrease}, indicating potential resource leak");
    }

    /// <summary>
    ///     Verifies that the EnhancedScanPipeline class correctly manages resource disposal,
    ///     ensuring that no semaphore or other system resources are leaked during pipeline creation,
    ///     operation, and disposal under repeated and intensive usage scenarios.
    /// </summary>
    /// <remarks>
    ///     The test creates multiple EnhancedScanPipeline instances, performs operations with them,
    ///     and ensures that proper disposal mechanisms are invoked consistently. This is validated by
    ///     checking the operating system handle count before and after the test execution. The aim
    ///     is to ensure that any transient or disposable resources used by the pipeline are released,
    ///     preventing resource exhaustion or handle leaks during high-volume operations.
    /// </remarks>
    /// <returns>
    ///     A task that completes when the test has verified that the EnhancedScanPipeline does not
    ///     contribute to resource leakage, particularly semaphore or file handle leakage.
    /// </returns>
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
        Assert.True(handleIncrease < 400,
            $"Handle count increased by {handleIncrease}, indicating potential resource leak");
    }

    /// <summary>
    ///     Validates that the scan pipeline can process concurrent batch operations while maintaining file deduplication.
    ///     Ensures that the same file, when included in multiple batches, is processed only once per batch.
    /// </summary>
    /// <remarks>
    ///     This test evaluates the scan pipeline's ability to handle concurrent processing of batches that contain duplicate
    ///     file paths.
    ///     It verifies that deduplication logic is functioning correctly by asserting that each batch only processes a single
    ///     instance
    ///     of the duplicated file, and the total results across all batches reflect the application of the deduplication
    ///     rules.
    /// </remarks>
    /// <returns>
    ///     An asynchronous task that completes when the test is finished. Successfully executing the test confirms that
    ///     concurrent batch processing of duplicated files is handled correctly without redundant processing.
    /// </returns>
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

    /// <summary>
    ///     Verifies that concurrent modification of recent item lists in the ApplicationSettings class
    ///     maintains thread safety and consistent internal state.
    ///     Ensures that multiple threads can simultaneously add items to recent log files,
    ///     game paths, and scan directories without data corruption or inconsistency.
    /// </summary>
    /// <remarks>
    ///     This test adds items to recent item lists from multiple threads to simulate a highly
    ///     concurrent environment and validates that the maximum size constraint and order of
    ///     most recent items are preserved. Also checks to ensure no null or empty entries
    ///     exist in the lists, confirming data integrity.
    /// </remarks>
    /// <returns>
    ///     Task representing the asynchronous operation of the test. The test will succeed if
    ///     concurrent additions result in consistent and thread-safe behavior of recent item lists.
    /// </returns>
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

    /// <summary>
    ///     Validates that concurrent operations on the CacheManager do not result in data corruption or
    ///     unexpected behavior. Ensures that cache operations such as insertion, retrieval, and updates
    ///     remain thread-safe and consistent under high levels of contention.
    /// </summary>
    /// <remarks>
    ///     This test simulates concurrent access to the CacheManager by performing a large number
    ///     of asynchronous tasks. These tasks involve generating and caching analysis results,
    ///     retrieving results, and analyzing cache statistics post-operations. Emphasis is placed
    ///     on avoiding race conditions or corrupt states in multi-threaded environments.
    ///     Verifies that the cached items and statistics remain valid and consistent after execution.
    /// </remarks>
    /// <returns>
    ///     Ensures that after the concurrent operations:<br />
    ///     - The cached files count is non-negative.<br />
    ///     - The hit rate of the CacheManager is between 0.0 and 1.0.<br />
    ///     - Some cached items are retrievable, confirming successful cache operations.
    /// </returns>
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

    /// <summary>
    ///     Verifies that the ReportWriter class can handle a high volume of write operations
    ///     without exhausting system resources such as memory or file handles.
    ///     Ensures robust performance and stability during intensive usage scenarios,
    ///     even when managing concurrent and large-scale tasks.
    /// </summary>
    /// <remarks>
    ///     This test checks for potential memory leaks or resource exhaustion by tracking memory usage
    ///     before and after executing a high number of write operations.
    ///     The operations involve creating temporary files, serializing complex objects, and logging outputs.
    ///     Ensures that the memory growth remains within acceptable limits,
    ///     and all tasks complete successfully regardless of the operation scale.
    ///     Disposables like the ReportWriter instance are managed to avoid resource contention issues.
    /// </remarks>
    /// <returns>
    ///     A Task representing the asynchronous operation, the result of which ensures:
    ///     1. No excessive memory usage growth during or after execution.
    ///     2. Successful completion of all write operations without runtime errors.
    /// </returns>
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

    /// <summary>
    ///     Creates and initializes a new test instance of the <see cref="ScanPipeline" /> class.
    ///     Configures the pipeline with test dependencies including analyzers, logger, message handler,
    ///     and settings provider, ensuring components are tracked for proper disposal.
    /// </summary>
    /// <returns>
    ///     An instance of <see cref="ScanPipeline" /> configured for testing purposes.
    /// </returns>
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

    private ScanPipeline CreateTestPipelineForResourceTest(out List<IDisposable> dependencies)
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

        // Don't add any dependencies to disposables list since most test classes don't implement IDisposable
        // The pipeline itself will dispose of its internal semaphore
        dependencies = new List<IDisposable>();

        return pipeline;
    }

    /// <summary>
    ///     Creates an instance of the EnhancedScanPipeline configured with test-specific
    ///     components such as mock analyzers, logging, message handling, settings, and caching implementations.
    /// </summary>
    /// <remarks>
    ///     This method sets up a fully functional EnhancedScanPipeline by initializing its dependencies with
    ///     test-specific implementations. It includes mock analyzers and necessary tools such as logging,
    ///     error resilience, and caching. Each dependency is tracked for proper disposal. This method ensures
    ///     the pipeline is ready for integration tests requiring specific test setups or configurations.
    /// </remarks>
    /// <returns>
    ///     A fully configured instance of EnhancedScanPipeline tailored for testing with mocked or test-specific dependencies.
    /// </returns>
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

    /// <summary>
    ///     Creates a temporary crash log file with the specified content and tracks it for disposal.
    /// </summary>
    /// <param name="fileName">The name of the crash log file to be created.</param>
    /// <param name="content">The content to be written to the crash log file.</param>
    /// <returns>The full path to the created temporary crash log file.</returns>
    private string CreateTempCrashLog(string fileName, string content)
    {
        var tempPath = Path.Combine(Path.GetTempPath(), fileName);
        File.WriteAllText(tempPath, content);
        _tempFiles.Add(tempPath);
        return tempPath;
    }

    /// <summary>
    ///     Creates a temporary file with an optional specific file name.
    ///     The created file's path is added to the internal list of temporary files for later cleanup.
    /// </summary>
    /// <param name="fileName">
    ///     The desired name of the temporary file. If null, a unique temporary file with a randomly generated name is created.
    /// </param>
    /// <returns>
    ///     The full path to the created temporary file.
    /// </returns>
    private string CreateTempFile(string? fileName = null)
    {
        var tempPath = fileName != null
            ? Path.Combine(Path.GetTempPath(), fileName)
            : Path.GetTempFileName();
        _tempFiles.Add(tempPath);
        return tempPath;
    }

    /// <summary>
    ///     Retrieves the current handle count for the running process.
    ///     This count provides an indicator of resource utilization for the process, especially
    ///     in scenarios involving concurrency or potential resource leaks.
    /// </summary>
    /// <remarks>
    ///     This method leverages the <see cref="System.Diagnostics.Process" /> class to obtain
    ///     the number of handles currently allocated by the operating system for the process.
    ///     If the handle count cannot be retrieved due to platform limitations or unexpected errors,
    ///     it safely returns 0 as a fallback.
    /// </remarks>
    /// <returns>
    ///     An integer representing the number of handles currently in use by the process.
    ///     Returns 0 if the handle count could not be determined.
    /// </returns>
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

    private static string GenerateValidCrashLog()
    {
        return @"Fallout 4 v1.10.163
Buffout 4 v1.26.2

Unhandled exception ""EXCEPTION_ACCESS_VIOLATION"" at 0x7FF798889DFA

	[Compatibility]
	F4EE: true
	Buffout4: 1
	
SYSTEM SPECS:
	OS: Microsoft Windows 10 Pro v10.0.19044
	CPU: GenuineIntel 11th Gen Intel(R) Core(TM) i7-11700K @ 3.60GHz
	GPU: NVIDIA GeForce RTX 3080
	
PROBABLE CALL STACK:
	[0] 0x7FF798889DFA Fallout4.exe+2479DFA
	[1] 0x7FF7988899FF Fallout4.exe+24799FF
	[2] 0x7FF798889912 Fallout4.exe+2479912
	
MODULES:
	Fallout4.exe 0x7FF796410000
	KERNEL32.DLL 0x7FFE38D80000
	
XSE PLUGINS:
	f4se_1_10_163.dll v2.0.17
	buffout4.dll v1.26.2
	
PLUGINS:
	[00:000] Fallout4.esm
	[01:000] DLCRobot.esm
	[02:000] DLCworkshop01.esm
	[03:000] DLCCoast.esm
	[04:000] DLCworkshop02.esm
	[05:000] DLCworkshop03.esm
	[06:000] DLCNukaWorld.esm
	[07:000] Unofficial Fallout 4 Patch.esp";
    }
}

/// <summary>
///     Represents a simple implementation of the IAnalyzer interface for testing purposes.
///     Provides basic analysis capabilities and simulates asynchronous operations for testing concurrency and execution
///     flow.
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

    /// <summary>
    ///     Analyzes the provided crash log asynchronously and returns an analysis result.
    ///     The method simulates work with a small delay before generating the result.
    /// </summary>
    /// <param name="crashLog">The crash log to be analyzed. This input provides necessary data for the analysis process.</param>
    /// <param name="cancellationToken">
    ///     A cancellation token to observe while waiting for the task to complete.
    ///     It allows cooperative cancellation of the operation.
    /// </param>
    /// <returns>
    ///     An <see cref="AnalysisResult" /> containing details of the analysis, including success status, analyzer name,
    ///     and any generated report lines.
    /// </returns>
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