namespace Scanner111.Common.Models.Analysis;

/// <summary>
/// Represents the result of scanning a crash log call stack for named records.
/// Named records are game objects, record types, or mod files that appear in crash logs.
/// </summary>
public record RecordScanResult
{
    /// <summary>
    /// Gets the list of matched records found in the call stack.
    /// Each entry is a line from the call stack containing a named record.
    /// </summary>
    public IReadOnlyList<string> MatchedRecords { get; init; } = Array.Empty<string>();

    /// <summary>
    /// Gets the record counts (how many times each unique record appeared).
    /// Key is the record text, value is the count.
    /// </summary>
    public IReadOnlyDictionary<string, int> RecordCounts { get; init; } = new Dictionary<string, int>();

    /// <summary>
    /// Gets a value indicating whether any records were found.
    /// </summary>
    public bool HasRecords => MatchedRecords.Count > 0;

    /// <summary>
    /// Gets the total number of record matches (including duplicates).
    /// </summary>
    public int TotalMatches => MatchedRecords.Count;

    /// <summary>
    /// Gets the number of unique records found.
    /// </summary>
    public int UniqueRecordCount => RecordCounts.Count;

    /// <summary>
    /// Returns an empty result indicating no records were found.
    /// </summary>
    public static RecordScanResult Empty { get; } = new();
}

/// <summary>
/// Configuration for the record scanner, containing patterns to search for and ignore.
/// </summary>
public record RecordScannerConfiguration
{
    /// <summary>
    /// Gets the list of record names to search for in call stacks.
    /// These are typically game record types like "WEAP", "NPC_", "ARMO", etc.
    /// </summary>
    public IReadOnlyList<string> TargetRecords { get; init; } = Array.Empty<string>();

    /// <summary>
    /// Gets the list of record names to ignore when found.
    /// These filter out common false positives.
    /// </summary>
    public IReadOnlyList<string> IgnoreRecords { get; init; } = Array.Empty<string>();

    /// <summary>
    /// Gets the name of the crash generator (e.g., "Buffout4") for report messages.
    /// </summary>
    public string CrashGeneratorName { get; init; } = "Crash Generator";

    /// <summary>
    /// Returns an empty configuration.
    /// </summary>
    public static RecordScannerConfiguration Empty { get; } = new();
}
