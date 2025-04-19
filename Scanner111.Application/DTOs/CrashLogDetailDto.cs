namespace Scanner111.Application.DTOs;

public class CrashLogDetailDto : CrashLogDto
{
    public string GameVersion { get; set; } = string.Empty;
    public string CrashGenVersion { get; set; } = string.Empty;
    public List<PluginDto> LoadedPlugins { get; set; } = new();
    public List<string> CallStack { get; set; } = new();
    public List<ModIssueDto> DetectedIssues { get; set; } = new();
    public string RawContent { get; set; } = string.Empty;
}