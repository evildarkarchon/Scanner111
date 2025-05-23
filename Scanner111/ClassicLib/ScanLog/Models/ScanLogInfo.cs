using System;
using System.Collections.Generic;
using Scanner111.ClassicLib.ScanLog.Services.Interfaces;
using Scanner111.Services;
using Scanner111.Services.Interfaces;

namespace Scanner111.ClassicLib.ScanLog.Models;

/// <summary>
///     Contains configuration and data needed for crash log scanning operations.
///     Equivalent to Python's ClassicScanLogsInfo class.
/// </summary>
public class ScanLogInfo
{
    public List<string> ClassicGameHints { get; init; } = [];
    public List<string> ClassicRecordsList { get; init; } = [];
    public string ClassicVersion { get; init; } = string.Empty;
    public string ClassicVersionDate { get; init; } = string.Empty;
    public string CrashgenName { get; init; } = string.Empty;
    public string CrashgenLatestOg { get; init; } = string.Empty;
    public string CrashgenLatestVr { get; init; } = string.Empty;
    public HashSet<string> CrashgenIgnore { get; init; } = [];
    public string WarnNoPlugins { get; init; } = string.Empty;
    public string WarnOutdated { get; init; } = string.Empty;
    public string XseAcronym { get; init; } = string.Empty;
    public List<string> GameIgnorePlugins { get; init; } = [];
    public List<string> GameIgnoreRecords { get; init; } = [];
    public Dictionary<string, string> SuspectsErrorList { get; init; } = [];
    public Dictionary<string, List<string>> SuspectsStackList { get; init; } = [];
    public string AutoscanText { get; init; } = string.Empty;
    public List<string> IgnoreList { get; init; } = [];
    public Dictionary<string, string> GameModsConf { get; init; } = [];
    public Dictionary<string, string> GameModsCore { get; init; } = [];
    public Dictionary<string, string> GameModsCoreFolon { get; init; } = [];
    public Dictionary<string, string> GameModsFreq { get; init; } = [];
    public Dictionary<string, string> GameModsOpc2 { get; init; } = [];
    public Dictionary<string, string> GameModsSolu { get; init; } = [];
    public Version GameVersion { get; init; } = new(0, 0, 0, 0);
    public Version GameVersionNew { get; init; } = new(0, 0, 0, 0);
    public Version GameVersionVr { get; init; } = new(0, 0, 0, 0);

    /// <summary>
    ///     Loads scan log information from YAML settings.
    /// </summary>
    /// <param name="yamlSettings">The YAML settings cache to load from.</param>
    /// <param name="gameContext">The game context service for game-specific settings.</param>
    /// <returns>A populated ScanLogInfo instance.</returns>
    public static ScanLogInfo LoadFromSettings(IYamlSettingsCache yamlSettings, IGameContextService gameContext)
    {
        var currentGame = gameContext.GetCurrentGame();
        var vrSuffix = currentGame.Contains("VR") ? "VR" : "";

        return new ScanLogInfo
        {
            ClassicGameHints = yamlSettings.GetSetting<List<string>>(YamlStore.Game, "Game_Hints") ?? [],
            ClassicRecordsList = yamlSettings.GetSetting<List<string>>(YamlStore.Main, "catch_log_records") ?? [],
            ClassicVersion = yamlSettings.GetSetting<string>(YamlStore.Main, "CLASSIC_Info.version") ?? string.Empty,
            ClassicVersionDate = yamlSettings.GetSetting<string>(YamlStore.Main, "CLASSIC_Info.version_date") ??
                                 string.Empty,
            CrashgenName =
                yamlSettings.GetSetting<string>(YamlStore.Game, "Game_Info.CRASHGEN_LogName") ?? string.Empty,
            CrashgenLatestOg = yamlSettings.GetSetting<string>(YamlStore.Game, "Game_Info.CRASHGEN_LatestVer") ??
                               string.Empty,
            CrashgenLatestVr = yamlSettings.GetSetting<string>(YamlStore.Game, "GameVR_Info.CRASHGEN_LatestVer") ??
                               string.Empty,
            CrashgenIgnore =
            [
                ..yamlSettings.GetSetting<List<string>>(YamlStore.Game, $"Game{vrSuffix}_Info.CRASHGEN_Ignore") ??
                  []
            ],
            WarnNoPlugins = yamlSettings.GetSetting<string>(YamlStore.Game, "Warnings_CRASHGEN.Warn_NOPlugins") ??
                            string.Empty,
            WarnOutdated = yamlSettings.GetSetting<string>(YamlStore.Game, "Warnings_CRASHGEN.Warn_Outdated") ??
                           string.Empty,
            XseAcronym = yamlSettings.GetSetting<string>(YamlStore.Game, "Game_Info.XSE_Acronym") ?? string.Empty,
            GameIgnorePlugins = yamlSettings.GetSetting<List<string>>(YamlStore.Game, "Crashlog_Plugins_Exclude") ?? [],
            GameIgnoreRecords = yamlSettings.GetSetting<List<string>>(YamlStore.Game, "Crashlog_Records_Exclude") ?? [],
            SuspectsErrorList =
                yamlSettings.GetSetting<Dictionary<string, string>>(YamlStore.Game, "Crashlog_Error_Check") ?? [],
            SuspectsStackList =
                yamlSettings.GetSetting<Dictionary<string, List<string>>>(YamlStore.Game, "Crashlog_Stack_Check") ?? [],
            AutoscanText =
                yamlSettings.GetSetting<string>(YamlStore.Main, $"CLASSIC_Interface.autoscan_text_{currentGame}") ??
                string.Empty,
            IgnoreList = yamlSettings.GetSetting<List<string>>(YamlStore.Ignore, $"CLASSIC_Ignore_{currentGame}") ?? [],
            GameModsConf = yamlSettings.GetSetting<Dictionary<string, string>>(YamlStore.Game, "Mods_CONF") ?? [],
            GameModsCore = yamlSettings.GetSetting<Dictionary<string, string>>(YamlStore.Game, "Mods_CORE") ?? [],
            GameModsCoreFolon =
                yamlSettings.GetSetting<Dictionary<string, string>>(YamlStore.Game, "Mods_CORE_FOLON") ?? [],
            GameModsFreq = yamlSettings.GetSetting<Dictionary<string, string>>(YamlStore.Game, "Mods_FREQ") ?? [],
            GameModsOpc2 = yamlSettings.GetSetting<Dictionary<string, string>>(YamlStore.Game, "Mods_OPC2") ?? [],
            GameModsSolu = yamlSettings.GetSetting<Dictionary<string, string>>(YamlStore.Game, "Mods_SOLU") ?? [],
            GameVersion = ParseVersion(yamlSettings.GetSetting<string>(YamlStore.Game, "Game_Info.GameVersion")),
            GameVersionNew = ParseVersion(yamlSettings.GetSetting<string>(YamlStore.Game, "Game_Info.GameVersionNEW")),
            GameVersionVr = ParseVersion(yamlSettings.GetSetting<string>(YamlStore.Game, "GameVR_Info.GameVersion"))
        };
    }

    private static Version ParseVersion(string? versionString)
    {
        if (string.IsNullOrEmpty(versionString) || !Version.TryParse(versionString, out var version))
            return new Version(0, 0, 0, 0);

        return version;
    }
}