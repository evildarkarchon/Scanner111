namespace Scanner111.Models;

/// <summary>
///     Defines the YAML configuration sections used throughout the application.
///     This should align with YamlSettingsCacheService and configuration files.
/// </summary>
public enum Yaml
{
    /// <summary>
    ///     Main configuration (e.g., CLASSIC Main.yaml)
    /// </summary>
    Main,

    /// <summary>
    ///     User settings configuration (e.g., CLASSIC Settings.yaml)
    /// </summary>
    Settings,

    /// <summary>
    ///     Ignore list configuration (e.g., CLASSIC Ignore.yaml)
    /// </summary>
    Ignore,

    /// <summary>
    ///     Game-specific configuration (e.g., CLASSIC Fallout4.yaml)
    /// </summary>
    Game,

    /// <summary>
    ///     Game-specific local configuration (e.g., CLASSIC Fallout4 Local.yaml)
    /// </summary>
    GameLocal,

    /// <summary>
    ///     Test configuration (e.g., tests/test_settings.yaml)
    /// </summary>
    Test
}