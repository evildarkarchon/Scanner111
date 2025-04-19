using System;
using System.Collections.Generic;
using ReactiveUI;
using Scanner111.Application.Services;
using Scanner111.Application.DTOs;
using System.Collections.ObjectModel;
using System.Reactive;
using System.Reactive.Linq;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

namespace Scanner111.UI.ViewModels;

public class GameDetailViewModel : ViewModelBase
{
    private readonly GameService _gameService;
    private readonly CrashLogService _crashLogService;
    private readonly PluginAnalysisService _pluginAnalysisService;
    
    private GameDto? _game;
    private ObservableCollection<PluginDto> _installedPlugins = new();
    private ObservableCollection<string> _requiredFiles = new();
    private ObservableCollection<CrashLogDto> _recentCrashLogs = new();
    private bool _isGameValid;
    private bool _isXSEInstalled;
    private bool _isCrashGenInstalled;
    private bool _isBusy;
    private string _statusMessage = string.Empty;
    private string _customGamePath = string.Empty;
    private string _customDocsPath = string.Empty;
    
    public GameDetailViewModel(
        GameService gameService,
        CrashLogService crashLogService,
        PluginAnalysisService pluginAnalysisService)
    {
        _gameService = gameService;
        _crashLogService = crashLogService;
        _pluginAnalysisService = pluginAnalysisService;
        
        ValidateGameFilesCommand = ReactiveCommand.CreateFromTask(ValidateGameFilesAsync);
        ScanForCrashLogsCommand = ReactiveCommand.CreateFromTask(ScanForCrashLogsAsync);
        AnalyzePluginsCommand = ReactiveCommand.CreateFromTask(AnalyzePluginsAsync);
        RefreshPluginsCommand = ReactiveCommand.CreateFromTask(LoadPluginsAsync);
        SelectGamePathCommand = ReactiveCommand.CreateFromTask<string>(SelectGamePathAsync);
        SelectDocsPathCommand = ReactiveCommand.CreateFromTask<string>(SelectDocsPathAsync);
        SavePathsCommand = ReactiveCommand.CreateFromTask(SaveCustomPathsAsync);
    }
    
    public GameDto? Game
    {
        get => _game;
        set => this.RaiseAndSetIfChanged(ref _game, value);
    }
    
    public ObservableCollection<PluginDto> InstalledPlugins
    {
        get => _installedPlugins;
        set => this.RaiseAndSetIfChanged(ref _installedPlugins, value);
    }
    
    public ObservableCollection<string> RequiredFiles
    {
        get => _requiredFiles;
        set => this.RaiseAndSetIfChanged(ref _requiredFiles, value);
    }
    
    public ObservableCollection<CrashLogDto> RecentCrashLogs
    {
        get => _recentCrashLogs;
        set => this.RaiseAndSetIfChanged(ref _recentCrashLogs, value);
    }
    
    public bool IsGameValid
    {
        get => _isGameValid;
        set => this.RaiseAndSetIfChanged(ref _isGameValid, value);
    }
    
    public bool IsXSEInstalled
    {
        get => _isXSEInstalled;
        set => this.RaiseAndSetIfChanged(ref _isXSEInstalled, value);
    }
    
    public bool IsCrashGenInstalled
    {
        get => _isCrashGenInstalled;
        set => this.RaiseAndSetIfChanged(ref _isCrashGenInstalled, value);
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
    
    public string CustomGamePath
    {
        get => _customGamePath;
        set => this.RaiseAndSetIfChanged(ref _customGamePath, value);
    }
    
    public string CustomDocsPath
    {
        get => _customDocsPath;
        set => this.RaiseAndSetIfChanged(ref _customDocsPath, value);
    }
    
    public ReactiveCommand<Unit, Unit> ValidateGameFilesCommand { get; }
    public ReactiveCommand<Unit, Unit> ScanForCrashLogsCommand { get; }
    public ReactiveCommand<Unit, Unit> AnalyzePluginsCommand { get; }
    public ReactiveCommand<Unit, Unit> RefreshPluginsCommand { get; }
    public ReactiveCommand<string, Unit> SelectGamePathCommand { get; }
    public ReactiveCommand<string, Unit> SelectDocsPathCommand { get; }
    public ReactiveCommand<Unit, Unit> SavePathsCommand { get; }
    
    public async Task LoadGameAsync(string gameId)
    {
        IsBusy = true;
        StatusMessage = "Loading game details...";
        
        try
        {
            var game = await _gameService.GetGameByIdAsync(gameId);
            if (game == null)
            {
                StatusMessage = "Game not found.";
                return;
            }
            
            Game = game;
            CustomGamePath = game.InstallPath;
            CustomDocsPath = game.DocumentsPath;
            
            // Load plugins
            await LoadPluginsAsync();
            
            // Load recent crash logs
            await LoadRecentCrashLogsAsync();
            
            // Check game validity
            await ValidateGameFilesAsync();
            
            StatusMessage = "Game details loaded successfully.";
        }
        catch (Exception ex)
        {
            StatusMessage = $"Error loading game details: {ex.Message}";
        }
        finally
        {
            IsBusy = false;
        }
    }
    
    private async Task LoadPluginsAsync()
    {
        if (Game == null)
            return;
            
        IsBusy = true;
        StatusMessage = "Loading plugins...";
        
        try
        {
            var plugins = await _pluginAnalysisService.GetPluginsForGameAsync(Game.Id);
            InstalledPlugins = new ObservableCollection<PluginDto>(plugins);
            
            StatusMessage = $"Loaded {InstalledPlugins.Count} plugins.";
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
    
    private async Task LoadRecentCrashLogsAsync()
    {
        if (Game == null)
            return;
            
        IsBusy = true;
        StatusMessage = "Loading recent crash logs...";
        
        try
        {
            var crashLogs = await _crashLogService.GetCrashLogsByGameAsync(Game.Id);
            RecentCrashLogs = new ObservableCollection<CrashLogDto>(
                crashLogs.OrderByDescending(c => c.CrashTime).Take(5));
            
            StatusMessage = $"Loaded {RecentCrashLogs.Count} recent crash logs.";
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
    
    private async Task ValidateGameFilesAsync()
    {
        if (Game == null)
            return;
            
        IsBusy = true;
        StatusMessage = "Validating game files...";
        
        try
        {
            // Check if game is valid
            IsGameValid = await _gameService.ValidateGameFilesAsync(Game.Id);
            
            // Check if script extender is installed
            IsXSEInstalled = await CheckScriptExtenderInstalledAsync();
            
            // Check if crash generator is installed
            IsCrashGenInstalled = await CheckCrashGeneratorInstalledAsync();
            
            // Load required files list
            RequiredFiles = new ObservableCollection<string>(await GetRequiredFilesAsync());
            
            StatusMessage = IsGameValid 
                ? "Game files validation passed." 
                : "Game files validation failed. Some required files are missing.";
        }
        catch (Exception ex)
        {
            StatusMessage = $"Error validating game files: {ex.Message}";
        }
        finally
        {
            IsBusy = false;
        }
    }
    
    private async Task<bool> CheckScriptExtenderInstalledAsync()
    {
        if (Game == null || string.IsNullOrEmpty(Game.InstallPath))
            return false;
            
        // This would be specific to the game - for Fallout 4, check for f4se_loader.exe
        var xsePath = Path.Combine(Game.InstallPath, "f4se_loader.exe");
        return File.Exists(xsePath);
    }
    
    private async Task<bool> CheckCrashGeneratorInstalledAsync()
    {
        if (Game == null || string.IsNullOrEmpty(Game.InstallPath))
            return false;
            
        // This would be specific to the game - for Fallout 4, check for Buffout4.dll
        var crashGenPath = Path.Combine(Game.InstallPath, "Data", "F4SE", "Plugins", "Buffout4.dll");
        return File.Exists(crashGenPath);
    }
    
    private async Task<IEnumerable<string>> GetRequiredFilesAsync()
    {
        // This would be specific to the game plugin
        // For Fallout 4, would include files like:
        return new List<string>
        {
            "Fallout4.exe",
            "Fallout4.esm",
            "f4se_loader.exe",
            "Data\\F4SE\\Plugins\\Buffout4.dll",
            "Data\\F4SE\\Plugins\\version-1-10-163-0.bin"
        };
    }
    
    private async Task ScanForCrashLogsAsync()
    {
        if (Game == null || string.IsNullOrEmpty(Game.DocumentsPath))
            return;
            
        IsBusy = true;
        StatusMessage = "Scanning for crash logs...";
        
        try
        {
            // For Fallout 4, crash logs are in the F4SE folder
            var crashLogFolder = Path.Combine(Game.DocumentsPath, "F4SE");
            
            if (Directory.Exists(crashLogFolder))
            {
                var crashLogIds = await _crashLogService.ScanForCrashLogsAsync(crashLogFolder);
                StatusMessage = $"Scanned {crashLogIds.Count()} crash logs.";
                
                // Reload recent crash logs
                await LoadRecentCrashLogsAsync();
            }
            else
            {
                StatusMessage = $"Crash log folder not found: {crashLogFolder}";
            }
        }
        catch (Exception ex)
        {
            StatusMessage = $"Error scanning for crash logs: {ex.Message}";
        }
        finally
        {
            IsBusy = false;
        }
    }
    
    private async Task AnalyzePluginsAsync()
    {
        if (Game == null)
            return;
            
        IsBusy = true;
        StatusMessage = "Analyzing plugins...";
        
        try
        {
            // Load plugins if not already loaded
            if (InstalledPlugins.Count == 0)
            {
                await _pluginAnalysisService.LoadPluginsAsync(Game.Id);
            }
            
            // Analyze load order
            var issues = await _pluginAnalysisService.AnalyzeLoadOrderAsync(Game.Id);
            
            // Reload plugins to show updated status
            await LoadPluginsAsync();
            
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
    
    private async Task SelectGamePathAsync(string path)
    {
        CustomGamePath = path;
    }
    
    private async Task SelectDocsPathAsync(string path)
    {
        CustomDocsPath = path;
    }
    
    private async Task SaveCustomPathsAsync()
    {
        if (Game == null)
            return;
            
        IsBusy = true;
        StatusMessage = "Saving custom paths...";
        
        try
        {
            // Create updated game DTO with custom paths
            var updatedGame = new GameDto
            {
                Id = Game.Id,
                Name = Game.Name,
                ExecutableName = Game.ExecutableName,
                InstallPath = CustomGamePath,
                DocumentsPath = CustomDocsPath,
                Version = Game.Version,
                IsInstalled = true,
                IsSupported = Game.IsSupported
            };
            
            // Update the game in the database - would require a new method in GameService
            // await _gameService.UpdateGameAsync(updatedGame);
            
            // Update local game object
            Game = updatedGame;
            
            StatusMessage = "Custom paths saved successfully.";
            
            // Revalidate game files with new paths
            await ValidateGameFilesAsync();
        }
        catch (Exception ex)
        {
            StatusMessage = $"Error saving custom paths: {ex.Message}";
        }
        finally
        {
            IsBusy = false;
        }
    }
}