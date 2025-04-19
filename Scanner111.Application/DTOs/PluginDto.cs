namespace Scanner111.Application.DTOs;

public class PluginDto
{
    public string Name { get; set; } = string.Empty;
    public string FileName { get; set; } = string.Empty;
    public string LoadOrderId { get; set; } = string.Empty;
    public string Type { get; set; } = string.Empty;
    public bool IsEnabled { get; set; }
    public bool IsOfficial { get; set; }
    public bool HasIssues { get; set; }
    public int IssueCount { get; set; }
}