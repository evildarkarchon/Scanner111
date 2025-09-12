using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;

namespace Scanner111.Core.Async;

/// <summary>
/// Provides rate limiting functionality using a token bucket algorithm.
/// Thread-safe for concurrent usage.
/// </summary>
public sealed class RateLimiter : IAsyncDisposable
{
    private readonly int _maxTokens;
    private readonly TimeSpan _refillInterval;
    private readonly int _refillAmount;
    private readonly SemaphoreSlim _semaphore;
    private readonly ILogger<RateLimiter>? _logger;
    private readonly Timer _refillTimer;
    private readonly object _tokenLock = new();
    private int _currentTokens;
    private bool _disposed;

    /// <summary>
    /// Creates a new rate limiter with specified parameters.
    /// </summary>
    /// <param name="maxTokens">Maximum number of tokens in the bucket</param>
    /// <param name="refillInterval">How often to refill tokens</param>
    /// <param name="refillAmount">Number of tokens to add per refill</param>
    /// <param name="logger">Optional logger</param>
    public RateLimiter(
        int maxTokens,
        TimeSpan refillInterval,
        int refillAmount,
        ILogger<RateLimiter>? logger = null)
    {
        if (maxTokens <= 0)
            throw new ArgumentOutOfRangeException(nameof(maxTokens), "Must be positive");
        if (refillAmount <= 0)
            throw new ArgumentOutOfRangeException(nameof(refillAmount), "Must be positive");
        if (refillInterval <= TimeSpan.Zero)
            throw new ArgumentOutOfRangeException(nameof(refillInterval), "Must be positive");

        _maxTokens = maxTokens;
        _refillInterval = refillInterval;
        _refillAmount = refillAmount;
        _currentTokens = maxTokens;
        _semaphore = new SemaphoreSlim(1, 1);
        _logger = logger;

        _refillTimer = new Timer(RefillTokens, null, refillInterval, refillInterval);
    }

    /// <summary>
    /// Acquires a token, waiting if necessary.
    /// </summary>
    public async Task AcquireAsync(CancellationToken cancellationToken = default)
    {
        await AcquireAsync(1, cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// Acquires multiple tokens, waiting if necessary.
    /// </summary>
    public async Task AcquireAsync(int tokens, CancellationToken cancellationToken = default)
    {
        if (tokens <= 0)
            throw new ArgumentOutOfRangeException(nameof(tokens), "Must be positive");
        if (tokens > _maxTokens)
            throw new ArgumentOutOfRangeException(nameof(tokens), $"Cannot acquire more than {_maxTokens} tokens");

        while (true)
        {
            cancellationToken.ThrowIfCancellationRequested();

            await _semaphore.WaitAsync(cancellationToken).ConfigureAwait(false);
            try
            {
                if (_currentTokens >= tokens)
                {
                    _currentTokens -= tokens;
                    _logger?.LogDebug("Acquired {Tokens} tokens, {Remaining} remaining", 
                        tokens, _currentTokens);
                    return;
                }

                _logger?.LogDebug("Waiting for tokens: need {Needed}, have {Current}", 
                    tokens, _currentTokens);
            }
            finally
            {
                _semaphore.Release();
            }

            // Wait a bit before trying again
            await Task.Delay(100, cancellationToken).ConfigureAwait(false);
        }
    }

    /// <summary>
    /// Tries to acquire a token without waiting.
    /// </summary>
    public async Task<bool> TryAcquireAsync(CancellationToken cancellationToken = default)
    {
        return await TryAcquireAsync(1, cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// Tries to acquire multiple tokens without waiting.
    /// </summary>
    public async Task<bool> TryAcquireAsync(int tokens, CancellationToken cancellationToken = default)
    {
        if (tokens <= 0)
            throw new ArgumentOutOfRangeException(nameof(tokens), "Must be positive");
        if (tokens > _maxTokens)
            return false;

        await _semaphore.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            if (_currentTokens >= tokens)
            {
                _currentTokens -= tokens;
                _logger?.LogDebug("Acquired {Tokens} tokens, {Remaining} remaining", 
                    tokens, _currentTokens);
                return true;
            }

            _logger?.LogDebug("Failed to acquire {Tokens} tokens, only {Current} available", 
                tokens, _currentTokens);
            return false;
        }
        finally
        {
            _semaphore.Release();
        }
    }

    /// <summary>
    /// Executes an operation with rate limiting.
    /// </summary>
    public async Task<T> ExecuteAsync<T>(
        Func<CancellationToken, Task<T>> operation,
        CancellationToken cancellationToken = default)
    {
        await AcquireAsync(cancellationToken).ConfigureAwait(false);
        return await operation(cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// Executes an operation with rate limiting (void return).
    /// </summary>
    public async Task ExecuteAsync(
        Func<CancellationToken, Task> operation,
        CancellationToken cancellationToken = default)
    {
        await AcquireAsync(cancellationToken).ConfigureAwait(false);
        await operation(cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// Gets the current number of available tokens.
    /// </summary>
    public int AvailableTokens
    {
        get
        {
            lock (_tokenLock)
            {
                return _currentTokens;
            }
        }
    }

    private void RefillTokens(object? state)
    {
        lock (_tokenLock)
        {
            var previousTokens = _currentTokens;
            _currentTokens = Math.Min(_currentTokens + _refillAmount, _maxTokens);
            
            if (_currentTokens != previousTokens)
            {
                _logger?.LogDebug("Refilled tokens: {Previous} -> {Current} (max: {Max})",
                    previousTokens, _currentTokens, _maxTokens);
            }
        }
    }

    public async ValueTask DisposeAsync()
    {
        if (_disposed)
            return;

        _disposed = true;
        await _refillTimer.DisposeAsync().ConfigureAwait(false);
        _semaphore.Dispose();
    }

    /// <summary>
    /// Creates a rate limiter for API calls (e.g., 100 requests per minute).
    /// </summary>
    public static RateLimiter ForApiCalls(
        int requestsPerMinute,
        ILogger<RateLimiter>? logger = null)
    {
        return new RateLimiter(
            maxTokens: requestsPerMinute,
            refillInterval: TimeSpan.FromMinutes(1),
            refillAmount: requestsPerMinute,
            logger);
    }

    /// <summary>
    /// Creates a rate limiter with burst capacity.
    /// </summary>
    public static RateLimiter WithBurst(
        int burstCapacity,
        int sustainedRate,
        TimeSpan period,
        ILogger<RateLimiter>? logger = null)
    {
        var refillInterval = TimeSpan.FromMilliseconds(period.TotalMilliseconds / sustainedRate);
        return new RateLimiter(
            maxTokens: burstCapacity,
            refillInterval: refillInterval,
            refillAmount: 1,
            logger);
    }
}

/// <summary>
/// Provides sliding window rate limiting for more precise control.
/// </summary>
public sealed class SlidingWindowRateLimiter : IAsyncDisposable
{
    private readonly int _maxRequests;
    private readonly TimeSpan _windowSize;
    private readonly Queue<DateTimeOffset> _requestTimestamps;
    private readonly SemaphoreSlim _semaphore;
    private readonly ILogger<SlidingWindowRateLimiter>? _logger;
    private bool _disposed;

    public SlidingWindowRateLimiter(
        int maxRequests,
        TimeSpan windowSize,
        ILogger<SlidingWindowRateLimiter>? logger = null)
    {
        if (maxRequests <= 0)
            throw new ArgumentOutOfRangeException(nameof(maxRequests), "Must be positive");
        if (windowSize <= TimeSpan.Zero)
            throw new ArgumentOutOfRangeException(nameof(windowSize), "Must be positive");

        _maxRequests = maxRequests;
        _windowSize = windowSize;
        _requestTimestamps = new Queue<DateTimeOffset>();
        _semaphore = new SemaphoreSlim(1, 1);
        _logger = logger;
    }

    /// <summary>
    /// Acquires permission to proceed, waiting if necessary.
    /// </summary>
    public async Task AcquireAsync(CancellationToken cancellationToken = default)
    {
        while (true)
        {
            cancellationToken.ThrowIfCancellationRequested();

            await _semaphore.WaitAsync(cancellationToken).ConfigureAwait(false);
            try
            {
                var now = DateTimeOffset.UtcNow;
                CleanOldTimestamps(now);

                if (_requestTimestamps.Count < _maxRequests)
                {
                    _requestTimestamps.Enqueue(now);
                    _logger?.LogDebug("Request allowed, {Count}/{Max} in window",
                        _requestTimestamps.Count, _maxRequests);
                    return;
                }

                // Calculate wait time until oldest request expires
                var oldestTimestamp = _requestTimestamps.Peek();
                var waitTime = oldestTimestamp.Add(_windowSize) - now;
                
                if (waitTime > TimeSpan.Zero)
                {
                    _logger?.LogDebug("Rate limit reached, waiting {WaitTime}ms",
                        waitTime.TotalMilliseconds);
                }
            }
            finally
            {
                _semaphore.Release();
            }

            // Wait before retrying
            await Task.Delay(100, cancellationToken).ConfigureAwait(false);
        }
    }

    /// <summary>
    /// Tries to acquire permission without waiting.
    /// </summary>
    public async Task<bool> TryAcquireAsync(CancellationToken cancellationToken = default)
    {
        await _semaphore.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            var now = DateTimeOffset.UtcNow;
            CleanOldTimestamps(now);

            if (_requestTimestamps.Count < _maxRequests)
            {
                _requestTimestamps.Enqueue(now);
                _logger?.LogDebug("Request allowed, {Count}/{Max} in window",
                    _requestTimestamps.Count, _maxRequests);
                return true;
            }

            _logger?.LogDebug("Request denied, limit reached: {Max} requests in {Window}",
                _maxRequests, _windowSize);
            return false;
        }
        finally
        {
            _semaphore.Release();
        }
    }

    private void CleanOldTimestamps(DateTimeOffset now)
    {
        var cutoff = now - _windowSize;
        while (_requestTimestamps.Count > 0 && _requestTimestamps.Peek() < cutoff)
        {
            _requestTimestamps.Dequeue();
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