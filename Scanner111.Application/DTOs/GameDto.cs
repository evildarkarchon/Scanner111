namespace Scanner111.Application.DTOs;

public class GameDto
{
    public string Id { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string ExecutableName { get; set; } = string.Empty;
    public string InstallPath { get; set; } = string.Empty;
    public string DocumentsPath { get; set; } = string.Empty;
    public string Version { get; set; } = string.Empty;
    public bool IsInstalled { get; set; }
    public bool IsSupported { get; set; }
}