namespace Scanner111.Core.Models;

/// <summary>
/// Represents a game version with details
/// </summary>
public class GameVersion
{
    /// <summary>
    /// Version number (e.g., "1.10.163.0")
    /// </summary>
    public string Version { get; set; } = string.Empty;
    
    /// <summary>
    /// Display name for the version (e.g., "Pre-Next Gen Update")
    /// </summary>
    public string DisplayName { get; set; } = string.Empty;
    
    /// <summary>
    /// SHA256 hash of the game executable
    /// </summary>
    public string ExecutableHash { get; set; } = string.Empty;
    
    /// <summary>
    /// Whether this version is the most mod-compatible
    /// </summary>
    public bool IsMostCompatible { get; set; }
    
    /// <summary>
    /// Compatibility notes for this version
    /// </summary>
    public string CompatibilityNotes { get; set; } = string.Empty;
    
    /// <summary>
    /// Required Script Extender version (e.g., "0.6.23")
    /// </summary>
    public string RequiredXseVersion { get; set; } = string.Empty;
    
    /// <summary>
    /// Release date of this version
    /// </summary>
    public DateTime? ReleaseDate { get; set; }
}