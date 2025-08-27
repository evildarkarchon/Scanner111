using System.Collections.Concurrent;
using System.Text;
using Microsoft.Extensions.Logging;
using Scanner111.Core.Configuration;
using Scanner111.Core.Reporting;

namespace Scanner111.Core.Analysis.Analyzers;

/// <summary>
/// Analyzes crash logs to detect and count named records in call stacks.
/// Thread-safe analyzer that identifies game objects, record types, and mod files referenced in crashes.
/// </summary>
public sealed class RecordScannerAnalyzer : AnalyzerBase
{
    private const string RspMarker = "[RSP+";
    private const int RspOffset = 30;
    
    private readonly IAsyncYamlSettingsCore _yamlCore;
    private readonly string _crashGenName;

    // Cache for performance - loaded once and reused
    private readonly Lazy<Task<RecordScannerConfig>> _configLazy;

    public RecordScannerAnalyzer(
        ILogger<RecordScannerAnalyzer> logger,
        IAsyncYamlSettingsCore yamlCore,
        string? crashGenName = null)
        : base(logger)
    {
        _yamlCore = yamlCore ?? throw new ArgumentNullException(nameof(yamlCore));
        _crashGenName = crashGenName ?? "Scanner111";
        _configLazy = new Lazy<Task<RecordScannerConfig>>(LoadConfigurationAsync);
    }

    /// <inheritdoc />
    public override string Name => "RecordScanner";

    /// <inheritdoc />
    public override string DisplayName => "Named Records Scanner";

    /// <inheritdoc />
    public override int Priority => 50; // Run after basic analysis but before report generation

    /// <inheritdoc />
    public override TimeSpan Timeout => TimeSpan.FromSeconds(30);

    /// <inheritdoc />
    protected override async Task<AnalysisResult> PerformAnalysisAsync(
        AnalysisContext context,
        CancellationToken cancellationToken)
    {
        LogDebug("Starting record scanning for {Path}", context.InputPath);

        try
        {
            // Get call stack segment from context
            if (!context.TryGetSharedData<List<string>>("CallStackSegment", out var callStackSegment) ||
                callStackSegment == null || callStackSegment.Count == 0)
            {
                LogDebug("No call stack segment found in context");
                return CreateNoCallStackResult();
            }

            // Load configuration
            var config = await _configLazy.Value.ConfigureAwait(false);
            
            // Scan for named records
            var (fragment, foundRecords) = await ScanNamedRecordsAsync(callStackSegment, config, cancellationToken)
                .ConfigureAwait(false);

            // Store found records in context for other analyzers
            context.SetSharedData("FoundRecords", foundRecords.AsReadOnly());

            var result = new AnalysisResult(Name)
            {
                Success = true,
                Fragment = fragment,
                Severity = DetermineRecordSeverity(foundRecords.Count)
            };

            LogDebug("Record scanning completed. Found {RecordCount} records", foundRecords.Count);
            return result;
        }
        catch (Exception ex)
        {
            LogError(ex, "Record scanning failed");
            var result = new AnalysisResult(Name)
            {
                Success = false,
                Fragment = ReportFragment.CreateError("Named Records Scanner", 
                    "Failed to scan for named records in crash log call stack.", 1000)
            };
            result.AddError(ex.Message);
            return result;
        }
    }

    /// <summary>
    /// Scans for named records in the call stack segment.
    /// </summary>
    private async Task<(ReportFragment fragment, List<string> foundRecords)> ScanNamedRecordsAsync(
        IReadOnlyList<string> callStackSegment,
        RecordScannerConfig config,
        CancellationToken cancellationToken)
    {
        var recordMatches = new List<string>();

        // Find matching records using parallel processing for large call stacks
        if (callStackSegment.Count > 100)
        {
            // Use concurrent collection for thread-safe access
            var concurrentMatches = new ConcurrentBag<string>();
            
            await Task.Run(() =>
            {
                Parallel.ForEach(callStackSegment, new ParallelOptions
                {
                    CancellationToken = cancellationToken,
                    MaxDegreeOfParallelism = Environment.ProcessorCount
                }, line =>
                {
                    var matches = FindRecordsInLine(line, config);
                    foreach (var match in matches)
                    {
                        concurrentMatches.Add(match);
                    }
                });
            }, cancellationToken).ConfigureAwait(false);

            recordMatches.AddRange(concurrentMatches);
        }
        else
        {
            // Use sequential processing for smaller call stacks
            foreach (var line in callStackSegment)
            {
                cancellationToken.ThrowIfCancellationRequested();
                var matches = FindRecordsInLine(line, config);
                recordMatches.AddRange(matches);
            }
        }

        // Generate report fragment
        var fragment = recordMatches.Count > 0
            ? GenerateFoundRecordsFragment(recordMatches)
            : GenerateNoRecordsFragment();

        return (fragment, recordMatches);
    }

    /// <summary>
    /// Finds records in a single line of the call stack.
    /// </summary>
    private List<string> FindRecordsInLine(string line, RecordScannerConfig config)
    {
        if (string.IsNullOrWhiteSpace(line))
            return new List<string>();

        var lowerLine = line.ToLowerInvariant();
        var matches = new List<string>();

        // Check if line contains any target record and doesn't contain any ignored terms
        var hasTargetRecord = config.LowerRecords.Count == 0 || config.LowerRecords.Any(record => lowerLine.Contains(record, StringComparison.Ordinal));
        var hasIgnoredTerm = config.LowerIgnore.Any(ignore => lowerLine.Contains(ignore, StringComparison.Ordinal));

        if (hasTargetRecord && !hasIgnoredTerm)
        {
            // Extract the relevant part of the line based on format
            var extractedContent = line.Contains(RspMarker, StringComparison.Ordinal) && line.Length > RspOffset
                ? line.Substring(RspOffset).Trim()
                : line.Trim();

            if (!string.IsNullOrEmpty(extractedContent))
            {
                matches.Add(extractedContent);
            }
        }

        return matches;
    }

    /// <summary>
    /// Generates report fragment for found records.
    /// </summary>
    private ReportFragment GenerateFoundRecordsFragment(IReadOnlyList<string> recordMatches)
    {
        var lines = new List<string>();

        // Count and sort the records
        var recordCounts = recordMatches
            .GroupBy(record => record, StringComparer.OrdinalIgnoreCase)
            .OrderBy(group => group.Key, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(group => group.Key, group => group.Count());

        lines.Add("### Named Records Found\n\n");

        // Add each record with its count
        foreach (var (record, count) in recordCounts)
        {
            lines.Add($"- **{record}** | *{count} occurrence{(count == 1 ? "" : "s")}*\n");
        }

        lines.Add("\n");

        // Add explanatory notes
        lines.Add("**Analysis Notes:**\n");
        lines.Add("- The number indicates how many times each named record appears in the crash log\n");
        lines.Add($"- These records were detected by {_crashGenName} and may be related to this crash\n");
        lines.Add("- Named records provide information about involved game objects, record types, or mod files\n");
        lines.Add("\n");

        return ReportFragment.CreateInfo("Named Records Analysis", string.Join("", lines), 300);
    }

    /// <summary>
    /// Generates report fragment when no records are found.
    /// </summary>
    private ReportFragment GenerateNoRecordsFragment()
    {
        return ReportFragment.CreateInfo("Named Records Analysis", 
            "**No named records detected in crash log call stack.**\n\n" +
            "This may indicate that the crash doesn't involve specific game objects or mod records, " +
            "or the crash log format doesn't contain identifiable named records.\n\n", 400);
    }

    /// <summary>
    /// Creates result when no call stack segment is available.
    /// </summary>
    private AnalysisResult CreateNoCallStackResult()
    {
        return new AnalysisResult(Name)
        {
            Success = true,
            Fragment = ReportFragment.CreateWarning("Named Records Scanner", 
                "No call stack segment found in crash log for record scanning.", 900)
        };
    }

    /// <summary>
    /// Loads configuration from YAML settings.
    /// </summary>
    private Task<RecordScannerConfig> LoadConfigurationAsync()
    {
        try
        {
            // Load record lists from YAML configuration
            // These would come from the catch_log_records and exclude_log_records sections
            var targetRecords = new List<string>
            {
                ".bgsm", ".bto", ".btr", ".dds", ".dll+", ".fuz", ".hkb", ".hkx", 
                ".ini", ".nif", ".pex", ".strings", ".swf", ".txt", ".uvd", 
                ".wav", ".xwm", "data/", "data\\", "scaleform", "editorid:", 
                "file:", "function:", "name:"
            };

            var ignoreRecords = new List<string>
            {
                "\"\"", "...", "FE:", ".esl", ".esm", ".esp", ".exe", 
                "Buffout4.dll+", "KERNEL", "MSVC", "USER32", "Unhandled", 
                "cudart64_75.dll+", "d3d11.dll+", "dxgi.dll+", "f4se", 
                "flexRelease_x64.dll+", "kernel32.dll+", "ntdll", 
                "nvcuda64.dll+", "nvumdshimx.dll+", "nvwgf2umx.dll+", 
                "steamclient64.dll+", "usvfs_x64", "vrclient_x64.dll+", "win32u"
            };

            return Task.FromResult(new RecordScannerConfig
            {
                TargetRecords = targetRecords,
                IgnoreRecords = ignoreRecords,
                LowerRecords = new HashSet<string>(targetRecords.Select(r => r.ToLowerInvariant())),
                LowerIgnore = new HashSet<string>(ignoreRecords.Select(r => r.ToLowerInvariant()))
            });
        }
        catch (Exception ex)
        {
            LogWarning("Failed to load record scanner configuration, using defaults: {ErrorMessage}", ex.Message);
            
            // Return minimal configuration as fallback
            return Task.FromResult(new RecordScannerConfig
            {
                TargetRecords = new List<string> { ".nif", ".dds", ".pex" },
                IgnoreRecords = new List<string> { ".exe", "KERNEL", "ntdll" },
                LowerRecords = new HashSet<string> { ".nif", ".dds", ".pex" },
                LowerIgnore = new HashSet<string> { ".exe", "kernel", "ntdll" }
            });
        }
    }

    /// <summary>
    /// Determines severity based on number of records found.
    /// </summary>
    private static AnalysisSeverity DetermineRecordSeverity(int recordCount)
    {
        return recordCount switch
        {
            0 => AnalysisSeverity.Info, // Info - no records found
            <= 5 => AnalysisSeverity.Info, // Info - few records found
            <= 15 => AnalysisSeverity.Info, // Info - moderate records found  
            _ => AnalysisSeverity.Info // Info - many records found (not necessarily bad)
        };
    }

    /// <summary>
    /// Configuration data for record scanning.
    /// </summary>
    private sealed class RecordScannerConfig
    {
        public required List<string> TargetRecords { get; init; }
        public required List<string> IgnoreRecords { get; init; }
        public required HashSet<string> LowerRecords { get; init; }
        public required HashSet<string> LowerIgnore { get; init; }
    }
}