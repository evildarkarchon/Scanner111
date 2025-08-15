using System.Collections.Concurrent;
using System.Text.Json;
using Scanner111.Core.Infrastructure;
using Scanner111.Core.Models;

namespace Scanner111.Core.Services;

/// <summary>
///     Service for monitoring and analyzing Papyrus log files
/// </summary>
public class PapyrusMonitorService : IPapyrusMonitorService
{
    private readonly ConcurrentBag<PapyrusStats> _historicalStats = new();
    private readonly SemaphoreSlim _monitoringSemaphore = new(1, 1);
    private readonly IApplicationSettingsService _settingsService;
    private readonly IYamlSettingsProvider _yamlSettings;
    private bool _disposed;
    private long _lastFilePosition;
    private CancellationTokenSource? _monitoringCts;
    private Timer? _pollingTimer;

    private FileSystemWatcher? _watcher;

    public PapyrusMonitorService(
        IApplicationSettingsService settingsService,
        IYamlSettingsProvider yamlSettings)
    {
        _settingsService = settingsService;
        _yamlSettings = yamlSettings;
        MonitoringInterval = 1000; // Default 1 second
    }

    public event EventHandler<PapyrusStatsUpdatedEventArgs>? StatsUpdated;
    public event EventHandler<ErrorEventArgs>? Error;

    public bool IsMonitoring { get; private set; }
    public PapyrusStats? CurrentStats { get; private set; }

    public string? MonitoredPath { get; private set; }
    public int MonitoringInterval { get; set; }

    public async Task StartMonitoringAsync(string logPath, CancellationToken cancellationToken = default)
    {
        if (IsMonitoring) await StopMonitoringAsync().ConfigureAwait(false);

        await _monitoringSemaphore.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            if (!File.Exists(logPath)) throw new FileNotFoundException($"Papyrus log file not found: {logPath}");

            MonitoredPath = logPath;
            _monitoringCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            _lastFilePosition = 0;

            // Initial analysis
            CurrentStats = await AnalyzeLogInternalAsync(logPath, cancellationToken).ConfigureAwait(false);
            _historicalStats.Add(CurrentStats);
            OnStatsUpdated(new PapyrusStatsUpdatedEventArgs(CurrentStats));

            // Set up file watcher for immediate notifications
            SetupFileWatcher(logPath);

            // Set up polling timer for periodic checks
            _pollingTimer = new Timer(
                async _ => await CheckForUpdatesAsync().ConfigureAwait(false),
                null,
                TimeSpan.FromMilliseconds(MonitoringInterval),
                TimeSpan.FromMilliseconds(MonitoringInterval));

            IsMonitoring = true;
        }
        finally
        {
            _monitoringSemaphore.Release();
        }
    }

    public async Task StartMonitoringAsync(GameType gameType, CancellationToken cancellationToken = default)
    {
        var logPath = await DetectLogPathAsync(gameType).ConfigureAwait(false);
        if (string.IsNullOrEmpty(logPath))
            throw new FileNotFoundException($"Could not detect Papyrus log path for {gameType}");

        await StartMonitoringAsync(logPath, cancellationToken).ConfigureAwait(false);
    }

    public async Task StopMonitoringAsync()
    {
        await _monitoringSemaphore.WaitAsync().ConfigureAwait(false);
        try
        {
            IsMonitoring = false;

            _pollingTimer?.Dispose();
            _pollingTimer = null;

            _watcher?.Dispose();
            _watcher = null;

            _monitoringCts?.Cancel();
            _monitoringCts?.Dispose();
            _monitoringCts = null;

            MonitoredPath = null;
        }
        finally
        {
            _monitoringSemaphore.Release();
        }
    }

    public async Task<PapyrusStats> AnalyzeLogAsync(string logPath, CancellationToken cancellationToken = default)
    {
        if (!File.Exists(logPath)) throw new FileNotFoundException($"Papyrus log file not found: {logPath}");

        var stats = await AnalyzeLogInternalAsync(logPath, cancellationToken).ConfigureAwait(false);
        _historicalStats.Add(stats);
        return stats;
    }

    public IReadOnlyList<PapyrusStats> GetHistoricalStats()
    {
        return _historicalStats.OrderBy(s => s.Timestamp).ToList();
    }

    public void ClearHistory()
    {
        _historicalStats.Clear();
    }

    public async Task<string?> DetectLogPathAsync(GameType gameType)
    {
        var settings = await _settingsService.LoadSettingsAsync().ConfigureAwait(false);

        // First check if we have a configured path
        if (settings != null && !string.IsNullOrEmpty(settings.PapyrusLogPath) && File.Exists(settings.PapyrusLogPath))
            return settings.PapyrusLogPath;

        // Try to detect based on game type
        var possiblePaths = GetPossibleLogPaths(gameType);

        foreach (var path in possiblePaths)
            if (File.Exists(path))
                return path;

        return null;
    }

    public async Task ExportStatsAsync(string filePath, string format = "csv",
        CancellationToken cancellationToken = default)
    {
        var stats = GetHistoricalStats();

        if (format.Equals("csv", StringComparison.OrdinalIgnoreCase))
            await ExportToCsvAsync(filePath, stats, cancellationToken).ConfigureAwait(false);
        else if (format.Equals("json", StringComparison.OrdinalIgnoreCase))
            await ExportToJsonAsync(filePath, stats, cancellationToken).ConfigureAwait(false);
        else
            throw new ArgumentException($"Unsupported export format: {format}");
    }

    public void Dispose()
    {
        if (_disposed) return;

        StopMonitoringAsync().GetAwaiter().GetResult();
        _monitoringSemaphore?.Dispose();
        _disposed = true;
    }

    private async Task<PapyrusStats> AnalyzeLogInternalAsync(string logPath, CancellationToken cancellationToken)
    {
        var encoding = await DetectEncodingAsync(logPath).ConfigureAwait(false);

        int dumps = 0, stacks = 0, warnings = 0, errors = 0;

        using var fileStream = new FileStream(logPath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
        using var reader = new StreamReader(fileStream, encoding);

        string? line;
        while ((line = await reader.ReadLineAsync(cancellationToken).ConfigureAwait(false)) != null)
        {
            var lowerLine = line.ToLowerInvariant();

            if (lowerLine.Contains("dumping stack"))
            {
                // Check if it's plural (multiple stacks) or singular
                if (lowerLine.Contains("dumping stacks"))
                    dumps++;
                else
                    dumps++;
            }

            if (lowerLine.Contains("stack:")) stacks++;

            if (lowerLine.Contains("warning:")) warnings++;

            if (lowerLine.Contains("error:")) errors++;
        }

        _lastFilePosition = fileStream.Position;

        var ratio = stacks == 0 ? 0.0 : (double)dumps / stacks;

        return new PapyrusStats
        {
            Timestamp = DateTime.Now,
            Dumps = dumps,
            Stacks = stacks,
            Warnings = warnings,
            Errors = errors,
            Ratio = ratio,
            LogPath = logPath
        };
    }

    private async Task CheckForUpdatesAsync()
    {
        if (!IsMonitoring || string.IsNullOrEmpty(MonitoredPath) ||
            _monitoringCts?.Token.IsCancellationRequested == true)
            return;

        try
        {
            var fileInfo = new FileInfo(MonitoredPath);
            if (!fileInfo.Exists) return;

            // Check if file has grown
            if (fileInfo.Length > _lastFilePosition)
            {
                var newStats = await AnalyzeLogInternalAsync(MonitoredPath, _monitoringCts.Token).ConfigureAwait(false);

                // Only emit if stats have changed
                if (newStats != CurrentStats)
                {
                    var previousStats = CurrentStats;
                    CurrentStats = newStats;
                    _historicalStats.Add(newStats);

                    OnStatsUpdated(new PapyrusStatsUpdatedEventArgs(newStats, previousStats));
                }
            }
        }
        catch (Exception ex)
        {
            OnError(new ErrorEventArgs(ex));
        }
    }

    private void SetupFileWatcher(string logPath)
    {
        var directory = Path.GetDirectoryName(logPath);
        var fileName = Path.GetFileName(logPath);

        if (string.IsNullOrEmpty(directory) || string.IsNullOrEmpty(fileName))
            return;

        _watcher = new FileSystemWatcher(directory, fileName)
        {
            NotifyFilter = NotifyFilters.LastWrite | NotifyFilters.Size,
            EnableRaisingEvents = true
        };

        _watcher.Changed += async (sender, e) => await CheckForUpdatesAsync().ConfigureAwait(false);
        _watcher.Error += (sender, e) => OnError(e);
    }

    private List<string> GetPossibleLogPaths(GameType gameType)
    {
        var paths = new List<string>();
        var documentsPath = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);

        switch (gameType)
        {
            case GameType.Fallout4:
                paths.Add(Path.Combine(documentsPath, "My Games", "Fallout4", "Logs", "Script", "Papyrus.0.log"));
                paths.Add(Path.Combine(documentsPath, "My Games", "Fallout4VR", "Logs", "Script", "Papyrus.0.log"));
                break;
            case GameType.Skyrim:
                paths.Add(Path.Combine(documentsPath, "My Games", "Skyrim Special Edition", "Logs", "Script",
                    "Papyrus.0.log"));
                paths.Add(Path.Combine(documentsPath, "My Games", "SkyrimVR", "Logs", "Script", "Papyrus.0.log"));
                paths.Add(Path.Combine(documentsPath, "My Games", "Skyrim", "Logs", "Script", "Papyrus.0.log"));
                break;
        }

        return paths;
    }

    private async Task<Encoding> DetectEncodingAsync(string filePath)
    {
        // Simple encoding detection - can be enhanced with a library like CharsetDetector
        var buffer = new byte[4096];
        using var fileStream = new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
        var bytesRead = await fileStream.ReadAsync(buffer, 0, buffer.Length).ConfigureAwait(false);

        // Check for BOM
        if (bytesRead >= 3 && buffer[0] == 0xEF && buffer[1] == 0xBB && buffer[2] == 0xBF)
            return Encoding.UTF8;
        if (bytesRead >= 2 && buffer[0] == 0xFF && buffer[1] == 0xFE)
            return Encoding.Unicode;
        if (bytesRead >= 2 && buffer[0] == 0xFE && buffer[1] == 0xFF)
            return Encoding.BigEndianUnicode;

        // Default to UTF8
        return Encoding.UTF8;
    }

    private async Task ExportToCsvAsync(string filePath, IReadOnlyList<PapyrusStats> stats,
        CancellationToken cancellationToken)
    {
        var csv = new StringBuilder();
        csv.AppendLine("Timestamp,Dumps,Stacks,Ratio,Warnings,Errors,TotalIssues");

        foreach (var stat in stats)
            csv.AppendLine(
                $"{stat.Timestamp:yyyy-MM-dd HH:mm:ss},{stat.Dumps},{stat.Stacks},{stat.Ratio:F3},{stat.Warnings},{stat.Errors},{stat.TotalIssues}");

        await File.WriteAllTextAsync(filePath, csv.ToString(), cancellationToken).ConfigureAwait(false);
    }

    private async Task ExportToJsonAsync(string filePath, IReadOnlyList<PapyrusStats> stats,
        CancellationToken cancellationToken)
    {
        var json = JsonSerializer.Serialize(stats, new JsonSerializerOptions
        {
            WriteIndented = true
        });

        await File.WriteAllTextAsync(filePath, json, cancellationToken).ConfigureAwait(false);
    }

    private void OnStatsUpdated(PapyrusStatsUpdatedEventArgs e)
    {
        StatsUpdated?.Invoke(this, e);
    }

    private void OnError(ErrorEventArgs e)
    {
        Error?.Invoke(this, e);
    }
}