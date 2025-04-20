using System;
using ReactiveUI;
using Scanner111.Application.Services;
using Scanner111.Application.DTOs;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Reactive;
using System.Threading.Tasks;

namespace Scanner111.UI.ViewModels;

public class DashboardViewModel : ViewModelBase
{
    private readonly GameService _gameService;
    private readonly CrashLogService _crashLogService;
    private readonly PluginAnalysisService _pluginAnalysisService;
    private GameDto? _selectedGame;
    
    private ObservableCollection<GameDto> _installedGames = [];
    private ObservableCollection<CrashLogDto> _recentCrashLogs = [];
    private int _totalCrashLogs;
    private int _unsolvedCrashLogs;
    private bool _isBusy;
    private string _statusMessage = string.Empty;
    
    public DashboardViewModel(
        GameService gameService,
        CrashLogService crashLogService,
        PluginAnalysisService pluginAnalysisService)
    {
        _gameService = gameService;
        _crashLogService = crashLogService;
        _pluginAnalysisService = pluginAnalysisService;
        
        RefreshCommand = ReactiveCommand.CreateFromTask(RefreshDataAsync);
        ScanCrashLogsCommand = ReactiveCommand.CreateFromTask<string>(ScanCrashLogsAsync);
        AnalyzePluginsCommand = ReactiveCommand.CreateFromTask<string>(AnalyzePluginsAsync);
    }
    
    public GameDto? SelectedGame
    {
        get => _selectedGame;
        set => this.RaiseAndSetIfChanged(ref _selectedGame, value);
    }

    public ObservableCollection<GameDto> InstalledGames
    {
        get => _installedGames;
        set => this.RaiseAndSetIfChanged(ref _installedGames, value);
    }
    
    public ObservableCollection<CrashLogDto> RecentCrashLogs
    {
        get => _recentCrashLogs;
        set => this.RaiseAndSetIfChanged(ref _recentCrashLogs, value);
    }
    
    public int TotalCrashLogs
    {
        get => _totalCrashLogs;
        set => this.RaiseAndSetIfChanged(ref _totalCrashLogs, value);
    }
    
    public int UnsolvedCrashLogs
    {
        get => _unsolvedCrashLogs;
        set => this.RaiseAndSetIfChanged(ref _unsolvedCrashLogs, value);
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
    
    public ReactiveCommand<Unit, Unit> RefreshCommand { get; }
    public ReactiveCommand<string, Unit> ScanCrashLogsCommand { get; }
    public ReactiveCommand<string, Unit> AnalyzePluginsCommand { get; }
    
    public async Task InitializeAsync()
    {
        await RefreshDataAsync();
    }
    
    private async Task RefreshDataAsync()
    {
        IsBusy = true;
        StatusMessage = "Refreshing data...";
        
        try
        {
            // Detect installed games
            var games = await _gameService.DetectInstalledGamesAsync();
            InstalledGames = new ObservableCollection<GameDto>(games);
            
            // Get recent crash logs
            var crashLogs = await _crashLogService.GetAllCrashLogsAsync();
            RecentCrashLogs = new ObservableCollection<CrashLogDto>(
                crashLogs.OrderByDescending(c => c.CrashTime).Take(5));
            
            // Update statistics
            TotalCrashLogs = crashLogs.Count();
            UnsolvedCrashLogs = crashLogs.Count(c => !c.IsSolved);
            
            StatusMessage = "Data refreshed successfully.";
        }
        catch (Exception ex)
        {
            StatusMessage = $"Error refreshing data: {ex.Message}";
        }
        finally
        {
            IsBusy = false;
        }
    }
    
    private async Task ScanCrashLogsAsync(string gameId)
    {
        IsBusy = true;
        StatusMessage = "Scanning crash logs...";
        
        try
        {
            // Get crash log folder path
            var game = await _gameService.GetGameByIdAsync(gameId);
            if (game == null)
            {
                StatusMessage = "Invalid game selected.";
                return;
            }
            
            var documentsPath = game.DocumentsPath;
            if (string.IsNullOrEmpty(documentsPath))
            {
                StatusMessage = "Game documents path not found.";
                return;
            }
            
            // Scan for crash logs
            var crashLogFolder = Path.Combine(documentsPath, "F4SE");
            var crashLogIds = await _crashLogService.ScanForCrashLogsAsync(crashLogFolder);
            
            StatusMessage = $"Scanned {crashLogIds.Count()} crash logs.";
            
            // Refresh data
            await RefreshDataAsync();
        }
        catch (Exception ex)
        {
            StatusMessage = $"Error scanning crash logs: {ex.Message}";
        }
        finally
        {
            IsBusy = false;
        }
    }
    
    private async Task AnalyzePluginsAsync(string gameId)
    {
        IsBusy = true;
        StatusMessage = "Analyzing plugins...";
        
        try
        {
            // Load plugins
            await _pluginAnalysisService.LoadPluginsAsync(gameId);
            
            // Analyze load order
            var issues = await _pluginAnalysisService.AnalyzeLoadOrderAsync(gameId);
            
            StatusMessage = $"Analyzed plugins. Found {issues.Count()} issues.";
        }
        catch (Exception ex)
        {
            StatusMessage = $"Error analyzing plugins: {ex.Message}";
        }
        finally
        {
            IsBusy = false;
        }
    }
}