using System;
using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace Scanner111.GUI.Models;

public class UserSettings
{
    [JsonPropertyName("defaultLogPath")]
    public string DefaultLogPath { get; set; } = "";

    [JsonPropertyName("defaultGamePath")]
    public string DefaultGamePath { get; set; } = "";

    [JsonPropertyName("defaultScanDirectory")]
    public string DefaultScanDirectory { get; set; } = "";

    [JsonPropertyName("autoLoadF4SELogs")]
    public bool AutoLoadF4SELogs { get; set; } = true;

    [JsonPropertyName("maxLogMessages")]
    public int MaxLogMessages { get; set; } = 100;

    [JsonPropertyName("enableProgressNotifications")]
    public bool EnableProgressNotifications { get; set; } = true;

    [JsonPropertyName("rememberWindowSize")]
    public bool RememberWindowSize { get; set; } = true;

    [JsonPropertyName("windowWidth")]
    public double WindowWidth { get; set; } = 1200;

    [JsonPropertyName("windowHeight")]
    public double WindowHeight { get; set; } = 800;

    [JsonPropertyName("enableDebugLogging")]
    public bool EnableDebugLogging { get; set; } = false;

    [JsonPropertyName("recentLogFiles")]
    public List<string> RecentLogFiles { get; set; } = new();

    [JsonPropertyName("recentGamePaths")]
    public List<string> RecentGamePaths { get; set; } = new();

    [JsonPropertyName("recentScanDirectories")]
    public List<string> RecentScanDirectories { get; set; } = new();

    [JsonPropertyName("maxRecentItems")]
    public int MaxRecentItems { get; set; } = 10;

    [JsonPropertyName("lastUsedAnalyzers")]
    public List<string> LastUsedAnalyzers { get; set; } = new();

    [JsonPropertyName("autoSaveResults")]
    public bool AutoSaveResults { get; set; } = false;

    [JsonPropertyName("defaultOutputFormat")]
    public string DefaultOutputFormat { get; set; } = "text";

    public void AddRecentLogFile(string path)
    {
        if (string.IsNullOrEmpty(path)) return;
        
        RecentLogFiles.Remove(path);
        RecentLogFiles.Insert(0, path);
        
        while (RecentLogFiles.Count > MaxRecentItems)
        {
            RecentLogFiles.RemoveAt(RecentLogFiles.Count - 1);
        }
    }

    public void AddRecentGamePath(string path)
    {
        if (string.IsNullOrEmpty(path)) return;
        
        RecentGamePaths.Remove(path);
        RecentGamePaths.Insert(0, path);
        
        while (RecentGamePaths.Count > MaxRecentItems)
        {
            RecentGamePaths.RemoveAt(RecentGamePaths.Count - 1);
        }
    }

    public void AddRecentScanDirectory(string path)
    {
        if (string.IsNullOrEmpty(path)) return;
        
        RecentScanDirectories.Remove(path);
        RecentScanDirectories.Insert(0, path);
        
        while (RecentScanDirectories.Count > MaxRecentItems)
        {
            RecentScanDirectories.RemoveAt(RecentScanDirectories.Count - 1);
        }
    }
}