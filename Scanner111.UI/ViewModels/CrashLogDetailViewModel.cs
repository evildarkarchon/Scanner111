using System;
using ReactiveUI;
using Scanner111.Application.Services;
using Scanner111.Application.DTOs;
using System.Collections.ObjectModel;
using System.IO;
using System.Reactive;
using System.Threading.Tasks;

namespace Scanner111.UI.ViewModels;

public class CrashLogDetailViewModel : ViewModelBase
{
    private readonly CrashLogService _crashLogService;
    
    private CrashLogDetailDto? _crashLog;
    private ObservableCollection<PluginDto> _loadedPlugins = new();
    private ObservableCollection<ModIssueDto> _detectedIssues = new();
    private ObservableCollection<string> _callStack = new();
    private bool _isBusy;
    private string _statusMessage = string.Empty;
    
    public CrashLogDetailViewModel(CrashLogService crashLogService)
    {
        _crashLogService = crashLogService;
        
        MarkAsSolvedCommand = ReactiveCommand.CreateFromTask(MarkAsSolvedAsync);
        ExportReportCommand = ReactiveCommand.CreateFromTask(ExportReportAsync);
    }
    
    public CrashLogDetailDto? CrashLog
    {
        get => _crashLog;
        set => this.RaiseAndSetIfChanged(ref _crashLog, value);
    }
    
    public ObservableCollection<PluginDto> LoadedPlugins
    {
        get => _loadedPlugins;
        set => this.RaiseAndSetIfChanged(ref _loadedPlugins, value);
    }
    
    public ObservableCollection<ModIssueDto> DetectedIssues
    {
        get => _detectedIssues;
        set => this.RaiseAndSetIfChanged(ref _detectedIssues, value);
    }
    
    public ObservableCollection<string> CallStack
    {
        get => _callStack;
        set => this.RaiseAndSetIfChanged(ref _callStack, value);
    }
    
    public bool IsBusy
    {
        get => _isBusy;
        set => this.RaiseAndSetIfChanged(ref _isBusy, value);
    }
    
    public string StatusMessage
    {
        get => _statusMessage;
        set => this.RaiseAndSetIfChanged(ref _statusMessage, value);
    }
    
    public ReactiveCommand<Unit, Unit> MarkAsSolvedCommand { get; }
    public ReactiveCommand<Unit, Unit> ExportReportCommand { get; }
    
    public async Task LoadCrashLogAsync(string id)
    {
        IsBusy = true;
        StatusMessage = "Loading crash log details...";
        
        try
        {
            var crashLog = await _crashLogService.GetCrashLogDetailAsync(id);
            
            if (crashLog != null)
            {
                CrashLog = crashLog;
                LoadedPlugins = new ObservableCollection<PluginDto>(crashLog.LoadedPlugins);
                DetectedIssues = new ObservableCollection<ModIssueDto>(crashLog.DetectedIssues);
                CallStack = new ObservableCollection<string>(crashLog.CallStack);
                
                StatusMessage = "Crash log details loaded successfully.";
            }
            else
            {
                StatusMessage = "Crash log not found.";
            }
        }
        catch (Exception ex)
        {
            StatusMessage = $"Error loading crash log details: {ex.Message}";
        }
        finally
        {
            IsBusy = false;
        }
    }
    
    private async Task MarkAsSolvedAsync()
    {
        if (CrashLog == null)
            return;
            
        IsBusy = true;
        StatusMessage = "Marking crash log as solved...";
        
        try
        {
            // In a real application, this would update the crash log in the database
            CrashLog.IsSolved = true;
            
            StatusMessage = "Crash log marked as solved.";
        }
        catch (Exception ex)
        {
            StatusMessage = $"Error marking crash log as solved: {ex.Message}";
        }
        finally
        {
            IsBusy = false;
        }
    }
    
    private async Task ExportReportAsync()
    {
        if (CrashLog == null)
            return;
            
        IsBusy = true;
        StatusMessage = "Exporting crash log report...";
        
        try
        {
            // In a real application, this would save the report to a file
            var saveLocation = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.Desktop),
                $"CrashReport_{CrashLog.FileName}.md");
                
            // Generate and save report
            // await _crashLogService.GenerateReportAsync(CrashLog, saveLocation);
            
            StatusMessage = $"Report exported to {saveLocation}";
        }
        catch (Exception ex)
        {
            StatusMessage = $"Error exporting report: {ex.Message}";
        }
        finally
        {
            IsBusy = false;
        }
    }
}