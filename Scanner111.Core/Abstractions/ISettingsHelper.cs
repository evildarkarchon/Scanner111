namespace Scanner111.Core.Abstractions;

/// <summary>
///     Interface for managing application settings,
///     including loading, saving, and directory management for settings files.
/// </summary>
public interface ISettingsHelper
{
    /// <summary>
    ///     Gets the Scanner111 settings directory in the AppData folder.
    /// </summary>
    /// <returns>The path to the Scanner111 settings directory.</returns>
    string GetSettingsDirectory();

    /// <summary>
    ///     Ensures that the settings directory for the application exists.
    /// </summary>
    void EnsureSettingsDirectoryExists();

    /// <summary>
    ///     Asynchronously loads settings from a JSON file or creates default settings if the file does not exist or fails to load.
    /// </summary>
    /// <typeparam name="T">The type of the settings object to load.</typeparam>
    /// <param name="filePath">The path to the settings file.</param>
    /// <param name="defaultFactory">A factory function to create default settings if the file cannot be loaded.</param>
    /// <returns>The loaded settings, or the default settings if the file does not exist or fails to load.</returns>
    Task<T> LoadSettingsAsync<T>(string filePath, Func<T> defaultFactory) where T : class;

    /// <summary>
    ///     Saves the specified settings to a JSON file asynchronously.
    /// </summary>
    /// <typeparam name="T">The type of the settings object.</typeparam>
    /// <param name="filePath">The file path where the settings will be saved.</param>
    /// <param name="settings">The settings object to be saved.</param>
    /// <returns>A task that represents the asynchronous save operation.</returns>
    Task SaveSettingsAsync<T>(string filePath, T settings) where T : class;

    /// <summary>
    ///     Converts a value to the specified target type for use in application settings.
    /// </summary>
    /// <param name="value">The value to be converted.</param>
    /// <param name="targetType">The type to which the value should be converted.</param>
    /// <returns>The converted value of the specified target type.</returns>
    object ConvertValue(object value, Type targetType);

    /// <summary>
    ///     Converts an input string to PascalCase format.
    /// </summary>
    /// <param name="input">The input string to be converted.</param>
    /// <returns>The input string converted to PascalCase format.</returns>
    string ToPascalCase(string input);
}