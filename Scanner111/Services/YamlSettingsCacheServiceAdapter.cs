using Scanner111.Models;

namespace Scanner111.Services
{
    /// <summary>
    /// Adapter for YamlSettingsCacheService that implements IYamlSettingsCacheService
    /// </summary>
    public class YamlSettingsCacheServiceAdapter : IYamlSettingsCacheService
    {
        private readonly YamlSettingsCacheService _yamlService;

        public YamlSettingsCacheServiceAdapter()
        {
            _yamlService = YamlSettingsCacheService.Instance;
        }

        public T? GetSetting<T>(YAML yamlType, string path, T? defaultValue = default)
        {
            return _yamlService.GetSetting<T>(yamlType, path, defaultValue);
        }

        public T? GetSetting<T>(YamlStoreType storeType, string path)
        {
            return _yamlService.GetSetting<T>(storeType, path);
        }
    }
}
