using Scanner111.Core.Analysis;

namespace Scanner111.Core.Models;

/// <summary>
///     Represents a conflict between two mods that should not be used together.
///     Immutable record for thread-safety during concurrent analysis.
/// </summary>
public sealed record ModConflict
{
    /// <summary>
    ///     Gets the name or identifier of the first conflicting mod.
    /// </summary>
    public required string FirstMod { get; init; }

    /// <summary>
    ///     Gets the name or identifier of the second conflicting mod.
    /// </summary>
    public required string SecondMod { get; init; }

    /// <summary>
    ///     Gets the conflict warning message explaining why these mods conflict.
    /// </summary>
    public required string ConflictWarning { get; init; }

    /// <summary>
    ///     Gets the severity level of this mod conflict.
    /// </summary>
    public AnalysisSeverity Severity { get; init; } = AnalysisSeverity.Warning;

    /// <summary>
    ///     Gets the plugin IDs found in the crash log for the conflicting mods.
    /// </summary>
    public IReadOnlyList<string> FoundPluginIds { get; init; } = Array.Empty<string>();

    /// <summary>
    ///     Creates a mod conflict from detection data.
    /// </summary>
    public static ModConflict Create(
        string firstMod,
        string secondMod,
        string conflictWarning,
        AnalysisSeverity severity = AnalysisSeverity.Warning,
        IEnumerable<string>? foundPluginIds = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(firstMod);
        ArgumentException.ThrowIfNullOrWhiteSpace(secondMod);
        ArgumentException.ThrowIfNullOrWhiteSpace(conflictWarning);

        return new ModConflict
        {
            FirstMod = firstMod,
            SecondMod = secondMod,
            ConflictWarning = conflictWarning,
            Severity = severity,
            FoundPluginIds = foundPluginIds?.ToArray() ?? Array.Empty<string>()
        };
    }

    /// <summary>
    ///     Gets a display string for the conflicting mod pair.
    /// </summary>
    public string GetConflictPair() => $"{FirstMod} | {SecondMod}";
}