using System.Text.Json;

namespace Scanner111.Core.Infrastructure;

/// <summary>
/// Provides utility methods to manage application settings,
/// including loading, saving, and directory management for
/// settings files.
/// </summary>
public static class SettingsHelper
{
    /// <summary>
    ///     Common JsonSerializerOptions for all settings serialization
    /// </summary>
    public static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    /// <summary>
    /// Gets the Scanner111 settings directory in the AppData folder.
    /// </summary>
    /// <returns>
    /// The path to the Scanner111 settings directory.
    /// </returns>
    public static string GetSettingsDirectory()
    {
        return Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "Scanner111");
    }

    /// <summary>
    /// Ensures that the settings directory for the application exists.
    /// </summary>
    public static void EnsureSettingsDirectoryExists()
    {
        var directory = GetSettingsDirectory();
        Directory.CreateDirectory(directory);
    }

    /// <summary>
    /// Asynchronously loads settings from a JSON file or creates default settings if the file does not exist or fails to load.
    /// </summary>
    /// <typeparam name="T">The type of the settings object to load.</typeparam>
    /// <param name="filePath">The path to the settings file.</param>
    /// <param name="defaultFactory">A factory function to create default settings if the file cannot be loaded.</param>
    /// <returns>
    /// A task representing the asynchronous operation. The result contains the loaded settings,
    /// or the default settings if the file does not exist or fails to load.
    /// </returns>
    public static async Task<T> LoadSettingsAsync<T>(string filePath, Func<T> defaultFactory)
        where T : class
    {
        try
        {
            if (!File.Exists(filePath))
            {
                var defaultSettings = defaultFactory();
                await SaveSettingsAsync(filePath, defaultSettings);
                return defaultSettings;
            }

            var json = await File.ReadAllTextAsync(filePath);
            var settings = JsonSerializer.Deserialize<T>(json, JsonOptions);

            return settings ?? defaultFactory();
        }
        catch (Exception ex)
        {
            MessageHandler.MsgDebug($"Error loading settings from {filePath}: {ex.Message}");
            return defaultFactory();
        }
    }

    /// <summary>
    /// Saves the specified settings to a JSON file asynchronously.
    /// </summary>
    /// <typeparam name="T">The type of the settings object.</typeparam>
    /// <param name="filePath">The file path where the settings will be saved.</param>
    /// <param name="settings">The settings object to be saved.</param>
    /// <returns>
    /// A task that represents the asynchronous save operation.
    /// </returns>
    public static async Task SaveSettingsAsync<T>(string filePath, T settings)
        where T : class
    {
        try
        {
            EnsureSettingsDirectoryExists();

            var json = JsonSerializer.Serialize(settings, JsonOptions);
            await File.WriteAllTextAsync(filePath, json);
        }
        catch (Exception ex)
        {
            MessageHandler.MsgDebug($"Error saving settings to {filePath}: {ex.Message}");
            throw;
        }
    }

    /// <summary>
    /// Converts a value to the specified target type for use in application settings.
    /// </summary>
    /// <param name="value">The value to be converted.</param>
    /// <param name="targetType">The type to which the value should be converted.</param>
    /// <returns>The converted value of the specified target type.</returns>
    public static object ConvertValue(object value, Type targetType)
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
    /// Converts an input string to PascalCase format.
    /// </summary>
    /// <param name="input">The input string to be converted.</param>
    /// <returns>The input string converted to PascalCase format.</returns>
    public static string ToPascalCase(string input)
    {
        if (string.IsNullOrEmpty(input))
            return input;

        var words = input.Split(['-', '_', ' '], StringSplitOptions.RemoveEmptyEntries);
        var result = string.Join("", words.Select(w =>
            char.ToUpperInvariant(w[0]) + w.Substring(1).ToLowerInvariant()));

        return result;
    }
}