using Scanner111.Core.Models;
using Scanner111.Core.Infrastructure;

namespace Scanner111.Core.Analyzers;

/// <summary>
/// Handles validation of crash generator and mod settings, direct port of Python SettingsScanner
/// </summary>
public class SettingsScanner : IAnalyzer
{
    private readonly IYamlSettingsProvider _yamlSettings;

    /// <summary>
    /// Name of the analyzer
    /// </summary>
    public string Name => "Settings Scanner";
    
    /// <summary>
    /// Priority of the analyzer (lower values run first)
    /// </summary>
    public int Priority => 5;
    
    /// <summary>
    /// Whether this analyzer can be run in parallel with others
    /// </summary>
    public bool CanRunInParallel => false;

    /// <summary>
    /// Initialize the settings scanner
    /// </summary>
    /// <param name="yamlSettings">YAML settings provider for configuration</param>
    public SettingsScanner(IYamlSettingsProvider yamlSettings)
    {
        _yamlSettings = yamlSettings;
    }

    /// <summary>
    /// Analyze a crash log for settings validation
    /// </summary>
    /// <param name="crashLog">Crash log to analyze</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Settings analysis result</returns>
    public async Task<AnalysisResult> AnalyzeAsync(CrashLog crashLog, CancellationToken cancellationToken = default)
    {
        await Task.CompletedTask; // Make it async-ready

        var reportLines = new List<string>();

        // TODO: Extract XSE modules and crashgen settings from crash log
        // This would require parsing the crash log content for these sections
        var xseModules = new HashSet<string>();
        var crashgenSettings = new Dictionary<string, object>();
        var crashgenIgnore = new HashSet<string>();

        // Perform various settings validations
        ScanBuffoutAchievementsSetting(reportLines, xseModules, crashgenSettings);
        ScanBuffoutMemoryManagementSettings(reportLines, crashgenSettings, 
            xseModules.Contains("x-cell.dll"), 
            xseModules.Contains("bakascrapheap.dll"));
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
                {"XSEModules", xseModules},
                {"CrashgenSettings", crashgenSettings}
            }
        };
    }

    /// <summary>
    /// Scans the achievements setting in the configuration for potential conflicts.
    /// Direct port of Python scan_buffout_achievements_setting method.
    /// </summary>
    /// <param name="autoscanReport">The list used to store the autoscan report</param>
    /// <param name="xseModules">A set of currently loaded XSE plugin modules</param>
    /// <param name="crashgen">A dictionary containing the configuration settings for the crash generator</param>
    private void ScanBuffoutAchievementsSetting(List<string> autoscanReport, HashSet<string> xseModules, Dictionary<string, object> crashgen)
    {
        var crashgenAchievements = crashgen.GetValueOrDefault("Achievements");
        if (crashgenAchievements is true && (xseModules.Contains("achievements.dll") || xseModules.Contains("unlimitedsurvivalmode.dll")))
        {
            autoscanReport.AddRange(new[]
            {
                "# ❌ CAUTION : The Achievements Mod and/or Unlimited Survival Mode is installed, but Achievements is set to TRUE # \n",
                $" FIX: Open {_yamlSettings.GetSetting("CLASSIC Fallout4", "Game_Info.CRASHGEN_LogName", "Crash Logger")}'s TOML file and change Achievements to FALSE, this prevents conflicts with {_yamlSettings.GetSetting("CLASSIC Fallout4", "Game_Info.CRASHGEN_LogName", "Crash Logger")}.\n-----\n"
            });
        }
        else
        {
            autoscanReport.Add($"✔️ Achievements parameter is correctly configured in your {_yamlSettings.GetSetting("CLASSIC Fallout4", "Game_Info.CRASHGEN_LogName", "Crash Logger")} settings! \n-----\n");
        }
    }

    /// <summary>
    /// Analyzes and adjusts memory management settings based on compatibility requirements.
    /// Direct port of Python scan_buffout_memorymanagement_settings method.
    /// </summary>
    /// <param name="autoscanReport">A list containing the current autoscan report</param>
    /// <param name="crashgen">A dictionary containing the current memory management configuration settings</param>
    /// <param name="hasXCell">A flag indicating whether the X-Cell mod is installed</param>
    /// <param name="hasBakaScrapheap">A flag indicating whether the Baka ScrapHeap mod is installed</param>
    private void ScanBuffoutMemoryManagementSettings(List<string> autoscanReport, Dictionary<string, object> crashgen, bool hasXCell, bool hasBakaScrapheap)
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
            autoscanReport.AddRange(new[] { $"{warningPrefix}{warning} # \n", $"{fixPrefix}{fix}{separator}" });
        }

        // Check main MemoryManager setting
        var memManagerEnabled = crashgen.GetValueOrDefault("MemoryManager", false);

        // Handle main memory manager configuration
        if (memManagerEnabled is true)
        {
            if (hasXCell)
            {
                AddWarningMessage(
                    "X-Cell is installed, but MemoryManager parameter is set to TRUE",
                    $"Open {crashgenName}'s TOML file and change MemoryManager to FALSE, this prevents conflicts with X-Cell."
                );
            }
            else if (hasBakaScrapheap)
            {
                AddWarningMessage(
                    $"The Baka ScrapHeap Mod is installed, but is redundant with {crashgenName}",
                    $"Uninstall the Baka ScrapHeap Mod, this prevents conflicts with {crashgenName}."
                );
            }
            else
            {
                AddSuccessMessage($"Memory Manager parameter is correctly configured in your {crashgenName} settings!");
            }
        }
        else if (hasXCell)
        {
            if (hasBakaScrapheap)
            {
                AddWarningMessage(
                    "The Baka ScrapHeap Mod is installed, but is redundant with X-Cell",
                    "Uninstall the Baka ScrapHeap Mod, this prevents conflicts with X-Cell."
                );
            }
            else
            {
                AddSuccessMessage($"Memory Manager parameter is correctly configured for use with X-Cell in your {crashgenName} settings!");
            }
        }
        else if (hasBakaScrapheap)
        {
            AddWarningMessage(
                $"The Baka ScrapHeap Mod is installed, but is redundant with {crashgenName}",
                $"Uninstall the Baka ScrapHeap Mod and open {crashgenName}'s TOML file and change MemoryManager to TRUE, this improves performance."
            );
        }

        // Check additional memory settings for X-Cell compatibility
        if (hasXCell)
        {
            var memorySettings = new Dictionary<string, string>
            {
                {"HavokMemorySystem", "Havok Memory System"},
                {"BSTextureStreamerLocalHeap", "BSTextureStreamerLocalHeap"},
                {"ScaleformAllocator", "Scaleform Allocator"},
                {"SmallBlockAllocator", "Small Block Allocator"}
            };

            foreach (var (settingKey, displayName) in memorySettings)
            {
                if (crashgen.GetValueOrDefault(settingKey) is true)
                {
                    AddWarningMessage(
                        $"X-Cell is installed, but {settingKey} parameter is set to TRUE",
                        $"Open {crashgenName}'s TOML file and change {settingKey} to FALSE, this prevents conflicts with X-Cell."
                    );
                }
                else
                {
                    AddSuccessMessage($"{displayName} parameter is correctly configured for use with X-Cell in your {crashgenName} settings!");
                }
            }
        }
    }

    /// <summary>
    /// Scans and validates the "ArchiveLimit" setting in the provided crash generation configuration.
    /// Direct port of Python scan_archivelimit_setting method.
    /// </summary>
    /// <param name="autoscanReport">List to store warnings or confirmations regarding the "ArchiveLimit" parameter</param>
    /// <param name="crashgen">Dictionary containing crash generation settings, including "ArchiveLimit"</param>
    private void ScanArchiveLimitSetting(List<string> autoscanReport, Dictionary<string, object> crashgen)
    {
        var crashgenArchiveLimit = crashgen.GetValueOrDefault("ArchiveLimit");
        if (crashgenArchiveLimit is true)
        {
            autoscanReport.AddRange(new[]
            {
                "# ❌ CAUTION : ArchiveLimit is set to TRUE, this setting is known to cause instability. # \n",
                $" FIX: Open {_yamlSettings.GetSetting("CLASSIC Fallout4", "Game_Info.CRASHGEN_LogName", "Crash Logger")}'s TOML file and change ArchiveLimit to FALSE.\n-----\n"
            });
        }
        else
        {
            autoscanReport.Add($"✔️ ArchiveLimit parameter is correctly configured in your {_yamlSettings.GetSetting("CLASSIC Fallout4", "Game_Info.CRASHGEN_LogName", "Crash Logger")} settings! \n-----\n");
        }
    }

    /// <summary>
    /// Analyzes the Looksmenu setting in the provided crash generation configuration, ensuring proper compatibility settings.
    /// Direct port of Python scan_buffout_looksmenu_setting method.
    /// </summary>
    /// <param name="crashgen">A mapping containing the crash generation settings</param>
    /// <param name="autoscanReport">A list used for appending messages generated by the scan process</param>
    /// <param name="xseModules">A set of module names that indicates the external script extender modules available</param>
    private void ScanBuffoutLooksMenuSetting(Dictionary<string, object> crashgen, List<string> autoscanReport, HashSet<string> xseModules)
    {
        var crashgenF4ee = crashgen.GetValueOrDefault("F4EE");
        if (crashgenF4ee != null)
        {
            if (crashgenF4ee is not true && xseModules.Contains("f4ee.dll"))
            {
                autoscanReport.AddRange(new[]
                {
                    "# ❌ CAUTION : Looks Menu is installed, but F4EE parameter under [Compatibility] is set to FALSE # \n",
                    $" FIX: Open {_yamlSettings.GetSetting("CLASSIC Fallout4", "Game_Info.CRASHGEN_LogName", "Crash Logger")}'s TOML file and change F4EE to TRUE, this prevents bugs and crashes from Looks Menu.\n-----\n"
                });
            }
            else
            {
                autoscanReport.Add($"✔️ F4EE (Looks Menu) parameter is correctly configured in your {_yamlSettings.GetSetting("CLASSIC Fallout4", "Game_Info.CRASHGEN_LogName", "Crash Logger")} settings! \n-----\n");
            }
        }
    }

    /// <summary>
    /// Check disabled settings in crash generation configuration and log notices.
    /// Direct port of Python check_disabled_settings method.
    /// </summary>
    /// <param name="crashgen">A dictionary containing crash generation settings</param>
    /// <param name="autoscanReport">A list to which any generated notice messages will be appended</param>
    /// <param name="crashgenIgnore">A set of setting names that should be ignored</param>
    private void CheckDisabledSettings(Dictionary<string, object> crashgen, List<string> autoscanReport, HashSet<string> crashgenIgnore)
    {
        if (crashgen.Count > 0)
        {
            foreach (var (settingName, settingValue) in crashgen)
            {
                if (settingValue is false && !crashgenIgnore.Contains(settingName))
                {
                    autoscanReport.Add($"* NOTICE : {settingName} is disabled in your {_yamlSettings.GetSetting("CLASSIC Fallout4", "Game_Info.CRASHGEN_LogName", "Crash Logger")} settings, is this intentional? * \n-----\n");
                }
            }
        }
    }
}