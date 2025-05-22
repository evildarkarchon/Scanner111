using Scanner111.Models;

namespace Scanner111.Services;

/// <summary>
///     Adapter for YamlSettingsCacheService that implements IYamlSettingsCacheService
/// </summary>
public class YamlSettingsCacheServiceAdapter : IYamlSettingsCacheService
{
    private readonly IYamlSettingsCacheService _yamlService;

    public YamlSettingsCacheServiceAdapter()
    {
        _yamlService = YamlSettingsCacheService.Instance;
    }

    public T? GetSetting<T>(Yaml yamlType, string path, T? defaultValue = default)
    {
        return _yamlService.GetSetting<T>(yamlType, path, defaultValue);
    }

    public void SetSetting<T>(Yaml yamlType, string path, T value)
    {
        _yamlService.SetSetting<T>(yamlType, path, value);
    }

    /// <summary>
    ///     Gets a setting from the cache
    /// </summary>
    /// <typeparam name="T">Type of setting to retrieve</typeparam>
    /// <param name="section">YAML section</param>
    /// <param name="key">Setting key</param>
    /// <returns>The setting value or default for type if not found</returns>
    public T? GetSetting<T>(Yaml section, string key)
    {
        // Use existing path-based method to implement this simplified version
        return GetSetting<T>(section, key, default);
    }
}