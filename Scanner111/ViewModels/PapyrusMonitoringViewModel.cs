using System;
using System.Reactive;
using System.Reactive.Linq;
using System.Threading;
using System.Threading.Tasks;
using Avalonia.Threading;
using ReactiveUI;
using Scanner111.Models;
using Scanner111.Services;

namespace Scanner111.ViewModels
{
    public class PapyrusMonitoringViewModel : ViewModelBase, IDisposable
    {
        private readonly IPapyrusLogMonitoringService _monitoringService;
        private readonly CancellationTokenSource _monitoringCts = new();
        private bool _isMonitoring;
        private PapyrusLogAnalysis? _currentAnalysis;
        private string _monitoringStatus = "Not Monitoring";

        /// <summary>
        /// Command to start or stop the monitoring
        /// </summary>
        public ReactiveCommand<Unit, Unit> ToggleMonitoringCommand { get; }
        /// <summary>
        /// Command to manually analyze the log file once
        /// </summary>
        public ReactiveCommand<Unit, PapyrusLogAnalysis> AnalyzeLogCommand { get; }

        /// <summary>
        /// Current state of the monitoring (true if actively monitoring)
        /// </summary>
        public bool IsMonitoring
        {
            get => _isMonitoring;
            set => this.RaiseAndSetIfChanged(ref _isMonitoring, value);
        }

        /// <summary>
        /// Current analysis results from the log file
        /// </summary>
        public PapyrusLogAnalysis? CurrentAnalysis
        {
            get => _currentAnalysis;
            private set => this.RaiseAndSetIfChanged(ref _currentAnalysis, value);
        }

        /// <summary>
        /// Status message about the monitoring system
        /// </summary>
        public string MonitoringStatus
        {
            get => _monitoringStatus;
            private set => this.RaiseAndSetIfChanged(ref _monitoringStatus, value);
        }

        public PapyrusMonitoringViewModel(IPapyrusLogMonitoringService monitoringService)
        {
            _monitoringService = monitoringService ?? throw new ArgumentNullException(nameof(monitoringService));

            // Setup commands
            ToggleMonitoringCommand = ReactiveCommand.CreateFromTask(ToggleMonitoringAsync);
            // Setup analysis command
            var canAnalyze = this.WhenAnyValue(x => x.IsMonitoring)
                .Select(isMonitoring => !isMonitoring);  // Only allow manual analysis when not monitoring

            AnalyzeLogCommand = ReactiveCommand.CreateFromTask(
                async () => await _monitoringService.AnalyzePapyrusLogAsync(),
                canAnalyze);

            // When analysis command executes, update the current analysis
            AnalyzeLogCommand.Subscribe(result => CurrentAnalysis = result);
        }

        /// <summary>
        /// Toggles the log monitoring state
        /// </summary>
        private async Task ToggleMonitoringAsync()
        {
            if (IsMonitoring)
            {
                StopMonitoring();
            }
            else
            {
                await StartMonitoringAsync();
            }
        }

        /// <summary>
        /// Starts monitoring the Papyrus log file for changes
        /// </summary>
        public async Task StartMonitoringAsync()
        {
            if (IsMonitoring) return;

            MonitoringStatus = "Starting monitoring...";

            try
            {
                // Start the monitoring, passing a callback that will update the UI
                await _monitoringService.StartMonitoringAsync(UpdateFromAnalysis, _monitoringCts.Token);
                IsMonitoring = true;
                MonitoringStatus = "Monitoring active";
            }
            catch (Exception ex)
            {
                MonitoringStatus = $"Failed to start monitoring: {ex.Message}";
                IsMonitoring = false;
            }
        }

        /// <summary>
        /// Callback method to handle log file updates during monitoring
        /// </summary>
        private void UpdateFromAnalysis(PapyrusLogAnalysis analysis)
        {
            // Use Avalonia Dispatcher to safely update the UI properties from a background thread
            Dispatcher.UIThread.Post(() =>
            {
                CurrentAnalysis = analysis;

                // Update the status message based on the counts
                if (analysis.DumpCount > 0)
                {
                    MonitoringStatus = $"Monitoring active - {analysis.DumpCount} dumps found";
                }
                else
                {
                    MonitoringStatus = "Monitoring active - no issues found";
                }
            });
        }

        /// <summary>
        /// Stops the Papyrus log monitoring
        /// </summary>
        public void StopMonitoring()
        {
            if (!IsMonitoring) return;

            _monitoringService.StopMonitoring();
            IsMonitoring = false;
            MonitoringStatus = "Monitoring stopped";
        }

        /// <summary>
        /// Clean up resources on disposal
        /// </summary>
        public void Dispose()
        {
            StopMonitoring();
            _monitoringCts.Cancel();
            _monitoringCts.Dispose();
        }
    }
}
