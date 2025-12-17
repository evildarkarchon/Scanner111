using Scanner111.Common.Models.GamePath;

namespace Scanner111.Common.Services.GamePath;

/// <summary>
/// Provides methods for detecting game installation paths.
/// </summary>
/// <remarks>
/// <para>
/// This service locates game installations using multiple detection strategies:
/// </para>
/// <list type="bullet">
/// <item><description>Windows Registry (Bethesda Softworks and GOG keys)</description></item>
/// <item><description>XSE log file parsing</description></item>
/// <item><description>Cached path lookup</description></item>
/// </list>
/// <para>
/// All operations are read-only and never modify the registry or file system.
/// </para>
/// </remarks>
public interface IGamePathDetector
{
    /// <summary>
    /// Detects the installation path for a specific game type using all available methods.
    /// </summary>
    /// <param name="gameType">The type of game to detect.</param>
    /// <param name="xseLogPath">Optional path to the XSE log file for log-based detection.</param>
    /// <param name="cancellationToken">A token to cancel the operation.</param>
    /// <returns>A <see cref="GamePathResult"/> containing the detection result.</returns>
    /// <remarks>
    /// Detection is attempted in the following order:
    /// <list type="number">
    /// <item><description>Windows Registry (Bethesda Softworks key)</description></item>
    /// <item><description>GOG Galaxy registry key (for applicable games)</description></item>
    /// <item><description>XSE log file parsing (if <paramref name="xseLogPath"/> is provided)</description></item>
    /// </list>
    /// </remarks>
    Task<GamePathResult> DetectGamePathAsync(
        GameType gameType,
        string? xseLogPath = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Attempts to find the game path from the Windows registry.
    /// </summary>
    /// <param name="gameType">The type of game to find.</param>
    /// <param name="cancellationToken">A token to cancel the operation.</param>
    /// <returns>The game path if found and valid, otherwise <c>null</c>.</returns>
    Task<string?> FindFromRegistryAsync(
        GameType gameType,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Attempts to extract the game path from an XSE log file.
    /// </summary>
    /// <param name="gameType">The type of game (used for path validation).</param>
    /// <param name="xseLogPath">The path to the XSE log file.</param>
    /// <param name="cancellationToken">A token to cancel the operation.</param>
    /// <returns>The game path if found and valid, otherwise <c>null</c>.</returns>
    /// <remarks>
    /// XSE log files contain a "plugin directory" line that includes the game installation path.
    /// This method parses that line to extract the game root folder.
    /// </remarks>
    Task<string?> FindFromXseLogAsync(
        GameType gameType,
        string xseLogPath,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Validates that a path is a valid game installation directory.
    /// </summary>
    /// <param name="gameType">The type of game to validate.</param>
    /// <param name="gamePath">The path to validate.</param>
    /// <returns><c>true</c> if the path contains a valid game installation; otherwise, <c>false</c>.</returns>
    /// <remarks>
    /// Validation checks:
    /// <list type="bullet">
    /// <item><description>Path exists and is a directory</description></item>
    /// <item><description>Game executable file exists in the directory</description></item>
    /// </list>
    /// </remarks>
    bool ValidateGamePath(GameType gameType, string gamePath);

    /// <summary>
    /// Gets all installed games by checking the registry for each supported game type.
    /// </summary>
    /// <param name="cancellationToken">A token to cancel the operation.</param>
    /// <returns>A collection of <see cref="GamePathResult"/> for all found games.</returns>
    Task<IReadOnlyList<GamePathResult>> DetectAllInstalledGamesAsync(
        CancellationToken cancellationToken = default);
}
