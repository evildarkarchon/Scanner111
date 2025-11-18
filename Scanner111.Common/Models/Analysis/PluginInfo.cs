namespace Scanner111.Common.Models.Analysis;

/// <summary>
/// Represents information about a game plugin (mod) loaded during the crash.
/// Plugins are listed in load order, with their FormID prefix indicating their position.
/// </summary>
public record PluginInfo
{
    /// <summary>
    /// Gets the FormID prefix for this plugin.
    /// Examples: "E7" for regular plugins, "FE:000" for light plugins (.esl files).
    /// </summary>
    public string FormIdPrefix { get; init; } = string.Empty;

    /// <summary>
    /// Gets the name of the plugin file (e.g., "StartMeUp.esp").
    /// </summary>
    public string PluginName { get; init; } = string.Empty;

    /// <summary>
    /// Gets a value indicating whether this is a light plugin.
    /// Light plugins have FormID prefixes starting with "FE:".
    /// </summary>
    public bool IsLightPlugin => FormIdPrefix.StartsWith("FE:", StringComparison.OrdinalIgnoreCase);
}
