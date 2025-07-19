using System;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;

namespace Scanner111.Core.Infrastructure;

/// <summary>
/// Common settings infrastructure for both CLI and GUI applications
/// </summary>
public static class SettingsHelper
{
    /// <summary>
    /// Common JsonSerializerOptions for all settings serialization
    /// </summary>
    public static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };
    
    /// <summary>
    /// Gets the Scanner111 settings directory in AppData
    /// </summary>
    public static string GetSettingsDirectory()
    {
        return Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "Scanner111");
    }
    
    /// <summary>
    /// Ensures the settings directory exists
    /// </summary>
    public static void EnsureSettingsDirectoryExists()
    {
        var directory = GetSettingsDirectory();
        Directory.CreateDirectory(directory);
    }
    
    /// <summary>
    /// Loads settings from a JSON file
    /// </summary>
    /// <typeparam name="T">The settings type</typeparam>
    /// <param name="filePath">The path to the settings file</param>
    /// <param name="defaultFactory">Factory function to create default settings</param>
    /// <returns>The loaded settings or default if file doesn't exist or fails to load</returns>
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
            Console.WriteLine($"Error loading settings from {filePath}: {ex.Message}");
            return defaultFactory();
        }
    }
    
    /// <summary>
    /// Saves settings to a JSON file
    /// </summary>
    /// <typeparam name="T">The settings type</typeparam>
    /// <param name="filePath">The path to the settings file</param>
    /// <param name="settings">The settings to save</param>
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
            Console.WriteLine($"Error saving settings to {filePath}: {ex.Message}");
            throw;
        }
    }
    
    /// <summary>
    /// Converts a string value to the appropriate type for settings
    /// </summary>
    /// <param name="value">The value to convert</param>
    /// <param name="targetType">The target type</param>
    /// <returns>The converted value</returns>
    public static object ConvertValue(object value, Type targetType)
    {
        if (value == null)
            return null!;
        
        // Handle string inputs
        if (value is string stringValue)
        {
            if (targetType == typeof(bool))
            {
                return stringValue.ToLowerInvariant() switch
                {
                    "true" or "yes" or "1" or "on" => true,
                    "false" or "no" or "0" or "off" => false,
                    _ => throw new ArgumentException($"Invalid boolean value: {stringValue}")
                };
            }
            else if (targetType == typeof(int))
            {
                return int.Parse(stringValue);
            }
        }
        
        return Convert.ChangeType(value, targetType);
    }
    
    /// <summary>
    /// Converts a string to PascalCase for property matching
    /// </summary>
    /// <param name="input">The input string</param>
    /// <returns>The PascalCase string</returns>
    public static string ToPascalCase(string input)
    {
        if (string.IsNullOrEmpty(input))
            return input;
        
        var words = input.Split(new[] { '-', '_', ' ' }, StringSplitOptions.RemoveEmptyEntries);
        var result = string.Join("", words.Select(w => 
            char.ToUpperInvariant(w[0]) + w.Substring(1).ToLowerInvariant()));
        
        return result;
    }
}