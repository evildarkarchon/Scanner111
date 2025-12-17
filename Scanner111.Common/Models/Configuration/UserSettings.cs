namespace Scanner111.Common.Models.Configuration;

/// <summary>
/// Represents user-configurable settings that are persisted across sessions.
/// These settings are stored in JSON format in the application data directory.
/// </summary>
public record UserSettings
{
    /// <summary>
    /// Gets the custom scan path for crash logs, if set by the user.
    /// When null or empty, the default scan paths will be used.
    /// </summary>
    public string? CustomScanPath { get; init; }

    /// <summary>
    /// Gets the path to the mods staging folder (e.g., MO2 mods directory).
    /// Used for mod-related analysis and recommendations.
    /// </summary>
    public string? ModsFolderPath { get; init; }

    /// <summary>
    /// Gets the custom INI folder path, if different from the default documents location.
    /// </summary>
    public string? IniFolderPath { get; init; }

    /// <summary>
    /// Gets the game installation root folder path.
    /// Contains the game executable and Data folder.
    /// </summary>
    public string? GameRootPath { get; init; }

    /// <summary>
    /// Gets the game documents folder path.
    /// Contains INI files and logs (e.g., Documents\My Games\Fallout4).
    /// </summary>
    public string? DocumentsPath { get; init; }

    /// <summary>
    /// Gets the currently selected game name (e.g., "Fallout4", "SkyrimSE").
    /// </summary>
    public string? SelectedGame { get; init; }

    /// <summary>
    /// Gets a value indicating whether VR mode is enabled.
    /// Affects path detection and game executable names.
    /// </summary>
    public bool IsVrMode { get; init; }

    /// <summary>
    /// Gets the default settings with no paths configured.
    /// </summary>
    public static UserSettings Default => new()
    {
        SelectedGame = "Fallout4",
        IsVrMode = false
    };
}
