using System;
using System.IO;
using System.Threading.Tasks;
using Scanner111.Core.Models;

namespace Scanner111.Core.Infrastructure;

public class ApplicationSettingsService : IApplicationSettingsService
{
    private static readonly string SettingsFilePath = Path.Combine(SettingsHelper.GetSettingsDirectory(), "settings.json");
    
    private ApplicationSettings? _cachedSettings;
    
    public async Task<ApplicationSettings> LoadSettingsAsync()
    {
        var settings = await SettingsHelper.LoadSettingsAsync(SettingsFilePath, GetDefaultSettings);
        _cachedSettings = settings;
        return settings;
    }
    
    public async Task SaveSettingsAsync(ApplicationSettings settings)
    {
        await SettingsHelper.SaveSettingsAsync(SettingsFilePath, settings);
        _cachedSettings = settings;
    }
    
    public async Task SaveSettingAsync(string key, object value)
    {
        var settings = _cachedSettings ?? await LoadSettingsAsync();
        
        // Update the specific setting using reflection
        var property = typeof(ApplicationSettings).GetProperty(key);
        if (property == null)
        {
            // Try with different casing conventions
            property = typeof(ApplicationSettings).GetProperty(SettingsHelper.ToPascalCase(key));
        }
        
        if (property != null && property.CanWrite)
        {
            try
            {
                // Convert value to appropriate type
                var convertedValue = SettingsHelper.ConvertValue(value, property.PropertyType);
                property.SetValue(settings, convertedValue);
                await SaveSettingsAsync(settings);
            }
            catch (Exception ex)
            {
                throw new ArgumentException($"Failed to set {key}: {ex.Message}", ex);
            }
        }
        else
        {
            throw new ArgumentException($"Unknown setting: {key}");
        }
    }
    
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
            AutoLoadF4SELogs = true,
            SkipXSECopy = false,
            
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