using YamlDotNet.Serialization;
using YamlDotNet.Serialization.NamingConventions;

namespace Scanner111.Core.Infrastructure;

/// <summary>
/// Provides caching for YAML configuration files with key path navigation
/// </summary>
public static class YamlSettingsCache
{
    private static readonly Dictionary<string, object?> _cache = new();
    private static readonly IDeserializer _deserializer = new DeserializerBuilder()
        .WithNamingConvention(UnderscoredNamingConvention.Instance)
        .Build();
    
    /// <summary>
    /// Get a setting value from a YAML file with caching
    /// </summary>
    /// <typeparam name="T">Type of value to return</typeparam>
    /// <param name="yamlFile">YAML filename (without extension)</param>
    /// <param name="keyPath">Dot-separated path to the setting (e.g., "CLASSIC_Settings.Show FormID Values")</param>
    /// <param name="defaultValue">Default value if not found</param>
    /// <returns>The setting value or default</returns>
    public static T? YamlSettings<T>(string yamlFile, string keyPath, T? defaultValue = default)
    {
        var cacheKey = $"{yamlFile}:{keyPath}";
        
        if (_cache.TryGetValue(cacheKey, out var cached))
            return (T?)cached;
        
        try
        {
            var yamlPath = Path.Combine("CLASSIC Data", "databases", $"{yamlFile}.yaml");
            if (!File.Exists(yamlPath))
                return defaultValue;
            
            var yaml = File.ReadAllText(yamlPath, System.Text.Encoding.UTF8);
            var data = _deserializer.Deserialize<Dictionary<string, object>>(yaml);
            
            // Navigate key path (e.g., "CLASSIC_Settings.Show FormID Values")
            var value = NavigateKeyPath(data, keyPath);
            
            _cache[cacheKey] = value;
            return value != null ? ConvertValue<T>(value) : defaultValue;
        }
        catch (Exception ex)
        {
            // Log error but don't throw - return default value
            Console.WriteLine($"Error loading YAML setting {yamlFile}:{keyPath} - {ex.Message}");
            return defaultValue;
        }
    }
    
    /// <summary>
    /// Set a setting value in a YAML file (for future implementation)
    /// </summary>
    /// <typeparam name="T">Type of value to set</typeparam>
    /// <param name="yamlFile">YAML filename (without extension)</param>
    /// <param name="keyPath">Dot-separated path to the setting</param>
    /// <param name="value">Value to set</param>
    public static void SetYamlSetting<T>(string yamlFile, string keyPath, T value)
    {
        // TODO: Implement saving settings back to YAML file
        var cacheKey = $"{yamlFile}:{keyPath}";
        _cache[cacheKey] = value;
    }
    
    /// <summary>
    /// Clear the settings cache
    /// </summary>
    public static void ClearCache()
    {
        _cache.Clear();
    }
    
    /// <summary>
    /// Navigate a nested dictionary using dot notation
    /// </summary>
    /// <param name="data">Root dictionary</param>
    /// <param name="keyPath">Dot-separated path</param>
    /// <returns>Value at path or null if not found</returns>
    private static object? NavigateKeyPath(Dictionary<string, object> data, string keyPath)
    {
        var parts = keyPath.Split('.');
        object? current = data;
        
        foreach (var part in parts)
        {
            if (current is Dictionary<string, object> stringDict)
            {
                if (!stringDict.TryGetValue(part, out current))
                    return null;
            }
            else if (current is Dictionary<object, object> objectDict)
            {
                if (!objectDict.TryGetValue(part, out current))
                    return null;
            }
            else
            {
                return null;
            }
        }
        
        return current;
    }
    
    /// <summary>
    /// Convert a value to the specified type
    /// </summary>
    /// <typeparam name="T">Target type</typeparam>
    /// <param name="value">Value to convert</param>
    /// <returns>Converted value</returns>
    private static T? ConvertValue<T>(object value)
    {
        if (value is T directValue)
            return directValue;
        
        // Handle common type conversions
        if (typeof(T) == typeof(bool) && value is string strValue)
        {
            if (bool.TryParse(strValue, out var boolValue))
                return (T)(object)boolValue;
        }
        
        if (typeof(T) == typeof(int) && value is string intStrValue)
        {
            if (int.TryParse(intStrValue, out var intValue))
                return (T)(object)intValue;
        }
        
        if (typeof(T) == typeof(double) && value is string doubleStrValue)
        {
            if (double.TryParse(doubleStrValue, out var doubleValue))
                return (T)(object)doubleValue;
        }
        
        // Try generic conversion
        try
        {
            return (T)Convert.ChangeType(value, typeof(T));
        }
        catch
        {
            return default;
        }
    }
}