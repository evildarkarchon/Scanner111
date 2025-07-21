using System.Diagnostics;
using Microsoft.Extensions.Logging;

namespace Scanner111.Core.Infrastructure;

/// <summary>
///     Error handling policy for different types of failures
/// </summary>
public interface IErrorHandlingPolicy
{
    /// <summary>
    ///     Handle an error and determine the recovery action
    /// </summary>
    ErrorHandlingResult HandleError(Exception exception, string context, int attemptNumber);
}

/// <summary>
///     Result of error handling
/// </summary>
public record ErrorHandlingResult
{
    public ErrorAction Action { get; init; }
    public TimeSpan? RetryDelay { get; init; }
    public string? Message { get; init; }
    public bool ShouldLog { get; init; } = true;
    public LogLevel LogLevel { get; init; } = LogLevel.Error;
}

/// <summary>
///     Actions to take when handling errors
/// </summary>
public enum ErrorAction
{
    /// <summary>
    ///     Continue processing, ignore the error
    /// </summary>
    Continue,

    /// <summary>
    ///     Retry the operation after a delay
    /// </summary>
    Retry,

    /// <summary>
    ///     Fail fast and stop processing
    /// </summary>
    Fail,

    /// <summary>
    ///     Skip this item and continue with the next
    /// </summary>
    Skip
}

/// <summary>
///     Default error handling policy implementation
/// </summary>
public class DefaultErrorHandlingPolicy : IErrorHandlingPolicy
{
    private readonly TimeSpan _baseRetryDelay;
    private readonly ILogger<DefaultErrorHandlingPolicy> _logger;
    private readonly int _maxRetries;

    public DefaultErrorHandlingPolicy(
        ILogger<DefaultErrorHandlingPolicy> logger,
        int maxRetries = 3,
        TimeSpan? baseRetryDelay = null)
    {
        _logger = logger;
        _maxRetries = maxRetries;
        _baseRetryDelay = baseRetryDelay ?? TimeSpan.FromSeconds(1);
    }

    public ErrorHandlingResult HandleError(Exception exception, string context, int attemptNumber)
    {
        var result = exception switch
        {
            OperationCanceledException => new ErrorHandlingResult
            {
                Action = ErrorAction.Fail,
                Message = "Operation was cancelled",
                LogLevel = LogLevel.Information
            },

            UnauthorizedAccessException => new ErrorHandlingResult
            {
                Action = ErrorAction.Skip,
                Message = $"Access denied to file in {context}",
                LogLevel = LogLevel.Warning
            },

            FileNotFoundException => new ErrorHandlingResult
            {
                Action = ErrorAction.Skip,
                Message = $"File not found in {context}",
                LogLevel = LogLevel.Warning
            },

            DirectoryNotFoundException => new ErrorHandlingResult
            {
                Action = ErrorAction.Skip,
                Message = $"Directory not found in {context}",
                LogLevel = LogLevel.Warning
            },

            IOException when ShouldRetry(exception, attemptNumber) => new ErrorHandlingResult
            {
                Action = ErrorAction.Retry,
                RetryDelay = CalculateRetryDelay(attemptNumber),
                Message = $"IO error in {context}, retrying in {CalculateRetryDelay(attemptNumber)}",
                LogLevel = LogLevel.Warning
            },

            IOException => new ErrorHandlingResult
            {
                Action = ErrorAction.Skip,
                Message = $"IO error in {context} - max retries exceeded, skipping",
                LogLevel = LogLevel.Warning
            },

            OutOfMemoryException => new ErrorHandlingResult
            {
                Action = ErrorAction.Fail,
                Message = "Out of memory - cannot continue processing",
                LogLevel = LogLevel.Critical
            },

            _ when ShouldRetry(exception, attemptNumber) => new ErrorHandlingResult
            {
                Action = ErrorAction.Retry,
                RetryDelay = CalculateRetryDelay(attemptNumber),
                Message = $"Unexpected error in {context}, retrying",
                LogLevel = LogLevel.Warning
            },

            _ => new ErrorHandlingResult
            {
                Action = ErrorAction.Fail,
                Message = $"Unrecoverable error in {context}",
                LogLevel = LogLevel.Error
            }
        };

        // Log the error handling decision
        _logger.LogTrace("Error handling policy decision for {Context} (attempt {Attempt}): {Action} - {Message}",
            context, attemptNumber, result.Action, result.Message);

        return result;
    }

    public bool ShouldRetry(Exception exception, int attemptNumber)
    {
        if (attemptNumber > _maxRetries)
            return false;

        return exception switch
        {
            OperationCanceledException => false,
            UnauthorizedAccessException => false,
            FileNotFoundException => false,
            DirectoryNotFoundException => false,
            OutOfMemoryException => false,
            IOException => true,
            TimeoutException => true,
            _ => attemptNumber < _maxRetries / 2 // Only retry a few times for unexpected errors
        };
    }

    private TimeSpan CalculateRetryDelay(int attemptNumber)
    {
        // Exponential backoff with jitter
        var delay = TimeSpan.FromMilliseconds(_baseRetryDelay.TotalMilliseconds * Math.Pow(2, attemptNumber - 1));
        var jitter = TimeSpan.FromMilliseconds(Random.Shared.Next(0, (int)(delay.TotalMilliseconds * 0.1)));
        return delay + jitter;
    }
}

/// <summary>
///     Resilient operation executor with error handling and retries
/// </summary>
public class ResilientExecutor
{
    private readonly IErrorHandlingPolicy _errorPolicy;
    private readonly ILogger<ResilientExecutor> _logger;

    public ResilientExecutor(IErrorHandlingPolicy errorPolicy, ILogger<ResilientExecutor> logger)
    {
        _errorPolicy = errorPolicy;
        _logger = logger;
    }

    /// <summary>
    ///     Execute an operation with error handling and retries
    /// </summary>
    public async Task<T?> ExecuteAsync<T>(
        Func<CancellationToken, Task<T>> operation,
        string context,
        CancellationToken cancellationToken = default)
    {
        var attemptNumber = 1;
        var stopwatch = Stopwatch.StartNew();

        while (true)
            try
            {
                cancellationToken.ThrowIfCancellationRequested();

                _logger.LogTrace("Executing operation: {Context} (attempt {Attempt})", context, attemptNumber);
                var result = await operation(cancellationToken);

                if (attemptNumber > 1)
                    _logger.LogInformation(
                        "Operation succeeded after {Attempts} attempts in {Duration}ms: {Context}",
                        attemptNumber, stopwatch.ElapsedMilliseconds, context);

                return result;
            }
            catch (Exception ex)
            {
                var errorResult = _errorPolicy.HandleError(ex, context, attemptNumber);

                if (errorResult.ShouldLog)
                    _logger.Log(errorResult.LogLevel, ex,
                        "Error in operation {Context} (attempt {Attempt}): {Message}",
                        context, attemptNumber, errorResult.Message);

                switch (errorResult.Action)
                {
                    case ErrorAction.Continue:
                        return default;

                    case ErrorAction.Skip:
                        return default;

                    case ErrorAction.Fail:
                        throw;

                    case ErrorAction.Retry:
                        if (errorResult.RetryDelay.HasValue)
                            await Task.Delay(errorResult.RetryDelay.Value, cancellationToken);
                        attemptNumber++;
                        continue;

                    default:
                        throw new InvalidOperationException($"Unknown error action: {errorResult.Action}");
                }
            }
    }

    /// <summary>
    ///     Execute an operation without return value
    /// </summary>
    public async Task ExecuteAsync(
        Func<CancellationToken, Task> operation,
        string context,
        CancellationToken cancellationToken = default)
    {
        await ExecuteAsync(async ct =>
        {
            await operation(ct);
            return true; // Dummy return value
        }, context, cancellationToken);
    }
}

/// <summary>
///     Circuit breaker to prevent cascading failures
/// </summary>
public class CircuitBreaker
{
    private readonly int _failureThreshold;
    private readonly object _lock = new();
    private readonly ILogger<CircuitBreaker> _logger;
    private readonly TimeSpan _timeout;

    private int _failureCount;
    private DateTime _lastFailureTime;
    private CircuitBreakerState _state = CircuitBreakerState.Closed;

    public CircuitBreaker(int failureThreshold, TimeSpan timeout, ILogger<CircuitBreaker> logger)
    {
        _failureThreshold = failureThreshold;
        _timeout = timeout;
        _logger = logger;
    }

    public async Task<T> ExecuteAsync<T>(Func<Task<T>> operation)
    {
        if (!CanExecute()) throw new CircuitBreakerOpenException("Circuit breaker is open");

        try
        {
            var result = await operation();
            OnSuccess();
            return result;
        }
        catch (Exception)
        {
            OnFailure();
            throw;
        }
    }

    private bool CanExecute()
    {
        lock (_lock)
        {
            if (_state == CircuitBreakerState.Closed)
                return true;

            if (_state == CircuitBreakerState.Open)
            {
                if (DateTime.UtcNow - _lastFailureTime >= _timeout)
                {
                    _state = CircuitBreakerState.HalfOpen;
                    _logger.LogInformation("Circuit breaker transitioning to half-open");
                    return true;
                }

                return false;
            }

            // Half-open state - allow one test request
            return true;
        }
    }

    private void OnSuccess()
    {
        lock (_lock)
        {
            _failureCount = 0;
            _state = CircuitBreakerState.Closed;
            if (_state != CircuitBreakerState.Closed) _logger.LogInformation("Circuit breaker closed");
        }
    }

    private void OnFailure()
    {
        lock (_lock)
        {
            _failureCount++;
            _lastFailureTime = DateTime.UtcNow;

            if (_failureCount >= _failureThreshold)
            {
                _state = CircuitBreakerState.Open;
                _logger.LogWarning("Circuit breaker opened after {FailureCount} failures", _failureCount);
            }
        }
    }
}

public enum CircuitBreakerState
{
    Closed,
    Open,
    HalfOpen
}

public class CircuitBreakerOpenException(string message) : Exception(message);