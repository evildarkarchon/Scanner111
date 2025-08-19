using System.Text.Json;
using Scanner111.Core.Abstractions;

namespace Scanner111.Core.Infrastructure;

/// <summary>
///     Provides utility methods to manage application settings,
///     including loading, saving, and directory management for
///     settings files.
/// </summary>
public class SettingsHelper : ISettingsHelper
{
    private readonly IFileSystem _fileSystem;
    private readonly IPathService _pathService;
    private readonly IEnvironmentPathProvider _environmentProvider;

    /// <summary>
    ///     Common JsonSerializerOptions for all settings serialization
    /// </summary>
    public static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    public SettingsHelper(IFileSystem fileSystem, IPathService pathService, IEnvironmentPathProvider environmentProvider)
    {
        _fileSystem = fileSystem;
        _pathService = pathService;
        _environmentProvider = environmentProvider;
    }

    /// <summary>
    ///     Gets the Scanner111 settings directory in the AppData folder.
    /// </summary>
    /// <returns>
    ///     The path to the Scanner111 settings directory.
    /// </returns>
    public string GetSettingsDirectory()
    {
        return _pathService.Combine(
            _environmentProvider.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "Scanner111");
    }

    /// <summary>
    ///     Ensures that the settings directory for the application exists.
    /// </summary>
    public void EnsureSettingsDirectoryExists()
    {
        var directory = GetSettingsDirectory();
        _fileSystem.CreateDirectory(directory);
    }

    /// <summary>
    ///     Asynchronously loads settings from a JSON file or creates default settings if the file does not exist or fails to
    ///     load.
    /// </summary>
    /// <typeparam name="T">The type of the settings object to load.</typeparam>
    /// <param name="filePath">The path to the settings file.</param>
    /// <param name="defaultFactory">A factory function to create default settings if the file cannot be loaded.</param>
    /// <returns>
    ///     A task representing the asynchronous operation. The result contains the loaded settings,
    ///     or the default settings if the file does not exist or fails to load.
    /// </returns>
    public async Task<T> LoadSettingsAsync<T>(string filePath, Func<T> defaultFactory)
        where T : class
    {
        try
        {
            if (!_fileSystem.FileExists(filePath))
            {
                var defaultSettings = defaultFactory();
                await SaveSettingsAsync(filePath, defaultSettings).ConfigureAwait(false);
                return defaultSettings;
            }

            var json = await _fileSystem.ReadAllTextAsync(filePath).ConfigureAwait(false);
            var settings = JsonSerializer.Deserialize<T>(json, JsonOptions);

            return settings ?? defaultFactory();
        }
        catch (Exception)
        {
            // Return default settings on error
            return defaultFactory();
        }
    }

    /// <summary>
    ///     Saves the specified settings to a JSON file asynchronously.
    /// </summary>
    /// <typeparam name="T">The type of the settings object.</typeparam>
    /// <param name="filePath">The file path where the settings will be saved.</param>
    /// <param name="settings">The settings object to be saved.</param>
    /// <returns>
    ///     A task that represents the asynchronous save operation.
    /// </returns>
    public async Task SaveSettingsAsync<T>(string filePath, T settings)
        where T : class
    {
        // Ensure the directory exists for the specific file path
        var directory = _pathService.GetDirectoryName(filePath);
        if (!string.IsNullOrEmpty(directory)) _fileSystem.CreateDirectory(directory);

        var json = JsonSerializer.Serialize(settings, JsonOptions);
        await _fileSystem.WriteAllTextAsync(filePath, json).ConfigureAwait(false);
    }

    /// <summary>
    ///     Converts a value to the specified target type for use in application settings.
    /// </summary>
    /// <param name="value">The value to be converted.</param>
    /// <param name="targetType">The type to which the value should be converted.</param>
    /// <returns>The converted value of the specified target type.</returns>
    public object ConvertValue(object value, Type targetType)
    {
        return value switch
        {
            null => null!,
            // Handle string inputs
            string stringValue when targetType == typeof(bool) => stringValue.ToLowerInvariant() switch
            {
                "true" or "yes" or "1" or "on" => true,
                "false" or "no" or "0" or "off" => false,
                _ => throw new ArgumentException($"Invalid boolean value: {stringValue}")
            },
            string stringValue when targetType == typeof(int) => int.Parse(stringValue),
            _ => Convert.ChangeType(value, targetType)
        };
    }

    /// <summary>
    ///     Converts an input string to PascalCase format.
    /// </summary>
    /// <param name="input">The input string to be converted.</param>
    /// <returns>The input string converted to PascalCase format.</returns>
    public string ToPascalCase(string input)
    {
        if (string.IsNullOrEmpty(input))
            return input;

        var words = input.Split(['-', '_', ' '], StringSplitOptions.RemoveEmptyEntries);
        var result = string.Join("", words.Select(w =>
            char.ToUpperInvariant(w[0]) + w.Substring(1).ToLowerInvariant()));

        return result;
    }
}