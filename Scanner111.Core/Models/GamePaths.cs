namespace Scanner111.Core.Models;

/// <summary>
///     Represents all discovered paths for a game installation.
///     Thread-safe immutable model.
/// </summary>
public sealed record GamePaths
{
    /// <summary>
    ///     Gets the game root directory path.
    /// </summary>
    public required string GameRootPath { get; init; }

    /// <summary>
    ///     Gets the Data folder path.
    /// </summary>
    public string DataPath => Path.Combine(GameRootPath, "Data");

    /// <summary>
    ///     Gets the Scripts folder path.
    /// </summary>
    public string ScriptsPath => Path.Combine(DataPath, "Scripts");

    /// <summary>
    ///     Gets the Script Extender plugins folder path.
    /// </summary>
    public string? ScriptExtenderPluginsPath { get; init; }

    /// <summary>
    ///     Gets the game executable path.
    /// </summary>
    public required string ExecutablePath { get; init; }

    /// <summary>
    ///     Gets the documents folder path.
    /// </summary>
    public string? DocumentsPath { get; init; }

    /// <summary>
    ///     Gets the Script Extender log file path.
    /// </summary>
    public string? ScriptExtenderLogPath { get; init; }

    /// <summary>
    ///     Gets the Papyrus log file path.
    /// </summary>
    public string? PapyrusLogPath { get; init; }

    /// <summary>
    ///     Gets the game INI file path.
    /// </summary>
    public string? GameIniPath { get; init; }

    /// <summary>
    ///     Gets the game custom INI file path.
    /// </summary>
    public string? GameCustomIniPath { get; init; }

    /// <summary>
    ///     Gets the Steam API INI file path.
    /// </summary>
    public string? SteamApiIniPath { get; init; }

    /// <summary>
    ///     Gets the Address Library file path (for SKSE/F4SE).
    /// </summary>
    public string? AddressLibraryPath { get; init; }

    /// <summary>
    ///     Gets the timestamp when these paths were discovered.
    /// </summary>
    public DateTimeOffset DiscoveredAt { get; init; } = DateTimeOffset.UtcNow;

    /// <summary>
    ///     Gets a value indicating whether all critical paths are valid.
    /// </summary>
    public bool AreAllCriticalPathsValid()
    {
        return !string.IsNullOrWhiteSpace(GameRootPath) &&
               !string.IsNullOrWhiteSpace(ExecutablePath) &&
               Directory.Exists(GameRootPath) &&
               File.Exists(ExecutablePath);
    }

    /// <summary>
    ///     Gets a value indicating whether documents paths are valid.
    /// </summary>
    public bool AreDocumentPathsValid()
    {
        return !string.IsNullOrWhiteSpace(DocumentsPath) &&
               Directory.Exists(DocumentsPath);
    }
}