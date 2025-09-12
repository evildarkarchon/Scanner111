using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using NSubstitute;
using Scanner111.Core.Async;
using Xunit;

namespace Scanner111.Test.Async;

public class AsyncUtilitiesTests
{
    private readonly ILogger<RetryPolicy> _mockLogger = Substitute.For<ILogger<RetryPolicy>>();

    [Fact]
    public async Task RetryPolicy_SucceedsOnFirstAttempt_NoRetries()
    {
        // Arrange
        var attempts = 0;
        var policy = new RetryPolicy(maxRetries: 3, logger: _mockLogger);

        // Act
        var result = await policy.ExecuteAsync(async ct =>
        {
            attempts++;
            await Task.Delay(10, ct);
            return "success";
        });

        // Assert
        result.Should().Be("success");
        attempts.Should().Be(1);
    }

    [Fact]
    public async Task RetryPolicy_RetriesOnFailure_SucceedsEventually()
    {
        // Arrange
        var attempts = 0;
        var policy = new RetryPolicy(
            maxRetries: 3, 
            initialDelay: TimeSpan.FromMilliseconds(10),
            logger: _mockLogger);

        // Act
        var result = await policy.ExecuteAsync(async ct =>
        {
            attempts++;
            if (attempts < 3)
                throw new InvalidOperationException("Transient error");
            
            await Task.Delay(10, ct);
            return attempts;
        });

        // Assert
        result.Should().Be(3);
        attempts.Should().Be(3);
    }

    [Fact]
    public async Task RetryPolicy_ExceedsMaxRetries_ThrowsLastException()
    {
        // Arrange
        var attempts = 0;
        var policy = new RetryPolicy(
            maxRetries: 2,
            initialDelay: TimeSpan.FromMilliseconds(10),
            logger: _mockLogger);

        // Act & Assert
        await Assert.ThrowsAsync<InvalidOperationException>(async () =>
        {
            await policy.ExecuteAsync<string>(async ct =>
            {
                attempts++;
                await Task.Delay(10, ct);
                throw new InvalidOperationException($"Failure {attempts}");
            });
        });

        attempts.Should().Be(3); // Initial + 2 retries
    }

    [Fact]
    public async Task RetryPolicy_CustomShouldRetry_OnlyRetriesSpecificExceptions()
    {
        // Arrange
        var attempts = 0;
        var policy = new RetryPolicy(
            maxRetries: 3,
            initialDelay: TimeSpan.FromMilliseconds(10),
            logger: _mockLogger);

        // Act & Assert
        await Assert.ThrowsAsync<ArgumentException>(async () =>
        {
            await policy.ExecuteAsync<string>(
                async ct =>
                {
                    attempts++;
                    await Task.Delay(10, ct);
                    throw new ArgumentException("Non-retryable");
                },
                shouldRetry: ex => ex is InvalidOperationException);
        });

        attempts.Should().Be(1); // No retries for ArgumentException
    }

    [Fact]
    public async Task RetryPolicy_ExponentialBackoff_IncreasesDelay()
    {
        // Arrange
        var delays = new List<long>();
        var stopwatch = new Stopwatch();
        var policy = new RetryPolicy(
            maxRetries: 3,
            initialDelay: TimeSpan.FromMilliseconds(10),
            backoffMultiplier: 2.0,
            useJitter: false,
            logger: _mockLogger);

        // Act
        try
        {
            await policy.ExecuteAsync<string>(async ct =>
            {
                if (stopwatch.IsRunning)
                {
                    delays.Add(stopwatch.ElapsedMilliseconds);
                    stopwatch.Restart();
                }
                else
                {
                    stopwatch.Start();
                }
                
                await Task.Delay(1, ct);
                throw new InvalidOperationException("Force retry");
            });
        }
        catch
        {
            // Expected to fail
        }

        // Assert
        delays.Count.Should().Be(3);
        delays[1].Should().BeGreaterThan(delays[0]); // Exponential increase
        delays[2].Should().BeGreaterThan(delays[1]);
    }

    [Fact]
    public async Task RateLimiter_EnforcesTokenLimit()
    {
        // Arrange
        await using var limiter = new RateLimiter(
            maxTokens: 2,
            refillInterval: TimeSpan.FromSeconds(10),
            refillAmount: 2);

        // Act
        var acquired1 = await limiter.TryAcquireAsync();
        var acquired2 = await limiter.TryAcquireAsync();
        var acquired3 = await limiter.TryAcquireAsync();

        // Assert
        acquired1.Should().BeTrue();
        acquired2.Should().BeTrue();
        acquired3.Should().BeFalse();
        limiter.AvailableTokens.Should().Be(0);
    }

    [Fact]
    public async Task RateLimiter_RefillsTokens()
    {
        // Arrange
        await using var limiter = new RateLimiter(
            maxTokens: 2,
            refillInterval: TimeSpan.FromMilliseconds(100),
            refillAmount: 2);

        // Act
        await limiter.AcquireAsync(2);
        limiter.AvailableTokens.Should().Be(0);

        await Task.Delay(150); // Wait for refill

        // Assert
        limiter.AvailableTokens.Should().Be(2);
    }

    [Fact]
    public async Task SlidingWindowRateLimiter_EnforcesWindowLimit()
    {
        // Arrange
        await using var limiter = new SlidingWindowRateLimiter(
            maxRequests: 3,
            windowSize: TimeSpan.FromSeconds(1));

        // Act
        var results = new List<bool>();
        for (int i = 0; i < 5; i++)
        {
            results.Add(await limiter.TryAcquireAsync());
        }

        // Assert
        results.Take(3).Should().AllBeEquivalentTo(true);
        results.Skip(3).Should().AllBeEquivalentTo(false);
    }

    [Fact]
    public async Task AsyncLazy_InitializesOnce()
    {
        // Arrange
        var initCount = 0;
        var lazy = new AsyncLazy<string>(() =>
        {
            Interlocked.Increment(ref initCount);
            return Task.FromResult("value");
        });

        // Act
        var tasks = Enumerable.Range(0, 10)
            .Select(_ => lazy.Value)
            .ToArray();

        var results = await Task.WhenAll(tasks);

        // Assert
        initCount.Should().Be(1);
        results.Should().AllBeEquivalentTo("value");
    }

    [Fact]
    public async Task AsyncLazy_PropagatesExceptions()
    {
        // Arrange
        var lazy = new AsyncLazy<string>(() =>
            Task.FromException<string>(new InvalidOperationException("Init failed")));

        // Act & Assert
        await Assert.ThrowsAsync<InvalidOperationException>(async () => await lazy.Value);
        lazy.IsValueFaulted.Should().BeTrue();
    }

    [Fact]
    public async Task ResettableAsyncLazy_CanReset()
    {
        // Arrange
        var initCount = 0;
        var lazy = new ResettableAsyncLazy<int>(ct =>
        {
            var count = Interlocked.Increment(ref initCount);
            return Task.FromResult(count);
        });

        // Act
        var value1 = await lazy.GetValueAsync();
        var value2 = await lazy.GetValueAsync();
        await lazy.ResetAsync();
        var value3 = await lazy.GetValueAsync();

        // Assert
        value1.Should().Be(1);
        value2.Should().Be(1);
        value3.Should().Be(2);
        initCount.Should().Be(2);
    }

    [Fact]
    public async Task CachedAsyncLazy_RefreshesAfterExpiration()
    {
        // Arrange
        var callCount = 0;
        await using var lazy = new CachedAsyncLazy<int>(
            ct => Task.FromResult(Interlocked.Increment(ref callCount)),
            cacheExpiration: TimeSpan.FromMilliseconds(100));

        // Act
        var value1 = await lazy.GetValueAsync();
        var value2 = await lazy.GetValueAsync();
        await Task.Delay(150);
        var value3 = await lazy.GetValueAsync();

        // Assert
        value1.Should().Be(1);
        value2.Should().Be(1);
        value3.Should().Be(2);
    }

    [Fact]
    public async Task ExecuteWithConcurrency_LimitsParallelism()
    {
        // Arrange
        var concurrentCount = 0;
        var maxObserved = 0;
        var tasks = Enumerable.Range(0, 10).Select(i =>
            new Func<CancellationToken, Task<int>>(async ct =>
            {
                var current = Interlocked.Increment(ref concurrentCount);
                maxObserved = Math.Max(maxObserved, current);
                await Task.Delay(50, ct);
                Interlocked.Decrement(ref concurrentCount);
                return i;
            })).ToList();

        // Act
        var results = await AsyncUtilities.ExecuteWithConcurrencyAsync(
            tasks, maxConcurrency: 3);

        // Assert
        maxObserved.Should().BeLessThanOrEqualTo(3);
        results.Should().BeEquivalentTo(Enumerable.Range(0, 10));
    }

    [Fact]
    public async Task BatchProcessAsync_ProcessesInBatches()
    {
        // Arrange
        var items = Enumerable.Range(1, 10).ToList();
        var batchSizes = new List<int>();

        // Act
        var results = await AsyncUtilities.BatchProcessAsync(
            items,
            async (item, ct) =>
            {
                await Task.Delay(10, ct);
                return item * 2;
            },
            batchSize: 3,
            maxConcurrency: 2);

        // Assert
        results.Should().BeEquivalentTo(items.Select(i => i * 2));
    }

    [Fact]
    public async Task ExecuteWithTimeout_RespectsTimeout()
    {
        // Arrange
        var tasks = new List<Func<CancellationToken, Task<string>>>
        {
            async ct => { await Task.Delay(10, ct); return "fast"; },
            async ct => { await Task.Delay(200, ct); return "slow"; }
        };

        // Act
        var results = await AsyncUtilities.ExecuteWithTimeoutAsync(
            tasks, timeout: TimeSpan.FromMilliseconds(100));

        // Assert
        results[0].Should().Be("fast");
        results[1].Should().BeNull(); // Timed out
    }

    [Fact]
    public async Task FirstSuccessfulAsync_ReturnsFirstSuccess()
    {
        // Arrange
        var attempts = new int[3];
        var tasks = new List<Func<CancellationToken, Task<string>>>
        {
            async ct => { attempts[0]++; await Task.Delay(100, ct); throw new Exception("fail1"); },
            async ct => { attempts[1]++; await Task.Delay(50, ct); return "success"; },
            async ct => { attempts[2]++; await Task.Delay(200, ct); return "too late"; }
        };

        // Act
        var result = await AsyncUtilities.FirstSuccessfulAsync(tasks);

        // Assert
        result.Should().Be("success");
        attempts[1].Should().Be(1);
    }

    [Fact]
    public async Task CircuitBreaker_OpensAfterThreshold()
    {
        // Arrange
        var attempts = 0;
        var breaker = AsyncUtilities.CreateCircuitBreaker<string>(
            async ct =>
            {
                attempts++;
                await Task.Delay(10, ct);
                throw new InvalidOperationException("Always fails");
            },
            failureThreshold: 3,
            resetTimeout: TimeSpan.FromSeconds(1));

        // Act & Assert
        for (int i = 0; i < 3; i++)
        {
            await Assert.ThrowsAsync<InvalidOperationException>(
                async () => await breaker.ExecuteAsync());
        }

        breaker.State.Should().Be(CircuitBreakerState.Open);
        breaker.FailureCount.Should().Be(3);

        // Circuit is open, should fail immediately
        await Assert.ThrowsAsync<InvalidOperationException>(
            async () => await breaker.ExecuteAsync());
        
        attempts.Should().Be(3); // No additional attempt
    }

    [Fact]
    public async Task ParallelForEachAsync_ProcessesAllItems()
    {
        // Arrange
        var items = Enumerable.Range(1, 10).ToList();
        var processed = new List<int>();
        var lockObj = new object();

        // Act
        await AsyncUtilities.ParallelForEachAsync(
            items,
            async (item, ct) =>
            {
                await Task.Delay(10, ct);
                lock (lockObj)
                {
                    processed.Add(item);
                }
            },
            maxDegreeOfParallelism: 3);

        // Assert
        processed.Should().BeEquivalentTo(items);
    }

    [Fact(Timeout = 5000)]
    public async Task RetryPolicy_CancellationToken_PropagatesCancellation()
    {
        // Arrange
        using var cts = new CancellationTokenSource();
        var policy = new RetryPolicy(maxRetries: 10, initialDelay: TimeSpan.FromSeconds(1));
        var attempts = 0;

        // Act
        var task = policy.ExecuteAsync(async ct =>
        {
            attempts++;
            await Task.Delay(100, ct);
            throw new InvalidOperationException("Force retry");
        }, cts.Token);

        await Task.Delay(150);
        cts.Cancel();

        // Assert
        await Assert.ThrowsAsync<TaskCanceledException>(async () => await task);
        attempts.Should().BeLessThan(10);
    }
}