using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Scanner111.Core.Infrastructure;

namespace Scanner111.Tests.Infrastructure;

public class ErrorHandlingTests
{
    private readonly ILogger<ResilientExecutor> _executorLogger;
    private readonly ILogger<DefaultErrorHandlingPolicy> _logger;

    public ErrorHandlingTests()
    {
        _logger = NullLogger<DefaultErrorHandlingPolicy>.Instance;
        _executorLogger = NullLogger<ResilientExecutor>.Instance;
    }

    [Fact]
    public void DefaultErrorHandlingPolicy_HandlesOperationCancelledException()
    {
        // Arrange
        var policy = new DefaultErrorHandlingPolicy(_logger);
        var exception = new OperationCanceledException("Test cancellation");

        // Act
        var result = policy.HandleError(exception, "test context", 1);

        // Assert
        Assert.Equal(ErrorAction.Fail, result.Action);
        Assert.Equal(LogLevel.Information, result.LogLevel);
        Assert.Contains("cancelled", result.Message);
    }

    [Fact]
    public void DefaultErrorHandlingPolicy_HandlesUnauthorizedAccessException()
    {
        // Arrange
        var policy = new DefaultErrorHandlingPolicy(_logger);
        var exception = new UnauthorizedAccessException("Access denied");

        // Act
        var result = policy.HandleError(exception, "test context", 1);

        // Assert
        Assert.Equal(ErrorAction.Skip, result.Action);
        Assert.Equal(LogLevel.Warning, result.LogLevel);
        Assert.Contains("Access denied", result.Message);
    }

    [Fact]
    public void DefaultErrorHandlingPolicy_HandlesFileNotFoundException()
    {
        // Arrange
        var policy = new DefaultErrorHandlingPolicy(_logger);
        var exception = new FileNotFoundException("File not found");

        // Act
        var result = policy.HandleError(exception, "test context", 1);

        // Assert
        Assert.Equal(ErrorAction.Skip, result.Action);
        Assert.Equal(LogLevel.Warning, result.LogLevel);
        Assert.Contains("File not found", result.Message);
    }

    [Fact]
    public void DefaultErrorHandlingPolicy_RetriesIOException()
    {
        // Arrange
        var policy = new DefaultErrorHandlingPolicy(_logger);
        var exception = new IOException("IO error");

        // Act
        var result = policy.HandleError(exception, "test context", 1);

        // Assert
        Assert.Equal(ErrorAction.Retry, result.Action);
        Assert.NotNull(result.RetryDelay);
        Assert.True(result.RetryDelay.Value.TotalMilliseconds > 0);
        Assert.Equal(LogLevel.Warning, result.LogLevel);
    }

    [Fact]
    public void DefaultErrorHandlingPolicy_DoesNotRetryAfterMaxAttempts()
    {
        // Arrange
        var policy = new DefaultErrorHandlingPolicy(_logger, 2);
        var exception = new IOException("IO error");

        // Act
        var result = policy.HandleError(exception, "test context", 3); // Exceeds max retries

        // Assert
        Assert.Equal(ErrorAction.Skip, result.Action);
    }

    [Fact]
    public void DefaultErrorHandlingPolicy_HandlesOutOfMemoryException()
    {
        // Arrange
        var policy = new DefaultErrorHandlingPolicy(_logger);
        var exception = new OutOfMemoryException("Out of memory");

        // Act
        var result = policy.HandleError(exception, "test context", 1);

        // Assert
        Assert.Equal(ErrorAction.Fail, result.Action);
        Assert.Equal(LogLevel.Critical, result.LogLevel);
        Assert.Contains("Out of memory", result.Message);
    }

    [Fact]
    public void DefaultErrorHandlingPolicy_ShouldRetry_RespectsMaxRetries()
    {
        // Arrange
        var policy = new DefaultErrorHandlingPolicy(_logger);
        var exception = new IOException("IO error");

        // Act & Assert
        Assert.True(policy.ShouldRetry(exception, 1));
        Assert.True(policy.ShouldRetry(exception, 2));
        Assert.True(policy.ShouldRetry(exception, 3));
        Assert.False(policy.ShouldRetry(exception, 4));
    }

    [Fact]
    public async Task ResilientExecutor_ExecutesSuccessfulOperation()
    {
        // Arrange
        var policy = new DefaultErrorHandlingPolicy(_logger);
        var executor = new ResilientExecutor(policy, _executorLogger);
        var expectedResult = "success";

        // Act
        var result = await executor.ExecuteAsync(
            _ => Task.FromResult(expectedResult),
            "test operation");

        // Assert
        Assert.Equal(expectedResult, result);
    }

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
        Assert.Equal("success", result);
        Assert.Equal(3, attemptCount);
    }

    [Fact]
    public async Task ResilientExecutor_ThrowsAfterMaxRetries()
    {
        // Arrange
        var policy = new DefaultErrorHandlingPolicy(_logger, 2, TimeSpan.FromMilliseconds(1));
        var executor = new ResilientExecutor(policy, _executorLogger);

        // Act & Assert
        await Assert.ThrowsAsync<InvalidOperationException>(async () =>
        {
            await executor.ExecuteAsync(_ => throw new InvalidOperationException("Persistent failure"),
                "test operation");
        });
    }

    [Fact]
    public async Task ResilientExecutor_HandlesCancellation()
    {
        // Arrange
        var policy = new DefaultErrorHandlingPolicy(_logger);
        var executor = new ResilientExecutor(policy, _executorLogger);
        using var cts = new CancellationTokenSource();

        // Act & Assert
        await Assert.ThrowsAsync<OperationCanceledException>(async () =>
        {
            await executor.ExecuteAsync(ct =>
            {
                cts.Cancel();
                ct.ThrowIfCancellationRequested();
                return Task.FromResult("should not reach here");
            }, "test operation", cts.Token);
        });
    }

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
        Assert.True(executed);
    }

    [Fact]
    public async Task CircuitBreaker_AllowsOperationWhenClosed()
    {
        // Arrange
        var logger = NullLogger<CircuitBreaker>.Instance;
        var circuitBreaker = new CircuitBreaker(3, TimeSpan.FromSeconds(1), logger);

        // Act
        var result = await circuitBreaker.ExecuteAsync(() => Task.FromResult("success"));

        // Assert
        Assert.Equal("success", result);
    }

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
        await Assert.ThrowsAsync<CircuitBreakerOpenException>(async () =>
        {
            await circuitBreaker.ExecuteAsync(() => Task.FromResult("should not execute"));
        });
    }

    [Fact]
    public void NoRetryErrorPolicy_NeverRetries()
    {
        // Arrange
        var policy = new NoRetryErrorPolicy();
        var exception = new IOException("IO error");

        // Act
        var shouldRetry = policy.ShouldRetry(exception, 1);

        // Assert
        Assert.False(shouldRetry);
    }

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
        Assert.Equal(ErrorAction.Skip, ioResult.Action);
        Assert.Equal(ErrorAction.Fail, cancellationResult.Action);
    }

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
        Assert.Equal(expectedResult, shouldRetry);
    }

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
        Assert.Equal(ErrorAction.Continue, result.Action);
        Assert.Equal("Test message", result.Message);
        Assert.True(result.ShouldLog);
        Assert.Equal(LogLevel.Error, result.LogLevel);
        Assert.Null(result.RetryDelay);
    }
}