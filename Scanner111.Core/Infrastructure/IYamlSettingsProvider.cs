using YamlDotNet.Serialization;
using YamlDotNet.Serialization.NamingConventions;

namespace Scanner111.Core.Infrastructure;

/// <summary>
/// Interface for accessing YAML settings with caching
/// </summary>
public interface IYamlSettingsProvider
{
    /// <summary>
    /// Get a setting value from a YAML file
    /// </summary>
    T? GetSetting<T>(string yamlFile, string keyPath, T? defaultValue = default);
    
    /// <summary>
    /// Set a setting value in memory cache
    /// </summary>
    void SetSetting<T>(string yamlFile, string keyPath, T value);
    
    /// <summary>
    /// Load a complete YAML file as an object
    /// </summary>
    T? LoadYaml<T>(string yamlFile) where T : class;
    
    /// <summary>
    /// Clear the settings cache
    /// </summary>
    void ClearCache();
}

/// <summary>
/// Singleton service for YAML settings using CacheManager
/// </summary>
public class YamlSettingsService : IYamlSettingsProvider
{
    private readonly ICacheManager _cacheManager;
    private readonly IDeserializer _deserializer;
    
    public YamlSettingsService(ICacheManager cacheManager)
    {
        _cacheManager = cacheManager;
        _deserializer = new DeserializerBuilder()
            .WithNamingConvention(UnderscoredNamingConvention.Instance)
            .Build();
    }
    
    public T? GetSetting<T>(string yamlFile, string keyPath, T? defaultValue = default)
    {
        return _cacheManager.GetOrSetYamlSetting(
            yamlFile, 
            keyPath, 
            () => LoadSettingFromFile<T>(yamlFile, keyPath, defaultValue),
            TimeSpan.FromMinutes(30));
    }
    
    public void SetSetting<T>(string yamlFile, string keyPath, T value)
    {
        _cacheManager.GetOrSetYamlSetting(yamlFile, keyPath, () => value, TimeSpan.FromMinutes(30));
    }
    
    public T? LoadYaml<T>(string yamlFile) where T : class
    {
        return _cacheManager.GetOrSetYamlSetting(
            yamlFile,
            "__FULL_FILE__",
            () => LoadFullFile<T>(yamlFile),
            TimeSpan.FromMinutes(30));
    }
    
    public void ClearCache()
    {
        _cacheManager.ClearCache();
    }
    
    private T? LoadSettingFromFile<T>(string yamlFile, string keyPath, T? defaultValue)
    {
        try
        {
            var yamlPath = Path.Combine("Data", $"{yamlFile}.yaml");
            System.Console.WriteLine($"DEBUG: Looking for YAML file: {yamlPath} (yamlFile='{yamlFile}', keyPath='{keyPath}')");
            if (!File.Exists(yamlPath))
            {
                System.Console.WriteLine($"DEBUG: YAML file not found: {yamlPath}");
                return defaultValue;
            }
            System.Console.WriteLine($"DEBUG: YAML file found: {yamlPath}");
            
            var yaml = File.ReadAllText(yamlPath, System.Text.Encoding.UTF8);
            var data = _deserializer.Deserialize<Dictionary<string, object>>(yaml);
            
            var value = NavigateKeyPath(data, keyPath);
            return value != null ? ConvertValue<T>(value) : defaultValue;
        }
        catch (Exception)
        {
            return defaultValue;
        }
    }
    
    private T? LoadFullFile<T>(string yamlFile) where T : class
    {
        try
        {
            var yamlPath = Path.Combine("Data", $"{yamlFile}.yaml");
            if (!File.Exists(yamlPath))
                return null;
            
            var yaml = File.ReadAllText(yamlPath, System.Text.Encoding.UTF8);
            return _deserializer.Deserialize<T>(yaml);
        }
        catch (Exception)
        {
            return null;
        }
    }
    
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
    
    private static T? ConvertValue<T>(object value)
    {
        if (value is T directValue)
            return directValue;
        
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