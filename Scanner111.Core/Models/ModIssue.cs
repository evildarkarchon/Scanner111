namespace Scanner111.Core.Models;

public class ModIssue
{
    public string Id { get; set; } = string.Empty;
    public string PluginName { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public int Severity { get; set; }
    public ModIssueType IssueType { get; set; }
    public string Solution { get; set; } = string.Empty;
    public List<string> PatchLinks { get; set; } = [];
}