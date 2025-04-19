// Scanner111.Application.Interfaces.Services.IYamlCompatibilityService.cs
namespace Scanner111.Application.Interfaces.Services;

/// <summary>
/// Service interface for parsing YAML files in the legacy CLASSIC format
/// </summary>
public interface IYamlCompatibilityService
{
    /// <summary>
    /// Loads a YAML file and returns its content as a dictionary
    /// </summary>
    /// <param name="filePath">Path to the YAML file</param>
    /// <returns>Dictionary representation of the YAML content</returns>
    Task<Dictionary<string, object>> LoadYamlFileAsync(string filePath);
    
    /// <summary>
    /// Loads a YAML file and deserializes it to the specified type
    /// </summary>
    /// <typeparam name="T">Type to deserialize to</typeparam>
    /// <param name="filePath">Path to the YAML file</param>
    /// <returns>Deserialized object of type T</returns>
    Task<T?> LoadYamlFileTypedAsync<T>(string filePath) where T : class, new();
    
    /// <summary>
    /// Loads crash suspects from the CLASSIC YAML format
    /// </summary>
    /// <param name="yamlPath">Path to the YAML file</param>
    /// <param name="sectionName">Section name within the YAML file (e.g., "Crashlog_Error_Check")</param>
    /// <returns>Dictionary mapping crash suspect names to their descriptions</returns>
    Task<Dictionary<string, string>> LoadCrashSuspectsAsync(string yamlPath, string sectionName);
    
    /// <summary>
    /// Loads crash stack checks from the CLASSIC YAML format
    /// </summary>
    /// <param name="yamlPath">Path to the YAML file</param>
    /// <returns>Dictionary mapping crash stack check names to their signal lists</returns>
    Task<Dictionary<string, List<string>>> LoadCrashStackCheckAsync(string yamlPath);
    
    /// <summary>
    /// Loads mods list from the CLASSIC YAML format
    /// </summary>
    /// <param name="yamlPath">Path to the YAML file</param>
    /// <param name="sectionName">Section name within the YAML file (e.g., "Mods_FREQ", "Mods_CONF")</param>
    /// <returns>Dictionary mapping mod names to their descriptions</returns>
    Task<Dictionary<string, string>> LoadModsListAsync(string yamlPath, string sectionName);
}