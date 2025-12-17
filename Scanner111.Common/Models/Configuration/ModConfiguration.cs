namespace Scanner111.Common.Models.Configuration;

/// <summary>
/// Configuration for mod detection loaded from YAML files.
/// Contains mappings for single mods, mod conflicts, and important mods.
/// </summary>
public record ModConfiguration
{
    /// <summary>
    /// Gets mods that can cause frequent crashes.
    /// Key is the mod identifier pattern, value is the warning message.
    /// </summary>
    public Dictionary<string, string> FrequentCrashMods { get; init; } = new();

    /// <summary>
    /// Gets mods with known solutions or community patches.
    /// Key is the mod identifier pattern, value is the warning message.
    /// </summary>
    public Dictionary<string, string> SolutionMods { get; init; } = new();

    /// <summary>
    /// Gets mods patched through OPC installer.
    /// Key is the mod identifier pattern, value is the warning message.
    /// </summary>
    public Dictionary<string, string> OpcPatchedMods { get; init; } = new();

    /// <summary>
    /// Gets mod conflicts (pairs that shouldn't be used together).
    /// Key is "mod1 | mod2" format, value is the warning message.
    /// </summary>
    public Dictionary<string, string> ConflictingMods { get; init; } = new();

    /// <summary>
    /// Gets important/core mods that players should have installed.
    /// Key is "modId | DisplayName" format, value is the recommendation message.
    /// </summary>
    public Dictionary<string, string> ImportantMods { get; init; } = new();

    /// <summary>
    /// Creates an empty mod configuration.
    /// </summary>
    public static ModConfiguration Empty => new();
}
