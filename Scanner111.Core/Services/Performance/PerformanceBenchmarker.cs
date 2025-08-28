using System.Diagnostics;
using Microsoft.Extensions.Logging;

namespace Scanner111.Core.Services.Performance;

/// <summary>
///     Simple performance benchmarking service for tracking operation performance.
///     Focuses on practical metrics rather than complex profiling.
/// </summary>
public sealed class PerformanceBenchmarker : IDisposable
{
    private readonly ILogger<PerformanceBenchmarker> _logger;
    private readonly Dictionary<string, OperationMetrics> _metrics;
    private readonly object _metricsLock;
    private readonly Timer? _reportingTimer;
    private bool _disposed;

    public PerformanceBenchmarker(ILogger<PerformanceBenchmarker> logger, bool enablePeriodicReporting = true)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _metrics = new Dictionary<string, OperationMetrics>();
        _metricsLock = new object();

        if (enablePeriodicReporting)
        {
            _reportingTimer = new Timer(ReportMetrics, null, 
                TimeSpan.FromMinutes(5), 
                TimeSpan.FromMinutes(5));
        }
    }

    /// <summary>
    ///     Times an operation and records the metrics.
    /// </summary>
    /// <param name="operationName">Name of the operation being timed.</param>
    /// <param name="operation">The operation to time.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The result of the operation.</returns>
    public async Task<T> TimeOperationAsync<T>(
        string operationName, 
        Func<CancellationToken, Task<T>> operation,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(operationName);
        ArgumentNullException.ThrowIfNull(operation);

        var stopwatch = Stopwatch.StartNew();
        var initialMemory = GC.GetTotalMemory(false);

        try
        {
            var result = await operation(cancellationToken).ConfigureAwait(false);
            stopwatch.Stop();
            
            var finalMemory = GC.GetTotalMemory(false);
            var memoryDelta = finalMemory - initialMemory;

            RecordMetrics(operationName, stopwatch.Elapsed, true, memoryDelta);
            
            return result;
        }
        catch (Exception ex)
        {
            stopwatch.Stop();
            RecordMetrics(operationName, stopwatch.Elapsed, false, 0);
            _logger.LogError(ex, "Operation {Operation} failed after {Duration}ms", 
                operationName, stopwatch.ElapsedMilliseconds);
            throw;
        }
    }

    /// <summary>
    ///     Times a synchronous operation and records the metrics.
    /// </summary>
    public T TimeOperation<T>(string operationName, Func<T> operation)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(operationName);
        ArgumentNullException.ThrowIfNull(operation);

        var stopwatch = Stopwatch.StartNew();
        var initialMemory = GC.GetTotalMemory(false);

        try
        {
            var result = operation();
            stopwatch.Stop();
            
            var finalMemory = GC.GetTotalMemory(false);
            var memoryDelta = finalMemory - initialMemory;

            RecordMetrics(operationName, stopwatch.Elapsed, true, memoryDelta);
            
            return result;
        }
        catch (Exception ex)
        {
            stopwatch.Stop();
            RecordMetrics(operationName, stopwatch.Elapsed, false, 0);
            _logger.LogError(ex, "Operation {Operation} failed after {Duration}ms", 
                operationName, stopwatch.ElapsedMilliseconds);
            throw;
        }
    }

    /// <summary>
    ///     Records metrics for an operation.
    /// </summary>
    private void RecordMetrics(string operationName, TimeSpan duration, bool succeeded, long memoryDelta)
    {
        lock (_metricsLock)
        {
            if (!_metrics.TryGetValue(operationName, out var metrics))
            {
                metrics = new OperationMetrics(operationName);
                _metrics[operationName] = metrics;
            }

            metrics.RecordExecution(duration, succeeded, memoryDelta);
        }
    }

    /// <summary>
    ///     Gets performance metrics for all operations.
    /// </summary>
    public IReadOnlyDictionary<string, OperationSummary> GetMetrics()
    {
        lock (_metricsLock)
        {
            return _metrics.ToDictionary(
                kvp => kvp.Key,
                kvp => kvp.Value.ToSummary());
        }
    }

    /// <summary>
    ///     Gets performance metrics for a specific operation.
    /// </summary>
    public OperationSummary? GetMetrics(string operationName)
    {
        lock (_metricsLock)
        {
            return _metrics.TryGetValue(operationName, out var metrics) 
                ? metrics.ToSummary() 
                : null;
        }
    }

    /// <summary>
    ///     Clears all recorded metrics.
    /// </summary>
    public void ClearMetrics()
    {
        lock (_metricsLock)
        {
            _metrics.Clear();
        }
        _logger.LogInformation("Performance metrics cleared");
    }

    /// <summary>
    ///     Reports current metrics to the logger.
    /// </summary>
    private void ReportMetrics(object? state)
    {
        var metrics = GetMetrics();
        
        if (metrics.Count == 0)
        {
            return;
        }

        _logger.LogInformation("=== Performance Metrics Report ===");
        
        foreach (var (operationName, summary) in metrics.OrderBy(kvp => kvp.Key))
        {
            _logger.LogInformation(
                "{Operation}: {Count} executions, Avg: {AvgMs}ms, Min: {MinMs}ms, Max: {MaxMs}ms, Success: {SuccessRate:P2}",
                operationName,
                summary.ExecutionCount,
                summary.AverageExecutionTime.TotalMilliseconds,
                summary.MinExecutionTime.TotalMilliseconds,
                summary.MaxExecutionTime.TotalMilliseconds,
                summary.SuccessRate);

            if (summary.AverageMemoryDelta != 0)
            {
                _logger.LogInformation(
                    "{Operation}: Avg Memory Delta: {MemoryMB:F2} MB",
                    operationName,
                    summary.AverageMemoryDelta / (1024.0 * 1024.0));
            }
        }
    }

    public void Dispose()
    {
        if (_disposed)
            return;

        _reportingTimer?.Dispose();
        _disposed = true;
    }
}

/// <summary>
///     Internal metrics tracking for an operation.
/// </summary>
internal sealed class OperationMetrics
{
    private readonly List<TimeSpan> _executionTimes;
    private readonly List<long> _memoryDeltas;
    private int _successCount;
    private int _failureCount;

    public string OperationName { get; }

    public OperationMetrics(string operationName)
    {
        OperationName = operationName;
        _executionTimes = new List<TimeSpan>();
        _memoryDeltas = new List<long>();
    }

    public void RecordExecution(TimeSpan duration, bool succeeded, long memoryDelta)
    {
        _executionTimes.Add(duration);
        _memoryDeltas.Add(memoryDelta);

        if (succeeded)
            _successCount++;
        else
            _failureCount++;
    }

    public OperationSummary ToSummary()
    {
        if (_executionTimes.Count == 0)
        {
            return new OperationSummary
            {
                OperationName = OperationName,
                ExecutionCount = 0,
                SuccessRate = 0,
                AverageExecutionTime = TimeSpan.Zero,
                MinExecutionTime = TimeSpan.Zero,
                MaxExecutionTime = TimeSpan.Zero,
                AverageMemoryDelta = 0
            };
        }

        return new OperationSummary
        {
            OperationName = OperationName,
            ExecutionCount = _executionTimes.Count,
            SuccessRate = _successCount + _failureCount > 0 
                ? (double)_successCount / (_successCount + _failureCount) 
                : 0,
            AverageExecutionTime = TimeSpan.FromTicks((long)_executionTimes.Average(t => t.Ticks)),
            MinExecutionTime = _executionTimes.Min(),
            MaxExecutionTime = _executionTimes.Max(),
            AverageMemoryDelta = _memoryDeltas.Count > 0 ? _memoryDeltas.Average() : 0
        };
    }
}

/// <summary>
///     Summary of performance metrics for an operation.
/// </summary>
public sealed class OperationSummary
{
    public string OperationName { get; init; } = string.Empty;
    public int ExecutionCount { get; init; }
    public double SuccessRate { get; init; }
    public TimeSpan AverageExecutionTime { get; init; }
    public TimeSpan MinExecutionTime { get; init; }
    public TimeSpan MaxExecutionTime { get; init; }
    public double AverageMemoryDelta { get; init; }
}