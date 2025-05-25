namespace Scanner111.ClassicLib;

/// <summary>
/// Provides registry access for game-related information.
/// </summary>
public interface IGameRegistry
{
    /// <summary>
    /// Gets the current game identifier.
    /// </summary>
    /// <returns>The game identifier (e.g., "Fallout4").</returns>
    string GetGame();

    /// <summary>
    /// Gets the VR status of the current game.
    /// </summary>
    /// <returns>VR identifier if the game is VR, otherwise an empty string.</returns>
    string GetVR();
}