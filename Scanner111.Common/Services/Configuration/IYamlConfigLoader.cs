namespace Scanner111.Common.Services.Configuration;

/// <summary>
/// Interface for loading YAML configuration files.
/// </summary>
public interface IYamlConfigLoader
{
    /// <summary>
    /// Loads and deserializes a YAML file into a strongly-typed object.
    /// </summary>
    /// <typeparam name="T">The type to deserialize into.</typeparam>
    /// <param name="yamlPath">The path to the YAML file.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>The deserialized object.</returns>
    Task<T> LoadAsync<T>(string yamlPath, CancellationToken ct = default);

    /// <summary>
    /// Loads a YAML file into a dynamic dictionary structure.
    /// </summary>
    /// <param name="yamlPath">The path to the YAML file.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>A dictionary representing the YAML content.</returns>
    Task<Dictionary<string, object>> LoadDynamicAsync(string yamlPath, CancellationToken ct = default);
}
