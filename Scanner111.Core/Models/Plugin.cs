namespace Scanner111.Core.Models;

/// <summary>
///     Represents a plugin found in crash logs
/// </summary>
public class Plugin
{
    /// <summary>
    ///     Plugin filename (e.g., "MyMod.esp")
    /// </summary>
    public string FileName { get; set; } = string.Empty;

    /// <summary>
    ///     Load order of the plugin (e.g., "FE:001")
    /// </summary>
    public string LoadOrder { get; set; } = string.Empty;

    /// <summary>
    ///     Whether this plugin is suspected to be problematic
    /// </summary>
    public bool IsSuspected { get; set; }

    /// <summary>
    ///     Reason why this plugin is suspected (if applicable)
    /// </summary>
    public string SuspicionReason { get; set; } = string.Empty;
}