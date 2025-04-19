namespace Scanner111.Plugins.Interface.Models;

public class GameInfo
{
    public string Id { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string[] ExecutableNames { get; set; } = Array.Empty<string>();
    public string Version { get; set; } = string.Empty;
    public string InstallPath { get; set; } = string.Empty;
    public string DocumentsPath { get; set; } = string.Empty;
    public bool IsInstalled { get; set; }
    public bool IsSupported { get; set; }
}