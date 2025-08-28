using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using NSubstitute;
using Scanner111.Core.Processing;
using Xunit;

namespace Scanner111.Test.Processing;

/// <summary>
/// Comprehensive tests for ChannelBasedBatchProcessor covering parallel processing,
/// streaming, backpressure, error handling, cancellation, and statistics tracking.
/// </summary>
public sealed class ChannelBasedBatchProcessorTests : IAsyncDisposable
{
    private readonly ILogger<ChannelBasedBatchProcessor<int, string>> _logger;
    private readonly List<ChannelBasedBatchProcessor<int, string>> _processors;

    public ChannelBasedBatchProcessorTests()
    {
        _logger = Substitute.For<ILogger<ChannelBasedBatchProcessor<int, string>>>();
        _processors = new List<ChannelBasedBatchProcessor<int, string>>();
    }

    #region Constructor Tests

    [Fact]
    public void Constructor_WithNullLogger_ThrowsArgumentNullException()
    {
        // Arrange & Act
        var act = () => new ChannelBasedBatchProcessor<int, string>(
            null!, 
            (i, ct) => Task.FromResult(i.ToString()));

        // Assert
        act.Should().Throw<ArgumentNullException>()
            .WithParameterName("logger");
    }

    [Fact]
    public void Constructor_WithNullProcessor_ThrowsArgumentNullException()
    {
        // Arrange & Act
        var act = () => new ChannelBasedBatchProcessor<int, string>(
            _logger, 
            null!);

        // Assert
        act.Should().Throw<ArgumentNullException>()
            .WithParameterName("processor");
    }

    [Fact]
    public void Constructor_WithValidParameters_CreatesInstance()
    {
        // Arrange & Act
        var processor = CreateProcessor((i, ct) => Task.FromResult(i.ToString()));

        // Assert
        processor.Should().NotBeNull();
    }

    [Fact]
    public void Constructor_WithCustomOptions_UsesProvidedOptions()
    {
        // Arrange
        var options = new ProcessorOptions
        {
            WorkerCount = 2,
            ChannelCapacity = 50
        };

        // Act
        var processor = CreateProcessor(
            (i, ct) => Task.FromResult(i.ToString()), 
            options);

        // Assert
        processor.Should().NotBeNull();
        // Verify workers started with logging
        _logger.Received(1).Log(
            LogLevel.Debug,
            Arg.Any<EventId>(),
            Arg.Is<object>(o => o.ToString()!.Contains("Starting 2 worker threads")),
            null,
            Arg.Any<Func<object, Exception?, string>>());
    }

    #endregion

    #region Basic Batch Processing Tests

    [Fact]
    public async Task ProcessBatchAsync_WithEmptyCollection_ReturnsEmptyResult()
    {
        // Arrange
        var processor = CreateProcessor((i, ct) => Task.FromResult(i.ToString()));
        var items = Enumerable.Empty<int>();

        // Act
        var result = await processor.ProcessBatchAsync(items);

        // Assert
        result.Success.Should().BeTrue();
        result.Results.Should().BeEmpty();
        result.ItemsProcessed.Should().Be(0);
        result.Errors.Should().BeEmpty();
        result.ProcessingTime.Should().BeGreaterThan(TimeSpan.Zero);
    }

    [Fact]
    public async Task ProcessBatchAsync_WithSingleItem_ProcessesSuccessfully()
    {
        // Arrange
        var processor = CreateProcessor((i, ct) => Task.FromResult($"Processed: {i}"));
        var items = new[] { 42 };

        // Act
        var result = await processor.ProcessBatchAsync(items);

        // Assert
        result.Success.Should().BeTrue();
        result.Results.Should().ContainSingle()
            .Which.Should().Be("Processed: 42");
        result.ItemsProcessed.Should().Be(1);
        result.Errors.Should().BeEmpty();
    }

    [Fact]
    public async Task ProcessBatchAsync_WithMultipleItems_ProcessesAllItems()
    {
        // Arrange
        var processor = CreateProcessor((i, ct) => Task.FromResult($"Item-{i}"));
        var items = Enumerable.Range(1, 10);

        // Act
        var result = await processor.ProcessBatchAsync(items);

        // Assert
        result.Success.Should().BeTrue();
        result.Results.Should().HaveCount(10);
        result.Results.Should().Contain(Enumerable.Range(1, 10).Select(i => $"Item-{i}"));
        result.ItemsProcessed.Should().Be(10);
    }

    [Fact]
    public async Task ProcessBatchAsync_WithLargeDataset_ProcessesInParallel()
    {
        // Arrange
        var processedItems = new System.Collections.Concurrent.ConcurrentBag<int>();
        var processor = CreateProcessor(async (i, ct) =>
        {
            processedItems.Add(Thread.CurrentThread.ManagedThreadId);
            await Task.Delay(10, ct); // Simulate work
            return i.ToString();
        }, new ProcessorOptions { WorkerCount = 4, ChannelCapacity = 100 });
        
        var items = Enumerable.Range(1, 50);

        // Act
        var result = await processor.ProcessBatchAsync(items);

        // Assert
        result.Success.Should().BeTrue();
        result.Results.Should().HaveCount(50);
        
        // Verify multiple threads were used
        var uniqueThreads = processedItems.Distinct().Count();
        uniqueThreads.Should().BeGreaterThan(1, "Multiple threads should process items in parallel");
    }

    #endregion

    #region Streaming Tests

    [Fact]
    public async Task ProcessStreamAsync_WithAsyncEnumerable_ProcessesStreamingly()
    {
        // Arrange
        var processor = CreateProcessor((i, ct) => Task.FromResult($"Stream-{i}"));
        var results = new List<string>();

        // Act
        await foreach (var result in processor.ProcessStreamAsync(GenerateAsyncItems(5)))
        {
            results.Add(result);
        }

        // Assert
        results.Should().HaveCount(5);
        results.Should().Contain(Enumerable.Range(1, 5).Select(i => $"Stream-{i}"));
    }

    [Fact]
    public async Task ProcessStreamAsync_WithCancellation_StopsProcessing()
    {
        // Arrange
        var processor = CreateProcessor(async (i, ct) =>
        {
            await Task.Delay(100, ct);
            return i.ToString();
        });
        
        var cts = new CancellationTokenSource();
        var results = new List<string>();

        // Act
        cts.CancelAfter(150);
        try
        {
            await foreach (var result in processor.ProcessStreamAsync(GenerateAsyncItems(100), cts.Token))
            {
                results.Add(result);
            }
        }
        catch (OperationCanceledException)
        {
            // Expected
        }

        // Assert
        results.Count.Should().BeLessThan(100);
    }

    [Fact]
    public async Task ProcessStreamAsync_WithSlowProducer_ProcessesContinuously()
    {
        // Arrange
        var processor = CreateProcessor((i, ct) => Task.FromResult($"Slow-{i}"));
        var results = new List<string>();

        async IAsyncEnumerable<int> SlowProducer([EnumeratorCancellation] CancellationToken ct = default)
        {
            for (int i = 1; i <= 3; i++)
            {
                await Task.Delay(50, ct);
                yield return i;
            }
        }

        // Act
        await foreach (var result in processor.ProcessStreamAsync(SlowProducer()))
        {
            results.Add(result);
        }

        // Assert
        results.Should().HaveCount(3);
        results.Should().BeEquivalentTo(new[] { "Slow-1", "Slow-2", "Slow-3" });
    }

    #endregion

    #region Error Handling Tests

    [Fact]
    public async Task ProcessBatchAsync_WithProcessorException_ContinuesProcessing()
    {
        // Arrange
        var processor = CreateProcessor((i, ct) =>
        {
            if (i % 3 == 0)
                throw new InvalidOperationException($"Failed on {i}");
            return Task.FromResult(i.ToString());
        });
        
        var items = Enumerable.Range(1, 10);

        // Act
        var result = await processor.ProcessBatchAsync(items);

        // Assert
        result.Success.Should().BeTrue();
        result.Results.Count.Should().BeLessThan(10); // Some items failed
        result.Results.Should().NotContain("3");
        result.Results.Should().NotContain("6");
        result.Results.Should().NotContain("9");
        
        // Verify error logging
        _logger.Received().Log(
            LogLevel.Error,
            Arg.Any<EventId>(),
            Arg.Is<object>(o => o.ToString()!.Contains("failed to process item")),
            Arg.Any<Exception>(),
            Arg.Any<Func<object, Exception?, string>>());
    }

    [Fact]
    public async Task ProcessBatchAsync_WithAllItemsFailing_ReturnsPartialResult()
    {
        // Arrange
        var processor = CreateProcessor<int, string>((i, ct) =>
        {
            throw new InvalidOperationException("Always fails");
        });
        
        var items = Enumerable.Range(1, 5);

        // Act
        var result = await processor.ProcessBatchAsync(items);

        // Assert
        result.Success.Should().BeTrue(); // Batch itself succeeded
        result.Results.Should().BeEmpty(); // But no results produced
        result.ItemsProcessed.Should().Be(0);
    }

    #endregion

    #region Cancellation Tests

    [Fact]
    public async Task ProcessBatchAsync_WithCancellationBeforeStart_ReturnsCancelled()
    {
        // Arrange
        var processor = CreateProcessor((i, ct) => Task.FromResult(i.ToString()));
        var items = Enumerable.Range(1, 10);
        var cts = new CancellationTokenSource();
        cts.Cancel();

        // Act
        var result = await processor.ProcessBatchAsync(items, cts.Token);

        // Assert
        result.Success.Should().BeFalse();
        result.ItemsProcessed.Should().Be(0);
    }

    [Fact]
    public async Task ProcessBatchAsync_WithCancellationDuringProcessing_StopsEarly()
    {
        // Arrange
        var cts = new CancellationTokenSource();
        var processedCount = 0;
        
        var processor = CreateProcessor(async (i, ct) =>
        {
            Interlocked.Increment(ref processedCount);
            await Task.Delay(50, ct);
            return i.ToString();
        });
        
        var items = Enumerable.Range(1, 100);

        // Act
        cts.CancelAfter(100);
        var result = await processor.ProcessBatchAsync(items, cts.Token);

        // Assert
        result.Success.Should().BeFalse();
        processedCount.Should().BeLessThan(100);
    }

    #endregion

    #region Statistics Tests

    [Fact]
    public async Task GetStatisticsAsync_AfterProcessing_ReturnsAccurateStats()
    {
        // Arrange
        var processor = CreateProcessor(async (i, ct) =>
        {
            await Task.Delay(10, ct);
            if (i % 5 == 0)
                throw new InvalidOperationException("Failed");
            return i.ToString();
        });
        
        var items = Enumerable.Range(1, 20);

        // Act
        await processor.ProcessBatchAsync(items);
        var stats = await processor.GetStatisticsAsync();

        // Assert
        stats.ItemsProcessed.Should().Be(16); // 20 - 4 failures (5, 10, 15, 20)
        stats.FailedItems.Should().Be(4);
        stats.AverageProcessingTimeMs.Should().BeGreaterThan(0);
        stats.ThroughputPerSecond.Should().BeGreaterThan(0);
    }

    [Fact]
    public async Task GetStatisticsAsync_BeforeProcessing_ReturnsZeroStats()
    {
        // Arrange
        var processor = CreateProcessor((i, ct) => Task.FromResult(i.ToString()));

        // Act
        var stats = await processor.GetStatisticsAsync();

        // Assert
        stats.ItemsProcessed.Should().Be(0);
        stats.FailedItems.Should().Be(0);
        stats.AverageProcessingTimeMs.Should().Be(0);
        stats.ThroughputPerSecond.Should().Be(0);
    }

    [Fact]
    public async Task GetStatisticsAsync_WithConcurrentProcessing_MaintainsAccuracy()
    {
        // Arrange
        var processor = CreateProcessor(async (i, ct) =>
        {
            await Task.Delay(5, ct);
            return i.ToString();
        }, new ProcessorOptions { WorkerCount = 4 });

        // Act - Process multiple batches concurrently
        var task1 = processor.ProcessBatchAsync(Enumerable.Range(1, 20));
        var task2 = processor.ProcessBatchAsync(Enumerable.Range(21, 20));
        var task3 = processor.ProcessBatchAsync(Enumerable.Range(41, 20));

        await Task.WhenAll(task1, task2, task3);

        var stats = await processor.GetStatisticsAsync();

        // Assert
        stats.ItemsProcessed.Should().Be(60);
        stats.FailedItems.Should().Be(0);
        stats.AverageProcessingTimeMs.Should().BeGreaterThan(0);
    }

    #endregion

    #region Backpressure Tests

    [Fact]
    public async Task ProcessBatchAsync_WithSmallChannelCapacity_HandlesBackpressure()
    {
        // Arrange
        var processor = CreateProcessor(async (i, ct) =>
        {
            await Task.Delay(10, ct); // Slow processing
            return i.ToString();
        }, new ProcessorOptions { WorkerCount = 1, ChannelCapacity = 2 });

        var items = Enumerable.Range(1, 20);

        // Act
        var result = await processor.ProcessBatchAsync(items);

        // Assert
        result.Success.Should().BeTrue();
        result.Results.Should().HaveCount(20);
    }

    #endregion

    #region Processor Options Tests

    [Fact]
    public async Task ProcessBatchAsync_WithCpuIntensiveOptions_UsesOptimalSettings()
    {
        // Arrange
        var processor = CreateProcessor(
            (i, ct) => Task.FromResult(i.ToString()),
            ProcessorOptions.CpuIntensive);

        var items = Enumerable.Range(1, 50);

        // Act
        var result = await processor.ProcessBatchAsync(items);

        // Assert
        result.Success.Should().BeTrue();
        result.Results.Should().HaveCount(50);
    }

    [Fact]
    public async Task ProcessBatchAsync_WithIoIntensiveOptions_UsesHigherConcurrency()
    {
        // Arrange
        var concurrentExecutions = new System.Collections.Concurrent.ConcurrentBag<DateTime>();
        
        var processor = CreateProcessor(async (i, ct) =>
        {
            concurrentExecutions.Add(DateTime.UtcNow);
            await Task.Delay(50, ct); // Simulate I/O
            return i.ToString();
        }, ProcessorOptions.IoIntensive);

        var items = Enumerable.Range(1, 20);

        // Act
        var result = await processor.ProcessBatchAsync(items);

        // Assert
        result.Success.Should().BeTrue();
        
        // Check for high concurrency by looking at timestamp clustering
        var groups = concurrentExecutions
            .GroupBy(t => t.ToString("yyyy-MM-dd HH:mm:ss.ff"))
            .Select(g => g.Count());
        
        groups.Max().Should().BeGreaterThan(1, "Multiple items should be processed concurrently");
    }

    #endregion

    #region Disposal Tests

    [Fact]
    public async Task DisposeAsync_WhenCalled_StopsAllWorkers()
    {
        // Arrange
        var processor = CreateProcessor((i, ct) => Task.FromResult(i.ToString()));

        // Act
        await processor.DisposeAsync();
        
        // Try to use after disposal
        var act = () => processor.ProcessBatchAsync(new[] { 1 });

        // Assert
        await act.Should().ThrowAsync<ObjectDisposedException>();
    }

    [Fact]
    public async Task DisposeAsync_DuringProcessing_CancelsWork()
    {
        // Arrange
        var processor = CreateProcessor(async (i, ct) =>
        {
            await Task.Delay(1000, ct); // Long running task
            return i.ToString();
        });

        // Act
        var processTask = processor.ProcessBatchAsync(Enumerable.Range(1, 10));
        await processor.DisposeAsync();
        var result = await processTask;

        // Assert
        result.Success.Should().BeFalse();
    }

    [Fact]
    public async Task DisposeAsync_CalledMultipleTimes_DoesNotThrow()
    {
        // Arrange
        var processor = CreateProcessor((i, ct) => Task.FromResult(i.ToString()));

        // Act & Assert
        await processor.DisposeAsync();
        await processor.DisposeAsync(); // Should not throw
    }

    #endregion

    #region Complex Scenarios

    [Fact]
    public async Task ProcessBatchAsync_WithVariableProcessingTimes_CompletesAllItems()
    {
        // Arrange
        var random = new Random(42);
        var processor = CreateProcessor(async (i, ct) =>
        {
            await Task.Delay(random.Next(1, 50), ct);
            return $"Result-{i}";
        }, new ProcessorOptions { WorkerCount = 4 });

        var items = Enumerable.Range(1, 30);

        // Act
        var result = await processor.ProcessBatchAsync(items);

        // Assert
        result.Success.Should().BeTrue();
        result.Results.Should().HaveCount(30);
        result.Results.Should().Contain(Enumerable.Range(1, 30).Select(i => $"Result-{i}"));
    }

    [Fact]
    public async Task ProcessBatchAsync_WithTransformation_AppliesCorrectly()
    {
        // Arrange
        var processor = CreateProcessor((i, ct) => 
            Task.FromResult(new string('*', i)));

        var items = new[] { 1, 3, 5, 7 };

        // Act
        var result = await processor.ProcessBatchAsync(items);

        // Assert
        result.Success.Should().BeTrue();
        result.Results.Should().BeEquivalentTo(new[] { "*", "***", "*****", "*******" });
    }

    [Fact]
    public async Task ProcessBatchAsync_WithAsyncProcessor_HandlesCorrectly()
    {
        // Arrange
        var processor = CreateProcessor(async (i, ct) =>
        {
            await Task.Yield(); // Force async
            await Task.Delay(1, ct);
            return await Task.FromResult($"Async-{i}");
        });

        var items = Enumerable.Range(1, 10);

        // Act
        var result = await processor.ProcessBatchAsync(items);

        // Assert
        result.Success.Should().BeTrue();
        result.Results.Should().HaveCount(10);
        result.Results.Should().Contain(Enumerable.Range(1, 10).Select(i => $"Async-{i}"));
    }

    #endregion

    #region Helper Methods

    private ChannelBasedBatchProcessor<TInput, TOutput> CreateProcessor<TInput, TOutput>(
        Func<TInput, CancellationToken, Task<TOutput>> processorFunc,
        ProcessorOptions? options = null)
    {
        var logger = Substitute.For<ILogger<ChannelBasedBatchProcessor<TInput, TOutput>>>();
        var processor = new ChannelBasedBatchProcessor<TInput, TOutput>(logger, processorFunc, options);
        
        // Track for disposal
        if (processor is ChannelBasedBatchProcessor<int, string> intStringProcessor)
        {
            _processors.Add(intStringProcessor);
        }
        
        return processor;
    }

    private ChannelBasedBatchProcessor<int, string> CreateProcessor(
        Func<int, CancellationToken, Task<string>> processorFunc,
        ProcessorOptions? options = null)
    {
        var processor = new ChannelBasedBatchProcessor<int, string>(_logger, processorFunc, options);
        _processors.Add(processor);
        return processor;
    }

    private static async IAsyncEnumerable<int> GenerateAsyncItems(
        int count,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        for (int i = 1; i <= count; i++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            await Task.Yield();
            yield return i;
        }
    }

    public async ValueTask DisposeAsync()
    {
        foreach (var processor in _processors)
        {
            await processor.DisposeAsync();
        }
    }

    #endregion
}