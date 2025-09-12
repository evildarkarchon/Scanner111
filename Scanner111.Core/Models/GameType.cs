namespace Scanner111.Core.Models;

/// <summary>
/// Represents the supported game types.
/// </summary>
public enum GameType
{
    /// <summary>
    /// Fallout 4.
    /// </summary>
    Fallout4,
    
    /// <summary>
    /// Skyrim Special Edition.
    /// </summary>
    Skyrim,
    
    /// <summary>
    /// Skyrim VR.
    /// </summary>
    SkyrimVR,
    
    /// <summary>
    /// Fallout 4 VR.
    /// </summary>
    Fallout4VR
}

/// <summary>
/// Extension methods for GameType enum.
/// </summary>
public static class GameTypeExtensions
{
    /// <summary>
    /// Converts the GameType enum to the string representation used throughout the system.
    /// </summary>
    public static string ToGameString(this GameType gameType)
    {
        return gameType switch
        {
            GameType.Fallout4 => "Fallout4",
            GameType.Skyrim => "Skyrim",
            GameType.SkyrimVR => "SkyrimVR",
            GameType.Fallout4VR => "Fallout4VR",
            _ => "Fallout4"
        };
    }
    
    /// <summary>
    /// Converts a game string to the GameType enum.
    /// </summary>
    public static GameType FromGameString(string gameString)
    {
        return gameString?.ToLowerInvariant() switch
        {
            "fallout4" => GameType.Fallout4,
            "skyrim" => GameType.Skyrim,
            "skyrimvr" => GameType.SkyrimVR,
            "fallout4vr" => GameType.Fallout4VR,
            _ => GameType.Fallout4
        };
    }
    
    /// <summary>
    /// Gets the display name for the game type.
    /// </summary>
    public static string GetDisplayName(this GameType gameType)
    {
        return gameType switch
        {
            GameType.Fallout4 => "Fallout 4",
            GameType.Skyrim => "Skyrim Special Edition",
            GameType.SkyrimVR => "Skyrim VR",
            GameType.Fallout4VR => "Fallout 4 VR",
            _ => "Unknown"
        };
    }
}