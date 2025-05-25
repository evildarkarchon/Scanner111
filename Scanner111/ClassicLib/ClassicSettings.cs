using System;
using System.IO;
using Microsoft.Extensions.Logging;

namespace Scanner111.ClassicLib;

/// <summary>
/// Provides access to CLASSIC settings stored in the CLASSIC Settings.yaml file.
/// </summary>
public class ClassicSettings
{
    private readonly IFileSystem _fileSystem;
    private readonly ILogger<ClassicSettings> _logger;
    private readonly YamlSettings _yamlSettings;

    /// <summary>
    /// Initializes a new instance of the <see cref="ClassicSettings"/> class.
    /// </summary>
    /// <param name="yamlSettings">The YAML settings service.</param>
    /// <param name="fileSystem">The file system service.</param>
    /// <param name="logger">The logger.</param>
    public ClassicSettings(
        YamlSettings yamlSettings,
        IFileSystem fileSystem,
        ILogger<ClassicSettings> logger)
    {
        _yamlSettings = yamlSettings ?? throw new ArgumentNullException(nameof(yamlSettings));
        _fileSystem = fileSystem ?? throw new ArgumentNullException(nameof(fileSystem));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));

        EnsureSettingsFileExists();
    }

    /// <summary>
    /// Fetches a specific setting from the CLASSIC settings file.
    /// </summary>
    /// <typeparam name="T">The expected type of the setting value.</typeparam>
    /// <param name="setting">The key of the setting to retrieve.</param>
    /// <returns>The value of the requested setting.</returns>
    public T? GetSetting<T>(string setting)
    {
        return _yamlSettings.GetSetting<T>(YamlStoreType.Settings, $"CLASSIC_Settings.{setting}");
    }

    /// <summary>
    /// Updates a specific setting in the CLASSIC settings file.
    /// </summary>
    /// <typeparam name="T">The type of the setting value.</typeparam>
    /// <param name="setting">The key of the setting to update.</param>
    /// <param name="value">The new value for the setting.</param>
    /// <returns>The updated value.</returns>
    public T? UpdateSetting<T>(string setting, T value)
    {
        return _yamlSettings.GetSetting(YamlStoreType.Settings, $"CLASSIC_Settings.{setting}", value);
    }

    /// <summary>
    /// Ensures the existence of the CLASSIC Settings.yaml file in the current directory.
    /// If the file does not exist, it creates the file using the default settings retrieved
    /// from the Main YAML configuration.
    /// </summary>
    /// <exception cref="InvalidOperationException">
    /// Thrown if the default settings in 'CLASSIC Main.yaml' are invalid or unavailable.
    /// </exception>
    private void EnsureSettingsFileExists()
    {
        var settingsPath = Path.Combine(Directory.GetCurrentDirectory(), "CLASSIC Settings.yaml");

        if (!_fileSystem.FileExists(settingsPath))
        {
            _logger.LogInformation("Creating default CLASSIC Settings.yaml file");

            // Get default settings from Main YAML
            var defaultSettings = _yamlSettings.GetSetting<string>(
                YamlStoreType.Main, "CLASSIC_Info.default_settings");

            if (string.IsNullOrEmpty(defaultSettings))
            {
                _logger.LogError("Invalid Default Settings in 'CLASSIC Main.yaml'");
                throw new InvalidOperationException("Invalid Default Settings in 'CLASSIC Main.yaml'");
            }

            // Create the settings file
            _fileSystem.WriteAllText(settingsPath, defaultSettings);
        }
    }
}