using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Channels;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;

namespace Scanner111.Core.Async;

/// <summary>
/// Provides advanced async utilities for concurrency control, batching, and parallel execution.
/// </summary>
public static class AsyncUtilities
{
    /// <summary>
    /// Executes multiple tasks with limited concurrency.
    /// </summary>
    public static async Task<IReadOnlyList<T>> ExecuteWithConcurrencyAsync<T>(
        IEnumerable<Func<CancellationToken, Task<T>>> taskFactories,
        int maxConcurrency,
        CancellationToken cancellationToken = default)
    {
        if (taskFactories == null)
            throw new ArgumentNullException(nameof(taskFactories));
        if (maxConcurrency <= 0)
            throw new ArgumentOutOfRangeException(nameof(maxConcurrency), "Must be positive");

        var factories = taskFactories.ToList();
        if (factories.Count == 0)
            return Array.Empty<T>();

        var semaphore = new SemaphoreSlim(maxConcurrency, maxConcurrency);
        var results = new T[factories.Count];

        async Task ProcessItem(int index, Func<CancellationToken, Task<T>> factory)
        {
            await semaphore.WaitAsync(cancellationToken).ConfigureAwait(false);
            try
            {
                results[index] = await factory(cancellationToken).ConfigureAwait(false);
            }
            finally
            {
                semaphore.Release();
            }
        }

        var tasks = factories.Select((factory, index) => ProcessItem(index, factory));
        await Task.WhenAll(tasks).ConfigureAwait(false);

        return results;
    }

    /// <summary>
    /// Processes items in batches with concurrency control.
    /// </summary>
    public static async Task<IReadOnlyList<TResult>> BatchProcessAsync<TInput, TResult>(
        IEnumerable<TInput> items,
        Func<TInput, CancellationToken, Task<TResult>> processor,
        int batchSize,
        int maxConcurrency,
        CancellationToken cancellationToken = default)
    {
        if (items == null)
            throw new ArgumentNullException(nameof(items));
        if (processor == null)
            throw new ArgumentNullException(nameof(processor));
        if (batchSize <= 0)
            throw new ArgumentOutOfRangeException(nameof(batchSize), "Must be positive");
        if (maxConcurrency <= 0)
            throw new ArgumentOutOfRangeException(nameof(maxConcurrency), "Must be positive");

        var itemList = items.ToList();
        if (itemList.Count == 0)
            return Array.Empty<TResult>();

        var results = new List<TResult>();
        var semaphore = new SemaphoreSlim(maxConcurrency, maxConcurrency);

        for (int i = 0; i < itemList.Count; i += batchSize)
        {
            var batch = itemList.Skip(i).Take(batchSize).ToList();
            var batchTasks = batch.Select(async item =>
            {
                await semaphore.WaitAsync(cancellationToken).ConfigureAwait(false);
                try
                {
                    return await processor(item, cancellationToken).ConfigureAwait(false);
                }
                finally
                {
                    semaphore.Release();
                }
            });

            var batchResults = await Task.WhenAll(batchTasks).ConfigureAwait(false);
            results.AddRange(batchResults);
        }

        return results;
    }

    /// <summary>
    /// Executes tasks with timeout and returns results or defaults.
    /// </summary>
    public static async Task<IReadOnlyList<T?>> ExecuteWithTimeoutAsync<T>(
        IEnumerable<Func<CancellationToken, Task<T>>> taskFactories,
        TimeSpan timeout,
        CancellationToken cancellationToken = default) where T : class
    {
        var factories = taskFactories.ToList();
        if (factories.Count == 0)
            return Array.Empty<T?>();

        using var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        cts.CancelAfter(timeout);

        var tasks = factories.Select(async factory =>
        {
            try
            {
                return await factory(cts.Token).ConfigureAwait(false);
            }
            catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
            {
                return null; // Timeout
            }
        });

        return await Task.WhenAll(tasks).ConfigureAwait(false);
    }

    /// <summary>
    /// Runs tasks and returns the first successful result.
    /// </summary>
    public static async Task<T> FirstSuccessfulAsync<T>(
        IEnumerable<Func<CancellationToken, Task<T>>> taskFactories,
        CancellationToken cancellationToken = default)
    {
        var factories = taskFactories.ToList();
        if (factories.Count == 0)
            throw new InvalidOperationException("No task factories provided");

        using var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        var tasks = factories.Select(f => f(cts.Token)).ToList();

        while (tasks.Count > 0)
        {
            var completedTask = await Task.WhenAny(tasks).ConfigureAwait(false);
            
            if (completedTask.IsCompletedSuccessfully)
            {
                cts.Cancel(); // Cancel remaining tasks
                return await completedTask.ConfigureAwait(false);
            }

            tasks.Remove(completedTask);

            if (completedTask.IsFaulted)
            {
                // Log or handle the exception if needed
                _ = completedTask.Exception;
            }
        }

        throw new InvalidOperationException("All tasks failed");
    }

    /// <summary>
    /// Provides async enumerable with concurrency control.
    /// </summary>
    public static async IAsyncEnumerable<T> ProcessAsyncEnumerable<T>(
        IAsyncEnumerable<T> source,
        Func<T, CancellationToken, Task<T>> processor,
        int maxConcurrency,
        [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        var channel = Channel.CreateUnbounded<T>();
        var semaphore = new SemaphoreSlim(maxConcurrency, maxConcurrency);

        var processingTask = Task.Run(async () =>
        {
            var tasks = new List<Task>();

            await foreach (var item in source.WithCancellation(cancellationToken))
            {
                await semaphore.WaitAsync(cancellationToken).ConfigureAwait(false);

                tasks.Add(Task.Run(async () =>
                {
                    try
                    {
                        var result = await processor(item, cancellationToken).ConfigureAwait(false);
                        await channel.Writer.WriteAsync(result, cancellationToken).ConfigureAwait(false);
                    }
                    finally
                    {
                        semaphore.Release();
                    }
                }, cancellationToken));
            }

            await Task.WhenAll(tasks).ConfigureAwait(false);
            channel.Writer.Complete();
        }, cancellationToken);

        await foreach (var result in channel.Reader.ReadAllAsync(cancellationToken))
        {
            yield return result;
        }

        await processingTask.ConfigureAwait(false);
    }

    /// <summary>
    /// Executes an operation with circuit breaker pattern.
    /// </summary>
    public static ICircuitBreaker<T> CreateCircuitBreaker<T>(
        Func<CancellationToken, Task<T>> operation,
        int failureThreshold = 5,
        TimeSpan resetTimeout = default,
        ILogger? logger = null)
    {
        return new CircuitBreaker<T>(operation, failureThreshold, 
            resetTimeout == default ? TimeSpan.FromMinutes(1) : resetTimeout, logger);
    }

    /// <summary>
    /// Parallel ForEach with async support and concurrency control.
    /// </summary>
    public static async Task ParallelForEachAsync<T>(
        IEnumerable<T> source,
        Func<T, CancellationToken, Task> body,
        int maxDegreeOfParallelism,
        CancellationToken cancellationToken = default)
    {
        var semaphore = new SemaphoreSlim(maxDegreeOfParallelism, maxDegreeOfParallelism);
        var tasks = source.Select(async item =>
        {
            await semaphore.WaitAsync(cancellationToken).ConfigureAwait(false);
            try
            {
                await body(item, cancellationToken).ConfigureAwait(false);
            }
            finally
            {
                semaphore.Release();
            }
        });

        await Task.WhenAll(tasks).ConfigureAwait(false);
    }

    /// <summary>
    /// Debounces async operations to prevent rapid successive calls.
    /// </summary>
    public static Func<CancellationToken, Task<T>> Debounce<T>(
        Func<CancellationToken, Task<T>> operation,
        TimeSpan delay)
    {
        CancellationTokenSource? cts = null;
        Task<T>? lastTask = null;

        return async (CancellationToken cancellationToken) =>
        {
            cts?.Cancel();
            cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            
            try
            {
                await Task.Delay(delay, cts.Token).ConfigureAwait(false);
                lastTask = operation(cancellationToken);
                return await lastTask.ConfigureAwait(false);
            }
            catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
            {
                // Debounced - wait for the last task if available
                if (lastTask != null)
                    return await lastTask.ConfigureAwait(false);
                throw;
            }
        };
    }
}

/// <summary>
/// Circuit breaker interface for fault tolerance.
/// </summary>
public interface ICircuitBreaker<T>
{
    Task<T> ExecuteAsync(CancellationToken cancellationToken = default);
    CircuitBreakerState State { get; }
    int FailureCount { get; }
    void Reset();
}

/// <summary>
/// Circuit breaker states.
/// </summary>
public enum CircuitBreakerState
{
    Closed,
    Open,
    HalfOpen
}

/// <summary>
/// Circuit breaker implementation.
/// </summary>
internal sealed class CircuitBreaker<T> : ICircuitBreaker<T>
{
    private readonly Func<CancellationToken, Task<T>> _operation;
    private readonly int _failureThreshold;
    private readonly TimeSpan _resetTimeout;
    private readonly ILogger? _logger;
    private readonly SemaphoreSlim _semaphore = new(1, 1);

    private CircuitBreakerState _state = CircuitBreakerState.Closed;
    private int _failureCount;
    private DateTimeOffset _lastFailureTime;

    public CircuitBreakerState State => _state;
    public int FailureCount => _failureCount;

    public CircuitBreaker(
        Func<CancellationToken, Task<T>> operation,
        int failureThreshold,
        TimeSpan resetTimeout,
        ILogger? logger)
    {
        _operation = operation;
        _failureThreshold = failureThreshold;
        _resetTimeout = resetTimeout;
        _logger = logger;
    }

    public async Task<T> ExecuteAsync(CancellationToken cancellationToken = default)
    {
        await _semaphore.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            if (_state == CircuitBreakerState.Open)
            {
                if (DateTimeOffset.UtcNow - _lastFailureTime > _resetTimeout)
                {
                    _state = CircuitBreakerState.HalfOpen;
                    _logger?.LogInformation("Circuit breaker entering half-open state");
                }
                else
                {
                    throw new InvalidOperationException("Circuit breaker is open");
                }
            }

            try
            {
                var result = await _operation(cancellationToken).ConfigureAwait(false);
                
                if (_state == CircuitBreakerState.HalfOpen)
                {
                    _state = CircuitBreakerState.Closed;
                    _failureCount = 0;
                    _logger?.LogInformation("Circuit breaker closed after successful operation");
                }
                
                return result;
            }
            catch (Exception ex)
            {
                _failureCount++;
                _lastFailureTime = DateTimeOffset.UtcNow;

                if (_failureCount >= _failureThreshold)
                {
                    _state = CircuitBreakerState.Open;
                    _logger?.LogWarning(ex, "Circuit breaker opened after {Count} failures", _failureCount);
                }

                throw;
            }
        }
        finally
        {
            _semaphore.Release();
        }
    }

    public void Reset()
    {
        _state = CircuitBreakerState.Closed;
        _failureCount = 0;
        _logger?.LogInformation("Circuit breaker manually reset");
    }
}