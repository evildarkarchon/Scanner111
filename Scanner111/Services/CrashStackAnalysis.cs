using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using Scanner111.Models;

namespace Scanner111.Services;

/// <summary>
///     Service for crash stack parsing and analysis ported from the Python CLASSIC_ScanLogs implementation
/// </summary>
public partial class CrashStackAnalysis
{
    private readonly AppSettings _appSettings;
    private readonly IYamlSettingsCacheService _yamlSettingsCacheService;

    // Parameterless constructor for testing with Moq
    public CrashStackAnalysis()
    {
        _appSettings = new AppSettings();
        _yamlSettingsCacheService = null!;
    }

    public CrashStackAnalysis(AppSettings appSettings, IYamlSettingsCacheService yamlSettingsCacheService)
    {
        _appSettings = appSettings;
        _yamlSettingsCacheService = yamlSettingsCacheService;
    }

    /// <summary>
    ///     Finds and extracts segments from crash data text and extracts metadata including game version, crash
    ///     generator version, and main error message. Processes the segments for whitespace trimming and ensures
    ///     completeness by adding placeholders for missing segments.
    /// </summary>
    /// <param name="crashData">List of strings representing lines of the crash data</param>
    /// <param name="crashgenName">Name of the crash generator to identify in the crash data</param>
    /// <returns>A tuple containing game version, crash generator version, main error message, and processed segments</returns>
    public (string GameVersion, string CrashgenVersion, string MainError, List<List<string>> Segments) FindSegments(
        List<string> crashData, string? crashgenName)
    {
        const string unknown = "UNKNOWN";
        const string eofMarker = "EOF";

        // Get required information from configuration
        var xse = _appSettings.CrashXseAcronym?.ToUpper() ?? "F4SE";

        // Define segment boundaries
        var segmentBoundaries = new List<(string start, string end)>
        {
            ("[Compatibility]", "SYSTEM SPECS:"), // segment_crashgen
            ("SYSTEM SPECS:", "PROBABLE CALL STACK:"), // segment_system
            ("PROBABLE CALL STACK:", "MODULES:"), // segment_callstack
            ("MODULES:", $"{xse} PLUGINS:"), // segment_allmodules
            ($"{xse} PLUGINS:", "PLUGINS:"), // segment_xsemodules
            ("PLUGINS:", eofMarker) // segment_plugins
        }; // Initialize metadata variables
        string? gameVersion = null;
        string? crashgenVersion = null;
        string? mainError = null; // Parse segments
        var segments = ExtractSegments(crashData, segmentBoundaries, eofMarker);

        // Extract metadata from crash data
        var gameRootName = _appSettings.GameRootName;
        foreach (var line in crashData.OfType<string>())
            if (gameVersion == null && !string.IsNullOrEmpty(gameRootName) && line.StartsWith(gameRootName))
                gameVersion = line.Trim();
            else if (crashgenVersion == null && crashgenName != null && line.StartsWith(crashgenName))
                crashgenVersion = line.Trim();
            else if (mainError == null && line.StartsWith("Unhandled exception"))
                mainError = line.Replace("|", Environment.NewLine, (StringComparison)1);

        var processedSegments = segments.Select(segment =>
            segment.Select(line => line.Trim()).ToList()
        ).ToList();

        // Ensure all expected segments exist (add empty lists for missing segments)
        var missingSectionsCount = segmentBoundaries.Count - processedSegments.Count;
        if (missingSectionsCount > 0)
            for (var i = 0; i < missingSectionsCount; i++)
                processedSegments.Add([]);

        return (
            GameVersion: gameVersion ?? unknown,
            CrashgenVersion: crashgenVersion ?? unknown,
            MainError: mainError ?? unknown,
            Segments: processedSegments
        );
    }

    /// <summary>
    ///     Extracts segments from crash data based on defined boundaries.
    /// </summary>
    /// <param name="crashData">The raw crash report data</param>
    /// <param name="segmentBoundaries">List of tuples with (start_marker, end_marker) for each segment</param>
    /// <param name="eofMarker">The marker used to indicate end of file</param>
    /// <returns>A list of segments where each segment is a list of lines</returns>
    private List<List<string>> ExtractSegments(List<string> crashData,
        List<(string start, string end)> segmentBoundaries, string eofMarker)
    {
        var segments = new List<List<string>>();
        var totalLines = crashData.Count;
        var currentIndex = 0;
        var segmentIndex = 0;
        var collecting = false;
        var segmentStartIndex = 0;
        var currentBoundary = segmentBoundaries[0].start; // Start with first boundary

        while (currentIndex < totalLines)
        {
            var line = crashData[currentIndex];

            // Check if we've hit a boundary
            if (line.StartsWith(currentBoundary))
            {
                if (collecting)
                {
                    // End of current segment
                    var segmentEndIndex = currentIndex - 1 >= 0 ? currentIndex - 1 : currentIndex;
                    segments.Add(crashData.GetRange(segmentStartIndex, segmentEndIndex - segmentStartIndex + 1));
                    segmentIndex++;

                    // Check if we've processed all segments
                    if (segmentIndex == segmentBoundaries.Count) break;
                }
                else
                {
                    // Start of a new segment
                    segmentStartIndex = totalLines > currentIndex ? currentIndex + 1 : currentIndex;
                }

                // Toggle collection state and update boundary
                collecting = !collecting;
                currentBoundary = collecting
                    ? segmentBoundaries[segmentIndex].end
                    : segmentBoundaries[segmentIndex].start;

                // Handle special cases
                if (collecting && currentBoundary == eofMarker)
                {
                    // Add all remaining lines
                    segments.Add(crashData.GetRange(segmentStartIndex, totalLines - segmentStartIndex));
                    break;
                }

                if (!collecting)
                    // Don't increment index in case the current line is also the next start boundary
                    currentIndex--;
            }

            // Check if we've reached the end while still collecting
            if (collecting && currentIndex == totalLines - 1)
                segments.Add(crashData.GetRange(segmentStartIndex, totalLines - segmentStartIndex));

            currentIndex++;
        }

        return segments;
    }

    /// <summary>
    ///     Matches plugins in the given segment callstack against the crashlog plugins and appends the result
    ///     to the issues list.
    /// </summary>
    /// <param name="logFilePath"></param>
    /// <param name="callstack">List of callstack strings (should be lowercase)</param>
    /// <param name="crashlogPlugins">Set of lowercase plugin names from the crash log</param>
    /// <param name="crashgenName"></param>
    /// <returns>A list of LogIssues with plugin matches</returns>
    public List<LogIssue> PluginMatch(string logFilePath, List<string>? callstack,
        IEnumerable<string>? crashlogPlugins, string crashgenName)
    {
        var issues = new List<LogIssue>();
        var enumerable = crashlogPlugins as string[] ?? (crashlogPlugins ?? Array.Empty<string>()).ToArray();
        if (callstack == null || callstack.Count == 0 || enumerable.Length == 0) return issues;

        // Pre-filter call stack lines that won't match and convert to lowercase
        var relevantLines = callstack
            .Select(line => line.ToLower())
            .Where(line => !line.Contains("modified by:"))
            .ToList();

        var crashlogPluginsLower = enumerable.Select(p => p.ToLower()).ToList();

        // Plugin matches counter - Count occurrences of plugins in the callstack
        var pluginsMatches = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);

        // Get ignore plugins list from settings
        var ignorePlugins = _appSettings.GameIgnorePlugins
            .Select(p => p.ToLower())
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        foreach (var plugin in from line in relevantLines
                 from plugin in crashlogPluginsLower
                 where !ignorePlugins.Contains(plugin)
                 where line.Contains(plugin)
                 select plugin)
            if (pluginsMatches.TryGetValue(plugin, out var value))
                pluginsMatches[plugin] = ++value;
            else
                pluginsMatches[plugin] = 1;

        // Generate report if any plugins were found
        if (pluginsMatches.Count == 0) return issues;
        var sortedMatches = pluginsMatches
            .OrderByDescending(kv => kv.Value)
            .ThenBy(kv => kv.Key)
            .ToList();

        var messageBuilder = new StringBuilder("The following PLUGINS were found in the CRASH STACK:\n");

        foreach (var match in sortedMatches) messageBuilder.AppendLine($"- {match.Key} | {match.Value}");

        messageBuilder.AppendLine();
        messageBuilder.AppendLine(
            "[Last number counts how many times each Plugin Suspect shows up in the crash log.]");
        messageBuilder.AppendLine(
            $"These Plugins were caught by {crashgenName} and some of them might be responsible for this crash.");
        messageBuilder.AppendLine(
            "You can try disabling these plugins and check if the game still crashes, though this method can be unreliable.");

        issues.Add(new LogIssue
        {
            FileName = Path.GetFileName(logFilePath),
            IssueId = "PluginSuspects",
            Title = "Plugin Suspects Found In Crash Stack",
            Message = messageBuilder.ToString(),
            Severity = SeverityLevel.Warning,
            Source = "PluginAnalysis"
        });

        return issues;
    }

    /// <summary>
    ///     Scans a call stack segment for named records and reports matches.
    /// </summary>
    /// <param name="logFilePath">Path to the log file</param>
    /// <param name="callstack">The call stack segment to analyze</param>
    /// <param name="crashgenName">Name of the crash generator</param>
    /// <returns>A list of LogIssues with named record matches</returns>
    public List<LogIssue> ScanNamedRecords(string logFilePath, List<string> callstack, string crashgenName)
    {
        var issues = new List<LogIssue>();
        if (callstack.Count == 0) return issues;

        // Constants
        const string rspMarker = "[RSP+";
        const int rspOffset = 30;

        // Find matching records
        var recordsMatches = FindMatchingRecords(callstack, rspMarker, rspOffset);

        if (recordsMatches.Count == 0) return issues;
        // Report found records
        var issueMessage = ReportFoundRecords(recordsMatches, crashgenName);

        issues.Add(new LogIssue
        {
            FileName = Path.GetFileName(logFilePath),
            IssueId = "NamedRecords",
            Title = "Named Records Found In Crash Stack",
            Message = issueMessage,
            Severity = SeverityLevel.Information,
            Source = "RecordAnalysis"
        });

        return issues;
    }

    /// <summary>
    ///     Extract matching records from the call stack based on predefined criteria.
    /// </summary>
    /// <param name="callstack">The call stack to analyze</param>
    /// <param name="rspMarker">Marker used to identify RSP lines</param>
    /// <param name="rspOffset">Offset to use when extracting data from RSP lines</param>
    /// <returns>A list of matched record strings</returns>
    private List<string> FindMatchingRecords(List<string> callstack, string rspMarker, int rspOffset)
    {
        var recordsMatches = new List<string>();

        // Get the records list and ignore list from settings
        var lowerRecords = _appSettings.ClassicRecordsList
            .Select(record => record.ToLower())
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        var lowerIgnore = _appSettings.GameIgnoreRecords
            .Select(record => record.ToLower())
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        foreach (var line in from line in callstack
                 let lowerLine = line.ToLower()
                 where lowerRecords.Any(lowerLine.Contains) &&
                       lowerIgnore.All(record => !lowerLine.Contains(record))
                 select line)
            // Extract the relevant part of the line based on format
            if (line.Contains(rspMarker))
                // Ensure we don't go out of bounds
                recordsMatches.Add(line.Length > rspOffset ? line[rspOffset..].Trim() : line.Trim());
            else
                recordsMatches.Add(line.Trim());

        return recordsMatches;
    }

    /// <summary>
    ///     Format and generate a report message for the found records.
    /// </summary>
    /// <param name="recordsMatches">List of matched record strings</param>
    /// <param name="crashgenName">Name of the crash generator</param>
    /// <returns>Formatted message string</returns>
    private string ReportFoundRecords(List<string> recordsMatches, string crashgenName)
    {
        var messageBuilder = new StringBuilder();

        // Count and sort the records
        var recordsFound = recordsMatches
            .GroupBy(record => record)
            .OrderBy(group => group.Key)
            .ToDictionary(group => group.Key, group => group.Count());

        // Add each record with its count
        foreach (var (record, count) in recordsFound) messageBuilder.AppendLine($"- {record} | {count}");

        // Add explanatory notes
        messageBuilder.AppendLine();
        messageBuilder.AppendLine(
            "[Last number counts how many times each Named Record shows up in the crash log.]");
        messageBuilder.AppendLine(
            $"These records were caught by {crashgenName} and some of them might be related to this crash.");
        messageBuilder.AppendLine(
            "Named records should give extra info on involved game objects, record types or mod files.");

        return messageBuilder.ToString();
    }

    /// <summary>
    ///     Processes and analyzes Form IDs from the provided crash log data,
    ///     matching them against plugins and generating a report.
    /// </summary>
    /// <param name="logFilePath">Path to the log file</param>
    /// <param name="callstack">The call stack segment to analyze</param>
    /// <param name="crashlogPlugins">Dictionary of plugin names to their IDs</param>
    /// <param name="crashgenName">Name of the crash generator</param>
    /// <returns>A list of LogIssues with FormID matches</returns>
    public List<LogIssue> FormIdMatch(string logFilePath, List<string> callstack,
        Dictionary<string, string> crashlogPlugins, string crashgenName)
    {
        var issues = new List<LogIssue>();
        if (callstack.Count == 0) return issues;

        // Extract FormIDs from callstack
        var formIdRegex = GetFormIdRegex();

        var formidsMatches = (from line in callstack
            select formIdRegex.Match(line)
            into match
            where match.Success
            select $"Form ID: {match.Groups[1].Value.Trim().Replace("0x", "")}").ToList();

        if (formidsMatches.Count == 0) return issues;
        var messageBuilder = new StringBuilder();

        // Count occurrences of each FormID
        var formidsFound = formidsMatches
            .GroupBy(formid => formid)
            .OrderBy(group => group.Key)
            .ToDictionary(group => group.Key, group => group.Count());

        foreach (var (formidFull, count) in formidsFound)
        {
            var formidSplit = formidFull.Split(": ", 2);
            if (formidSplit.Length < 2) continue;

            var foundMatch = false;
            foreach (var (plugin, pluginId) in crashlogPlugins)
            {
                if (formidSplit[1].Length < 2 || pluginId != formidSplit[1].Substring(0, 2)) continue;

                // Check FormID database (optional)
                // In a real implementation, we'd query a database here for the FormID entry

                messageBuilder.AppendLine($"- {formidFull} | [{plugin}] | {count}");
                foundMatch = true;
                break;
            }

            // If no plugin match was found, just record the FormID and count
            if (!foundMatch) messageBuilder.AppendLine($"- {formidFull} | [Unknown] | {count}");
        }

        // Add explanatory notes
        messageBuilder.AppendLine();
        messageBuilder.AppendLine(
            "[Last number counts how many times each Form ID shows up in the crash log.]");
        messageBuilder.AppendLine(
            $"These Form IDs were caught by {crashgenName} and some of them might be related to this crash.");
        messageBuilder.AppendLine(
            "You can try searching any listed Form IDs in xEdit and see if they lead to relevant records.");

        issues.Add(new LogIssue
        {
            FileName = Path.GetFileName(logFilePath),
            IssueId = "FormIDMatches",
            Title = "Form IDs Found In Crash Stack",
            Message = messageBuilder.ToString(),
            Severity = SeverityLevel.Information,
            Source = "FormIDAnalysis"
        });

        return issues;
    }

    [GeneratedRegex(@"^(?!.*0xFF)(?=.*id:).*Form ID: ([0-9A-F]{8})", RegexOptions.IgnoreCase | RegexOptions.Multiline,
        "en-US")]
    private static partial Regex GetFormIdRegex();
}