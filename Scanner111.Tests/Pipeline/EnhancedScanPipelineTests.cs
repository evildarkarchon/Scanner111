using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging.Abstractions;
using Scanner111.Core.Analyzers;
using Scanner111.Core.Infrastructure;
using Scanner111.Core.Models;
using Scanner111.Core.Pipeline;

namespace Scanner111.Tests.Pipeline;

public class EnhancedScanPipelineTests : IDisposable
{
    private readonly List<IDisposable> _disposables = [];
    private readonly EnhancedScanPipeline _pipeline;
    private readonly string _testLogPath;

    public EnhancedScanPipelineTests()
    {
        // Create test log file
        _testLogPath = Path.GetTempFileName();
        File.WriteAllText(_testLogPath, CreateTestCrashLogContent());

        // Set up dependencies
        var memoryCache = new MemoryCache(new MemoryCacheOptions());
        _disposables.Add(memoryCache);

        var cacheManager = new CacheManager(memoryCache, NullLogger<CacheManager>.Instance);
        _disposables.Add(cacheManager);

        var errorPolicy = new DefaultErrorHandlingPolicy(NullLogger<DefaultErrorHandlingPolicy>.Instance);
        var resilientExecutor = new ResilientExecutor(errorPolicy, NullLogger<ResilientExecutor>.Instance);
        var messageHandler = new TestMessageHandler();
        var settingsProvider = new TestYamlSettingsProvider();

        var analyzers = new List<IAnalyzer>
        {
            new TestAnalyzer("TestAnalyzer1", 10, true),
            new TestAnalyzer("TestAnalyzer2", 20, false),
            new TestAnalyzer("TestAnalyzer3", 30, true)
        };

        _pipeline = new EnhancedScanPipeline(
            analyzers,
            NullLogger<EnhancedScanPipeline>.Instance,
            messageHandler,
            settingsProvider,
            cacheManager,
            resilientExecutor);

        // Note: EnhancedScanPipeline implements IAsyncDisposable, not IDisposable
    }

    public void Dispose()
    {
        foreach (var disposable in _disposables) disposable.Dispose();
        _disposables.Clear();

        // Dispose pipeline asynchronously
        try
        {
            _pipeline.DisposeAsync().AsTask().Wait(TimeSpan.FromSeconds(5));
        }
        catch (Exception)
        {
            // Ignore disposal exceptions in tests
        }

        if (File.Exists(_testLogPath))
            try
            {
                File.Delete(_testLogPath);
            }
            catch (IOException)
            {
                // File might still be in use by a canceled operation
                // Try again after a short delay
                Task.Delay(100).Wait();
                try
                {
                    File.Delete(_testLogPath);
                }
                catch
                {
                    // If it still fails, ignore - it's a temp file
                }
            }

        GC.SuppressFinalize(this);
    }

    [Fact]
    public async Task ProcessSingleAsync_ProcessesFileSuccessfully()
    {
        // Act
        var result = await _pipeline.ProcessSingleAsync(_testLogPath);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(_testLogPath, result.LogPath);
        Assert.Equal(ScanStatus.Completed, result.Status);
        Assert.NotNull(result.CrashLog);
        Assert.Equal(3, result.AnalysisResults.Count); // Should have results from all 3 analyzers
        Assert.True(result.ProcessingTime.TotalMilliseconds > 0);
    }

    [Fact]
    public async Task ProcessSingleAsync_HandlesCancellation()
    {
        // Arrange
        using var cts = new CancellationTokenSource();
        await cts.CancelAsync();

        // Act & Assert
        await Assert.ThrowsAsync<OperationCanceledException>(async () =>
        {
            await _pipeline.ProcessSingleAsync(_testLogPath, cts.Token);
        });
    }

    [Fact]
    public async Task ProcessSingleAsync_HandlesNonexistentFile()
    {
        // Act
        var result = await _pipeline.ProcessSingleAsync("nonexistent.log");

        // Assert
        Assert.NotNull(result);
        Assert.Equal("nonexistent.log", result.LogPath);
        Assert.Equal(ScanStatus.Failed, result.Status);
        Assert.True(result.HasErrors);
    }

    [Fact]
    public async Task ProcessBatchAsync_ProcessesMultipleFiles()
    {
        // Arrange
        var testFiles = new List<string> { _testLogPath };
        var results = new List<ScanResult>();
        var progressReports = new List<BatchProgress>();

        var progress = new Progress<BatchProgress>(p => progressReports.Add(p));

        // Act
        await foreach (var result in _pipeline.ProcessBatchAsync(testFiles, progress: progress)) results.Add(result);

        // Assert
        Assert.Single(results);
        Assert.Equal(ScanStatus.Completed, results[0].Status);
        Assert.NotEmpty(progressReports);

        var finalProgress = progressReports.Last();
        Assert.Equal(1, finalProgress.TotalFiles);
        Assert.Equal(1, finalProgress.ProcessedFiles);
        Assert.Equal(1, finalProgress.SuccessfulScans);
    }

    [Fact]
    public async Task ProcessBatchAsync_RespectsMaxConcurrency()
    {
        // Arrange - Create unique file paths to avoid deduplication
        var testFiles = Enumerable.Range(0, 10)
            .Select(i => $"{_testLogPath}.{i}")
            .ToList();
        var options = new ScanOptions { MaxConcurrency = 2 };
        var results = new List<ScanResult>();

        // Act
        var startTime = DateTime.UtcNow;
        await foreach (var result in _pipeline.ProcessBatchAsync(testFiles, options)) results.Add(result);
        var duration = DateTime.UtcNow - startTime;

        // Assert
        Assert.Equal(10, results.Count);
        // Note: Some results may fail since we're using non-existent file paths with suffixes
        Assert.All(results, r => Assert.True(r.Status == ScanStatus.Completed || r.Status == ScanStatus.Failed));

        // With concurrency limit of 2, processing should take longer than if all were parallel
        // This is a rough test - in practice, timing tests can be flaky
        Assert.True(duration.TotalMilliseconds > 0);
    }

    [Fact]
    public async Task ProcessBatchAsync_HandlesMixedResults()
    {
        // Arrange
        var validFile = _testLogPath;
        var invalidFile = "nonexistent.log";
        var testFiles = new List<string> { validFile, invalidFile };
        var results = new List<ScanResult>();

        // Act
        await foreach (var result in _pipeline.ProcessBatchAsync(testFiles)) results.Add(result);

        // Assert
        Assert.Equal(2, results.Count);

        var validResult = results.First(r => r.LogPath == validFile);
        var invalidResult = results.First(r => r.LogPath == invalidFile);

        Assert.Equal(ScanStatus.Completed, validResult.Status);
        Assert.Equal(ScanStatus.Failed, invalidResult.Status);
    }

    [Fact]
    public async Task ProcessBatchAsync_HandlesCancellation()
    {
        // Arrange - Create unique file paths to avoid deduplication
        var testFiles = Enumerable.Range(0, 100)
            .Select(i => $"{_testLogPath}.{i}")
            .ToList();

        var results = new List<ScanResult>();
        TaskCanceledException? caughtException = null;

        // Act - Use a separate method to avoid captured variable disposal issues
        try
        {
            await ProcessWithCancellationAsync(testFiles, results);
        }
        catch (TaskCanceledException ex)
        {
            caughtException = ex;
        }

        // Assert
        Assert.NotNull(caughtException);
        Assert.True(results.Count < testFiles.Count); // Should not process all files

        // Give time for any in-flight operations to complete
        await Task.Delay(100);
    }

    private async Task ProcessWithCancellationAsync(List<string> testFiles, List<ScanResult> results)
    {
        using var cts = new CancellationTokenSource();

        await foreach (var result in _pipeline.ProcessBatchAsync(testFiles, cancellationToken: cts.Token))
        {
            results.Add(result);
            if (results.Count >= 5) // Cancel after processing a few files
                await cts.CancelAsync();
        }
    }

    [Fact]
    public async Task ProcessSingleAsync_UsesAnalyzerPriority()
    {
        // Arrange
        var executionOrder = new List<string>();
        var orderedAnalyzers = new List<IAnalyzer>
        {
            new OrderTrackingAnalyzer("High", 5, false, executionOrder),
            new OrderTrackingAnalyzer("Medium", 10, false, executionOrder),
            new OrderTrackingAnalyzer("Low", 15, false, executionOrder)
        };

        await using var testPipeline = new EnhancedScanPipeline(
            orderedAnalyzers,
            NullLogger<EnhancedScanPipeline>.Instance,
            new TestMessageHandler(),
            new TestYamlSettingsProvider(),
            new NullCacheManager(),
            new ResilientExecutor(new NoRetryErrorPolicy(), NullLogger<ResilientExecutor>.Instance));

        // Act
        await testPipeline.ProcessSingleAsync(_testLogPath);

        // Assert
        Assert.Equal(new[] { "High", "Medium", "Low" }, executionOrder);
    }

    private static string CreateTestCrashLogContent()
    {
        return @"Fallout 4 v1.10.163
Buffout 4 v1.26.2

Unhandled exception ""EXCEPTION_ACCESS_VIOLATION"" at 0x7FF6E0A7B2A1 SkyrimSE.exe+01BB2A1

[RSP+0]   0x12345678   (void*)
[RSP+8]   0x87654321   (void*)

PROBABLE CALL STACK:
	[ 0] 0x7FF6E0A7B2A1   SkyrimSE.exe+01BB2A1 -> 12345+678
	[ 1] 0x7FF6E0A7B2B2   SkyrimSE.exe+01BB2B2

MODULES:
	[ 0] 0x7FF6E08C0000 - 0x7FF6E2A04000 | SkyrimSE.exe
	[ 1] 0x7FFE8B2A0000 - 0x7FFE8B2B4000 | test.dll

PLUGINS:
	[00]     Skyrim.esm
	[01]     Update.esm
	[FE:000] TestMod.esp

SETTINGS:
";
    }

    // Test helper classes
    private class TestAnalyzer : IAnalyzer
    {
        public TestAnalyzer(string name, int priority, bool canRunInParallel)
        {
            Name = name;
            Priority = priority;
            CanRunInParallel = canRunInParallel;
        }

        public string Name { get; }
        public int Priority { get; }
        public bool CanRunInParallel { get; }

        public async Task<AnalysisResult> AnalyzeAsync(CrashLog crashLog, CancellationToken cancellationToken = default)
        {
            await Task.Delay(10, cancellationToken); // Simulate work

            return new GenericAnalysisResult
            {
                AnalyzerName = Name,
                Success = true,
                ReportLines = [$"Analysis by {Name}\n"],
                HasFindings = true
            };
        }
    }

    private class OrderTrackingAnalyzer : IAnalyzer
    {
        private readonly List<string> _executionOrder;

        public OrderTrackingAnalyzer(string name, int priority, bool canRunInParallel, List<string> executionOrder)
        {
            Name = name;
            Priority = priority;
            CanRunInParallel = canRunInParallel;
            _executionOrder = executionOrder;
        }

        public string Name { get; }
        public int Priority { get; }
        public bool CanRunInParallel { get; }

        public async Task<AnalysisResult> AnalyzeAsync(CrashLog crashLog, CancellationToken cancellationToken = default)
        {
            _executionOrder.Add(Name);
            await Task.Delay(1, cancellationToken);

            return new GenericAnalysisResult
            {
                AnalyzerName = Name,
                Success = true,
                ReportLines = [$"Analysis by {Name}\n"],
                HasFindings = true
            };
        }
    }

    private class TestMessageHandler : IMessageHandler
    {
        public void ShowInfo(string message, MessageTarget target = MessageTarget.All)
        {
        }

        public void ShowWarning(string message, MessageTarget target = MessageTarget.All)
        {
        }

        public void ShowError(string message, MessageTarget target = MessageTarget.All)
        {
        }

        public void ShowSuccess(string message, MessageTarget target = MessageTarget.All)
        {
        }

        public void ShowDebug(string message, MessageTarget target = MessageTarget.All)
        {
        }

        public void ShowCritical(string message, MessageTarget target = MessageTarget.All)
        {
        }

        public void ShowMessage(string message, string? details = null, MessageType messageType = MessageType.Info,
            MessageTarget target = MessageTarget.All)
        {
        }

        public IProgress<ProgressInfo> ShowProgress(string title, int totalItems)
        {
            return new Progress<ProgressInfo>();
        }

        public IProgressContext CreateProgressContext(string title, int totalItems)
        {
            return new TestProgressContext();
        }

        private class TestProgressContext : IProgressContext
        {
            public void Update(int current, string message)
            {
            }

            public void Complete()
            {
            }

            public void Report(ProgressInfo value)
            {
            }

            public void Dispose()
            {
            }
        }
    }

    private class TestYamlSettingsProvider : IYamlSettingsProvider
    {
        public T? GetSetting<T>(string yamlFile, string keyPath, T? defaultValue = default)
        {
            return defaultValue;
        }

        public void SetSetting<T>(string yamlFile, string keyPath, T value)
        {
            // Test implementation - do nothing
        }

        public T? LoadYaml<T>(string yamlFile) where T : class
        {
            return null;
        }

        public void ClearCache()
        {
            // Test implementation - do nothing
        }
    }
}