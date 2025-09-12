using System;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;

namespace Scanner111.Core.Async;

/// <summary>
/// Provides lazy initialization for async operations.
/// Thread-safe and ensures the factory is only called once.
/// </summary>
/// <typeparam name="T">The type of object being lazily initialized</typeparam>
public sealed class AsyncLazy<T>
{
    private readonly Lazy<Task<T>> _lazy;
    private readonly bool _continueOnCapturedContext;

    /// <summary>
    /// Creates a new AsyncLazy instance with a synchronous factory.
    /// </summary>
    public AsyncLazy(Func<T> valueFactory, bool continueOnCapturedContext = false)
        : this(() => Task.FromResult(valueFactory()), continueOnCapturedContext)
    {
        if (valueFactory == null)
            throw new ArgumentNullException(nameof(valueFactory));
    }

    /// <summary>
    /// Creates a new AsyncLazy instance with an async factory.
    /// </summary>
    public AsyncLazy(Func<Task<T>> taskFactory, bool continueOnCapturedContext = false)
    {
        if (taskFactory == null)
            throw new ArgumentNullException(nameof(taskFactory));

        _continueOnCapturedContext = continueOnCapturedContext;
        _lazy = new Lazy<Task<T>>(() => Task.Run(taskFactory));
    }

    /// <summary>
    /// Creates a new AsyncLazy instance with a cancellable async factory.
    /// </summary>
    public AsyncLazy(Func<CancellationToken, Task<T>> taskFactory, bool continueOnCapturedContext = false)
    {
        if (taskFactory == null)
            throw new ArgumentNullException(nameof(taskFactory));

        _continueOnCapturedContext = continueOnCapturedContext;
        _lazy = new Lazy<Task<T>>(() => Task.Run(() => taskFactory(CancellationToken.None)));
    }

    /// <summary>
    /// Gets the value, initializing it if necessary.
    /// </summary>
    public Task<T> Value => _lazy.Value;

    /// <summary>
    /// Gets the value asynchronously.
    /// </summary>
    public ConfiguredTaskAwaitable<T> GetValueAsync() =>
        _lazy.Value.ConfigureAwait(_continueOnCapturedContext);

    /// <summary>
    /// Gets whether the value has been created.
    /// </summary>
    public bool IsValueCreated => _lazy.IsValueCreated && _lazy.Value.IsCompleted;

    /// <summary>
    /// Gets whether the value creation has started.
    /// </summary>
    public bool IsValueCreationStarted => _lazy.IsValueCreated;

    /// <summary>
    /// Gets whether the value creation has faulted.
    /// </summary>
    public bool IsValueFaulted => _lazy.IsValueCreated && _lazy.Value.IsFaulted;

    /// <summary>
    /// Allows awaiting the AsyncLazy directly.
    /// </summary>
    public TaskAwaiter<T> GetAwaiter() => Value.GetAwaiter();

    /// <summary>
    /// Implicit conversion to Task for easier usage.
    /// </summary>
    public static implicit operator Task<T>(AsyncLazy<T> lazy) => lazy.Value;
}

/// <summary>
/// Provides a resettable version of AsyncLazy for scenarios where re-initialization is needed.
/// </summary>
public sealed class ResettableAsyncLazy<T>
{
    private readonly Func<CancellationToken, Task<T>> _factory;
    private readonly SemaphoreSlim _semaphore = new(1, 1);
    private Task<T>? _currentTask;
    private readonly bool _continueOnCapturedContext;

    public ResettableAsyncLazy(
        Func<CancellationToken, Task<T>> factory,
        bool continueOnCapturedContext = false)
    {
        _factory = factory ?? throw new ArgumentNullException(nameof(factory));
        _continueOnCapturedContext = continueOnCapturedContext;
    }

    /// <summary>
    /// Gets the value, initializing it if necessary.
    /// </summary>
    public async Task<T> GetValueAsync(CancellationToken cancellationToken = default)
    {
        await _semaphore.WaitAsync(cancellationToken).ConfigureAwait(_continueOnCapturedContext);
        try
        {
            if (_currentTask == null || _currentTask.IsFaulted || _currentTask.IsCanceled)
            {
                _currentTask = _factory(cancellationToken);
            }
            return await _currentTask.ConfigureAwait(_continueOnCapturedContext);
        }
        finally
        {
            _semaphore.Release();
        }
    }

    /// <summary>
    /// Resets the lazy value, causing it to be re-initialized on next access.
    /// </summary>
    public async Task ResetAsync()
    {
        await _semaphore.WaitAsync().ConfigureAwait(false);
        try
        {
            _currentTask = null;
        }
        finally
        {
            _semaphore.Release();
        }
    }

    /// <summary>
    /// Gets whether the value has been created.
    /// </summary>
    public bool IsValueCreated => _currentTask?.IsCompleted == true;
}

/// <summary>
/// Provides async lazy initialization with timeout support.
/// </summary>
public sealed class TimeoutAsyncLazy<T>
{
    private readonly AsyncLazy<T> _lazy;
    private readonly TimeSpan _timeout;

    public TimeoutAsyncLazy(
        Func<CancellationToken, Task<T>> factory,
        TimeSpan timeout,
        bool continueOnCapturedContext = false)
    {
        if (factory == null)
            throw new ArgumentNullException(nameof(factory));
        if (timeout <= TimeSpan.Zero)
            throw new ArgumentOutOfRangeException(nameof(timeout), "Must be positive");

        _timeout = timeout;
        _lazy = new AsyncLazy<T>(async ct =>
        {
            using var cts = CancellationTokenSource.CreateLinkedTokenSource(ct);
            cts.CancelAfter(_timeout);
            
            try
            {
                return await factory(cts.Token).ConfigureAwait(false);
            }
            catch (OperationCanceledException) when (!ct.IsCancellationRequested)
            {
                throw new TimeoutException($"Lazy initialization timed out after {_timeout}");
            }
        }, continueOnCapturedContext);
    }

    /// <summary>
    /// Gets the value with timeout.
    /// </summary>
    public Task<T> Value => _lazy.Value;

    /// <summary>
    /// Gets whether the value has been created.
    /// </summary>
    public bool IsValueCreated => _lazy.IsValueCreated;
}

/// <summary>
/// Provides cached async lazy values with expiration.
/// </summary>
public sealed class CachedAsyncLazy<T> : IAsyncDisposable
{
    private readonly Func<CancellationToken, Task<T>> _factory;
    private readonly TimeSpan _cacheExpiration;
    private readonly SemaphoreSlim _semaphore = new(1, 1);
    private Task<T>? _currentTask;
    private DateTimeOffset _lastRefresh;
    private readonly bool _continueOnCapturedContext;
    private bool _disposed;

    public CachedAsyncLazy(
        Func<CancellationToken, Task<T>> factory,
        TimeSpan cacheExpiration,
        bool continueOnCapturedContext = false)
    {
        _factory = factory ?? throw new ArgumentNullException(nameof(factory));
        if (cacheExpiration <= TimeSpan.Zero)
            throw new ArgumentOutOfRangeException(nameof(cacheExpiration), "Must be positive");

        _cacheExpiration = cacheExpiration;
        _continueOnCapturedContext = continueOnCapturedContext;
        _lastRefresh = DateTimeOffset.MinValue;
    }

    /// <summary>
    /// Gets the value, refreshing if cache has expired.
    /// </summary>
    public async Task<T> GetValueAsync(CancellationToken cancellationToken = default)
    {
        await _semaphore.WaitAsync(cancellationToken).ConfigureAwait(_continueOnCapturedContext);
        try
        {
            var now = DateTimeOffset.UtcNow;
            var isExpired = now - _lastRefresh > _cacheExpiration;

            if (_currentTask == null || _currentTask.IsFaulted || _currentTask.IsCanceled || isExpired)
            {
                _currentTask = _factory(cancellationToken);
                _lastRefresh = now;
            }

            return await _currentTask.ConfigureAwait(_continueOnCapturedContext);
        }
        finally
        {
            _semaphore.Release();
        }
    }

    /// <summary>
    /// Forces a refresh of the cached value.
    /// </summary>
    public async Task<T> RefreshAsync(CancellationToken cancellationToken = default)
    {
        await _semaphore.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            _currentTask = _factory(cancellationToken);
            _lastRefresh = DateTimeOffset.UtcNow;
            return await _currentTask.ConfigureAwait(false);
        }
        finally
        {
            _semaphore.Release();
        }
    }

    public async ValueTask DisposeAsync()
    {
        if (_disposed)
            return;

        _disposed = true;
        _semaphore.Dispose();
        await Task.CompletedTask;
    }
}