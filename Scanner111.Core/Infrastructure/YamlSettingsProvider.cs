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
}