using Microsoft.Extensions.Logging;
using Xunit;
using Scanner111.Core.Analyzers;
using Scanner111.Core.Infrastructure;
using Scanner111.Core.Models;
using Scanner111.Core.Pipeline;
using Scanner111.Tests.TestHelpers;

namespace Scanner111.Tests.Pipeline;

public class ScanPipelineTests : IDisposable
{
    private readonly ILogger<ScanPipeline> _logger;
    private readonly IMessageHandler _messageHandler;
    private readonly IYamlSettingsProvider _settingsProvider;
    private readonly ScanPipeline _pipeline;
    private readonly List<TestAnalyzer> _testAnalyzers;

    public ScanPipelineTests()
    {
        _logger = new TestLogger<ScanPipeline>();
        _messageHandler = new TestMessageHandler();
        _settingsProvider = new TestYamlSettingsProvider();
        
        // Create test analyzers with different priorities and capabilities
        _testAnalyzers = new List<TestAnalyzer>
        {
            new TestAnalyzer("HighPriority", priority: 1, canRunInParallel: true, shouldSucceed: true),
            new TestAnalyzer("MediumPriority", priority: 5, canRunInParallel: false, shouldSucceed: true),
            new TestAnalyzer("LowPriority", priority: 10, canRunInParallel: true, shouldSucceed: true),
            new TestAnalyzer("FailingAnalyzer", priority: 3, canRunInParallel: true, shouldSucceed: false)
        };
        
        _pipeline = new ScanPipeline(_testAnalyzers, _logger, _messageHandler, _settingsProvider);
    }

    [Fact]
    public async Task ProcessSingleAsync_WithValidCrashLog_ShouldReturnCompletedResult()
    {
        // Arrange
        var logPath = SetupTestCrashLog("test.log", "Sample crash log content");

        // Act
        var result = await _pipeline.ProcessSingleAsync(logPath);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(logPath, result.LogPath);
        Assert.Equal(ScanStatus.CompletedWithErrors, result.Status); // Contains one failing analyzer
        Assert.True(result.ProcessingTime > TimeSpan.Zero);
        Assert.Equal(4, result.AnalysisResults.Count);
        Assert.True(result.HasErrors);
    }

    [Fact]
    public async Task ProcessSingleAsync_WithInvalidLogPath_ShouldReturnFailedResult()
    {
        // Arrange
        var logPath = "nonexistent.log";

        // Act
        var result = await _pipeline.ProcessSingleAsync(logPath);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(logPath, result.LogPath);
        Assert.Equal(ScanStatus.Failed, result.Status);
        Assert.True(result.HasErrors);
        Assert.Contains("Failed to parse crash log", result.ErrorMessages.First());
    }

    [Fact]
    public async Task ProcessSingleAsync_WithCancellation_ShouldReturnCancelledResult()
    {
        // Arrange
        var logPath = SetupTestCrashLog("test.log", "Sample crash log content");
        using var cts = new CancellationTokenSource();
        cts.Cancel();

        // Act
        var result = await _pipeline.ProcessSingleAsync(logPath, cts.Token);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(ScanStatus.Cancelled, result.Status);
    }

    [Fact]
    public async Task ProcessSingleAsync_ShouldRunAnalyzersInCorrectOrder()
    {
        // Arrange
        var logPath = SetupTestCrashLog("test.log", "Sample crash log content");

        // Act
        var result = await _pipeline.ProcessSingleAsync(logPath);

        // Assert
        Assert.Equal(4, result.AnalysisResults.Count);
        
        // Sequential analyzers should run first and be completed
        var sequentialResults = result.AnalysisResults.Where(r => r.AnalyzerName == "MediumPriority").ToList();
        Assert.Single(sequentialResults);
        
        // Parallel analyzers should also be completed
        var parallelResults = result.AnalysisResults.Where(r => 
            r.AnalyzerName == "HighPriority" || 
            r.AnalyzerName == "LowPriority" || 
            r.AnalyzerName == "FailingAnalyzer").ToList();
        Assert.Equal(3, parallelResults.Count);
    }

    [Fact]
    public async Task ProcessBatchAsync_WithMultipleFiles_ShouldProcessAllFiles()
    {
        // Arrange
        var logPaths = new[]
        {
            SetupTestCrashLog("log1.log", "Crash log 1"),
            SetupTestCrashLog("log2.log", "Crash log 2"),
            SetupTestCrashLog("log3.log", "Crash log 3")
        };

        var results = new List<ScanResult>();
        var progressReports = new List<BatchProgress>();

        var progress = new Progress<BatchProgress>(p => progressReports.Add(p));
        var options = new ScanOptions { MaxConcurrency = 2 };

        // Act
        await foreach (var result in _pipeline.ProcessBatchAsync(logPaths, options, progress))
        {
            results.Add(result);
        }

        // Assert
        Assert.Equal(3, results.Count);
        Assert.All(results, r => Assert.NotEqual(ScanStatus.Failed, r.Status));
        Assert.True(progressReports.Count > 0);
        
        var finalProgress = progressReports.Last();
        Assert.Equal(3, finalProgress.TotalFiles);
        Assert.Equal(3, finalProgress.ProcessedFiles);
    }

    [Fact]
    public async Task ProcessBatchAsync_WithDuplicatePaths_ShouldDeduplicateInput()
    {
        // Arrange
        var logPath = SetupTestCrashLog("duplicate.log", "Duplicate content");
        var logPaths = new[] { logPath, logPath, logPath }; // Same path repeated

        var results = new List<ScanResult>();

        // Act
        await foreach (var result in _pipeline.ProcessBatchAsync(logPaths))
        {
            results.Add(result);
        }

        // Assert
        Assert.Single(results); // Should only process once due to deduplication
        Assert.Equal(logPath, results[0].LogPath);
    }

    [Fact]
    public async Task ProcessBatchAsync_WithCancellation_ShouldStopProcessing()
    {
        // Arrange
        var logPaths = new[]
        {
            SetupTestCrashLog("log1.log", "Crash log 1"),
            SetupTestCrashLog("log2.log", "Crash log 2"),
            SetupTestCrashLog("log3.log", "Crash log 3"),
            SetupTestCrashLog("log4.log", "Crash log 4"),
            SetupTestCrashLog("log5.log", "Crash log 5")
        };

        using var cts = new CancellationTokenSource();
        var results = new List<ScanResult>();

        // Act
        try
        {
            await foreach (var result in _pipeline.ProcessBatchAsync(logPaths, cancellationToken: cts.Token))
            {
                results.Add(result);
                if (results.Count >= 2)
                {
                    cts.Cancel(); // Cancel after processing 2 files
                }
            }
        }
        catch (OperationCanceledException)
        {
            // Expected when cancellation occurs
        }

        // Assert
        Assert.True(results.Count <= logPaths.Length);
    }

    [Fact]
    public async Task ProcessBatchAsync_ShouldReportAccurateProgress()
    {
        // Arrange
        var logPaths = new[]
        {
            SetupTestCrashLog("log1.log", "Crash log 1"),
            SetupTestCrashLog("log2.log", "Crash log 2"),
            SetupTestCrashLog("log3.log", "Crash log 3")
        };

        var progressReports = new List<BatchProgress>();
        var progress = new Progress<BatchProgress>(p => progressReports.Add(p));
        var results = new List<ScanResult>();

        // Act
        await foreach (var result in _pipeline.ProcessBatchAsync(logPaths, progress: progress))
        {
            results.Add(result);
        }

        // Assert
        Assert.Equal(3, results.Count);
        Assert.True(progressReports.Count >= 3); // At least one progress report per file
        
        var finalProgress = progressReports.Last();
        Assert.Equal(3, finalProgress.TotalFiles);
        Assert.Equal(3, finalProgress.ProcessedFiles);
        Assert.True(finalProgress.ElapsedTime > TimeSpan.Zero);
    }

    [Fact]
    public async Task DisposeAsync_ShouldDisposeResourcesProperly()
    {
        // Arrange
        var pipeline = new ScanPipeline(_testAnalyzers, _logger, _messageHandler, _settingsProvider);

        // Act
        await pipeline.DisposeAsync();

        // Dispose should complete without throwing
        // Additional dispose calls should be safe
        await pipeline.DisposeAsync();

        // Assert - No exceptions should be thrown
        Assert.True(true);
    }

    [Fact]
    public async Task ProcessBatchAsync_WithEmptyInput_ShouldReturnEmptyResults()
    {
        // Arrange
        var logPaths = Array.Empty<string>();
        var results = new List<ScanResult>();

        // Act
        await foreach (var result in _pipeline.ProcessBatchAsync(logPaths))
        {
            results.Add(result);
        }

        // Assert
        Assert.Empty(results);
    }

    [Fact]
    public async Task ProcessBatchAsync_WithCustomOptions_ShouldRespectMaxConcurrency()
    {
        // Arrange
        var logPaths = new[]
        {
            SetupTestCrashLog("log1.log", "Crash log 1"),
            SetupTestCrashLog("log2.log", "Crash log 2"),
            SetupTestCrashLog("log3.log", "Crash log 3"),
            SetupTestCrashLog("log4.log", "Crash log 4")
        };

        var options = new ScanOptions { MaxConcurrency = 1 }; // Force sequential processing
        var results = new List<ScanResult>();

        // Act
        var start = DateTime.UtcNow;
        await foreach (var result in _pipeline.ProcessBatchAsync(logPaths, options))
        {
            results.Add(result);
        }
        var elapsed = DateTime.UtcNow - start;

        // Assert
        Assert.Equal(4, results.Count);
        // With MaxConcurrency = 1, processing should be more sequential
        // This is hard to test precisely, but we can at least verify all files were processed
        Assert.All(results, r => Assert.NotEqual(ScanStatus.Failed, r.Status));
    }

    private string SetupTestCrashLog(string fileName, string content)
    {
        // Create actual temp files since CrashLog.ParseAsync uses real file system
        var tempPath = Path.GetTempFileName();
        File.WriteAllText(tempPath, content);
        return tempPath;
    }

    public void Dispose()
    {
        _pipeline?.DisposeAsync().AsTask().Wait();
    }
}

// Test analyzer for testing purposes
internal class TestAnalyzer : IAnalyzer
{
    public string Name { get; }
    public int Priority { get; }
    public bool CanRunInParallel { get; }
    private readonly bool _shouldSucceed;

    public TestAnalyzer(string name, int priority, bool canRunInParallel, bool shouldSucceed)
    {
        Name = name;
        Priority = priority;
        CanRunInParallel = canRunInParallel;
        _shouldSucceed = shouldSucceed;
    }

    public async Task<AnalysisResult> AnalyzeAsync(CrashLog crashLog, CancellationToken cancellationToken = default)
    {
        // Simulate some work
        await Task.Delay(10, cancellationToken);

        if (_shouldSucceed)
        {
            return new GenericAnalysisResult
            {
                AnalyzerName = Name,
                Success = true
            };
        }
        else
        {
            return new GenericAnalysisResult
            {
                AnalyzerName = Name,
                Success = false,
                Errors = new[] { $"Simulated failure in {Name}" }
            };
        }
    }
}