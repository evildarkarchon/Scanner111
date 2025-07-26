using Scanner111.Core.Infrastructure;

namespace Scanner111.Core.Models;

/// <summary>
///     Represents a parsed crash log from Bethesda games using Buffout 4/Crash Logger format
/// </summary>
public class CrashLog
{
    // Static properties for FCX functionality
    public static string GameRootPath { get; set; } = string.Empty;
    public static string PluginsDirectory { get; set; } = string.Empty;
    public static string IniDirectory { get; set; } = string.Empty;
    public static GameType Game { get; set; }

    /// <summary>
    ///     Full path to the crash log file
    /// </summary>
    public string FilePath { get; init; } = string.Empty;

    /// <summary>
    ///     Just the filename without path
    /// </summary>
    public string FileName => Path.GetFileName(FilePath);

    /// <summary>
    ///     Original lines from the crash log file
    /// </summary>
    public List<string> OriginalLines { get; set; } = [];

    /// <summary>
    ///     Full content as a single string
    /// </summary>
    public string Content => string.Join("\n", OriginalLines);

    // Parsed sections
    /// <summary>
    ///     Main error message extracted from the crash log
    /// </summary>
    public string MainError { get; set; } = string.Empty;

    /// <summary>
    ///     Call stack lines from the crash
    /// </summary>
    public List<string> CallStack { get; set; } = [];

    /// <summary>
    ///     Plugin information: filename -> loadOrder
    /// </summary>
    public Dictionary<string, string> Plugins { get; set; } = [];

    /// <summary>
    ///     XSE (F4SE/SKSE) modules loaded in the crash
    /// </summary>
    public HashSet<string> XseModules { get; set; } = [];

    /// <summary>
    ///     Crash generator configuration settings extracted from the log
    /// </summary>
    public Dictionary<string, object> CrashgenSettings { get; set; } = [];

    /// <summary>
    ///     Version of the crash generator (e.g., Buffout 4 version)
    /// </summary>
    public string CrashGenVersion { get; set; } = string.Empty;

    /// <summary>
    ///     Timestamp when the crash occurred
    /// </summary>
    public DateTime? CrashTime { get; set; }

    // Validation properties
    /// <summary>
    ///     True if the crash log contains enough information to be analyzed
    /// </summary>
    public bool IsComplete => Plugins.Count > 0;

    /// <summary>
    ///     True if the crash log contains an error message
    /// </summary>
    public bool HasError => !string.IsNullOrEmpty(MainError);

    /// <summary>
    ///     Game version extracted from the crash log
    /// </summary>
    public string GameVersion { get; set; } = string.Empty;

    /// <summary>
    ///     True if the crash log is incomplete or truncated
    /// </summary>
    public bool IsIncomplete { get; set; }

    // FCX-specific metadata
    /// <summary>
    ///     Detected game type (Fallout4, Skyrim, etc.)
    /// </summary>
    public string GameType { get; set; } = string.Empty;

    /// <summary>
    ///     Detected game installation path
    /// </summary>
    public string GamePath { get; set; } = string.Empty;

    /// <summary>
    ///     Detected game platform (Steam, GOG, Epic, etc.)
    /// </summary>
    public string GamePlatform { get; set; } = string.Empty;

    /// <summary>
    ///     XSE (F4SE/SKSE) version if detected
    /// </summary>
    public string XseVersion { get; set; } = string.Empty;

    /// <summary>
    ///     Documents folder path for INI files
    /// </summary>
    public string DocumentsPath { get; set; } = string.Empty;

    /// <summary>
    ///     Executable file hash (SHA256) for version verification
    /// </summary>
    public string ExecutableHash { get; set; } = string.Empty;

    /// <summary>
    ///     Release memory used by original lines after analysis is complete
    /// </summary>
    public void DisposeOriginalLines()
    {
        OriginalLines.Clear();
        OriginalLines.TrimExcess();
    }

    /// <summary>
    ///     Parse a crash log from file asynchronously
    /// </summary>
    public static async Task<CrashLog?> ParseAsync(string filePath, CancellationToken cancellationToken = default)
    {
        return await CrashLogParser.ParseAsync(filePath, cancellationToken);
    }
}