namespace Scanner111.Core.Models;

public class CrashLogPlugin
{
    public string Id { get; set; } = string.Empty;
    public string CrashLogId { get; set; } = string.Empty;
    public string PluginName { get; set; } = string.Empty;
    public string LoadOrderId { get; set; } = string.Empty;
    
    // Navigation property
    public CrashLog CrashLog { get; set; } = null!;
}