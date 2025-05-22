using System;
using Microsoft.Extensions.Logging;
using Scanner111.Services;

namespace Scanner111.ViewModels;

public class MainViewModel : ViewModelBase
{
    private readonly ILogger<MainViewModel>? _logger;
    private readonly IYamlSettingsCache _yamlSettingsCache;

    // Default constructor for design-time or non-DI usage
    public MainViewModel()
    {
        // Try to get services from the App's ServiceProvider first
        if (App.ServiceProvider != null)
        {
            _yamlSettingsCache = App.ServiceProvider.GetService(typeof(IYamlSettingsCache)) as IYamlSettingsCache ??
                                 throw new InvalidOperationException(
                                     "YAML cache service not registered in DI container");

            _logger = App.ServiceProvider.GetService(typeof(ILogger<MainViewModel>)) as ILogger<MainViewModel>;
        }
        else
        {
            // If ServiceProvider is not available, this is likely design-time
            throw new InvalidOperationException("Service provider is not available");
        }
    }

    // Constructor for dependency injection
    public MainViewModel(IYamlSettingsCache yamlSettingsCache, ILogger<MainViewModel> logger)
    {
        _yamlSettingsCache = yamlSettingsCache;
        _logger = logger;

        // Test the YAML settings cache
        TestYamlSettings();
    }

    public string Greeting => "Welcome to Avalonia!";

    private void TestYamlSettings()
    {
        try
        {
            // Create a test settings file
            var testSetting =
                _yamlSettingsCache.GetSetting<string>(YamlStore.Settings, "TestSection.TestKey", "TestValue");
            _logger?.LogInformation($"Test setting value: {testSetting}");

            // Read it back
            var readSetting = _yamlSettingsCache.GetSetting<string>(YamlStore.Settings, "TestSection.TestKey");
            _logger?.LogInformation($"Read setting value: {readSetting}");
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Error testing YAML settings cache");
        }
    }
}