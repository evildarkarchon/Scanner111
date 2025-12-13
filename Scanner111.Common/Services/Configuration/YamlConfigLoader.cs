using YamlDotNet.Serialization;
using YamlDotNet.Serialization.NamingConventions;

namespace Scanner111.Common.Services.Configuration;

/// <summary>
/// Implementation of <see cref="IYamlConfigLoader"/> using YamlDotNet.
/// </summary>
public class YamlConfigLoader : IYamlConfigLoader
{
    private readonly IDeserializer _deserializer;

    /// <summary>
    /// Initializes a new instance of the <see cref="YamlConfigLoader"/> class.
    /// </summary>
    public YamlConfigLoader()
    {
        _deserializer = new DeserializerBuilder()
            .WithNamingConvention(CamelCaseNamingConvention.Instance)
            .IgnoreUnmatchedProperties()
            .Build();
    }

    /// <inheritdoc/>
    public async Task<T> LoadAsync<T>(string yamlPath, CancellationToken ct = default)
    {
        if (!File.Exists(yamlPath))
        {
            throw new FileNotFoundException($"YAML file not found: {yamlPath}");
        }

        var content = await File.ReadAllTextAsync(yamlPath, ct).ConfigureAwait(false);
        return _deserializer.Deserialize<T>(content);
    }

    /// <inheritdoc/>
    public async Task<Dictionary<string, object>> LoadDynamicAsync(string yamlPath, CancellationToken ct = default)
    {
        if (!File.Exists(yamlPath))
        {
            throw new FileNotFoundException($"YAML file not found: {yamlPath}");
        }

        var content = await File.ReadAllTextAsync(yamlPath, ct).ConfigureAwait(false);
        return _deserializer.Deserialize<Dictionary<string, object>>(content);
    }
}
