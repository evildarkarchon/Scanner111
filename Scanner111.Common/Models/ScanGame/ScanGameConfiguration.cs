using Scanner111.Common.Models.GameIntegrity;
using Scanner111.Common.Models.GamePath;

namespace Scanner111.Common.Models.ScanGame;

/// <summary>
/// Configuration for comprehensive game and mod installation scanning.
/// </summary>
/// <remarks>
/// <para>
/// This configuration record specifies which scans to perform and provides
/// all necessary paths and options. Use the factory method
/// <see cref="CreateForGame"/> for easy configuration with sensible defaults.
/// </para>
/// <para>
/// Individual scan options can be enabled/disabled. If a path is null or
/// the scan is disabled, that scanner will be skipped.
/// </para>
/// </remarks>
public record ScanGameConfiguration
{
    /// <summary>
    /// Gets the game type being scanned.
    /// </summary>
    public GameType GameType { get; init; }

    /// <summary>
    /// Gets the XSE acronym for the game (e.g., "F4SE", "SKSE64").
    /// </summary>
    /// <remarks>
    /// If not set, derived from <see cref="GameType"/> via extension method.
    /// </remarks>
    public string XseAcronym { get; init; } = string.Empty;

    /// <summary>
    /// Gets the display name for the game (e.g., "Fallout 4").
    /// </summary>
    /// <remarks>
    /// If not set, derived from <see cref="GameType"/> via extension method.
    /// </remarks>
    public string GameDisplayName { get; init; } = string.Empty;

    // === Path Configuration ===

    /// <summary>
    /// Gets the game root folder path (where the executable is located).
    /// </summary>
    public string? GameRootPath { get; init; }

    /// <summary>
    /// Gets the mod directory path (for unpacked and BA2 scanning).
    /// </summary>
    /// <remarks>
    /// Typically the game's Data folder or a mod organizer staging folder.
    /// </remarks>
    public string? ModPath { get; init; }

    /// <summary>
    /// Gets the path to the Documents\My Games\[Game] folder.
    /// </summary>
    /// <remarks>
    /// Used for INI scanning and XSE log file detection.
    /// </remarks>
    public string? DocumentsGamePath { get; init; }

    /// <summary>
    /// Gets the path to the XSE plugins folder (Data\F4SE\Plugins or Data\SKSE\Plugins).
    /// </summary>
    public string? XsePluginsPath { get; init; }

    // === Scan Options ===

    /// <summary>
    /// Gets whether to scan unpacked (loose) mod files.
    /// </summary>
    public bool ScanUnpacked { get; init; } = true;

    /// <summary>
    /// Gets whether to scan BA2 archive files.
    /// </summary>
    public bool ScanArchives { get; init; } = true;

    /// <summary>
    /// Gets whether to validate INI configuration files.
    /// </summary>
    public bool ValidateIni { get; init; } = true;

    /// <summary>
    /// Gets whether to validate TOML crash generator configuration.
    /// </summary>
    public bool ValidateToml { get; init; } = true;

    /// <summary>
    /// Gets whether to check XSE installation integrity.
    /// </summary>
    public bool CheckXse { get; init; } = true;

    /// <summary>
    /// Gets whether to check game installation integrity.
    /// </summary>
    public bool CheckGameIntegrity { get; init; } = true;

    // === Scanner-Specific Options ===

    /// <summary>
    /// Gets whether to analyze DDS texture dimensions (slower but more thorough).
    /// </summary>
    public bool AnalyzeDdsTextures { get; init; } = true;

    /// <summary>
    /// Gets XSE script file mappings for detection (e.g., "f4se.dll" -> "F4SE").
    /// </summary>
    public IReadOnlyDictionary<string, string>? XseScriptFiles { get; init; }

    /// <summary>
    /// Gets XSE script folder mappings for archive detection (e.g., "f4se" -> "F4SE").
    /// </summary>
    public IReadOnlyDictionary<string, string>? XseScriptFolders { get; init; }

    /// <summary>
    /// Gets the crash generator name for TOML validation (e.g., "Buffout4").
    /// </summary>
    public string? CrashGenName { get; init; }

    /// <summary>
    /// Gets the XSE configuration for integrity checking.
    /// </summary>
    public XseConfiguration? XseConfiguration { get; init; }

    /// <summary>
    /// Gets the game integrity configuration.
    /// </summary>
    public GameIntegrityConfiguration? GameIntegrityConfiguration { get; init; }

    // === Factory Methods ===

    /// <summary>
    /// Creates a configuration for the specified game type with default paths and settings.
    /// </summary>
    /// <param name="gameType">The game type to configure.</param>
    /// <param name="gameRootPath">The game root folder path (where executable is located).</param>
    /// <param name="documentsGamePath">The Documents game folder path (My Games\[Game]).</param>
    /// <param name="modPath">Optional override mod path (defaults to Data folder).</param>
    /// <returns>A new configuration instance with sensible defaults.</returns>
    /// <remarks>
    /// <para>
    /// This factory method sets up all paths and options based on the game type:
    /// <list type="bullet">
    /// <item>XSE acronym and display name from GameType extension methods</item>
    /// <item>ModPath defaults to [GameRootPath]\Data</item>
    /// <item>XsePluginsPath defaults to [Data]\[XSE]\Plugins</item>
    /// <item>CrashGenName is "Buffout4" for Fallout, "CrashLogger" for Skyrim</item>
    /// <item>XSE script file/folder mappings appropriate for the game</item>
    /// </list>
    /// </para>
    /// <para>
    /// Note: XseConfiguration and GameIntegrityConfiguration are NOT set by this
    /// factory method and should be provided separately if those checks are needed.
    /// </para>
    /// </remarks>
    public static ScanGameConfiguration CreateForGame(
        GameType gameType,
        string gameRootPath,
        string documentsGamePath,
        string? modPath = null)
    {
        var dataPath = Path.Combine(gameRootPath, "Data");
        var xseAcronym = gameType.GetXseAcronym();

        return new ScanGameConfiguration
        {
            GameType = gameType,
            XseAcronym = xseAcronym,
            GameDisplayName = gameType.GetDisplayName(),
            GameRootPath = gameRootPath,
            ModPath = modPath ?? dataPath,
            DocumentsGamePath = documentsGamePath,
            XsePluginsPath = Path.Combine(dataPath, xseAcronym, "Plugins"),
            CrashGenName = gameType.IsFallout() ? "Buffout4" : "CrashLogger",
            XseScriptFiles = CreateXseScriptFiles(gameType),
            XseScriptFolders = CreateXseScriptFolders(gameType)
        };
    }

    private static Dictionary<string, string> CreateXseScriptFiles(GameType gameType)
    {
        var acronymBase = gameType.GetXseAcronymBase();
        var acronymLower = acronymBase.ToLowerInvariant();

        var files = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            [$"{acronymLower}.dll"] = acronymBase,
            [$"{acronymLower}_steam_loader.dll"] = acronymBase
        };

        // Add version-specific DLLs for Fallout 4
        if (gameType == GameType.Fallout4)
        {
            files["f4se_1_10_163.dll"] = "F4SE";
        }

        return files;
    }

    private static Dictionary<string, string> CreateXseScriptFolders(GameType gameType)
    {
        var acronymBase = gameType.GetXseAcronymBase();
        var acronymLower = acronymBase.ToLowerInvariant();

        return new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            [acronymLower] = acronymBase,
            ["scripts"] = "Scripts"
        };
    }
}
