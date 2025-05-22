using System.Collections.Generic;

namespace Scanner111.ClassicLib.ScanLog.Models;

/// <summary>
/// Represents the different segments extracted from a crash log.
/// </summary>
public record CrashLogSegments
{
    /// <summary>
    /// Gets or sets the game version from the crash log.
    /// </summary>
    public string GameVersion { get; init; } = string.Empty;

    /// <summary>
    /// Gets or sets the crash generator version.
    /// </summary>
    public string Crashgen { get; init; } = string.Empty;

    /// <summary>
    /// Gets or sets the main error message from the crash log.
    /// </summary>
    public string MainError { get; init; } = string.Empty;

    /// <summary>
    /// Gets or sets the crash generator segment lines.
    /// </summary>
    public List<string> CrashgenSegment { get; init; } = [];

    /// <summary>
    /// Gets or sets the system information segment lines.
    /// </summary>
    public List<string> SystemSegment { get; init; } = [];

    /// <summary>
    /// Gets or sets the call stack segment lines.
    /// </summary>
    public List<string> CallStackSegment { get; init; } = [];

    /// <summary>
    /// Gets or sets the loaded modules segment lines.
    /// </summary>
    public List<string> AllModulesSegment { get; init; } = [];

    /// <summary>
    /// Gets or sets the script extender modules segment lines.
    /// </summary>
    public List<string> XseModulesSegment { get; init; } = [];

    /// <summary>
    /// Gets or sets the plugins segment lines.
    /// </summary>
    public List<string> PluginsSegment { get; init; } = [];
}
