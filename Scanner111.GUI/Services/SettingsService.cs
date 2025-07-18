using System;
using System.IO;
using System.Text.Json;
using System.Threading.Tasks;
using Scanner111.GUI.Models;
using Microsoft.Win32;
using System.Runtime.InteropServices;

namespace Scanner111.GUI.Services;

public interface ISettingsService
{
    Task<UserSettings> LoadSettingsAsync();
    Task SaveSettingsAsync(UserSettings settings);
    UserSettings GetDefaultSettings();
}

public class SettingsService : ISettingsService
{
    private static readonly string SettingsDirectory = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
        "Scanner111");

    private static readonly string SettingsFilePath = Path.Combine(SettingsDirectory, "settings.json");

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    public async Task<UserSettings> LoadSettingsAsync()
    {
        try
        {
            if (!File.Exists(SettingsFilePath))
            {
                var defaultSettings = GetDefaultSettings();
                await SaveSettingsAsync(defaultSettings);
                return defaultSettings;
            }

            var json = await File.ReadAllTextAsync(SettingsFilePath);
            var settings = JsonSerializer.Deserialize<UserSettings>(json, JsonOptions);
            
            return settings ?? GetDefaultSettings();
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error loading settings: {ex.Message}");
            return GetDefaultSettings();
        }
    }

    public async Task SaveSettingsAsync(UserSettings settings)
    {
        try
        {
            Directory.CreateDirectory(SettingsDirectory);
            
            var json = JsonSerializer.Serialize(settings, JsonOptions);
            await File.WriteAllTextAsync(SettingsFilePath, json);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error saving settings: {ex.Message}");
            throw;
        }
    }

    public UserSettings GetDefaultSettings()
    {
        return new UserSettings
        {
            DefaultLogPath = "",
            DefaultGamePath = GetDefaultGamePath(),
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

    private static string GetDefaultGamePath()
    {
        // Try registry detection first (matching Python implementation)
        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            var registryPath = TryGetGamePathFromRegistry();
            if (!string.IsNullOrEmpty(registryPath))
                return registryPath;
        }

        // Try XSE log file parsing (matching Python implementation)
        var xseLogPath = TryGetGamePathFromXSELog();
        if (!string.IsNullOrEmpty(xseLogPath))
            return xseLogPath;

        // Check common installation paths as fallback
        var commonPaths = new[]
        {
            @"C:\Program Files (x86)\Steam\steamapps\common\Fallout 4",
            @"C:\Program Files\Steam\steamapps\common\Fallout 4",
            @"C:\Steam\steamapps\common\Fallout 4",
            @"D:\Steam\steamapps\common\Fallout 4",
            @"C:\GOG Games\Fallout 4",
            @"C:\Games\Fallout 4",
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), "Bethesda.net Launcher", "games", "Fallout4")
        };

        foreach (var path in commonPaths)
        {
            if (ValidateGamePath(path))
                return path;
        }

        return "";
    }

    private static string TryGetGamePathFromRegistry()
    {
        if (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            return "";

        try
        {
            // Try Bethesda Softworks registry key first
            using var regKey = Registry.LocalMachine.OpenSubKey(@"SOFTWARE\WOW6432Node\Bethesda Softworks\Fallout4");
            if (regKey?.GetValue("installed path") is string bethesdaPath && ValidateGamePath(bethesdaPath))
            {
                return bethesdaPath;
            }

            // Try Fallout4VR if regular Fallout4 not found
            using var regKeyVR = Registry.LocalMachine.OpenSubKey(@"SOFTWARE\WOW6432Node\Bethesda Softworks\Fallout4VR");
            if (regKeyVR?.GetValue("installed path") is string bethesdaVRPath && ValidateGamePath(bethesdaVRPath))
            {
                return bethesdaVRPath;
            }

            // Try GOG registry key
            using var regKeyGog = Registry.LocalMachine.OpenSubKey(@"SOFTWARE\WOW6432Node\GOG.com\Games\1998527297");
            if (regKeyGog?.GetValue("path") is string gogPath && ValidateGamePath(gogPath))
            {
                return gogPath;
            }
        }
        catch
        {
            // Registry access failed, continue to other methods
        }

        return "";
    }

    private static string TryGetGamePathFromXSELog()
    {
        try
        {
            // Check for F4SE log files in Documents (matching Python implementation)
            var documentsPath = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);
            var f4sePaths = new[]
            {
                Path.Combine(documentsPath, "My Games", "Fallout4", "F4SE", "f4se.log"),
                Path.Combine(documentsPath, "My Games", "Fallout4VR", "F4SE", "f4se.log")
            };

            foreach (var logPath in f4sePaths)
            {
                if (File.Exists(logPath))
                {
                    var lines = File.ReadAllLines(logPath);
                    foreach (var line in lines)
                    {
                        if (line.StartsWith("plugin directory"))
                        {
                            // Extract path: "plugin directory = C:\Steam\steamapps\common\Fallout 4\Data\F4SE\Plugins"
                            var parts = line.Split('=', 2);
                            if (parts.Length == 2)
                            {
                                var fullPath = parts[1].Trim();
                                // Remove "\Data\F4SE\Plugins" suffix to get game root
                                var gamePath = fullPath.Replace(@"\Data\F4SE\Plugins", "").Replace(@"\Data\SKSE\Plugins", "");
                                if (ValidateGamePath(gamePath))
                                {
                                    return gamePath;
                                }
                            }
                        }
                    }
                }
            }
        }
        catch
        {
            // XSE log parsing failed, continue to other methods
        }

        return "";
    }

    private static bool ValidateGamePath(string path)
    {
        if (string.IsNullOrEmpty(path) || !Directory.Exists(path))
            return false;

        // Check for game executables
        var executables = new[] { "Fallout4.exe", "Fallout4VR.exe", "SkyrimSE.exe", "Skyrim.exe" };
        foreach (var exe in executables)
        {
            if (File.Exists(Path.Combine(path, exe)))
                return true;
        }

        return false;
    }
}