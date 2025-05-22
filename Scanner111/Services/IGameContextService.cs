using System;

namespace Scanner111.Services;

/// <summary>
/// Service for managing game context information.
/// </summary>
public interface IGameContextService
{
    /// <summary>
    /// Gets the current game name.
    /// </summary>
    /// <returns>The current game name, or "Default" if not set.</returns>
    string GetCurrentGame();
    
    /// <summary>
    /// Sets the current game name.
    /// </summary>
    /// <param name="gameName">The game name to set.</param>
    void SetCurrentGame(string gameName);
}