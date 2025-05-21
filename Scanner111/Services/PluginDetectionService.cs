using Scanner111.Models;
using System;
using System.Collections.Generic;
using System.IO;

namespace Scanner111.Services
{
    /// <summary>
    /// Service responsible for detecting issues related to plugins
    /// </summary>
    public class PluginDetectionService
    {
        private readonly WarningDatabase _warningDatabase;

        private const string FalloutLondonPluginName = "LondonWorldspace.esm"; // Fallout London plugin name

        public PluginDetectionService(WarningDatabase warningDatabase)
        {
            _warningDatabase = warningDatabase;
        }

        /// <summary>
        /// Main method that coordinates detection of all plugin-related issues
        /// </summary>
        public void DetectModIssues(ParsedCrashLog parsedLog, List<LogIssue> issues)
        {
            if (_warningDatabase == null) return; // Or throw, or log warning

            DetectSinglePluginIssues(parsedLog, issues);
            DetectPluginConflictIssues(parsedLog, issues);
            DetectImportantPluginNotes(parsedLog, issues);
        }

        /// <summary>
        /// Detects issues with individual plugins
        /// </summary>
        private void DetectSinglePluginIssues(ParsedCrashLog parsedLog, List<LogIssue> issues)
        {
            // Assuming _warningDatabase has a way to get single plugin warnings
            // e.g., _warningDatabase.GetSinglePluginWarnings() returns Dictionary<string, WarningDetails>
            // where WarningDetails includes Message, Recommendation, Severity

            var singlePluginRules = _warningDatabase.GetSinglePluginWarnings(); // Hypothetical method
            if (singlePluginRules == null) return;

            foreach (var loadedPlugin in parsedLog.LoadedPlugins.Keys)
            {
                if (singlePluginRules.TryGetValue(loadedPlugin, out var warningDetails))
                {
                    issues.Add(new LogIssue
                    {
                        FileName = Path.GetFileName(parsedLog.FilePath),
                        IssueId = $"SinglePluginIssue_{loadedPlugin.Replace(".", "_")}",
                        Title = warningDetails.Title ?? $"Plugin Alert: {loadedPlugin}",
                        Message = warningDetails.Message,
                        Recommendation = warningDetails.Recommendation ?? string.Empty, // Added null check
                        Severity = warningDetails.Severity,
                        Source = "PluginScan"
                    });
                }
            }
        }

        /// <summary>
        /// Detects conflicts between plugins
        /// </summary>
        private void DetectPluginConflictIssues(ParsedCrashLog parsedLog, List<LogIssue> issues)
        {
            // Assuming _warningDatabase has a way to get plugin conflict warnings
            // e.g., _warningDatabase.GetPluginConflictWarnings() returns List<ConflictRule>
            // where ConflictRule has PluginA, PluginB, Message, Recommendation, Severity

            var conflictRules = _warningDatabase.GetPluginConflictWarnings(); // Hypothetical method
            if (conflictRules == null) return;

            var loadedPluginNames = new HashSet<string>(parsedLog.LoadedPlugins.Keys, StringComparer.OrdinalIgnoreCase);

            foreach (var rule in conflictRules)
            {
                if (loadedPluginNames.Contains(rule.PluginA) && loadedPluginNames.Contains(rule.PluginB))
                {
                    issues.Add(new LogIssue
                    {
                        FileName = Path.GetFileName(parsedLog.FilePath),
                        IssueId = $"PluginConflict_{rule.PluginA.Replace(".", "_")}_{rule.PluginB.Replace(".", "_")}",
                        Title = rule.Title ?? $"Plugin Conflict: {rule.PluginA} & {rule.PluginB}",
                        Message = rule.Message,
                        Recommendation = rule.Recommendation ?? string.Empty, // Added null check
                        Severity = rule.Severity,
                        Source = "PluginConflictScan"
                    });
                }
            }
        }

        /// <summary>
        /// Detects important notes for loaded plugins
        /// </summary>
        private void DetectImportantPluginNotes(ParsedCrashLog parsedLog, List<LogIssue> issues)
        {
            if (_warningDatabase == null) return;

            var coreNotes = _warningDatabase.GetImportantCorePluginNotes();
            if (coreNotes != null)
            {
                foreach (var loadedPlugin in parsedLog.LoadedPlugins.Keys)
                {
                    if (coreNotes.TryGetValue(loadedPlugin, out var noteDetails))
                    {
                        issues.Add(new LogIssue
                        {
                            FileName = Path.GetFileName(parsedLog.FilePath),
                            IssueId = $"ImportantCoreNote_{loadedPlugin.Replace(".", "_")}",
                            Title = noteDetails.Title ?? $"Important Note: {loadedPlugin}",
                            Message = noteDetails.Message,
                            Recommendation = noteDetails.Recommendation ?? string.Empty, // Added null check
                            Severity = noteDetails.Severity,
                            Source = "ImportantPluginNotes"
                        });
                    }
                }
            }

            // Check if Fallout London is active
            bool isFolonActive = parsedLog.LoadedPlugins.ContainsKey(FalloutLondonPluginName);

            if (isFolonActive)
            {
                var folonNotes = _warningDatabase.GetImportantFolonPluginNotes();
                if (folonNotes != null)
                {
                    foreach (var loadedPlugin in parsedLog.LoadedPlugins.Keys)
                    {
                        if (folonNotes.TryGetValue(loadedPlugin, out var noteDetails))
                        {
                            // Optional: Could add logic here to replace or supplement a core note if one already exists for the same plugin.
                            // For now, it will add it as a separate issue.
                            issues.Add(new LogIssue
                            {
                                FileName = Path.GetFileName(parsedLog.FilePath),
                                IssueId = $"ImportantFolonNote_{loadedPlugin.Replace(".", "_")}",
                                Title = noteDetails.Title ?? $"FOLON Note: {loadedPlugin}",
                                Message = noteDetails.Message,
                                Recommendation = noteDetails.Recommendation ?? string.Empty, // Added null check
                                Severity = noteDetails.Severity,
                                Source = "ImportantPluginNotes (FOLON)"
                            });
                        }
                    }
                }
            }
        }
    }
}
