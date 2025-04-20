namespace Scanner111.Core.Models;

public class Plugin
{
    public string Name { get; set; } = string.Empty;
    public string FileName { get; set; } = string.Empty;
    public string FilePath { get; set; } = string.Empty;
    public string LoadOrderId { get; set; } = string.Empty;
    public PluginType Type { get; set; } = PluginType.Esp;
    public bool IsEnabled { get; set; }
    public bool IsOfficial { get; set; }
    public bool IsMaster { get; set; }
    public bool HasIssues { get; set; }
    public List<string> RequiredPlugins { get; set; } = [];
}