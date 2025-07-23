namespace Scanner111.Core.Models.Yaml;

public class ClassicInfo
{
    public string Version { get; set; } = string.Empty;
    public string VersionDate { get; set; } = string.Empty;
    public bool IsPrerelease { get; set; }
    public string DefaultSettings { get; set; } = string.Empty;
    public string DefaultLocalYaml { get; set; } = string.Empty;
    public string DefaultIgnorefile { get; set; } = string.Empty;
}