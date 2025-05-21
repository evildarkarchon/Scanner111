using System.Collections.Generic;
using System.Linq;
using Scanner111.Models;
using YamlDotNet.RepresentationModel; // Required for YAML parsing

namespace Scanner111.Services
{
    public class WarningDatabase
    {
        private readonly YamlSettingsCacheService _yamlCache;
        private Dictionary<string, WarningDetails> _singlePluginWarnings = new Dictionary<string, WarningDetails>(System.StringComparer.OrdinalIgnoreCase);
        private List<ConflictRule> _pluginConflictRules = new List<ConflictRule>();
        private Dictionary<string, WarningDetails> _importantCorePluginNotes = new Dictionary<string, WarningDetails>(System.StringComparer.OrdinalIgnoreCase);
        private Dictionary<string, WarningDetails> _importantFolonPluginNotes = new Dictionary<string, WarningDetails>(System.StringComparer.OrdinalIgnoreCase);
        private Dictionary<string, Dictionary<string, string>> _crashlogErrorCheck = new Dictionary<string, Dictionary<string, string>>(System.StringComparer.OrdinalIgnoreCase);
        private Dictionary<string, List<string>> _crashlogStackCheck = new Dictionary<string, List<string>>(System.StringComparer.OrdinalIgnoreCase);

        // Define YAML keys based on CLASSIC Fallout4.yaml structure
        private const string GameModsCoreKey = "Mods_CORE";
        private const string GameModsFrequentlyReportedKey = "Mods_FREQ";
        private const string GameModsConflictsKey = "Mods_CONF";
        private const string GameModsSolutionsKey = "Mods_SOLU";
        private const string GameModsImportantCoreKey = "Mods_CORE"; // For important core notes
        private const string GameModsImportantFolonKey = "Mods_CORE_FOLON"; // For important FOLON notes
        private const string CrashlogErrorCheckKey = "Crashlog_Error_Check";
        private const string CrashlogStackCheckKey = "Crashlog_Stack_Check";
        private const string CrashlogExcludePluginsKey = "Crashlog_Plugins_Exclude";
        private const string CrashlogExcludeRecordsKey = "Crashlog_Records_Exclude"; public WarningDatabase(YamlSettingsCacheService yamlCache)
        {
            _yamlCache = yamlCache;
            LoadWarningData();
        }

        private void LoadWarningData()
        {
            // Assuming YAML.Game is the correct enum for "CLASSIC Fallout4.yaml"
            var gameYamlNode = _yamlCache.GetYamlNode(Models.YAML.Game);
            if (gameYamlNode == null) return; // Or log an error

            // Load single plugin warnings from various sections
            LoadSingleWarningsFromSection(gameYamlNode, GameModsCoreKey);
            LoadSingleWarningsFromSection(gameYamlNode, GameModsFrequentlyReportedKey);
            LoadSingleWarningsFromSection(gameYamlNode, GameModsSolutionsKey);

            // Load conflict rules
            LoadConflictRules(gameYamlNode, GameModsConflictsKey);

            // Load important notes
            LoadGenericPluginNotes(gameYamlNode, GameModsImportantCoreKey, _importantCorePluginNotes, "Important Core Note", SeverityLevel.Information);
            LoadGenericPluginNotes(gameYamlNode, GameModsImportantFolonKey, _importantFolonPluginNotes, "Important FOLON Note", SeverityLevel.Information);

            // Load crash error and stack check patterns
            LoadCrashErrorChecks(gameYamlNode, CrashlogErrorCheckKey);
            LoadCrashStackChecks(gameYamlNode, CrashlogStackCheckKey);
        }

        private void LoadSingleWarningsFromSection(YamlNode rootNode, string sectionPath)
        {
            var sectionNode = _yamlCache.GetNodeByPath(rootNode, sectionPath);
            if (sectionNode is YamlMappingNode mappingNode)
            {
                foreach (var entry in mappingNode.Children)
                {
                    if (entry.Key is YamlScalarNode keyNode && keyNode.Value != null &&
                        entry.Value is YamlScalarNode valueNode && valueNode.Value != null)
                    {
                        string pluginName = keyNode.Value;
                        string message = valueNode.Value;
                        // Defaults, can be made more specific if YAML contains more data per warning
                        _singlePluginWarnings[pluginName] = new WarningDetails
                        {
                            Message = message,
                            Title = $"Plugin Note: {pluginName}",
                            Severity = SeverityLevel.Warning // Default, adjust if YAML has severity
                        };
                    }
                }
            }
        }

        private void LoadGenericPluginNotes(YamlNode rootNode, string sectionPath, Dictionary<string, WarningDetails> targetDictionary, string titlePrefix, SeverityLevel defaultSeverity)
        {
            var sectionNode = _yamlCache.GetNodeByPath(rootNode, sectionPath);
            if (sectionNode is YamlMappingNode mappingNode)
            {
                foreach (var entry in mappingNode.Children)
                {
                    if (entry.Key is YamlScalarNode keyNode && keyNode.Value != null &&
                        entry.Value is YamlScalarNode valueNode && valueNode.Value != null)
                    {
                        string pluginName = keyNode.Value;
                        string message = valueNode.Value;
                        targetDictionary[pluginName] = new WarningDetails
                        {
                            Message = message,
                            Title = $"{titlePrefix}: {pluginName}",
                            Severity = defaultSeverity,
                            Recommendation = "Review this note for important information."
                        };
                    }
                }
            }
        }
        private void LoadCrashErrorChecks(YamlNode rootNode, string sectionPath)
        {
            var sectionNode = _yamlCache.GetNodeByPath(rootNode, sectionPath);
            if (sectionNode is YamlMappingNode mappingNode)
            {
                foreach (var entry in mappingNode.Children)
                {
                    if (entry.Key is YamlScalarNode keyNode && keyNode.Value != null &&
                        entry.Value is YamlScalarNode valueNode && valueNode.Value != null)
                    {
                        // Parse the key which has format "5 | Stack Overflow Crash"
                        string key = keyNode.Value;
                        if (key.Contains("|"))
                        {
                            string[] parts = key.Split('|', 2);
                            if (parts.Length == 2)
                            {
                                string severityStr = parts[0].Trim();
                                string crashTitle = parts[1].Trim();

                                // Create a dictionary entry for this crash type
                                if (!_crashlogErrorCheck.ContainsKey(crashTitle))
                                {
                                    _crashlogErrorCheck[crashTitle] = new Dictionary<string, string>
                                    {
                                        ["Severity"] = severityStr,
                                        ["Pattern"] = valueNode.Value
                                    };
                                }
                            }
                        }
                    }
                }
            }
        }

        private void LoadCrashStackChecks(YamlNode rootNode, string sectionPath)
        {
            var sectionNode = _yamlCache.GetNodeByPath(rootNode, sectionPath);
            if (sectionNode is YamlMappingNode mappingNode)
            {
                foreach (var entry in mappingNode.Children)
                {
                    if (entry.Key is YamlScalarNode keyNode && keyNode.Value != null &&
                        entry.Value is YamlNode valueNode)
                    {
                        // Parse the key which has format "5 | Scaleform Gfx Crash:"
                        string key = keyNode.Value;
                        if (key.Contains("|"))
                        {
                            string[] parts = key.Split('|', 2);
                            if (parts.Length == 2)
                            {
                                string severityStr = parts[0].Trim();
                                string crashTitle = parts[1].Trim();

                                // Get the patterns from the value
                                List<string> patterns = new List<string>();

                                // If it's a sequence node (list)
                                if (valueNode is YamlSequenceNode sequenceNode)
                                {
                                    foreach (var patternNode in sequenceNode)
                                    {
                                        if (patternNode is YamlScalarNode scalarPatternNode && scalarPatternNode.Value != null)
                                        {
                                            patterns.Add(scalarPatternNode.Value);
                                        }
                                    }
                                }
                                // If it's a scalar node (single value)
                                else if (valueNode is YamlScalarNode scalarNode && scalarNode.Value != null)
                                {
                                    patterns.Add(scalarNode.Value);
                                }

                                // Store this crash type and its patterns
                                _crashlogStackCheck[crashTitle] = patterns;
                            }
                        }
                    }
                }
            }
        }

        private void LoadConflictRules(YamlNode rootNode, string sectionPath)
        {
            var sectionNode = _yamlCache.GetNodeByPath(rootNode, sectionPath);
            if (sectionNode is YamlMappingNode mappingNode)
            {
                foreach (var entry in mappingNode.Children)
                {
                    if (entry.Key is YamlScalarNode keyNode && keyNode.Value != null &&
                        entry.Value is YamlScalarNode valueNode && valueNode.Value != null)
                    {
                        string[] plugins = keyNode.Value.Split(new[] { " | " }, System.StringSplitOptions.RemoveEmptyEntries);
                        if (plugins.Length == 2)
                        {
                            _pluginConflictRules.Add(new ConflictRule
                            {
                                PluginA = plugins[0].Trim(),
                                PluginB = plugins[1].Trim(),
                                Message = valueNode.Value,
                                Title = $"Conflict: {plugins[0].Trim()} & {plugins[1].Trim()}",
                                Severity = SeverityLevel.Warning // Default, adjust as needed
                            });
                        }
                    }
                }
            }
        }        // Public getters for accessing the database entries
        public Dictionary<string, WarningDetails> GetSinglePluginWarnings() => _singlePluginWarnings;
        public List<ConflictRule> GetPluginConflictWarnings() => _pluginConflictRules;
        public Dictionary<string, WarningDetails> GetImportantCorePluginNotes() => _importantCorePluginNotes;
        public Dictionary<string, WarningDetails> GetImportantFolonPluginNotes() => _importantFolonPluginNotes;
        public Dictionary<string, Dictionary<string, string>> GetCrashlogErrorChecks() => _crashlogErrorCheck;
        public Dictionary<string, List<string>> GetCrashlogStackChecks() => _crashlogStackCheck;

        /// <summary>
        /// Gets crash error check pattern and severity by title
        /// </summary>
        public (string Pattern, int Severity) GetCrashErrorCheck(string crashTitle)
        {
            if (_crashlogErrorCheck.TryGetValue(crashTitle, out var check) &&
                check.TryGetValue("Pattern", out var pattern) &&
                check.TryGetValue("Severity", out var severityStr) &&
                int.TryParse(severityStr, out var severity))
            {
                return (pattern, severity);
            }

            return (string.Empty, 0);
        }

        /// <summary>
        /// Gets crash stack check patterns by title
        /// </summary>
        public (List<string> Patterns, int Severity) GetCrashStackCheck(string crashTitle)
        {
            // Extract the severity from the title (format: "5 | Title")
            int severity = 0;
            if (crashTitle.Contains("|"))
            {
                string[] parts = crashTitle.Split('|', 2);
                if (parts.Length == 2 && int.TryParse(parts[0].Trim(), out var parsedSeverity))
                {
                    severity = parsedSeverity;
                }
            }

            if (_crashlogStackCheck.TryGetValue(crashTitle, out var patterns))
            {
                return (patterns, severity);
            }

            return (new List<string>(), 0);
        }
    }
}