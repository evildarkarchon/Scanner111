namespace Scanner111.Common.Models.Configuration;

/// <summary>
/// Represents a specific mod entry in the database.
/// </summary>
public record ModEntry
{
    /// <summary>
    /// Gets the name of the mod.
    /// </summary>
    public string Name { get; init; } = string.Empty;

    /// <summary>
    /// Gets the list of plugin patterns that identify this mod.
    /// </summary>
    public IReadOnlyList<string> PluginPatterns { get; init; } = Array.Empty<string>();

    /// <summary>
    /// Gets the category of the mod (e.g., "CONFLICT", "FREQUENTLY", "SOLUTIONS").
    /// </summary>
    public string Category { get; init; } = string.Empty;

    /// <summary>
    /// Gets the list of recommendations or warnings associated with this mod.
    /// </summary>
    public IReadOnlyList<string> Recommendations { get; init; } = Array.Empty<string>();
}

/// <summary>
/// Represents the database of known mods with specific behaviors or issues.
/// </summary>
public record ModDatabase
{
    /// <summary>
    /// Gets mods known to cause conflicts.
    /// </summary>
    public IReadOnlyList<ModEntry> ConflictingMods { get; init; } = Array.Empty<ModEntry>();

    /// <summary>
    /// Gets mods that frequently cause crashes.
    /// </summary>
    public IReadOnlyList<ModEntry> FrequentlyCrashing { get; init; } = Array.Empty<ModEntry>();

    /// <summary>
    /// Gets mods that offer solutions or fixes.
    /// </summary>
    public IReadOnlyList<ModEntry> WithSolutions { get; init; } = Array.Empty<ModEntry>();

    /// <summary>
    /// Gets core game mods or essential utilities.
    /// </summary>
    public IReadOnlyList<ModEntry> CoreMods { get; init; } = Array.Empty<ModEntry>();
}
