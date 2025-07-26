namespace Scanner111.Core.Models.Yaml;

/// <summary>
/// Represents game information with support for multiple game versions (e.g., Pre-NG, NG)
/// </summary>
public class GameInfoV2
{
    /// <summary>
    /// Display name of the game (e.g., "Fallout 4")
    /// </summary>
    public string MainRootName { get; set; } = string.Empty;
    
    /// <summary>
    /// Documents folder name (e.g., "Fallout4")
    /// </summary>
    public string MainDocsName { get; set; } = string.Empty;
    
    /// <summary>
    /// Steam App ID
    /// </summary>
    public int MainSteamId { get; set; }
    
    /// <summary>
    /// Version-specific information (keyed by version identifier like "pre_ng", "next_gen")
    /// </summary>
    public Dictionary<string, GameVersionInfo> Versions { get; set; } = new();
    
    // Common crash generator settings
    public string CrashgenAcronym { get; set; } = string.Empty;
    public string CrashgenLogName { get; set; } = string.Empty;
    public string CrashgenDllFile { get; set; } = string.Empty;
    public List<string> CrashgenIgnore { get; set; } = new();
    
    // Common XSE settings
    public string XseAcronym { get; set; } = string.Empty;
    public string XseFullName { get; set; } = string.Empty;
    public int XseFileCount { get; set; }
}