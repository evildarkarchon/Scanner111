using System.Collections.ObjectModel;
using System.Reactive;
using ReactiveUI;
using ReactiveUI.Fody.Helpers; // Required for [Reactive] attribute
using Scanner111.Common.Models.Configuration;
using Scanner111.Common.Services.Orchestration;
using Scanner111.Models;
using System; // Required for Func and Exception
using System.Threading.Tasks; // Required for Task and CancellationToken
using Scanner111.Views; // Required for SettingsWindow
using Microsoft.Extensions.DependencyInjection; // Required for Services.GetRequiredService
using Scanner111.Services; // Required for IDialogService

namespace Scanner111.ViewModels;

public class MainWindowViewModel : ViewModelBase
{
    private readonly IScanExecutor _scanExecutor;
    private readonly Func<SettingsViewModel> _settingsViewModelFactory;
    private readonly IDialogService _dialogService; // Injected service

    public ReactiveCommand<Unit, Unit> ScanCommand { get; }
    public ReactiveCommand<Unit, Unit> OpenSettingsCommand { get; }
    public ReactiveCommand<Unit, Unit> ExitCommand { get; }

    [Reactive] public bool FcxMode { get; set; }
    [Reactive] public bool ShowFormIds { get; set; }
    [Reactive] public string StatusText { get; set; } = "Ready";
    [Reactive] public double Progress { get; set; }
    [Reactive] public bool IsScanning { get; set; }

    public ObservableCollection<LogAnalysisResultDisplay> ScanResults { get; } = new();

    public MainWindowViewModel(
        IScanExecutor scanExecutor, 
        Func<SettingsViewModel> settingsViewModelFactory,
        IDialogService dialogService) // Inject IDialogService
    {
        _scanExecutor = scanExecutor;
        _settingsViewModelFactory = settingsViewModelFactory;
        _dialogService = dialogService;

        ScanCommand = ReactiveCommand.CreateFromTask(ExecuteScanAsync);
        OpenSettingsCommand = ReactiveCommand.CreateFromTask(OpenSettings); // Make it async
        ExitCommand = ReactiveCommand.Create(ExitApplication);
    }

    private async Task ExecuteScanAsync()
    {
        IsScanning = true;
        StatusText = "Scanning...";
        ScanResults.Clear();

        var config = new ScanConfig
        {
            FcxMode = FcxMode,
            ShowFormIdValues = ShowFormIds,
            // ScanPath should come from settings or user input
            // For now, hardcode or retrieve from a dummy config
            ScanPath = "C:\\Temp\\SampleLogs" // TODO: Get from settings
        };

        var progress = new Progress<ScanProgress>(p =>
        {
            Progress = (double)p.FilesProcessed / p.TotalFiles * 100;
            StatusText = $"Processing: {p.CurrentFile} ({p.FilesProcessed}/{p.TotalFiles})";
        });

        ScanResult? result = null;
        try
        {
            result = await _scanExecutor.ExecuteScanAsync(config, progress);
            foreach (var processedFile in result.ProcessedFiles)
            {
                ScanResults.Add(new LogAnalysisResultDisplay { FileName = processedFile, Status = "Completed" });
            }
            foreach (var failedLog in result.FailedLogs)
            {
                ScanResults.Add(new LogAnalysisResultDisplay { FileName = failedLog, Status = "Failed" });
            }
        }
        catch (Exception ex)
        {
            StatusText = $"Error: {ex.Message}";
            // Log the exception
        }
        finally
        {
            IsScanning = false;
            if (result != null)
            {
                StatusText = $"Complete - Scanned: {result.Statistics.Scanned}, Failed: {result.Statistics.Failed} in {result.ScanDuration.TotalSeconds:F2}s";
            }
        }
    }

    private async Task OpenSettings()
    {
        var settingsViewModel = _settingsViewModelFactory();
        await _dialogService.ShowSettingsDialogAsync(settingsViewModel);
        StatusText = "Settings dialog closed";
    }

    private void ExitApplication()
    {
        // Avalonia UI has its own way to exit, usually Application.Current.Shutdown()
        // For ViewModel, this would typically be handled by a service or directly in the View's code-behind.
        StatusText = "Exiting application (not yet implemented)";
    }
}
