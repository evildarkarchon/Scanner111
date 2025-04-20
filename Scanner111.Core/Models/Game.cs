namespace Scanner111.Core.Models;

public class Game
{
    public string Id { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string ExecutableName { get; set; } = string.Empty;
    public string DocumentsPath { get; set; } = string.Empty;
    public string InstallPath { get; set; } = string.Empty;
    public string Version { get; set; } = string.Empty;
    public List<string> SupportedScriptExtenders { get; set; } = [];
}