using Scanner111.Core.Infrastructure;
using Scanner111.Core.Models;
using Scanner111.Core.Models.Yaml;

namespace Scanner111.Core.Analyzers;

/// <summary>
/// Represents an analyzer that scans and extracts named records from crash logs.
/// </summary>
/// <remarks>
/// This class provides functionality to identify and process named record patterns within the call stack data of crash logs.
/// It utilizes YAML-based settings for configuration and is designed to be a direct implementation of a Python record scanner.
/// </remarks>
/// <seealso cref="Scanner111.Core.Analyzers.IAnalyzer"/>
public class RecordScanner : IAnalyzer
{
    private readonly IYamlSettingsProvider _yamlSettings;

    /// <summary>
    ///     Initialize the record scanner
    /// </summary>
    /// <param name="yamlSettings">YAML settings provider for configuration</param>
    public RecordScanner(IYamlSettingsProvider yamlSettings)
    {
        _yamlSettings = yamlSettings;
    }

    /// <summary>
    ///     Name of the analyzer
    /// </summary>
    public string Name => "Record Scanner";

    /// <summary>
    ///     Priority of the analyzer (lower values run first)
    /// </summary>
    public int Priority => 30;

    /// <summary>
    ///     Whether this analyzer can be run in parallel with others
    /// </summary>
    public bool CanRunInParallel => true;

    /// <summary>
    /// Analyze a crash log for named records.
    /// </summary>
    /// <param name="crashLog">The crash log to analyze.</param>
    /// <param name="cancellationToken">The cancellation token to observe.</param>
    /// <returns>A task that represents the asynchronous operation, containing the record analysis result.</returns>
    public async Task<AnalysisResult> AnalyzeAsync(CrashLog crashLog, CancellationToken cancellationToken = default)
    {
        await Task.CompletedTask; // Make it async-ready

        var reportLines = new List<string>();
        var recordsMatches = new List<string>();

        // Extract records from the call stack
        var extractedRecords = ExtractRecords(crashLog.CallStack);

        // Scan for named records
        ScanNamedRecords(crashLog.CallStack, recordsMatches, reportLines);

        return new GenericAnalysisResult
        {
            AnalyzerName = Name,
            ReportLines = reportLines,
            HasFindings = recordsMatches.Count > 0,
            Data = new Dictionary<string, object>
            {
                { "RecordsMatches", recordsMatches },
                { "ExtractedRecords", extractedRecords }
            }
        };
    }

    /// <summary>
    /// Scans named records in the provided segment callstack, identifies matches, and updates the autoscan report
    /// accordingly.
    /// </summary>
    /// <param name="segmentCallstack">The callstack to scan for named records</param>
    /// <param name="recordsMatches">A list to hold records that match the scan criteria</param>
    /// <param name="autoscanReport">The report to be updated based on scanning results</param>
    private void ScanNamedRecords(List<string> segmentCallstack, List<string> recordsMatches,
        List<string> autoscanReport)
    {
        // Constants
        const string rspMarker = "[RSP+";
        const int rspOffset = 30;

        // Find matching records
        FindMatchingRecords(segmentCallstack, recordsMatches, rspMarker, rspOffset);

        // Report results
        if (recordsMatches.Count > 0)
            ReportFoundRecords(recordsMatches, autoscanReport);
        else
            autoscanReport.Add("* COULDN'T FIND ANY NAMED RECORDS *\n\n");
    }

    /// <summary>
    /// Finds and collects matching records from a given segment of a call stack based on specified criteria.
    /// </summary>
    /// <param name="segmentCallstack">A list of strings representing the segment of the call stack to analyze</param>
    /// <param name="recordsMatches">A list where matching record lines will be added</param>
    /// <param name="rspMarker">A string representing the marker used to locate relevant portions of each call stack line</param>
    /// <param name="rspOffset">An integer specifying the character offset from the rspMarker to extract record content</param>
    private void FindMatchingRecords(List<string> segmentCallstack, List<string> recordsMatches, string rspMarker,
        int rspOffset)
    {
        var mainYaml = _yamlSettings.LoadYaml<ClassicMainYaml>("CLASSIC Main");
        var recordsList = mainYaml?.CatchLogRecords ?? [];
        var lowerRecords = recordsList.Select(r => r.ToLower()).ToHashSet();

        var fallout4Yaml = _yamlSettings.LoadYaml<ClassicFallout4Yaml>("CLASSIC Fallout4");
        var ignoredList = fallout4Yaml?.CrashlogRecordsExclude ?? [];
        var lowerIgnore = ignoredList.Select(r => r.ToLower()).ToHashSet();

        foreach (var line in from line in segmentCallstack
                 let lowerLine = line.ToLower()
                 where lowerRecords.Any(lowerLine.Contains) &&
                       !lowerIgnore.Any(lowerLine.Contains) select line)
        {
            // Extract the relevant part of the line based on format
            if (line.Contains(rspMarker))
            {
                if (line.Length > rspOffset) recordsMatches.Add(line.Substring(rspOffset).Trim());
            }
            else
            {
                recordsMatches.Add(line.Trim());
            }
        }
    }

    /// <summary>
    /// Reports the found named records by summarizing their occurrences and adding explanatory notes.
    /// </summary>
    /// <param name="recordsMatches">A list containing the matched named records extracted from a crash log.</param>
    /// <param name="autoscanReport">A list that will be populated with the summarized report of named records.</param>
    private void ReportFoundRecords(List<string> recordsMatches, List<string> autoscanReport)
    {
        // Count and sort the records
        var recordsFound = recordsMatches
            .GroupBy(record => record)
            .OrderBy(g => g.Key)
            .ToDictionary(g => g.Key, g => g.Count());

        // Add each record with its count
        foreach (var (record, count) in recordsFound) autoscanReport.Add($"- {record} | {count}\n");

        // Add explanatory notes
        var fallout4Yaml = _yamlSettings.LoadYaml<ClassicFallout4Yaml>("CLASSIC Fallout4");
        var crashgenLogName = fallout4Yaml?.GameInfo?.CrashgenLogName ?? "Crash Logger";
        
        var explanatoryNotes = new[]
        {
            "\n[Last number counts how many times each Named Record shows up in the crash log.]\n",
            $"These records were caught by {crashgenLogName} and some of them might be related to this crash.\n",
            "Named records should give extra info on involved game objects, record types or mod files.\n\n"
        };
        autoscanReport.AddRange(explanatoryNotes);
    }

    /// <summary>
    /// Extracts records from a segment callstack based on specific matching criteria.
    /// </summary>
    /// <param name="segmentCallstack">The list of strings representing the segment callstack to be analyzed.</param>
    /// <returns>A list of strings containing records identified from the provided segment callstack.</returns>
    private List<string> ExtractRecords(List<string> segmentCallstack)
    {
        var recordsMatches = new List<string>();

        // Constants
        const string rspMarker = "[RSP+";
        const int rspOffset = 30;

        FindMatchingRecords(segmentCallstack, recordsMatches, rspMarker, rspOffset);

        return recordsMatches;
    }
}