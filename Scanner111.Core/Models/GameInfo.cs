namespace Scanner111.Core.Models;

/// <summary>
///     Represents metadata about a game installation.
///     Thread-safe immutable model.
/// </summary>
public sealed record GameInfo
{
    /// <summary>
    ///     Gets the game name (e.g., "Fallout4", "Skyrim").
    /// </summary>
    public required string GameName { get; init; }

    /// <summary>
    ///     Gets a value indicating whether this is the VR version.
    /// </summary>
    public bool IsVR { get; init; }

    /// <summary>
    ///     Gets the game version.
    /// </summary>
    public Version? GameVersion { get; init; }

    /// <summary>
    ///     Gets the Steam application ID.
    /// </summary>
    public int? SteamId { get; init; }

    /// <summary>
    ///     Gets the GOG game ID if applicable.
    /// </summary>
    public string? GogId { get; init; }

    /// <summary>
    ///     Gets the executable name (e.g., "Fallout4.exe").
    /// </summary>
    public required string ExecutableName { get; init; }

    /// <summary>
    ///     Gets the Script Extender acronym (e.g., "F4SE", "SKSE").
    /// </summary>
    public required string ScriptExtenderAcronym { get; init; }

    /// <summary>
    ///     Gets the base Script Extender acronym without version (e.g., "F4SE", "SKSE").
    /// </summary>
    public required string ScriptExtenderBase { get; init; }

    /// <summary>
    ///     Gets the documents folder name (e.g., "Fallout4", "Skyrim Special Edition").
    /// </summary>
    public required string DocumentsFolderName { get; init; }

    /// <summary>
    ///     Gets the registry key path for this game.
    /// </summary>
    public string? RegistryKeyPath { get; init; }

    /// <summary>
    ///     Gets a value indicating whether this is the Next-Gen update version (for Fallout 4).
    /// </summary>
    public bool IsNextGen { get; init; }
}