namespace Scanner111.Core.Models.Yaml;

public class ClassicMainYaml
{
    public ClassicInfo ClassicInfo { get; set; } = new();
    public List<string> ClassicAutoBackup { get; set; } = new();
    public ClassicInterface ClassicInterface { get; set; } = new();
    public WarningsGameConfig WarningsGame { get; set; } = new();
    public WarningsWryeConfig WarningsWrye { get; set; } = new();
    public ModsWarnConfig ModsWarn { get; set; } = new();
    public List<string> CatchLogErrors { get; set; } = new();
    public List<string> CatchLogRecords { get; set; } = new();
    public List<string> ExcludeLogRecords { get; set; } = new();
    public List<string> ExcludeLogErrors { get; set; } = new();
    public List<string> ExcludeLogFiles { get; set; } = new();
}

public class ClassicInterface
{
    public string StartMessage { get; set; } = string.Empty;
    public string HelpPopupMain { get; set; } = string.Empty;
    public string HelpPopupBackup { get; set; } = string.Empty;
    public string UpdatePopupText { get; set; } = string.Empty;
    public string UpdateWarningFallout4 { get; set; } = string.Empty;
    public string UpdateUnableFallout4 { get; set; } = string.Empty;
    public string AutoscanTextFallout4 { get; set; } = string.Empty;
}