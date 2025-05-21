using Scanner111.Models;

namespace Scanner111.Services
{
    /// <summary>
    /// Interface for YAML settings cache service to enable easier testing and dependency injection
    /// </summary>
    public interface IYamlSettingsCacheService
    {
        /// <summary>
        /// Gets a setting from the YAML cache
        /// </summary>
        /// <typeparam name="T">Type of the setting to retrieve</typeparam>
        /// <param name="yamlType">The YAML file type</param>
        /// <param name="path">Path to the setting in the YAML file</param>
        /// <param name="defaultValue">Default value to return if setting is not found</param>
        /// <returns>The setting value, or default for the type if not found</returns>
        T? GetSetting<T>(YAML yamlType, string path, T? defaultValue = default);

        /// <summary>
        /// Gets a setting from the YAML cache using YamlStoreType
        /// </summary>
        /// <typeparam name="T">Type of the setting to retrieve</typeparam>
        /// <param name="storeType">The YAML store type</param>
        /// <param name="path">Path to the setting in the YAML file</param>
        /// <returns>The setting value, or default for the type if not found</returns>
        T? GetSetting<T>(YamlStoreType storeType, string path);
    }
}
