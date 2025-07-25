using System.Threading.Tasks;
using Scanner111.Core.Infrastructure;
using Scanner111.Core.Models;

namespace Scanner111.GUI.Services;

/// <summary>
/// Adapter that bridges between GUI's ISettingsService and Core's IApplicationSettingsService
/// </summary>
public class GuiApplicationSettingsAdapter : IApplicationSettingsService
{
    private readonly ISettingsService _settingsService;

    public GuiApplicationSettingsAdapter(ISettingsService settingsService)
    {
        _settingsService = settingsService;
    }

    public async Task<ApplicationSettings> LoadSettingsAsync()
    {
        var userSettings = await _settingsService.LoadUserSettingsAsync();
        
        // Convert UserSettings to ApplicationSettings
        return new ApplicationSettings
        {
            // Core Analysis Settings
            FcxMode = userSettings.FcxMode,
            ShowFormIdValues = false, // Not in UserSettings
            SimplifyLogs = false, // Not in UserSettings
            MoveUnsolvedLogs = false, // Not in UserSettings
            VrMode = false, // Not in UserSettings
            
            // Path Settings
            DefaultLogPath = userSettings.DefaultLogPath,
            DefaultGamePath = userSettings.DefaultGamePath,
            DefaultScanDirectory = userSettings.DefaultScanDirectory,
            CrashLogsDirectory = userSettings.CrashLogsDirectory,
            BackupDirectory = userSettings.BackupDirectory,
            
            // Output Settings
            DefaultOutputFormat = userSettings.DefaultOutputFormat,
            AutoSaveResults = userSettings.AutoSaveResults,
            
            // XSE Settings
            AutoLoadF4SeLogs = userSettings.AutoLoadF4SeLogs,
            SkipXseCopy = userSettings.SkipXseCopy,
            
            // Performance Settings
            MaxConcurrentScans = 16, // Not in UserSettings
            CacheEnabled = true, // Not in UserSettings
            
            // Debug/Logging Settings
            EnableDebugLogging = userSettings.EnableDebugLogging,
            VerboseLogging = false, // Not in UserSettings
            
            // Notification Settings
            AudioNotifications = false, // Not in UserSettings
            EnableProgressNotifications = userSettings.EnableProgressNotifications,
            
            // Update Check Settings
            EnableUpdateCheck = userSettings.EnableUpdateCheck,
            UpdateSource = userSettings.UpdateSource,
            
            // CLI-Specific Display Settings
            DisableColors = false, // Not in UserSettings
            DisableProgress = false, // Not in UserSettings
            
            // GUI-Specific Settings
            RememberWindowSize = userSettings.RememberWindowSize,
            WindowWidth = userSettings.WindowWidth,
            WindowHeight = userSettings.WindowHeight,
            MaxLogMessages = userSettings.MaxLogMessages,
            
            // Recent Items Management
            RecentLogFiles = userSettings.RecentLogFiles,
            RecentGamePaths = userSettings.RecentGamePaths,
            RecentScanDirectories = userSettings.RecentScanDirectories,
            MaxRecentItems = userSettings.MaxRecentItems,
            LastUsedAnalyzers = userSettings.LastUsedAnalyzers
        };
    }

    public async Task SaveSettingsAsync(ApplicationSettings settings)
    {
        var userSettings = await _settingsService.LoadUserSettingsAsync();
        
        // Update UserSettings with values from ApplicationSettings
        userSettings.FcxMode = settings.FcxMode;
        userSettings.DefaultLogPath = settings.DefaultLogPath;
        userSettings.DefaultGamePath = settings.DefaultGamePath;
        userSettings.DefaultScanDirectory = settings.DefaultScanDirectory;
        userSettings.CrashLogsDirectory = settings.CrashLogsDirectory;
        userSettings.BackupDirectory = settings.BackupDirectory;
        userSettings.DefaultOutputFormat = settings.DefaultOutputFormat;
        userSettings.AutoSaveResults = settings.AutoSaveResults;
        userSettings.AutoLoadF4SeLogs = settings.AutoLoadF4SeLogs;
        userSettings.SkipXseCopy = settings.SkipXseCopy;
        userSettings.EnableDebugLogging = settings.EnableDebugLogging;
        userSettings.EnableProgressNotifications = settings.EnableProgressNotifications;
        userSettings.EnableUpdateCheck = settings.EnableUpdateCheck;
        userSettings.UpdateSource = settings.UpdateSource;
        userSettings.RememberWindowSize = settings.RememberWindowSize;
        userSettings.WindowWidth = settings.WindowWidth;
        userSettings.WindowHeight = settings.WindowHeight;
        userSettings.MaxLogMessages = settings.MaxLogMessages;
        userSettings.RecentLogFiles = settings.RecentLogFiles;
        userSettings.RecentGamePaths = settings.RecentGamePaths;
        userSettings.RecentScanDirectories = settings.RecentScanDirectories;
        userSettings.MaxRecentItems = settings.MaxRecentItems;
        userSettings.LastUsedAnalyzers = settings.LastUsedAnalyzers;
        userSettings.ModsFolder = settings.ModsFolder ?? "";
        userSettings.IniFolder = settings.IniFolder ?? "";
        
        await _settingsService.SaveUserSettingsAsync(userSettings);
    }

    public async Task SaveSettingAsync(string key, object value)
    {
        var settings = await LoadSettingsAsync();
        
        // Use reflection to set the property value
        var property = settings.GetType().GetProperty(key);
        if (property != null && property.CanWrite)
        {
            property.SetValue(settings, value);
            await SaveSettingsAsync(settings);
        }
    }

    public ApplicationSettings GetDefaultSettings()
    {
        return new ApplicationSettings();
    }
}