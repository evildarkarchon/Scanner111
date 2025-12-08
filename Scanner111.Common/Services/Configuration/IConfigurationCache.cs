using Scanner111.Common.Models.Analysis;
using Scanner111.Common.Models.Configuration;
using Scanner111.Common.Services.Analysis;

namespace Scanner111.Common.Services.Configuration;

/// <summary>
/// Interface for caching configuration and database data.
/// </summary>
public interface IConfigurationCache
{
    /// <summary>
    /// Gets the game configuration for the specified game.
    /// </summary>
    /// <param name="gameName">The name of the game.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>The game configuration.</returns>
    Task<GameConfiguration> GetGameConfigAsync(string gameName, CancellationToken ct = default);

    /// <summary>
    /// Gets the game settings for the specified game (used for settings validation).
    /// </summary>
    /// <param name="gameName">The name of the game.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>The game settings.</returns>
    Task<GameSettings> GetGameSettingsAsync(string gameName, CancellationToken ct = default);

    // TODO: ModDatabase for Phase 6
    // Task<ModDatabase> GetModDatabaseAsync(string gameName, CancellationToken ct = default);

    /// <summary>
    /// Gets the suspect patterns for the specified game.
    /// </summary>
    /// <param name="gameName">The name of the game.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>The suspect patterns.</returns>
    Task<SuspectPatterns> GetSuspectPatternsAsync(string gameName, CancellationToken ct = default);

    /// <summary>
    /// Clears the cache.
    /// </summary>
    void Clear();
}
