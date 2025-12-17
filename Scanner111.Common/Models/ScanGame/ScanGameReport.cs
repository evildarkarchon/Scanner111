using Scanner111.Common.Models.GameIntegrity;

namespace Scanner111.Common.Models.ScanGame;

/// <summary>
/// Represents a complete ScanGame report aggregating results from all scanners.
/// </summary>
/// <remarks>
/// <para>
/// This record combines results from multiple scanning operations:
/// <list type="bullet">
/// <item>Unpacked (loose) file scanning</item>
/// <item>BA2 archive scanning</item>
/// <item>INI configuration validation</item>
/// <item>TOML crash generator configuration validation</item>
/// <item>XSE installation and integrity checking</item>
/// <item>Game installation integrity checking</item>
/// </list>
/// </para>
/// <para>
/// Use <see cref="Services.Reporting.IScanGameReportBuilder"/> to generate
/// formatted markdown reports from this data.
/// </para>
/// </remarks>
public record ScanGameReport
{
    /// <summary>
    /// Gets the XSE acronym for the target game (e.g., "F4SE", "SKSE64").
    /// </summary>
    public string XseAcronym { get; init; } = "F4SE";

    /// <summary>
    /// Gets the display name of the target game (e.g., "Fallout 4", "Skyrim Special Edition").
    /// </summary>
    public string GameName { get; init; } = "Fallout 4";

    /// <summary>
    /// Gets the timestamp when the scan was performed.
    /// </summary>
    public DateTimeOffset ScanTimestamp { get; init; } = DateTimeOffset.Now;

    /// <summary>
    /// Gets the result of scanning unpacked (loose) mod files, if performed.
    /// </summary>
    public UnpackedScanResult? UnpackedResult { get; init; }

    /// <summary>
    /// Gets the result of scanning BA2 archive files, if performed.
    /// </summary>
    public BA2ScanResult? ArchivedResult { get; init; }

    /// <summary>
    /// Gets the result of scanning INI configuration files, if performed.
    /// </summary>
    public IniScanResult? IniResult { get; init; }

    /// <summary>
    /// Gets the result of scanning TOML crash generator configuration, if performed.
    /// </summary>
    public TomlScanResult? TomlResult { get; init; }

    /// <summary>
    /// Gets the result of checking XSE installation and integrity, if performed.
    /// </summary>
    public XseScanResult? XseResult { get; init; }

    /// <summary>
    /// Gets the result of checking game installation integrity, if performed.
    /// </summary>
    public GameIntegrityResult? IntegrityResult { get; init; }

    /// <summary>
    /// Gets a value indicating whether any issues were found across all scan results.
    /// </summary>
    public bool HasAnyIssues =>
        (UnpackedResult?.HasIssues ?? false) ||
        (ArchivedResult?.HasIssues ?? false) ||
        (IniResult?.HasIssues ?? false) ||
        (TomlResult?.HasIssues ?? false) ||
        (XseResult?.HasIssues ?? false) ||
        (IntegrityResult?.HasIssues ?? false);

    /// <summary>
    /// Gets a value indicating whether any scans were actually performed.
    /// </summary>
    public bool HasAnyResults =>
        UnpackedResult != null ||
        ArchivedResult != null ||
        IniResult != null ||
        TomlResult != null ||
        XseResult != null ||
        IntegrityResult != null;
}
