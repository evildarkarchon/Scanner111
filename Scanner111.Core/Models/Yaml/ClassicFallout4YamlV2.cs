namespace Scanner111.Core.Models.Yaml;

/// <summary>
///     Represents the restructured CLASSIC Fallout4.yaml without VR support
/// </summary>
public class ClassicFallout4YamlV2
{
    public GameInfoV2 GameInfo { get; set; } = new();

    // Backup configurations
    public List<string> BackupEnb { get; set; } = new();
    public List<string> BackupReshade { get; set; } = new();
    public List<string> BackupVulkan { get; set; } = new();
    public List<string> BackupXse { get; set; } = new();

    public List<string> GameHints { get; set; } = new();
    public string DefaultCustomIni { get; set; } = string.Empty;
    public string DefaultFidMods { get; set; } = string.Empty;

    // Warning configurations
    public WarningsCrashgenConfig WarningsCrashgen { get; set; } = new();
    public WarningsXseConfig WarningsXse { get; set; } = new();
    public WarningsModsConfig WarningsMods { get; set; } = new();

    // Crash log configurations
    public List<string> CrashlogRecordsExclude { get; set; } = new();
    public List<string> CrashlogPluginsExclude { get; set; } = new();
    public Dictionary<string, string> CrashlogErrorCheck { get; set; } = new();
    public Dictionary<string, List<string>> CrashlogStackCheck { get; set; } = new();

    // Mod configurations
    public Dictionary<string, string> ModsCore { get; set; } = new();
    public Dictionary<string, string> ModsCoreFollon { get; set; } = new();
    public Dictionary<string, string> ModsFreq { get; set; } = new();
    public Dictionary<string, string> ModsConf { get; set; } = new();
    public Dictionary<string, string> ModsSolu { get; set; } = new();
    public Dictionary<string, string> ModsOpc2 { get; set; } = new();
}