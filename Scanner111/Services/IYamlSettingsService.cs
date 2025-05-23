using System;

namespace Scanner111.Services
{
    /// <summary>
    /// Interface for accessing and manipulating YAML settings
    /// Equivalent to the Python functions classic_settings and yaml_settings
    /// </summary>
    public interface IYamlSettingsService
    {
        /// <summary>
        /// Gets a string setting from the specified section
        /// </summary>
        /// <param name="key">The key to retrieve (can include dots for nested properties)</param>
        /// <param name="section">The section name, defaults to "CLASSIC"</param>
        /// <returns>The string value, or null if not found</returns>
        string GetStringSetting(string key, string section = "CLASSIC");

        /// <summary>
        /// Gets a boolean setting from the specified section
        /// </summary>
        /// <param name="key">The key to retrieve (can include dots for nested properties)</param>
        /// <param name="section">The section name, defaults to "CLASSIC"</param>
        /// <returns>The boolean value, or false if not found</returns>
        bool GetBoolSetting(string key, string section = "CLASSIC");

        /// <summary>
        /// Gets an integer setting from the specified section
        /// </summary>
        /// <param name="key">The key to retrieve (can include dots for nested properties)</param>
        /// <param name="section">The section name, defaults to "CLASSIC"</param>
        /// <returns>The integer value, or 0 if not found</returns>
        int GetIntSetting(string key, string section = "CLASSIC");

        /// <summary>
        /// Gets a setting of the specified type from the specified section
        /// </summary>
        /// <typeparam name="T">The type of the setting to retrieve</typeparam>
        /// <param name="key">The key to retrieve (can include dots for nested properties)</param>
        /// <param name="section">The section name, defaults to "CLASSIC"</param>
        /// <returns>The value as type T, or default(T) if not found</returns>
        T GetSetting<T>(string key, string section = "CLASSIC");

        /// <summary>
        /// Sets a setting value in the specified section
        /// </summary>
        /// <typeparam name="T">The type of the setting to set</typeparam>
        /// <param name="key">The key to set (can include dots for nested properties)</param>
        /// <param name="value">The value to set</param>
        /// <param name="section">The section name, defaults to "CLASSIC"</param>
        /// <returns>True if successful, false otherwise</returns>
        bool SetSetting<T>(string key, T value, string section = "CLASSIC");

        /// <summary>
        /// Saves any pending changes to the YAML files
        /// </summary>
        void SaveChanges();
    }
}
