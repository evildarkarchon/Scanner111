namespace Scanner111.Application.DTOs;

public class CrashLogDetailDto : CrashLogDto
{
    public string GameVersion { get; set; } = string.Empty;
    public string CrashGenVersion { get; set; } = string.Empty;
    public List<PluginDto> LoadedPlugins { get; set; } = [];
    public List<string> CallStack { get; set; } = [];
    public List<ModIssueDto> DetectedIssues { get; set; } = [];
    public string RawContent { get; set; } = string.Empty;
}