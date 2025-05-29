namespace Scanner111.Models.CrashLog;

/// <summary>
/// Contains information extracted from a crash log file header
/// </summary>
public class CrashLogInfo
{
    /// <summary>
    /// The full path to the crash log file
    /// </summary>
    public string FilePath { get; set; } = "";

    /// <summary>
    /// The name of the game (e.g., "Fallout 4", "Skyrim Special Edition")
    /// </summary>
    public string GameName { get; set; } = "";

    /// <summary>
    /// The version of the game (e.g., "v1.2.72")
    /// </summary>
    public string GameVersion { get; set; } = "";

    /// <summary>
    /// The name of the crash generator (e.g., "Buffout 4", "Crash Logger")
    /// </summary>
    public string CrashGenerator { get; set; } = "";

    /// <summary>
    /// The version of the crash generator (e.g., "v1.31.1")
    /// </summary>
    public string CrashGeneratorVersion { get; set; } = "";

    /// <summary>
    /// The full first line from the crash log
    /// </summary>
    public string GameLine { get; set; } = "";

    /// <summary>
    /// The full second line from the crash log
    /// </summary>
    public string CrashGeneratorLine { get; set; } = "";

    /// <summary>
    /// Whether this crash log is from a VR version of the game
    /// </summary>
    public bool IsVrVersion { get; set; }

    /// <summary>
    /// The combination key for this crash log (GameName|CrashGenerator)
    /// </summary>
    public string CombinationKey => $"{GameName}|{CrashGenerator}";
}

/// <summary>
/// Represents a supported game and crash generator combination
/// </summary>
public class SupportedCombination
{
    /// <summary>
    /// The name of the game
    /// </summary>
    public string GameName { get; set; } = "";

    /// <summary>
    /// The name of the crash generator
    /// </summary>
    public string CrashGenerator { get; set; } = "";

    /// <summary>
    /// Whether this combination supports VR versions
    /// </summary>
    public bool SupportsVr { get; set; }

    /// <summary>
    /// Optional description of this combination
    /// </summary>
    public string Description { get; set; } = "";

    /// <summary>
    /// Whether this combination is currently enabled
    /// </summary>
    public bool IsEnabled { get; set; } = true;

    /// <summary>
    /// The combination key (GameName|CrashGenerator)
    /// </summary>
    public string Key => $"{GameName}|{CrashGenerator}";
}