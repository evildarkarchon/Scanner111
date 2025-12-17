using Scanner111.Common.Models.GameIntegrity;

namespace Scanner111.Common.Services.GameIntegrity;

/// <summary>
/// Provides functionality for checking game installation integrity.
/// </summary>
/// <remarks>
/// <para>
/// This service validates game installation integrity by performing:
/// <list type="bullet">
/// <item>Executable version validation via SHA-256 hash comparison</item>
/// <item>Installation location check (Program Files detection)</item>
/// <item>Steam INI file detection (outdated installation indicator)</item>
/// </list>
/// </para>
/// <para>
/// The checker operates in read-only mode and never modifies any files.
/// </para>
/// </remarks>
public interface IGameIntegrityChecker
{
    /// <summary>
    /// Performs a complete game integrity check.
    /// </summary>
    /// <param name="configuration">The configuration containing paths and expected hashes.</param>
    /// <param name="cancellationToken">A token to cancel the operation.</param>
    /// <returns>A <see cref="GameIntegrityResult"/> containing all check results.</returns>
    Task<GameIntegrityResult> CheckIntegrityAsync(
        GameIntegrityConfiguration configuration,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Checks only the game executable version.
    /// </summary>
    /// <param name="executablePath">The path to the game executable.</param>
    /// <param name="expectedHashOld">The SHA-256 hash of the older known version.</param>
    /// <param name="expectedHashNew">The SHA-256 hash of the latest known version.</param>
    /// <param name="cancellationToken">A token to cancel the operation.</param>
    /// <returns>A tuple containing the version status, computed hash, and matched version ("old", "new", or null).</returns>
    Task<(ExecutableVersionStatus Status, string? ComputedHash, string? MatchedVersion)> CheckExecutableVersionAsync(
        string? executablePath,
        string? expectedHashOld,
        string? expectedHashNew,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Checks if the game is installed in a recommended location.
    /// </summary>
    /// <param name="gamePath">The game installation path.</param>
    /// <returns>The installation location status.</returns>
    InstallationLocationStatus CheckInstallationLocation(string? gamePath);

    /// <summary>
    /// Checks if a Steam INI file exists at the specified path.
    /// </summary>
    /// <param name="steamIniPath">The path to check for the Steam INI file.</param>
    /// <returns><c>true</c> if the Steam INI file exists; otherwise, <c>false</c>.</returns>
    bool CheckSteamIniExists(string? steamIniPath);

    /// <summary>
    /// Computes the SHA-256 hash of a file.
    /// </summary>
    /// <param name="filePath">The path to the file.</param>
    /// <param name="cancellationToken">A token to cancel the operation.</param>
    /// <returns>The lowercase hexadecimal SHA-256 hash, or <c>null</c> if the file cannot be read.</returns>
    Task<string?> ComputeFileHashAsync(
        string filePath,
        CancellationToken cancellationToken = default);
}
