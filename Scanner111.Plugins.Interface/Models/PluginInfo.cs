namespace Scanner111.Plugins.Interface.Models;

public class PluginInfo
{
    public string Id { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string Version { get; set; } = string.Empty;
    public string[] SupportedGameIds { get; set; } = [];
    public string AssemblyPath { get; set; } = string.Empty;
    public string TypeName { get; set; } = string.Empty;
    public bool IsLoaded { get; set; }
    public bool IsEnabled { get; set; }
}