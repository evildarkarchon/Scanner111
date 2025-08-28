using System.Diagnostics;
using System.Threading.Channels;
using Microsoft.Extensions.Logging;

namespace Scanner111.Core.Processing;

/// <summary>
///     High-performance batch processor using System.Threading.Channels.
///     Replaces Python's multiprocessing with efficient thread-based processing.
/// </summary>
public sealed class ChannelBasedBatchProcessor<TInput, TOutput> : IAsyncDisposable
{
    private readonly ILogger<ChannelBasedBatchProcessor<TInput, TOutput>> _logger;
    private readonly Func<TInput, CancellationToken, Task<TOutput>> _processor;
    private readonly Channel<WorkItem> _workChannel;
    private readonly Channel<TOutput> _resultChannel;
    private readonly List<Task> _workers;
    private readonly CancellationTokenSource _shutdownCts;
    private readonly SemaphoreSlim _statsLock;
    private long _itemsProcessed;
    private long _totalProcessingTimeMs;
    private long _failedItems;
    private bool _disposed;

    public ChannelBasedBatchProcessor(
        ILogger<ChannelBasedBatchProcessor<TInput, TOutput>> logger,
        Func<TInput, CancellationToken, Task<TOutput>> processor,
        ProcessorOptions? options = null)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _processor = processor ?? throw new ArgumentNullException(nameof(processor));
        
        options ??= ProcessorOptions.Default;
        
        // Create bounded channels for backpressure
        _workChannel = Channel.CreateBounded<WorkItem>(new BoundedChannelOptions(options.ChannelCapacity)
        {
            FullMode = BoundedChannelFullMode.Wait,
            SingleReader = false,
            SingleWriter = false
        });

        _resultChannel = Channel.CreateUnbounded<TOutput>(new UnboundedChannelOptions
        {
            SingleReader = false,
            SingleWriter = false
        });

        _shutdownCts = new CancellationTokenSource();
        _statsLock = new SemaphoreSlim(1, 1);
        _workers = new List<Task>();

        // Start worker tasks - no process pools needed!
        StartWorkers(options.WorkerCount);
    }

    /// <summary>
    ///     Gets current processing statistics.
    /// </summary>
    public async Task<ProcessingStatistics> GetStatisticsAsync()
    {
        await _statsLock.WaitAsync();
        try
        {
            return new ProcessingStatistics
            {
                ItemsProcessed = _itemsProcessed,
                FailedItems = _failedItems,
                AverageProcessingTimeMs = _itemsProcessed > 0 
                    ? _totalProcessingTimeMs / (double)_itemsProcessed 
                    : 0,
                ThroughputPerSecond = _totalProcessingTimeMs > 0 
                    ? _itemsProcessed * 1000.0 / _totalProcessingTimeMs 
                    : 0
            };
        }
        finally
        {
            _statsLock.Release();
        }
    }

    /// <summary>
    ///     Processes items in batches with optimal parallelism.
    /// </summary>
    public async Task<BatchResult<TOutput>> ProcessBatchAsync(
        IEnumerable<TInput> items,
        CancellationToken cancellationToken = default)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        var stopwatch = Stopwatch.StartNew();
        var results = new List<TOutput>();
        var errors = new List<ProcessingError>();
        
        using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(
            cancellationToken, 
            _shutdownCts.Token);

        try
        {
            // Producer task - posts work items to the channel
            var producerTask = ProduceWorkAsync(items, linkedCts.Token);

            // Consumer task - collects results
            var consumerTask = ConsumeResultsAsync(results, linkedCts.Token);

            // Wait for production to complete
            await producerTask;
            
            // Signal no more work items
            _workChannel.Writer.TryComplete();

            // Wait for all workers to finish processing
            await Task.WhenAll(_workers);

            // Signal no more results
            _resultChannel.Writer.TryComplete();

            // Collect remaining results
            await consumerTask;

            stopwatch.Stop();
            
            _logger.LogInformation(
                "Batch processing completed: {Count} items in {Time:F2}s ({Rate:F2} items/s)",
                results.Count,
                stopwatch.Elapsed.TotalSeconds,
                results.Count / stopwatch.Elapsed.TotalSeconds);

            return new BatchResult<TOutput>
            {
                Success = true,
                Results = results,
                ProcessingTime = stopwatch.Elapsed,
                ItemsProcessed = results.Count,
                Errors = errors
            };
        }
        catch (OperationCanceledException)
        {
            _logger.LogInformation("Batch processing cancelled");
            return new BatchResult<TOutput>
            {
                Success = false,
                Results = results,
                ProcessingTime = stopwatch.Elapsed,
                ItemsProcessed = results.Count,
                Errors = errors
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Batch processing failed");
            errors.Add(new ProcessingError
            {
                Message = ex.Message,
                Exception = ex
            });
            
            return new BatchResult<TOutput>
            {
                Success = false,
                Results = results,
                ProcessingTime = stopwatch.Elapsed,
                ItemsProcessed = results.Count,
                Errors = errors
            };
        }
    }

    /// <summary>
    ///     Processes a stream of items continuously.
    /// </summary>
    public async IAsyncEnumerable<TOutput> ProcessStreamAsync(
        IAsyncEnumerable<TInput> items,
        [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        
        using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(
            cancellationToken, 
            _shutdownCts.Token);

        // Create dedicated channels for this stream operation
        var streamWorkChannel = Channel.CreateUnbounded<TInput>();
        var streamResultChannel = Channel.CreateUnbounded<TOutput>();

        // Start writer task
        var writerTask = Task.Run(async () =>
        {
            try
            {
                await foreach (var item in items.WithCancellation(linkedCts.Token))
                {
                    await streamWorkChannel.Writer.WriteAsync(item, linkedCts.Token);
                }
            }
            finally
            {
                streamWorkChannel.Writer.TryComplete();
            }
        }, linkedCts.Token);

        // Start processor task
        var processorTask = Task.Run(async () =>
        {
            try
            {
                await foreach (var item in streamWorkChannel.Reader.ReadAllAsync(linkedCts.Token))
                {
                    try
                    {
                        var result = await _processor(item, linkedCts.Token);
                        await streamResultChannel.Writer.WriteAsync(result, linkedCts.Token);
                        await UpdateStatisticsAsync(0, success: true);
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "Failed to process stream item");
                        await UpdateStatisticsAsync(0, success: false);
                        // Continue processing other items
                    }
                }
            }
            finally
            {
                streamResultChannel.Writer.TryComplete();
            }
        }, linkedCts.Token);

        // Yield results as they become available
        await foreach (var result in streamResultChannel.Reader.ReadAllAsync(linkedCts.Token))
        {
            yield return result;
        }

        // Ensure tasks complete
        await Task.WhenAll(writerTask, processorTask).ConfigureAwait(false);
    }

    private void StartWorkers(int workerCount)
    {
        _logger.LogDebug("Starting {Count} worker threads", workerCount);

        for (int i = 0; i < workerCount; i++)
        {
            var workerId = i;
            var worker = Task.Run(async () => await WorkerLoopAsync(workerId), _shutdownCts.Token);
            _workers.Add(worker);
        }
    }

    private async Task WorkerLoopAsync(int workerId)
    {
        _logger.LogTrace("Worker {Id} started", workerId);

        try
        {
            await foreach (var workItem in _workChannel.Reader.ReadAllAsync(_shutdownCts.Token))
            {
                var stopwatch = Stopwatch.StartNew();
                
                try
                {
                    var result = await _processor(workItem.Input, _shutdownCts.Token);
                    await _resultChannel.Writer.WriteAsync(result, _shutdownCts.Token);
                    
                    stopwatch.Stop();
                    await UpdateStatisticsAsync(stopwatch.ElapsedMilliseconds, success: true);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Worker {Id} failed to process item", workerId);
                    await UpdateStatisticsAsync(stopwatch.ElapsedMilliseconds, success: false);
                }
            }
        }
        catch (OperationCanceledException)
        {
            _logger.LogTrace("Worker {Id} cancelled", workerId);
        }
        finally
        {
            _logger.LogTrace("Worker {Id} stopped", workerId);
        }
    }

    private async Task ProduceWorkAsync(IEnumerable<TInput> items, CancellationToken cancellationToken)
    {
        foreach (var item in items)
        {
            cancellationToken.ThrowIfCancellationRequested();
            
            await _workChannel.Writer.WriteAsync(
                new WorkItem { Input = item }, 
                cancellationToken);
        }
    }

    private async Task ConsumeResultsAsync(List<TOutput> results, CancellationToken cancellationToken)
    {
        await foreach (var result in _resultChannel.Reader.ReadAllAsync(cancellationToken))
        {
            results.Add(result);
        }
    }

    private async Task UpdateStatisticsAsync(long processingTimeMs, bool success)
    {
        await _statsLock.WaitAsync();
        try
        {
            if (success)
            {
                Interlocked.Increment(ref _itemsProcessed);
                Interlocked.Add(ref _totalProcessingTimeMs, processingTimeMs);
            }
            else
            {
                Interlocked.Increment(ref _failedItems);
            }
        }
        finally
        {
            _statsLock.Release();
        }
    }

    public async ValueTask DisposeAsync()
    {
        if (_disposed)
            return;

        // Signal shutdown
        _shutdownCts?.Cancel();

        // Complete channels
        _workChannel?.Writer.TryComplete();
        _resultChannel?.Writer.TryComplete();

        // Wait for workers to finish
        if (_workers?.Count > 0)
        {
            try
            {
                await Task.WhenAll(_workers);
            }
            catch (OperationCanceledException)
            {
                // Expected during shutdown
            }
        }

        _shutdownCts?.Dispose();
        _statsLock?.Dispose();
        
        _disposed = true;
    }

    private sealed class WorkItem
    {
        public required TInput Input { get; init; }
    }
}

/// <summary>
///     Configuration options for the channel-based processor.
/// </summary>
public sealed class ProcessorOptions
{
    /// <summary>
    ///     Number of worker threads.
    ///     Unlike Python, we can use threads efficiently without GIL.
    /// </summary>
    public int WorkerCount { get; set; } = Environment.ProcessorCount;

    /// <summary>
    ///     Channel capacity for backpressure control.
    /// </summary>
    public int ChannelCapacity { get; set; } = 100;

    /// <summary>
    ///     Default options optimized for general use.
    /// </summary>
    public static ProcessorOptions Default => new()
    {
        WorkerCount = Environment.ProcessorCount,
        ChannelCapacity = 100
    };

    /// <summary>
    ///     Options for CPU-intensive operations.
    ///     No need for process pools like Python!
    /// </summary>
    public static ProcessorOptions CpuIntensive => new()
    {
        WorkerCount = Environment.ProcessorCount,
        ChannelCapacity = Environment.ProcessorCount * 2
    };

    /// <summary>
    ///     Options for I/O-intensive operations.
    /// </summary>
    public static ProcessorOptions IoIntensive => new()
    {
        WorkerCount = Environment.ProcessorCount * 2,
        ChannelCapacity = 1000
    };
}

/// <summary>
///     Result of batch processing.
/// </summary>
public sealed class BatchResult<T>
{
    public bool Success { get; init; }
    public required List<T> Results { get; init; }
    public TimeSpan ProcessingTime { get; init; }
    public int ItemsProcessed { get; init; }
    public required List<ProcessingError> Errors { get; init; }
}

/// <summary>
///     Processing error information.
/// </summary>
public sealed class ProcessingError
{
    public required string Message { get; init; }
    public Exception? Exception { get; init; }
    public DateTimeOffset Timestamp { get; init; } = DateTimeOffset.UtcNow;
}

/// <summary>
///     Processing statistics.
/// </summary>
public sealed class ProcessingStatistics
{
    public long ItemsProcessed { get; init; }
    public long FailedItems { get; init; }
    public double AverageProcessingTimeMs { get; init; }
    public double ThroughputPerSecond { get; init; }
}