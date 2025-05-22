using Scanner111.Models;
using Scanner111.Services;

namespace Scanner111.Tests.Services;

/// <summary>
///     A test implementation of IYamlSettingsCacheService for unit tests
/// </summary>
public class TestYamlSettingsCacheService : IYamlSettingsCacheService
{
    private readonly Dictionary<string, object> _testValues = new();

    public T? GetSetting<T>(Yaml yamlType, string path, T? defaultValue = default)
    {
        var key = $"{yamlType}_{path}";

        if (_testValues.TryGetValue(key, out var value) && value is T typedValue) return typedValue;

        return defaultValue;
    }

    public void SetSetting<T>(Yaml yamlType, string path, T value)
    {
        // Store the test value
        var key = $"{yamlType}_{path}";
        _testValues[key] = value;
    }

    public void SetTestValue<T>(Yaml yamlType, string path, T value)
    {
        var key = $"{yamlType}_{path}";
        _testValues[key] = value;
    }
}