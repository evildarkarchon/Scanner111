using System.Collections.Concurrent;
using System.Text.RegularExpressions;
using Scanner111.Common.Models.Analysis;
using Scanner111.Common.Models.Reporting;

namespace Scanner111.Common.Services.Analysis;

/// <summary>
/// Scans crash log call stacks for named records (game objects, record types, mod files).
/// </summary>
public class RecordScanner : IRecordScanner
{
    private const string RspMarker = "[RSP+";
    private const int RspOffset = 30;
    private const string StackSegmentName = "STACK";

    private readonly ConcurrentDictionary<string, Regex> _patternCache = new();

    private Regex? _targetPattern;
    private Regex? _ignorePattern;
    private RecordScannerConfiguration _configuration = RecordScannerConfiguration.Empty;

    /// <inheritdoc/>
    public RecordScannerConfiguration Configuration
    {
        get => _configuration;
        set
        {
            _configuration = value;
            RebuildPatterns();
        }
    }

    /// <inheritdoc/>
    public async Task<RecordScanResult> ScanAsync(
        LogSegment? callStackSegment,
        CancellationToken cancellationToken = default)
    {
        await Task.Yield();
        cancellationToken.ThrowIfCancellationRequested();

        if (callStackSegment == null || callStackSegment.Lines.Count == 0)
        {
            return RecordScanResult.Empty;
        }

        if (_targetPattern == null)
        {
            return RecordScanResult.Empty;
        }

        var matchedRecords = new List<string>();

        foreach (var line in callStackSegment.Lines)
        {
            cancellationToken.ThrowIfCancellationRequested();

            // Check if line contains any target record
            if (!_targetPattern.IsMatch(line))
            {
                continue;
            }

            // Check if line should be ignored
            if (_ignorePattern != null && _ignorePattern.IsMatch(line))
            {
                continue;
            }

            // Extract the relevant part of the line
            var recordText = ExtractRecordText(line);
            if (!string.IsNullOrWhiteSpace(recordText))
            {
                matchedRecords.Add(recordText);
            }
        }

        // Build record counts
        var recordCounts = matchedRecords
            .GroupBy(r => r, StringComparer.OrdinalIgnoreCase)
            .OrderBy(g => g.Key)
            .ToDictionary(g => g.Key, g => g.Count());

        return new RecordScanResult
        {
            MatchedRecords = matchedRecords,
            RecordCounts = recordCounts
        };
    }

    /// <inheritdoc/>
    public async Task<RecordScanResult> ScanFromSegmentsAsync(
        IReadOnlyList<LogSegment> segments,
        CancellationToken cancellationToken = default)
    {
        // Find the STACK segment (contains RSP+ entries with record info)
        var stackSegment = segments.FirstOrDefault(s =>
            s.Name.Equals(StackSegmentName, StringComparison.OrdinalIgnoreCase));

        return await ScanAsync(stackSegment, cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc/>
    public ReportFragment CreateReportFragment(RecordScanResult result)
    {
        var lines = new List<string>();

        if (!result.HasRecords)
        {
            lines.Add("* COULDN'T FIND ANY NAMED RECORDS *");
            lines.Add(string.Empty);
            return ReportFragment.FromLines(lines.ToArray());
        }

        lines.Add("## Named Records Found");
        lines.Add(string.Empty);

        // Add each record with its count
        foreach (var (record, count) in result.RecordCounts)
        {
            lines.Add($"- {record} | {count}");
        }

        lines.Add(string.Empty);

        // Add explanatory notes
        lines.Add($"> Last number counts how many times each Named Record shows up in the crash log.");
        lines.Add($"> These records were caught by {Configuration.CrashGeneratorName} and some may be related to this crash.");
        lines.Add("> Named records give extra info on involved game objects, record types, or mod files.");
        lines.Add(string.Empty);

        return ReportFragment.FromLines(lines.ToArray());
    }

    private void RebuildPatterns()
    {
        _targetPattern = BuildCombinedPattern(_configuration.TargetRecords);
        _ignorePattern = BuildCombinedPattern(_configuration.IgnoreRecords);
    }

    private Regex? BuildCombinedPattern(IReadOnlyList<string> patterns)
    {
        if (patterns.Count == 0)
        {
            return null;
        }

        // Sort by length descending for most specific matches first
        var sortedPatterns = patterns
            .Where(p => !string.IsNullOrWhiteSpace(p))
            .Select(p => Regex.Escape(p.ToLowerInvariant()))
            .OrderByDescending(p => p.Length)
            .ToList();

        if (sortedPatterns.Count == 0)
        {
            return null;
        }

        var combinedPattern = string.Join("|", sortedPatterns);
        return new Regex(combinedPattern, RegexOptions.Compiled | RegexOptions.IgnoreCase);
    }

    private static string ExtractRecordText(string line)
    {
        // If line contains RSP marker, extract content after the offset
        var rspIndex = line.IndexOf(RspMarker, StringComparison.Ordinal);
        if (rspIndex >= 0 && line.Length > rspIndex + RspOffset)
        {
            return line[(rspIndex + RspOffset)..].Trim();
        }

        // Otherwise, return the trimmed line
        return line.Trim();
    }
}
