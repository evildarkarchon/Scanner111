namespace Scanner111.Application.DTOs;

public class CrashLogDto
{
    public string Id { get; set; } = string.Empty;
    public string FileName { get; set; } = string.Empty;
    public string FilePath { get; set; } = string.Empty;
    public DateTime CrashTime { get; set; }
    public string GameName { get; set; } = string.Empty;
    public string MainError { get; set; } = string.Empty;
    public bool IsAnalyzed { get; set; }
    public bool IsSolved { get; set; }
    public int IssueCount { get; set; }
    public int PluginCount { get; set; }
}