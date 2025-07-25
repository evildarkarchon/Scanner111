using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace Scanner111.Core.Infrastructure;

/// <summary>
/// Represents an enhanced cancellation token source that provides additional functionality,
/// including support for timeout handling, progress tracking, and integrated logging.
/// </summary>
public class EnhancedCancellationTokenSource : IDisposable
{
    private readonly CancellationTokenSource _cts;
    private readonly ILogger<EnhancedCancellationTokenSource> _logger;
    private readonly Timer? _timeoutTimer;
    private volatile bool _disposed;

    /// <summary>
    /// Represents an enhanced cancellation token source that provides additional functionality
    /// such as automatic timeout handling, logging, and progress tracking.
    /// </summary>
    public EnhancedCancellationTokenSource(
        TimeSpan? timeout = null,
        ILogger<EnhancedCancellationTokenSource>? logger = null)
    {
        _cts = new CancellationTokenSource();
        _logger = logger ?? NullLogger<EnhancedCancellationTokenSource>.Instance;

        if (!timeout.HasValue) return;
        _timeoutTimer = new Timer(OnTimeout, null, timeout.Value, Timeout.InfiniteTimeSpan);
        _logger.LogDebug("Cancellation token created with timeout: {Timeout}", timeout.Value);
    }

    public CancellationToken Token => _cts.Token;
    public bool IsCancellationRequested => _cts.Token.IsCancellationRequested;

    /// <summary>
    /// Releases all resources used by the current instance of the EnhancedCancellationTokenSource class.
    /// </summary>
    /// <remarks>
    /// This method ensures that the associated CancellationTokenSource and any timer resources are properly disposed,
    /// and suppresses finalization of the object to optimize garbage collection.
    /// </remarks>
    /// <exception cref="ObjectDisposedException">
    /// Thrown if the method is called on an already disposed object.
    /// </exception>
    public void Dispose()
    {
        if (_disposed) return;

        _timeoutTimer?.Dispose();
        _cts.Dispose();
        _disposed = true;
        GC.SuppressFinalize(this);
    }

    /// <summary>
    /// Requests cancellation on the associated cancellation token.
    /// </summary>
    /// <remarks>
    /// If the EnhancedCancellationTokenSource instance has already been disposed or if cancellation
    /// has already been requested, this method does nothing. Otherwise, it marks the token for
    /// cancellation, logs the cancellation action, and triggers any registered callbacks.
    /// </remarks>
    public void Cancel()
    {
        if (_disposed || _cts.Token.IsCancellationRequested) return;
        _logger.LogInformation("Cancellation requested");
        _cts.Cancel();
    }

    /// <summary>
    /// Schedules a cancellation request to occur after the specified delay.
    /// </summary>
    /// <param name="delay">
    /// The time span to wait before canceling the token.
    /// If the specified value is TimeSpan.Zero, the cancellation is requested immediately.
    /// </param>
    /// <remarks>
    /// This method allows you to set a delay for cancellation, ensuring that the operation
    /// is canceled if not completed within the given timeframe.
    /// </remarks>
    /// <exception cref="ObjectDisposedException">
    /// Thrown if the method is called on an already disposed object.
    /// </exception>
    public void CancelAfter(TimeSpan delay)
    {
        if (_disposed) return;
        _cts.CancelAfter(delay);
        _logger.LogDebug("Cancellation scheduled after: {Delay}", delay);
    }

    /// <summary>
    /// Handles the timeout event and triggers cancellation of the token.
    /// </summary>
    /// <param name="state">
    /// An optional state object passed by the timer that initiated the timeout event.
    /// </param>
    private void OnTimeout(object? state)
    {
        if (_disposed || _cts.Token.IsCancellationRequested) return;
        _logger.LogWarning("Operation timed out, cancelling");
        _cts.Cancel();
    }
}

/// <summary>
/// Provides helper methods and utilities for working with cancellation tokens to facilitate cooperative
/// cancellation, including linked tokens, timeout tokens, and checkpointing for long-running operations.
/// </summary>
public static class CancellationHelper
{
    /// <summary>
    /// Throws an <see cref="OperationCanceledException"/> if the cancellation token has been canceled,
    /// optionally including a context-specific operation name in the exception message.
    /// </summary>
    /// <param name="cancellationToken">The <see cref="CancellationToken"/> to monitor for cancellation requests.</param>
    /// <param name="operation">An optional string specifying the name of the operation for context in the exception message.</param>
    public static void ThrowIfCancellationRequested(this CancellationToken cancellationToken, string? operation = null)
    {
        if (!cancellationToken.IsCancellationRequested) return;
        var message = operation != null ? $"Operation '{operation}' was cancelled" : "Operation was cancelled";
        throw new OperationCanceledException(message, cancellationToken);
    }

    /// <summary>
    /// Executes a checkpoint that checks for cancellation, optionally reports progress,
    /// and yields control to allow concurrency in long-running operations.
    /// </summary>
    /// <param name="cancellationToken">The cancellation token to monitor for cancellation requests.</param>
    /// <param name="operation">An optional description of the operation being performed.</param>
    /// <param name="progress">An optional progress reporter to report the current operation status.</param>
    /// <returns>A task that completes when the checkpoint operation has finished.</returns>
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
    /// Combines multiple <see cref="CancellationToken"/> instances into a single
    /// linked cancellation token source, allowing cancellation to be triggered
    /// by any of the provided tokens.
    /// </summary>
    /// <param name="tokens">An array of <see cref="CancellationToken"/> instances
    /// to link together.</param>
    /// <returns>A new <see cref="CancellationTokenSource"/> that is linked to
    /// the provided tokens.</returns>
    public static CancellationTokenSource CreateLinkedTokenSource(params CancellationToken[] tokens)
    {
        return CancellationTokenSource.CreateLinkedTokenSource(tokens);
    }

    /// <summary>
    /// Creates a cancellation token source that will automatically cancel after the specified timeout interval.
    /// </summary>
    /// <param name="timeout">The duration after which the cancellation token source will be canceled.</param>
    /// <returns>A <see cref="CancellationTokenSource"/> that triggers cancellation after the specified timeout.</returns>
    public static CancellationTokenSource CreateTimeoutToken(TimeSpan timeout)
    {
        return new CancellationTokenSource(timeout);
    }

    /// <summary>
    /// Combines a user-provided <see cref="CancellationToken"/> with a timeout, resulting in
    /// a linked cancellation token source that will be canceled when either the user token
    /// or the timeout expires.
    /// </summary>
    /// <param name="userToken">The user-provided <see cref="CancellationToken"/> to combine.</param>
    /// <param name="timeout">The <see cref="TimeSpan"/> representing the duration for the timeout.</param>
    /// <returns>A new <see cref="CancellationTokenSource"/> linked to the user token and the timeout.</returns>
    public static CancellationTokenSource CreateCombinedToken(CancellationToken userToken, TimeSpan timeout)
    {
        var timeoutSource = new CancellationTokenSource(timeout);
        return CreateLinkedTokenSource(userToken, timeoutSource.Token);
    }
}

/// <summary>
/// Provides a mechanism for reporting progress with built-in support for cancellation.
/// This class wraps an existing progress handler and monitors a cancellation token
/// to stop reporting when a cancellation is requested. Optionally supports logging
/// for progress reporting failures or cancellations.
/// </summary>
/// <typeparam name="T">The type of progress data being reported.</typeparam>
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

    /// <summary>
    /// Releases all resources used by the CancellableProgress object and marks it as disposed.
    /// After calling Dispose, the object can no longer be used to track or report progress.
    /// </summary>
    public void Dispose()
    {
        _disposed = true;
        GC.SuppressFinalize(this);
    }

    /// <summary>
    /// Reports a progress value to the associated progress handler while respecting cancellation requests
    /// and handling exceptions during reporting.
    /// </summary>
    /// <param name="value">
    /// The value to report to the progress handler.
    /// </param>
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
/// Represents a cancellation-aware semaphore wrapper that allows controlled access to a shared resource,
/// while supporting both cancellation and timeout handling for acquiring permits.
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

    /// <summary>
    /// Releases all resources used by the <see cref="CancellableSemaphore"/>.
    /// Ensures proper disposal of the underlying semaphore and suppresses finalization
    /// to prevent unnecessary resource overhead.
    /// </summary>
    public void Dispose()
    {
        if (_disposed) return;

        _semaphore.Dispose();
        _disposed = true;
        GC.SuppressFinalize(this);
    }

    /// <summary>
    /// Asynchronously waits to acquire the semaphore, supporting cancellation.
    /// If cancellation is requested before acquiring the semaphore, an <see cref="OperationCanceledException"/> is thrown.
    /// </summary>
    /// <param name="cancellationToken">
    /// The <see cref="CancellationToken"/> to observe for cancellation requests while waiting to acquire the semaphore.
    /// </param>
    /// <returns>
    /// A disposable resource that releases the semaphore when disposed.
    /// </returns>
    public async Task<IDisposable> WaitAsync(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        await _semaphore.WaitAsync(cancellationToken);
        _logger?.LogTrace("Semaphore acquired");

        return new SemaphoreReleaser(_semaphore, _logger);
    }

    /// <summary>
    /// Attempts to acquire the semaphore asynchronously within a specified timeout, with support for cancellation.
    /// Returns a disposable object representing the semaphore release handle if acquired successfully, or null if the acquisition times out.
    /// </summary>
    /// <param name="timeout">The maximum amount of time to wait for the semaphore to be acquired.</param>
    /// <param name="cancellationToken">A token to observe for cancellation requests.</param>
    /// <returns>An <see cref="IDisposable"/> object that releases the semaphore upon disposal, or null if the operation times out.</returns>
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

    /// <summary>
    /// Represents a disposable helper class for releasing a semaphore and logging the release operation.
    /// Ensures that the semaphore is properly released when the instance is disposed,
    /// preventing resource leaks or deadlocks.
    /// </summary>
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

        /// <summary>
        /// Releases all resources used by the <see cref="SemaphoreReleaser"/>.
        /// Ensures that the associated semaphore is released, prevents resource leaks or deadlocks,
        /// and suppresses finalization to minimize resource overhead.
        /// </summary>
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