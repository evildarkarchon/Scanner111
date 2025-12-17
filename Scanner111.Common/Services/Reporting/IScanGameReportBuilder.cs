using Scanner111.Common.Models.GameIntegrity;
using Scanner111.Common.Models.Reporting;
using Scanner111.Common.Models.ScanGame;

namespace Scanner111.Common.Services.Reporting;

/// <summary>
/// Provides functionality for building formatted reports from ScanGame results.
/// </summary>
/// <remarks>
/// <para>
/// This service composes <see cref="ReportFragment"/>s from various ScanGame
/// result types (unpacked scans, archive scans, configuration checks, etc.)
/// into formatted markdown reports suitable for display or export.
/// </para>
/// <para>
/// The builder uses <see cref="ScanGameSections"/> internally for individual
/// section generation and composes them according to CLASSIC report conventions.
/// </para>
/// </remarks>
public interface IScanGameReportBuilder
{
    /// <summary>
    /// Builds a report section for unpacked (loose) mod file scan results.
    /// </summary>
    /// <param name="result">The unpacked scan result.</param>
    /// <param name="xseAcronym">The XSE acronym (e.g., "F4SE", "SKSE64").</param>
    /// <returns>A <see cref="ReportFragment"/> containing the formatted section, or empty if no issues.</returns>
    ReportFragment BuildUnpackedSection(UnpackedScanResult result, string xseAcronym);

    /// <summary>
    /// Builds a report section for BA2 archive scan results.
    /// </summary>
    /// <param name="result">The BA2 scan result.</param>
    /// <param name="xseAcronym">The XSE acronym (e.g., "F4SE", "SKSE64").</param>
    /// <returns>A <see cref="ReportFragment"/> containing the formatted section, or empty if no issues.</returns>
    ReportFragment BuildArchivedSection(BA2ScanResult result, string xseAcronym);

    /// <summary>
    /// Builds a report section for INI configuration scan results.
    /// </summary>
    /// <param name="result">The INI scan result.</param>
    /// <returns>A <see cref="ReportFragment"/> containing the formatted section, or empty if no issues.</returns>
    ReportFragment BuildIniSection(IniScanResult result);

    /// <summary>
    /// Builds a report section for TOML crash generator configuration results.
    /// </summary>
    /// <param name="result">The TOML scan result.</param>
    /// <returns>A <see cref="ReportFragment"/> containing the formatted section, or empty if no issues.</returns>
    ReportFragment BuildTomlSection(TomlScanResult result);

    /// <summary>
    /// Builds a report section for XSE installation status results.
    /// </summary>
    /// <param name="result">The XSE scan result.</param>
    /// <param name="xseAcronym">The XSE acronym (e.g., "F4SE", "SKSE64").</param>
    /// <returns>A <see cref="ReportFragment"/> containing the formatted section, or empty if no issues.</returns>
    ReportFragment BuildXseSection(XseScanResult result, string xseAcronym);

    /// <summary>
    /// Builds a report section for game installation integrity results.
    /// </summary>
    /// <param name="result">The game integrity result.</param>
    /// <returns>A <see cref="ReportFragment"/> containing the formatted section, or empty if no issues.</returns>
    ReportFragment BuildIntegritySection(GameIntegrityResult result);

    /// <summary>
    /// Builds a complete combined report from all ScanGame scan results.
    /// </summary>
    /// <param name="report">The aggregate scan report containing all results.</param>
    /// <returns>A <see cref="ReportFragment"/> containing the complete formatted report.</returns>
    /// <remarks>
    /// <para>
    /// The combined report includes:
    /// <list type="number">
    /// <item>Main header with game name and timestamp</item>
    /// <item>Unpacked file scan section (if has issues)</item>
    /// <item>Archived file scan section (if has issues)</item>
    /// <item>Configuration section (INI + TOML, if has issues)</item>
    /// <item>XSE status section (if has issues)</item>
    /// <item>Game integrity section (if has issues)</item>
    /// <item>Success message (if no issues found)</item>
    /// <item>Standard footer</item>
    /// </list>
    /// </para>
    /// </remarks>
    ReportFragment BuildCombinedReport(ScanGameReport report);
}
