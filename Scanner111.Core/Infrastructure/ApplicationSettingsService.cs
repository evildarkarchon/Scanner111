using Scanner111.Core.Models;
using System.Linq;

namespace Scanner111.Core.Infrastructure;

public class ApplicationSettingsService : IApplicationSettingsService
{
    private ApplicationSettings? _cachedSettings;
    
    private string GetSettingsFilePath()
    {
        var settingsPath = Environment.GetEnvironmentVariable("SCANNER111_SETTINGS_PATH");
        if (!string.IsNullOrEmpty(settingsPath))
        {
            return Path.Combine(settingsPath, "settings.json");
        }
        return Path.Combine(SettingsHelper.GetSettingsDirectory(), "settings.json");
    }

    /// Asynchronously loads application settings from a predefined file path.
    /// If the settings file does not exist or is invalid, default settings are loaded and returned.
    /// The loaded settings are cached for future access.
    /// <returns>An instance of ApplicationSettings containing the current configuration.</returns>
    public async Task<ApplicationSettings> LoadSettingsAsync()
    {
        var settings = await SettingsHelper.LoadSettingsAsync(GetSettingsFilePath(), GetDefaultSettings).ConfigureAwait(false);
        _cachedSettings = settings;
        return settings;
    }

    /// Asynchronously saves the provided application settings to a predefined file location.
    /// The settings are also cached in memory for future access.
    /// <param name="settings">An instance of ApplicationSettings containing configuration values to be saved.</param>
    /// <returns>A Task representing the asynchronous save operation.</returns>
    public async Task SaveSettingsAsync(ApplicationSettings settings)
    {
        await SettingsHelper.SaveSettingsAsync(GetSettingsFilePath(), settings).ConfigureAwait(false);
        _cachedSettings = settings;
    }

    /// Asynchronously updates a specific setting within the application settings file.
    /// The method identifies the setting by the provided key, updates its value, and saves the changes to the settings file.
    /// If the key does not match any property in the application settings or the value cannot be converted to the property's type, an exception is thrown.
    /// <param name="key">The name of the setting to be updated (case-insensitive).</param>
    /// <param name="value">The new value to assign to the setting. The value must be convertible to the property's data type.</param>
    /// <returns>A task that represents the asynchronous save operation.</returns>
    /// <exception cref="ArgumentException">
    /// Thrown if the key does not correspond to an existing property of the application settings,
    /// or if the value cannot be converted to the property's type.
    /// </exception>
    public async Task SaveSettingAsync(string key, object value)
    {
        var settings = _cachedSettings ?? await LoadSettingsAsync().ConfigureAwait(false);

        // Update the specific setting using reflection
        // First try exact match, then case-insensitive match, then PascalCase conversion
        var property = typeof(ApplicationSettings).GetProperty(key) 
            ?? typeof(ApplicationSettings).GetProperties()
                .FirstOrDefault(p => string.Equals(p.Name, key, StringComparison.OrdinalIgnoreCase))
            ?? typeof(ApplicationSettings).GetProperty(SettingsHelper.ToPascalCase(key));
            
        if (property != null && property.CanWrite)
            try
            {
                // Convert value to appropriate type
                var convertedValue = SettingsHelper.ConvertValue(value, property.PropertyType);
                property.SetValue(settings, convertedValue);
                await SaveSettingsAsync(settings).ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                throw new ArgumentException($"Failed to set {key}: {ex.Message}", ex);
            }
        else
            throw new ArgumentException($"Unknown setting: {key}");
    }

    /// Retrieves an instance of the default application settings with all properties
    /// initialized to predefined values. These default values include configurations
    /// for core functionality, paths, output formats, performance, logging, notifications,
    /// and both CLI- and GUI-specific settings.
    /// <returns>An instance of ApplicationSettings with default predefined values.</returns>
    public ApplicationSettings GetDefaultSettings()
    {
        return new ApplicationSettings
        {
            // Core Analysis Settings
            FcxMode = false,
            ShowFormIdValues = false,
            SimplifyLogs = false,
            MoveUnsolvedLogs = false,
            VrMode = false,

            // Path Settings
            DefaultLogPath = "",
            DefaultGamePath = GamePathDetection.TryDetectGamePath(),
            DefaultScanDirectory = "",
            CrashLogsDirectory = "",

            // Output Settings
            DefaultOutputFormat = "detailed",
            AutoSaveResults = true,

            // XSE Settings
            AutoLoadF4SeLogs = true,
            SkipXseCopy = false,

            // Performance Settings
            MaxConcurrentScans = Environment.ProcessorCount * 2,
            CacheEnabled = true,

            // Debug/Logging Settings
            EnableDebugLogging = false,
            VerboseLogging = false,

            // Notification Settings
            AudioNotifications = false,
            EnableProgressNotifications = true,

            // CLI-Specific Display Settings
            DisableColors = false,
            DisableProgress = false,

            // GUI-Specific Settings
            RememberWindowSize = true,
            WindowWidth = 1200,
            WindowHeight = 800,
            MaxLogMessages = 100,

            // Recent Items
            MaxRecentItems = 10
        };
    }
}