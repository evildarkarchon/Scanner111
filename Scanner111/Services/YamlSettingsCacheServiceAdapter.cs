using Scanner111.Models;

namespace Scanner111.Services
{
    /// <summary>
    /// Adapter for YamlSettingsCacheService that implements IYamlSettingsCacheService
    /// </summary>
    public class YamlSettingsCacheServiceAdapter : IYamlSettingsCacheService
    {
        private readonly IYamlSettingsCacheService _yamlService;

        public YamlSettingsCacheServiceAdapter()
        {
            _yamlService = YamlSettingsCacheService.Instance;
        }

        public T? GetSetting<T>(YAML yamlType, string path, T? defaultValue = default)
        {
            return _yamlService.GetSetting<T>(yamlType, path, defaultValue);
        }

        public void SetSetting<T>(YAML yamlType, string path, T value)
        {
            _yamlService.SetSetting<T>(yamlType, path, value);
        }
    }
}

