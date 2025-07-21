namespace Scanner111.Core.Models;

/// <summary>
///     Represents a FormID found in crash logs
/// </summary>
public class FormId
{
    /// <summary>
    ///     The FormID value (e.g., "00012345")
    /// </summary>
    public string Id { get; set; } = string.Empty;

    /// <summary>
    ///     The plugin that contains this FormID
    /// </summary>
    public string PluginName { get; set; } = string.Empty;

    /// <summary>
    ///     Description or name of the form (if resolved)
    /// </summary>
    public string Description { get; set; } = string.Empty;

    /// <summary>
    ///     Whether this FormID was successfully resolved
    /// </summary>
    public bool IsResolved { get; set; }
}