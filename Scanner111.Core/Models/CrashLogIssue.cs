namespace Scanner111.Core.Models;

public class CrashLogIssue
{
    public string Id { get; set; } = string.Empty;
    public string CrashLogId { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    
    // Navigation property
    public CrashLog CrashLog { get; set; } = null!;
}