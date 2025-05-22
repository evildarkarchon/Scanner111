using System;
using System.IO;
using System.Text;

namespace Scanner111.Services;

/// <summary>
///     Implementation of the game context service.
/// </summary>
public class GameContextService : IGameContextService
{
    private readonly IYamlSettingsCache _settingsCache;
    private string _currentGame = "Default";

    /// <summary>
    ///     Initializes a new instance of the <see cref="GameContextService" /> class.
    /// </summary>
    /// <param name="settingsCache">The YAML settings cache.</param>
    public GameContextService(IYamlSettingsCache settingsCache)
    {
        _settingsCache = settingsCache;
    }

    /// <inheritdoc />
    public string GetCurrentGame()
    {
        return _currentGame;
    }

    /// <inheritdoc />
    public void SetCurrentGame(string gameName)
    {
        if (string.IsNullOrEmpty(gameName))
            throw new ArgumentException("Game name cannot be null or empty", nameof(gameName));

        _currentGame = gameName;
    }

    /// <inheritdoc />
    public string GetGameVr()
    {
        return _currentGame.Contains("VR") ? "VR" : "";
    }

    /// <inheritdoc />
    public string CheckXsePlugins()
    {
        var gameVr = GetGameVr();
        var xseAcronym = _settingsCache.GetSetting<string>(YamlStore.Game, $"Game{gameVr}_Info.XSE_Acronym") ?? "XSE";
        var xsePluginPath =
            _settingsCache.GetSetting<string>(YamlStore.GameLocal, $"Game{gameVr}_Info.Folder_XSE_Plugins");

        if (string.IsNullOrEmpty(xsePluginPath) || !Directory.Exists(xsePluginPath)) return string.Empty;

        var badPlugins = new StringBuilder();

        // Get allowed load order values
        var loadOrderMin = _settingsCache.GetSetting<int>(YamlStore.Game, "XSE_Plugins.Order_Min");
        var loadOrderMax = _settingsCache.GetSetting<int>(YamlStore.Game, "XSE_Plugins.Order_Max");

        // Get excluded plugins
        var excludePlugins = _settingsCache.GetSetting<string[]>(YamlStore.Game, "XSE_Plugins.Exclude_These");

        foreach (var file in Directory.GetFiles(xsePluginPath, "*.esp"))
        {
            var fileName = Path.GetFileName(file);

            // Skip excluded plugins
            if (excludePlugins != null && Array.Exists(excludePlugins,
                    exclude => fileName.Equals(exclude, StringComparison.OrdinalIgnoreCase))) continue;

            // Check for load order issues
            var loadOrderStr = fileName.Substring(0, 2);
            if (int.TryParse(loadOrderStr, out var loadOrder))
                if (loadOrder < loadOrderMin || loadOrder > loadOrderMax)
                {
                    badPlugins.AppendLine(
                        $"[!] CAUTION : THIS {xseAcronym} PLUGIN HAS A BAD LOAD ORDER AND MAY CAUSE PROBLEMS!");
                    badPlugins.AppendLine($"  Plugin: {fileName}");
                    badPlugins.AppendLine(
                        $"  Load Order: {loadOrder} (should be between {loadOrderMin} and {loadOrderMax})");
                    badPlugins.AppendLine();
                }
        }

        return badPlugins.ToString();
    }

    /// <inheritdoc />
    public string CheckCrashgenSettings()
    {
        var gameVr = GetGameVr();
        var crashgenIni =
            _settingsCache.GetSetting<string>(YamlStore.GameLocal, $"Game{gameVr}_Info.File_Crashgen_Ini");

        if (string.IsNullOrEmpty(crashgenIni) || !File.Exists(crashgenIni)) return string.Empty;

        var badSettings = new StringBuilder();
        var lines = File.ReadAllLines(crashgenIni);
        var memDumpValue = string.Empty;
        var fullMiniDumpValue = string.Empty;

        foreach (var line in lines)
            if (line.StartsWith("bCreateMiniDump="))
                memDumpValue = line.Substring("bCreateMiniDump=".Length).Trim();
            else if (line.StartsWith("bCreateFullDump="))
                fullMiniDumpValue = line.Substring("bCreateFullDump=".Length).Trim();

        if (memDumpValue.Equals("0", StringComparison.OrdinalIgnoreCase) ||
            fullMiniDumpValue.Equals("1", StringComparison.OrdinalIgnoreCase))
        {
            badSettings.AppendLine("[!] CAUTION : YOUR CRASH SETTINGS ARE INCORRECT AND MAY CAUSE PROBLEMS!");
            badSettings.AppendLine("  The following settings should be changed in your crashgen.ini file:");

            if (memDumpValue.Equals("0", StringComparison.OrdinalIgnoreCase))
                badSettings.AppendLine("  bCreateMiniDump=0 should be set to bCreateMiniDump=1");

            if (fullMiniDumpValue.Equals("1", StringComparison.OrdinalIgnoreCase))
                badSettings.AppendLine("  bCreateFullDump=1 should be set to bCreateFullDump=0");

            badSettings.AppendLine();
        }

        return badSettings.ToString();
    }

    /// <inheritdoc />
    public string ScanWryeCheck()
    {
        var gameVr = GetGameVr();
        var dataPath = _settingsCache.GetSetting<string>(YamlStore.GameLocal, $"Game{gameVr}_Info.Root_Folder_Data");

        if (string.IsNullOrEmpty(dataPath) || !Directory.Exists(dataPath)) return string.Empty;

        var warnings = new StringBuilder();
        var wryeBashProfile = Path.Combine(dataPath, "Bash Patches", "WryeBash.csv");

        if (File.Exists(wryeBashProfile))
        {
            var lines = File.ReadAllLines(wryeBashProfile);
            foreach (var line in lines)
                if (line.Contains(",\"Deactivated,"))
                {
                    var parts = line.Split(',');
                    if (parts.Length > 0 && parts[0].StartsWith("\"") && parts[0].EndsWith("\""))
                    {
                        var pluginName = parts[0].Trim('"');
                        warnings.AppendLine($"[!] CAUTION : PLUGIN DEACTIVATED BY WRYE BASH: {pluginName}");
                    }
                }

            if (warnings.Length > 0)
            {
                warnings.Insert(0, "[!] CAUTION : WRYE BASH HAS DEACTIVATED ONE OR MORE PLUGINS!\n");
                warnings.AppendLine();
            }
        }

        return warnings.ToString();
    }

    /// <inheritdoc />
    public string ScanModInis()
    {
        var gameVr = GetGameVr();
        var iniPath = _settingsCache.GetSetting<string>(YamlStore.GameLocal, $"Game{gameVr}_Info.File_Custom_Ini");

        if (string.IsNullOrEmpty(iniPath) || !File.Exists(iniPath)) return string.Empty;

        var warnings = new StringBuilder();
        var lines = File.ReadAllLines(iniPath);

        // Check for specific problematic settings
        var hasArchiveInvalidation = false;
        var hasBadWaterSettings = false;

        foreach (var line in lines)
        {
            var trimmedLine = line.Trim();

            // Check for archive invalidation settings
            if (trimmedLine.StartsWith("bInvalidateOlderFiles=") &&
                trimmedLine.EndsWith("0", StringComparison.Ordinal))
                hasArchiveInvalidation = true;

            // Check for problematic water settings
            if (trimmedLine.Contains("Water") &&
                (trimmedLine.Contains("bForceHighDetailReflections=0") ||
                 trimmedLine.Contains("bUseWaterHiRes=0")))
                hasBadWaterSettings = true;
        }

        if (hasArchiveInvalidation)
        {
            warnings.AppendLine("[!] CAUTION : ARCHIVE INVALIDATION IS DISABLED IN YOUR INI FILE!");
            warnings.AppendLine("  This may cause texture mods to not work correctly.");
            warnings.AppendLine("  Change bInvalidateOlderFiles=0 to bInvalidateOlderFiles=1");
            warnings.AppendLine();
        }

        if (hasBadWaterSettings)
        {
            warnings.AppendLine("[!] CAUTION : PROBLEMATIC WATER SETTINGS DETECTED IN YOUR INI FILE!");
            warnings.AppendLine("  These settings may cause water-related visual issues.");
            warnings.AppendLine("  Ensure bForceHighDetailReflections=1 and bUseWaterHiRes=1 are set.");
            warnings.AppendLine();
        }

        return warnings.ToString();
    }
}