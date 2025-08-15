namespace Scanner111.Core.Models;

/// <summary>
///     Supported game types
/// </summary>
public enum GameType
{
    /// <summary>
    ///     Unknown or unsupported game
    /// </summary>
    Unknown = 0,

    /// <summary>
    ///     Fallout 4
    /// </summary>
    Fallout4 = 1,

    /// <summary>
    ///     The Elder Scrolls V: Skyrim Special Edition
    /// </summary>
    SkyrimSE = 2,

    /// <summary>
    ///     The Elder Scrolls V: Skyrim (Original)
    /// </summary>
    Skyrim = 3,

    /// <summary>
    ///     Fallout: New Vegas
    /// </summary>
    FalloutNV = 4,

    /// <summary>
    ///     Fallout 3
    /// </summary>
    Fallout3 = 5
}