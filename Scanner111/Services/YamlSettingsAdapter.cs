using Scanner111.Services.Interfaces;

namespace Scanner111.Services;

/// <summary>
///     Adapter that implements IYamlSettingsService using the existing YamlSettings static class
///     to provide compatibility with the Update service
/// </summary>
public class YamlSettingsAdapter : IYamlSettingsService
{
    private readonly IYamlSettingsCache _yamlSettingsCache;

    public YamlSettingsAdapter(IYamlSettingsCache yamlSettingsCache)
    {
        _yamlSettingsCache = yamlSettingsCache;
        YamlSettings.Initialize(yamlSettingsCache);
    }

    /// <inheritdoc />
    public string? GetStringSetting(string key, string section = "CLASSIC")
    {
        // Map the section parameter to the appropriate YamlStore
        var yamlStore = MapSectionToYamlStore(section);

        // If the section is "CLASSIC", use GetClassicSetting
        return section == "CLASSIC"
            ? YamlSettings.GetClassicSetting<string>(key)
            :
            // Otherwise, use Get with the mapped YamlStore
            YamlSettings.Get<string>(yamlStore, key);
    }

    /// <inheritdoc />
    public bool GetBoolSetting(string key, string section = "CLASSIC")
    {
        // Map the section parameter to the appropriate YamlStore
        var yamlStore = MapSectionToYamlStore(section);

        // If the section is "CLASSIC", use GetClassicSetting
        return section == "CLASSIC"
            ? YamlSettings.GetClassicSetting<bool>(key)
            :
            // Otherwise, use Get with the mapped YamlStore
            YamlSettings.Get<bool>(yamlStore, key);
    }

    /// <inheritdoc />
    public int GetIntSetting(string key, string section = "CLASSIC")
    {
        // Map the section parameter to the appropriate YamlStore
        var yamlStore = MapSectionToYamlStore(section);

        // If the section is "CLASSIC", use GetClassicSetting
        return section == "CLASSIC"
            ? YamlSettings.GetClassicSetting<int>(key)
            :
            // Otherwise, use Get with the mapped YamlStore
            YamlSettings.Get<int>(yamlStore, key);
    }

    /// <inheritdoc />
    public T? GetSetting<T>(string key, string section = "CLASSIC")
    {
        // Map the section parameter to the appropriate YamlStore
        var yamlStore = MapSectionToYamlStore(section);

        // If the section is "CLASSIC", use GetClassicSetting
        return section != "CLASSIC" ? YamlSettings.Get<T>(yamlStore, key) : YamlSettings.GetClassicSetting<T>(key);

        // Otherwise, use Get with the mapped YamlStore
    }

    /// <inheritdoc />
    public bool SetSetting<T>(string key, T value, string section = "CLASSIC")
    {
        // Map the section parameter to the appropriate YamlStore
        var yamlStore = MapSectionToYamlStore(section);

        try
        {
            // Use Get with a new value to set the setting
            YamlSettings.Get(yamlStore, key, value);
            return true;
        }
        catch
        {
            return false;
        }
    }

    /// <inheritdoc />
    public void SaveChanges()
    {
        // In this implementation, changes are saved automatically when SetSetting is called
        // No additional action is required to save changes
    }

    /// <summary>
    ///     Maps a section name to the corresponding YamlStore enum value
    /// </summary>
    /// <param name="section">The section name to map</param>
    /// <returns>The corresponding YamlStore enum value</returns>
    private YamlStore MapSectionToYamlStore(string section)
    {
        return section.ToUpperInvariant() switch
        {
            "CLASSIC" => YamlStore.Settings,
            "MAIN" => YamlStore.Main,
            "IGNORE" => YamlStore.Ignore,
            "GAME" => YamlStore.Game,
            "GAMELOCAL" => YamlStore.GameLocal,
            "TEST" => YamlStore.Test,
            _ => YamlStore.Settings // Default to Settings for unknown sections
        };
    }
}