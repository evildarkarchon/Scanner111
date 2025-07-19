using System;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using Scanner111.CLI.Models;
using Scanner111.Core.Infrastructure;

namespace Scanner111.CLI.Services;

/// <summary>
/// Service for managing CLI settings persistence
/// </summary>
public interface ICliSettingsService
{
    Task<CliSettings> LoadSettingsAsync();
    Task SaveSettingsAsync(CliSettings settings);
    Task SaveSettingAsync(string key, object value);
    CliSettings GetDefaultSettings();
}

public class CliSettingsService : ICliSettingsService
{
    private static readonly string SettingsFilePath = Path.Combine(SettingsHelper.GetSettingsDirectory(), "cli-settings.json");
    
    private CliSettings? _cachedSettings;
    
    public async Task<CliSettings> LoadSettingsAsync()
    {
        var settings = await SettingsHelper.LoadSettingsAsync(SettingsFilePath, GetDefaultSettings);
        _cachedSettings = settings;
        return settings;
    }
    
    public async Task SaveSettingsAsync(CliSettings settings)
    {
        await SettingsHelper.SaveSettingsAsync(SettingsFilePath, settings);
        _cachedSettings = settings;
    }
    
    public async Task SaveSettingAsync(string key, object value)
    {
        var settings = _cachedSettings ?? await LoadSettingsAsync();
        
        // Update the specific setting using reflection
        var property = typeof(CliSettings).GetProperty(key);
        if (property == null)
        {
            // Try with different casing conventions
            property = typeof(CliSettings).GetProperty(SettingsHelper.ToPascalCase(key));
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
    
    public CliSettings GetDefaultSettings()
    {
        return new CliSettings
        {
            FcxMode = false,
            ShowFormIdValues = false,
            SimplifyLogs = false,
            MoveUnsolvedLogs = false,
            AudioNotifications = false,
            VrMode = false,
            DefaultScanDirectory = "",
            DefaultGamePath = GamePathDetection.TryDetectGamePath(),
            DefaultOutputFormat = "detailed",
            DisableColors = false,
            DisableProgress = false,
            VerboseLogging = false,
            MaxConcurrentScans = Environment.ProcessorCount * 2,
            CacheEnabled = true
        };
    }
    
}