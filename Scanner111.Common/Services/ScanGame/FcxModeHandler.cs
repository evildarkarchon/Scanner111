using Scanner111.Common.Models.Reporting;
using Scanner111.Common.Models.ScanGame;

namespace Scanner111.Common.Services.ScanGame;

/// <summary>
/// Handles FCX (Full Configuration eXamination) mode operations.
/// Performs read-only detection of configuration issues without modifying files.
/// </summary>
public sealed class FcxModeHandler : IFcxModeHandler
{
    private readonly IIniValidator _iniValidator;

    /// <summary>
    /// Initializes a new instance of the <see cref="FcxModeHandler"/> class.
    /// </summary>
    /// <param name="iniValidator">The INI validator service.</param>
    public FcxModeHandler(IIniValidator iniValidator)
    {
        _iniValidator = iniValidator ?? throw new ArgumentNullException(nameof(iniValidator));
    }

    /// <inheritdoc/>
    public async Task<FcxModeResult> CheckAsync(
        string? gameRootPath,
        string gameName,
        bool fcxEnabled,
        CancellationToken cancellationToken = default)
    {
        if (!fcxEnabled)
        {
            var disabledResult = FcxModeResult.Disabled;
            return disabledResult with
            {
                ReportFragment = CreateReportFragment(disabledResult)
            };
        }

        if (string.IsNullOrEmpty(gameRootPath) || !Directory.Exists(gameRootPath))
        {
            var noPathResult = new FcxModeResult { IsEnabled = true };
            return noPathResult with
            {
                ReportFragment = CreateReportFragment(noPathResult)
            };
        }

        // Run INI validation
        var iniResult = await _iniValidator.ScanAsync(gameRootPath, gameName, cancellationToken)
            .ConfigureAwait(false);

        return FromScanResults(iniResult, null, fcxEnabled);
    }

    /// <inheritdoc/>
    public FcxModeResult FromScanResults(
        IniScanResult? iniResult,
        TomlScanResult? tomlResult,
        bool fcxEnabled)
    {
        if (!fcxEnabled)
        {
            var disabledResult = FcxModeResult.Disabled;
            return disabledResult with
            {
                ReportFragment = CreateReportFragment(disabledResult)
            };
        }

        var iniConfigIssues = iniResult?.ConfigIssues ?? Array.Empty<ConfigIssue>();
        var consoleCommandIssues = iniResult?.ConsoleCommandIssues ?? Array.Empty<ConsoleCommandIssue>();
        var vsyncIssues = iniResult?.VSyncIssues ?? Array.Empty<VSyncIssue>();
        var tomlConfigIssues = tomlResult?.ConfigIssues ?? Array.Empty<ConfigIssue>();

        var result = new FcxModeResult
        {
            IsEnabled = true,
            IniConfigIssues = iniConfigIssues,
            ConsoleCommandIssues = consoleCommandIssues,
            VSyncIssues = vsyncIssues,
            TomlConfigIssues = tomlConfigIssues
        };

        return result with
        {
            ReportFragment = CreateReportFragment(result)
        };
    }

    /// <inheritdoc/>
    public ReportFragment CreateReportFragment(FcxModeResult result)
    {
        var lines = new List<string>();

        if (result.IsEnabled)
        {
            lines.Add("## FCX Mode Analysis");
            lines.Add(string.Empty);
            lines.Add("> **NOTICE:** FCX MODE IS ENABLED - Configuration issues will be detected but NOT modified.");
            lines.Add(string.Empty);

            if (!result.HasIssues)
            {
                lines.Add("No configuration issues detected.");
                lines.Add(string.Empty);
            }
            else
            {
                lines.Add($"**{result.TotalIssueCount} configuration issue(s) detected:**");
                lines.Add(string.Empty);

                // Add INI configuration issues
                if (result.IniConfigIssues.Count > 0)
                {
                    lines.Add("### INI Configuration Issues");
                    lines.Add(string.Empty);

                    foreach (var issue in result.IniConfigIssues)
                    {
                        var severityIcon = GetSeverityIcon(issue.Severity);
                        lines.Add($"{severityIcon} **{issue.FileName}** [{issue.Section}] {issue.Setting}");
                        lines.Add($"  - Current: `{issue.CurrentValue}`");
                        lines.Add($"  - Recommended: `{issue.RecommendedValue}`");
                        lines.Add($"  - {issue.Description}");
                        lines.Add(string.Empty);
                    }
                }

                // Add console command issues
                if (result.ConsoleCommandIssues.Count > 0)
                {
                    lines.Add("### Console Command Warnings");
                    lines.Add(string.Empty);
                    lines.Add("> Console commands in INI files can slow down game startup.");
                    lines.Add(string.Empty);

                    foreach (var issue in result.ConsoleCommandIssues)
                    {
                        lines.Add($"- **{issue.FileName}**: `{issue.CommandValue}`");
                    }
                    lines.Add(string.Empty);
                }

                // Add VSync issues
                if (result.VSyncIssues.Count > 0)
                {
                    lines.Add("### VSync Settings Detected");
                    lines.Add(string.Empty);
                    lines.Add("> Multiple VSync settings may conflict. Consider using a single VSync control.");
                    lines.Add(string.Empty);

                    foreach (var issue in result.VSyncIssues)
                    {
                        var status = issue.IsEnabled ? "Enabled" : "Disabled";
                        lines.Add($"- **{issue.FileName}** [{issue.Section}] {issue.Setting}: {status}");
                    }
                    lines.Add(string.Empty);
                }

                // Add TOML issues
                if (result.TomlConfigIssues.Count > 0)
                {
                    lines.Add("### TOML Configuration Issues");
                    lines.Add(string.Empty);

                    foreach (var issue in result.TomlConfigIssues)
                    {
                        var severityIcon = GetSeverityIcon(issue.Severity);
                        lines.Add($"{severityIcon} **{issue.FileName}** [{issue.Section}] {issue.Setting}");
                        lines.Add($"  - Current: `{issue.CurrentValue}`");
                        lines.Add($"  - Recommended: `{issue.RecommendedValue}`");
                        lines.Add($"  - {issue.Description}");
                        lines.Add(string.Empty);
                    }
                }
            }
        }
        else
        {
            lines.Add("## FCX Mode");
            lines.Add(string.Empty);
            lines.Add("> **NOTICE:** FCX MODE IS DISABLED.");
            lines.Add("> Enable FCX Mode in settings to detect problems in mod and game configuration files.");
            lines.Add(string.Empty);
        }

        return ReportFragment.FromLines(lines.ToArray());
    }

    private static string GetSeverityIcon(ConfigIssueSeverity severity)
    {
        return severity switch
        {
            ConfigIssueSeverity.Info => "[i]",
            ConfigIssueSeverity.Warning => "[!]",
            ConfigIssueSeverity.Error => "[X]",
            _ => "[?]"
        };
    }
}
