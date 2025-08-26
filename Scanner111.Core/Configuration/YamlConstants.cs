namespace Scanner111.Core.Configuration;

/// <summary>
///     Contains constants used by the YAML settings system.
/// </summary>
public static class YamlConstants
{
    /// <summary>
    ///     Default cache time-to-live in seconds for dynamic YAML files.
    /// </summary>
    public const double DefaultCacheTtlSeconds = 5.0;

    /// <summary>
    ///     Default game for path resolution when no game is configured.
    /// </summary>
    public const string DefaultGame = "Fallout4";

    /// <summary>
    ///     Settings keys that are allowed to be null without warnings.
    /// </summary>
    public static readonly HashSet<string> SettingsIgnoreNone = new()
    {
        "SCAN Custom Path",
        "MODS Folder Path",
        "INI Folder Path",
        "Root_Folder_Game",
        "Root_Folder_Docs"
    };

    /// <summary>
    ///     YAML stores that are considered static (won't change during execution).
    ///     These stores cannot be modified and are cached permanently.
    /// </summary>
    public static readonly HashSet<YamlStore> StaticYamlStores = new()
    {
        YamlStore.Main,
        YamlStore.Game
    };
}