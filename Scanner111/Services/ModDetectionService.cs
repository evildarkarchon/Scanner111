using Scanner111.Models;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;

namespace Scanner111.Services
{
    /// <summary>    /// Service responsible for detecting mods from crash logs and identifying potential issues
    /// Ported from the Python detect_mods_* functions in CLASSIC_ScanLogs.py
    /// </summary>
    public class ModDetectionService
    {
        private readonly IYamlSettingsCacheService _yamlSettingsCache;
        private readonly WarningDatabase _warningDatabase;
        private readonly AppSettings _appSettings;

        public ModDetectionService(
            IYamlSettingsCacheService yamlSettingsCache,
            WarningDatabase warningDatabase,
            AppSettings appSettings)
        {
            _yamlSettingsCache = yamlSettingsCache;
            _warningDatabase = warningDatabase;
            _appSettings = appSettings;
        }

        /// <summary>
        /// Detects issues with single mods based on the plugins loaded
        /// </summary>
        public void DetectSingleMods(ParsedCrashLog parsedLog, List<LogIssue> issues)
        {
            if (parsedLog?.LoadedPlugins == null || !parsedLog.LoadedPlugins.Any())
                return;

            var singleModWarnings = _warningDatabase.GetSinglePluginWarnings();
            if (singleModWarnings == null || !singleModWarnings.Any())
                return;

            foreach (var pluginName in parsedLog.LoadedPlugins.Keys)
            {
                // Check for exact matches
                if (singleModWarnings.TryGetValue(pluginName, out var warningDetails))
                {
                    AddModIssue(parsedLog, issues, pluginName, warningDetails);
                }

                // Also check for partial matches (e.g., plugin name as substring)
                foreach (var warningEntry in singleModWarnings)
                {
                    // Skip if we already found an exact match
                    if (string.Equals(warningEntry.Key, pluginName, StringComparison.OrdinalIgnoreCase))
                        continue;

                    // Check if the plugin name contains the warning key or vice versa
                    if (pluginName.Contains(warningEntry.Key, StringComparison.OrdinalIgnoreCase) ||
                        warningEntry.Key.Contains(pluginName, StringComparison.OrdinalIgnoreCase))
                    {
                        AddModIssue(parsedLog, issues, pluginName, warningEntry.Value, "Potential");
                    }
                }
            }
        }

        /// <summary>
        /// Detects conflicts between pairs of mods based on the plugins loaded
        /// </summary>
        public void DetectModConflicts(ParsedCrashLog parsedLog, List<LogIssue> issues)
        {
            if (parsedLog?.LoadedPlugins == null || !parsedLog.LoadedPlugins.Any())
                return;

            var modConflicts = _warningDatabase.GetPluginConflictWarnings();
            if (modConflicts == null || !modConflicts.Any())
                return;

            var loadedPluginNames = new HashSet<string>(parsedLog.LoadedPlugins.Keys, StringComparer.OrdinalIgnoreCase);

            foreach (var conflict in modConflicts)
            {
                // Check if both conflicting plugins are loaded
                bool pluginALoaded = IsPluginOrPartialMatch(loadedPluginNames, conflict.PluginA);
                bool pluginBLoaded = IsPluginOrPartialMatch(loadedPluginNames, conflict.PluginB);

                if (pluginALoaded && pluginBLoaded)
                {
                    issues.Add(new LogIssue
                    {
                        FileName = Path.GetFileName(parsedLog.FilePath),
                        IssueId = $"ModConflict_{conflict.PluginA}_{conflict.PluginB}",
                        Title = conflict.Title ?? $"Mod Conflict: {conflict.PluginA} & {conflict.PluginB}",
                        Message = conflict.Message,
                        Recommendation = conflict.Recommendation ??
                            "These mods are known to conflict. Consider using a compatibility patch or disabling one of them.",
                        Severity = conflict.Severity,
                        Source = "ModConflictDetection"
                    });
                }
            }
        }

        /// <summary>
        /// Checks if a plugin name matches exactly or partially in the loaded plugins list
        /// </summary>
        private bool IsPluginOrPartialMatch(HashSet<string> loadedPlugins, string pluginName)
        {
            // Exact match first
            if (loadedPlugins.Contains(pluginName))
                return true;

            // Then try for partial matches
            foreach (var loadedPlugin in loadedPlugins)
            {
                if (loadedPlugin.Contains(pluginName, StringComparison.OrdinalIgnoreCase) ||
                    pluginName.Contains(loadedPlugin, StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }
            }

            return false;
        }

        /// <summary>
        /// Detects issues with important mods that might require special attention
        /// </summary>
        public void DetectImportantMods(ParsedCrashLog parsedLog, List<LogIssue> issues)
        {
            if (parsedLog?.LoadedPlugins == null || !parsedLog.LoadedPlugins.Any())
                return;

            // Check if this is a Fallout London setup by looking for the main plugin
            bool isFolonActive = parsedLog.LoadedPlugins.ContainsKey("LondonWorldspace.esm");

            // Get the appropriate set of important mod notes
            var importantMods = isFolonActive
                ? _warningDatabase.GetImportantFolonPluginNotes()
                : _warningDatabase.GetImportantCorePluginNotes();

            if (importantMods == null || !importantMods.Any())
                return;

            // Check if important mods are present in the load order
            foreach (var importantMod in importantMods)
            {
                bool modFound = false;

                // Check for exact match
                if (parsedLog.LoadedPlugins.ContainsKey(importantMod.Key))
                {
                    modFound = true;
                }
                else
                {
                    // Check for partial match in plugin names
                    foreach (var plugin in parsedLog.LoadedPlugins.Keys)
                    {
                        if (plugin.Contains(importantMod.Key, StringComparison.OrdinalIgnoreCase))
                        {
                            modFound = true;
                            break;
                        }
                    }
                }

                // If the important mod is missing, add an issue
                if (!modFound)
                {
                    issues.Add(new LogIssue
                    {
                        FileName = Path.GetFileName(parsedLog.FilePath),
                        IssueId = $"MissingImportantMod_{importantMod.Key}",
                        Title = $"Missing Important Mod: {importantMod.Key}",
                        Message = importantMod.Value.Message,
                        Recommendation = importantMod.Value.Recommendation ??
                            $"Consider installing {importantMod.Key} to improve stability.",
                        Severity = SeverityLevel.Warning,
                        Source = "ImportantModDetection"
                    });
                }
            }
        }

        /// <summary>
        /// Checks for plugin count limits that could cause instability
        /// </summary>
        public void CheckPluginLimits(ParsedCrashLog parsedLog, List<LogIssue> issues)
        {
            if (parsedLog?.LoadedPlugins == null || !parsedLog.LoadedPlugins.Any())
                return;

            int totalPluginCount = parsedLog.LoadedPlugins.Count;
            int lightPluginCount = 0;
            int fullPluginCount = 0;

            // Count light plugins vs full plugins
            foreach (var plugin in parsedLog.LoadedPlugins)
            {
                if (plugin.Value.Contains("FE", StringComparison.OrdinalIgnoreCase) ||
                    plugin.Key.EndsWith(".esl", StringComparison.OrdinalIgnoreCase))
                {
                    lightPluginCount++;
                }
                else
                {
                    fullPluginCount++;
                }
            }

            // Plugin limit checks for Fallout 4
            const int MaxFullPlugins = 254; // FE is the 255th plugin, FF is reserved
            const int MaxLightPlugins = 4096; // Theoretical max is much higher, but this is a safe number

            // Check full plugin count
            if (fullPluginCount > MaxFullPlugins)
            {
                issues.Add(new LogIssue
                {
                    FileName = Path.GetFileName(parsedLog.FilePath),
                    IssueId = "PluginLimit_Full",
                    Title = "Full Plugin Limit Exceeded",
                    Message = $"You have {fullPluginCount} full plugins loaded, which exceeds the maximum of {MaxFullPlugins}.",
                    Recommendation = "Convert some plugins to ESL (light) format, or remove plugins you don't need.",
                    Severity = SeverityLevel.Critical,
                    Source = "PluginLimitCheck"
                });
            }
            else if (fullPluginCount > MaxFullPlugins - 20) // Warning when approaching the limit
            {
                issues.Add(new LogIssue
                {
                    FileName = Path.GetFileName(parsedLog.FilePath),
                    IssueId = "PluginLimit_FullWarning",
                    Title = "Approaching Full Plugin Limit",
                    Message = $"You have {fullPluginCount} full plugins loaded, which is close to the maximum of {MaxFullPlugins}.",
                    Recommendation = "Consider converting some plugins to ESL (light) format.",
                    Severity = SeverityLevel.Warning,
                    Source = "PluginLimitCheck"
                });
            }

            // Check light plugin count
            if (lightPluginCount > MaxLightPlugins)
            {
                issues.Add(new LogIssue
                {
                    FileName = Path.GetFileName(parsedLog.FilePath),
                    IssueId = "PluginLimit_Light",
                    Title = "Light Plugin Limit Exceeded",
                    Message = $"You have {lightPluginCount} light plugins loaded, which exceeds the safe maximum of {MaxLightPlugins}.",
                    Recommendation = "Remove light plugins you don't need.",
                    Severity = SeverityLevel.Critical,
                    Source = "PluginLimitCheck"
                });
            }

            // Total plugin warning
            if (totalPluginCount > 500) // Arbitrary large number that might cause performance issues
            {
                issues.Add(new LogIssue
                {
                    FileName = Path.GetFileName(parsedLog.FilePath),
                    IssueId = "PluginLimit_Total",
                    Title = "Very High Plugin Count",
                    Message = $"You have {totalPluginCount} total plugins loaded, which is a very high number.",
                    Recommendation = "Having too many plugins can cause performance issues and increase crash risk. Consider using merged plugins or removing unnecessary ones.",
                    Severity = SeverityLevel.Warning,
                    Source = "PluginLimitCheck"
                });
            }
        }

        private void AddModIssue(ParsedCrashLog parsedLog, List<LogIssue> issues, string pluginName, WarningDetails warningDetails, string prefix = "")
        {
            string titlePrefix = string.IsNullOrEmpty(prefix) ? "" : prefix + " ";

            issues.Add(new LogIssue
            {
                FileName = Path.GetFileName(parsedLog.FilePath),
                IssueId = $"SingleModIssue_{pluginName.Replace(".", "_")}",
                Title = !string.IsNullOrEmpty(warningDetails.Title)
                    ? $"{titlePrefix}{warningDetails.Title}"
                    : $"{titlePrefix}Plugin Issue: {pluginName}",
                Message = warningDetails.Message,
                Recommendation = warningDetails.Recommendation ?? string.Empty,
                Severity = warningDetails.Severity,
                Source = "ModDetection"
            });
        }
    }
}

