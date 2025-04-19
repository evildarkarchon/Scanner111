using Scanner111.Core.Interfaces.Repositories;
using Scanner111.Core.Interfaces.Services;
using Scanner111.Core.Models;
using Scanner111.Application.DTOs;

namespace Scanner111.Application.Services;

public class GameService
{
    private readonly IGameRepository _gameRepository;
    private readonly IGameDetectionService _gameDetectionService;
    private readonly IPluginSystemService _pluginSystemService;
    
    public GameService(
        IGameRepository gameRepository,
        IGameDetectionService gameDetectionService,
        IPluginSystemService pluginSystemService)
    {
        _gameRepository = gameRepository;
        _gameDetectionService = gameDetectionService;
        _pluginSystemService = pluginSystemService;
    }
    
    public async Task<IEnumerable<GameDto>> GetAllGamesAsync()
    {
        var games = await _gameRepository.GetAllAsync();
        return games.Select(g => new GameDto
        {
            Id = g.Id,
            Name = g.Name,
            ExecutableName = g.ExecutableName,
            InstallPath = g.InstallPath,
            DocumentsPath = g.DocumentsPath,
            Version = g.Version,
            IsInstalled = !string.IsNullOrEmpty(g.InstallPath),
            IsSupported = true
        });
    }
    
    public async Task<GameDto?> GetGameByIdAsync(string id)
    {
        var game = await _gameRepository.GetByIdAsync(id);
        if (game == null)
            return null;
            
        return new GameDto
        {
            Id = game.Id,
            Name = game.Name,
            ExecutableName = game.ExecutableName,
            InstallPath = game.InstallPath,
            DocumentsPath = game.DocumentsPath,
            Version = game.Version,
            IsInstalled = !string.IsNullOrEmpty(game.InstallPath),
            IsSupported = true
        };
    }
    
    public async Task<IEnumerable<GameDto>> DetectInstalledGamesAsync()
    {
        var detectedGames = await _gameDetectionService.DetectInstalledGamesAsync();
        var result = new List<GameDto>();
        
        foreach (var game in detectedGames)
        {
            var existingGame = await _gameRepository.GetByIdAsync(game.Id);
            if (existingGame == null)
            {
                await _gameRepository.AddAsync(game);
                existingGame = game;
            }
            else
            {
                existingGame.InstallPath = game.InstallPath;
                existingGame.DocumentsPath = game.DocumentsPath;
                existingGame.Version = game.Version;
                await _gameRepository.UpdateAsync(existingGame);
            }
            
            result.Add(new GameDto
            {
                Id = existingGame.Id,
                Name = existingGame.Name,
                ExecutableName = existingGame.ExecutableName,
                InstallPath = existingGame.InstallPath,
                DocumentsPath = existingGame.DocumentsPath,
                Version = existingGame.Version,
                IsInstalled = !string.IsNullOrEmpty(existingGame.InstallPath),
                IsSupported = true
            });
        }
        
        return result;
    }
    
    public async Task<bool> ValidateGameFilesAsync(string gameId)
    {
        var game = await _gameRepository.GetByIdAsync(gameId);
        if (game == null)
            return false;
            
        var plugin = await _pluginSystemService.GetPluginForGameAsync(gameId);
        if (plugin == null)
            return false;
            
        return await plugin.ValidateGameFilesAsync(game);
    }
}