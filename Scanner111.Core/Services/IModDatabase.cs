namespace Scanner111.Core.Services;

/// <summary>
///     Interface for loading mod database information from YAML configuration files.
///     Provides access to mod warnings, conflicts, and important mod recommendations.
/// </summary>
public interface IModDatabase
{
    /// <summary>
    ///     Loads problematic mod warnings from YAML configuration.
    /// </summary>
    /// <param name="category">Category of mods to load (e.g., "FREQ", "PERF", "STAB")</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Dictionary mapping mod identifiers to warning messages</returns>
    Task<IReadOnlyDictionary<string, string>> LoadModWarningsAsync(
        string category,
        CancellationToken cancellationToken = default);

    /// <summary>
    ///     Loads mod conflict pairs from YAML configuration.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Dictionary mapping conflict pairs (mod1|mod2) to conflict messages</returns>
    Task<IReadOnlyDictionary<string, string>> LoadModConflictsAsync(
        CancellationToken cancellationToken = default);

    /// <summary>
    ///     Loads important/recommended mods from YAML configuration.
    /// </summary>
    /// <param name="category">Category of important mods (e.g., "CORE", "CORE_FOLON")</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Dictionary mapping mod identifiers to recommendation messages</returns>
    Task<IReadOnlyDictionary<string, string>> LoadImportantModsAsync(
        string category,
        CancellationToken cancellationToken = default);

    /// <summary>
    ///     Gets all available mod warning categories.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Collection of available warning categories</returns>
    Task<IReadOnlyList<string>> GetModWarningCategoriesAsync(
        CancellationToken cancellationToken = default);

    /// <summary>
    ///     Gets all available important mod categories.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Collection of available important mod categories</returns>
    Task<IReadOnlyList<string>> GetImportantModCategoriesAsync(
        CancellationToken cancellationToken = default);

    /// <summary>
    ///     Checks if the mod database is available and can be loaded.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>True if database is available, false otherwise</returns>
    Task<bool> IsAvailableAsync(CancellationToken cancellationToken = default);
}