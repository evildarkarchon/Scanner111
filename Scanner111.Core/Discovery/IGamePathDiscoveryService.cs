using Scanner111.Core.Models;

namespace Scanner111.Core.Discovery;

/// <summary>
///     Service for discovering game installation paths.
/// </summary>
public interface IGamePathDiscoveryService
{
    /// <summary>
    ///     Discovers the game installation path asynchronously.
    /// </summary>
    /// <param name="gameInfo">Information about the game to discover.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The discovery result containing paths or error information.</returns>
    Task<PathDiscoveryResult> DiscoverGamePathAsync(GameInfo gameInfo, CancellationToken cancellationToken = default);

    /// <summary>
    ///     Attempts to discover game path via Windows registry.
    /// </summary>
    /// <param name="gameInfo">Information about the game to discover.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The discovery result or null if not found.</returns>
    Task<PathDiscoveryResult?> TryDiscoverViaRegistryAsync(GameInfo gameInfo,
        CancellationToken cancellationToken = default);

    /// <summary>
    ///     Attempts to discover game path via Script Extender log file.
    /// </summary>
    /// <param name="gameInfo">Information about the game to discover.</param>
    /// <param name="scriptExtenderLogPath">Path to the Script Extender log file.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The discovery result or null if not found.</returns>
    Task<PathDiscoveryResult?> TryDiscoverViaScriptExtenderLogAsync(
        GameInfo gameInfo,
        string scriptExtenderLogPath,
        CancellationToken cancellationToken = default);

    /// <summary>
    ///     Attempts to discover game path via Steam library.
    /// </summary>
    /// <param name="gameInfo">Information about the game to discover.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The discovery result or null if not found.</returns>
    Task<PathDiscoveryResult?> TryDiscoverViaSteamAsync(GameInfo gameInfo,
        CancellationToken cancellationToken = default);

    /// <summary>
    ///     Validates a discovered or provided game path.
    /// </summary>
    /// <param name="gameInfo">Information about the game.</param>
    /// <param name="gamePath">The game path to validate.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>True if the path is valid, false otherwise.</returns>
    Task<bool> ValidateGamePathAsync(GameInfo gameInfo, string gamePath, CancellationToken cancellationToken = default);

    /// <summary>
    ///     Gets cached discovery result if available and not expired.
    /// </summary>
    /// <param name="gameInfo">Information about the game.</param>
    /// <returns>Cached result or null if not available or expired.</returns>
    PathDiscoveryResult? GetCachedResult(GameInfo gameInfo);

    /// <summary>
    ///     Clears cached discovery results.
    /// </summary>
    void ClearCache();
}