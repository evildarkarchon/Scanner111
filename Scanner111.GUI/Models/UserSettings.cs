using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace Scanner111.GUI.Models;

/// <summary>
/// Represents a set of configurable user settings for the application.
/// </summary>
/// <remarks>
/// This class is designed to store and manage user preferences related to the
/// application's behavior and appearance. The settings include paths, UI
/// preferences, logging configurations, and other relevant options. Most
/// properties are serialized and deserialized using JSON for ease of
/// persistence.
/// </remarks>
/// <example>
/// Use this class to load, modify, and save user-specific settings in the
/// application. It provides various configurable properties such as file
/// paths, window dimensions, and options for enabling or disabling specific
/// features.
/// </example>
/// <remarks>
/// The settings can be saved or loaded using a suitable persistence mechanism,
/// such as a settings service, to enable customization and state management.
/// Specific helper methods are provided to ease the management of the
/// "recent items" lists.
/// </remarks>
public class UserSettings
{
    [JsonPropertyName("defaultLogPath")] public string DefaultLogPath { get; set; } = "";

    [JsonPropertyName("defaultGamePath")] public string DefaultGamePath { get; set; } = "";

    [JsonPropertyName("defaultScanDirectory")]
    public string DefaultScanDirectory { get; set; } = "";

    [JsonPropertyName("autoLoadF4SELogs")] public bool AutoLoadF4SeLogs { get; set; } = true;

    [JsonPropertyName("maxLogMessages")] public int MaxLogMessages { get; set; } = 100;

    [JsonPropertyName("enableProgressNotifications")]
    public bool EnableProgressNotifications { get; set; } = true;

    [JsonPropertyName("rememberWindowSize")]
    public bool RememberWindowSize { get; set; } = true;

    [JsonPropertyName("windowWidth")] public double WindowWidth { get; set; } = 1200;

    [JsonPropertyName("windowHeight")] public double WindowHeight { get; set; } = 800;

    [JsonPropertyName("enableDebugLogging")]
    public bool EnableDebugLogging { get; set; } = false;

    [JsonPropertyName("recentLogFiles")] public List<string> RecentLogFiles { get; set; } = new();

    [JsonPropertyName("recentGamePaths")] public List<string> RecentGamePaths { get; set; } = new();

    [JsonPropertyName("recentScanDirectories")]
    public List<string> RecentScanDirectories { get; set; } = new();

    [JsonPropertyName("maxRecentItems")] public int MaxRecentItems { get; set; } = 10;

    [JsonPropertyName("lastUsedAnalyzers")]
    public List<string> LastUsedAnalyzers { get; set; } = new();

    [JsonPropertyName("autoSaveResults")] public bool AutoSaveResults { get; set; } = true;

    [JsonPropertyName("defaultOutputFormat")]
    public string DefaultOutputFormat { get; set; } = "text"; // Hardcoded to text - JSON/XML formats not implemented

    [JsonPropertyName("crashLogsDirectory")]
    public string CrashLogsDirectory { get; set; } = "";

    [JsonPropertyName("skipXSECopy")] public bool SkipXseCopy { get; set; } = false;

    [JsonPropertyName("enableUpdateCheck")] public bool EnableUpdateCheck { get; set; } = true;

    [JsonPropertyName("updateSource")] public string UpdateSource { get; set; } = "Both"; // "Both", "GitHub", "Nexus"

    [JsonPropertyName("fcxMode")] public bool FcxMode { get; set; } = false;

    [JsonPropertyName("moveUnsolvedLogs")] public bool MoveUnsolvedLogs { get; set; } = false;

    [JsonPropertyName("modsFolder")] public string ModsFolder { get; set; } = "";

    [JsonPropertyName("iniFolder")] public string IniFolder { get; set; } = "";

    [JsonPropertyName("backupDirectory")] public string BackupDirectory { get; set; } = "";

    /// Adds a specified log file path to the list of recent log files.
    /// Maintains the maximum number of recent items as defined by MaxRecentItems.
    /// If the path already exists in the list, it is moved to the most recent position.
    /// <param name="path">The file path of the log to be added to the recent list.</param>
    public void AddRecentLogFile(string path)
    {
        if (string.IsNullOrEmpty(path)) return;

        RecentLogFiles.Remove(path);
        RecentLogFiles.Insert(0, path);

        while (RecentLogFiles.Count > MaxRecentItems) RecentLogFiles.RemoveAt(RecentLogFiles.Count - 1);
    }

    /// Adds a specified game path to the list of recent game paths.
    /// Maintains the maximum number of recent items as defined by MaxRecentItems.
    /// If the path already exists in the list, it is moved to the most recent position.
    /// <param name="path">The file path of the game to be added to the recent list.</param>
    public void AddRecentGamePath(string path)
    {
        if (string.IsNullOrEmpty(path)) return;

        RecentGamePaths.Remove(path);
        RecentGamePaths.Insert(0, path);

        while (RecentGamePaths.Count > MaxRecentItems) RecentGamePaths.RemoveAt(RecentGamePaths.Count - 1);
    }

    /// Adds a specified directory path to the list of recent scan directories.
    /// Ensures the list does not exceed the maximum allowed recent items.
    /// If the directory path already exists in the list, it is moved to the most recent position.
    /// <param name="path">The directory path to be added to the recent scan directories list.</param>
    public void AddRecentScanDirectory(string path)
    {
        if (string.IsNullOrEmpty(path)) return;

        RecentScanDirectories.Remove(path);
        RecentScanDirectories.Insert(0, path);

        while (RecentScanDirectories.Count > MaxRecentItems)
            RecentScanDirectories.RemoveAt(RecentScanDirectories.Count - 1);
    }
}