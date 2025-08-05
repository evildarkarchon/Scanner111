using Scanner111.CLI.Models;
using Scanner111.Core.Infrastructure;
using Scanner111.Core.Models;

namespace Scanner111.CLI.Services;

/// <summary>
/// Implements the functionality for managing and persisting CLI-related settings, ensuring compatibility with both legacy configurations and updated application-wide settings.
/// </summary>
public class CliSettingsService : ICliSettingsService
{
    private readonly IApplicationSettingsService _applicationSettingsService = new ApplicationSettingsService();

    // New unified settings methods
    public async Task<ApplicationSettings> LoadSettingsAsync()
    {
        return await _applicationSettingsService.LoadSettingsAsync();
    }

    public async Task SaveSettingsAsync(ApplicationSettings settings)
    {
        await _applicationSettingsService.SaveSettingsAsync(settings);
    }

    public async Task SaveSettingAsync(string key, object value)
    {
        await _applicationSettingsService.SaveSettingAsync(key, value);
    }

    public ApplicationSettings GetDefaultSettings()
    {
        return _applicationSettingsService.GetDefaultSettings();
    }

    // Backward compatibility methods for existing CLI code
    public async Task<CliSettings> LoadCliSettingsAsync()
    {
        var appSettings = await LoadSettingsAsync();
        return MapToCliSettings(appSettings);
    }

    public async Task SaveCliSettingsAsync(CliSettings cliSettings)
    {
        var appSettings = await LoadSettingsAsync();
        MapFromCliSettings(cliSettings, appSettings);
        await SaveSettingsAsync(appSettings);
    }

    private CliSettings MapToCliSettings(ApplicationSettings appSettings)
    {
        return new CliSettings
        {
            FcxMode = appSettings.FcxMode,
            ShowFormIdValues = appSettings.ShowFormIdValues,
            SimplifyLogs = appSettings.SimplifyLogs,
            MoveUnsolvedLogs = appSettings.MoveUnsolvedLogs,
            AudioNotifications = appSettings.AudioNotifications,
            VrMode = appSettings.VrMode,
            DefaultScanDirectory = appSettings.DefaultScanDirectory,
            DefaultGamePath = appSettings.DefaultGamePath,
            DefaultOutputFormat = appSettings.DefaultOutputFormat,
            DisableColors = appSettings.DisableColors,
            DisableProgress = appSettings.DisableProgress,
            VerboseLogging = appSettings.VerboseLogging,
            MaxConcurrentScans = appSettings.MaxConcurrentScans,
            CacheEnabled = appSettings.CacheEnabled,
            CrashLogsDirectory = appSettings.CrashLogsDirectory,
            RecentScanPaths = appSettings.RecentScanDirectories,
            MaxRecentPaths = appSettings.MaxRecentItems
        };
    }

    private void MapFromCliSettings(CliSettings cliSettings, ApplicationSettings appSettings)
    {
        appSettings.FcxMode = cliSettings.FcxMode;
        appSettings.ShowFormIdValues = cliSettings.ShowFormIdValues;
        appSettings.SimplifyLogs = cliSettings.SimplifyLogs;
        appSettings.MoveUnsolvedLogs = cliSettings.MoveUnsolvedLogs;
        appSettings.AudioNotifications = cliSettings.AudioNotifications;
        appSettings.VrMode = cliSettings.VrMode;
        appSettings.DefaultScanDirectory = cliSettings.DefaultScanDirectory;
        appSettings.DefaultGamePath = cliSettings.DefaultGamePath;
        appSettings.DefaultOutputFormat = cliSettings.DefaultOutputFormat;
        appSettings.DisableColors = cliSettings.DisableColors;
        appSettings.DisableProgress = cliSettings.DisableProgress;
        appSettings.VerboseLogging = cliSettings.VerboseLogging;
        appSettings.MaxConcurrentScans = cliSettings.MaxConcurrentScans;
        appSettings.CacheEnabled = cliSettings.CacheEnabled;
        appSettings.CrashLogsDirectory = cliSettings.CrashLogsDirectory;
        appSettings.RecentScanDirectories = cliSettings.RecentScanPaths ?? new List<string>();
        appSettings.MaxRecentItems = cliSettings.MaxRecentPaths;
    }
}