namespace Scanner111.ClassicLib.ScanLog.Services.Interfaces;

/// <summary>
///     Service for managing game context information.
/// </summary>
public interface IGameContextService
{
    /// <summary>
    ///     Gets the current game name.
    /// </summary>
    /// <returns>The current game name, or "Default" if not set.</returns>
    string GetCurrentGame();

    /// <summary>
    ///     Sets the current game name.
    /// </summary>
    /// <param name="gameName">The game name to set.</param>
    void SetCurrentGame(string gameName);

    /// <summary>
    ///     Gets the current game version (VR).
    /// </summary>
    /// <returns>The version of the game.</returns>
    string GetGameVr();

    /// <summary>
    ///     Checks XSE plugins for issues.
    /// </summary>
    /// <returns>A report of any issues found with XSE plugins.</returns>
    string CheckXsePlugins();

    /// <summary>
    ///     Checks crash generation settings.
    /// </summary>
    /// <returns>A report of any issues found with crash generation settings.</returns>
    string CheckCrashgenSettings();

    /// <summary>
    ///     Scans for Wrye Bash issues.
    /// </summary>
    /// <returns>A report of any issues found with Wrye Bash.</returns>
    string ScanWryeCheck();

    /// <summary>
    ///     Scans mod INI files for issues.
    /// </summary>
    /// <returns>A report of any issues found with mod INI files.</returns>
    string ScanModInis();
}