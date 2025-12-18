using Scanner111.Common.Models.Papyrus;

namespace Scanner111.Common.Services.Papyrus;

/// <summary>
/// Background service for monitoring Papyrus log files.
/// </summary>
/// <remarks>
/// Uses a background task with periodic polling to read the log file.
/// Only tracks new entries written after monitoring starts.
/// Emits updates only when statistics change to avoid unnecessary notifications.
/// </remarks>
public sealed class PapyrusMonitorService : IPapyrusMonitorService
{
    private readonly IPapyrusLogReader _logReader;

    private CancellationTokenSource? _cts;
    private Task? _monitorTask;
    private PapyrusStats? _lastStats;
    private bool _disposed;

    /// <summary>
    /// Initializes a new instance of the <see cref="PapyrusMonitorService"/> class.
    /// </summary>
    /// <param name="logReader">The log reader service.</param>
    public PapyrusMonitorService(IPapyrusLogReader logReader)
    {
        _logReader = logReader ?? throw new ArgumentNullException(nameof(logReader));
    }

    /// <inheritdoc/>
    public event Action<PapyrusStats>? StatsUpdated;

    /// <inheritdoc/>
    public event Action<string>? ErrorOccurred;

    /// <inheritdoc/>
    public bool IsMonitoring => _monitorTask is { IsCompleted: false };

    /// <inheritdoc/>
    public void StartMonitoring(string logPath, int pollIntervalMs = 1000)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        if (IsMonitoring)
        {
            return; // Already monitoring
        }

        _cts = new CancellationTokenSource();
        _monitorTask = MonitorLoopAsync(logPath, pollIntervalMs, _cts.Token);
    }

    /// <inheritdoc/>
    public void StopMonitoring()
    {
        if (_cts is not null)
        {
            _cts.Cancel();
            _cts.Dispose();
            _cts = null;
        }

        _monitorTask = null;
        _lastStats = null;
    }

    private async Task MonitorLoopAsync(string logPath, int pollIntervalMs, CancellationToken cancellationToken)
    {
        long currentPosition;
        PapyrusStats currentStats;

        try
        {
            // Record initial file position - we only track new entries
            currentPosition = _logReader.GetFileEndPosition(logPath);
            currentStats = PapyrusStats.Empty;

            // Emit initial empty stats
            _lastStats = currentStats;
            StatsUpdated?.Invoke(currentStats);
        }
        catch (FileNotFoundException)
        {
            ErrorOccurred?.Invoke("Papyrus log file not found. Logging may be disabled or the game hasn't been run yet.");
            return;
        }
        catch (Exception ex)
        {
            ErrorOccurred?.Invoke($"Error initializing Papyrus monitor: {ex.Message}");
            return;
        }

        using var timer = new PeriodicTimer(TimeSpan.FromMilliseconds(pollIntervalMs));

        try
        {
            while (await timer.WaitForNextTickAsync(cancellationToken).ConfigureAwait(false))
            {
                await ReadAndEmitAsync(logPath, currentPosition, currentStats, cancellationToken)
                    .ContinueWith(task =>
                    {
                        if (task.IsCompletedSuccessfully)
                        {
                            (currentStats, currentPosition) = task.Result;
                        }
                    }, cancellationToken)
                    .ConfigureAwait(false);
            }
        }
        catch (OperationCanceledException)
        {
            // Expected when stopping - do nothing
        }
    }

    private async Task<(PapyrusStats stats, long position)> ReadAndEmitAsync(
        string logPath,
        long position,
        PapyrusStats currentStats,
        CancellationToken cancellationToken)
    {
        try
        {
            var result = await _logReader.ReadNewContentAsync(logPath, position, currentStats, cancellationToken)
                .ConfigureAwait(false);

            // Only emit if stats changed
            if (!result.Stats.Equals(_lastStats))
            {
                _lastStats = result.Stats;
                StatsUpdated?.Invoke(result.Stats);
            }

            return (result.Stats, result.NewPosition);
        }
        catch (OperationCanceledException)
        {
            // Expected when stopping
            return (currentStats, position);
        }
        catch (FileNotFoundException)
        {
            ErrorOccurred?.Invoke("Papyrus log file not found. It may have been deleted.");
            return (currentStats, position);
        }
        catch (Exception ex)
        {
            ErrorOccurred?.Invoke($"Error reading Papyrus log: {ex.Message}");
            return (currentStats, position);
        }
    }

    /// <inheritdoc/>
    public void Dispose()
    {
        if (_disposed) return;

        StopMonitoring();
        _disposed = true;
    }
}
