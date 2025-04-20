using System;
using ReactiveUI;
using Scanner111.Application.Services;
using Scanner111.Application.DTOs;
using System.Collections.ObjectModel;
using System.Linq;
using System.Reactive;
using System.Threading.Tasks;

namespace Scanner111.UI.ViewModels;

public class PluginAnalysisViewModel : ViewModelBase
{
    private readonly PluginAnalysisService _pluginAnalysisService;
    private readonly GameService _gameService;
    
    private ObservableCollection<PluginDto> _plugins = [];
    private ObservableCollection<ModIssueDto> _issues = [];
    private ObservableCollection<GameDto> _games = [];
    private GameDto? _selectedGame;
    private PluginDto? _selectedPlugin;
    private bool _isBusy;
    private string _statusMessage = string.Empty;
    private string _searchTerm = string.Empty;
    private bool _showOnlyPluginsWithIssues;
    
    public PluginAnalysisViewModel(
        PluginAnalysisService pluginAnalysisService,
        GameService gameService)
    {
        _pluginAnalysisService = pluginAnalysisService;
        _gameService = gameService;
        
        RefreshCommand = ReactiveCommand.CreateFromTask(RefreshDataAsync);
        LoadPluginsCommand = ReactiveCommand.CreateFromTask<string>(LoadPluginsAsync);
        AnalyzeLoadOrderCommand = ReactiveCommand.CreateFromTask<string>(AnalyzeLoadOrderAsync);
        ViewPluginCommand = ReactiveCommand.CreateFromTask<PluginDto>(ViewPluginAsync);
        SearchCommand = ReactiveCommand.CreateFromTask(SearchAsync);
    }
    
    public ObservableCollection<PluginDto> Plugins
    {
        get => _plugins;
        set => this.RaiseAndSetIfChanged(ref _plugins, value);
    }
    
    public ObservableCollection<ModIssueDto> Issues
    {
        get => _issues;
        set => this.RaiseAndSetIfChanged(ref _issues, value);
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
    
    public PluginDto? SelectedPlugin
    {
        get => _selectedPlugin;
        set => this.RaiseAndSetIfChanged(ref _selectedPlugin, value);
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
    
    public bool ShowOnlyPluginsWithIssues
    {
        get => _showOnlyPluginsWithIssues;
        set
        {
            this.RaiseAndSetIfChanged(ref _showOnlyPluginsWithIssues, value);
            RefreshDataAsync().ConfigureAwait(false);
        }
    }
    
    public ReactiveCommand<Unit, Unit> RefreshCommand { get; }
    public ReactiveCommand<string, Unit> LoadPluginsCommand { get; }
    public ReactiveCommand<string, Unit> AnalyzeLoadOrderCommand { get; }
    public ReactiveCommand<PluginDto, Unit> ViewPluginCommand { get; }
    public ReactiveCommand<Unit, Unit> SearchCommand { get; }
    
    public async Task InitializeAsync()
    {
        await RefreshDataAsync();
    }
    
    private async Task RefreshDataAsync()
    {
        IsBusy = true;
        StatusMessage = "Loading data...";
        
        try
        {
            // Load games
            var games = await _gameService.GetAllGamesAsync();
            Games = new ObservableCollection<GameDto>(games);
            
            // Load plugins if game is selected
            if (SelectedGame != null)
            {
                await LoadPluginsAsync(SelectedGame.Id);
            }
            
            StatusMessage = "Data loaded successfully.";
        }
        catch (Exception ex)
        {
            StatusMessage = $"Error loading data: {ex.Message}";
        }
        finally
        {
            IsBusy = false;
        }
    }
    
    private async Task LoadPluginsAsync(string gameId)
    {
        IsBusy = true;
        StatusMessage = "Loading plugins...";
        
        try
        {
            var plugins = await _pluginAnalysisService.GetPluginsForGameAsync(gameId);
            
            // Filter plugins
            if (!string.IsNullOrWhiteSpace(SearchTerm))
            {
                plugins = plugins.Where(p =>
                    p.Name.Contains(SearchTerm, StringComparison.OrdinalIgnoreCase) ||
                    p.FileName.Contains(SearchTerm, StringComparison.OrdinalIgnoreCase));
            }
            
            if (ShowOnlyPluginsWithIssues)
            {
                plugins = plugins.Where(p => p.HasIssues);
            }
            
            Plugins = new ObservableCollection<PluginDto>(plugins);
            
            StatusMessage = $"Loaded {Plugins.Count} plugins.";
        }
        catch (Exception ex)
        {
            StatusMessage = $"Error loading plugins: {ex.Message}";
        }
        finally
        {
            IsBusy = false;
        }
    }
    
    private async Task AnalyzeLoadOrderAsync(string gameId)
    {
        IsBusy = true;
        StatusMessage = "Analyzing load order...";
        
        try
        {
            var issues = await _pluginAnalysisService.AnalyzeLoadOrderAsync(gameId);
            Issues = new ObservableCollection<ModIssueDto>(issues);
            
            // Refresh plugins to show updated issue status
            await LoadPluginsAsync(gameId);
            
            StatusMessage = $"Analysis complete. Found {Issues.Count} issues.";
        }
        catch (Exception ex)
        {
            StatusMessage = $"Error analyzing load order: {ex.Message}";
        }
        finally
        {
            IsBusy = false;
        }
    }
    
    private async Task ViewPluginAsync(PluginDto plugin)
    {
        IsBusy = true;
        StatusMessage = $"Loading issues for {plugin.Name}...";
        
        try
        {
            SelectedPlugin = plugin;
            
            var issues = await _pluginAnalysisService.GetIssuesForPluginAsync(plugin.Name);
            Issues = new ObservableCollection<ModIssueDto>(issues);
            
            StatusMessage = $"Loaded {Issues.Count} issues for {plugin.Name}.";
        }
        catch (Exception ex)
        {
            StatusMessage = $"Error loading plugin issues: {ex.Message}";
        }
        finally
        {
            IsBusy = false;
        }
    }
    
    private async Task SearchAsync()
    {
        if (SelectedGame != null)
        {
            await LoadPluginsAsync(SelectedGame.Id);
        }
    }
}