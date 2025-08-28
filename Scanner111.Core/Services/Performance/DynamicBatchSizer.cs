using System.Diagnostics;
using Microsoft.Extensions.Logging;

namespace Scanner111.Core.Services.Performance;

/// <summary>
///     Simple adaptive batch sizing based on system resources and processing throughput.
///     Focuses on practical optimizations rather than complex algorithms.
/// </summary>
public sealed class DynamicBatchSizer
{
    private readonly ILogger<DynamicBatchSizer> _logger;
    private readonly int _baseBatchSize;
    private readonly int _maxBatchSize;
    private readonly int _minBatchSize;
    private int _currentBatchSize;
    private double _lastThroughput;
    private DateTime _lastMeasurement;

    public DynamicBatchSizer(
        ILogger<DynamicBatchSizer> logger,
        int baseBatchSize = 20,
        int minBatchSize = 5,
        int maxBatchSize = 100)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _baseBatchSize = Math.Max(1, baseBatchSize);
        _minBatchSize = Math.Max(1, minBatchSize);
        _maxBatchSize = Math.Max(_baseBatchSize, maxBatchSize);
        _currentBatchSize = _baseBatchSize;
        _lastMeasurement = DateTime.UtcNow;
    }

    /// <summary>
    ///     Gets the current recommended batch size.
    /// </summary>
    public int CurrentBatchSize => _currentBatchSize;

    /// <summary>
    ///     Updates batch size based on processing performance.
    /// </summary>
    /// <param name="itemsProcessed">Number of items processed in the last batch.</param>
    /// <param name="processingTime">Time taken to process the batch.</param>
    public void UpdateBatchSize(int itemsProcessed, TimeSpan processingTime)
    {
        if (itemsProcessed <= 0 || processingTime <= TimeSpan.Zero)
        {
            return;
        }

        var currentThroughput = itemsProcessed / processingTime.TotalSeconds;
        var now = DateTime.UtcNow;

        // Only adjust if we have a baseline and enough time has passed
        if (_lastThroughput > 0 && (now - _lastMeasurement).TotalSeconds >= 1.0)
        {
            var throughputChange = (currentThroughput - _lastThroughput) / _lastThroughput;

            // If throughput improved significantly, try increasing batch size
            if (throughputChange > 0.1 && _currentBatchSize < _maxBatchSize)
            {
                var newBatchSize = Math.Min(_maxBatchSize, (int)(_currentBatchSize * 1.2));
                _logger.LogDebug("Increasing batch size from {Old} to {New} (throughput improved by {Change:P2})",
                    _currentBatchSize, newBatchSize, throughputChange);
                _currentBatchSize = newBatchSize;
            }
            // If throughput decreased significantly, try decreasing batch size
            else if (throughputChange < -0.2 && _currentBatchSize > _minBatchSize)
            {
                var newBatchSize = Math.Max(_minBatchSize, (int)(_currentBatchSize * 0.8));
                _logger.LogDebug("Decreasing batch size from {Old} to {New} (throughput decreased by {Change:P2})",
                    _currentBatchSize, newBatchSize, throughputChange);
                _currentBatchSize = newBatchSize;
            }

            _lastMeasurement = now;
        }

        _lastThroughput = currentThroughput;
    }

    /// <summary>
    ///     Adjusts batch size based on available system memory.
    /// </summary>
    public void AdjustForMemoryPressure()
    {
        try
        {
            // Simple memory pressure check using GC
            var generation2Collections = GC.CollectionCount(2);
            
            // If we've had recent Gen 2 collections, reduce batch size
            GC.Collect(0, GCCollectionMode.Optimized);
            
            var memoryBefore = GC.GetTotalMemory(false);
            GC.Collect();
            var memoryAfter = GC.GetTotalMemory(true);
            
            var memoryFreed = memoryBefore - memoryAfter;
            
            // If significant memory was freed, we might be under pressure
            if (memoryFreed > 50 * 1024 * 1024) // 50MB
            {
                var newBatchSize = Math.Max(_minBatchSize, (int)(_currentBatchSize * 0.7));
                if (newBatchSize != _currentBatchSize)
                {
                    _logger.LogInformation("Reducing batch size due to memory pressure: {Old} -> {New}",
                        _currentBatchSize, newBatchSize);
                    _currentBatchSize = newBatchSize;
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to adjust batch size for memory pressure");
        }
    }

    /// <summary>
    ///     Resets batch size to the base value.
    /// </summary>
    public void Reset()
    {
        _currentBatchSize = _baseBatchSize;
        _lastThroughput = 0;
        _lastMeasurement = DateTime.UtcNow;
        _logger.LogDebug("Reset batch size to base value: {BatchSize}", _baseBatchSize);
    }
}