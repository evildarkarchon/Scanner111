namespace Scanner111.Core.Models;

/// <summary>
///     Represents configuration for a specific game installation
/// </summary>
public class GameConfiguration
{
    /// <summary>
    ///     Name of the game (e.g., "Fallout 4", "Skyrim Special Edition")
    /// </summary>
    public string GameName { get; set; } = string.Empty;
    
    /// <summary>
    ///     Root installation path of the game
    /// </summary>
    public string RootPath { get; set; } = string.Empty;
    
    /// <summary>
    ///     Full path to the game executable
    /// </summary>
    public string ExecutablePath { get; set; } = string.Empty;
    
    /// <summary>
    ///     Path to the Documents folder containing INI files
    /// </summary>
    public string DocumentsPath { get; set; } = string.Empty;
    
    /// <summary>
    ///     Known file hashes for integrity verification (filepath -> SHA256 hash)
    /// </summary>
    public Dictionary<string, string> FileHashes { get; set; } = new();
    
    /// <summary>
    ///     Game platform (Steam, GOG, Epic, etc.)
    /// </summary>
    public string Platform { get; set; } = string.Empty;
    
    /// <summary>
    ///     Detected game version
    /// </summary>
    public string Version { get; set; } = string.Empty;
    
    /// <summary>
    ///     XSE (Script Extender) installation path if detected
    /// </summary>
    public string XsePath { get; set; } = string.Empty;
    
    /// <summary>
    ///     XSE version if detected
    /// </summary>
    public string XseVersion { get; set; } = string.Empty;
    
    /// <summary>
    ///     True if this is a valid game installation
    /// </summary>
    public bool IsValid => !string.IsNullOrEmpty(RootPath) && Directory.Exists(RootPath);
    
    /// <summary>
    ///     Gets the game's data directory path
    /// </summary>
    public string DataPath => Path.Combine(RootPath, "Data");
    
    /// <summary>
    ///     Gets the mods directory path (if using a mod manager)
    /// </summary>
    public string ModsPath { get; set; } = string.Empty;
    
    /// <summary>
    ///     Steam App ID if applicable
    /// </summary>
    public string SteamAppId { get; set; } = string.Empty;
    
    /// <summary>
    ///     Registry key path for this game installation
    /// </summary>
    public string RegistryPath { get; set; } = string.Empty;
}