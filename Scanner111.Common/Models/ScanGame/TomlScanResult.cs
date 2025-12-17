namespace Scanner111.Common.Models.ScanGame;

/// <summary>
/// Represents the result of scanning TOML configuration files for crash generator mods.
/// </summary>
/// <remarks>
/// <para>
/// This result type is used by <see cref="Services.ScanGame.ITomlValidator"/> to report
/// issues found in Buffout4 and similar crash generator TOML configuration files.
/// </para>
/// <para>
/// The validator checks for:
/// <list type="bullet">
/// <item>Missing or duplicate configuration files</item>
/// <item>Plugin conflicts (X-Cell, Achievements, Looks Menu)</item>
/// <item>Incorrect settings based on installed plugins</item>
/// </list>
/// </para>
/// </remarks>
public record TomlScanResult
{
    /// <summary>
    /// Gets the name of the crash generator being validated (e.g., "Buffout4").
    /// </summary>
    public string CrashGenName { get; init; } = "Buffout4";

    /// <summary>
    /// Gets the path to the configuration file that was scanned, if found.
    /// </summary>
    public string? ConfigFilePath { get; init; }

    /// <summary>
    /// Gets a value indicating whether a configuration file was found.
    /// </summary>
    public bool ConfigFileFound { get; init; }

    /// <summary>
    /// Gets a value indicating whether duplicate configuration files were detected.
    /// </summary>
    public bool HasDuplicateConfigs { get; init; }

    /// <summary>
    /// Gets the list of configuration issues detected.
    /// </summary>
    /// <remarks>
    /// Uses the same <see cref="ConfigIssue"/> type as INI validation for consistency.
    /// </remarks>
    public IReadOnlyList<ConfigIssue> ConfigIssues { get; init; } = Array.Empty<ConfigIssue>();

    /// <summary>
    /// Gets the list of installed plugins detected in the plugins directory.
    /// </summary>
    /// <remarks>
    /// These are DLL files found in the F4SE/SKSE plugins directory that influence
    /// which TOML settings should be checked.
    /// </remarks>
    public IReadOnlyList<string> InstalledPlugins { get; init; } = Array.Empty<string>();

    /// <summary>
    /// Gets the formatted message report for display.
    /// </summary>
    public string FormattedReport { get; init; } = string.Empty;

    /// <summary>
    /// Gets a value indicating whether any issues were found.
    /// </summary>
    public bool HasIssues =>
        ConfigIssues.Count > 0 ||
        HasDuplicateConfigs ||
        !ConfigFileFound;
}

/// <summary>
/// Represents a setting rule for TOML configuration validation.
/// </summary>
/// <remarks>
/// These rules define what settings should be checked based on installed plugins
/// and what values are recommended.
/// </remarks>
/// <param name="Section">The TOML section name (e.g., "Patches", "Compatibility").</param>
/// <param name="Key">The setting key within the section.</param>
/// <param name="DisplayName">Human-readable name for reporting.</param>
/// <param name="DesiredValue">The recommended value for this setting.</param>
/// <param name="Description">Description of why this setting matters.</param>
/// <param name="Reason">The reason for the recommended value.</param>
/// <param name="SpecialCase">Optional special case identifier for custom handling.</param>
public record TomlSettingRule(
    string Section,
    string Key,
    string DisplayName,
    object DesiredValue,
    string Description,
    string Reason,
    string? SpecialCase = null);
