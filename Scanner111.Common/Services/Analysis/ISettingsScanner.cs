using Scanner111.Common.Models.Analysis;

namespace Scanner111.Common.Services.Analysis;

/// <summary>
/// Provides settings validation functionality for crash logs.
/// </summary>
public interface ISettingsScanner
{
    /// <summary>
    /// Scans crash log settings and compares them against expected settings.
    /// </summary>
    /// <param name="compatibilitySegment">The Compatibility segment containing crash logger settings.</param>
    /// <param name="expectedSettings">The expected game settings.</param>
    /// <param name="cancellationToken">A token to cancel the operation.</param>
    /// <returns>A <see cref="SettingsScanResult"/> with validation results.</returns>
    Task<SettingsScanResult> ScanAsync(
        LogSegment? compatibilitySegment,
        GameSettings expectedSettings,
        CancellationToken cancellationToken = default);
}

/// <summary>
/// Result of settings validation scanning.
/// </summary>
public record SettingsScanResult
{
    /// <summary>
    /// Gets the detected settings and their values.
    /// </summary>
    public IReadOnlyDictionary<string, string> DetectedSettings { get; init; } =
        new Dictionary<string, string>();

    /// <summary>
    /// Gets the list of settings that don't match recommended values.
    /// </summary>
    public IReadOnlyList<string> Misconfigurations { get; init; } = Array.Empty<string>();

    /// <summary>
    /// Gets the list of warnings about settings issues.
    /// </summary>
    public IReadOnlyList<string> Warnings { get; init; } = Array.Empty<string>();

    /// <summary>
    /// Gets the detected crash logger version.
    /// </summary>
    public string? DetectedVersion { get; init; }

    /// <summary>
    /// Gets a value indicating whether the crash logger version is outdated.
    /// </summary>
    public bool IsOutdated { get; init; }
}
