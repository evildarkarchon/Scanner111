namespace Scanner111.Core.Models.Yaml;

public class GameInfo
{
    public string MainRootName { get; set; } = string.Empty;
    public string MainDocsName { get; set; } = string.Empty;
    public int MainSteamId { get; set; }
    public string ExeHashedOld { get; set; } = string.Empty;
    public string ExeHashedNew { get; set; } = string.Empty;
    public string GameVersion { get; set; } = string.Empty;
    public string GameVersionNew { get; set; } = string.Empty;

    public string CrashgenAcronym { get; set; } = string.Empty;
    public string CrashgenLogName { get; set; } = string.Empty;
    public string CrashgenDllFile { get; set; } = string.Empty;
    public string CrashgenLatestVer { get; set; } = string.Empty;
    public List<string> CrashgenIgnore { get; set; } = new();

    public string XseAcronym { get; set; } = string.Empty;
    public string XseFullName { get; set; } = string.Empty;
    public string XseVerLatest { get; set; } = string.Empty;
    public string XseVerLatestNg { get; set; } = string.Empty;
    public int XseFileCount { get; set; }

    public string RootFolderGame { get; set; } = string.Empty;
}