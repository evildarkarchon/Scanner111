namespace Scanner111.Core.Infrastructure;

/// <summary>
///     Enhanced resource manager that provides dynamic concurrency control
///     based on system resources and adaptive limits
/// </summary>
public class EnhancedResourceManager : IDisposable
{
    private readonly SemaphoreSlim _concurrencyLimiter;
    private readonly object _lockObject = new();
    private readonly Timer _resourceMonitorTimer;
    private int _currentConcurrency;
    private bool _disposed;
    private DateTime _lastCpuCheck = DateTime.UtcNow;
    private TimeSpan _lastTotalProcessorTime;

    /// <summary>
    ///     Initialize the enhanced resource manager
    /// </summary>
    /// <param name="initialConcurrency">Initial concurrency limit (default: processor count)</param>
    /// <param name="maxConcurrency">Maximum concurrency limit (default: processor count * 2)</param>
    /// <param name="monitoringInterval">Resource monitoring interval (default: 5 seconds)</param>
    public EnhancedResourceManager(
        int? initialConcurrency = null,
        int? maxConcurrency = null,
        TimeSpan? monitoringInterval = null)
    {
        var processorCount = Environment.ProcessorCount;
        MaxConcurrencyLimit = maxConcurrency ?? Math.Max(processorCount * 2, 4);
        var initialLimit = initialConcurrency ?? processorCount;

        _concurrencyLimiter = new SemaphoreSlim(initialLimit, MaxConcurrencyLimit);
        _currentConcurrency = initialLimit;

        // Initialize CPU monitoring baseline
        try
        {
            var process = Process.GetCurrentProcess();
            _lastTotalProcessorTime = process.TotalProcessorTime;
        }
        catch (Exception)
        {
            // Ignore initialization errors
        }

        // Start resource monitoring timer
        var interval = monitoringInterval ?? TimeSpan.FromSeconds(5);
        _resourceMonitorTimer = new Timer(MonitorResources, null, interval, interval);
    }

    /// <summary>
    ///     Current concurrency limit
    /// </summary>
    public int CurrentConcurrencyLimit
    {
        get
        {
            lock (_lockObject)
            {
                return _currentConcurrency;
            }
        }
    }

    /// <summary>
    ///     Maximum allowed concurrency
    /// </summary>
    public int MaxConcurrencyLimit { get; }

    /// <summary>
    ///     Available semaphore slots
    /// </summary>
    public int AvailableSlots => _concurrencyLimiter.CurrentCount;

    public void Dispose()
    {
        if (_disposed) return;

        _disposed = true;
        _resourceMonitorTimer?.Dispose();
        _concurrencyLimiter?.Dispose();
    }

    /// <summary>
    ///     Acquire a resource slot asynchronously
    /// </summary>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Resource slot that should be disposed when done</returns>
    public async Task<IResourceSlot> AcquireSlotAsync(CancellationToken cancellationToken = default)
    {
        ThrowIfDisposed();

        await _concurrencyLimiter.WaitAsync(cancellationToken).ConfigureAwait(false);
        return new ResourceSlot(_concurrencyLimiter);
    }

    /// <summary>
    ///     Try to acquire a resource slot without waiting
    /// </summary>
    /// <returns>Resource slot if available, null otherwise</returns>
    public IResourceSlot? TryAcquireSlot()
    {
        ThrowIfDisposed();

        if (_concurrencyLimiter.Wait(0)) return new ResourceSlot(_concurrencyLimiter);

        return null;
    }

    /// <summary>
    ///     Execute an action with automatic resource management
    /// </summary>
    /// <param name="action">Action to execute</param>
    /// <param name="cancellationToken">Cancellation token</param>
    public async Task ExecuteAsync(Func<Task> action, CancellationToken cancellationToken = default)
    {
        using var slot = await AcquireSlotAsync(cancellationToken).ConfigureAwait(false);
        await action().ConfigureAwait(false);
    }

    /// <summary>
    ///     Execute a function with automatic resource management
    /// </summary>
    /// <typeparam name="T">Return type</typeparam>
    /// <param name="function">Function to execute</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Function result</returns>
    public async Task<T> ExecuteAsync<T>(Func<Task<T>> function, CancellationToken cancellationToken = default)
    {
        using var slot = await AcquireSlotAsync(cancellationToken).ConfigureAwait(false);
        return await function().ConfigureAwait(false);
    }

    /// <summary>
    ///     Get current system resource usage
    /// </summary>
    /// <returns>Resource usage information</returns>
    public SystemResourceUsage GetResourceUsage()
    {
        var cpuUsage = 0.0;
        var availableMemoryMB = 0.0;
        var workingSetMB = 0.0;

        try
        {
            var process = Process.GetCurrentProcess();
            workingSetMB = process.WorkingSet64 / (1024.0 * 1024.0);

            // Calculate CPU usage based on process time
            var currentTime = DateTime.UtcNow;
            var currentTotalProcessorTime = process.TotalProcessorTime;

            if (_lastCpuCheck != default)
            {
                var timeDiff = currentTime - _lastCpuCheck;
                var cpuDiff = currentTotalProcessorTime - _lastTotalProcessorTime;

                if (timeDiff.TotalMilliseconds > 0)
                    cpuUsage = cpuDiff.TotalMilliseconds / timeDiff.TotalMilliseconds * 100.0 /
                               Environment.ProcessorCount;
            }

            _lastCpuCheck = currentTime;
            _lastTotalProcessorTime = currentTotalProcessorTime;

            // Estimate available memory using GC information
            var totalMemory = GC.GetTotalMemory(false) / (1024.0 * 1024.0);
            availableMemoryMB = Math.Max(0, 1024 - totalMemory); // Rough estimate
        }
        catch (Exception)
        {
            // Ignore resource monitoring errors
        }

        return new SystemResourceUsage
        {
            CpuUsagePercent = Math.Max(0, Math.Min(100, cpuUsage)),
            AvailableMemoryMB = availableMemoryMB,
            ProcessorCount = Environment.ProcessorCount,
            WorkingSetMB = workingSetMB
        };
    }

    /// <summary>
    ///     Manually adjust the concurrency limit
    /// </summary>
    /// <param name="newLimit">New concurrency limit</param>
    public void AdjustConcurrencyLimit(int newLimit)
    {
        ThrowIfDisposed();

        newLimit = Math.Max(1, Math.Min(newLimit, MaxConcurrencyLimit));

        lock (_lockObject)
        {
            if (newLimit == _currentConcurrency) return;

            var difference = newLimit - _currentConcurrency;
            _currentConcurrency = newLimit;

            if (difference > 0)
                // Increase semaphore count
                _concurrencyLimiter.Release(difference);
            // Note: We cannot decrease semaphore count directly
            // The limit will be naturally reduced as slots are released
        }
    }

    private void MonitorResources(object? state)
    {
        if (_disposed) return;

        try
        {
            var usage = GetResourceUsage();
            var newLimit = CalculateOptimalConcurrency(usage);

            if (newLimit != _currentConcurrency) AdjustConcurrencyLimit(newLimit);
        }
        catch (Exception)
        {
            // Ignore monitoring errors to prevent crashes
        }
    }

    private int CalculateOptimalConcurrency(SystemResourceUsage usage)
    {
        var processorCount = Environment.ProcessorCount;

        // Base concurrency on processor count
        var baseConcurrency = processorCount;

        // Adjust based on CPU usage
        if (usage.CpuUsagePercent > 80)
            // High CPU usage - reduce concurrency
            baseConcurrency = Math.Max(1, baseConcurrency / 2);
        else if (usage.CpuUsagePercent < 40)
            // Low CPU usage - can increase concurrency
            baseConcurrency = Math.Min(MaxConcurrencyLimit, baseConcurrency * 2);

        // Adjust based on available memory (if available)
        if (usage.AvailableMemoryMB > 0 && usage.AvailableMemoryMB < 500)
            // Low memory - reduce concurrency
            baseConcurrency = Math.Max(1, baseConcurrency / 2);

        return Math.Max(1, Math.Min(baseConcurrency, MaxConcurrencyLimit));
    }

    private void ThrowIfDisposed()
    {
        if (_disposed) throw new ObjectDisposedException(nameof(EnhancedResourceManager));
    }
}

/// <summary>
///     Represents a resource slot that can be disposed
/// </summary>
public interface IResourceSlot : IDisposable
{
}

/// <summary>
///     Implementation of a resource slot
/// </summary>
internal class ResourceSlot : IResourceSlot
{
    private readonly SemaphoreSlim _semaphore;
    private bool _disposed;

    public ResourceSlot(SemaphoreSlim semaphore)
    {
        _semaphore = semaphore;
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        _semaphore.Release();
    }
}

/// <summary>
///     System resource usage information
/// </summary>
public class SystemResourceUsage
{
    /// <summary>
    ///     CPU usage percentage (0-100)
    /// </summary>
    public double CpuUsagePercent { get; init; }

    /// <summary>
    ///     Available memory in megabytes
    /// </summary>
    public double AvailableMemoryMB { get; init; }

    /// <summary>
    ///     Number of processor cores
    /// </summary>
    public int ProcessorCount { get; init; }

    /// <summary>
    ///     Current process working set in megabytes
    /// </summary>
    public double WorkingSetMB { get; init; }
}