using Scanner111.Services.Interfaces;

namespace Scanner111.Services;

/// <summary>
///     Implementation of IGameContextService that provides game context information
/// </summary>
public class GameContextService : IGameContextService
{
    // The default game - can be modified to be configurable if needed
    private const string DefaultGame = "Fallout4";

    private string _currentGame = DefaultGame;

    /// <summary>
    ///     Gets the current game name
    /// </summary>
    /// <returns>The name of the current game</returns>
    public string GetCurrentGame()
    {
        return _currentGame;
    }

    /// <summary>
    ///     Sets the current game name
    /// </summary>
    /// <param name="gameName">The game name to set</param>
    public void SetCurrentGame(string gameName)
    {
        if (!string.IsNullOrEmpty(gameName)) _currentGame = gameName;
    }

    /// <summary>
    ///     Gets the current game version (VR)
    /// </summary>
    /// <returns>The version of the game</returns>
    public string GetGameVr()
    {
        // Default implementation - should be replaced with actual logic
        return "Unknown";
    }

    /// <summary>
    ///     Checks XSE plugins for issues
    /// </summary>
    /// <returns>A report of any issues found with XSE plugins</returns>
    public string CheckXsePlugins()
    {
        // Default implementation - should be replaced with actual logic
        return string.Empty;
    }

    /// <summary>
    ///     Checks crash generation settings
    /// </summary>
    /// <returns>A report of any issues found with crash generation settings</returns>
    public string CheckCrashgenSettings()
    {
        // Default implementation - should be replaced with actual logic
        return string.Empty;
    }

    /// <summary>
    ///     Scans for Wrye Bash issues
    /// </summary>
    /// <returns>A report of any issues found with Wrye Bash</returns>
    public string ScanWryeCheck()
    {
        // Default implementation - should be replaced with actual logic
        return string.Empty;
    }

    /// <summary>
    ///     Scans mod INI files for issues
    /// </summary>
    /// <returns>A report of any issues found with mod INI files</returns>
    public string ScanModInis()
    {
        // Default implementation - should be replaced with actual logic
        return string.Empty;
    }
}