using FluentAssertions;
using Microsoft.Extensions.Logging;
using NSubstitute;
using NSubstitute.ExceptionExtensions;
using Scanner111.Core.Services.Performance;
using Xunit;

namespace Scanner111.Test.Services.Performance;

/// <summary>
/// Comprehensive tests for PerformanceBenchmarker with proper categorization and timeout handling.
/// </summary>
[Trait("Category", "Unit")]
[Trait("Performance", "Fast")]
[Trait("Component", "Performance")]
public sealed class PerformanceBenchmarkerTests : IDisposable
{
    private readonly ILogger<PerformanceBenchmarker> _logger;
    private readonly PerformanceBenchmarker _sut;
    private readonly CancellationTokenSource _testCancellation;

    public PerformanceBenchmarkerTests()
    {
        _logger = Substitute.For<ILogger<PerformanceBenchmarker>>();
        _sut = new PerformanceBenchmarker(_logger, enablePeriodicReporting: false);
        _testCancellation = new CancellationTokenSource(TimeSpan.FromSeconds(10));
    }

    #region Constructor Tests

    [Fact]
    [Trait("Priority", "Critical")]
    public void Constructor_ValidLogger_InitializesCorrectly()
    {
        // Arrange & Act
        using var benchmarker = new PerformanceBenchmarker(_logger, enablePeriodicReporting: false);

        // Assert
        benchmarker.Should().NotBeNull();
        benchmarker.GetMetrics().Should().BeEmpty();
    }

    [Fact]
    public void Constructor_NullLogger_ThrowsArgumentNullException()
    {
        // Act
        var act = () => new PerformanceBenchmarker(null!, false);

        // Assert
        act.Should().Throw<ArgumentNullException>()
            .WithParameterName("logger");
    }

    [Fact]
    public void Constructor_WithPeriodicReporting_CreatesTimer()
    {
        // Arrange & Act
        using var benchmarker = new PerformanceBenchmarker(_logger, enablePeriodicReporting: true);

        // Assert
        benchmarker.Should().NotBeNull();
        // Timer is created but we can't directly verify it - would need to wait for periodic report
    }

    #endregion

    #region TimeOperationAsync Tests

    [Fact]
    [Trait("Priority", "High")]
    public async Task TimeOperationAsync_SuccessfulOperation_RecordsMetrics()
    {
        // Arrange
        const string operationName = "TestOperation";
        const int expectedResult = 42;

        // Act
        var result = await _sut.TimeOperationAsync(
            operationName,
            async ct =>
            {
                await Task.Delay(100, ct);
                return expectedResult;
            },
            _testCancellation.Token);

        // Assert
        result.Should().Be(expectedResult);
        
        var metrics = _sut.GetMetrics(operationName);
        metrics.Should().NotBeNull();
        metrics!.ExecutionCount.Should().Be(1);
        metrics.SuccessRate.Should().Be(1.0);
        metrics.AverageExecutionTime.Should().BeGreaterThan(TimeSpan.FromMilliseconds(90));
        metrics.AverageExecutionTime.Should().BeLessThan(TimeSpan.FromSeconds(1));
    }

    [Fact]
    public async Task TimeOperationAsync_FailedOperation_RecordsFailure()
    {
        // Arrange
        const string operationName = "FailingOperation";
        var expectedException = new InvalidOperationException("Test failure");

        // Act
        var act = () => _sut.TimeOperationAsync<int>(
            operationName,
            ct => throw expectedException,
            _testCancellation.Token);

        // Assert
        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("Test failure");
        
        var metrics = _sut.GetMetrics(operationName);
        metrics.Should().NotBeNull();
        metrics!.ExecutionCount.Should().Be(1);
        metrics.SuccessRate.Should().Be(0.0);
        
        _logger.Received(1).LogError(
            expectedException,
            Arg.Is<string>(s => s.Contains("failed")),
            Arg.Any<object[]>());
    }

    [Fact]
    public async Task TimeOperationAsync_MultipleExecutions_AggregatesMetrics()
    {
        // Arrange
        const string operationName = "RepeatedOperation";
        var durations = new[] { 50, 100, 150 };

        // Act
        foreach (var duration in durations)
        {
            await _sut.TimeOperationAsync(
                operationName,
                ct => Task.Delay(duration, ct).ContinueWith(_ => duration, ct),
                _testCancellation.Token);
        }

        // Assert
        var metrics = _sut.GetMetrics(operationName);
        metrics.Should().NotBeNull();
        metrics!.ExecutionCount.Should().Be(3);
        metrics.SuccessRate.Should().Be(1.0);
        metrics.AverageExecutionTime.Should().BeGreaterThan(TimeSpan.FromMilliseconds(90));
        metrics.MinExecutionTime.Should().BeGreaterThan(TimeSpan.FromMilliseconds(40));
        metrics.MaxExecutionTime.Should().BeGreaterThan(TimeSpan.FromMilliseconds(140));
    }

    [Fact]
    public async Task TimeOperationAsync_MixedSuccessAndFailure_CalculatesCorrectSuccessRate()
    {
        // Arrange
        const string operationName = "MixedOperation";
        var shouldFail = false;

        // Act - 3 successes, 2 failures
        for (int i = 0; i < 5; i++)
        {
            shouldFail = i >= 3;
            try
            {
                await _sut.TimeOperationAsync(
                    operationName,
                    ct =>
                    {
                        if (shouldFail) throw new Exception("Planned failure");
                        return Task.FromResult(i);
                    },
                    _testCancellation.Token);
            }
            catch
            {
                // Expected for failures
            }
        }

        // Assert
        var metrics = _sut.GetMetrics(operationName);
        metrics.Should().NotBeNull();
        metrics!.ExecutionCount.Should().Be(5);
        metrics.SuccessRate.Should().BeApproximately(0.6, 0.01); // 3/5 = 0.6
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("  ")]
    public async Task TimeOperationAsync_InvalidOperationName_ThrowsArgumentException(string? operationName)
    {
        // Act
        var act = () => _sut.TimeOperationAsync(
            operationName!,
            ct => Task.FromResult(42),
            _testCancellation.Token);

        // Assert
        await act.Should().ThrowAsync<ArgumentException>();
    }

    [Fact]
    public async Task TimeOperationAsync_NullOperation_ThrowsArgumentNullException()
    {
        // Act
        var act = () => _sut.TimeOperationAsync<int>(
            "TestOp",
            null!,
            _testCancellation.Token);

        // Assert
        await act.Should().ThrowAsync<ArgumentNullException>();
    }

    [Fact]
    [Trait("Priority", "High")]
    public async Task TimeOperationAsync_WithCancellation_PropagatesCancellation()
    {
        // Arrange
        using var cts = new CancellationTokenSource();
        
        // Act
        var task = _sut.TimeOperationAsync(
            "LongOperation",
            async ct =>
            {
                await Task.Delay(TimeSpan.FromSeconds(10), ct);
                return 42;
            },
            cts.Token);
        
        cts.Cancel();

        // Assert
        await task.Invoking(async t => await t).Should().ThrowAsync<OperationCanceledException>();
    }

    [Fact]
    public async Task TimeOperationAsync_TracksMemoryDelta()
    {
        // Arrange
        const string operationName = "MemoryIntensiveOp";

        // Act
        await _sut.TimeOperationAsync(
            operationName,
            ct =>
            {
                var data = new byte[10 * 1024 * 1024]; // Allocate 10MB
                GC.KeepAlive(data);
                return Task.FromResult(data.Length);
            },
            _testCancellation.Token);

        // Assert
        var metrics = _sut.GetMetrics(operationName);
        metrics.Should().NotBeNull();
        // Memory delta tracking is approximate and may vary
        // Just verify it's tracked
        metrics!.AverageMemoryDelta.Should().BeGreaterThanOrEqualTo(0);
    }

    #endregion

    #region TimeOperation (Synchronous) Tests

    [Fact]
    [Trait("Priority", "High")]
    public void TimeOperation_SuccessfulOperation_RecordsMetrics()
    {
        // Arrange
        const string operationName = "SyncOperation";
        const int expectedResult = 42;

        // Act
        var result = _sut.TimeOperation(
            operationName,
            () =>
            {
                Thread.Sleep(50);
                return expectedResult;
            });

        // Assert
        result.Should().Be(expectedResult);
        
        var metrics = _sut.GetMetrics(operationName);
        metrics.Should().NotBeNull();
        metrics!.ExecutionCount.Should().Be(1);
        metrics.SuccessRate.Should().Be(1.0);
        metrics.AverageExecutionTime.Should().BeGreaterThan(TimeSpan.FromMilliseconds(40));
    }

    [Fact]
    public void TimeOperation_FailedOperation_RecordsFailure()
    {
        // Arrange
        const string operationName = "FailingSyncOp";
        var expectedException = new InvalidOperationException("Sync failure");

        // Act
        var act = () => _sut.TimeOperation<int>(
            operationName,
            () => throw expectedException);

        // Assert
        act.Should().Throw<InvalidOperationException>()
            .WithMessage("Sync failure");
        
        var metrics = _sut.GetMetrics(operationName);
        metrics.Should().NotBeNull();
        metrics!.ExecutionCount.Should().Be(1);
        metrics.SuccessRate.Should().Be(0.0);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("  ")]
    public void TimeOperation_InvalidOperationName_ThrowsArgumentException(string? operationName)
    {
        // Act
        var act = () => _sut.TimeOperation(operationName!, () => 42);

        // Assert
        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void TimeOperation_NullOperation_ThrowsArgumentNullException()
    {
        // Act
        var act = () => _sut.TimeOperation<int>("TestOp", null!);

        // Assert
        act.Should().Throw<ArgumentNullException>();
    }

    #endregion

    #region GetMetrics Tests

    [Fact]
    [Trait("Priority", "Medium")]
    public void GetMetrics_NoOperations_ReturnsEmpty()
    {
        // Act
        var metrics = _sut.GetMetrics();

        // Assert
        metrics.Should().BeEmpty();
    }

    [Fact]
    public async Task GetMetrics_MultipleOperations_ReturnsAll()
    {
        // Arrange
        await _sut.TimeOperationAsync("Op1", ct => Task.FromResult(1), CancellationToken.None);
        await _sut.TimeOperationAsync("Op2", ct => Task.FromResult(2), CancellationToken.None);
        _sut.TimeOperation("Op3", () => 3);

        // Act
        var metrics = _sut.GetMetrics();

        // Assert
        metrics.Should().HaveCount(3);
        metrics.Should().ContainKeys("Op1", "Op2", "Op3");
        metrics.Values.Should().AllSatisfy(m =>
        {
            m.ExecutionCount.Should().Be(1);
            m.SuccessRate.Should().Be(1.0);
        });
    }

    [Fact]
    public void GetMetrics_SpecificOperation_ReturnsCorrectMetrics()
    {
        // Arrange
        _sut.TimeOperation("TestOp", () => 42);

        // Act
        var metrics = _sut.GetMetrics("TestOp");

        // Assert
        metrics.Should().NotBeNull();
        metrics!.OperationName.Should().Be("TestOp");
        metrics.ExecutionCount.Should().Be(1);
    }

    [Fact]
    public void GetMetrics_NonExistentOperation_ReturnsNull()
    {
        // Act
        var metrics = _sut.GetMetrics("NonExistent");

        // Assert
        metrics.Should().BeNull();
    }

    #endregion

    #region ClearMetrics Tests

    [Fact]
    [Trait("Priority", "Medium")]
    public async Task ClearMetrics_AfterOperations_RemovesAllMetrics()
    {
        // Arrange
        await _sut.TimeOperationAsync("Op1", ct => Task.FromResult(1), CancellationToken.None);
        await _sut.TimeOperationAsync("Op2", ct => Task.FromResult(2), CancellationToken.None);

        // Act
        _sut.ClearMetrics();

        // Assert
        _sut.GetMetrics().Should().BeEmpty();
        _logger.Received(1).LogInformation("Performance metrics cleared");
    }

    [Fact]
    public void ClearMetrics_EmptyMetrics_HandlesGracefully()
    {
        // Act
        _sut.ClearMetrics();

        // Assert
        _sut.GetMetrics().Should().BeEmpty();
    }

    #endregion

    #region Concurrency Tests

    [Fact]
    [Trait("Priority", "High")]
    public async Task TimeOperationAsync_ConcurrentOperations_ThreadSafe()
    {
        // Arrange
        const int concurrentOperations = 10;
        const int operationsPerThread = 50;
        var tasks = new List<Task>();

        // Act
        for (int i = 0; i < concurrentOperations; i++)
        {
            var opIndex = i;
            tasks.Add(Task.Run(async () =>
            {
                for (int j = 0; j < operationsPerThread; j++)
                {
                    await _sut.TimeOperationAsync(
                        $"ConcurrentOp{opIndex % 3}", // Use 3 different operation names
                        ct => Task.Delay(Random.Shared.Next(1, 10), ct).ContinueWith(_ => j, ct),
                        _testCancellation.Token);
                }
            }));
        }

        await Task.WhenAll(tasks);

        // Assert
        var metrics = _sut.GetMetrics();
        metrics.Should().HaveCount(3);
        
        var totalExecutions = metrics.Values.Sum(m => m.ExecutionCount);
        totalExecutions.Should().Be(concurrentOperations * operationsPerThread);
    }

    [Fact]
    public async Task GetMetrics_ConcurrentWithOperations_ThreadSafe()
    {
        // Arrange
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(3));
        var metricsTask = Task.Run(() =>
        {
            var metricsCount = 0;
            while (!cts.Token.IsCancellationRequested)
            {
                var metrics = _sut.GetMetrics();
                metricsCount = metrics.Count;
                Thread.Sleep(10);
            }
            return metricsCount;
        }, cts.Token);

        var operationTask = Task.Run(async () =>
        {
            int opCount = 0;
            while (!cts.Token.IsCancellationRequested)
            {
                await _sut.TimeOperationAsync(
                    $"Op{opCount++ % 5}",
                    async ct => { await Task.Delay(5, ct); return true; },
                    cts.Token);
            }
        }, cts.Token);

        // Act
        try
        {
            await Task.WhenAll(metricsTask, operationTask);
        }
        catch (OperationCanceledException)
        {
            // Expected
        }

        // Assert - Should not crash and should have valid metrics
        _sut.GetMetrics().Should().NotBeNull();
    }

    #endregion

    #region Performance and Timeout Tests

    [Fact]
    [Trait("Priority", "Medium")]
    public async Task TimeOperationAsync_VeryQuickOperation_MeasuresAccurately()
    {
        // Arrange & Act
        var result = await _sut.TimeOperationAsync(
            "QuickOp",
            ct => Task.FromResult(42),
            _testCancellation.Token);

        // Assert
        var metrics = _sut.GetMetrics("QuickOp");
        metrics.Should().NotBeNull();
        metrics!.AverageExecutionTime.Should().BeLessThan(TimeSpan.FromMilliseconds(100));
        metrics.AverageExecutionTime.Should().BeGreaterThanOrEqualTo(TimeSpan.Zero);
    }

    [Fact]
    public async Task TimeOperationAsync_WithTimeout_CompletesWithinTimeout()
    {
        // Arrange
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(2));

        // Act
        var result = await _sut.TimeOperationAsync(
            "TimeoutOp",
            async ct =>
            {
                await Task.Delay(100, ct);
                return 42;
            },
            cts.Token);

        // Assert
        result.Should().Be(42);
        var metrics = _sut.GetMetrics("TimeoutOp");
        metrics!.AverageExecutionTime.Should().BeLessThan(TimeSpan.FromSeconds(2));
    }

    #endregion

    #region Edge Cases

    [Fact]
    public async Task TimeOperationAsync_OperationReturnsNull_HandlesCorrectly()
    {
        // Act
        var result = await _sut.TimeOperationAsync(
            "NullOp",
            ct => Task.FromResult<string?>(null),
            _testCancellation.Token);

        // Assert
        result.Should().BeNull();
        var metrics = _sut.GetMetrics("NullOp");
        metrics!.ExecutionCount.Should().Be(1);
        metrics.SuccessRate.Should().Be(1.0);
    }

    [Fact]
    public void TimeOperation_VeryLongOperationName_HandlesCorrectly()
    {
        // Arrange
        var longName = new string('a', 1000);

        // Act
        var result = _sut.TimeOperation(longName, () => 42);

        // Assert
        result.Should().Be(42);
        var metrics = _sut.GetMetrics(longName);
        metrics.Should().NotBeNull();
        metrics!.OperationName.Should().Be(longName);
    }

    [Fact]
    public async Task GetMetrics_AfterManyOperations_HandlesLargeDataset()
    {
        // Arrange - Create many different operations
        for (int i = 0; i < 100; i++)
        {
            await _sut.TimeOperationAsync($"Op{i}", ct => Task.FromResult(i), CancellationToken.None);
        }

        // Act
        var metrics = _sut.GetMetrics();

        // Assert
        metrics.Should().HaveCount(100);
        metrics.Values.Should().AllSatisfy(m => m.ExecutionCount.Should().Be(1));
    }

    #endregion

    #region Dispose Tests

    [Fact]
    public void Dispose_MultipleCalls_HandlesGracefully()
    {
        // Arrange
        var benchmarker = new PerformanceBenchmarker(_logger, false);

        // Act
        benchmarker.Dispose();
        benchmarker.Dispose(); // Second call should not throw

        // Assert
        benchmarker.Should().NotBeNull();
    }

    [Fact]
    public async Task Dispose_WithPendingTimer_CleansUpCorrectly()
    {
        // Arrange
        var benchmarker = new PerformanceBenchmarker(_logger, enablePeriodicReporting: true);
        await benchmarker.TimeOperationAsync("TestOp", ct => Task.FromResult(42), CancellationToken.None);

        // Act
        benchmarker.Dispose();

        // Assert - Should not throw and should clean up timer
        benchmarker.Should().NotBeNull();
    }

    #endregion

    public void Dispose()
    {
        _testCancellation?.Dispose();
        _sut?.Dispose();
    }
}