namespace Scanner111.Core.Models.Yaml;

/// <summary>
/// Represents version-specific information for a game (e.g., Pre-NG vs Next Gen)
/// </summary>
public class GameVersionInfo
{
    /// <summary>
    /// Friendly name for this version (e.g., "Pre-Next Gen", "Next Gen Update")
    /// </summary>
    public string Name { get; set; } = string.Empty;
    
    /// <summary>
    /// Game version number (e.g., "1.10.163", "1.10.984")
    /// </summary>
    public string GameVersion { get; set; } = string.Empty;
    
    /// <summary>
    /// SHA-256 hash of the game executable for this version
    /// </summary>
    public string ExeHash { get; set; } = string.Empty;
    
    /// <summary>
    /// Latest compatible XSE version for this game version
    /// </summary>
    public string XseVersion { get; set; } = string.Empty;
    
    /// <summary>
    /// Latest compatible Buffout 4 version for this game version
    /// </summary>
    public string BuffoutLatest { get; set; } = string.Empty;
    
    /// <summary>
    /// Version-specific crash generator ignore list (optional - overrides default)
    /// </summary>
    public List<string>? CrashgenIgnore { get; set; }
    
    /// <summary>
    /// XSE script hashes for this version
    /// </summary>
    public Dictionary<string, string> XseScripts { get; set; } = new();
}