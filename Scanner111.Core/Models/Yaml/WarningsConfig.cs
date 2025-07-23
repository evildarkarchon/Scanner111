namespace Scanner111.Core.Models.Yaml;

public class WarningsGameConfig
{
    public string WarnRootPath { get; set; } = string.Empty;
    public string WarnDocsPath { get; set; } = string.Empty;
}

public class WarningsWryeConfig
{
    public string Corrupted { get; set; } = string.Empty;
    public string IncorrectEslFlag { get; set; } = string.Empty;
    public string MissingMasters { get; set; } = string.Empty;
    public string DelinquentMasters { get; set; } = string.Empty;
    public string OldHeaderFormVersions { get; set; } = string.Empty;
    public string DeletedNavmeshes { get; set; } = string.Empty;
    public string DeletedBaseRecords { get; set; } = string.Empty;
    public string HitMes { get; set; } = string.Empty;
    public string DuplicateFormIds { get; set; } = string.Empty;
    public string RecordTypeCollisions { get; set; } = string.Empty;
    public string ProbableInjectedCollisions { get; set; } = string.Empty;
    public string Invalid { get; set; } = string.Empty;
    public string CleaningWith { get; set; } = string.Empty;
}

public class WarningsXseConfig
{
    public string WarnOutdated { get; set; } = string.Empty;
    public string WarnMissing { get; set; } = string.Empty;
    public string WarnMismatch { get; set; } = string.Empty;
}

public class WarningsModsConfig
{
    public string WarnAdlibMissing { get; set; } = string.Empty;
    public string WarnModXsePreloader { get; set; } = string.Empty;
    public string WarnWryeMissingHtml { get; set; } = string.Empty;
}

public class WarningsCrashgenConfig
{
    public string WarnTomlAchievements { get; set; } = string.Empty;
    public string WarnTomlMemory { get; set; } = string.Empty;
    public string WarnTomlF4ee { get; set; } = string.Empty;
    public string WarnOutdated { get; set; } = string.Empty;
    public string WarnMissing { get; set; } = string.Empty;
    public string WarnNoPlugins { get; set; } = string.Empty;
}

public class ModsWarnConfig
{
    public string ModsReminders { get; set; } = string.Empty;
    public string ModsPathInvalid { get; set; } = string.Empty;
    public string ModsPathMissing { get; set; } = string.Empty;
    public string ModsBsArchMissing { get; set; } = string.Empty;
    public string ModsPluginLimit { get; set; } = string.Empty;
}