namespace Scanner111.Application.DTOs;

public class ModIssueDto
{
    public string Id { get; set; } = string.Empty;
    public string PluginName { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public int Severity { get; set; }
    public string IssueType { get; set; } = string.Empty;
    public string Solution { get; set; } = string.Empty;
    public List<string> PatchLinks { get; set; } = new();
}