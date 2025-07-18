using Scanner111.Core.Models;

namespace Scanner111.Core.Analyzers;

/// <summary>
/// Handles scanning for named records in crash logs, direct port of Python RecordScanner
/// </summary>
public class RecordScanner : IAnalyzer
{
    private readonly ClassicScanLogsInfo _yamlData;
    private readonly HashSet<string> _lowerRecords;
    private readonly HashSet<string> _lowerIgnore;

    /// <summary>
    /// Name of the analyzer
    /// </summary>
    public string Name => "Record Scanner";
    
    /// <summary>
    /// Priority of the analyzer (lower values run first)
    /// </summary>
    public int Priority => 30;
    
    /// <summary>
    /// Whether this analyzer can be run in parallel with others
    /// </summary>
    public bool CanRunInParallel => true;

    /// <summary>
    /// Initialize the record scanner
    /// </summary>
    /// <param name="yamlData">Configuration data containing record patterns</param>
    public RecordScanner(ClassicScanLogsInfo yamlData)
    {
        _yamlData = yamlData;
        
        // Note: Using empty sets if the lists are not available in the configuration
        // The actual lists would need to be loaded from YAML configuration
        _lowerRecords = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        _lowerIgnore = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
    }

    /// <summary>
    /// Analyze a crash log for named records
    /// </summary>
    /// <param name="crashLog">Crash log to analyze</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Record analysis result</returns>
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
                {"RecordsMatches", recordsMatches},
                {"ExtractedRecords", extractedRecords}
            }
        };
    }

    /// <summary>
    /// Scans named records in the provided segment callstack, identifies matches, and updates the autoscan report accordingly.
    /// Direct port of Python scan_named_records method.
    /// </summary>
    /// <param name="segmentCallstack">The callstack to scan for named records</param>
    /// <param name="recordsMatches">A list to hold records that match the scan criteria</param>
    /// <param name="autoscanReport">The report to be updated based on scanning results</param>
    private void ScanNamedRecords(List<string> segmentCallstack, List<string> recordsMatches, List<string> autoscanReport)
    {
        // Constants
        const string rspMarker = "[RSP+";
        const int rspOffset = 30;

        // Find matching records
        FindMatchingRecords(segmentCallstack, recordsMatches, rspMarker, rspOffset);

        // Report results
        if (recordsMatches.Count > 0)
        {
            ReportFoundRecords(recordsMatches, autoscanReport);
        }
        else
        {
            autoscanReport.Add("* COULDN'T FIND ANY NAMED RECORDS *\n\n");
        }
    }

    /// <summary>
    /// Finds and collects matching records from a given segment of a call stack based on specified criteria.
    /// Direct port of Python _find_matching_records method.
    /// </summary>
    /// <param name="segmentCallstack">A list of strings representing segment of the call stack to be analyzed</param>
    /// <param name="recordsMatches">A list where matching record lines will be appended</param>
    /// <param name="rspMarker">A marker string to identify the relevant portion of the call stack lines</param>
    /// <param name="rspOffset">An integer representing the character offset from rsp_marker used to determine where to begin extracting record content</param>
    private void FindMatchingRecords(List<string> segmentCallstack, List<string> recordsMatches, string rspMarker, int rspOffset)
    {
        foreach (var line in segmentCallstack)
        {
            var lowerLine = line.ToLower();

            // Check if line contains any target record and doesn't contain any ignored terms
            if (_lowerRecords.Any(item => lowerLine.Contains(item)) && 
                !_lowerIgnore.Any(record => lowerLine.Contains(record)))
            {
                // Extract the relevant part of the line based on format
                if (line.Contains(rspMarker))
                {
                    if (line.Length > rspOffset)
                    {
                        recordsMatches.Add(line.Substring(rspOffset).Trim());
                    }
                }
                else
                {
                    recordsMatches.Add(line.Trim());
                }
            }
        }
    }

    /// <summary>
    /// Format and add report entries for found records.
    /// Direct port of Python _report_found_records method.
    /// </summary>
    /// <param name="recordsMatches">List of found records</param>
    /// <param name="autoscanReport">List to append formatted report</param>
    private void ReportFoundRecords(List<string> recordsMatches, List<string> autoscanReport)
    {
        // Count and sort the records
        var recordsFound = recordsMatches
            .GroupBy(record => record)
            .OrderBy(g => g.Key)
            .ToDictionary(g => g.Key, g => g.Count());

        // Add each record with its count
        foreach (var (record, count) in recordsFound)
        {
            autoscanReport.Add($"- {record} | {count}\n");
        }

        // Add explanatory notes
        var explanatoryNotes = new[]
        {
            "\n[Last number counts how many times each Named Record shows up in the crash log.]\n",
            $"These records were caught by {_yamlData.CrashgenName} and some of them might be related to this crash.\n",
            "Named records should give extra info on involved game objects, record types or mod files.\n\n"
        };
        autoscanReport.AddRange(explanatoryNotes);
    }

    /// <summary>
    /// Extract records from a segment callstack based on specific matching criteria.
    /// Direct port of Python extract_records method.
    /// </summary>
    /// <param name="segmentCallstack">The list of strings representing the segment callstack to be processed</param>
    /// <returns>A list of strings containing the matching records identified from the segment callstack</returns>
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