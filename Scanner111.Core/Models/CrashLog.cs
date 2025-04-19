namespace Scanner111.Core.Models;

public class CrashLog
{
    public string Id { get; set; } = string.Empty;
    public string FileName { get; set; } = string.Empty;
    public string FilePath { get; set; } = string.Empty;
    public DateTime CrashTime { get; set; }
    public string GameId { get; set; } = string.Empty;
    public string GameVersion { get; set; } = string.Empty;
    public string CrashGenVersion { get; set; } = string.Empty;
    public string MainError { get; set; } = string.Empty;
    
    // Navigation properties
    public ICollection<CrashLogPlugin> Plugins { get; set; } = new List<CrashLogPlugin>();
    public ICollection<CrashLogCallStack> CallStackEntries { get; set; } = new List<CrashLogCallStack>();
    public ICollection<CrashLogIssue> DetectedIssues { get; set; } = new List<CrashLogIssue>();
    
    public bool IsAnalyzed { get; set; }
    public bool IsSolved { get; set; }
}