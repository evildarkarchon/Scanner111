using System.Diagnostics;
using Microsoft.Extensions.Logging;

namespace Scanner111.Core.Infrastructure;

/// <summary>
/// Specifies a contract for defining error handling policies tailored to different types of failures.
/// </summary>
public interface IErrorHandlingPolicy
{
    /// <summary>
    ///     Handle an error and determine the recovery action
    /// </summary>
    ErrorHandlingResult HandleError(Exception exception, string context, int attemptNumber);
}

/// <summary>
/// Represents the result of an error handling operation, encapsulating the action to take,
/// the delay before retrying (if applicable), and additional metadata like logging preferences and messages.
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
/// Defines the possible actions to take when handling errors during execution.
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
/// Represents the default implementation of the error handling policy,
/// providing mechanisms to handle and log exceptions, determine retry behavior,
/// and define actions based on specific error contexts.
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

    /// <summary>
    /// Handles an exception and determines the appropriate recovery action based on the error type, context,
    /// and attempt number.
    /// </summary>
    /// <param name="exception">
    /// The exception that needs to be processed.
    /// </param>
    /// <param name="context">
    /// The context in which the error occurred, providing additional information about the failure.
    /// </param>
    /// <param name="attemptNumber">
    /// The current attempt number of the operation, used for calculating retry decisions.
    /// </param>
    /// <returns>
    /// An <see cref="ErrorHandlingResult"/> containing the decision on how to handle the error, which may include
    /// an action, a message, a log level, and optional retry information.
    /// </returns>
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

    /// <summary>
    /// Determines whether an operation should be retried
    /// based on the provided exception and the current attempt number.
    /// </summary>
    /// <param name="exception">The exception that occurred during the operation.</param>
    /// <param name="attemptNumber">The current attempt number of the operation.</param>
    /// <returns>
    /// A boolean value indicating whether the operation should be retried.
    /// Returns true if the operation is eligible for a retry, otherwise false.
    /// </returns>
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

    /// <summary>
    /// Calculates the delay time before the next retry attempt using an exponential backoff strategy with jitter.
    /// </summary>
    /// <param name="attemptNumber">The current retry attempt number, starting from 1.</param>
    /// <returns>The calculated delay as a <see cref="TimeSpan"/> before the next retry attempt.</returns>
    private TimeSpan CalculateRetryDelay(int attemptNumber)
    {
        // Exponential backoff with jitter
        var delay = TimeSpan.FromMilliseconds(_baseRetryDelay.TotalMilliseconds * Math.Pow(2, attemptNumber - 1));
        var jitter = TimeSpan.FromMilliseconds(Random.Shared.Next(0, (int)(delay.TotalMilliseconds * 0.1)));
        return delay + jitter;
    }
}

/// <summary>
/// Provides a mechanism for executing operations with built-in error handling and retry capabilities.
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
    /// Executes an asynchronous operation with error handling and retry logic.
    /// </summary>
    /// <param name="operation">
    /// The operation to be executed, represented as a function that takes a <see cref="CancellationToken"/>
    /// and returns a task.
    /// </param>
    /// <param name="context">
    /// A contextual string describing the operation for logging and diagnostic purposes.
    /// </param>
    /// <param name="cancellationToken">
    /// A <see cref="CancellationToken"/> used to signal the operation should be cancelled. Optional parameter.
    /// </param>
    /// <returns>
    /// A <see cref="Task"/> representing the asynchronous operation. If the operation produces a result,
    /// the result is returned upon successful completion; otherwise, the method returns null.
    /// </returns>
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
                var result = await operation(cancellationToken).ConfigureAwait(false);

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
                    case ErrorAction.Skip:
                        return default;
                    case ErrorAction.Fail:
                        throw;
                    case ErrorAction.Retry:
                        if (errorResult.RetryDelay.HasValue)
                            await Task.Delay(errorResult.RetryDelay.Value, cancellationToken).ConfigureAwait(false);
                        attemptNumber++;
                        continue;
                    default:
                        throw new InvalidOperationException($"Unknown error action: {errorResult.Action}");
                }
            }
    }

    /// <summary>
    /// Executes an asynchronous operation with error handling and retry logic.
    /// </summary>
    /// <param name="operation">The asynchronous operation to execute.</param>
    /// <param name="context">A contextual description of the operation being executed, used for logging purposes.</param>
    /// <param name="cancellationToken">The cancellation token to observe while executing the operation.</param>
    /// <returns>A task that represents the asynchronous execution of the operation.</returns>
    public async Task ExecuteAsync(
        Func<CancellationToken, Task> operation,
        string context,
        CancellationToken cancellationToken = default)
    {
        await ExecuteAsync(async ct =>
        {
            await operation(ct).ConfigureAwait(false);
            return true; // Dummy return value
        }, context, cancellationToken);
    }
}

/// <summary>
/// Represents a circuit breaker designed to prevent cascading failures by limiting the execution of operations
/// when a pre-defined failure threshold is exceeded or during a configured timeout period.
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

    /// <summary>
    /// Executes the provided asynchronous operation while enforcing circuit breaker policies to manage failures
    /// and prevent cascading errors when the circuit is open.
    /// </summary>
    /// <typeparam name="T">The type of the result returned by the asynchronous operation.</typeparam>
    /// <param name="operation">A function that represents the asynchronous operation to be executed.</param>
    /// <returns>The result of the operation if successfully executed, of type <typeparamref name="T"/>.</returns>
    /// <exception cref="CircuitBreakerOpenException">Thrown if the circuit breaker is open and the operation cannot be executed.</exception>
    /// <exception cref="Exception">Throws any exception that occurs during the execution of the operation.</exception>
    public async Task<T> ExecuteAsync<T>(Func<Task<T>> operation)
    {
        if (!CanExecute()) throw new CircuitBreakerOpenException("Circuit breaker is open");

        try
        {
            var result = await operation().ConfigureAwait(false);
            OnSuccess();
            return result;
        }
        catch (Exception)
        {
            OnFailure();
            throw;
        }
    }

    /// <summary>
    /// Determines if the current operation is allowed to execute based on the circuit breaker's state
    /// and the elapsed time since the last failure when in the Open state.
    /// </summary>
    /// <returns>
    /// A boolean value indicating whether the execution is permitted:
    /// true if the execution is allowed, false otherwise.
    /// </returns>
    private bool CanExecute()
    {
        lock (_lock)
        {
            switch (_state)
            {
                case CircuitBreakerState.Closed:
                    return true;
                case CircuitBreakerState.Open when DateTime.UtcNow - _lastFailureTime >= _timeout:
                    _state = CircuitBreakerState.HalfOpen;
                    _logger.LogInformation("Circuit breaker transitioning to half-open");
                    return true;
                case CircuitBreakerState.Open:
                    return false;
                case CircuitBreakerState.HalfOpen:
                default:
                    // Half-open state - allow one test request
                    return true;
            }
        }
    }

    /// <summary>
    /// Resets the failure count and changes the circuit breaker state to Closed to indicate that operations
    /// can proceed normally. Logs a message if the circuit breaker state changes.
    /// </summary>
    private void OnSuccess()
    {
        lock (_lock)
        {
            _failureCount = 0;
            _state = CircuitBreakerState.Closed;
            if (_state != CircuitBreakerState.Closed) _logger.LogInformation("Circuit breaker closed");
        }
    }

    /// <summary>
    /// Handles failure by updating the internal state of the circuit breaker, including
    /// incrementing the failure count, recording the time of the last failure, and transitioning
    /// to the open state if the failure threshold has been reached.
    /// </summary>
    private void OnFailure()
    {
        lock (_lock)
        {
            _failureCount++;
            _lastFailureTime = DateTime.UtcNow;

            if (_failureCount < _failureThreshold) return;
            _state = CircuitBreakerState.Open;
            _logger.LogWarning("Circuit breaker opened after {FailureCount} failures", _failureCount);
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