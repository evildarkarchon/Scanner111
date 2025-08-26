namespace Scanner111.Core.Configuration;

/// <summary>
/// Represents different types of YAML configuration stores.
/// </summary>
public enum YamlStore
{
    /// <summary>
    /// CLASSIC Data/databases/CLASSIC Main.yaml - Static configuration
    /// </summary>
    Main,
    
    /// <summary>
    /// CLASSIC Settings.yaml - User modifiable settings
    /// </summary>
    Settings,
    
    /// <summary>
    /// CLASSIC Ignore.yaml - Ignore patterns configuration
    /// </summary>
    Ignore,
    
    /// <summary>
    /// CLASSIC Data/databases/CLASSIC {Game}.yaml - Game-specific static data
    /// </summary>
    Game,
    
    /// <summary>
    /// CLASSIC Data/CLASSIC {Game} Local.yaml - Game-specific local configuration
    /// </summary>
    GameLocal,
    
    /// <summary>
    /// tests/test_settings.yaml - Test configuration
    /// </summary>
    Test
}