using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows.Input;
using Avalonia.Threading;
using Scanner111.Core.Infrastructure;
using Scanner111.Core.Models;
using Scanner111.Core.Services;
using Scanner111.GUI.Services;

namespace Scanner111.GUI.ViewModels;

public class PapyrusMonitorViewModel : INotifyPropertyChanged, IDisposable
{
    private readonly IAudioNotificationService? _audioService;
    private readonly GuiMessageHandlerService _messageHandler;
    private readonly IPapyrusMonitorService _papyrusService;
    private readonly IApplicationSettingsService _settingsService;
    private readonly DispatcherTimer _uiUpdateTimer;
    private string _alertMessage = "";
    private bool _autoExport;

    private PapyrusStats? _currentStats;
    private bool _disposed;
    private string _exportPath = "";
    private bool _hasAlerts;
    private bool _isMonitoring;
    private string? _monitoredPath;
    private CancellationTokenSource? _monitoringCts;
    private int _monitoringInterval = 1000;
    private GameType _selectedGameType = GameType.Fallout4;
    private string _statusMessage = "Not monitoring";

    public PapyrusMonitorViewModel(
        IPapyrusMonitorService papyrusService,
        IApplicationSettingsService settingsService,
        GuiMessageHandlerService messageHandler,
        IAudioNotificationService? audioService = null)
    {
        _papyrusService = papyrusService;
        _settingsService = settingsService;
        _messageHandler = messageHandler;
        _audioService = audioService;

        History = new ObservableCollection<PapyrusStats>();
        RecentMessages = new ObservableCollection<string>();

        // Initialize commands
        StartMonitoringCommand = new RelayCommand(async () => await StartMonitoringAsync(), () => !IsMonitoring);
        StopMonitoringCommand = new RelayCommand(async () => await StopMonitoringAsync(), () => IsMonitoring);
        BrowseLogFileCommand = new RelayCommand(async () => await BrowseLogFileAsync());
        ExportStatsCommand = new RelayCommand(async () => await ExportStatsAsync(), () => History.Count > 0);
        ClearHistoryCommand = new RelayCommand(ClearHistory, () => History.Count > 0);
        RefreshCommand = new RelayCommand(async () => await RefreshAsync(), () => IsMonitoring);

        // Subscribe to events
        _papyrusService.StatsUpdated += OnStatsUpdated;
        _papyrusService.Error += OnError;

        // Set up UI update timer
        _uiUpdateTimer = new DispatcherTimer
        {
            Interval = TimeSpan.FromMilliseconds(100)
        };
        _uiUpdateTimer.Tick += (s, e) => UpdateUI();

        // Load settings
        _ = LoadSettingsAsync();
    }

    public ObservableCollection<PapyrusStats> History { get; }
    public ObservableCollection<string> RecentMessages { get; }

    public ICommand StartMonitoringCommand { get; }
    public ICommand StopMonitoringCommand { get; }
    public ICommand BrowseLogFileCommand { get; }
    public ICommand ExportStatsCommand { get; }
    public ICommand ClearHistoryCommand { get; }
    public ICommand RefreshCommand { get; }

    public PapyrusStats? CurrentStats
    {
        get => _currentStats;
        private set
        {
            if (_currentStats != value)
            {
                _currentStats = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(HasStats));
                OnPropertyChanged(nameof(DumpsCount));
                OnPropertyChanged(nameof(StacksCount));
                OnPropertyChanged(nameof(WarningsCount));
                OnPropertyChanged(nameof(ErrorsCount));
                OnPropertyChanged(nameof(Ratio));
                OnPropertyChanged(nameof(RatioDisplay));
                OnPropertyChanged(nameof(HasCriticalIssues));
            }
        }
    }

    public bool IsMonitoring
    {
        get => _isMonitoring;
        private set
        {
            if (_isMonitoring != value)
            {
                _isMonitoring = value;
                OnPropertyChanged();
                ((RelayCommand)StartMonitoringCommand).RaiseCanExecuteChanged();
                ((RelayCommand)StopMonitoringCommand).RaiseCanExecuteChanged();
                ((RelayCommand)RefreshCommand).RaiseCanExecuteChanged();
            }
        }
    }

    public string? MonitoredPath
    {
        get => _monitoredPath;
        set
        {
            if (_monitoredPath != value)
            {
                _monitoredPath = value;
                OnPropertyChanged();
            }
        }
    }

    public string StatusMessage
    {
        get => _statusMessage;
        private set
        {
            if (_statusMessage != value)
            {
                _statusMessage = value;
                OnPropertyChanged();
            }
        }
    }

    public bool HasAlerts
    {
        get => _hasAlerts;
        private set
        {
            if (_hasAlerts != value)
            {
                _hasAlerts = value;
                OnPropertyChanged();
            }
        }
    }

    public string AlertMessage
    {
        get => _alertMessage;
        private set
        {
            if (_alertMessage != value)
            {
                _alertMessage = value;
                OnPropertyChanged();
            }
        }
    }

    public GameType SelectedGameType
    {
        get => _selectedGameType;
        set
        {
            if (_selectedGameType != value)
            {
                _selectedGameType = value;
                OnPropertyChanged();
            }
        }
    }

    public int MonitoringInterval
    {
        get => _monitoringInterval;
        set
        {
            if (_monitoringInterval != value)
            {
                _monitoringInterval = value;
                _papyrusService.MonitoringInterval = value;
                OnPropertyChanged();
            }
        }
    }

    public bool AutoExport
    {
        get => _autoExport;
        set
        {
            if (_autoExport != value)
            {
                _autoExport = value;
                OnPropertyChanged();
            }
        }
    }

    public string ExportPath
    {
        get => _exportPath;
        set
        {
            if (_exportPath != value)
            {
                _exportPath = value;
                OnPropertyChanged();
            }
        }
    }

    // Computed properties for binding
    public bool HasStats => CurrentStats != null;
    public int DumpsCount => CurrentStats?.Dumps ?? 0;
    public int StacksCount => CurrentStats?.Stacks ?? 0;
    public int WarningsCount => CurrentStats?.Warnings ?? 0;
    public int ErrorsCount => CurrentStats?.Errors ?? 0;
    public double Ratio => CurrentStats?.Ratio ?? 0.0;
    public string RatioDisplay => Ratio.ToString("F3");
    public bool HasCriticalIssues => CurrentStats?.HasCriticalIssues ?? false;

    public void Dispose()
    {
        if (_disposed) return;

        _papyrusService.StatsUpdated -= OnStatsUpdated;
        _papyrusService.Error -= OnError;

        _uiUpdateTimer.Stop();
        _monitoringCts?.Cancel();
        _monitoringCts?.Dispose();
        _papyrusService?.Dispose();

        _disposed = true;
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    private async Task LoadSettingsAsync()
    {
        var settings = await _settingsService.LoadSettingsAsync();

        MonitoredPath = settings.PapyrusLogPath;
        MonitoringInterval = settings.PapyrusMonitorInterval;
        AutoExport = settings.PapyrusAutoExport;
        ExportPath = settings.PapyrusExportPath;
    }

    private async Task SaveSettingsAsync()
    {
        var settings = await _settingsService.LoadSettingsAsync();

        settings.PapyrusLogPath = MonitoredPath ?? "";
        settings.PapyrusMonitorInterval = MonitoringInterval;
        settings.PapyrusAutoExport = AutoExport;
        settings.PapyrusExportPath = ExportPath;

        await _settingsService.SaveSettingsAsync(settings);
    }

    private async Task StartMonitoringAsync()
    {
        try
        {
            _monitoringCts = new CancellationTokenSource();

            // Try to detect path if not set
            if (string.IsNullOrEmpty(MonitoredPath))
            {
                var detectedPath = await _papyrusService.DetectLogPathAsync(SelectedGameType);
                if (!string.IsNullOrEmpty(detectedPath))
                {
                    MonitoredPath = detectedPath;
                    _messageHandler.ShowInfo($"Auto-detected Papyrus log: {detectedPath}");
                }
                else
                {
                    _messageHandler.ShowError("Could not detect Papyrus log path. Please browse for the file.");
                    return;
                }
            }

            StatusMessage = "Starting monitoring...";
            _papyrusService.MonitoringInterval = MonitoringInterval;

            await _papyrusService.StartMonitoringAsync(MonitoredPath!, _monitoringCts.Token);

            IsMonitoring = true;
            StatusMessage = $"Monitoring: {MonitoredPath}";
            _uiUpdateTimer.Start();

            await SaveSettingsAsync();

            AddMessage($"Started monitoring: {MonitoredPath}");
        }
        catch (Exception ex)
        {
            _messageHandler.ShowError($"Failed to start monitoring: {ex.Message}");
            StatusMessage = "Failed to start monitoring";
            IsMonitoring = false;
        }
    }

    private async Task StopMonitoringAsync()
    {
        try
        {
            StatusMessage = "Stopping monitoring...";

            _monitoringCts?.Cancel();
            await _papyrusService.StopMonitoringAsync();

            IsMonitoring = false;
            StatusMessage = "Monitoring stopped";
            _uiUpdateTimer.Stop();

            AddMessage("Monitoring stopped");
        }
        catch (Exception ex)
        {
            _messageHandler.ShowError($"Failed to stop monitoring: {ex.Message}");
        }
    }

    private async Task BrowseLogFileAsync()
    {
        // File dialog not available in GuiMessageHandlerService
        // User needs to manually enter path or use auto-detection
        _messageHandler.ShowInfo("Please enter the Papyrus log path manually or use auto-detection.");
        await Task.CompletedTask;
    }

    private async Task ExportStatsAsync()
    {
        // For now, export to a default location
        var exportPath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments),
            $"papyrus_stats_{DateTime.Now:yyyyMMdd_HHmmss}.csv");

        try
        {
            await _papyrusService.ExportStatsAsync(exportPath);
            _messageHandler.ShowInfo($"Statistics exported to: {exportPath}");
        }
        catch (Exception ex)
        {
            _messageHandler.ShowError($"Failed to export statistics: {ex.Message}");
        }
    }

    private async Task RefreshAsync()
    {
        if (IsMonitoring && !string.IsNullOrEmpty(MonitoredPath))
            try
            {
                var stats = await _papyrusService.AnalyzeLogAsync(MonitoredPath);
                CurrentStats = stats;
                AddMessage("Manual refresh completed");
            }
            catch (Exception ex)
            {
                _messageHandler.ShowError($"Failed to refresh: {ex.Message}");
            }
    }

    private void ClearHistory()
    {
        History.Clear();
        _papyrusService.ClearHistory();
        AddMessage("History cleared");
        ((RelayCommand)ExportStatsCommand).RaiseCanExecuteChanged();
        ((RelayCommand)ClearHistoryCommand).RaiseCanExecuteChanged();
    }

    private void OnStatsUpdated(object? sender, PapyrusStatsUpdatedEventArgs e)
    {
        Dispatcher.UIThread.Post(() =>
        {
            CurrentStats = e.Stats;
            History.Add(e.Stats);

            // Keep history limited
            while (History.Count > 100) History.RemoveAt(0);

            // Check for alerts
            CheckAlerts(e.Stats);

            // Update messages
            if (e.DumpsDelta > 0 || e.ErrorsDelta > 0 || e.WarningsDelta > 0)
                AddMessage($"Stats updated - Dumps: {e.Stats.Dumps} (+{e.DumpsDelta}), " +
                           $"Errors: {e.Stats.Errors} (+{e.ErrorsDelta}), " +
                           $"Warnings: {e.Stats.Warnings} (+{e.WarningsDelta})");

            ((RelayCommand)ExportStatsCommand).RaiseCanExecuteChanged();
            ((RelayCommand)ClearHistoryCommand).RaiseCanExecuteChanged();
        });
    }

    private void OnError(object? sender, ErrorEventArgs e)
    {
        var errorMessage = e.GetException()?.Message ?? "Unknown error";
        Dispatcher.UIThread.Post(() =>
        {
            _messageHandler.ShowError($"Monitoring error: {errorMessage}");
            AddMessage($"Error: {errorMessage}");
        });
    }

    private void CheckAlerts(PapyrusStats stats)
    {
        var alerts = new List<string>();

        if (stats.Errors > 100)
            alerts.Add($"High error count: {stats.Errors}");

        if (stats.Warnings > 500)
            alerts.Add($"High warning count: {stats.Warnings}");

        if (stats.Ratio > 0.5)
            alerts.Add($"High dumps/stacks ratio: {stats.Ratio:F3}");

        if (stats.HasCriticalIssues)
        {
            alerts.Add("CRITICAL ISSUES DETECTED");
            _audioService?.PlayCriticalIssueAsync().ConfigureAwait(false);
        }

        HasAlerts = alerts.Count > 0;
        AlertMessage = string.Join(", ", alerts);
    }

    private void AddMessage(string message)
    {
        var timestamp = DateTime.Now.ToString("HH:mm:ss");
        RecentMessages.Insert(0, $"[{timestamp}] {message}");

        // Keep messages limited
        while (RecentMessages.Count > 50) RecentMessages.RemoveAt(RecentMessages.Count - 1);
    }

    private void UpdateUI()
    {
        // Force UI updates for real-time display
        OnPropertyChanged(nameof(CurrentStats));
        OnPropertyChanged(nameof(StatusMessage));
    }

    protected virtual void OnPropertyChanged([CallerMemberName] string? propertyName = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}

// Simple RelayCommand implementation
public class RelayCommand : ICommand
{
    private readonly Func<bool>? _canExecute;
    private readonly Action _execute;

    public RelayCommand(Action execute, Func<bool>? canExecute = null)
    {
        _execute = execute;
        _canExecute = canExecute;
    }

    public event EventHandler? CanExecuteChanged;

    public bool CanExecute(object? parameter)
    {
        return _canExecute?.Invoke() ?? true;
    }

    public void Execute(object? parameter)
    {
        _execute();
    }

    public void RaiseCanExecuteChanged()
    {
        CanExecuteChanged?.Invoke(this, EventArgs.Empty);
    }
}