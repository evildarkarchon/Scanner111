using Scanner111.Models;
using Scanner111.Services;
using System.Collections.Generic;
using YamlDotNet.RepresentationModel;

namespace Scanner111.Tests.Services
{
    /// <summary>
    /// A test implementation of IYamlSettingsCacheService for unit tests
    /// </summary>
    public class TestYamlSettingsCacheService : IYamlSettingsCacheService
    {
        private readonly Dictionary<string, object> _testValues = new Dictionary<string, object>();

        public void SetTestValue<T>(YAML yamlType, string path, T value)
        {
            string key = $"{yamlType}_{path}";
            _testValues[key] = value;
        }

        public T? GetSetting<T>(YAML yamlType, string path, T? defaultValue = default)
        {
            string key = $"{yamlType}_{path}";

            if (_testValues.TryGetValue(key, out object value) && value is T typedValue)
            {
                return typedValue;
            }

            return defaultValue;
        }

        public void SetSetting<T>(YAML yamlType, string path, T value)
        {
            // Store the test value
            string key = $"{yamlType}_{path}";
            _testValues[key] = value;
        }
    }
}
