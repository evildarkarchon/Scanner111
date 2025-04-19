namespace Scanner111.Plugins.Interface.Models;

public class CrashLogInfo
{
    public string Id { get; set; } = string.Empty;
    public string FileName { get; set; } = string.Empty;
    public string FilePath { get; set; } = string.Empty;
    public DateTime CrashTime { get; set; }
    public string GameId { get; set; } = string.Empty;
    public string GameVersion { get; set; } = string.Empty;
    public string CrashGenVersion { get; set; } = string.Empty;
    public string MainError { get; set; } = string.Empty;
    public Dictionary<string, string> LoadedPlugins { get; set; } = new();
    public List<string> CallStack { get; set; } = new();
    public List<string> DetectedIssues { get; set; } = new();
    public bool IsAnalyzed { get; set; }
    public bool IsSolved { get; set; }
}