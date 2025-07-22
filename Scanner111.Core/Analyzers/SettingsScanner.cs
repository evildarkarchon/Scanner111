using Scanner111.Core.Infrastructure;
using Scanner111.Core.Models;

namespace Scanner111.Core.Analyzers;

/// <summary>
/// Represents an analyzer that validates crash generator and mod settings.
/// </summary>
public class SettingsScanner : IAnalyzer
{
    private readonly IYamlSettingsProvider _yamlSettings;

    /// <summary>
    ///     Initialize the settings scanner
    /// </summary>
    /// <param name="yamlSettings">YAML settings provider for configuration</param>
    public SettingsScanner(IYamlSettingsProvider yamlSettings)
    {
        _yamlSettings = yamlSettings;
    }

    /// <summary>
    ///     Name of the analyzer
    /// </summary>
    public string Name => "Settings Scanner";

    /// <summary>
    ///     Priority of the analyzer (lower values run first)
    /// </summary>
    public int Priority => 5;

    /// <summary>
    ///     Whether this analyzer can be run in parallel with others
    /// </summary>
    public bool CanRunInParallel => false;

    /// <summary>
    /// Analyze a crash log for settings validation
    /// </summary>
    /// <param name="crashLog">The crash log to be analyzed</param>
    /// <param name="cancellationToken">Token to monitor for cancellation requests</param>
    /// <returns>A task that represents the analysis operation, containing the analysis result</returns>
    public async Task<AnalysisResult> AnalyzeAsync(CrashLog crashLog, CancellationToken cancellationToken = default)
    {
        await Task.CompletedTask; // Make it async-ready

        var reportLines = new List<string>();

        // Extract XSE modules and crashgen settings from crash log
        var xseModules = crashLog.XseModules;
        var crashgenSettings = crashLog.CrashgenSettings;

        var crashgenIgnoreList =
            _yamlSettings.GetSetting("CLASSIC Fallout4", "Game_Info.CRASHGEN_Ignore",
                new List<string>()) ?? new List<string>();
        var crashgenIgnore = new HashSet<string>(crashgenIgnoreList, StringComparer.OrdinalIgnoreCase);

        // Check for X-Cell and Baka ScrapHeap (matching Python implementation)
        var hasXCell = xseModules.Contains("x-cell-fo4.dll") ||
                       xseModules.Contains("x-cell-og.dll") ||
                       xseModules.Contains("x-cell-ng2.dll");
        var hasBakaScrapheap = xseModules.Contains("bakascrapheap.dll");

        // Perform various settings validations
        ScanBuffoutAchievementsSetting(reportLines, xseModules, crashgenSettings);
        ScanBuffoutMemoryManagementSettings(reportLines, crashgenSettings, hasXCell, hasBakaScrapheap);
        ScanArchiveLimitSetting(reportLines, crashgenSettings);
        ScanBuffoutLooksMenuSetting(crashgenSettings, reportLines, xseModules);
        CheckDisabledSettings(crashgenSettings, reportLines, crashgenIgnore);

        return new GenericAnalysisResult
        {
            AnalyzerName = Name,
            ReportLines = reportLines,
            HasFindings = reportLines.Count > 0,
            Data = new Dictionary<string, object>
            {
                { "XSEModules", xseModules },
                { "CrashgenSettings", crashgenSettings }
            }
        };
    }

    /// <summary>
    /// Scans the Buffout achievements setting in the configuration for conflicts and generates a report.
    /// </summary>
    /// <param name="autoscanReport">The list used to append the generated autoscan report messages</param>
    /// <param name="xseModules">A collection of currently loaded XSE plugin modules</param>
    /// <param name="crashgen">The configuration dictionary for the crash generator, including achievement settings</param>
    private void ScanBuffoutAchievementsSetting(List<string> autoscanReport, HashSet<string> xseModules,
        Dictionary<string, object> crashgen)
    {
        var crashgenAchievements = crashgen.GetValueOrDefault("Achievements");
        if (crashgenAchievements is true &&
            (xseModules.Contains("achievements.dll") || xseModules.Contains("unlimitedsurvivalmode.dll")))
            autoscanReport.AddRange([
                "# ❌ CAUTION : The Achievements Mod and/or Unlimited Survival Mode is installed, but Achievements is set to TRUE # \n",
                $" FIX: Open {_yamlSettings.GetSetting("CLASSIC Fallout4", "Game_Info.CRASHGEN_LogName", "Crash Logger")}'s TOML file and change Achievements to FALSE, this prevents conflicts with {_yamlSettings.GetSetting("CLASSIC Fallout4", "Game_Info.CRASHGEN_LogName", "Crash Logger")}.\n-----\n"
            ]);
        else
            autoscanReport.Add(
                $"✔️ Achievements parameter is correctly configured in your {_yamlSettings.GetSetting("CLASSIC Fallout4", "Game_Info.CRASHGEN_LogName", "Crash Logger")} settings! \n-----\n");
    }

    /// <summary>
    /// Analyzes and adjusts memory management settings based on compatibility requirements.
    /// </summary>
    /// <param name="autoscanReport">A list containing the current autoscan report</param>
    /// <param name="crashgen">A dictionary containing the current memory management configuration settings</param>
    /// <param name="hasXCell">A flag indicating whether the X-Cell mod is installed</param>
    /// <param name="hasBakaScrapheap">A flag indicating whether the Baka ScrapHeap mod is installed</param>
    private void ScanBuffoutMemoryManagementSettings(List<string> autoscanReport, Dictionary<string, object> crashgen,
        bool hasXCell, bool hasBakaScrapheap)
    {
        // Constants for messages and settings
        const string separator = "\n-----\n";
        const string successPrefix = "✔️ ";
        const string warningPrefix = "# ❌ CAUTION : ";
        const string fixPrefix = " FIX: ";
        var crashgenName = _yamlSettings.GetSetting("CLASSIC Fallout4", "Game_Info.CRASHGEN_LogName", "Crash Logger");

        void AddSuccessMessage(string message)
        {
            autoscanReport.Add($"{successPrefix}{message}{separator}");
        }

        void AddWarningMessage(string warning, string fix)
        {
            autoscanReport.AddRange([$"{warningPrefix}{warning} # \n", $"{fixPrefix}{fix}{separator}"]);
        }

        // Check main MemoryManager setting
        var memManagerEnabled = crashgen.GetValueOrDefault("MemoryManager", false);

        // Handle main memory manager configuration
        if (memManagerEnabled is true)
        {
            if (hasXCell)
                AddWarningMessage(
                    "X-Cell is installed, but MemoryManager parameter is set to TRUE",
                    $"Open {crashgenName}'s TOML file and change MemoryManager to FALSE, this prevents conflicts with X-Cell."
                );
            else if (hasBakaScrapheap)
                AddWarningMessage(
                    $"The Baka ScrapHeap Mod is installed, but is redundant with {crashgenName}",
                    $"Uninstall the Baka ScrapHeap Mod, this prevents conflicts with {crashgenName}."
                );
            else
                AddSuccessMessage($"Memory Manager parameter is correctly configured in your {crashgenName} settings!");
        }
        else if (hasXCell)
        {
            if (hasBakaScrapheap)
                AddWarningMessage(
                    "The Baka ScrapHeap Mod is installed, but is redundant with X-Cell",
                    "Uninstall the Baka ScrapHeap Mod, this prevents conflicts with X-Cell."
                );
            else
                AddSuccessMessage(
                    $"Memory Manager parameter is correctly configured for use with X-Cell in your {crashgenName} settings!");
        }
        else if (hasBakaScrapheap)
        {
            AddWarningMessage(
                $"The Baka ScrapHeap Mod is installed, but is redundant with {crashgenName}",
                $"Uninstall the Baka ScrapHeap Mod and open {crashgenName}'s TOML file and change MemoryManager to TRUE, this improves performance."
            );
        }

        // Check additional memory settings for X-Cell compatibility
        if (!hasXCell) return;
        var memorySettings = new Dictionary<string, string>
        {
            { "HavokMemorySystem", "Havok Memory System" },
            { "BSTextureStreamerLocalHeap", "BSTextureStreamerLocalHeap" },
            { "ScaleformAllocator", "Scaleform Allocator" },
            { "SmallBlockAllocator", "Small Block Allocator" }
        };

        foreach (var (settingKey, displayName) in memorySettings)
            if (crashgen.GetValueOrDefault(settingKey) is true)
                AddWarningMessage(
                    $"X-Cell is installed, but {settingKey} parameter is set to TRUE",
                    $"Open {crashgenName}'s TOML file and change {settingKey} to FALSE, this prevents conflicts with X-Cell."
                );
            else
                AddSuccessMessage(
                    $"{displayName} parameter is correctly configured for use with X-Cell in your {crashgenName} settings!");
    }

    /// <summary>
    /// Scans and assesses the configuration of the "ArchiveLimit" setting in crash generation settings.
    /// </summary>
    /// <param name="autoscanReport">Collection for storing validation results or recommendations about the "ArchiveLimit" setting</param>
    /// <param name="crashgen">Dictionary representing crash generation settings, including the "ArchiveLimit" parameter</param>
    private void ScanArchiveLimitSetting(List<string> autoscanReport, Dictionary<string, object> crashgen)
    {
        var crashgenArchiveLimit = crashgen.GetValueOrDefault("ArchiveLimit");
        if (crashgenArchiveLimit is true)
            autoscanReport.AddRange([
                "# ❌ CAUTION : ArchiveLimit is set to TRUE, this setting is known to cause instability. # \n",
                $" FIX: Open {_yamlSettings.GetSetting("CLASSIC Fallout4", "Game_Info.CRASHGEN_LogName", "Crash Logger")}'s TOML file and change ArchiveLimit to FALSE.\n-----\n"
            ]);
        else
            autoscanReport.Add(
                $"✔️ ArchiveLimit parameter is correctly configured in your {_yamlSettings.GetSetting("CLASSIC Fallout4", "Game_Info.CRASHGEN_LogName", "Crash Logger")} settings! \n-----\n");
    }

    /// <summary>
    /// Analyzes the LooksMenu setting in the provided crash generation settings, verifying compatibility and extracting any issues.
    /// </summary>
    /// <param name="crashgen">A dictionary containing crash generation settings to be analyzed</param>
    /// <param name="autoscanReport">A list for appending messages generated during the scan process</param>
    /// <param name="xseModules">A set of names representing external script extender modules available in the environment</param>
    private void ScanBuffoutLooksMenuSetting(Dictionary<string, object> crashgen, List<string> autoscanReport,
        HashSet<string> xseModules)
    {
        var crashgenF4Ee = crashgen.GetValueOrDefault("F4EE");
        if (crashgenF4Ee != null)
        {
            if (crashgenF4Ee is not true && xseModules.Contains("f4ee.dll"))
                autoscanReport.AddRange([
                    "# ❌ CAUTION : Looks Menu is installed, but F4EE parameter under [Compatibility] is set to FALSE # \n",
                    $" FIX: Open {_yamlSettings.GetSetting("CLASSIC Fallout4", "Game_Info.CRASHGEN_LogName", "Crash Logger")}'s TOML file and change F4EE to TRUE, this prevents bugs and crashes from Looks Menu.\n-----\n"
                ]);
            else
                autoscanReport.Add(
                    $"✔️ F4EE (Looks Menu) parameter is correctly configured in your {_yamlSettings.GetSetting("CLASSIC Fallout4", "Game_Info.CRASHGEN_LogName", "Crash Logger")} settings! \n-----\n");
        }
    }

    /// <summary>
    /// Checks for disabled settings in the crash generation configuration and
    /// logs warnings for any that are not in the ignore list.
    /// </summary>
    /// <param name="crashgen">A dictionary containing the crash generation settings.</param>
    /// <param name="autoscanReport">A list to which any warning or notice messages will be appended.</param>
    /// <param name="crashgenIgnore">A set of setting names that should be ignored during the check.</param>
    private void CheckDisabledSettings(Dictionary<string, object> crashgen, List<string> autoscanReport,
        HashSet<string> crashgenIgnore)
    {
        if (crashgen.Count <= 0) return;
        foreach (var (settingName, settingValue) in crashgen)
            if (settingValue is false)
            {
                var isIgnored = crashgenIgnore.Contains(settingName);

                if (!isIgnored)
                    autoscanReport.Add(
                        $"* NOTICE : {settingName} is disabled in your {_yamlSettings.GetSetting("CLASSIC Fallout4", "Game_Info.CRASHGEN_LogName", "Crash Logger")} settings, is this intentional? * \n-----\n");
            }
    }
}