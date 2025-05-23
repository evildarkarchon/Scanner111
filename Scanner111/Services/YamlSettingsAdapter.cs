using Scanner111.Services.Interfaces;

namespace Scanner111.Services
{
    /// <summary>
    /// Adapter that implements IYamlSettingsService using the existing YamlSettings static class
    /// to provide compatibility with the Update service
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
        public string GetStringSetting(string key, string section = "CLASSIC")
        {
            // Map the section parameter to the appropriate YamlStore
            YamlStore yamlStore = MapSectionToYamlStore(section);
            
            // If the section is "CLASSIC", use GetClassicSetting
            if (section == "CLASSIC")
            {
                return YamlSettings.GetClassicSetting<string>(key);
            }
            
            // Otherwise, use Get with the mapped YamlStore
            return YamlSettings.Get<string>(yamlStore, key);
        }

        /// <inheritdoc />
        public bool GetBoolSetting(string key, string section = "CLASSIC")
        {
            // Map the section parameter to the appropriate YamlStore
            YamlStore yamlStore = MapSectionToYamlStore(section);
            
            // If the section is "CLASSIC", use GetClassicSetting
            if (section == "CLASSIC")
            {
                return YamlSettings.GetClassicSetting<bool>(key);
            }
            
            // Otherwise, use Get with the mapped YamlStore
            return YamlSettings.Get<bool>(yamlStore, key);
        }

        /// <inheritdoc />
        public int GetIntSetting(string key, string section = "CLASSIC")
        {
            // Map the section parameter to the appropriate YamlStore
            YamlStore yamlStore = MapSectionToYamlStore(section);
            
            // If the section is "CLASSIC", use GetClassicSetting
            if (section == "CLASSIC")
            {
                return YamlSettings.GetClassicSetting<int>(key);
            }
            
            // Otherwise, use Get with the mapped YamlStore
            return YamlSettings.Get<int>(yamlStore, key);
        }

        /// <inheritdoc />
        public T GetSetting<T>(string key, string section = "CLASSIC")
        {
            // Map the section parameter to the appropriate YamlStore
            YamlStore yamlStore = MapSectionToYamlStore(section);
            
            // If the section is "CLASSIC", use GetClassicSetting
            if (section == "CLASSIC")
            {
                return YamlSettings.GetClassicSetting<T>(key);
            }
            
            // Otherwise, use Get with the mapped YamlStore
            return YamlSettings.Get<T>(yamlStore, key);
        }

        /// <inheritdoc />
        public bool SetSetting<T>(string key, T value, string section = "CLASSIC")
        {
            // Map the section parameter to the appropriate YamlStore
            YamlStore yamlStore = MapSectionToYamlStore(section);
            
            try
            {
                // Use Get with a new value to set the setting
                YamlSettings.Get<T>(yamlStore, key, value);
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
            // No explicit SaveChanges in YamlSettings - changes are saved immediately in Get<T> when newValue is provided
        }

        /// <summary>
        /// Maps a section string to the corresponding YamlStore enum value
        /// </summary>
        /// <param name="section">The section name</param>
        /// <returns>The corresponding YamlStore enum value</returns>
        private YamlStore MapSectionToYamlStore(string section)
        {
            return section switch
            {
                "CLASSIC" => YamlStore.Settings,
                "Main" => YamlStore.Main,
                "Game" => YamlStore.Game,
                "GameLocal" => YamlStore.GameLocal,
                "Ignore" => YamlStore.Ignore,
                "Test" => YamlStore.Test,
                _ => YamlStore.Settings // Default to Settings
            };
        }
    }
}
