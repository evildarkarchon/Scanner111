using Scanner111.Common.Models.GamePath;

namespace Scanner111.Common.Models.GameIntegrity;

/// <summary>
/// Configuration for game integrity checking.
/// </summary>
/// <remarks>
/// Contains the expected hash values, version information, and paths
/// needed to validate game installation integrity.
/// </remarks>
public record GameIntegrityConfiguration
{
    /// <summary>
    /// Gets the game type.
    /// </summary>
    public GameType GameType { get; init; }

    /// <summary>
    /// Gets the display name for the game (e.g., "Fallout 4").
    /// </summary>
    public string GameDisplayName { get; init; } = string.Empty;

    /// <summary>
    /// Gets the game root folder path.
    /// </summary>
    public string? GameRootPath { get; init; }

    /// <summary>
    /// Gets the SHA-256 hash of the older known good executable version.
    /// </summary>
    public string? ExecutableHashOld { get; init; }

    /// <summary>
    /// Gets the SHA-256 hash of the latest known good executable version.
    /// </summary>
    public string? ExecutableHashNew { get; init; }

    /// <summary>
    /// Gets the version string for the older executable (e.g., "1.10.163").
    /// </summary>
    public string? GameVersionOld { get; init; }

    /// <summary>
    /// Gets the version string for the latest executable (e.g., "1.10.984").
    /// </summary>
    public string? GameVersionNew { get; init; }

    /// <summary>
    /// Gets the path to the Steam INI file to check for.
    /// </summary>
    /// <remarks>
    /// If this file exists, it indicates an outdated Steam installation method.
    /// For Fallout 4, this is typically "steam_api64.ini" in the game root.
    /// </remarks>
    public string? SteamIniPath { get; init; }

    /// <summary>
    /// Gets the warning message for restricted installation location.
    /// </summary>
    public string? RestrictedLocationWarning { get; init; }
}
