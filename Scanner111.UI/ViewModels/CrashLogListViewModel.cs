using System;
using System.Collections.Generic;
using ReactiveUI;
using Scanner111.Application.Services;
using Scanner111.Application.DTOs;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Reactive;
using System.Threading.Tasks;

namespace Scanner111.UI.ViewModels;

public class CrashLogListViewModel : ViewModelBase
{
    private readonly CrashLogService _crashLogService;
    private readonly GameService _gameService;
    
    private ObservableCollection<CrashLogDto> _crashLogs = [];
    private CrashLogDto? _selectedCrashLog;
    private ObservableCollection<GameDto> _games = [];
    private GameDto? _selectedGame;
    private bool _isBusy;
    private string _statusMessage = string.Empty;
    private string _searchTerm = string.Empty;
    private bool _showSolvedLogs;
    
    public CrashLogListViewModel(
        CrashLogService crashLogService,
        GameService gameService)
    {
        _crashLogService = crashLogService;
        _gameService = gameService;
        
        RefreshCommand = ReactiveCommand.CreateFromTask(RefreshDataAsync);
        ScanCrashLogsCommand = ReactiveCommand.CreateFromTask(ScanCrashLogsAsync);
        ViewCrashLogCommand = ReactiveCommand.Create<CrashLogDto>(ViewCrashLog);
        FilterByGameCommand = ReactiveCommand.CreateFromTask<GameDto>(FilterByGameAsync);
        SearchCommand = ReactiveCommand.CreateFromTask(SearchAsync);
    }
    
    public ObservableCollection<CrashLogDto> CrashLogs
    {
        get => _crashLogs;
        set => this.RaiseAndSetIfChanged(ref _crashLogs, value);
    }
    
    public CrashLogDto? SelectedCrashLog
    {
        get => _selectedCrashLog;
        set => this.RaiseAndSetIfChanged(ref _selectedCrashLog, value);
    }
    
    public ObservableCollection<GameDto> Games
    {
        get => _games;
        set => this.RaiseAndSetIfChanged(ref _games, value);
    }
    
    public GameDto? SelectedGame
    {
        get => _selectedGame;
        set => this.RaiseAndSetIfChanged(ref _selectedGame, value);
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
    
    public string SearchTerm
    {
        get => _searchTerm;
        set => this.RaiseAndSetIfChanged(ref _searchTerm, value);
    }
    
    public bool ShowSolvedLogs
    {
        get => _showSolvedLogs;
        set
        {
            this.RaiseAndSetIfChanged(ref _showSolvedLogs, value);
            RefreshDataAsync().ConfigureAwait(false);
        }
    }
    
    public ReactiveCommand<Unit, Unit> RefreshCommand { get; }
    public ReactiveCommand<Unit, Unit> ScanCrashLogsCommand { get; }
    public ReactiveCommand<CrashLogDto, Unit> ViewCrashLogCommand { get; }
    public ReactiveCommand<GameDto, Unit> FilterByGameCommand { get; }
    public ReactiveCommand<Unit, Unit> SearchCommand { get; }
    
    public async Task InitializeAsync()
    {
        await RefreshDataAsync();
    }
    
    private async Task RefreshDataAsync()
    {
        IsBusy = true;
        StatusMessage = "Loading crash logs...";
        
        try
        {
            // Load games
            var games = await _gameService.GetAllGamesAsync();
            Games = new ObservableCollection<GameDto>(games);
            
            // Load crash logs
            IEnumerable<CrashLogDto> crashLogs;
            
            if (SelectedGame != null)
            {
                crashLogs = await _crashLogService.GetCrashLogsByGameAsync(SelectedGame.Id);
            }
            else
            {
                crashLogs = await _crashLogService.GetAllCrashLogsAsync();
            }
            
            // Filter by search term
            if (!string.IsNullOrWhiteSpace(SearchTerm))
            {
                crashLogs = crashLogs.Where(c => 
                    c.FileName.Contains(SearchTerm, StringComparison.OrdinalIgnoreCase) ||
                    c.MainError.Contains(SearchTerm, StringComparison.OrdinalIgnoreCase) ||
                    c.GameName.Contains(SearchTerm, StringComparison.OrdinalIgnoreCase));
            }
            
            // Filter by solved status
            if (!ShowSolvedLogs)
            {
                crashLogs = crashLogs.Where(c => !c.IsSolved);
            }
            
            CrashLogs = new ObservableCollection<CrashLogDto>(crashLogs.OrderByDescending(c => c.CrashTime));
            
            StatusMessage = $"Loaded {CrashLogs.Count} crash logs.";
        }
        catch (Exception ex)
        {
            StatusMessage = $"Error loading crash logs: {ex.Message}";
        }
        finally
        {
            IsBusy = false;
        }
    }
    
    private async Task ScanCrashLogsAsync()
    {
        IsBusy = true;
        StatusMessage = "Scanning for crash logs...";
        
        try
        {
            string crashLogFolder;
            
            if (SelectedGame != null && !string.IsNullOrEmpty(SelectedGame.DocumentsPath))
            {
                // Use game-specific documents path
                crashLogFolder = Path.Combine(SelectedGame.DocumentsPath, "F4SE");
            }
            else
            {
                // Use default documents path
                var documentsPath = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);
                crashLogFolder = Path.Combine(documentsPath, "My Games", "Fallout4", "F4SE");
            }
            
            if (Directory.Exists(crashLogFolder))
            {
                var crashLogIds = await _crashLogService.ScanForCrashLogsAsync(crashLogFolder);
                StatusMessage = $"Scanned {crashLogIds.Count()} crash logs.";
            }
            else
            {
                StatusMessage = $"Crash log folder not found: {crashLogFolder}";
            }
            
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
    
    private void ViewCrashLog(CrashLogDto crashLog)
    {
        SelectedCrashLog = crashLog;
        // In a real application, this would navigate to the crash log detail view
        StatusMessage = $"Viewing crash log: {crashLog.FileName}";
    }
    
    private async Task FilterByGameAsync(GameDto game)
    {
        SelectedGame = game;
        await RefreshDataAsync();
    }
    
    private async Task SearchAsync()
    {
        await RefreshDataAsync();
    }
}