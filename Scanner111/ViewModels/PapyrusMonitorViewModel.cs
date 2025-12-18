using System;
using System.Reactive;
using Avalonia.Threading;
using ReactiveUI;
using ReactiveUI.Fody.Helpers;
using Scanner111.Common.Models.Papyrus;
using Scanner111.Common.Services.Papyrus;

namespace Scanner111.ViewModels;

/// <summary>
/// ViewModel for the Papyrus log monitoring dialog.
/// </summary>
public class PapyrusMonitorViewModel : ViewModelBase, IDisposable
{
    private readonly IPapyrusMonitorService _monitorService;
    private bool _disposed;

    /// <summary>
    /// Gets the number of stack dump events.
    /// </summary>
    [Reactive] public int Dumps { get; private set; }

    /// <summary>
    /// Gets the number of individual stack frames.
    /// </summary>
    [Reactive] public int Stacks { get; private set; }

    /// <summary>
    /// Gets the ratio of dumps to stacks.
    /// </summary>
    [Reactive] public double Ratio { get; private set; }

    /// <summary>
    /// Gets the number of warning messages.
    /// </summary>
    [Reactive] public int Warnings { get; private set; }

    /// <summary>
    /// Gets the number of error messages.
    /// </summary>
    [Reactive] public int Errors { get; private set; }

    /// <summary>
    /// Gets the last updated timestamp display string.
    /// </summary>
    [Reactive] public string LastUpdated { get; private set; } = string.Empty;

    /// <summary>
    /// Gets any error message from monitoring.
    /// </summary>
    [Reactive] public string ErrorMessage { get; private set; } = string.Empty;

    /// <summary>
    /// Gets whether an error has occurred.
    /// </summary>
    [Reactive] public bool HasError { get; private set; }

    /// <summary>
    /// Command to stop monitoring.
    /// </summary>
    public ReactiveCommand<Unit, Unit> StopCommand { get; }

    /// <summary>
    /// Event raised when the user requests to stop monitoring.
    /// </summary>
    public event EventHandler? StopRequested;

    /// <summary>
    /// Initializes a new instance of the <see cref="PapyrusMonitorViewModel"/> class.
    /// </summary>
    /// <param name="monitorService">The Papyrus monitor service.</param>
    public PapyrusMonitorViewModel(IPapyrusMonitorService monitorService)
    {
        _monitorService = monitorService ?? throw new ArgumentNullException(nameof(monitorService));

        StopCommand = ReactiveCommand.Create(OnStopRequested);

        // Subscribe to stats updates - marshal to UI thread
        _monitorService.StatsUpdated += OnStatsUpdated;
        _monitorService.ErrorOccurred += OnErrorOccurred;
    }

    /// <summary>
    /// Starts monitoring the specified log file.
    /// </summary>
    /// <param name="logPath">The path to the Papyrus log file.</param>
    public void StartMonitoring(string logPath)
    {
        _monitorService.StartMonitoring(logPath);
    }

    private void OnStatsUpdated(PapyrusStats stats)
    {
        // Marshal to UI thread
        Dispatcher.UIThread.Post(() =>
        {
            Dumps = stats.Dumps;
            Stacks = stats.Stacks;
            Ratio = stats.Ratio;
            Warnings = stats.Warnings;
            Errors = stats.Errors;
            LastUpdated = $"Last Updated: {stats.Timestamp:HH:mm:ss}";
            HasError = false;
            ErrorMessage = string.Empty;
        });
    }

    private void OnErrorOccurred(string error)
    {
        // Marshal to UI thread
        Dispatcher.UIThread.Post(() =>
        {
            HasError = true;
            ErrorMessage = error;
        });
    }

    private void OnStopRequested()
    {
        _monitorService.StopMonitoring();
        StopRequested?.Invoke(this, EventArgs.Empty);
    }

    /// <summary>
    /// Disposes of resources used by this ViewModel.
    /// </summary>
    public void Dispose()
    {
        if (_disposed) return;

        _monitorService.StatsUpdated -= OnStatsUpdated;
        _monitorService.ErrorOccurred -= OnErrorOccurred;
        _monitorService.Dispose();
        _disposed = true;
    }
}
