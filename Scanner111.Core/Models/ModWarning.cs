using Scanner111.Core.Analysis;

namespace Scanner111.Core.Models;

/// <summary>
///     Represents a warning for a problematic mod detected in crash logs.
///     Immutable record for thread-safety during concurrent analysis.
/// </summary>
public sealed record ModWarning
{
    /// <summary>
    ///     Gets the name or identifier of the problematic mod.
    /// </summary>
    public required string ModName { get; init; }

    /// <summary>
    ///     Gets the warning message explaining the issue with this mod.
    /// </summary>
    public required string Warning { get; init; }

    /// <summary>
    ///     Gets the severity level of this mod warning.
    /// </summary>
    public AnalysisSeverity Severity { get; init; } = AnalysisSeverity.Warning;

    /// <summary>
    ///     Gets the plugin ID or identifier found in the crash log.
    /// </summary>
    public string? PluginId { get; init; }

    /// <summary>
    ///     Gets the category of this mod warning (e.g., "Frequent", "Performance", "Stability").
    /// </summary>
    public string? Category { get; init; }

    /// <summary>
    ///     Creates a mod warning from detection data.
    /// </summary>
    public static ModWarning Create(
        string modName,
        string warning,
        AnalysisSeverity severity = AnalysisSeverity.Warning,
        string? pluginId = null,
        string? category = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(modName);
        ArgumentException.ThrowIfNullOrWhiteSpace(warning);

        return new ModWarning
        {
            ModName = modName,
            Warning = warning,
            Severity = severity,
            PluginId = pluginId,
            Category = category
        };
    }
}