using Scanner111.Core.Models;

namespace Scanner111.Core.Infrastructure;

/// <summary>
/// Interface for detecting game installation paths and configurations.
/// </summary>
public interface IGamePathDetection
{
    /// <summary>
    /// Attempts to detect the game installation path using various detection methods.
    /// </summary>
    /// <returns>The detected game installation path or an empty string if detection fails.</returns>
    string TryDetectGamePath();
    
    /// <summary>
    /// Attempts to detect the game installation path for a specific game.
    /// </summary>
    /// <param name="gameType">The game type to detect (e.g., "Fallout4", "Skyrim")</param>
    /// <returns>The detected game installation path or an empty string if detection fails.</returns>
    string TryDetectGamePath(string gameType);
    
    /// <summary>
    /// Attempts to retrieve the installation path of the game from the Windows registry.
    /// </summary>
    /// <returns>The detected game installation path from the registry or an empty string if no registry entry is found or valid.</returns>
    string TryGetGamePathFromRegistry();
    
    /// <summary>
    /// Attempts to retrieve the game installation path by parsing XSE (Script Extender) log files.
    /// </summary>
    /// <returns>The detected game installation path from the XSE log files or an empty string if the path cannot be determined.</returns>
    string TryGetGamePathFromXseLog();
    
    /// <summary>
    /// Validates whether the specified path contains a valid game installation by checking for required game files.
    /// </summary>
    /// <param name="path">The directory path to validate.</param>
    /// <returns>True if the directory contains necessary game files; otherwise, false.</returns>
    bool ValidateGamePath(string path);
    
    /// <summary>
    /// Gets the Documents folder path for game INI files.
    /// </summary>
    /// <param name="gameType">The type of game.</param>
    /// <returns>The path to the game's documents folder.</returns>
    string GetGameDocumentsPath(string gameType);
    
    /// <summary>
    /// Detects game configuration including paths and version info.
    /// </summary>
    /// <param name="gameType">The type of game to detect. Defaults to "Fallout4".</param>
    /// <returns>The detected game configuration or null if detection fails.</returns>
    GameConfiguration? DetectGameConfiguration(string gameType = "Fallout4");
}