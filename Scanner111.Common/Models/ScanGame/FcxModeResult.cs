using Scanner111.Common.Models.Reporting;

namespace Scanner111.Common.Models.ScanGame;

/// <summary>
/// Represents the result of running FCX (Full Configuration eXamination) mode.
/// FCX mode performs read-only detection of configuration issues in game files.
/// </summary>
public record FcxModeResult
{
    /// <summary>
    /// Gets whether FCX mode is enabled.
    /// </summary>
    public bool IsEnabled { get; init; }

    /// <summary>
    /// Gets all detected INI configuration issues.
    /// </summary>
    public IReadOnlyList<ConfigIssue> IniConfigIssues { get; init; } = Array.Empty<ConfigIssue>();

    /// <summary>
    /// Gets all detected console command issues that may slow startup.
    /// </summary>
    public IReadOnlyList<ConsoleCommandIssue> ConsoleCommandIssues { get; init; } = Array.Empty<ConsoleCommandIssue>();

    /// <summary>
    /// Gets all detected VSync settings.
    /// </summary>
    public IReadOnlyList<VSyncIssue> VSyncIssues { get; init; } = Array.Empty<VSyncIssue>();

    /// <summary>
    /// Gets all detected TOML configuration issues.
    /// </summary>
    public IReadOnlyList<ConfigIssue> TomlConfigIssues { get; init; } = Array.Empty<ConfigIssue>();

    /// <summary>
    /// Gets a value indicating whether any issues were detected.
    /// </summary>
    public bool HasIssues =>
        IniConfigIssues.Count > 0 ||
        ConsoleCommandIssues.Count > 0 ||
        VSyncIssues.Count > 0 ||
        TomlConfigIssues.Count > 0;

    /// <summary>
    /// Gets the total number of issues detected.
    /// </summary>
    public int TotalIssueCount =>
        IniConfigIssues.Count +
        ConsoleCommandIssues.Count +
        VSyncIssues.Count +
        TomlConfigIssues.Count;

    /// <summary>
    /// Gets the generated report fragment for inclusion in crash log reports.
    /// </summary>
    public ReportFragment? ReportFragment { get; init; }

    /// <summary>
    /// Creates an empty result for when FCX mode is disabled.
    /// </summary>
    public static FcxModeResult Disabled { get; } = new()
    {
        IsEnabled = false
    };
}
