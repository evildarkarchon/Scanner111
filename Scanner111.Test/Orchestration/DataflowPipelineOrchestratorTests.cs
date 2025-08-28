using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using NSubstitute;
using Scanner111.Core.Analysis;
using Scanner111.Core.Configuration;
using Scanner111.Core.Orchestration;
using Scanner111.Core.Reporting;
using Xunit;

namespace Scanner111.Test.Orchestration;

/// <summary>
/// Comprehensive tests for DataflowPipelineOrchestrator covering TPL Dataflow scenarios,
/// parallel processing, cancellation, error handling, and performance metrics.
/// </summary>
[Trait("Category", "Integration")]
[Trait("Performance", "Medium")]
[Trait("Component", "Orchestration")]
public sealed class DataflowPipelineOrchestratorTests : IDisposable
{
    private readonly ILogger<DataflowPipelineOrchestrator> _logger;
    private readonly IReportComposer _reportComposer;
    private readonly IAsyncYamlSettingsCore _yamlCore;
    private readonly DataflowPipelineOrchestrator _orchestrator;
    private readonly string _testDirectory;
    private readonly List<string> _tempFiles;

    public DataflowPipelineOrchestratorTests()
    {
        _logger = Substitute.For<ILogger<DataflowPipelineOrchestrator>>();
        _reportComposer = Substitute.For<IReportComposer>();
        _yamlCore = Substitute.For<IAsyncYamlSettingsCore>();
        _orchestrator = new DataflowPipelineOrchestrator(_logger, _reportComposer, _yamlCore);
        
        _testDirectory = Path.Combine(Path.GetTempPath(), $"Scanner111_Tests_{Guid.NewGuid()}");
        Directory.CreateDirectory(_testDirectory);
        _tempFiles = new List<string>();
    }

    #region Constructor Tests

    [Fact]
    public void Constructor_WithNullLogger_ThrowsArgumentNullException()
    {
        // Arrange & Act
        var act = () => new DataflowPipelineOrchestrator(
            null!, 
            _reportComposer, 
            _yamlCore);

        // Assert
        act.Should().Throw<ArgumentNullException>()
            .WithParameterName("logger");
    }

    [Fact]
    public void Constructor_WithNullReportComposer_ThrowsArgumentNullException()
    {
        // Arrange & Act
        var act = () => new DataflowPipelineOrchestrator(
            _logger, 
            null!, 
            _yamlCore);

        // Assert
        act.Should().Throw<ArgumentNullException>()
            .WithParameterName("reportComposer");
    }

    [Fact]
    public void Constructor_WithNullYamlCore_ThrowsArgumentNullException()
    {
        // Arrange & Act
        var act = () => new DataflowPipelineOrchestrator(
            _logger, 
            _reportComposer, 
            null!);

        // Assert
        act.Should().Throw<ArgumentNullException>()
            .WithParameterName("yamlCore");
    }

    [Fact]
    public void Constructor_WithValidDependencies_CreatesInstance()
    {
        // Act
        var orchestrator = new DataflowPipelineOrchestrator(_logger, _reportComposer, _yamlCore);

        // Assert
        orchestrator.Should().NotBeNull();
    }

    #endregion

    #region Basic Pipeline Processing Tests

    [Fact]
    public async Task ProcessBatchAsync_WithEmptyRequests_ReturnsSuccessfulResult()
    {
        // Arrange
        var requests = Enumerable.Empty<AnalysisRequest>();
        var analyzers = new List<IAnalyzer>();

        // Act
        var result = await _orchestrator.ProcessBatchAsync(requests, analyzers);

        // Assert
        result.Success.Should().BeTrue();
        result.Results.Should().BeEmpty();
        result.ProcessedCount.Should().Be(0);
        result.TotalTime.Should().BeGreaterThan(TimeSpan.Zero);
    }

    [Fact]
    public async Task ProcessBatchAsync_WithSingleRequest_ProcessesSuccessfully()
    {
        // Arrange
        var testFile = CreateTestFile("test.log", "Test content");
        var request = new AnalysisRequest { InputPath = testFile };
        var requests = new[] { request };
        
        var analyzer = CreateMockAnalyzer("TestAnalyzer", 1);
        var analyzers = new[] { analyzer };

        var report = CreateTestReport();
        _reportComposer.ComposeFromFragmentsAsync(Arg.Any<IEnumerable<ReportFragment>>(), Arg.Any<ReportOptions>())
            .Returns(report);

        // Act
        var result = await _orchestrator.ProcessBatchAsync(requests, analyzers);

        // Assert
        result.Success.Should().BeTrue();
        result.ProcessedCount.Should().Be(1);
        result.Results.Should().HaveCount(1);
        result.Results[0].Success.Should().BeTrue();
        result.ThroughputPerSecond.Should().BeGreaterThan(0);
        
        await analyzer.Received(1).AnalyzeAsync(Arg.Any<AnalysisContext>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ProcessBatchAsync_WithMultipleRequests_ProcessesInParallel()
    {
        // Arrange
        var testFiles = Enumerable.Range(1, 10)
            .Select(i => CreateTestFile($"test{i}.log", $"Test content {i}"))
            .ToList();
        
        var requests = testFiles.Select(f => new AnalysisRequest { InputPath = f }).ToArray();
        
        var analyzer = CreateMockAnalyzer("TestAnalyzer", 1);
        var analyzers = new[] { analyzer };

        var report = CreateTestReport();
        _reportComposer.ComposeFromFragmentsAsync(Arg.Any<IEnumerable<ReportFragment>>(), Arg.Any<ReportOptions>())
            .Returns(report);

        // Act
        var result = await _orchestrator.ProcessBatchAsync(requests, analyzers);

        // Assert
        result.Success.Should().BeTrue();
        result.ProcessedCount.Should().Be(10);
        result.Results.Should().HaveCount(10);
        result.StageMetrics.Should().ContainKeys("LoadStage", "ProcessStage", "ReportStage");
        
        await analyzer.Received(10).AnalyzeAsync(Arg.Any<AnalysisContext>(), Arg.Any<CancellationToken>());
    }

    #endregion

    #region Pipeline Options Tests

    [Fact]
    public async Task ProcessBatchAsync_WithCustomBatchSize_RespectsBatchConfiguration()
    {
        // Arrange
        var requests = Enumerable.Range(1, 25)
            .Select(i => new AnalysisRequest { InputPath = $"fake{i}.log" })
            .ToArray();
        
        var analyzer = CreateMockAnalyzer("TestAnalyzer", 1);
        var analyzers = new[] { analyzer };
        
        var options = new PipelineOptions
        {
            BatchSize = 5,
            BoundedCapacity = 10,
            MaxAnalysisParallelism = 2
        };

        var report = CreateTestReport();
        _reportComposer.ComposeFromFragmentsAsync(Arg.Any<IEnumerable<ReportFragment>>(), Arg.Any<ReportOptions>())
            .Returns(report);

        // Act
        var result = await _orchestrator.ProcessBatchAsync(requests, analyzers, options);

        // Assert
        result.Success.Should().BeTrue();
        result.ProcessedCount.Should().Be(25);
    }

    [Fact]
    public async Task ProcessBatchAsync_WithHighThroughputOptions_UsesOptimizedSettings()
    {
        // Arrange
        var requests = Enumerable.Range(1, 100)
            .Select(i => new AnalysisRequest { InputPath = $"fake{i}.log" })
            .ToArray();
        
        var analyzer = CreateMockAnalyzer("TestAnalyzer", 1);
        var analyzers = new[] { analyzer };

        var report = CreateTestReport();
        _reportComposer.ComposeFromFragmentsAsync(Arg.Any<IEnumerable<ReportFragment>>(), Arg.Any<ReportOptions>())
            .Returns(report);

        // Act
        var result = await _orchestrator.ProcessBatchAsync(
            requests, 
            analyzers, 
            PipelineOptions.HighThroughput);

        // Assert
        result.Success.Should().BeTrue();
        result.ProcessedCount.Should().Be(100);
        result.ThroughputPerSecond.Should().BeGreaterThan(0);
    }

    #endregion

    #region Analyzer Priority and Grouping Tests

    [Fact]
    public async Task ProcessBatchAsync_WithDifferentPriorities_ProcessesInOrder()
    {
        // Arrange
        var testFile = CreateTestFile("test.log", "Test content");
        var request = new AnalysisRequest { InputPath = testFile };
        
        var callOrder = new List<string>();
        var analyzer1 = CreateTrackingAnalyzer("HighPriority", 1, callOrder);
        var analyzer2 = CreateTrackingAnalyzer("MediumPriority", 5, callOrder);
        var analyzer3 = CreateTrackingAnalyzer("LowPriority", 10, callOrder);
        
        var analyzers = new[] { analyzer3, analyzer1, analyzer2 }; // Mixed order

        var report = CreateTestReport();
        _reportComposer.ComposeFromFragmentsAsync(Arg.Any<IEnumerable<ReportFragment>>(), Arg.Any<ReportOptions>())
            .Returns(report);

        // Act
        var result = await _orchestrator.ProcessBatchAsync(new[] { request }, analyzers);

        // Assert
        result.Success.Should().BeTrue();
        callOrder.Should().BeEquivalentTo(new[] { "HighPriority", "MediumPriority", "LowPriority" }, 
            options => options.WithStrictOrdering());
    }

    [Fact]
    public async Task ProcessBatchAsync_WithSamePriority_ProcessesInParallel()
    {
        // Arrange
        var testFile = CreateTestFile("test.log", "Test content");
        var request = new AnalysisRequest { InputPath = testFile };
        
        var startTimes = new List<(string name, DateTime time)>();
        var analyzer1 = CreateTimingAnalyzer("Analyzer1", 1, startTimes);
        var analyzer2 = CreateTimingAnalyzer("Analyzer2", 1, startTimes);
        var analyzer3 = CreateTimingAnalyzer("Analyzer3", 1, startTimes);
        
        var analyzers = new[] { analyzer1, analyzer2, analyzer3 };

        var report = CreateTestReport();
        _reportComposer.ComposeFromFragmentsAsync(Arg.Any<IEnumerable<ReportFragment>>(), Arg.Any<ReportOptions>())
            .Returns(report);

        // Act
        var result = await _orchestrator.ProcessBatchAsync(new[] { request }, analyzers);

        // Assert
        result.Success.Should().BeTrue();
        
        // Check that all analyzers started within a short time window (indicating parallel execution)
        var minTime = startTimes.Min(t => t.time);
        var maxTime = startTimes.Max(t => t.time);
        (maxTime - minTime).TotalMilliseconds.Should().BeLessThan(100);
    }

    #endregion

    #region Cancellation Tests

    [Fact]
    public async Task ProcessBatchAsync_WithCancellation_StopsProcessing()
    {
        // Arrange
        var cts = new CancellationTokenSource();
        var requests = Enumerable.Range(1, 100)
            .Select(i => CreateTestFile($"test{i}.log", $"Content {i}"))
            .Select(f => new AnalysisRequest { InputPath = f })
            .ToArray();
        
        var analyzer = Substitute.For<IAnalyzer>();
        analyzer.Name.Returns("SlowAnalyzer");
        analyzer.Priority.Returns(1);
        analyzer.AnalyzeAsync(Arg.Any<AnalysisContext>(), Arg.Any<CancellationToken>())
            .Returns(async ci =>
            {
                await Task.Delay(100, ci.Arg<CancellationToken>());
                return new AnalysisResult("SlowAnalyzer") { Success = true };
            });
        
        var analyzers = new[] { analyzer };

        // Act
        cts.CancelAfter(50);
        var result = await _orchestrator.ProcessBatchAsync(requests, analyzers, cancellationToken: cts.Token);

        // Assert
        result.Success.Should().BeFalse();
        result.ProcessedCount.Should().BeLessThan(100);
    }

    [Fact]
    public async Task ProcessBatchAsync_WithPipelineCancellation_PropagatesCancellation()
    {
        // Arrange
        var testFile = CreateTestFile("test.log", "Test content");
        var request = new AnalysisRequest { InputPath = testFile };
        
        CancellationToken capturedToken = default;
        var analyzer = Substitute.For<IAnalyzer>();
        analyzer.Name.Returns("TestAnalyzer");
        analyzer.Priority.Returns(1);
        analyzer.AnalyzeAsync(Arg.Any<AnalysisContext>(), Arg.Any<CancellationToken>())
            .Returns(ci =>
            {
                capturedToken = ci.Arg<CancellationToken>();
                return Task.FromResult(new AnalysisResult("TestAnalyzer") { Success = true });
            });

        var report = CreateTestReport();
        _reportComposer.ComposeFromFragmentsAsync(Arg.Any<IEnumerable<ReportFragment>>(), Arg.Any<ReportOptions>())
            .Returns(report);

        // Act
        var cts = new CancellationTokenSource();
        var task = _orchestrator.ProcessBatchAsync(new[] { request }, new[] { analyzer }, cancellationToken: cts.Token);
        cts.Cancel();
        
        var result = await task;

        // Assert
        capturedToken.IsCancellationRequested.Should().BeTrue();
    }

    #endregion

    #region Error Handling Tests

    [Fact]
    public async Task ProcessBatchAsync_WithAnalyzerException_ContinuesProcessing()
    {
        // Arrange
        var testFile = CreateTestFile("test.log", "Test content");
        var request = new AnalysisRequest { InputPath = testFile };
        
        var failingAnalyzer = Substitute.For<IAnalyzer>();
        failingAnalyzer.Name.Returns("FailingAnalyzer");
        failingAnalyzer.Priority.Returns(1);
        failingAnalyzer.AnalyzeAsync(Arg.Any<AnalysisContext>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromException<AnalysisResult>(new InvalidOperationException("Analyzer failed")));
        
        var successAnalyzer = CreateMockAnalyzer("SuccessAnalyzer", 1);
        var analyzers = new[] { failingAnalyzer, successAnalyzer };

        var report = CreateTestReport();
        _reportComposer.ComposeFromFragmentsAsync(Arg.Any<IEnumerable<ReportFragment>>(), Arg.Any<ReportOptions>())
            .Returns(report);

        // Act
        var result = await _orchestrator.ProcessBatchAsync(new[] { request }, analyzers);

        // Assert
        result.Success.Should().BeTrue();
        result.ProcessedCount.Should().Be(1);
        
        await successAnalyzer.Received(1).AnalyzeAsync(Arg.Any<AnalysisContext>(), Arg.Any<CancellationToken>());
        _logger.Received().Log(
            LogLevel.Error,
            Arg.Any<EventId>(),
            Arg.Is<object>(o => o.ToString()!.Contains("FailingAnalyzer failed")),
            Arg.Any<Exception>(),
            Arg.Any<Func<object, Exception?, string>>());
    }

    [Fact]
    public async Task ProcessBatchAsync_WithNonExistentFile_HandlesGracefully()
    {
        // Arrange
        var request = new AnalysisRequest { InputPath = "nonexistent.log" };
        var analyzer = CreateMockAnalyzer("TestAnalyzer", 1);

        var report = CreateTestReport();
        _reportComposer.ComposeFromFragmentsAsync(Arg.Any<IEnumerable<ReportFragment>>(), Arg.Any<ReportOptions>())
            .Returns(report);

        // Act
        var result = await _orchestrator.ProcessBatchAsync(new[] { request }, new[] { analyzer });

        // Assert
        result.Success.Should().BeTrue();
        result.ProcessedCount.Should().Be(1);
    }

    #endregion

    #region Metrics and Performance Tests

    [Fact]
    public async Task ProcessBatchAsync_WithMetrics_RecordsStageTimings()
    {
        // Arrange
        var testFiles = Enumerable.Range(1, 5)
            .Select(i => CreateTestFile($"test{i}.log", $"Content {i}"))
            .Select(f => new AnalysisRequest { InputPath = f })
            .ToArray();
        
        var analyzer = CreateMockAnalyzer("TestAnalyzer", 1);

        var report = CreateTestReport();
        _reportComposer.ComposeFromFragmentsAsync(Arg.Any<IEnumerable<ReportFragment>>(), Arg.Any<ReportOptions>())
            .Returns(report);

        // Act
        var result = await _orchestrator.ProcessBatchAsync(testFiles, new[] { analyzer });

        // Assert
        result.Success.Should().BeTrue();
        result.StageMetrics.Should().NotBeNull();
        result.StageMetrics.Should().ContainKey("LoadStage");
        result.StageMetrics.Should().ContainKey("ProcessStage");
        result.StageMetrics.Should().ContainKey("ReportStage");
        
        result.StageMetrics!["LoadStage"].Should().BeGreaterThan(TimeSpan.Zero);
        result.StageMetrics["ProcessStage"].Should().BeGreaterThan(TimeSpan.Zero);
        result.StageMetrics["ReportStage"].Should().BeGreaterThan(TimeSpan.Zero);
    }

    [Fact]
    public async Task ProcessBatchAsync_WithLargeDataset_CalculatesThroughput()
    {
        // Arrange
        var requests = Enumerable.Range(1, 50)
            .Select(i => new AnalysisRequest { InputPath = $"fake{i}.log" })
            .ToArray();
        
        var analyzer = CreateMockAnalyzer("FastAnalyzer", 1);

        var report = CreateTestReport();
        _reportComposer.ComposeFromFragmentsAsync(Arg.Any<IEnumerable<ReportFragment>>(), Arg.Any<ReportOptions>())
            .Returns(report);

        // Act
        var result = await _orchestrator.ProcessBatchAsync(requests, new[] { analyzer });

        // Assert
        result.Success.Should().BeTrue();
        result.ProcessedCount.Should().Be(50);
        result.ThroughputPerSecond.Should().BeGreaterThan(0);
        result.TotalTime.Should().BeGreaterThan(TimeSpan.Zero);
    }

    #endregion

    #region Context Sharing Tests

    [Fact]
    public async Task ProcessBatchAsync_WithFileContent_SharesDataBetweenAnalyzers()
    {
        // Arrange
        var testFile = CreateTestFile("test.log", "Shared test content");
        var request = new AnalysisRequest { InputPath = testFile };
        
        string? capturedContent = null;
        var analyzer = Substitute.For<IAnalyzer>();
        analyzer.Name.Returns("ContentAnalyzer");
        analyzer.Priority.Returns(1);
        analyzer.AnalyzeAsync(Arg.Any<AnalysisContext>(), Arg.Any<CancellationToken>())
            .Returns(ci =>
            {
                var context = ci.Arg<AnalysisContext>();
                context.TryGetSharedData<string>("FileContent", out var content);
                capturedContent = content;
                return Task.FromResult(new AnalysisResult("ContentAnalyzer") { Success = true });
            });

        var report = CreateTestReport();
        _reportComposer.ComposeFromFragmentsAsync(Arg.Any<IEnumerable<ReportFragment>>(), Arg.Any<ReportOptions>())
            .Returns(report);

        // Act
        var result = await _orchestrator.ProcessBatchAsync(new[] { request }, new[] { analyzer });

        // Assert
        result.Success.Should().BeTrue();
        capturedContent.Should().Be("Shared test content");
    }

    #endregion

    #region Disposal Tests

    [Fact]
    public async Task DisposeAsync_WhenCalled_DisposesResources()
    {
        // Arrange
        var orchestrator = new DataflowPipelineOrchestrator(_logger, _reportComposer, _yamlCore);

        // Act
        await orchestrator.DisposeAsync();
        
        // Try to use after disposal
        var act = () => orchestrator.ProcessBatchAsync(
            new[] { new AnalysisRequest { InputPath = "test.log" } }, 
            new List<IAnalyzer>());

        // Assert
        await act.Should().ThrowAsync<ObjectDisposedException>();
    }

    [Fact]
    public async Task DisposeAsync_WhenCalledMultipleTimes_DoesNotThrow()
    {
        // Arrange
        var orchestrator = new DataflowPipelineOrchestrator(_logger, _reportComposer, _yamlCore);

        // Act & Assert
        await orchestrator.DisposeAsync();
        await orchestrator.DisposeAsync(); // Should not throw
    }

    #endregion

    #region Helper Methods

    private string CreateTestFile(string fileName, string content)
    {
        var filePath = Path.Combine(_testDirectory, fileName);
        File.WriteAllText(filePath, content);
        _tempFiles.Add(filePath);
        return filePath;
    }

    private static IAnalyzer CreateMockAnalyzer(string name, int priority)
    {
        var analyzer = Substitute.For<IAnalyzer>();
        analyzer.Name.Returns(name);
        analyzer.Priority.Returns(priority);
        analyzer.AnalyzeAsync(Arg.Any<AnalysisContext>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(new AnalysisResult(name)
            {
                Success = true,
                Fragment = ReportFragment.CreateSection($"{name} Results", $"Analysis by {name}")
            }));
        return analyzer;
    }

    private static IAnalyzer CreateTrackingAnalyzer(string name, int priority, List<string> callOrder)
    {
        var analyzer = Substitute.For<IAnalyzer>();
        analyzer.Name.Returns(name);
        analyzer.Priority.Returns(priority);
        analyzer.AnalyzeAsync(Arg.Any<AnalysisContext>(), Arg.Any<CancellationToken>())
            .Returns(ci =>
            {
                lock (callOrder)
                {
                    callOrder.Add(name);
                }
                return Task.FromResult(new AnalysisResult(name) { Success = true });
            });
        return analyzer;
    }

    private static IAnalyzer CreateTimingAnalyzer(string name, int priority, List<(string, DateTime)> startTimes)
    {
        var analyzer = Substitute.For<IAnalyzer>();
        analyzer.Name.Returns(name);
        analyzer.Priority.Returns(priority);
        analyzer.AnalyzeAsync(Arg.Any<AnalysisContext>(), Arg.Any<CancellationToken>())
            .Returns(async ci =>
            {
                lock (startTimes)
                {
                    startTimes.Add((name, DateTime.UtcNow));
                }
                await Task.Delay(10);
                return new AnalysisResult(name) { Success = true };
            });
        return analyzer;
    }

    private static string CreateTestReport()
    {
        return "Test Report\n==========\nTest content";
    }

    public void Dispose()
    {
        _orchestrator?.DisposeAsync().AsTask().Wait();
        
        if (Directory.Exists(_testDirectory))
        {
            try
            {
                Directory.Delete(_testDirectory, true);
            }
            catch
            {
                // Best effort cleanup
            }
        }
    }

    #endregion
}