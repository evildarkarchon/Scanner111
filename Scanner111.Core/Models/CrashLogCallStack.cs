namespace Scanner111.Core.Models;

public class CrashLogCallStack
{
    public string Id { get; set; } = string.Empty;
    public string CrashLogId { get; set; } = string.Empty;
    public int Order { get; set; }
    public string Entry { get; set; } = string.Empty;
    
    // Navigation property
    public CrashLog CrashLog { get; set; } = null!;
}
