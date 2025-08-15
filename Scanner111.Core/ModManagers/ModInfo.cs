namespace Scanner111.Core.ModManagers;

public class ModInfo
{
    public string Id { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string? Version { get; set; }
    public string? Author { get; set; }
    public string? Description { get; set; }
    public string FolderPath { get; set; } = string.Empty;
    public bool IsEnabled { get; set; }
    public int LoadOrder { get; set; }
    public DateTime? InstallDate { get; set; }
    public List<string> Files { get; set; } = new();
    public List<string> ConflictingMods { get; set; } = new();
    public Dictionary<string, object> Metadata { get; set; } = new();
}