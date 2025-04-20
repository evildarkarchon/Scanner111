using System;
using ReactiveUI;
using Scanner111.Application.Services;
using Scanner111.Application.DTOs;
using System.Collections.ObjectModel;
using System.Reactive;
using System.Threading.Tasks;

namespace Scanner111.UI.ViewModels;

public class GameListViewModel : ViewModelBase
{
    private readonly GameService _gameService;
    
    private ObservableCollection<GameDto> _games = [];
    private GameDto? _selectedGame;
    private bool _isBusy;
    private string _statusMessage = string.Empty;
    
    public GameListViewModel(GameService gameService)
    {
        _gameService = gameService;
        
        RefreshCommand = ReactiveCommand.CreateFromTask(RefreshDataAsync);
        DetectGamesCommand = ReactiveCommand.CreateFromTask(DetectGamesAsync);
        ViewGameCommand = ReactiveCommand.Create<GameDto>(ViewGame);
        ValidateGameFilesCommand = ReactiveCommand.CreateFromTask<string>(ValidateGameFilesAsync);
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
    
    public ReactiveCommand<Unit, Unit> RefreshCommand { get; }
    public ReactiveCommand<Unit, Unit> DetectGamesCommand { get; }
    public ReactiveCommand<GameDto, Unit> ViewGameCommand { get; }
    public ReactiveCommand<string, Unit> ValidateGameFilesCommand { get; }
    
    public async Task InitializeAsync()
    {
        await RefreshDataAsync();
    }
    
    private async Task RefreshDataAsync()
    {
        IsBusy = true;
        StatusMessage = "Loading games...";
        
        try
        {
            var games = await _gameService.GetAllGamesAsync();
            Games = new ObservableCollection<GameDto>(games);
            
            StatusMessage = $"Loaded {Games.Count} games.";
        }
        catch (Exception ex)
        {
            StatusMessage = $"Error loading games: {ex.Message}";
        }
        finally
        {
            IsBusy = false;
        }
    }
    
    private async Task DetectGamesAsync()
    {
        IsBusy = true;
        StatusMessage = "Detecting installed games...";
        
        try
        {
            var games = await _gameService.DetectInstalledGamesAsync();
            Games = new ObservableCollection<GameDto>(games);
            
            StatusMessage = $"Detected {Games.Count} installed games.";
        }
        catch (Exception ex)
        {
            StatusMessage = $"Error detecting games: {ex.Message}";
        }
        finally
        {
            IsBusy = false;
        }
    }
    
    private void ViewGame(GameDto game)
    {
        SelectedGame = game;
        // In a real application, this would navigate to the game detail view
        StatusMessage = $"Viewing game: {game.Name}";
    }
    
    private async Task ValidateGameFilesAsync(string gameId)
    {
        IsBusy = true;
        StatusMessage = "Validating game files...";
        
        try
        {
            var valid = await _gameService.ValidateGameFilesAsync(gameId);

            StatusMessage = valid ? "Game files validation passed." : "Game files validation failed. Some required files are missing.";
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
}