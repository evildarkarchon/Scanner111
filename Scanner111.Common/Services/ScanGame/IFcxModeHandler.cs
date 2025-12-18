using Scanner111.Common.Models.Reporting;
using Scanner111.Common.Models.ScanGame;

namespace Scanner111.Common.Services.ScanGame;

/// <summary>
/// Handles FCX (Full Configuration eXamination) mode operations.
/// FCX mode performs read-only detection of configuration issues without modifying any files.
/// </summary>
/// <remarks>
/// <para>
/// FCX mode can be used during crash log analysis to include game configuration
/// status in the report. When enabled, it scans INI, TOML, and other configuration
/// files for known problematic settings and reports current vs recommended values.
/// </para>
/// <para>
/// This handler operates in read-only mode and never modifies configuration files.
/// All detected issues are reported with their current and recommended values,
/// allowing users to make informed decisions about configuration changes.
/// </para>
/// </remarks>
public interface IFcxModeHandler
{
    /// <summary>
    /// Checks configuration files and generates FCX mode results.
    /// </summary>
    /// <param name="gameRootPath">The root path of the game installation.</param>
    /// <param name="gameName">The game name for game-specific INI checks.</param>
    /// <param name="fcxEnabled">Whether FCX mode is enabled.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The FCX mode result containing all detected issues and a report fragment.</returns>
    Task<FcxModeResult> CheckAsync(
        string? gameRootPath,
        string gameName,
        bool fcxEnabled,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Converts existing scan results into an FCX mode result.
    /// Use this when a full game scan has already been performed.
    /// </summary>
    /// <param name="iniResult">The INI scan result from a game scan.</param>
    /// <param name="tomlResult">The TOML scan result from a game scan (optional).</param>
    /// <param name="fcxEnabled">Whether FCX mode is enabled.</param>
    /// <returns>The FCX mode result containing all detected issues and a report fragment.</returns>
    FcxModeResult FromScanResults(
        IniScanResult? iniResult,
        TomlScanResult? tomlResult,
        bool fcxEnabled);

    /// <summary>
    /// Generates a report fragment for the given FCX mode result.
    /// </summary>
    /// <param name="result">The FCX mode result.</param>
    /// <returns>A report fragment suitable for inclusion in crash log reports.</returns>
    ReportFragment CreateReportFragment(FcxModeResult result);
}
