namespace Scanner111.Common.Models.Analysis;

/// <summary>
/// Represents the result of mod detection analysis on a crash log.
/// </summary>
public record ModDetectionResult
{
    /// <summary>
    /// Gets the list of problematic single mods detected.
    /// </summary>
    public IReadOnlyList<DetectedMod> ProblematicMods { get; init; } = Array.Empty<DetectedMod>();

    /// <summary>
    /// Gets the list of detected mod conflicts (pairs of mods that shouldn't be used together).
    /// </summary>
    public IReadOnlyList<ModConflict> Conflicts { get; init; } = Array.Empty<ModConflict>();

    /// <summary>
    /// Gets the list of important mods and their installation status.
    /// </summary>
    public IReadOnlyList<ImportantModStatus> ImportantMods { get; init; } = Array.Empty<ImportantModStatus>();

    /// <summary>
    /// Gets a value indicating whether any issues were detected.
    /// </summary>
    public bool HasIssues => ProblematicMods.Count > 0 || Conflicts.Count > 0;
}

/// <summary>
/// Represents a detected problematic mod.
/// </summary>
public record DetectedMod
{
    /// <summary>
    /// Gets the name of the detected mod.
    /// </summary>
    public string ModName { get; init; } = string.Empty;

    /// <summary>
    /// Gets the plugin that matched the mod pattern.
    /// </summary>
    public string MatchedPlugin { get; init; } = string.Empty;

    /// <summary>
    /// Gets the FormID prefix of the matched plugin.
    /// </summary>
    public string PluginFormId { get; init; } = string.Empty;

    /// <summary>
    /// Gets the warning message for this mod.
    /// </summary>
    public string Warning { get; init; } = string.Empty;

    /// <summary>
    /// Gets the category of the mod issue (e.g., "Frequent Crashes", "Known Issues").
    /// </summary>
    public ModCategory Category { get; init; } = ModCategory.Unknown;
}

/// <summary>
/// Represents a conflict between two mods.
/// </summary>
public record ModConflict
{
    /// <summary>
    /// Gets the first mod in the conflict.
    /// </summary>
    public string Mod1 { get; init; } = string.Empty;

    /// <summary>
    /// Gets the second mod in the conflict.
    /// </summary>
    public string Mod2 { get; init; } = string.Empty;

    /// <summary>
    /// Gets the warning message for this conflict.
    /// </summary>
    public string Warning { get; init; } = string.Empty;
}

/// <summary>
/// Represents the installation status of an important mod.
/// </summary>
public record ImportantModStatus
{
    /// <summary>
    /// Gets the display name of the mod.
    /// </summary>
    public string DisplayName { get; init; } = string.Empty;

    /// <summary>
    /// Gets a value indicating whether the mod is installed.
    /// </summary>
    public bool IsInstalled { get; init; }

    /// <summary>
    /// Gets a value indicating whether there is a GPU compatibility concern.
    /// </summary>
    public bool HasGpuConcern { get; init; }

    /// <summary>
    /// Gets the warning message (if not installed or has GPU concern).
    /// </summary>
    public string? Warning { get; init; }
}

/// <summary>
/// Categories for problematic mods.
/// </summary>
public enum ModCategory
{
    /// <summary>
    /// Unknown category.
    /// </summary>
    Unknown,

    /// <summary>
    /// Mods that can cause frequent crashes.
    /// </summary>
    FrequentCrashes,

    /// <summary>
    /// Mods with known solutions or patches.
    /// </summary>
    HasSolution,

    /// <summary>
    /// Mods patched through OPC installer.
    /// </summary>
    OpcPatched
}
