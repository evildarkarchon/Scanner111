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
    private readonly SemaphoreSlim _fileSemaphore = new(1, 1);

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
        return LoadYamlAsync<T>(yamlFile).GetAwaiter().GetResult();
    }

    public async Task<T?> LoadYamlAsync<T>(string yamlFile) where T : class
    {
        return await _cacheManager.GetOrSetYamlSettingAsync(
            yamlFile,
            "__FULL_FILE__",
            () => LoadFullFileAsync<T>(yamlFile),
            TimeSpan.FromMinutes(30)).ConfigureAwait(false);
    }

    public void ClearCache()
    {
        _cacheManager.ClearCache();
    }

    private async Task<T?> LoadFullFileAsync<T>(string yamlFile) where T : class
    {
        try
        {
            var yamlPath = Path.Combine("Data", $"{yamlFile}.yaml");
            if (!File.Exists(yamlPath))
                return null;

            await _fileSemaphore.WaitAsync().ConfigureAwait(false);
            try
            {
                var yaml = await File.ReadAllTextAsync(yamlPath, Encoding.UTF8).ConfigureAwait(false);
                return _deserializer.Deserialize<T>(yaml);
            }
            finally
            {
                _fileSemaphore.Release();
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to load YAML file: {YamlFile}", yamlFile);
            return null;
        }
    }
}