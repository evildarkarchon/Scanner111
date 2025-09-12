using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;

namespace Scanner111.Core.Async;

/// <summary>
/// Provides retry policy functionality with exponential backoff and jitter.
/// </summary>
public sealed class RetryPolicy
{
    private readonly int _maxRetries;
    private readonly TimeSpan _initialDelay;
    private readonly TimeSpan _maxDelay;
    private readonly double _backoffMultiplier;
    private readonly bool _useJitter;
    private readonly ILogger<RetryPolicy>? _logger;
    private readonly Random _jitterRandom = new();

    /// <summary>
    /// Creates a new retry policy with specified parameters.
    /// </summary>
    /// <param name="maxRetries">Maximum number of retry attempts (0 = no retries, only initial attempt)</param>
    /// <param name="initialDelay">Initial delay between retries</param>
    /// <param name="maxDelay">Maximum delay between retries</param>
    /// <param name="backoffMultiplier">Multiplier for exponential backoff (default 2.0)</param>
    /// <param name="useJitter">Whether to add jitter to prevent thundering herd</param>
    /// <param name="logger">Optional logger for retry attempts</param>
    public RetryPolicy(
        int maxRetries = 3,
        TimeSpan? initialDelay = null,
        TimeSpan? maxDelay = null,
        double backoffMultiplier = 2.0,
        bool useJitter = true,
        ILogger<RetryPolicy>? logger = null)
    {
        if (maxRetries < 0)
            throw new ArgumentOutOfRangeException(nameof(maxRetries), "Must be non-negative");
        if (backoffMultiplier < 1.0)
            throw new ArgumentOutOfRangeException(nameof(backoffMultiplier), "Must be >= 1.0");

        _maxRetries = maxRetries;
        _initialDelay = initialDelay ?? TimeSpan.FromSeconds(1);
        _maxDelay = maxDelay ?? TimeSpan.FromMinutes(1);
        _backoffMultiplier = backoffMultiplier;
        _useJitter = useJitter;
        _logger = logger;
    }

    /// <summary>
    /// Executes an async operation with retry logic.
    /// </summary>
    public async Task<T> ExecuteAsync<T>(
        Func<CancellationToken, Task<T>> operation,
        CancellationToken cancellationToken = default)
    {
        return await ExecuteAsync(
            operation,
            shouldRetry: _ => true,
            cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// Executes an async operation with retry logic and custom retry predicate.
    /// </summary>
    public async Task<T> ExecuteAsync<T>(
        Func<CancellationToken, Task<T>> operation,
        Func<Exception, bool> shouldRetry,
        CancellationToken cancellationToken = default)
    {
        if (operation == null)
            throw new ArgumentNullException(nameof(operation));
        if (shouldRetry == null)
            throw new ArgumentNullException(nameof(shouldRetry));

        var attempt = 0;
        var currentDelay = _initialDelay;

        while (true)
        {
            try
            {
                cancellationToken.ThrowIfCancellationRequested();
                
                _logger?.LogDebug("Executing operation, attempt {Attempt}/{MaxAttempts}", 
                    attempt + 1, _maxRetries + 1);
                
                return await operation(cancellationToken).ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
                _logger?.LogDebug("Operation cancelled");
                throw;
            }
            catch (Exception ex) when (attempt < _maxRetries && shouldRetry(ex))
            {
                attempt++;
                
                _logger?.LogWarning(ex, 
                    "Operation failed on attempt {Attempt}/{MaxAttempts}, retrying after {Delay}ms",
                    attempt, _maxRetries + 1, currentDelay.TotalMilliseconds);

                var delayToUse = CalculateDelay(currentDelay);
                await Task.Delay(delayToUse, cancellationToken).ConfigureAwait(false);

                currentDelay = TimeSpan.FromMilliseconds(
                    Math.Min(currentDelay.TotalMilliseconds * _backoffMultiplier, _maxDelay.TotalMilliseconds));
            }
        }
    }

    /// <summary>
    /// Executes an async operation with retry logic (void return).
    /// </summary>
    public async Task ExecuteAsync(
        Func<CancellationToken, Task> operation,
        CancellationToken cancellationToken = default)
    {
        await ExecuteAsync(
            operation,
            shouldRetry: _ => true,
            cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// Executes an async operation with retry logic and custom retry predicate (void return).
    /// </summary>
    public async Task ExecuteAsync(
        Func<CancellationToken, Task> operation,
        Func<Exception, bool> shouldRetry,
        CancellationToken cancellationToken = default)
    {
        await ExecuteAsync<object?>(
            async ct =>
            {
                await operation(ct).ConfigureAwait(false);
                return null;
            },
            shouldRetry,
            cancellationToken).ConfigureAwait(false);
    }

    private TimeSpan CalculateDelay(TimeSpan baseDelay)
    {
        if (!_useJitter)
            return baseDelay;

        // Add jitter: random value between 0.5x and 1.5x the base delay
        var jitterFactor = 0.5 + _jitterRandom.NextDouble();
        return TimeSpan.FromMilliseconds(baseDelay.TotalMilliseconds * jitterFactor);
    }

    /// <summary>
    /// Creates a default retry policy for transient errors.
    /// </summary>
    public static RetryPolicy Default(ILogger<RetryPolicy>? logger = null) =>
        new(maxRetries: 3, initialDelay: TimeSpan.FromSeconds(1), logger: logger);

    /// <summary>
    /// Creates an aggressive retry policy for critical operations.
    /// </summary>
    public static RetryPolicy Aggressive(ILogger<RetryPolicy>? logger = null) =>
        new(maxRetries: 5, initialDelay: TimeSpan.FromMilliseconds(100), logger: logger);

    /// <summary>
    /// Creates a conservative retry policy for non-critical operations.
    /// </summary>
    public static RetryPolicy Conservative(ILogger<RetryPolicy>? logger = null) =>
        new(maxRetries: 2, initialDelay: TimeSpan.FromSeconds(5), logger: logger);
}

/// <summary>
/// Builder for creating custom retry policies.
/// </summary>
public sealed class RetryPolicyBuilder
{
    private int _maxRetries = 3;
    private TimeSpan _initialDelay = TimeSpan.FromSeconds(1);
    private TimeSpan _maxDelay = TimeSpan.FromMinutes(1);
    private double _backoffMultiplier = 2.0;
    private bool _useJitter = true;
    private ILogger<RetryPolicy>? _logger;

    public RetryPolicyBuilder WithMaxRetries(int maxRetries)
    {
        _maxRetries = maxRetries;
        return this;
    }

    public RetryPolicyBuilder WithInitialDelay(TimeSpan delay)
    {
        _initialDelay = delay;
        return this;
    }

    public RetryPolicyBuilder WithMaxDelay(TimeSpan delay)
    {
        _maxDelay = delay;
        return this;
    }

    public RetryPolicyBuilder WithBackoffMultiplier(double multiplier)
    {
        _backoffMultiplier = multiplier;
        return this;
    }

    public RetryPolicyBuilder WithJitter(bool useJitter = true)
    {
        _useJitter = useJitter;
        return this;
    }

    public RetryPolicyBuilder WithLogger(ILogger<RetryPolicy> logger)
    {
        _logger = logger;
        return this;
    }

    public RetryPolicy Build() =>
        new(_maxRetries, _initialDelay, _maxDelay, _backoffMultiplier, _useJitter, _logger);
}