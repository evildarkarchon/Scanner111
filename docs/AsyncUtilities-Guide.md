# Scanner111 Async Utilities Guide

## Overview

The Scanner111 Async Utilities provide advanced patterns for managing asynchronous operations, improving performance, reliability, and resource management. These utilities are located in the `Scanner111.Core.Async` namespace.

## Core Components

### 1. RetryPolicy - Resilient Operations

The `RetryPolicy` class provides automatic retry capabilities with exponential backoff and jitter for handling transient failures.

#### Basic Usage

```csharp
using Scanner111.Core.Async;

// Use a pre-configured policy
var policy = RetryPolicy.Default(logger);

var result = await policy.ExecuteAsync(async ct =>
{
    return await httpClient.GetStringAsync(url, ct);
});
```

#### Custom Configuration

```csharp
// Using constructor
var policy = new RetryPolicy(
    maxRetries: 5,
    initialDelay: TimeSpan.FromMilliseconds(100),
    maxDelay: TimeSpan.FromSeconds(30),
    backoffMultiplier: 2.0,
    useJitter: true,
    logger: logger
);

// Using builder pattern
var policy = new RetryPolicyBuilder()
    .WithMaxRetries(3)
    .WithInitialDelay(TimeSpan.FromSeconds(1))
    .WithBackoffMultiplier(2.0)
    .WithJitter(true)
    .WithLogger(logger)
    .Build();
```

#### Selective Retry

```csharp
// Only retry specific exceptions
var result = await policy.ExecuteAsync(
    async ct => await SomeOperation(ct),
    shouldRetry: ex => ex is HttpRequestException || ex is TimeoutException
);
```

#### Pre-configured Policies

- `RetryPolicy.Default()` - 3 retries, 1 second initial delay
- `RetryPolicy.Aggressive()` - 5 retries, 100ms initial delay
- `RetryPolicy.Conservative()` - 2 retries, 5 seconds initial delay

### 2. RateLimiter - API & Resource Throttling

The `RateLimiter` uses a token bucket algorithm to control the rate of operations.

#### Basic Usage

```csharp
// Create a rate limiter for API calls (100 requests per minute)
await using var limiter = RateLimiter.ForApiCalls(
    requestsPerMinute: 100,
    logger: logger
);

// Execute with rate limiting
await limiter.ExecuteAsync(async ct =>
{
    await CallApiAsync(ct);
});
```

#### Advanced Configuration

```csharp
// Custom token bucket configuration
await using var limiter = new RateLimiter(
    maxTokens: 10,           // Bucket capacity
    refillInterval: TimeSpan.FromSeconds(1),
    refillAmount: 2,          // Tokens added per interval
    logger: logger
);

// Try to acquire without blocking
if (await limiter.TryAcquireAsync())
{
    await ProcessRequest();
}

// Acquire multiple tokens
await limiter.AcquireAsync(tokens: 3);
```

#### Sliding Window Rate Limiter

For more precise time-window control:

```csharp
await using var limiter = new SlidingWindowRateLimiter(
    maxRequests: 10,
    windowSize: TimeSpan.FromMinutes(1),
    logger: logger
);

await limiter.AcquireAsync(cancellationToken);
```

### 3. AsyncLazy - Deferred Initialization

`AsyncLazy<T>` provides thread-safe lazy initialization for async operations.

#### Basic Usage

```csharp
private readonly AsyncLazy<DatabaseConnection> _dbConnection = 
    new AsyncLazy<DatabaseConnection>(async () =>
    {
        var conn = new DatabaseConnection();
        await conn.InitializeAsync();
        return conn;
    });

// First access initializes, subsequent accesses return cached value
var db = await _dbConnection.Value;
```

#### Resettable Lazy

For scenarios requiring re-initialization:

```csharp
private readonly ResettableAsyncLazy<Configuration> _config = 
    new ResettableAsyncLazy<Configuration>(async ct =>
    {
        return await LoadConfigurationAsync(ct);
    });

// Get current value
var config = await _config.GetValueAsync();

// Force refresh
await _config.ResetAsync();
config = await _config.GetValueAsync(); // Re-initialized
```

#### Cached with Expiration

```csharp
await using var cachedData = new CachedAsyncLazy<WeatherData>(
    async ct => await FetchWeatherDataAsync(ct),
    cacheExpiration: TimeSpan.FromMinutes(5)
);

// Automatically refreshes after expiration
var weather = await cachedData.GetValueAsync();
```

#### Timeout Support

```csharp
var lazyWithTimeout = new TimeoutAsyncLazy<ExpensiveResource>(
    async ct => await CreateExpensiveResourceAsync(ct),
    timeout: TimeSpan.FromSeconds(30)
);

try
{
    var resource = await lazyWithTimeout.Value;
}
catch (TimeoutException)
{
    // Initialization took too long
}
```

### 4. AsyncUtilities - Concurrency Helpers

The `AsyncUtilities` static class provides various helper methods for concurrent operations.

#### Limited Concurrency

```csharp
// Execute multiple tasks with max 3 concurrent
var tasks = urls.Select(url => 
    new Func<CancellationToken, Task<string>>(ct => 
        httpClient.GetStringAsync(url, ct)));

var results = await AsyncUtilities.ExecuteWithConcurrencyAsync(
    tasks,
    maxConcurrency: 3
);
```

#### Batch Processing

```csharp
var logFiles = Directory.GetFiles(logsPath, "*.log");

var results = await AsyncUtilities.BatchProcessAsync(
    logFiles,
    async (file, ct) => await AnalyzeLogFileAsync(file, ct),
    batchSize: 10,
    maxConcurrency: 4
);
```

#### Timeout for Multiple Operations

```csharp
var operations = new List<Func<CancellationToken, Task<Result>>>
{
    ct => SlowOperation1(ct),
    ct => SlowOperation2(ct),
    ct => SlowOperation3(ct)
};

// Returns null for timed-out operations
var results = await AsyncUtilities.ExecuteWithTimeoutAsync(
    operations,
    timeout: TimeSpan.FromSeconds(5)
);
```

#### First Successful Result

```csharp
// Try multiple sources, return first success
var result = await AsyncUtilities.FirstSuccessfulAsync(new[]
{
    ct => TryPrimarySource(ct),
    ct => TrySecondarySource(ct),
    ct => TryFallbackSource(ct)
});
```

#### Circuit Breaker Pattern

```csharp
var breaker = AsyncUtilities.CreateCircuitBreaker(
    async ct => await UnreliableServiceCall(ct),
    failureThreshold: 5,
    resetTimeout: TimeSpan.FromMinutes(1),
    logger: logger
);

try
{
    var result = await breaker.ExecuteAsync();
}
catch (InvalidOperationException) when (breaker.State == CircuitBreakerState.Open)
{
    // Circuit is open, service is unavailable
}
```

#### Parallel ForEach with Async

```csharp
await AsyncUtilities.ParallelForEachAsync(
    filesToProcess,
    async (file, ct) =>
    {
        await ProcessFileAsync(file, ct);
    },
    maxDegreeOfParallelism: 8
);
```

## Integration Examples

### Example 1: Analyzer with Retry and Rate Limiting

```csharp
public class ExternalApiAnalyzer : AnalyzerBase
{
    private readonly RetryPolicy _retryPolicy;
    private readonly RateLimiter _rateLimiter;
    
    public ExternalApiAnalyzer(ILogger<ExternalApiAnalyzer> logger)
    {
        _retryPolicy = RetryPolicy.Default(logger);
        _rateLimiter = RateLimiter.ForApiCalls(60, logger); // 60/min
    }
    
    protected override async Task<ReportFragment> AnalyzeInternalAsync(
        AnalysisContext context,
        CancellationToken cancellationToken)
    {
        // Rate limit and retry API calls
        var apiData = await _rateLimiter.ExecuteAsync(async ct =>
        {
            return await _retryPolicy.ExecuteAsync(
                async innerCt => await CallExternalApi(context.LogPath, innerCt),
                shouldRetry: ex => ex is HttpRequestException,
                ct
            );
        }, cancellationToken);
        
        return BuildReport(apiData);
    }
}
```

### Example 2: Batch Processing with Progress

```csharp
public class BatchLogProcessor
{
    public async Task<ProcessingResults> ProcessLogsAsync(
        string[] logPaths,
        IProgress<int> progress,
        CancellationToken cancellationToken)
    {
        var processedCount = 0;
        
        var results = await AsyncUtilities.BatchProcessAsync(
            logPaths,
            async (logPath, ct) =>
            {
                var result = await AnalyzeLogAsync(logPath, ct);
                
                var count = Interlocked.Increment(ref processedCount);
                progress.Report(count * 100 / logPaths.Length);
                
                return result;
            },
            batchSize: 20,
            maxConcurrency: Environment.ProcessorCount
        );
        
        return new ProcessingResults(results);
    }
}
```

### Example 3: Cached Configuration with AsyncLazy

```csharp
public class ConfigurationService
{
    private readonly CachedAsyncLazy<AppConfiguration> _configuration;
    
    public ConfigurationService(ILogger<ConfigurationService> logger)
    {
        _configuration = new CachedAsyncLazy<AppConfiguration>(
            async ct =>
            {
                logger.LogInformation("Loading configuration...");
                var config = await LoadFromYamlAsync("config.yaml", ct);
                await ValidateConfigurationAsync(config, ct);
                return config;
            },
            cacheExpiration: TimeSpan.FromMinutes(15)
        );
    }
    
    public Task<AppConfiguration> GetConfigurationAsync(CancellationToken ct = default)
    {
        return _configuration.GetValueAsync(ct);
    }
}
```

## Best Practices

### 1. Resource Management

Always dispose of disposable utilities:

```csharp
await using var limiter = new RateLimiter(...);
await using var cachedLazy = new CachedAsyncLazy<T>(...);
```

### 2. Cancellation Token Propagation

Always pass cancellation tokens through:

```csharp
await policy.ExecuteAsync(
    async ct => await Operation(ct),  // Pass ct, not cancellationToken
    cancellationToken
);
```

### 3. Error Handling

Be specific about what errors to retry:

```csharp
// Good: Specific retry conditions
shouldRetry: ex => ex is TransientException || 
                   (ex is HttpRequestException httpEx && 
                    httpEx.StatusCode == HttpStatusCode.TooManyRequests)

// Bad: Retry everything
shouldRetry: _ => true
```

### 4. Logging

Provide loggers for debugging:

```csharp
var policy = new RetryPolicy(
    maxRetries: 3,
    logger: loggerFactory.CreateLogger<RetryPolicy>()
);
```

### 5. ConfigureAwait Usage

In library code, always use ConfigureAwait(false):

```csharp
// Already handled internally by the utilities
var result = await lazy.Value; // No need for ConfigureAwait here
```

## Performance Considerations

### Memory Usage

- `AsyncLazy<T>` holds the result in memory until disposed
- `CachedAsyncLazy<T>` refreshes periodically, consider memory for large objects
- `BatchProcessAsync` accumulates all results in memory

### Thread Pool

- High concurrency limits can exhaust the thread pool
- Use `Environment.ProcessorCount` as a baseline for CPU-bound work
- For I/O-bound work, higher concurrency is usually acceptable

### Rate Limiting Strategy

- Token bucket: Good for burst capacity with sustained rate
- Sliding window: More precise, better for strict compliance
- Consider API limits and server capacity when setting rates

## Migration from Python

Key differences from the Python implementation:

1. **No GIL workarounds needed** - C# has true parallelism
2. **Channel-based instead of Queue** - Better async support
3. **SemaphoreSlim instead of asyncio.Semaphore** - More features
4. **ConfigureAwait(false)** - No Python equivalent needed
5. **ValueTask support** - Better performance for sync completion

## Troubleshooting

### Common Issues

**Issue**: Operations timing out unexpectedly
```csharp
// Solution: Increase timeout or check for deadlocks
var policy = new RetryPolicy(
    maxDelay: TimeSpan.FromMinutes(5) // Increase max delay
);
```

**Issue**: Rate limiter blocking indefinitely
```csharp
// Solution: Use TryAcquireAsync with fallback
if (!await limiter.TryAcquireAsync())
{
    // Queue for later or return cached result
}
```

**Issue**: Circuit breaker opens too frequently
```csharp
// Solution: Adjust threshold and timeout
var breaker = AsyncUtilities.CreateCircuitBreaker(
    operation,
    failureThreshold: 10,  // Increase threshold
    resetTimeout: TimeSpan.FromSeconds(30)  // Decrease reset time
);
```

## Summary

The Scanner111 Async Utilities provide:

- **Reliability**: Retry policies and circuit breakers for fault tolerance
- **Performance**: Rate limiting and concurrency control
- **Efficiency**: Lazy initialization and caching
- **Simplicity**: Clean APIs with sensible defaults

These utilities integrate seamlessly with the existing Scanner111 architecture and follow C# best practices for async programming.