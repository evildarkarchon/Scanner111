using System.Text.Json.Serialization;

namespace Scanner111.Core.Models;

/// <summary>
///     Unified application settings shared between GUI and CLI
/// </summary>
public class ApplicationSettings
{
    private readonly object _recentItemsLock = new();
    // === Core Analysis Settings ===

    [JsonPropertyName("fcxMode")] public bool FcxMode { get; set; }

    [JsonPropertyName("showFormIdValues")] public bool ShowFormIdValues { get; set; }

    [JsonPropertyName("simplifyLogs")] public bool SimplifyLogs { get; set; }

    [JsonPropertyName("moveUnsolvedLogs")] public bool MoveUnsolvedLogs { get; set; }

    [JsonPropertyName("vrMode")] public bool VrMode { get; set; }

    // === Path Settings ===

    [JsonPropertyName("defaultLogPath")] public string DefaultLogPath { get; set; } = "";

    [JsonPropertyName("defaultGamePath")] public string DefaultGamePath { get; set; } = "";

    [JsonPropertyName("gamePath")] public string GamePath { get; set; } = "";

    [JsonPropertyName("defaultScanDirectory")]
    public string DefaultScanDirectory { get; set; } = "";

    [JsonPropertyName("crashLogsDirectory")]
    public string CrashLogsDirectory { get; set; } = "";

    [JsonPropertyName("backupDirectory")]
    public string BackupDirectory { get; set; } = "";

    [JsonPropertyName("modsFolder")]
    public string ModsFolder { get; set; } = "";

    [JsonPropertyName("iniFolder")]
    public string IniFolder { get; set; } = "";

    // === Output Settings ===

    [JsonPropertyName("defaultOutputFormat")]
    public string DefaultOutputFormat { get; set; } = "text"; // Hardcoded to text - JSON/XML formats not implemented

    [JsonPropertyName("autoSaveResults")] public bool AutoSaveResults { get; set; } = true;

    // === XSE Settings ===

    [JsonPropertyName("autoLoadF4SELogs")] public bool AutoLoadF4SeLogs { get; set; } = true;

    [JsonPropertyName("skipXSECopy")] public bool SkipXseCopy { get; set; }

    // === Performance Settings ===

    [JsonPropertyName("maxConcurrentScans")]
    public int MaxConcurrentScans { get; set; } = 16;

    [JsonPropertyName("cacheEnabled")] public bool CacheEnabled { get; set; } = true;

    // === Debug/Logging Settings ===

    [JsonPropertyName("enableDebugLogging")]
    public bool EnableDebugLogging { get; set; }

    [JsonPropertyName("verboseLogging")] public bool VerboseLogging { get; set; }

    // === Notification Settings ===

    [JsonPropertyName("audioNotifications")]
    public bool AudioNotifications { get; set; }

    [JsonPropertyName("enableProgressNotifications")]
    public bool EnableProgressNotifications { get; set; } = true;

    // === Update Check Settings ===

    [JsonPropertyName("enableUpdateCheck")]
    public bool EnableUpdateCheck { get; set; } = true;

    [JsonPropertyName("updateSource")]
    public string UpdateSource { get; set; } = "Both"; // "Both", "GitHub", "Nexus"

    // === CLI-Specific Display Settings ===

    [JsonPropertyName("disableColors")] public bool DisableColors { get; set; }

    [JsonPropertyName("disableProgress")] public bool DisableProgress { get; set; }

    // === GUI-Specific Settings ===

    [JsonPropertyName("rememberWindowSize")]
    public bool RememberWindowSize { get; set; } = true;

    [JsonPropertyName("windowWidth")] public double WindowWidth { get; set; } = 1200;

    [JsonPropertyName("windowHeight")] public double WindowHeight { get; set; } = 800;

    [JsonPropertyName("maxLogMessages")] public int MaxLogMessages { get; set; } = 100;

    // === Recent Items Management ===

    [JsonPropertyName("recentLogFiles")] public List<string> RecentLogFiles { get; set; } = new();

    [JsonPropertyName("recentGamePaths")] public List<string> RecentGamePaths { get; set; } = new();

    [JsonPropertyName("recentScanDirectories")]
    public List<string> RecentScanDirectories { get; set; } = new();

    [JsonPropertyName("maxRecentItems")] public int MaxRecentItems { get; set; } = 10;

    [JsonPropertyName("lastUsedAnalyzers")]
    public List<string> LastUsedAnalyzers { get; set; } = new();

    /// <summary>
    ///     Compatibility property for CLI that uses a single recent paths list
    /// </summary>
    [JsonIgnore]
    public List<string> RecentScanPaths => RecentScanDirectories;

    // === Recent Items Management Methods ===

    public void AddRecentLogFile(string path)
    {
        if (string.IsNullOrWhiteSpace(path)) return;

        lock (_recentItemsLock)
        {
            RecentLogFiles.Remove(path);
            RecentLogFiles.Insert(0, path);

            while (RecentLogFiles.Count > MaxRecentItems) RecentLogFiles.RemoveAt(RecentLogFiles.Count - 1);
        }
    }

    public void AddRecentGamePath(string path)
    {
        if (string.IsNullOrWhiteSpace(path)) return;

        lock (_recentItemsLock)
        {
            RecentGamePaths.Remove(path);
            RecentGamePaths.Insert(0, path);

            while (RecentGamePaths.Count > MaxRecentItems) RecentGamePaths.RemoveAt(RecentGamePaths.Count - 1);
        }
    }

    public void AddRecentScanDirectory(string path)
    {
        if (string.IsNullOrWhiteSpace(path)) return;

        lock (_recentItemsLock)
        {
            RecentScanDirectories.Remove(path);
            RecentScanDirectories.Insert(0, path);

            while (RecentScanDirectories.Count > MaxRecentItems)
                RecentScanDirectories.RemoveAt(RecentScanDirectories.Count - 1);
        }
    }

    /// <summary>
    ///     Compatibility method for CLI that uses a single recent paths list
    /// </summary>
    public void AddRecentPath(string path)
    {
        AddRecentScanDirectory(path);
    }
}