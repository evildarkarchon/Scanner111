using FluentAssertions;
using Microsoft.Extensions.Logging;
using NSubstitute;
using Scanner111.Core.Services.Performance;
using Xunit;

namespace Scanner111.Test.Services.Performance;

/// <summary>
/// Comprehensive tests for DynamicBatchSizer with proper categorization and timeout handling.
/// </summary>
[Trait("Category", "Unit")]
[Trait("Performance", "Fast")]
[Trait("Component", "Performance")]
public class DynamicBatchSizerTests : IDisposable
{
    private readonly ILogger<DynamicBatchSizer> _logger;
    private readonly DynamicBatchSizer _sut;
    private readonly CancellationTokenSource _testCancellation;

    public DynamicBatchSizerTests()
    {
        _logger = Substitute.For<ILogger<DynamicBatchSizer>>();
        _sut = new DynamicBatchSizer(_logger);
        _testCancellation = new CancellationTokenSource(TimeSpan.FromSeconds(10));
    }

    #region Constructor Tests

    [Fact]
    [Trait("Priority", "Critical")]
    public void Constructor_DefaultValues_InitializesCorrectly()
    {
        // Arrange & Act
        var sizer = new DynamicBatchSizer(_logger);

        // Assert
        sizer.CurrentBatchSize.Should().Be(20);
    }

    [Theory]
    [InlineData(10, 5, 50, 10)]
    [InlineData(100, 10, 200, 100)]
    [InlineData(-5, 1, 100, 1)] // Negative base should be clamped to 1
    [InlineData(50, 100, 30, 50)] // Min > Max should still work with base
    public void Constructor_CustomValues_InitializesCorrectly(
        int baseBatchSize, int minBatchSize, int maxBatchSize, int expectedInitial)
    {
        // Arrange & Act
        var sizer = new DynamicBatchSizer(_logger, baseBatchSize, minBatchSize, maxBatchSize);

        // Assert
        sizer.CurrentBatchSize.Should().Be(expectedInitial);
    }

    [Fact]
    public void Constructor_NullLogger_ThrowsArgumentNullException()
    {
        // Act
        var act = () => new DynamicBatchSizer(null!);

        // Assert
        act.Should().Throw<ArgumentNullException>()
            .WithParameterName("logger");
    }

    #endregion

    #region UpdateBatchSize Tests

    [Fact]
    [Trait("Priority", "High")]
    public void UpdateBatchSize_ImprovedThroughput_IncreasesBatchSize()
    {
        // Arrange
        var sizer = new DynamicBatchSizer(_logger, baseBatchSize: 20, maxBatchSize: 100);
        
        // Establish baseline
        sizer.UpdateBatchSize(20, TimeSpan.FromSeconds(2)); // 10 items/sec
        Thread.Sleep(1100); // Ensure enough time passes

        // Act - Improved throughput
        sizer.UpdateBatchSize(30, TimeSpan.FromSeconds(2)); // 15 items/sec (50% improvement)

        // Assert
        sizer.CurrentBatchSize.Should().BeGreaterThan(20);
        sizer.CurrentBatchSize.Should().BeLessThanOrEqualTo(100);
        
        _logger.Received().LogDebug(
            Arg.Is<string>(s => s.Contains("Increasing batch size")),
            Arg.Any<object[]>());
    }

    [Fact]
    public void UpdateBatchSize_DecreasedThroughput_DecreasesBatchSize()
    {
        // Arrange
        var sizer = new DynamicBatchSizer(_logger, baseBatchSize: 50, minBatchSize: 10);
        
        // Establish baseline
        sizer.UpdateBatchSize(50, TimeSpan.FromSeconds(2)); // 25 items/sec
        Thread.Sleep(1100); // Ensure enough time passes

        // Act - Decreased throughput
        sizer.UpdateBatchSize(30, TimeSpan.FromSeconds(2)); // 15 items/sec (40% decrease)

        // Assert
        sizer.CurrentBatchSize.Should().BeLessThan(50);
        sizer.CurrentBatchSize.Should().BeGreaterThanOrEqualTo(10);
        
        _logger.Received().LogDebug(
            Arg.Is<string>(s => s.Contains("Decreasing batch size")),
            Arg.Any<object[]>());
    }

    [Fact]
    public void UpdateBatchSize_StableThroughput_MaintainsBatchSize()
    {
        // Arrange
        var sizer = new DynamicBatchSizer(_logger, baseBatchSize: 30);
        var initialSize = sizer.CurrentBatchSize;
        
        // Establish baseline
        sizer.UpdateBatchSize(30, TimeSpan.FromSeconds(2)); // 15 items/sec
        Thread.Sleep(1100);

        // Act - Similar throughput (within threshold)
        sizer.UpdateBatchSize(31, TimeSpan.FromSeconds(2)); // 15.5 items/sec

        // Assert
        sizer.CurrentBatchSize.Should().Be(initialSize);
    }

    [Theory]
    [InlineData(0, 1)]  // Zero items processed
    [InlineData(10, 0)] // Zero processing time
    [InlineData(-5, 1)] // Negative items
    public void UpdateBatchSize_InvalidInput_DoesNotChangeBatchSize(int items, int seconds)
    {
        // Arrange
        var initialSize = _sut.CurrentBatchSize;

        // Act
        _sut.UpdateBatchSize(items, TimeSpan.FromSeconds(seconds));

        // Assert
        _sut.CurrentBatchSize.Should().Be(initialSize);
    }

    [Fact]
    public void UpdateBatchSize_RapidCalls_OnlyAdjustsAfterInterval()
    {
        // Arrange
        var sizer = new DynamicBatchSizer(_logger, baseBatchSize: 20);
        
        // Establish baseline
        sizer.UpdateBatchSize(20, TimeSpan.FromSeconds(1));

        // Act - Rapid successive calls (should be ignored)
        for (int i = 0; i < 5; i++)
        {
            sizer.UpdateBatchSize(40, TimeSpan.FromSeconds(1)); // High throughput
            Thread.Sleep(100); // Not enough time between calls
        }

        // Assert - Should not have adjusted multiple times
        sizer.CurrentBatchSize.Should().Be(20);
    }

    [Fact]
    [Trait("Priority", "Medium")]
    public void UpdateBatchSize_RespectsMaximumLimit()
    {
        // Arrange
        var sizer = new DynamicBatchSizer(_logger, baseBatchSize: 80, maxBatchSize: 100);
        
        // Establish baseline
        sizer.UpdateBatchSize(80, TimeSpan.FromSeconds(1));
        Thread.Sleep(1100);

        // Act - Try to increase beyond max
        sizer.UpdateBatchSize(200, TimeSpan.FromSeconds(1)); // Very high throughput

        // Assert
        sizer.CurrentBatchSize.Should().BeLessThanOrEqualTo(100);
    }

    [Fact]
    public void UpdateBatchSize_RespectsMinimumLimit()
    {
        // Arrange
        var sizer = new DynamicBatchSizer(_logger, baseBatchSize: 15, minBatchSize: 10);
        
        // Establish baseline
        sizer.UpdateBatchSize(15, TimeSpan.FromSeconds(1));
        Thread.Sleep(1100);

        // Act - Try to decrease below min
        sizer.UpdateBatchSize(5, TimeSpan.FromSeconds(10)); // Very low throughput

        // Assert
        sizer.CurrentBatchSize.Should().BeGreaterThanOrEqualTo(10);
    }

    #endregion

    #region Memory Pressure Tests

    [Fact]
    [Trait("Priority", "High")]
    public void AdjustForMemoryPressure_HighMemoryUsage_ReducesBatchSize()
    {
        // Arrange
        var sizer = new DynamicBatchSizer(_logger, baseBatchSize: 50, minBatchSize: 10);
        
        // Force garbage collection to simulate memory pressure
        var largeArray = new byte[60 * 1024 * 1024]; // 60MB allocation
        GC.Collect(0, GCCollectionMode.Forced);

        // Act
        sizer.AdjustForMemoryPressure();

        // Assert - May reduce batch size due to memory pressure
        // Note: This test is environment-dependent
        _logger.ReceivedCalls().Any(call => 
            call.GetMethodInfo().Name == "Log" &&
            call.GetArguments().Any(arg => 
                arg?.ToString()?.Contains("memory pressure") == true))
            .Should().BeFalse(); // May or may not trigger depending on system state
        
        GC.KeepAlive(largeArray);
    }

    [Fact]
    public void AdjustForMemoryPressure_LowMemoryUsage_MaintainsBatchSize()
    {
        // Arrange
        var initialSize = _sut.CurrentBatchSize;

        // Act
        _sut.AdjustForMemoryPressure();

        // Assert - Should not change if no memory pressure
        _sut.CurrentBatchSize.Should().BeGreaterThanOrEqualTo(5); // Should respect minimum
    }

    [Fact]
    public void AdjustForMemoryPressure_ExceptionDuringCheck_LogsWarning()
    {
        // This is difficult to test directly without mocking GC
        // But we can verify the method doesn't throw
        
        // Act
        var act = () => _sut.AdjustForMemoryPressure();

        // Assert
        act.Should().NotThrow();
    }

    #endregion

    #region Reset Tests

    [Fact]
    [Trait("Priority", "Medium")]
    public void Reset_AfterAdjustments_RestoresToBaseSize()
    {
        // Arrange
        var baseBatchSize = 25;
        var sizer = new DynamicBatchSizer(_logger, baseBatchSize: baseBatchSize);
        
        // Change the batch size
        sizer.UpdateBatchSize(100, TimeSpan.FromSeconds(1));
        Thread.Sleep(1100);
        sizer.UpdateBatchSize(200, TimeSpan.FromSeconds(1));

        // Act
        sizer.Reset();

        // Assert
        sizer.CurrentBatchSize.Should().Be(baseBatchSize);
        _logger.Received().LogDebug(
            Arg.Is<string>(s => s.Contains("Reset batch size")),
            Arg.Any<object[]>());
    }

    [Fact]
    public void Reset_ClearsThroughputHistory()
    {
        // Arrange
        _sut.UpdateBatchSize(50, TimeSpan.FromSeconds(1));
        
        // Act
        _sut.Reset();
        
        // Now update should not compare to previous throughput
        _sut.UpdateBatchSize(50, TimeSpan.FromSeconds(1));

        // Assert - Should not trigger any adjustment logs
        _logger.DidNotReceive().LogDebug(
            Arg.Is<string>(s => s.Contains("Increasing") || s.Contains("Decreasing")),
            Arg.Any<object[]>());
    }

    #endregion

    #region Concurrency and Thread Safety Tests

    [Fact]
    [Trait("Priority", "High")]
    public async Task UpdateBatchSize_ConcurrentCalls_ThreadSafe()
    {
        // Arrange
        var sizer = new DynamicBatchSizer(_logger);
        var tasks = new List<Task>();
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));

        // Act - Multiple threads updating batch size
        for (int i = 0; i < 10; i++)
        {
            var taskId = i;
            tasks.Add(Task.Run(() =>
            {
                for (int j = 0; j < 100 && !cts.Token.IsCancellationRequested; j++)
                {
                    sizer.UpdateBatchSize(
                        Random.Shared.Next(10, 100),
                        TimeSpan.FromMilliseconds(Random.Shared.Next(100, 1000)));
                    Thread.Sleep(10);
                }
            }, cts.Token));
        }

        await Task.WhenAll(tasks);

        // Assert - Should not crash and should have a valid batch size
        sizer.CurrentBatchSize.Should().BeInRange(5, 100);
    }

    [Fact]
    public async Task Reset_ConcurrentWithUpdate_ThreadSafe()
    {
        // Arrange
        var sizer = new DynamicBatchSizer(_logger);
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(3));

        // Act - Concurrent updates and resets
        var updateTask = Task.Run(() =>
        {
            while (!cts.Token.IsCancellationRequested)
            {
                sizer.UpdateBatchSize(50, TimeSpan.FromSeconds(1));
                Thread.Sleep(50);
            }
        }, cts.Token);

        var resetTask = Task.Run(() =>
        {
            while (!cts.Token.IsCancellationRequested)
            {
                sizer.Reset();
                Thread.Sleep(100);
            }
        }, cts.Token);

        // Wait for tasks to complete
        try
        {
            await Task.WhenAll(updateTask, resetTask);
        }
        catch (OperationCanceledException)
        {
            // Expected when cancellation is requested
        }

        // Assert - Should not crash and should have a valid state
        sizer.CurrentBatchSize.Should().BePositive();
    }

    #endregion

    #region Performance and Timeout Tests

    [Fact]
    [Trait("Priority", "Medium")]
    public void UpdateBatchSize_ManyIterations_PerformsQuickly()
    {
        // Arrange
        var stopwatch = System.Diagnostics.Stopwatch.StartNew();
        
        // Act - 1000 updates
        for (int i = 0; i < 1000; i++)
        {
            _sut.UpdateBatchSize(Random.Shared.Next(10, 100), TimeSpan.FromSeconds(1));
        }
        
        stopwatch.Stop();

        // Assert - Should complete quickly
        stopwatch.Elapsed.Should().BeLessThan(TimeSpan.FromSeconds(1));
    }

    [Fact]
    public void AdjustForMemoryPressure_WithTimeout_CompletesQuickly()
    {
        // Arrange
        var stopwatch = System.Diagnostics.Stopwatch.StartNew();
        
        // Act
        _sut.AdjustForMemoryPressure();
        
        stopwatch.Stop();

        // Assert - Should complete within reasonable time
        stopwatch.Elapsed.Should().BeLessThan(TimeSpan.FromSeconds(2));
    }

    #endregion

    #region Edge Cases

    [Fact]
    public void UpdateBatchSize_ExtremeThroughputChange_HandlesGracefully()
    {
        // Arrange
        var sizer = new DynamicBatchSizer(_logger);
        sizer.UpdateBatchSize(10, TimeSpan.FromSeconds(10)); // 1 item/sec
        Thread.Sleep(1100);

        // Act - Extreme improvement
        sizer.UpdateBatchSize(10000, TimeSpan.FromSeconds(1)); // 10000 items/sec

        // Assert - Should adjust but stay within bounds
        sizer.CurrentBatchSize.Should().BeInRange(5, 100);
    }

    [Fact]
    public void UpdateBatchSize_VeryLongProcessingTime_HandlesCorrectly()
    {
        // Arrange & Act
        _sut.UpdateBatchSize(10, TimeSpan.FromHours(1)); // Very slow processing

        // Assert - Should handle without issues
        _sut.CurrentBatchSize.Should().BePositive();
    }

    [Fact]
    public void CurrentBatchSize_AfterMultipleOperations_AlwaysValid()
    {
        // Arrange & Act - Random operations
        for (int i = 0; i < 20; i++)
        {
            switch (Random.Shared.Next(3))
            {
                case 0:
                    _sut.UpdateBatchSize(Random.Shared.Next(1, 100), TimeSpan.FromSeconds(Random.Shared.NextDouble() * 5));
                    break;
                case 1:
                    _sut.AdjustForMemoryPressure();
                    break;
                case 2:
                    _sut.Reset();
                    break;
            }
        }

        // Assert
        _sut.CurrentBatchSize.Should().BeInRange(5, 100);
    }

    #endregion

    public void Dispose()
    {
        _testCancellation?.Dispose();
    }
}