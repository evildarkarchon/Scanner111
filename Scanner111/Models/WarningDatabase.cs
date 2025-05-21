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

        // Define YAML keys based on CLASSIC Fallout4.yaml structure
        private const string GameModsCoreKey = "Game_Mods.Core";
        private const string GameModsFrequentlyReportedKey = "Game_Mods.Frequently_Reported";
        private const string GameModsSolutionsKey = "Game_Mods.Solutions";
        private const string GameModsConflictsKey = "Game_Mods.Conflicts";
        private const string GameModsImportantCoreKey = "Game_Mods.CORE"; // Specific for important core notes
        private const string GameModsImportantFolonKey = "Game_Mods.CORE_FOLON"; // Specific for important FOLON notes

        public WarningDatabase(YamlSettingsCacheService yamlCache)
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
            // Add calls for other sections like "Game_Mods.Outdated_Core" if they follow the same structure

            // Load conflict rules
            LoadConflictRules(gameYamlNode, GameModsConflictsKey);

            // Load important notes
            LoadGenericPluginNotes(gameYamlNode, GameModsImportantCoreKey, _importantCorePluginNotes, "Important Core Note", SeverityLevel.Information);
            LoadGenericPluginNotes(gameYamlNode, GameModsImportantFolonKey, _importantFolonPluginNotes, "Important FOLON Note", SeverityLevel.Information);
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
        }

        public Dictionary<string, WarningDetails> GetSinglePluginWarnings()
        {
            return _singlePluginWarnings;
        }

        public List<ConflictRule> GetPluginConflictWarnings()
        {
            return _pluginConflictRules;
        }

        public Dictionary<string, WarningDetails> GetImportantCorePluginNotes()
        {
            return _importantCorePluginNotes;
        }

        public Dictionary<string, WarningDetails> GetImportantFolonPluginNotes()
        {
            return _importantFolonPluginNotes;
        }

        // TODO: Implement method for "Important Notes" (detect_mods_important)
        // This might involve a different YAML structure or specific keys.
    }
}