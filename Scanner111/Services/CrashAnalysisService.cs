using Scanner111.Models;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;

namespace Scanner111.Services
{
    /// <summary>
    /// Service responsible for analyzing crash logs for specific issues not related to plugins
    /// </summary>
    public class CrashAnalysisService
    {
        private readonly AppSettings _appSettings;
        private readonly WarningDatabase _warningDatabase;
        private readonly CrashStackAnalysis _crashStackAnalysis;
        private readonly IYamlSettingsCacheService _yamlSettingsCache;
        private readonly FormIdDatabaseService _formIdDatabaseService;

        public CrashAnalysisService(
            AppSettings appSettings,
            WarningDatabase warningDatabase,
            CrashStackAnalysis crashStackAnalysis,
            IYamlSettingsCacheService yamlSettingsCache,
            FormIdDatabaseService formIdDatabaseService)
        {
            _appSettings = appSettings;
            _warningDatabase = warningDatabase;
            _crashStackAnalysis = crashStackAnalysis;
            _yamlSettingsCache = yamlSettingsCache;
            _formIdDatabaseService = formIdDatabaseService;
        }

        /// <summary>
        /// Performs various analyses on the crash log
        /// </summary>
        public void AnalyzeCrashLog(ParsedCrashLog parsedLog, List<LogIssue> issues)
        {
            // Core analysis methods
            ScanForFormIDMatches(parsedLog, issues);
            ScanForPluginMatches(parsedLog, issues);
            ScanForNamedRecords(parsedLog, issues);

            // Advanced analysis methods
            ScanForMainErrorSuspects(parsedLog, issues);
            ScanForCallStackSuspects(parsedLog, issues);
            ScanForLoadOrderIssues(parsedLog, issues);
            ScanBuffoutSettings(parsedLog, issues);
        }

        /// <summary>
        /// Scans for FormID matches in the call stack
        /// </summary>
        public void ScanForFormIDMatches(ParsedCrashLog parsedLog, List<LogIssue> issues)
        {
            if (parsedLog?.CallStack == null || !parsedLog.CallStack.Any())
                return;

            // Skip if FormID databases are not available or showing FormIDs is disabled
            if (!_appSettings.ShowFormIdValues || !_formIdDatabaseService.DatabaseExists())
                return;

            var formIdRegex = new Regex(@"\b([0-9a-fA-F]{8})\b", RegexOptions.Compiled);
            // Additional regex to extract plugin IDs (first two characters of a FormID reference)
            var pluginIdRegex = new Regex(@"([0-9a-fA-F]{2})([0-9a-fA-F]{6})", RegexOptions.Compiled);

            // Create a dictionary to track unique FormIDs with their occurrence count
            var formIdCounts = new Dictionary<string, int>();
            var formIdPlugins = new Dictionary<string, Dictionary<string, int>>();

            // First pass: collect all FormIDs and count occurrences
            foreach (var line in parsedLog.CallStack)
            {
                if (string.IsNullOrEmpty(line))
                    continue;

                var matches = formIdRegex.Matches(line);
                foreach (Match match in matches)
                {
                    var formId = match.Groups[1].Value.ToUpper();

                    // Count total occurrences
                    if (formIdCounts.ContainsKey(formId))
                        formIdCounts[formId]++;
                    else
                        formIdCounts[formId] = 1;

                    // For each FormID, try to associate it with plugins
                    if (parsedLog.LoadedPlugins != null && parsedLog.LoadedPlugins.Any())
                    {
                        var pluginMatch = pluginIdRegex.Match(formId);
                        if (pluginMatch.Success)
                        {
                            string pluginId = pluginMatch.Groups[1].Value;
                            string actualFormId = pluginMatch.Groups[2].Value;

                            // Find matching plugins for this ID
                            foreach (var plugin in parsedLog.LoadedPlugins)
                            {
                                if (plugin.Value == pluginId)
                                {
                                    // Track which FormID belongs to which plugin
                                    if (!formIdPlugins.ContainsKey(formId))
                                        formIdPlugins[formId] = new Dictionary<string, int>();

                                    if (formIdPlugins[formId].ContainsKey(plugin.Key))
                                        formIdPlugins[formId][plugin.Key]++;
                                    else
                                        formIdPlugins[formId][plugin.Key] = 1;
                                }
                            }
                        }
                    }
                }
            }

            // Second pass: add issues for FormIDs with plugin and lookup information
            foreach (var formIdEntry in formIdCounts)
            {
                string formId = formIdEntry.Key;
                int count = formIdEntry.Value;

                if (formIdPlugins.TryGetValue(formId, out var pluginMatches) && pluginMatches.Any())
                {
                    foreach (var pluginMatch in pluginMatches)
                    {
                        string plugin = pluginMatch.Key;
                        // Try to get FormID information from the database
                        string formIdInfo = "FormID reference found in crash log";
                        var pluginIdMatch = pluginIdRegex.Match(formId);

                        if (pluginIdMatch.Success)
                        {
                            string actualFormId = pluginIdMatch.Groups[2].Value;
                            string? dbEntry = _formIdDatabaseService.GetEntry(actualFormId, plugin);

                            if (!string.IsNullOrEmpty(dbEntry))
                            {
                                formIdInfo = dbEntry;
                            }
                        }

                        issues.Add(new LogIssue
                        {
                            FileName = Path.GetFileName(parsedLog.FilePath),
                            IssueId = $"FormID_{formId}_{plugin}",
                            Title = $"FormID Found: {formId}",
                            Message = $"FormID {formId} from plugin [{plugin}] appears {count} times in the crash log.",
                            Details = formIdInfo,
                            Recommendation = "This FormID may be related to the crash. Check the referenced object in xEdit.",
                            Severity = SeverityLevel.Warning,
                            Source = "FormIDAnalysis"
                        });
                    }
                }
                else
                {
                    // FormID without plugin association
                    issues.Add(new LogIssue
                    {
                        FileName = Path.GetFileName(parsedLog.FilePath),
                        IssueId = $"FormID_{formId}_Unknown",
                        Title = $"FormID Found: {formId}",
                        Message = $"FormID {formId} appears {count} times in the crash log, but could not be associated with a plugin.",
                        Recommendation = "This FormID may be related to the crash. It might be from a missing plugin or core game file.",
                        Severity = SeverityLevel.Information,
                        Source = "FormIDAnalysis"
                    });
                }
            }
        }

        /// <summary>
        /// Scans for plugin references in the crash log
        /// </summary>
        public void ScanForPluginMatches(ParsedCrashLog parsedLog, List<LogIssue> issues)
        {
            if (parsedLog?.CallStack == null || !parsedLog.CallStack.Any())
                return;

            // Look for plugin name patterns in the stack trace
            var pluginRegex = new Regex(@"(?:\.esp|\.esm|\.esl)", RegexOptions.IgnoreCase | RegexOptions.Compiled);

            foreach (var line in parsedLog.CallStack)
            {
                if (string.IsNullOrEmpty(line))
                    continue;

                var matches = pluginRegex.Matches(line);
                foreach (Match match in matches)
                {
                    // Extract plugin name and context
                    string context = GetContextAroundMatch(line, match.Index, 50);

                    // Add to issues if this is a relevant plugin issue
                    issues.Add(new LogIssue
                    {
                        FileName = Path.GetFileName(parsedLog.FilePath),
                        IssueId = "PluginReference",
                        Title = "Plugin Reference in Crash",
                        Message = $"Plugin reference found in crash: {context}",
                        Recommendation = "This plugin may be related to the crash. Verify that it's up to date and compatible with your other mods.",
                        Severity = SeverityLevel.Warning,
                        Source = "PluginAnalysis"
                    });
                }
            }
        }

        /// <summary>
        /// Gets text context around a match position
        /// </summary>
        private string GetContextAroundMatch(string text, int position, int contextLength)
        {
            int start = Math.Max(0, position - contextLength);
            int end = Math.Min(text.Length, position + contextLength);
            return text.Substring(start, end - start);
        }

        /// <summary>
        /// Scans for named record references in the crash log
        /// </summary>
        public void ScanForNamedRecords(ParsedCrashLog parsedLog, List<LogIssue> issues)
        {
            if (parsedLog?.CallStack == null || !parsedLog.CallStack.Any())
                return;

            // Some common Bethesda engine record types
            var recordTypes = new[] { "ACTI", "NPC_", "CELL", "REFR", "ARMO", "WEAP", "MGEF", "SPEL" };

            foreach (var recordType in recordTypes)
            {
                var recordRegex = new Regex($@"\b{recordType}\b", RegexOptions.IgnoreCase | RegexOptions.Compiled);

                foreach (var line in parsedLog.CallStack)
                {
                    if (string.IsNullOrEmpty(line))
                        continue;

                    if (recordRegex.IsMatch(line))
                    {
                        issues.Add(new LogIssue
                        {
                            FileName = Path.GetFileName(parsedLog.FilePath),
                            IssueId = $"RecordType_{recordType}",
                            Title = $"{recordType} Record Reference",
                            Message = $"Found reference to {recordType} record type in crash: {line}",
                            Recommendation = $"This crash may be related to a {recordType} record. Check mods that modify these records.",
                            Severity = SeverityLevel.Information,
                            Source = "RecordTypeAnalysis"
                        });
                    }
                }
            }
        }

        /// <summary>
        /// Scans for primary error message patterns
        /// </summary>
        public void ScanForMainErrorSuspects(ParsedCrashLog parsedLog, List<LogIssue> issues)
        {
            if (parsedLog?.MainErrorSegment == null || !parsedLog.MainErrorSegment.Any())
                return;

            // Common crash patterns to look for
            var memoryPattern = new Regex(@"(memory|allocation|out of memory|heap|buffer)", RegexOptions.IgnoreCase | RegexOptions.Compiled);
            var nullRefPattern = new Regex(@"(null\s+pointer|null\s+reference|access violation)", RegexOptions.IgnoreCase | RegexOptions.Compiled);
            var graphicsPattern = new Regex(@"(directx|d3d|graphic|render|texture|mesh)", RegexOptions.IgnoreCase | RegexOptions.Compiled);

            foreach (var line in parsedLog.MainErrorSegment)
            {
                if (memoryPattern.IsMatch(line))
                {
                    issues.Add(new LogIssue
                    {
                        FileName = Path.GetFileName(parsedLog.FilePath),
                        IssueId = "MemoryError",
                        Title = "Memory-Related Crash",
                        Message = "The crash appears to be related to memory issues.",
                        Recommendation = "Check your memory settings and ENB configuration. Consider using memory optimization mods.",
                        Severity = SeverityLevel.Critical,
                        Source = "MainErrorAnalysis"
                    });
                }

                if (nullRefPattern.IsMatch(line))
                {
                    issues.Add(new LogIssue
                    {
                        FileName = Path.GetFileName(parsedLog.FilePath),
                        IssueId = "NullReference",
                        Title = "Null Reference Crash",
                        Message = "The crash appears to be a null reference or access violation.",
                        Recommendation = "This often indicates a mod conflict or missing dependency. Check your load order.",
                        Severity = SeverityLevel.Error,
                        Source = "MainErrorAnalysis"
                    });
                }

                if (graphicsPattern.IsMatch(line))
                {
                    issues.Add(new LogIssue
                    {
                        FileName = Path.GetFileName(parsedLog.FilePath),
                        IssueId = "GraphicsError",
                        Title = "Graphics-Related Crash",
                        Message = "The crash appears to be related to graphics rendering.",
                        Recommendation = "Check your graphics settings, ENB configuration, and texture mods.",
                        Severity = SeverityLevel.Error,
                        Source = "MainErrorAnalysis"
                    });
                }
            }
        }

        /// <summary>
        /// Analyzes call stack for known issue patterns
        /// </summary>
        public void ScanForCallStackSuspects(ParsedCrashLog parsedLog, List<LogIssue> issues)
        {
            if (parsedLog?.CallStack == null || !parsedLog.CallStack.Any())
                return;

            // Known problematic functions or modules
            var knownBadFunctions = new Dictionary<string, string>
            {
                { @"BSResource::Archive", "Archive loading issue - may indicate a problem with BA2 files" },
                { @"TESObjectREFR::Get3D", "Object reference 3D loading issue - often caused by mesh problems" },
                { @"BGSTextureSet::LoadForm", "Texture loading issue - may indicate corrupted or missing textures" },
                { @"BSGraphics::Renderer", "Graphics rendering issue - likely related to ENB or other graphics mods" },
                { @"Actor::Update", "Actor update issue - may be related to animation mods or NPC records" }
            };

            foreach (var line in parsedLog.CallStack)
            {
                foreach (var function in knownBadFunctions)
                {
                    if (line.Contains(function.Key))
                    {
                        issues.Add(new LogIssue
                        {
                            FileName = Path.GetFileName(parsedLog.FilePath),
                            IssueId = $"Function_{function.Key.Replace("::", "_")}",
                            Title = $"Suspect Function Call: {function.Key}",
                            Message = function.Value,
                            Recommendation = "Look for mods that might affect this functionality.",
                            Severity = SeverityLevel.Warning,
                            Source = "CallStackAnalysis"
                        });
                    }
                }
            }
        }

        /// <summary>
        /// Checks for load order issues based on crash data
        /// </summary>
        public void ScanForLoadOrderIssues(ParsedCrashLog parsedLog, List<LogIssue> issues)
        {
            if (parsedLog?.LoadedPlugins == null || !parsedLog.LoadedPlugins.Any())
                return;

            // Check for known plugin conflicts
            var knownConflictingSets = new List<string[]>
            {
                new[] { "plugin1.esp", "plugin2.esp" },
                new[] { "plugin3.esp", "plugin4.esp" }
                // Add more sets as needed
            };

            foreach (var conflictSet in knownConflictingSets)
            {
                if (conflictSet.All(plugin => parsedLog.LoadedPlugins.ContainsKey(plugin)))
                {
                    issues.Add(new LogIssue
                    {
                        FileName = Path.GetFileName(parsedLog.FilePath),
                        IssueId = "PluginConflict",
                        Title = "Plugin Conflict Detected",
                        Message = $"Conflicting plugins detected: {string.Join(", ", conflictSet)}",
                        Recommendation = "These plugins are known to conflict. Consider using a compatibility patch or disabling one of them.",
                        Severity = SeverityLevel.Error,
                        Source = "LoadOrderAnalysis"
                    });
                }
            }

            // Check for missing masters
            // This would require additional data about plugin dependencies
        }

        /// <summary>
        /// Checks Buffout4 settings that might be relevant to the crash
        /// </summary>
        public void ScanBuffoutSettings(ParsedCrashLog parsedLog, List<LogIssue> issues)
        {
            if (parsedLog?.CrashGeneratorName == null || !parsedLog.CrashGeneratorName.Contains("Buffout"))
                return;

            // This would typically check against settings stored in the YAML cache
            // For demonstration, we'll just add a generic recommendation
            issues.Add(new LogIssue
            {
                FileName = Path.GetFileName(parsedLog.FilePath),
                IssueId = "BuffoutSettings",
                Title = "Buffout4 Settings Check",
                Message = "This crash was logged by Buffout4.",
                Recommendation = "Ensure your Buffout4 settings are optimized. Consider enabling 'MaxStdio' and 'Memory Patches' in the Buffout4.toml file.",
                Severity = SeverityLevel.Information,
                Source = "BuffoutAnalysis"
            });

            // In a real implementation, we'd check the yamlSettingsCache for specific Buffout settings
            // and make recommendations based on the crash data
        }
    }
}

