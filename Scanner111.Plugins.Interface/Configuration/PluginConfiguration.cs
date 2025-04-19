namespace Scanner111.Plugins.Interface.Configuration;

public class PluginConfiguration
{
    public string Name { get; set; } = string.Empty;
    public bool IsEnabled { get; set; } = true;
    public Dictionary<string, string> Settings { get; set; } = new();
}