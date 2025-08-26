using Scanner111.Core.Models;

namespace Scanner111.Core.Discovery;

/// <summary>
///     Service for discovering game documents folder paths.
/// </summary>
public interface IDocumentsPathDiscoveryService
{
    /// <summary>
    ///     Discovers the game documents folder path asynchronously.
    /// </summary>
    /// <param name="gameInfo">Information about the game.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The discovery result containing paths or error information.</returns>
    Task<PathDiscoveryResult> DiscoverDocumentsPathAsync(GameInfo gameInfo,
        CancellationToken cancellationToken = default);

    /// <summary>
    ///     Discovers Windows documents folder path.
    /// </summary>
    /// <param name="gameInfo">Information about the game.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The discovered documents path or null if not found.</returns>
    Task<string?> DiscoverWindowsDocumentsPathAsync(GameInfo gameInfo, CancellationToken cancellationToken = default);

    /// <summary>
    ///     Discovers Linux/Steam Proton documents folder path.
    /// </summary>
    /// <param name="gameInfo">Information about the game.</param>
    /// <param name="steamLibraryPath">Path to the Steam library.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The discovered documents path or null if not found.</returns>
    Task<string?> DiscoverLinuxDocumentsPathAsync(
        GameInfo gameInfo,
        string steamLibraryPath,
        CancellationToken cancellationToken = default);

    /// <summary>
    ///     Validates a documents folder path.
    /// </summary>
    /// <param name="gameInfo">Information about the game.</param>
    /// <param name="documentsPath">The documents path to validate.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>True if the path is valid, false otherwise.</returns>
    Task<bool> ValidateDocumentsPathAsync(
        GameInfo gameInfo,
        string documentsPath,
        CancellationToken cancellationToken = default);

    /// <summary>
    ///     Checks and validates game INI files.
    /// </summary>
    /// <param name="gameInfo">Information about the game.</param>
    /// <param name="documentsPath">The documents folder path.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>List of validation results for INI files.</returns>
    Task<List<IniValidationResult>> ValidateIniFilesAsync(
        GameInfo gameInfo,
        string documentsPath,
        CancellationToken cancellationToken = default);

    /// <summary>
    ///     Ensures archive invalidation is properly configured.
    /// </summary>
    /// <param name="gameInfo">Information about the game.</param>
    /// <param name="customIniPath">Path to the custom INI file.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>True if archive invalidation was configured successfully.</returns>
    Task<bool> EnsureArchiveInvalidationAsync(
        GameInfo gameInfo,
        string customIniPath,
        CancellationToken cancellationToken = default);
}

/// <summary>
///     Represents the result of INI file validation.
/// </summary>
public sealed record IniValidationResult
{
    /// <summary>
    ///     Gets the INI file name.
    /// </summary>
    public required string FileName { get; init; }

    /// <summary>
    ///     Gets the full path to the INI file.
    /// </summary>
    public required string FilePath { get; init; }

    /// <summary>
    ///     Gets a value indicating whether the file exists.
    /// </summary>
    public bool Exists { get; init; }

    /// <summary>
    ///     Gets a value indicating whether the file is valid.
    /// </summary>
    public bool IsValid { get; init; }

    /// <summary>
    ///     Gets a value indicating whether archive invalidation is enabled.
    /// </summary>
    public bool HasArchiveInvalidation { get; init; }

    /// <summary>
    ///     Gets validation messages.
    /// </summary>
    public List<string> ValidationMessages { get; init; } = new();

    /// <summary>
    ///     Gets a value indicating whether the file needs to be created.
    /// </summary>
    public bool NeedsCreation { get; init; }

    /// <summary>
    ///     Gets a value indicating whether the file needs repair.
    /// </summary>
    public bool NeedsRepair { get; init; }
}