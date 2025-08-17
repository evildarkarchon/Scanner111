namespace Scanner111.Core.Models;

/// <summary>
///     Represents the results of a comprehensive game scan.
/// </summary>
public class GameScanResult
{
    /// <summary>
    ///     Gets or sets the timestamp when the scan was performed.
    /// </summary>
    public DateTime Timestamp { get; set; } = DateTime.UtcNow;

    /// <summary>
    ///     Gets or sets the Crash Generator check results.
    /// </summary>
    public string CrashGenResults { get; set; } = string.Empty;

    /// <summary>
    ///     Gets or sets the XSE plugin validation results.
    /// </summary>
    public string XsePluginResults { get; set; } = string.Empty;

    /// <summary>
    ///     Gets or sets the mod INI scan results.
    /// </summary>
    public string ModIniResults { get; set; } = string.Empty;

    /// <summary>
    ///     Gets or sets the Wrye Bash checker results.
    /// </summary>
    public string WryeBashResults { get; set; } = string.Empty;

    /// <summary>
    ///     Gets or sets whether any issues were found.
    /// </summary>
    public bool HasIssues { get; set; }

    /// <summary>
    ///     Gets or sets a summary of critical issues found.
    /// </summary>
    public List<string> CriticalIssues { get; set; } = new();

    /// <summary>
    ///     Gets or sets a summary of warnings found.
    /// </summary>
    public List<string> Warnings { get; set; } = new();

    /// <summary>
    ///     Gets the complete report as a formatted string.
    /// </summary>
    public string GetFullReport()
    {
        var report = new StringBuilder();
        report.AppendLine("=== GAME SCAN RESULTS ===");
        report.AppendLine($"Scan performed at: {Timestamp:yyyy-MM-dd HH:mm:ss}");
        report.AppendLine();

        if (!string.IsNullOrWhiteSpace(CrashGenResults))
        {
            report.AppendLine("--- Crash Generator Check ---");
            report.AppendLine(CrashGenResults);
        }

        if (!string.IsNullOrWhiteSpace(XsePluginResults))
        {
            report.AppendLine("--- XSE Plugin Validation ---");
            report.AppendLine(XsePluginResults);
        }

        if (!string.IsNullOrWhiteSpace(ModIniResults))
        {
            report.AppendLine("--- Mod INI Scan ---");
            report.AppendLine(ModIniResults);
        }

        if (!string.IsNullOrWhiteSpace(WryeBashResults))
        {
            report.AppendLine("--- Wrye Bash Check ---");
            report.AppendLine(WryeBashResults);
        }

        if (CriticalIssues.Count > 0)
        {
            report.AppendLine("\n=== CRITICAL ISSUES ===");
            foreach (var issue in CriticalIssues) report.AppendLine($"❌ {issue}");
        }

        if (Warnings.Count > 0)
        {
            report.AppendLine("\n=== WARNINGS ===");
            foreach (var warning in Warnings) report.AppendLine($"⚠️ {warning}");
        }

        return report.ToString();
    }
}