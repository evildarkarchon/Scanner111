using System;
using System.Collections.Generic;
using Scanner111.Models;

namespace Scanner111.Services;

/// <summary>
///     Service for managing and retrieving warning messages and notifications.
///     Ported from the Python warning database functionality in CLASSIC.
/// </summary>
public class WarningDatabase
{
    private readonly Dictionary<string, WarningDetails> _importantCorePluginNotes =
        new(StringComparer.OrdinalIgnoreCase);

    private readonly Dictionary<string, WarningDetails> _importantFolonPluginNotes =
        new(StringComparer.OrdinalIgnoreCase);

    private readonly List<ConflictRule> _pluginConflictWarnings = new();
    private readonly Dictionary<string, WarningDetails> _singlePluginWarnings = new(StringComparer.OrdinalIgnoreCase);
    private readonly IYamlSettingsCacheService? _yamlSettingsCache;
    private bool _initialized;

    /// <summary>
    ///     Initializes a new instance of the WarningDatabase class.
    /// </summary>
    public WarningDatabase()
    {
        // Default constructor for tests and dependency injection
    }

    /// <summary>
    ///     Initializes a new instance of the WarningDatabase class with YAML settings cache.
    /// </summary>
    /// <param name="yamlSettingsCache">The YAML settings cache service.</param>
    public WarningDatabase(IYamlSettingsCacheService yamlSettingsCache)
    {
        _yamlSettingsCache = yamlSettingsCache ?? throw new ArgumentNullException(nameof(yamlSettingsCache));
        Initialize();
    }

    /// <summary>
    ///     Initializes the warning database by loading data from YAML settings.
    /// </summary>
    private void Initialize()
    {
        if (_initialized || _yamlSettingsCache == null)
            return;

        // Load single plugin warnings
        LoadSinglePluginWarnings();

        // Load plugin conflict warnings
        LoadPluginConflictWarnings();

        // Load important plugin notes
        LoadImportantPluginNotes();

        _initialized = true;
    }

    /// <summary>
    ///     Loads single plugin warnings from YAML settings.
    /// </summary>
    private void LoadSinglePluginWarnings()
    {
        if (_yamlSettingsCache == null) return;

        var warningsDict = _yamlSettingsCache.GetSetting<Dictionary<string, Dictionary<string, object>>>(
            Yaml.Game, "SinglePluginWarnings");

        if (warningsDict == null) return;

        foreach (var warningEntry in warningsDict)
        {
            var pluginName = warningEntry.Key;
            var details = warningEntry.Value;

            if (details != null)
            {
                var warningDetails = new WarningDetails
                {
                    Title = details.TryGetValue("Title", out var title)
                        ? title?.ToString() ?? string.Empty
                        : string.Empty,
                    Message = details.TryGetValue("Message", out var message)
                        ? message?.ToString() ?? string.Empty
                        : string.Empty,
                    Recommendation = details.TryGetValue("Recommendation", out var recommendation)
                        ? recommendation?.ToString() ?? string.Empty
                        : string.Empty
                };

                // Parse severity level
                if (details.TryGetValue("Severity", out var severityObj) && severityObj != null &&
                    Enum.TryParse(severityObj.ToString(), true, out SeverityLevel severity))
                    warningDetails.Severity = severity;

                _singlePluginWarnings[pluginName] = warningDetails;
            }
        }
    }

    /// <summary>
    ///     Loads plugin conflict warnings from YAML settings.
    /// </summary>
    private void LoadPluginConflictWarnings()
    {
        if (_yamlSettingsCache == null) return;

        var conflictsDict = _yamlSettingsCache.GetSetting<Dictionary<string, Dictionary<string, object>>>(
            Yaml.Game, "PluginConflicts");

        if (conflictsDict == null) return;

        foreach (var conflictEntry in conflictsDict)
        {
            var conflictKey = conflictEntry.Key;
            var details = conflictEntry.Value;

            if (details != null)
            {
                var pluginParts = conflictKey.Split("_and_", StringSplitOptions.RemoveEmptyEntries);
                if (pluginParts.Length != 2) continue;

                var conflictRule = new ConflictRule
                {
                    PluginA = pluginParts[0].Trim(),
                    PluginB = pluginParts[1].Trim(),
                    Title = details.TryGetValue("Title", out var title)
                        ? title?.ToString() ?? string.Empty
                        : string.Empty,
                    Message = details.TryGetValue("Message", out var message)
                        ? message?.ToString() ?? string.Empty
                        : string.Empty,
                    Recommendation = details.TryGetValue("Recommendation", out var recommendation)
                        ? recommendation?.ToString() ?? string.Empty
                        : string.Empty
                };

                // Parse severity level
                if (details.TryGetValue("Severity", out var severityObj) && severityObj != null &&
                    Enum.TryParse(severityObj.ToString(), true, out SeverityLevel severity))
                    conflictRule.Severity = severity;

                _pluginConflictWarnings.Add(conflictRule);
            }
        }
    }

    /// <summary>
    ///     Loads important plugin notes from YAML settings.
    /// </summary>
    private void LoadImportantPluginNotes()
    {
        if (_yamlSettingsCache == null) return;

        // Load core plugin notes
        var coreNotesDict = _yamlSettingsCache.GetSetting<Dictionary<string, Dictionary<string, object>>>(
            Yaml.Game, "ImportantCorePluginNotes");

        if (coreNotesDict != null)
            foreach (var noteEntry in coreNotesDict)
                LoadImportantPluginNote(noteEntry, _importantCorePluginNotes);

        // Load Fallout London plugin notes
        var folonNotesDict = _yamlSettingsCache.GetSetting<Dictionary<string, Dictionary<string, object>>>(
            Yaml.Game, "ImportantFolonPluginNotes");

        if (folonNotesDict != null)
            foreach (var noteEntry in folonNotesDict)
                LoadImportantPluginNote(noteEntry, _importantFolonPluginNotes);
    }

    /// <summary>
    ///     Loads an important plugin note from a key-value pair.
    /// </summary>
    /// <param name="noteEntry">The key-value pair containing the note.</param>
    /// <param name="targetDictionary">The dictionary to add the note to.</param>
    private void LoadImportantPluginNote(KeyValuePair<string, Dictionary<string, object>> noteEntry,
        Dictionary<string, WarningDetails> targetDictionary)
    {
        var pluginName = noteEntry.Key;
        var details = noteEntry.Value;

        if (details != null)
        {
            var warningDetails = new WarningDetails
            {
                Title = details.TryGetValue("Title", out var title) ? title?.ToString() ?? string.Empty : string.Empty,
                Message = details.TryGetValue("Message", out var message)
                    ? message?.ToString() ?? string.Empty
                    : string.Empty,
                Recommendation = details.TryGetValue("Recommendation", out var recommendation)
                    ? recommendation?.ToString() ?? string.Empty
                    : string.Empty
            };

            // Parse severity level
            if (details.TryGetValue("Severity", out var severityObj) && severityObj != null &&
                Enum.TryParse(severityObj.ToString(), true, out SeverityLevel severity))
                warningDetails.Severity = severity;

            targetDictionary[pluginName] = warningDetails;
        }
    }

    /// <summary>
    ///     Gets the dictionary of single plugin warnings.
    /// </summary>
    /// <returns>Dictionary of plugin names to warning details.</returns>
    public Dictionary<string, WarningDetails> GetSinglePluginWarnings()
    {
        if (!_initialized && _yamlSettingsCache != null)
            Initialize();

        return _singlePluginWarnings;
    }

    /// <summary>
    ///     Gets the list of plugin conflict warnings.
    /// </summary>
    /// <returns>List of conflict rules.</returns>
    public List<ConflictRule> GetPluginConflictWarnings()
    {
        if (!_initialized && _yamlSettingsCache != null)
            Initialize();

        return _pluginConflictWarnings;
    }

    /// <summary>
    ///     Gets the dictionary of important core plugin notes.
    /// </summary>
    /// <returns>Dictionary of plugin names to warning details.</returns>
    public Dictionary<string, WarningDetails> GetImportantCorePluginNotes()
    {
        if (!_initialized && _yamlSettingsCache != null)
            Initialize();

        return _importantCorePluginNotes;
    }

    /// <summary>
    ///     Gets the dictionary of important Fallout London plugin notes.
    /// </summary>
    /// <returns>Dictionary of plugin names to warning details.</returns>
    public Dictionary<string, WarningDetails> GetImportantFolonPluginNotes()
    {
        if (!_initialized && _yamlSettingsCache != null)
            Initialize();

        return _importantFolonPluginNotes;
    }
}