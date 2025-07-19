using System;
using System.IO;
using System.Text.Json;
using System.Threading.Tasks;
using Scanner111.GUI.Models;
using Scanner111.Core.Infrastructure;

namespace Scanner111.GUI.Services;

public interface ISettingsService
{
    Task<UserSettings> LoadSettingsAsync();
    Task SaveSettingsAsync(UserSettings settings);
    UserSettings GetDefaultSettings();
}

public class SettingsService : ISettingsService
{
    private static readonly string SettingsFilePath = Path.Combine(SettingsHelper.GetSettingsDirectory(), "settings.json");

    public async Task<UserSettings> LoadSettingsAsync()
    {
        return await SettingsHelper.LoadSettingsAsync(SettingsFilePath, GetDefaultSettings);
    }

    public async Task SaveSettingsAsync(UserSettings settings)
    {
        await SettingsHelper.SaveSettingsAsync(SettingsFilePath, settings);
    }

    public UserSettings GetDefaultSettings()
    {
        return new UserSettings
        {
            DefaultLogPath = "",
            DefaultGamePath = GamePathDetection.TryDetectGamePath(),
            DefaultScanDirectory = "",
            AutoLoadF4SELogs = true,
            MaxLogMessages = 100,
            EnableProgressNotifications = true,
            RememberWindowSize = true,
            WindowWidth = 1200,
            WindowHeight = 800,
            EnableDebugLogging = false,
            MaxRecentItems = 10,
            AutoSaveResults = false,
            DefaultOutputFormat = "text"
        };
    }

}