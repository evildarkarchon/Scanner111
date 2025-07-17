namespace Scanner111.Core.Models;

/// <summary>
/// Main configuration class that matches the Python ClassicScanLogsInfo structure
/// IMPORTANT: Keep this class name as "ClassicScanLogsInfo" - referenced throughout codebase
/// </summary>
public class ClassicScanLogsInfo
{
    /// <summary>
    /// Version of the CLASSIC scanner
    /// </summary>
    public string ClassicVersion { get; set; } = "7.35.0";
    
    /// <summary>
    /// Name of the crash generator (e.g., "Buffout 4")
    /// </summary>
    public string CrashgenName { get; set; } = "Buffout 4";
    
    /// <summary>
    /// Game hints for detection
    /// </summary>
    public List<string> ClassicGameHints { get; set; } = new();
    
    /// <summary>
    /// Text to include in autoscan reports
    /// </summary>
    public string AutoscanText { get; set; } = string.Empty;
    
    // Suspect patterns from YAML configuration
    /// <summary>
    /// Suspect patterns for error messages: pattern -> description
    /// </summary>
    public Dictionary<string, string> SuspectsErrorList { get; set; } = new();
    
    /// <summary>
    /// Suspect patterns for stack traces: pattern -> description
    /// </summary>
    public Dictionary<string, string> SuspectsStackList { get; set; } = new();
    
    /// <summary>
    /// List of plugin names to ignore during analysis
    /// </summary>
    public List<string> IgnorePluginsList { get; set; } = new();
    
    // Named records patterns
    /// <summary>
    /// Named record patterns by type: recordType -> list of patterns
    /// </summary>
    public Dictionary<string, List<string>> NamedRecordsType { get; set; } = new();
}

/// <summary>
/// Plugin information
/// </summary>
public class Plugin
{
    /// <summary>
    /// Plugin filename
    /// </summary>
    public required string FileName { get; init; }
    
    /// <summary>
    /// Load order index
    /// </summary>
    public required string LoadOrder { get; init; }
    
    /// <summary>
    /// Whether this plugin should be ignored in analysis
    /// </summary>
    public bool IsIgnored { get; set; }
    
    /// <summary>
    /// Plugin type (ESM, ESP, etc.)
    /// </summary>
    public string Type { get; set; } = string.Empty;
}

/// <summary>
/// FormID information
/// </summary>
public class FormId
{
    /// <summary>
    /// Raw FormID value (e.g., "0014D0A2")
    /// </summary>
    public required string Value { get; init; }
    
    /// <summary>
    /// Associated plugin name if known
    /// </summary>
    public string? PluginName { get; set; }
    
    /// <summary>
    /// Description from database if available
    /// </summary>
    public string? Description { get; set; }
    
    /// <summary>
    /// Record type if known
    /// </summary>
    public string? RecordType { get; set; }
}

/// <summary>
/// Mod information
/// </summary>
public class ModInfo
{
    /// <summary>
    /// Mod name
    /// </summary>
    public required string Name { get; init; }
    
    /// <summary>
    /// Mod version if available
    /// </summary>
    public string? Version { get; set; }
    
    /// <summary>
    /// Associated plugin files
    /// </summary>
    public List<string> Plugins { get; set; } = new();
    
    /// <summary>
    /// Whether this mod is suspected of causing issues
    /// </summary>
    public bool IsSuspected { get; set; }
    
    /// <summary>
    /// Reason for suspicion if applicable
    /// </summary>
    public string? SuspectReason { get; set; }
}