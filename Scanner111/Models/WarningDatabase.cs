using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Scanner111.Services;

// Required for YAML parsing

namespace Scanner111.Models;

/// <summary>
///     Database for managing plugin warnings, conflicts, and crash detection patterns
/// </summary>
public class WarningDatabase
{
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
    private const string CrashlogExcludeRecordsKey = "Crashlog_Records_Exclude";

    private readonly Dictionary<string, Dictionary<string, string>> _crashlogErrorCheck =
        new(StringComparer.OrdinalIgnoreCase);

    private readonly Dictionary<string, List<string>> _crashlogStackCheck = new(StringComparer.OrdinalIgnoreCase);

    private readonly Dictionary<string, WarningDetails> _importantCorePluginNotes =
        new(StringComparer.OrdinalIgnoreCase);

    private readonly Dictionary<string, WarningDetails> _importantFolonPluginNotes =
        new(StringComparer.OrdinalIgnoreCase);

    private readonly List<ConflictRule> _pluginConflictRules = [];
    private readonly Dictionary<string, WarningDetails> _singlePluginWarnings = new(StringComparer.OrdinalIgnoreCase);
    private readonly IYamlSettingsCacheService _yamlCache;

    /// <summary>
    ///     Creates a new instance of the WarningDatabase class
    /// </summary>
    /// <param name="yamlCache">YAML settings cache service for accessing warning definitions</param>
    public WarningDatabase(IYamlSettingsCacheService yamlCache)
    {
        _yamlCache = yamlCache;
        LoadWarningData();
    }

    private void LoadWarningData()
    {
        // Skip loading if YAML cache is null (parameterless constructor case)
        if (_yamlCache == null) return;

        // Perform data loading logic with compatible methods
        LoadSingleWarningsFromSection(GameModsCoreKey);
        LoadSingleWarningsFromSection(GameModsFrequentlyReportedKey);
        LoadSingleWarningsFromSection(GameModsSolutionsKey);

        // Load conflict rules
        LoadConflictRules(GameModsConflictsKey);

        // Load important notes
        LoadGenericPluginNotes(GameModsImportantCoreKey, _importantCorePluginNotes, "Important Core Note",
            SeverityLevel.Information);
        LoadGenericPluginNotes(GameModsImportantFolonKey, _importantFolonPluginNotes, "Important FOLON Note",
            SeverityLevel.Information);

        // Load crash error and stack check patterns
        LoadCrashErrorChecks(CrashlogErrorCheckKey);
        LoadCrashStackChecks(CrashlogStackCheckKey);
    }

    // Helper method to safely get mapping values from YAML as dictionary
    private Dictionary<string, string> GetYamlSectionAsDictionary(string sectionPath)
    {
        var result = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        // Return empty dictionary if YAML cache is null
        if (_yamlCache == null) return result;

        try
        {
            // Get all key-value pairs for this section
            var sectionDict = _yamlCache.GetSetting<Dictionary<object, object>>(Yaml.Game, sectionPath, null);
            if (sectionDict != null)
                foreach (var entry in sectionDict)
                    if (entry.Key != null && entry.Value != null)
                        result[entry.Key.ToString()] = entry.Value.ToString();
        }
        catch
        {
            // Fail gracefully if section doesn't exist or has wrong format
        }

        return result;
    }

    private void LoadSingleWarningsFromSection(string sectionPath)
    {
        var sectionDict = GetYamlSectionAsDictionary(sectionPath);

        foreach (var entry in sectionDict)
        {
            var pluginName = entry.Key;
            var message = entry.Value;
            // Defaults, can be made more specific if YAML contains more data per warning
            _singlePluginWarnings[pluginName] = new WarningDetails
            {
                Message = message,
                Title = $"Plugin Note: {pluginName}",
                Severity = SeverityLevel.Warning // Default, adjust if YAML has severity
            };
        }
    }

    private void LoadGenericPluginNotes(string sectionPath, Dictionary<string, WarningDetails> targetDictionary,
        string titlePrefix, SeverityLevel defaultSeverity)
    {
        var sectionDict = GetYamlSectionAsDictionary(sectionPath);

        foreach (var entry in sectionDict)
        {
            var pluginName = entry.Key;
            var message = entry.Value;
            targetDictionary[pluginName] = new WarningDetails
            {
                Message = message,
                Title = $"{titlePrefix}: {pluginName}",
                Severity = defaultSeverity,
                Recommendation = "Review this note for important information."
            };
        }
    }

    private void LoadCrashErrorChecks(string sectionPath)
    {
        var sectionDict = GetYamlSectionAsDictionary(sectionPath);

        foreach (var (key, value) in sectionDict)
            // Parse the key which has format "5 | Stack Overflow Crash"
            if (key.Contains("|"))
            {
                var parts = key.Split('|', 2);
                if (parts.Length == 2)
                {
                    var severityStr = parts[0].Trim();
                    var crashTitle = parts[1].Trim();

                    // Create a dictionary entry for this crash type
                    if (!_crashlogErrorCheck.ContainsKey(crashTitle))
                        _crashlogErrorCheck[crashTitle] = new Dictionary<string, string>
                        {
                            ["Severity"] = severityStr,
                            ["Pattern"] = value
                        };
                }
            }
    }

    private void LoadCrashStackChecks(string sectionPath)
    {
        var sectionDict = GetYamlSectionAsDictionary(sectionPath);

        foreach (var (key, value) in sectionDict)
            // Parse the key which has format "5 | Scaleform Gfx Crash:"
            if (key.Contains("|"))
            {
                var parts = key.Split('|', 2);
                if (parts.Length == 2)
                {
                    var severityStr = parts[0].Trim();
                    var crashTitle = parts[1].Trim();

                    // Get the patterns from the value
                    List<string> patterns = [];

                    // For string values, just add directly
                    if (value is string stringValue)
                        patterns.Add(stringValue);
                    // For collection types (if available from YAML parsing)
                    else if (value is IEnumerable collection && !(value is string))
                        patterns.AddRange(collection.OfType<object>().Select(item => item.ToString())!);

                    // Store this crash type and its patterns
                    _crashlogStackCheck[crashTitle] = patterns;
                }
            }
    }

    private void LoadConflictRules(string sectionPath)
    {
        var sectionDict = GetYamlSectionAsDictionary(sectionPath);

        foreach (var entry in sectionDict)
        {
            var plugins = entry.Key.Split([" | "], StringSplitOptions.RemoveEmptyEntries);
            if (plugins.Length == 2)
                _pluginConflictRules.Add(new ConflictRule
                {
                    PluginA = plugins[0].Trim(),
                    PluginB = plugins[1].Trim(),
                    Message = entry.Value,
                    Title = $"Conflict: {plugins[0].Trim()} & {plugins[1].Trim()}",
                    Severity = SeverityLevel.Warning // Default, adjust as needed
                });
        }
    }

    /// <summary>
    ///     Gets all single plugin warnings
    /// </summary>
    public Dictionary<string, WarningDetails> GetSinglePluginWarnings()
    {
        return _singlePluginWarnings;
    }

    /// <summary>
    ///     Gets all plugin conflict warnings
    /// </summary>
    public List<ConflictRule> GetPluginConflictWarnings()
    {
        return _pluginConflictRules;
    }

    /// <summary>
    ///     Gets all important core plugin notes
    /// </summary>
    public Dictionary<string, WarningDetails> GetImportantCorePluginNotes()
    {
        return _importantCorePluginNotes;
    }

    /// <summary>
    ///     Gets all important FOLON plugin notes
    /// </summary>
    public Dictionary<string, WarningDetails> GetImportantFolonPluginNotes()
    {
        return _importantFolonPluginNotes;
    }

    /// <summary>
    ///     Gets all crashlog error check patterns
    /// </summary>
    public Dictionary<string, Dictionary<string, string>> GetCrashlogErrorChecks()
    {
        return _crashlogErrorCheck;
    }

    /// <summary>
    ///     Gets all crashlog stack check patterns
    /// </summary>
    public Dictionary<string, List<string>> GetCrashlogStackChecks()
    {
        return _crashlogStackCheck;
    }

    /// <summary>
    ///     Gets crash error check pattern and severity by title
    /// </summary>
    public (string Pattern, int Severity) GetCrashErrorCheck(string crashTitle)
    {
        if (_crashlogErrorCheck.TryGetValue(crashTitle, out var check) &&
            check.TryGetValue("Pattern", out var pattern) &&
            check.TryGetValue("Severity", out var severityStr) &&
            int.TryParse(severityStr, out var severity))
            return (pattern, severity);

        return (string.Empty, 0);
    }

    /// <summary>
    ///     Gets crash stack check patterns by title
    /// </summary>
    public (List<string> Patterns, int Severity) GetCrashStackCheck(string crashTitle)
    {
        // Extract the severity from the title (format: "5 | Title")
        var severity = 0;
        if (crashTitle.Contains("|"))
        {
            var parts = crashTitle.Split('|', 2);
            if (parts.Length == 2 && int.TryParse(parts[0].Trim(), out var parsedSeverity)) severity = parsedSeverity;
        }

        return _crashlogStackCheck.TryGetValue(crashTitle, out var patterns)
            ? (patterns, severity)
            : (new List<string>(), 0);
    }
}