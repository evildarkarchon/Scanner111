using System.Collections.Generic;

namespace Scanner111.Services;

/// <summary>
/// Enum representing different YAML configuration stores.
/// </summary>
public enum YamlStore
{
    Main,
    Settings,
    Ignore,
    Game,
    GameLocal,
    Test
}

/// <summary>
/// Constants used in YAML settings management.
/// </summary>
public static class YamlConstants
{
    /// <summary>
    /// Settings keys that can safely return null without triggering warnings.
    /// </summary>
    public static readonly HashSet<string> SettingsIgnoreNone = [];
    
    /// <summary>
    /// Static YAML stores that won't change during program execution.
    /// </summary>
    public static readonly HashSet<YamlStore> StaticYamlStores =
    [
        YamlStore.Main,
        YamlStore.Game
    ];
}