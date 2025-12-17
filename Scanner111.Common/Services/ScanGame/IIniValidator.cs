using Scanner111.Common.Models.ScanGame;

namespace Scanner111.Common.Services.ScanGame;

/// <summary>
/// Provides functionality for scanning and validating INI configuration files
/// in a game installation directory.
/// </summary>
/// <remarks>
/// <para>
/// This service scans for INI and CONF files, detects duplicate configurations,
/// and identifies known problematic settings such as console commands, VSync
/// configurations, and mod-specific issues.
/// </para>
/// <para>
/// The validator caches parsed INI files for efficient repeated access. The cache
/// can be cleared explicitly using <see cref="ClearCache"/>.
/// </para>
/// </remarks>
public interface IIniValidator
{
    /// <summary>
    /// Scans a game directory for INI/CONF files and validates their contents.
    /// </summary>
    /// <param name="gameRootPath">The path to the game's root directory.</param>
    /// <param name="gameName">The game name (e.g., "Fallout4", "SkyrimSE").</param>
    /// <param name="cancellationToken">A token to cancel the operation.</param>
    /// <returns>An <see cref="IniScanResult"/> containing all detected issues.</returns>
    Task<IniScanResult> ScanAsync(
        string gameRootPath,
        string gameName,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Scans a game directory with progress reporting.
    /// </summary>
    /// <param name="gameRootPath">The path to the game's root directory.</param>
    /// <param name="gameName">The game name (e.g., "Fallout4", "SkyrimSE").</param>
    /// <param name="progress">Progress reporter for scan updates.</param>
    /// <param name="cancellationToken">A token to cancel the operation.</param>
    /// <returns>An <see cref="IniScanResult"/> containing all detected issues.</returns>
    Task<IniScanResult> ScanWithProgressAsync(
        string gameRootPath,
        string gameName,
        IProgress<IniScanProgress>? progress,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets a cached value from a previously scanned INI file.
    /// </summary>
    /// <typeparam name="T">The type to parse the value as (bool, int, float, double).</typeparam>
    /// <param name="fileNameLower">The lowercase filename of the INI file.</param>
    /// <param name="section">The section name.</param>
    /// <param name="setting">The setting/key name.</param>
    /// <returns>The parsed value, or null if not found or parse failed.</returns>
    T? GetValue<T>(string fileNameLower, string section, string setting) where T : struct;

    /// <summary>
    /// Gets a string value from a previously scanned INI file.
    /// </summary>
    /// <param name="fileNameLower">The lowercase filename of the INI file.</param>
    /// <param name="section">The section name.</param>
    /// <param name="setting">The setting/key name.</param>
    /// <returns>The string value, or null if not found.</returns>
    string? GetStringValue(string fileNameLower, string section, string setting);

    /// <summary>
    /// Checks if a specific setting exists in a cached INI file.
    /// </summary>
    /// <param name="fileNameLower">The lowercase filename.</param>
    /// <param name="section">The section name.</param>
    /// <param name="setting">The setting name.</param>
    /// <returns>True if the setting exists; otherwise, false.</returns>
    bool HasSetting(string fileNameLower, string section, string setting);

    /// <summary>
    /// Clears the internal file cache.
    /// </summary>
    void ClearCache();
}

/// <summary>
/// Represents progress information during an INI validation scan.
/// </summary>
/// <param name="CurrentFile">The file currently being processed.</param>
/// <param name="FilesScanned">Number of files scanned so far.</param>
/// <param name="IssuesFound">Number of issues found so far.</param>
public record IniScanProgress(
    string CurrentFile,
    int FilesScanned,
    int IssuesFound);
