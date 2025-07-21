using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace Scanner111.Core.Infrastructure;

/// <summary>
///     Enhanced cancellation token source with timeout and progress tracking
/// </summary>
public class EnhancedCancellationTokenSource : IDisposable
{
    private readonly CancellationTokenSource _cts;
    private readonly ILogger<EnhancedCancellationTokenSource> _logger;
    private readonly Timer? _timeoutTimer;
    private volatile bool _disposed;

    public EnhancedCancellationTokenSource(
        TimeSpan? timeout = null,
        ILogger<EnhancedCancellationTokenSource>? logger = null)
    {
        _cts = new CancellationTokenSource();
        _logger = logger ?? NullLogger<EnhancedCancellationTokenSource>.Instance;

        if (timeout.HasValue)
        {
            _timeoutTimer = new Timer(OnTimeout, null, timeout.Value, Timeout.InfiniteTimeSpan);
            _logger.LogDebug("Cancellation token created with timeout: {Timeout}", timeout.Value);
        }
    }

    public CancellationToken Token => _cts.Token;
    public bool IsCancellationRequested => _cts.Token.IsCancellationRequested;

    public void Dispose()
    {
        if (_disposed) return;

        _timeoutTimer?.Dispose();
        _cts.Dispose();
        _disposed = true;
        GC.SuppressFinalize(this);
    }

    public void Cancel()
    {
        if (!_disposed && !_cts.Token.IsCancellationRequested)
        {
            _logger.LogInformation("Cancellation requested");
            _cts.Cancel();
        }
    }

    public void CancelAfter(TimeSpan delay)
    {
        if (!_disposed)
        {
            _cts.CancelAfter(delay);
            _logger.LogDebug("Cancellation scheduled after: {Delay}", delay);
        }
    }

    private void OnTimeout(object? state)
    {
        if (!_disposed && !_cts.Token.IsCancellationRequested)
        {
            _logger.LogWarning("Operation timed out, cancelling");
            _cts.Cancel();
        }
    }
}

/// <summary>
///     Cooperative cancellation helper for long-running operations
/// </summary>
public static class CancellationHelper
{
    /// <summary>
    ///     Throw if cancellation is requested, with context information
    /// </summary>
    public static void ThrowIfCancellationRequested(this CancellationToken cancellationToken, string? operation = null)
    {
        if (!cancellationToken.IsCancellationRequested) return;
        var message = operation != null ? $"Operation '{operation}' was cancelled" : "Operation was cancelled";
        throw new OperationCanceledException(message, cancellationToken);
    }

    /// <summary>
    ///     Execute a checkpoint that allows cancellation and optional progress reporting
    /// </summary>
    public static async Task CheckpointAsync(
        this CancellationToken cancellationToken,
        string? operation = null,
        IProgress<string>? progress = null)
    {
        cancellationToken.ThrowIfCancellationRequested(operation);

        if (progress != null && operation != null) progress.Report(operation);

        // Yield control to allow other tasks to run
        await Task.Yield();
    }

    /// <summary>
    ///     Create a linked cancellation token that combines multiple sources
    /// </summary>
    public static CancellationTokenSource CreateLinkedTokenSource(params CancellationToken[] tokens)
    {
        return CancellationTokenSource.CreateLinkedTokenSource(tokens);
    }

    /// <summary>
    ///     Create a timeout cancellation token
    /// </summary>
    public static CancellationTokenSource CreateTimeoutToken(TimeSpan timeout)
    {
        return new CancellationTokenSource(timeout);
    }

    /// <summary>
    ///     Combine a user cancellation token with a timeout
    /// </summary>
    public static CancellationTokenSource CreateCombinedToken(CancellationToken userToken, TimeSpan timeout)
    {
        var timeoutSource = new CancellationTokenSource(timeout);
        return CreateLinkedTokenSource(userToken, timeoutSource.Token);
    }
}

/// <summary>
///     Progress reporter that supports cancellation
/// </summary>
public class CancellableProgress<T> : IProgress<T>, IDisposable
{
    private readonly CancellationToken _cancellationToken;
    private readonly IProgress<T>? _innerProgress;
    private readonly ILogger? _logger;
    private volatile bool _disposed;

    public CancellableProgress(
        IProgress<T>? innerProgress,
        CancellationToken cancellationToken,
        ILogger? logger = null)
    {
        _innerProgress = innerProgress;
        _cancellationToken = cancellationToken;
        _logger = logger;
    }

    public void Dispose()
    {
        _disposed = true;
        GC.SuppressFinalize(this);
    }

    public void Report(T value)
    {
        if (_disposed) return;

        try
        {
            _cancellationToken.ThrowIfCancellationRequested();
            _innerProgress?.Report(value);
        }
        catch (OperationCanceledException)
        {
            _logger?.LogDebug("Progress reporting cancelled");
            // Don't propagate cancellation from progress reporting
        }
        catch (Exception ex)
        {
            _logger?.LogWarning(ex, "Error reporting progress");
        }
    }
}

/// <summary>
///     Cancellation-aware semaphore wrapper
/// </summary>
public class CancellableSemaphore : IDisposable
{
    private readonly ILogger<CancellableSemaphore>? _logger;
    private readonly SemaphoreSlim _semaphore;
    private volatile bool _disposed;

    public CancellableSemaphore(int initialCount, int maxCount, ILogger<CancellableSemaphore>? logger = null)
    {
        _semaphore = new SemaphoreSlim(initialCount, maxCount);
        _logger = logger;
    }

    public void Dispose()
    {
        if (_disposed) return;

        _semaphore.Dispose();
        _disposed = true;
        GC.SuppressFinalize(this);
    }

    public async Task<IDisposable> WaitAsync(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        await _semaphore.WaitAsync(cancellationToken);
        _logger?.LogTrace("Semaphore acquired");

        return new SemaphoreReleaser(_semaphore, _logger);
    }

    public async Task<IDisposable?> WaitAsync(TimeSpan timeout, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var acquired = await _semaphore.WaitAsync(timeout, cancellationToken);
        if (acquired)
        {
            _logger?.LogTrace("Semaphore acquired");
            return new SemaphoreReleaser(_semaphore, _logger);
        }

        _logger?.LogDebug("Semaphore acquisition timed out");
        return null;
    }

    private class SemaphoreReleaser : IDisposable
    {
        private readonly ILogger? _logger;
        private readonly SemaphoreSlim _semaphore;
        private volatile bool _released;

        public SemaphoreReleaser(SemaphoreSlim semaphore, ILogger? logger)
        {
            _semaphore = semaphore;
            _logger = logger;
        }

        public void Dispose()
        {
            if (_released) return;

            _semaphore.Release();
            _logger?.LogTrace("Semaphore released");
            _released = true;
            GC.SuppressFinalize(this);
        }
    }
}

/// <summary>
///     Cancellation token extensions for better composability
/// </summary>
public static class CancellationTokenExtensions
{
    /// <summary>
    ///     Register a callback that will be called when cancellation is requested
    /// </summary>
    public static IDisposable RegisterCallback(this CancellationToken cancellationToken, Action callback)
    {
        return cancellationToken.Register(callback);
    }

    /// <summary>
    ///     Register an async callback that will be called when cancellation is requested
    /// </summary>
    public static IDisposable RegisterAsyncCallback(this CancellationToken cancellationToken, Func<Task> callback)
    {
        return cancellationToken.Register(() => Task.Run(callback));
    }

    /// <summary>
    ///     Create a task that completes when cancellation is requested
    /// </summary>
    public static Task WaitForCancellationAsync(this CancellationToken cancellationToken)
    {
        var tcs = new TaskCompletionSource<bool>();
        cancellationToken.Register(() => tcs.SetResult(true));
        return tcs.Task;
    }

    /// <summary>
    ///     Race a task against cancellation
    /// </summary>
    public static async Task<T> WithCancellation<T>(this Task<T> task, CancellationToken cancellationToken)
    {
        var cancellationTask = cancellationToken.WaitForCancellationAsync();
        var completedTask = await Task.WhenAny(task, cancellationTask);

        if (completedTask == cancellationTask) cancellationToken.ThrowIfCancellationRequested();

        return await task;
    }
}