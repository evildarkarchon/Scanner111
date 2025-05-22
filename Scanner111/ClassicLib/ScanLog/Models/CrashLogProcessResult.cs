using System.Collections.Generic;

namespace Scanner111.ClassicLib.ScanLog.Models;

/// <summary>
///     Represents the result of processing a single crash log.
/// </summary>
public class CrashLogProcessResult
{
    /// <summary>
    ///     Gets or sets the name of the crash log file.
    /// </summary>
    public string LogFileName { get; set; } = string.Empty;

    /// <summary>
    ///     Gets or sets the report generated for the crash log.
    /// </summary>
    public List<string> Report { get; set; } = [];

    /// <summary>
    ///     Gets or sets a value indicating whether the scan failed.
    /// </summary>
    public bool ScanFailed { get; set; }

    /// <summary>
    ///     Gets or sets various statistics collected during processing.
    /// </summary>
    public Dictionary<string, int> Statistics { get; set; } = [];

    /// <summary>
    ///     Gets or sets detected issues in the crash log.
    /// </summary>
    public List<string> DetectedIssues { get; set; } = [];

    /// <summary>
    ///     Gets or sets the main crash error message.
    /// </summary>
    public string MainError { get; set; } = string.Empty;
}