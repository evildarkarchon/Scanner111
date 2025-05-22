namespace Scanner111.Models
{
    // Define the YAML stores that the application will use.
    // This should align with your YamlSettingsCacheService and configuration files.
    public enum YAML
    {
        Main, // e.g., CLASSIC Main.yaml
        Settings, // e.g., CLASSIC Settings.yaml
        Ignore, // e.g., CLASSIC Ignore.yaml
        Game, // e.g., CLASSIC Fallout4.yaml (game-specific)
        Game_Local, // e.g., CLASSIC Fallout4 Local.yaml

        Test // e.g., tests/test_settings.yaml
        // Add other YAML file types if needed
    }
}
