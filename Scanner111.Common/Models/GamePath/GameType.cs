namespace Scanner111.Common.Models.GamePath;

/// <summary>
/// Represents the supported game types for crash log analysis.
/// </summary>
public enum GameType
{
    /// <summary>
    /// Unknown or undetected game type.
    /// </summary>
    Unknown = 0,

    /// <summary>
    /// Fallout 4 (standard version).
    /// </summary>
    Fallout4,

    /// <summary>
    /// Fallout 4 VR.
    /// </summary>
    Fallout4VR,

    /// <summary>
    /// Skyrim Special Edition.
    /// </summary>
    SkyrimSE,

    /// <summary>
    /// Skyrim VR.
    /// </summary>
    SkyrimVR
}

/// <summary>
/// Extension methods for <see cref="GameType"/>.
/// </summary>
public static class GameTypeExtensions
{
    /// <summary>
    /// Gets the executable file name for the specified game type.
    /// </summary>
    /// <param name="gameType">The game type.</param>
    /// <returns>The executable file name (e.g., "Fallout4.exe").</returns>
    public static string GetExecutableName(this GameType gameType) => gameType switch
    {
        GameType.Fallout4 => "Fallout4.exe",
        GameType.Fallout4VR => "Fallout4VR.exe",
        GameType.SkyrimSE => "SkyrimSE.exe",
        GameType.SkyrimVR => "SkyrimVR.exe",
        _ => string.Empty
    };

    /// <summary>
    /// Gets the script extender acronym for the specified game type.
    /// </summary>
    /// <param name="gameType">The game type.</param>
    /// <returns>The XSE acronym (e.g., "F4SE", "SKSE").</returns>
    public static string GetXseAcronym(this GameType gameType) => gameType switch
    {
        GameType.Fallout4 => "F4SE",
        GameType.Fallout4VR => "F4SEVR",
        GameType.SkyrimSE => "SKSE64",
        GameType.SkyrimVR => "SKSEVR",
        _ => string.Empty
    };

    /// <summary>
    /// Gets the base script extender acronym (without VR suffix) for the specified game type.
    /// </summary>
    /// <param name="gameType">The game type.</param>
    /// <returns>The base XSE acronym (e.g., "F4SE", "SKSE").</returns>
    public static string GetXseAcronymBase(this GameType gameType) => gameType switch
    {
        GameType.Fallout4 or GameType.Fallout4VR => "F4SE",
        GameType.SkyrimSE or GameType.SkyrimVR => "SKSE",
        _ => string.Empty
    };

    /// <summary>
    /// Gets the XSE log file name for the specified game type.
    /// </summary>
    /// <param name="gameType">The game type.</param>
    /// <returns>The XSE log file name (e.g., "f4se.log").</returns>
    public static string GetXseLogFileName(this GameType gameType) => gameType switch
    {
        GameType.Fallout4 => "f4se.log",
        GameType.Fallout4VR => "f4sevr.log",
        GameType.SkyrimSE => "skse64.log",
        GameType.SkyrimVR => "sksevr.log",
        _ => string.Empty
    };

    /// <summary>
    /// Gets the registry key name under Bethesda Softworks for the specified game type.
    /// </summary>
    /// <param name="gameType">The game type.</param>
    /// <returns>The registry key name (e.g., "Fallout4", "Fallout4VR").</returns>
    public static string GetRegistryKeyName(this GameType gameType) => gameType switch
    {
        GameType.Fallout4 => "Fallout4",
        GameType.Fallout4VR => "Fallout4VR",
        GameType.SkyrimSE => "Skyrim Special Edition",
        GameType.SkyrimVR => "SkyrimVR",
        _ => string.Empty
    };

    /// <summary>
    /// Gets the display name for the specified game type.
    /// </summary>
    /// <param name="gameType">The game type.</param>
    /// <returns>The display name (e.g., "Fallout 4", "Skyrim Special Edition").</returns>
    public static string GetDisplayName(this GameType gameType) => gameType switch
    {
        GameType.Fallout4 => "Fallout 4",
        GameType.Fallout4VR => "Fallout 4 VR",
        GameType.SkyrimSE => "Skyrim Special Edition",
        GameType.SkyrimVR => "Skyrim VR",
        _ => "Unknown"
    };

    /// <summary>
    /// Gets the My Games folder name for the specified game type.
    /// </summary>
    /// <param name="gameType">The game type.</param>
    /// <returns>The My Games folder name (e.g., "Fallout4", "Skyrim Special Edition").</returns>
    public static string GetMyGamesFolderName(this GameType gameType) => gameType switch
    {
        GameType.Fallout4 => "Fallout4",
        GameType.Fallout4VR => "Fallout4VR",
        GameType.SkyrimSE => "Skyrim Special Edition",
        GameType.SkyrimVR => "SkyrimVR",
        _ => string.Empty
    };

    /// <summary>
    /// Gets whether the game type is a VR variant.
    /// </summary>
    /// <param name="gameType">The game type.</param>
    /// <returns><c>true</c> if the game is a VR variant; otherwise, <c>false</c>.</returns>
    public static bool IsVR(this GameType gameType) => gameType is GameType.Fallout4VR or GameType.SkyrimVR;

    /// <summary>
    /// Gets whether the game type is a Fallout game.
    /// </summary>
    /// <param name="gameType">The game type.</param>
    /// <returns><c>true</c> if the game is Fallout 4 or Fallout 4 VR; otherwise, <c>false</c>.</returns>
    public static bool IsFallout(this GameType gameType) => gameType is GameType.Fallout4 or GameType.Fallout4VR;

    /// <summary>
    /// Gets whether the game type is a Skyrim game.
    /// </summary>
    /// <param name="gameType">The game type.</param>
    /// <returns><c>true</c> if the game is Skyrim SE or Skyrim VR; otherwise, <c>false</c>.</returns>
    public static bool IsSkyrim(this GameType gameType) => gameType is GameType.SkyrimSE or GameType.SkyrimVR;
}
