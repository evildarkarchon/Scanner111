using System.Diagnostics.CodeAnalysis;
using System.Text.Json.Serialization;

namespace Scanner111.CLI.Models;

/// <summary>
///     CLI-specific settings that persist between runs
/// </summary>
[SuppressMessage("ReSharper", "RedundantDefaultMemberInitializer")]
public class CliSettings
{
    [JsonPropertyName("fcxMode")] public bool FcxMode { get; set; } = false;

    [JsonPropertyName("showFormIdValues")] public bool ShowFormIdValues { get; set; } = false;

    [JsonPropertyName("simplifyLogs")] public bool SimplifyLogs { get; set; } = false;

    [JsonPropertyName("moveUnsolvedLogs")] public bool MoveUnsolvedLogs { get; set; } = false;

    [JsonPropertyName("audioNotifications")]
    public bool AudioNotifications { get; set; } = false;

    [JsonPropertyName("vrMode")] public bool VrMode { get; set; } = false;

    [JsonPropertyName("defaultScanDirectory")]
    public string DefaultScanDirectory { get; set; } = "";

    [JsonPropertyName("defaultGamePath")] public string DefaultGamePath { get; set; } = "";

    [JsonPropertyName("defaultOutputFormat")]
    public string DefaultOutputFormat { get; set; } = "detailed";

    [JsonPropertyName("disableColors")] public bool DisableColors { get; set; } = false;

    [JsonPropertyName("disableProgress")] public bool DisableProgress { get; set; } = false;

    [JsonPropertyName("verboseLogging")] public bool VerboseLogging { get; set; } = false;

    [JsonPropertyName("maxConcurrentScans")]
    public int MaxConcurrentScans { get; set; } = 16;

    [JsonPropertyName("cacheEnabled")] public bool CacheEnabled { get; set; } = true;

    [JsonPropertyName("recentScanPaths")] public List<string> RecentScanPaths { get; set; } = new();

    [JsonPropertyName("maxRecentPaths")] public int MaxRecentPaths { get; set; } = 10;

    [JsonPropertyName("crashLogsDirectory")]
    public string CrashLogsDirectory { get; set; } = "";

    [JsonPropertyName("gamePath")] public string GamePath { get; set; } = "";

    [JsonPropertyName("modsFolder")] public string ModsFolder { get; set; } = "";

    [JsonPropertyName("iniFolder")] public string IniFolder { get; set; } = "";

    /// <summary>
    ///     Add a path to recent scan paths, maintaining the max limit
    /// </summary>
    public void AddRecentPath(string path)
    {
        if (string.IsNullOrWhiteSpace(path)) return;

        RecentScanPaths.Remove(path);
        RecentScanPaths.Insert(0, path);

        while (RecentScanPaths.Count > MaxRecentPaths) RecentScanPaths.RemoveAt(RecentScanPaths.Count - 1);
    }
}