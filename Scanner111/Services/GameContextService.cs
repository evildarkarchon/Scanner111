using System;

namespace Scanner111.Services;

/// <summary>
/// Implementation of the game context service.
/// </summary>
public class GameContextService : IGameContextService
{
    private string _currentGame = "Default";
    
    /// <inheritdoc />
    public string GetCurrentGame()
    {
        return _currentGame;
    }
    
    /// <inheritdoc />
    public void SetCurrentGame(string gameName)
    {
        if (string.IsNullOrEmpty(gameName))
        {
            throw new ArgumentException("Game name cannot be null or empty", nameof(gameName));
        }
        
        _currentGame = gameName;
    }
}