using System.Collections.Generic;

namespace Scanner111.ClassicLib.ScanLog.Models;

/// <summary>
/// Represents statistics collected during crash log scanning.
/// </summary>
public class ScanStatistics
{
    /// <summary>
    /// Gets or sets the total number of logs scanned.
    /// </summary>
    public int Scanned { get; set; }

    /// <summary>
    /// Gets or sets the number of logs with failed scans.
    /// </summary>
    public int Failed { get; set; }

    /// <summary>
    /// Gets or sets the number of incomplete logs.
    /// </summary>
    public int Incomplete { get; set; }

    /// <summary>
    /// Gets or sets the number of logs with successful matches.
    /// </summary>
    public int Matches { get; set; }

    /// <summary>
    /// Gets or sets the number of logs with no plugins detected.
    /// </summary>
    public int NoPlugins { get; set; }

    /// <summary>
    /// Gets or sets the list of files with failed scans.
    /// </summary>
    public List<string> FailedFiles { get; set; } = [];

    /// <summary>
    /// Gets or sets the common error patterns detected.
    /// </summary>
    public Dictionary<string, int> ErrorPatterns { get; set; } = [];

    /// <summary>
    /// Gets or sets the common plugin issues detected.
    /// </summary>
    public Dictionary<string, int> PluginIssues { get; set; } = [];
}
