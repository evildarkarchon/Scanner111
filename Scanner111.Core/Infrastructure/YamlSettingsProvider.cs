using System.Text;
using Microsoft.Extensions.Logging;
using YamlDotNet.Serialization;
using YamlDotNet.Serialization.NamingConventions;

namespace Scanner111.Core.Infrastructure;

/// <summary>
///     Singleton service for YAML settings using CacheManager
/// </summary>
public class YamlSettingsService : IYamlSettingsProvider
{
    private readonly ICacheManager _cacheManager;
    private readonly IDeserializer _deserializer;
    private readonly ISerializer _serializer;
    private readonly ILogger<YamlSettingsService> _logger;

    public YamlSettingsService(ICacheManager cacheManager, ILogger<YamlSettingsService> logger)
    {
        _cacheManager = cacheManager;
        _logger = logger;
        _deserializer = new DeserializerBuilder()
            .WithNamingConvention(UnderscoredNamingConvention.Instance)
            .IgnoreUnmatchedProperties()
            .Build();
        
        _serializer = new SerializerBuilder()
            .WithNamingConvention(UnderscoredNamingConvention.Instance)
            .Build();
    }

    [Obsolete("Use LoadYaml<T> method instead for strongly-typed YAML access")]
    public T? GetSetting<T>(string yamlFile, string keyPath, T? defaultValue = default)
    {
        return _cacheManager.GetOrSetYamlSetting(
            yamlFile,
            keyPath,
            () => LoadSettingFromFile(yamlFile, keyPath, defaultValue),
            TimeSpan.FromMinutes(30));
    }

    [Obsolete("YAML data files are read-only. Use LoadYaml<T> method for reading YAML data")]
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
            _logger.LogDebug("Looking for YAML file: {YamlPath} (yamlFile='{YamlFile}', keyPath='{KeyPath}')", yamlPath, yamlFile, keyPath);
            if (!File.Exists(yamlPath))
            {
                _logger.LogDebug("YAML file not found: {YamlPath}", yamlPath);
                return defaultValue;
            }

            _logger.LogDebug("YAML file found: {YamlPath}", yamlPath);

            var yaml = File.ReadAllText(yamlPath, Encoding.UTF8);
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

            var yaml = File.ReadAllText(yamlPath, Encoding.UTF8);
            return _deserializer.Deserialize<T>(yaml);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to load YAML file: {YamlFile}", yamlFile);
            return null;
        }
    }

    private static object? NavigateKeyPath(Dictionary<string, object> data, string keyPath)
    {
        var parts = keyPath.Split('.');
        object? current = data;

        foreach (var part in parts)
            switch (current)
            {
                case Dictionary<string, object> stringDict:
                    {
                        if (!stringDict.TryGetValue(part, out current))
                            return null;
                        break;
                    }
                case Dictionary<object, object> objectDict:
                    {
                        if (!objectDict.TryGetValue(part, out current))
                            return null;
                        break;
                    }
                default:
                    return null;
            }

        return current;
    }

    private static T? ConvertValue<T>(object value)
    {
        if (value is T directValue)
            return directValue;

        // Handle List<object> to List<string> conversion
        if (typeof(T) == typeof(List<string>) && value is List<object> objList)
        {
            var stringList = objList.Select(o => o.ToString()).Where(s => s != null).ToList();
            return (T)(object)stringList;
        }

        if (typeof(T) == typeof(bool) && value is string strValue)
            if (bool.TryParse(strValue, out var boolValue))
                return (T)(object)boolValue;

        if (typeof(T) == typeof(int) && value is string intStrValue)
            if (int.TryParse(intStrValue, out var intValue))
                return (T)(object)intValue;

        if (typeof(T) == typeof(double) && value is string doubleStrValue)
            if (double.TryParse(doubleStrValue, out var doubleValue))
                return (T)(object)doubleValue;

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