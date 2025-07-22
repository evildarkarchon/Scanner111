using System.Collections.Concurrent;
using System.Text.RegularExpressions;
using Scanner111.Core.Infrastructure;
using Scanner111.Core.Models;

namespace Scanner111.Core.Analyzers;

/// <summary>
/// Provides functionality for analyzing and extracting information from FormID entries
/// in crash logs using a specified pattern and configuration settings.
/// </summary>
public class FormIdAnalyzer : IAnalyzer
{
    private static readonly ConcurrentDictionary<string, Regex> PatternCache = new();
    private readonly IFormIdDatabaseService _formIdDatabase;
    private readonly Regex _formIdPattern;
    private readonly IYamlSettingsProvider _yamlSettings;

    /// <summary>
    ///     Initialize the FormID analyzer
    /// </summary>
    /// <param name="yamlSettings">YAML settings provider for configuration</param>
    /// <param name="formIdDatabase">FormID database service for lookups</param>
    public FormIdAnalyzer(IYamlSettingsProvider yamlSettings, IFormIdDatabaseService formIdDatabase)
    {
        _yamlSettings = yamlSettings;
        _formIdDatabase = formIdDatabase;

        // Pattern to match FormID format in crash logs (cached)
        const string patternKey = "formid_pattern";
        _formIdPattern = PatternCache.GetOrAdd(patternKey, _ => new Regex(
            @"^\s*Form ID:\s*0x([0-9A-F]{8})",
            RegexOptions.IgnoreCase | RegexOptions.Compiled
        ));
    }

    /// <summary>
    ///     Name of the analyzer
    /// </summary>
    public string Name => "FormID Analyzer";

    /// <summary>
    ///     Priority of the analyzer (lower values run first)
    /// </summary>
    public int Priority => 10;

    /// <summary>
    ///     Whether this analyzer can be run in parallel with others
    /// </summary>
    public bool CanRunInParallel => true;

    /// <summary>
    ///     Analyze a crash log for FormID information
    /// </summary>
    /// <param name="crashLog">Crash log to analyze</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>FormID analysis result</returns>
    public async Task<AnalysisResult> AnalyzeAsync(CrashLog crashLog, CancellationToken cancellationToken = default)
    {
        await Task.CompletedTask; // Make it async-ready

        var formIds = ExtractFormIds(crashLog.CallStack);
        var reportLines = new List<string>();

        // Load settings on-demand with caching
        var showFormIdValues = _yamlSettings.GetSetting("CLASSIC Main", "CLASSIC_Settings.Show FormID Values", false);
        var formIdDbExists = _formIdDatabase.DatabaseExists;

        GenerateFormIdReport(formIds, crashLog.Plugins, reportLines, showFormIdValues, formIdDbExists);

        return new FormIdAnalysisResult
        {
            AnalyzerName = Name,
            FormIds = formIds,
            ReportLines = reportLines,
            HasFindings = formIds.Count > 0
        };
    }

    /// <summary>
    ///     Extracts Form IDs from a given call stack.
    ///     Direct port of Python extract_formids method.
    /// </summary>
    /// <param name="segmentCallstack">A list of strings representing the call stack to be processed</param>
    /// <returns>A list containing all extracted and formatted Form IDs that meet the criteria</returns>
    private List<string> ExtractFormIds(List<string> segmentCallstack)
    {
        var formIdsMatches = new List<string>();

        if (segmentCallstack.Count == 0) return formIdsMatches;

        foreach (var line in segmentCallstack)
        {
            var match = _formIdPattern.Match(line);
            if (match.Success)
            {
                var formIdId = match.Groups[1].Value.ToUpper(); // Get the hex part without 0x
                // Skip if it starts with FF (plugin limit)
                if (!formIdId.StartsWith("FF")) formIdsMatches.Add($"Form ID: {formIdId}");
            }
        }

        return formIdsMatches;
    }

    /// <summary>
    ///     Processes and appends reports based on Form ID matches retrieved from crash logs and a scan report.
    ///     Direct port of Python formid_match method.
    /// </summary>
    /// <param name="formIdsMatches">A list of Form ID matches extracted from the crash log</param>
    /// <param name="crashlogPlugins">A dictionary mapping plugin filenames to plugin IDs found in the crash log</param>
    /// <param name="autoscanReport">A mutable list to which the generated or default report will be appended</param>
    /// <param name="showFormIdValues">Whether to show FormID values</param>
    /// <param name="formIdDbExists">Whether FormID database exists</param>
    private void GenerateFormIdReport(List<string> formIdsMatches, Dictionary<string, string> crashlogPlugins,
        List<string> autoscanReport, bool showFormIdValues, bool formIdDbExists)
    {
        if (formIdsMatches.Count > 0)
        {
            // Count occurrences of each FormID
            var formIdsFound = formIdsMatches
                .GroupBy(f => f)
                .OrderBy(g => g.Key)
                .ToDictionary(g => g.Key, g => g.Count());

            foreach (var (formIdFull, count) in formIdsFound)
            {
                var formIdSplit = formIdFull.Split(": ", 2);
                if (formIdSplit.Length < 2) continue;

                foreach (var (plugin, pluginId) in crashlogPlugins)
                {
                    if (pluginId != formIdSplit[1][..2]) continue;

                    if (showFormIdValues && formIdDbExists)
                    {
                        var report = LookupFormIdValue(formIdSplit[1][2..], plugin);
                        if (!string.IsNullOrEmpty(report))
                        {
                            autoscanReport.Add($"- {formIdFull} | [{plugin}] | {report} | {count}\n");
                            goto NextFormId;
                        }
                    }

                    autoscanReport.Add($"- {formIdFull} | [{plugin}] | {count}\n");
                    break;
                }

                NextFormId: ;
            }

            autoscanReport.AddRange([
              "\n[Last number counts how many times each Form ID shows up in the crash log.]\n",
                $"These Form IDs were caught by {_yamlSettings.GetSetting("CLASSIC Fallout4", "Game_Info.CRASHGEN_LogName", "Crash Logger")} and some of them might be related to this crash.\n",
                "You can try searching any listed Form IDs in xEdit and see if they lead to relevant records.\n\n"
            ]);
        }
        else
        {
            autoscanReport.Add("* COULDN'T FIND ANY FORM ID SUSPECTS *\n\n");
        }
    }

    /// <summary>
    ///     Look up the value associated with a given form ID and plugin in the database.
    ///     Direct port of Python lookup_formid_value method.
    /// </summary>
    /// <param name="formId">A string representing the form ID to look up</param>
    /// <param name="plugin">A string representing the plugin name associated with the form ID</param>
    /// <returns>
    ///     A string containing the value associated with the form ID and plugin if found in the database, or null if the
    ///     database does not exist or the value is not found
    /// </returns>
    private string? LookupFormIdValue(string formId, string plugin)
    {
        return _formIdDatabase.GetEntry(formId, plugin);
    }
}