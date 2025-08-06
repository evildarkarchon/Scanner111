using FluentAssertions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Scanner111.Core.Infrastructure;

namespace Scanner111.Tests.Infrastructure;

/// <summary>
/// Provides unit tests for error handling and resilience mechanisms in the infrastructure layer.
/// </summary>
public class ErrorHandlingTests
{
    private readonly ILogger<ResilientExecutor> _executorLogger;
    private readonly ILogger<DefaultErrorHandlingPolicy> _logger;

    public ErrorHandlingTests()
    {
        _logger = NullLogger<DefaultErrorHandlingPolicy>.Instance;
        _executorLogger = NullLogger<ResilientExecutor>.Instance;
    }

    /// Verifies that the DefaultErrorHandlingPolicy correctly handles an OperationCanceledException.
    /// This test ensures the policy produces an appropriate ErrorHandlingResult
    /// with the expected action, log level, and message content when an OperationCanceledException occurs.
    /// <remarks>Preconditions:<br/>
    /// - An instance of DefaultErrorHandlingPolicy is initialized with a logger.<br/>
    /// - An OperationCanceledException object is created with a test message.<br/></remarks>
    /// <remarks>Postconditions:<br/>
    /// - The returned ErrorHandlingResult has an action of Fail.<br/>
    /// - The log level is Information.<br/>
    /// - The result message contains the word "cancelled".</remarks>
    [Fact]
    public void DefaultErrorHandlingPolicy_HandlesOperationCancelledException()
    {
        // Arrange
        var policy = new DefaultErrorHandlingPolicy(_logger);
        var exception = new OperationCanceledException("Test cancellation");

        // Act
        var result = policy.HandleError(exception, "test context", 1);

        // Assert
        result.Action.Should().Be(ErrorAction.Fail, "because the action should fail");
        result.LogLevel.Should().Be(LogLevel.Information, "because log level is Information");
        result.Message.Should().Contain("cancelled", "because cancellation message should be included");
    }

    /// Verifies that the DefaultErrorHandlingPolicy correctly handles an UnauthorizedAccessException.
    /// This test ensures the policy produces an appropriate ErrorHandlingResult
    /// with the expected action, log level, and message content when an UnauthorizedAccessException occurs.
    /// <remarks>Preconditions:<br/>
    /// - An instance of DefaultErrorHandlingPolicy is initialized with a logger.<br/>
    /// - An UnauthorizedAccessException object is created with a specific error message.<br/></remarks>
    /// <remarks>Postconditions:<br/>
    /// - The returned ErrorHandlingResult has an action of Skip.<br/>
    /// - The log level is Warning.<br/>
    /// - The result message contains the provided exception message.</remarks>
    [Fact]
    public void DefaultErrorHandlingPolicy_HandlesUnauthorizedAccessException()
    {
        // Arrange
        var policy = new DefaultErrorHandlingPolicy(_logger);
        var exception = new UnauthorizedAccessException("Access denied");

        // Act
        var result = policy.HandleError(exception, "test context", 1);

        // Assert
        result.Action.Should().Be(ErrorAction.Skip, "because the action should skip");
        result.LogLevel.Should().Be(LogLevel.Warning, "because log level is Warning");
        result.Message.Should().Contain("Access denied", "because exception message should be included");
    }

    /// Verifies that the DefaultErrorHandlingPolicy correctly handles a FileNotFoundException.
    /// This test ensures the policy produces an appropriate ErrorHandlingResult
    /// with the expected action, log level, and message content when a FileNotFoundException is encountered.
    /// <remarks>Preconditions:<br/>
    /// - An instance of DefaultErrorHandlingPolicy is initialized with a logger.<br/>
    /// - A FileNotFoundException object is created with a specific test message ("File not found").<br/></remarks>
    /// <remarks>Postconditions:<br/>
    /// - The returned ErrorHandlingResult has an action of Skip.<br/>
    /// - The log level is Warning.<br/>
    /// - The result message contains the text "File not found".</remarks>
    [Fact]
    public void DefaultErrorHandlingPolicy_HandlesFileNotFoundException()
    {
        // Arrange
        var policy = new DefaultErrorHandlingPolicy(_logger);
        var exception = new FileNotFoundException("File not found");

        // Act
        var result = policy.HandleError(exception, "test context", 1);

        // Assert
        result.Action.Should().Be(ErrorAction.Skip, "because the action should skip");
        result.LogLevel.Should().Be(LogLevel.Warning, "because log level is Warning");
        result.Message.Should().Contain("File not found", "because exception message should be included");
    }

    /// Confirms that the DefaultErrorHandlingPolicy retries appropriately when an IOException occurs.
    /// This test validates that the policy provides a suitable ErrorHandlingResult with the correct action,
    /// log level, and a specified retry delay for an IOException scenario.
    /// <remarks>Preconditions:<br/>
    /// - An instance of DefaultErrorHandlingPolicy is configured with a logger.<br/>
    /// - An IOException object is created with a specific error message.<br/>
    /// </remarks>
    /// <remarks>Postconditions:<br/>
    /// - The returned ErrorHandlingResult has an action of Retry.<br/>
    /// - A non-null RetryDelay value is present and greater than zero milliseconds.<br/>
    /// - The returned log level is Warning.<br/>
    /// </remarks>
    [Fact]
    public void DefaultErrorHandlingPolicy_RetriesIOException()
    {
        // Arrange
        var policy = new DefaultErrorHandlingPolicy(_logger);
        var exception = new IOException("IO error");

        // Act
        var result = policy.HandleError(exception, "test context", 1);

        // Assert
        result.Action.Should().Be(ErrorAction.Retry, "because IO errors should be retried");
        result.RetryDelay.Should().NotBeNull("because retry needs a delay");
        result.RetryDelay.Value.TotalMilliseconds.Should().BeGreaterThan(0, "because retry delay should be positive");
        result.LogLevel.Should().Be(LogLevel.Warning, "because log level is Warning");
    }

    /// Validates that DefaultErrorHandlingPolicy does not retry an operation after exceeding the maximum allowed retry attempts.
    /// This test ensures that when the retry attempts surpass the configured maximum, the policy returns an ErrorHandlingResult
    /// with the appropriate action indicating that the operation should be skipped.
    /// <remarks>Preconditions:<br/>
    /// - An instance of DefaultErrorHandlingPolicy is initialized with a maximum retry count of 2.<br/>
    /// - An IOException object is created to simulate an I/O error.<br/>
    /// - The method is invoked with an `attemptNumber` that exceeds the maximum retry limit.</remarks>
    /// <remarks>Postconditions:<br/>
    /// - The returned ErrorHandlingResult has an action of Skip.<br/></remarks>
    [Fact]
    public void DefaultErrorHandlingPolicy_DoesNotRetryAfterMaxAttempts()
    {
        // Arrange
        var policy = new DefaultErrorHandlingPolicy(_logger, 2);
        var exception = new IOException("IO error");

        // Act
        var result = policy.HandleError(exception, "test context", 3); // Exceeds max retries

        // Assert
        result.Action.Should().Be(ErrorAction.Skip, "because the action should skip");
    }

    /// Verifies that the DefaultErrorHandlingPolicy correctly handles an OutOfMemoryException.
    /// This test ensures the policy produces an appropriate ErrorHandlingResult
    /// with the expected action, log level, and message content when an OutOfMemoryException occurs.
    /// <remarks>Preconditions:<br/>
    /// - An instance of DefaultErrorHandlingPolicy is initialized with a logger.<br/>
    /// - An OutOfMemoryException object is created with a test message.<br/></remarks>
    /// <remarks>Postconditions:<br/>
    /// - The returned ErrorHandlingResult has an action of Fail.<br/>
    /// - The log level is Critical.<br/>
    /// - The result message contains the error message of the thrown exception.<br/></remarks>
    [Fact]
    public void DefaultErrorHandlingPolicy_HandlesOutOfMemoryException()
    {
        // Arrange
        var policy = new DefaultErrorHandlingPolicy(_logger);
        var exception = new OutOfMemoryException("Out of memory");

        // Act
        var result = policy.HandleError(exception, "test context", 1);

        // Assert
        result.Action.Should().Be(ErrorAction.Fail, "because the action should fail");
        result.LogLevel.Should().Be(LogLevel.Critical, "because log level is Critical");
        result.Message.Should().Contain("Out of memory", "because exception message should be included");
    }

    /// Ensures that the DefaultErrorHandlingPolicy accurately respects the maximum retry limit
    /// when determining whether an operation should be retried after encountering an exception.
    /// This test validates that retries are permitted up to the configured maximum number of attempts
    /// and denied afterward as expected.
    /// <remarks>Preconditions:<br/>
    /// - An instance of DefaultErrorHandlingPolicy is initialized with a logger and a default maximum retry count.<br/>
    /// - An IOException object is created to simulate a retryable failure scenario.<br/></remarks>
    /// <remarks>Postconditions:<br/>
    /// - The ShouldRetry method returns true for attempts less than or equal to the maximum allowed retries.<br/>
    /// - The ShouldRetry method returns false for attempts exceeding the maximum retry limit.<br/></remarks>
    [Fact]
    public void DefaultErrorHandlingPolicy_ShouldRetry_RespectsMaxRetries()
    {
        // Arrange
        var policy = new DefaultErrorHandlingPolicy(_logger);
        var exception = new IOException("IO error");

        // Act & Assert
        policy.ShouldRetry(exception, 1).Should().BeTrue("because first retry is allowed");
        policy.ShouldRetry(exception, 2).Should().BeTrue("because second retry is allowed");
        policy.ShouldRetry(exception, 3).Should().BeTrue("because third retry is allowed");
        policy.ShouldRetry(exception, 4).Should().BeFalse("because fourth retry exceeds max retries");
    }

    /// Verifies that the ResilientExecutor successfully executes an operation
    /// without encountering any errors or requiring retries when the operation functions as intended.
    /// This test ensures the executor correctly processes a successful operation
    /// and returns the expected result without unnecessary intervention.
    /// <remarks>Preconditions:<br/>
    /// - An instance of ResilientExecutor is initialized with a DefaultErrorHandlingPolicy and appropriate loggers.<br/>
    /// - A test operation is defined that completes successfully and returns a predefined result.<br/></remarks>
    /// <remarks>Postconditions:<br/>
    /// - The operation is executed exactly once.<br/>
    /// - The result returned by ResilientExecutor matches the expected result of the operation.<br/></remarks>
    /// <returns>
    /// Confirms that ResilientExecutor correctly completes a successful operation
    /// and accurately returns the operation's result.
    /// </returns>
    [Fact]
    public async Task ResilientExecutor_ExecutesSuccessfulOperation()
    {
        // Arrange
        var policy = new DefaultErrorHandlingPolicy(_logger);
        var executor = new ResilientExecutor(policy, _executorLogger);
        const string expectedResult = "success";

        // Act
        var result = await executor.ExecuteAsync(
            _ => Task.FromResult(expectedResult),
            "test operation");

        // Assert
        result.Should().Be(expectedResult, "because operation should complete successfully");
    }

    /// Verifies that ResilientExecutor retries a failed operation the configured number of times
    /// before succeeding when the transient failures resolve within the retry limit.
    /// This test ensures the retrial mechanism functions as expected with a specific error handling policy.
    /// <remarks>Preconditions:<br/>
    /// - A DefaultErrorHandlingPolicy is configured with 3 maximum retries and a minimal retry delay.<br/>
    /// - A ResilientExecutor instance is initialized with the policy and a test logger.<br/>
    /// - The operation intentionally fails with IOException for the first two attempts and succeeds on the third attempt.<br/></remarks>
    /// <remarks>Postconditions:<br/>
    /// - The operation completes successfully and returns the expected result.<br/>
    /// - The operation is attempted exactly 3 times (2 failures and 1 success).</remarks>
    [Fact]
    public async Task ResilientExecutor_RetriesFailedOperation()
    {
        // Arrange
        var policy = new DefaultErrorHandlingPolicy(_logger, 3, TimeSpan.FromMilliseconds(1));
        var executor = new ResilientExecutor(policy, _executorLogger);
        var attemptCount = 0;

        // Act
        var result = await executor.ExecuteAsync(_ =>
        {
            attemptCount++;
            if (attemptCount < 3) throw new IOException("Temporary failure");
            return Task.FromResult("success");
        }, "test operation");

        // Assert
        result.Should().Be("success", "because operation completed successfully");
        attemptCount.Should().Be(3, "because it took 3 attempts to succeed");
    }

    /// Validates that the ResilientExecutor throws an exception after exceeding the maximum retry attempts.
    /// This test ensures that the retry mechanism adheres to the configured retry limit and propagates the exception
    /// when the operation consistently fails across retries.
    /// <remarks>Preconditions:<br/>
    /// - A DefaultErrorHandlingPolicy instance is created with a maximum retry limit of 2 and a minimal retry delay.<br/>
    /// - A ResilientExecutor is instantiated using the defined policy and logger.<br/>
    /// </remarks>
    /// <remarks>Postconditions:<br/>
    /// - The test verifies that an InvalidOperationException is thrown after the allowed retry attempts are exhausted.<br/>
    /// - The exception message contains context indicating a persistent failure.
    /// </remarks>
    [Fact]
    public async Task ResilientExecutor_ThrowsAfterMaxRetries()
    {
        // Arrange
        var policy = new DefaultErrorHandlingPolicy(_logger, 2, TimeSpan.FromMilliseconds(1));
        var executor = new ResilientExecutor(policy, _executorLogger);

        // Act & Assert
        var action = async () =>
            await executor.ExecuteAsync(_ => throw new InvalidOperationException("Persistent failure"),
                "test operation");
        
        await action.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*Persistent failure*", "because the exception should propagate after max retries");
    }

    /// Validates that the ResilientExecutor properly handles task cancellation scenarios.
    /// This test ensures the executor propagates the OperationCanceledException when the operation is canceled via a CancellationToken.
    /// <remarks>Preconditions:<br/>
    /// - An instance of DefaultErrorHandlingPolicy is initialized and assigned to the ResilientExecutor.<br/>
    /// - A CancellationTokenSource is created and associated with the operation to facilitate cancellation.<br/></remarks>
    /// <remarks>Postconditions:<br/>
    /// - The operation is canceled and an OperationCanceledException is thrown.<br/>
    /// - The exception is correctly handled and propagated by the ResilientExecutor.</remarks>
    [Fact]
    public async Task ResilientExecutor_HandlesCancellation()
    {
        // Arrange
        var policy = new DefaultErrorHandlingPolicy(_logger);
        var executor = new ResilientExecutor(policy, _executorLogger);
        using var cts = new CancellationTokenSource();

        // Act & Assert
        var action = async () =>
            await executor.ExecuteAsync(ct =>
            {
                cts.Cancel();
                ct.ThrowIfCancellationRequested();
                return Task.FromResult("should not reach here");
            }, "test operation", cts.Token);
        
        await action.Should().ThrowAsync<OperationCanceledException>()
            .WithMessage("*", "because cancellation should be propagated");
    }

    /// Verifies that the ResilientExecutor correctly executes an asynchronous operation
    /// without a return value. This test ensures that the provided operation is invoked
    /// as expected under normal conditions and no errors occur during execution.
    /// <remarks>Preconditions:<br/>
    /// - An instance of ResilientExecutor is initialized with a DefaultErrorHandlingPolicy and a logger.<br/>
    /// - An asynchronous operation is defined that sets a flag when executed.<br/></remarks>
    /// <remarks>Postconditions:<br/>
    /// - The provided operation is successfully executed.<br/>
    /// - The flag indicating operation execution is set to true.</remarks>
    /// <returns>Nothing. Confirms the execution of the operation by asserting the expected flag state.</returns>
    [Fact]
    public async Task ResilientExecutor_ExecuteAsync_WithoutReturnValue_Works()
    {
        // Arrange
        var policy = new DefaultErrorHandlingPolicy(_logger);
        var executor = new ResilientExecutor(policy, _executorLogger);
        var executed = false;

        // Act
        await executor.ExecuteAsync(_ =>
        {
            executed = true;
            return Task.CompletedTask;
        }, "test operation");

        // Assert
        executed.Should().BeTrue("because operation should have been executed");
    }

    /// Ensures that the CircuitBreaker permits operations to execute successfully
    /// when the circuit breaker is in a closed state. This test validates that the
    /// operation is performed without any interruptions and the result is returned as expected.
    /// <remarks>Preconditions:<br/>
    /// - A CircuitBreaker instance is created with a non-zero failure threshold and a valid timeout period.<br/>
    /// - The circuit breaker is in a closed state.<br/>
    /// - A reliable operation is defined that returns a successful result.<br/></remarks>
    /// <remarks>Postconditions:<br/>
    /// - The operation executes successfully without throwing any CircuitBreakerOpenException.<br/>
    /// - The returned result matches the expected output of the operation.</remarks>
    [Fact]
    public async Task CircuitBreaker_AllowsOperationWhenClosed()
    {
        // Arrange
        var logger = NullLogger<CircuitBreaker>.Instance;
        var circuitBreaker = new CircuitBreaker(3, TimeSpan.FromSeconds(1), logger);

        // Act
        var result = await circuitBreaker.ExecuteAsync(() => Task.FromResult("success"));

        // Assert
        result.Should().Be("success", "because operation completed successfully");
    }

    /// Validates that the CircuitBreaker transitions to an open state after the failure threshold is exceeded.
    /// This test ensures that when the specified number of consecutive failures occurs,
    /// the CircuitBreaker prevents further operations from being executed until the timeout period elapses.
    /// <remarks>Preconditions::<br/>
    /// - A CircuitBreaker instance is configured with a failure threshold of 2 and a timeout of 1 second.:<br/>
    /// - The operation passed to the CircuitBreaker always throws an exception.:<br/></remarks>
    /// <remarks>Postconditions::<br/>
    /// - After 2 consecutive failures, the CircuitBreaker opens.:<br/>
    /// - Further attempts to execute operations via the CircuitBreaker result in a CircuitBreakerOpenException being thrown.</remarks>
    [Fact]
    public async Task CircuitBreaker_OpensAfterFailureThreshold()
    {
        // Arrange
        var logger = NullLogger<CircuitBreaker>.Instance;
        var circuitBreaker = new CircuitBreaker(2, TimeSpan.FromSeconds(1), logger);

        // Act - Cause failures to exceed threshold
        for (var i = 0; i < 2; i++)
            try
            {
                await circuitBreaker.ExecuteAsync<string>(() => throw new InvalidOperationException("Test failure"));
            }
            catch (InvalidOperationException)
            {
                // Expected
            }

        // Assert - Circuit should be open now
        var action = async () =>
            await circuitBreaker.ExecuteAsync(() => Task.FromResult("should not execute"));
        
        await action.Should().ThrowAsync<CircuitBreakerOpenException>()
            .WithMessage("*", "because circuit breaker opened after threshold exceeded");
    }

    /// Validates that the NoRetryErrorPolicy never retries when an exception occurs.
    /// This test ensures that the ShouldRetry method always returns false,
    /// regardless of the exception and attempt number provided.
    /// <remarks>Preconditions:<br/>
    /// - An instance of NoRetryErrorPolicy is created.<br/>
    /// - An IOException is initialized with a test message.<br/></remarks>
    /// <remarks>Postconditions:<br/>
    /// - The ShouldRetry method returns false for the given exception and attempt number.<br/></remarks>
    [Fact]
    public void NoRetryErrorPolicy_NeverRetries()
    {
        // Arrange
        var policy = new NoRetryErrorPolicy();
        var exception = new IOException("IO error");

        // Act
        var shouldRetry = policy.ShouldRetry(exception, 1);

        // Assert
        shouldRetry.Should().BeFalse("because NoRetryErrorPolicy never allows retries");
    }

    /// Validates that the NoRetryErrorPolicy handles errors by either skipping or failing,
    /// depending on the type of exception provided.
    /// This test ensures the policy returns accurate ErrorHandlingResult actions
    /// when specific exceptions such as IOException or OperationCanceledException are encountered.
    /// <remarks>Preconditions::<br/>
    /// - An instance of NoRetryErrorPolicy is created.:<br/>
    /// - Specific exceptions including IOException and OperationCanceledException are generated.:<br/>
    /// </remarks>
    /// <remarks>Postconditions::<br/>
    /// - When handling an IOException, the action is Skip.:<br/>
    /// - When handling an OperationCanceledException, the action is Fail.
    /// </remarks>
    [Fact]
    public void NoRetryErrorPolicy_AlwaysSkipsOrFails()
    {
        // Arrange
        var policy = new NoRetryErrorPolicy();
        var ioException = new IOException("IO error");
        var cancellationException = new OperationCanceledException("Cancelled");

        // Act
        var ioResult = policy.HandleError(ioException, "test context", 1);
        var cancellationResult = policy.HandleError(cancellationException, "test context", 1);

        // Assert
        ioResult.Action.Should().Be(ErrorAction.Skip, "because IO errors are skipped without retry");
        cancellationResult.Action.Should().Be(ErrorAction.Fail, "because cancellation fails immediately");
    }

    /// Validates that the DefaultErrorHandlingPolicy accurately determines whether a retry
    /// should be attempted for a given exception and attempt number.
    /// This test ensures that the policy's retry logic behaves correctly across multiple attempts.
    /// <param name="attemptNumber">
    /// The current attempt number for processing the action. It is a positive integer representing how many times
    /// the action has been attempted.
    /// </param>
    /// <param name="expectedResult">
    /// The expected outcome of the retry logic. This value indicates if the policy should allow a retry
    /// (true) or avoid retrying (false) based on the provided attempt number and exception.
    /// </param>
    /// <remarks>Preconditions::<br/>
    /// - An instance of DefaultErrorHandlingPolicy is initialized with a logger.:<br/>
    /// - A specific exception, in this case a TimeoutException with a relevant message, is provided.:<br/>
    /// </remarks>
    /// <remarks>Postconditions::<br/>
    /// - The result of the ShouldRetry method matches the expectedResult for the given attemptNumber.:<br/>
    /// - The policy stops retrying when the maximum allowed attempts have been reached.
    /// </remarks>
    [Theory]
    [InlineData(1, true)]
    [InlineData(2, true)]
    [InlineData(3, true)]
    [InlineData(4, false)]
    public void DefaultErrorHandlingPolicy_ShouldRetry_HandlesDifferentAttempts(int attemptNumber, bool expectedResult)
    {
        // Arrange
        var policy = new DefaultErrorHandlingPolicy(_logger);
        var exception = new TimeoutException("Timeout");

        // Act
        var shouldRetry = policy.ShouldRetry(exception, attemptNumber);

        // Assert
        shouldRetry.Should().Be(expectedResult, $"because attempt {attemptNumber} should {(expectedResult ? "allow" : "deny")} retry");
    }

    /// Verifies the default properties of the ErrorHandlingResult object.
    /// This test ensures that an ErrorHandlingResult instance is initialized with the expected default values.
    /// <remarks>Preconditions:<br/>
    /// - A new instance of ErrorHandlingResult is created with specific values assigned for Action and Message.<br/></remarks>
    /// <remarks>Postconditions:<br/>
    /// - The Action property is set to Continue.<br/>
    /// - The Message property is set to "Test message".<br/>
    /// - The ShouldLog property is true.<br/>
    /// - The LogLevel property is Error.<br/>
    /// - The RetryDelay property is null.</remarks>
    [Fact]
    public void ErrorHandlingResult_HasCorrectDefaults()
    {
        // Arrange & Act
        var result = new ErrorHandlingResult
        {
            Action = ErrorAction.Continue,
            Message = "Test message"
        };

        // Assert
        result.Action.Should().Be(ErrorAction.Continue, "because Action was set to Continue");
        result.Message.Should().Be("Test message", "because Message was set");
        result.ShouldLog.Should().BeTrue("because ShouldLog defaults to true");
        result.LogLevel.Should().Be(LogLevel.Error, "because log level defaults to Error");
        result.RetryDelay.Should().BeNull("because RetryDelay was not set");
    }
}