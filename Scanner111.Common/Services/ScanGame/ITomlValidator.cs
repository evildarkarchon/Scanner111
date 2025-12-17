using Scanner111.Common.Models.ScanGame;

namespace Scanner111.Common.Services.ScanGame;

/// <summary>
/// Provides functionality for validating TOML configuration files for crash generator mods.
/// </summary>
/// <remarks>
/// <para>
/// This service validates Buffout4 and similar crash generator TOML configuration files,
/// detecting issues such as:
/// <list type="bullet">
/// <item>Missing or duplicate configuration files</item>
/// <item>Plugin conflicts (X-Cell, Achievements, Looks Menu/F4EE)</item>
/// <item>Incorrect settings based on installed plugins</item>
/// <item>Redundant mods (e.g., BakaScrapHeap with Buffout4)</item>
/// </list>
/// </para>
/// <para>
/// The validator operates in read-only mode and never modifies configuration files.
/// All detected issues are reported for the user to address manually.
/// </para>
/// </remarks>
public interface ITomlValidator
{
    /// <summary>
    /// Validates the crash generator TOML configuration for a game installation.
    /// </summary>
    /// <param name="pluginsPath">
    /// The path to the F4SE/SKSE plugins directory (e.g., "Data\F4SE\Plugins").
    /// </param>
    /// <param name="crashGenName">
    /// The name of the crash generator mod (e.g., "Buffout4", "CrashLogger").
    /// </param>
    /// <param name="gameName">
    /// The game name for conditional checks (e.g., "Fallout4", "SkyrimSE").
    /// </param>
    /// <param name="cancellationToken">A token to cancel the operation.</param>
    /// <returns>A <see cref="TomlScanResult"/> containing all detected issues.</returns>
    Task<TomlScanResult> ValidateAsync(
        string pluginsPath,
        string crashGenName,
        string gameName,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Validates TOML configuration with progress reporting.
    /// </summary>
    /// <param name="pluginsPath">
    /// The path to the F4SE/SKSE plugins directory.
    /// </param>
    /// <param name="crashGenName">
    /// The name of the crash generator mod.
    /// </param>
    /// <param name="gameName">
    /// The game name for conditional checks.
    /// </param>
    /// <param name="progress">Progress reporter for validation updates.</param>
    /// <param name="cancellationToken">A token to cancel the operation.</param>
    /// <returns>A <see cref="TomlScanResult"/> containing all detected issues.</returns>
    Task<TomlScanResult> ValidateWithProgressAsync(
        string pluginsPath,
        string crashGenName,
        string gameName,
        IProgress<TomlValidationProgress>? progress,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets a value from a TOML configuration file.
    /// </summary>
    /// <typeparam name="T">The type to parse the value as (bool, int, long, double, string).</typeparam>
    /// <param name="filePath">The path to the TOML file.</param>
    /// <param name="section">The section name (table name in TOML).</param>
    /// <param name="key">The key within the section.</param>
    /// <param name="cancellationToken">A token to cancel the operation.</param>
    /// <returns>The parsed value, or null if not found or parse failed.</returns>
    Task<T?> GetValueAsync<T>(
        string filePath,
        string section,
        string key,
        CancellationToken cancellationToken = default) where T : struct;

    /// <summary>
    /// Gets a string value from a TOML configuration file.
    /// </summary>
    /// <param name="filePath">The path to the TOML file.</param>
    /// <param name="section">The section name (table name in TOML).</param>
    /// <param name="key">The key within the section.</param>
    /// <param name="cancellationToken">A token to cancel the operation.</param>
    /// <returns>The string value, or null if not found.</returns>
    Task<string?> GetStringValueAsync(
        string filePath,
        string section,
        string key,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Checks if a TOML file exists and is valid.
    /// </summary>
    /// <param name="filePath">The path to the TOML file.</param>
    /// <param name="cancellationToken">A token to cancel the operation.</param>
    /// <returns>True if the file exists and parses successfully; otherwise, false.</returns>
    Task<bool> IsValidTomlFileAsync(
        string filePath,
        CancellationToken cancellationToken = default);
}

/// <summary>
/// Represents progress information during TOML validation.
/// </summary>
/// <param name="CurrentOperation">Description of the current operation.</param>
/// <param name="SettingsChecked">Number of settings checked so far.</param>
/// <param name="IssuesFound">Number of issues found so far.</param>
public record TomlValidationProgress(
    string CurrentOperation,
    int SettingsChecked,
    int IssuesFound);
