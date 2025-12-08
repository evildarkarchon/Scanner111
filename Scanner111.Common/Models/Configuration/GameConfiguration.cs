namespace Scanner111.Common.Models.Configuration;

/// <summary>
/// Configuration specific to a supported game.
/// </summary>
public record GameConfiguration
{
    /// <summary>
    /// Gets the name of the game (e.g., "Fallout 4", "Skyrim Special Edition").
    /// </summary>
    public string GameName { get; init; } = string.Empty;

    /// <summary>
    /// Gets the acronym used for the script extender (e.g., "F4SE", "SKSE").
    /// </summary>
    public string XseAcronym { get; init; } = string.Empty;

    /// <summary>
    /// Gets hints or tips specific to the game.
    /// </summary>
    public IReadOnlyList<string> GameHints { get; init; } = Array.Empty<string>();

    /// <summary>
    /// Gets a list of specific records to look for.
    /// </summary>
    public IReadOnlyList<string> RecordList { get; init; } = Array.Empty<string>();

    /// <summary>
    /// Gets known crash generator versions mapped to their descriptions or handling logic.
    /// </summary>
    public Dictionary<string, string> CrashGeneratorVersions { get; init; } = new();
}
