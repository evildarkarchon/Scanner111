using System.Threading.Tasks;
using Scanner111.Core.Infrastructure;
using Scanner111.Core.Models;
using Scanner111.GUI.Models;

namespace Scanner111.GUI.Services;

public interface ISettingsService
{
    Task<ApplicationSettings> LoadSettingsAsync();
    Task SaveSettingsAsync(ApplicationSettings settings);
    ApplicationSettings GetDefaultSettings();

    // Backward compatibility methods for existing GUI code
    Task<UserSettings> LoadUserSettingsAsync();
    Task SaveUserSettingsAsync(UserSettings settings);
}

/// <summary>
/// Provides services for handling application and user settings.
/// This service facilitates loading, saving, and managing settings used
/// throughout the application, with methods for both modern and backward-compatible workflows.
/// </summary>
public class SettingsService : ISettingsService
{
    private readonly ApplicationSettingsService _applicationSettingsService = new ApplicationSettingsService();

    // New unified settings methods
    /// <summary>
    /// Asynchronously loads the application settings.
    /// This method retrieves the application's configuration values
    /// by leveraging the underlying ApplicationSettingsService, ensuring
    /// the latest settings data is fetched.
    /// </summary>
    /// <returns>
    /// A task that represents the asynchronous operation. The task result
    /// contains an instance of <see cref="ApplicationSettings"/> representing
    /// the application's current configuration settings.
    /// </returns>
    public async Task<ApplicationSettings> LoadSettingsAsync()
    {
        return await _applicationSettingsService.LoadSettingsAsync();
    }

    /// <summary>
    /// Asynchronously saves the provided application settings.
    /// This method ensures that the given configuration is persisted by utilizing
    /// the underlying ApplicationSettingsService, making the settings available for future use.
    /// </summary>
    /// <param name="settings">
    /// An instance of <see cref="ApplicationSettings"/> representing the configuration
    /// values to be saved.
    /// </param>
    /// <returns>
    /// A task that represents the asynchronous save operation.
    /// </returns>
    public async Task SaveSettingsAsync(ApplicationSettings settings)
    {
        await _applicationSettingsService.SaveSettingsAsync(settings);
    }

    /// <summary>
    /// Retrieves the default application settings.
    /// This method provides a baseline configuration for the application by
    /// leveraging the <see cref="ApplicationSettingsService"/> to return a pre-defined
    /// set of settings that can be used as initialization defaults.
    /// </summary>
    /// <returns>
    /// An instance of <see cref="ApplicationSettings"/> containing the default configuration values.
    /// </returns>
    public ApplicationSettings GetDefaultSettings()
    {
        return _applicationSettingsService.GetDefaultSettings();
    }

    // Backward compatibility methods for existing GUI code
    /// <summary>
    /// Asynchronously loads the user settings.
    /// This method retrieves user-specific configuration data by leveraging
    /// the application's settings loading mechanism and mapping the data
    /// to a <see cref="UserSettings"/> instance for its use in user-related workflows.
    /// </summary>
    /// <returns>
    /// A task that represents the asynchronous operation. The task result
    /// contains an instance of <see cref="UserSettings"/> representing the user's
    /// configuration settings.
    /// </returns>
    public async Task<UserSettings> LoadUserSettingsAsync()
    {
        var appSettings = await LoadSettingsAsync();
        return MapToUserSettings(appSettings);
    }

    /// <summary>
    /// Asynchronously saves the user-specific settings.
    /// This method updates the application's configuration by mapping
    /// the provided user settings to the application settings and persisting
    /// the changes.
    /// </summary>
    /// <param name="userSettings">
    /// An instance of <see cref="UserSettings"/> containing the user's preferences to be saved.
    /// </param>
    /// <returns>
    /// A task that represents the asynchronous operation. The task completes when
    /// the user settings are successfully saved.
    /// </returns>
    public async Task SaveUserSettingsAsync(UserSettings userSettings)
    {
        var appSettings = await LoadSettingsAsync();
        MapFromUserSettings(userSettings, appSettings);
        await SaveSettingsAsync(appSettings);
    }

    /// <summary>
    /// Maps an instance of <see cref="ApplicationSettings"/> to an instance of <see cref="UserSettings"/>.
    /// This method transforms the application-level settings into a user-specific settings format
    /// while preserving the relevant configuration values.
    /// </summary>
    /// <param name="appSettings">
    /// An instance of <see cref="ApplicationSettings"/> containing the current application configuration values.
    /// </param>
    /// <returns>
    /// An instance of <see cref="UserSettings"/> populated with the mapped values from the application settings.
    /// </returns>
    private UserSettings MapToUserSettings(ApplicationSettings appSettings)
    {
        return new UserSettings
        {
            DefaultLogPath = appSettings.DefaultLogPath,
            DefaultGamePath = appSettings.DefaultGamePath,
            DefaultScanDirectory = appSettings.DefaultScanDirectory,
            AutoLoadF4SeLogs = appSettings.AutoLoadF4SeLogs,
            MaxLogMessages = appSettings.MaxLogMessages,
            EnableProgressNotifications = appSettings.EnableProgressNotifications,
            RememberWindowSize = appSettings.RememberWindowSize,
            WindowWidth = appSettings.WindowWidth,
            WindowHeight = appSettings.WindowHeight,
            EnableDebugLogging = appSettings.EnableDebugLogging,
            RecentLogFiles = appSettings.RecentLogFiles,
            RecentGamePaths = appSettings.RecentGamePaths,
            RecentScanDirectories = appSettings.RecentScanDirectories,
            MaxRecentItems = appSettings.MaxRecentItems,
            LastUsedAnalyzers = appSettings.LastUsedAnalyzers,
            AutoSaveResults = appSettings.AutoSaveResults,
            DefaultOutputFormat = appSettings.DefaultOutputFormat,
            CrashLogsDirectory = appSettings.CrashLogsDirectory,
            SkipXseCopy = appSettings.SkipXseCopy
        };
    }

    /// <summary>
    /// Maps properties from a <see cref="UserSettings"/> instance to an
    /// <see cref="ApplicationSettings"/> instance. This method ensures
    /// the user-provided settings are correctly applied to the application's
    /// configuration.
    /// </summary>
    /// <param name="userSettings">
    /// The source object containing user-defined settings.
    /// </param>
    /// <param name="appSettings">
    /// The target object where the settings will be applied.
    /// </param>
    private void MapFromUserSettings(UserSettings userSettings, ApplicationSettings appSettings)
    {
        appSettings.DefaultLogPath = userSettings.DefaultLogPath;
        appSettings.DefaultGamePath = userSettings.DefaultGamePath;
        appSettings.DefaultScanDirectory = userSettings.DefaultScanDirectory;
        appSettings.AutoLoadF4SeLogs = userSettings.AutoLoadF4SeLogs;
        appSettings.MaxLogMessages = userSettings.MaxLogMessages;
        appSettings.EnableProgressNotifications = userSettings.EnableProgressNotifications;
        appSettings.RememberWindowSize = userSettings.RememberWindowSize;
        appSettings.WindowWidth = userSettings.WindowWidth;
        appSettings.WindowHeight = userSettings.WindowHeight;
        appSettings.EnableDebugLogging = userSettings.EnableDebugLogging;
        appSettings.RecentLogFiles = userSettings.RecentLogFiles;
        appSettings.RecentGamePaths = userSettings.RecentGamePaths;
        appSettings.RecentScanDirectories = userSettings.RecentScanDirectories;
        appSettings.MaxRecentItems = userSettings.MaxRecentItems;
        appSettings.LastUsedAnalyzers = userSettings.LastUsedAnalyzers;
        appSettings.AutoSaveResults = userSettings.AutoSaveResults;
        appSettings.DefaultOutputFormat = userSettings.DefaultOutputFormat;
        appSettings.CrashLogsDirectory = userSettings.CrashLogsDirectory;
        appSettings.SkipXseCopy = userSettings.SkipXseCopy;
    }
}