using System;
using System.IO;
using System.Text.Json;
using System.Threading.Tasks;
using Scanner111.GUI.Models;
using Scanner111.Core.Infrastructure;
using Scanner111.Core.Models;

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

public class SettingsService : ISettingsService
{
    private readonly IApplicationSettingsService _applicationSettingsService;

    public SettingsService()
    {
        _applicationSettingsService = new ApplicationSettingsService();
    }

    // New unified settings methods
    public async Task<ApplicationSettings> LoadSettingsAsync()
    {
        return await _applicationSettingsService.LoadSettingsAsync();
    }

    public async Task SaveSettingsAsync(ApplicationSettings settings)
    {
        await _applicationSettingsService.SaveSettingsAsync(settings);
    }

    public ApplicationSettings GetDefaultSettings()
    {
        return _applicationSettingsService.GetDefaultSettings();
    }

    // Backward compatibility methods for existing GUI code
    public async Task<UserSettings> LoadUserSettingsAsync()
    {
        var appSettings = await LoadSettingsAsync();
        return MapToUserSettings(appSettings);
    }

    public async Task SaveUserSettingsAsync(UserSettings userSettings)
    {
        var appSettings = await LoadSettingsAsync();
        MapFromUserSettings(userSettings, appSettings);
        await SaveSettingsAsync(appSettings);
    }

    private UserSettings MapToUserSettings(ApplicationSettings appSettings)
    {
        return new UserSettings
        {
            DefaultLogPath = appSettings.DefaultLogPath,
            DefaultGamePath = appSettings.DefaultGamePath,
            DefaultScanDirectory = appSettings.DefaultScanDirectory,
            AutoLoadF4SELogs = appSettings.AutoLoadF4SELogs,
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
            SkipXSECopy = appSettings.SkipXSECopy
        };
    }

    private void MapFromUserSettings(UserSettings userSettings, ApplicationSettings appSettings)
    {
        appSettings.DefaultLogPath = userSettings.DefaultLogPath;
        appSettings.DefaultGamePath = userSettings.DefaultGamePath;
        appSettings.DefaultScanDirectory = userSettings.DefaultScanDirectory;
        appSettings.AutoLoadF4SELogs = userSettings.AutoLoadF4SELogs;
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
        appSettings.SkipXSECopy = userSettings.SkipXSECopy;
    }
}