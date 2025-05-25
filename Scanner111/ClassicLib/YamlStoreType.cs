namespace Scanner111.ClassicLib;

/// <summary>
/// Represents the different types of YAML configuration stores used in the application.
/// </summary>
public enum YamlStoreType
{
    /// <summary>
    /// CLASSIC Data/databases/CLASSIC Main.yaml
    /// </summary>
    Main,

    /// <summary>
    /// CLASSIC Settings.yaml
    /// </summary>
    Settings,

    /// <summary>
    /// CLASSIC Ignore.yaml
    /// </summary>
    Ignore,

    /// <summary>
    /// CLASSIC Data/databases/CLASSIC [GameName].yaml
    /// </summary>
    Game,

    /// <summary>
    /// CLASSIC Data/CLASSIC [GameName] Local.yaml
    /// </summary>
    GameLocal,

    /// <summary>
    /// tests/test_settings.yaml
    /// </summary>
    Test
}