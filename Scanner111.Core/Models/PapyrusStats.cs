using System.Text.Json.Serialization;

namespace Scanner111.Core.Models;

/// <summary>
///     Represents statistics extracted from Papyrus log files
/// </summary>
public class PapyrusStats : IEquatable<PapyrusStats>
{
    /// <summary>
    ///     The timestamp when the statistics were recorded
    /// </summary>
    [JsonPropertyName("timestamp")]
    public DateTime Timestamp { get; init; }

    /// <summary>
    ///     The number of dumps processed or analyzed
    /// </summary>
    [JsonPropertyName("dumps")]
    public int Dumps { get; init; }

    /// <summary>
    ///     The number of stacks involved or recorded
    /// </summary>
    [JsonPropertyName("stacks")]
    public int Stacks { get; init; }

    /// <summary>
    ///     The number of warnings encountered
    /// </summary>
    [JsonPropertyName("warnings")]
    public int Warnings { get; init; }

    /// <summary>
    ///     The number of errors encountered
    /// </summary>
    [JsonPropertyName("errors")]
    public int Errors { get; init; }

    /// <summary>
    ///     The calculated dumps to stacks ratio
    /// </summary>
    [JsonPropertyName("ratio")]
    public double Ratio { get; init; }

    /// <summary>
    ///     The path to the Papyrus log file being monitored
    /// </summary>
    [JsonPropertyName("logPath")]
    public string? LogPath { get; init; }

    /// <summary>
    ///     Total issues count (dumps + stacks + warnings + errors)
    /// </summary>
    [JsonIgnore]
    public int TotalIssues => Dumps + Stacks + Warnings + Errors;

    /// <summary>
    ///     Indicates if the log has critical issues based on thresholds
    /// </summary>
    [JsonIgnore]
    public bool HasCriticalIssues => Errors > 100 || Ratio > 0.5;

    /// <summary>
    ///     Compares this PapyrusStats instance for equality with another
    /// </summary>
    public bool Equals(PapyrusStats? other)
    {
        if (other is null) return false;
        if (ReferenceEquals(this, other)) return true;

        return Dumps == other.Dumps &&
               Stacks == other.Stacks &&
               Warnings == other.Warnings &&
               Errors == other.Errors;
    }

    /// <summary>
    ///     Determines whether the specified object is equal to the current object
    /// </summary>
    public override bool Equals(object? obj)
    {
        return Equals(obj as PapyrusStats);
    }

    /// <summary>
    ///     Returns the hash code for this instance
    /// </summary>
    public override int GetHashCode()
    {
        return HashCode.Combine(Dumps, Stacks, Warnings, Errors);
    }

    /// <summary>
    ///     Returns a string representation of the statistics
    /// </summary>
    public override string ToString()
    {
        return $"Dumps: {Dumps}, Stacks: {Stacks}, Ratio: {Ratio:F3}, Warnings: {Warnings}, Errors: {Errors}";
    }

    /// <summary>
    ///     Creates a new instance with updated values
    /// </summary>
    public PapyrusStats WithUpdatedStats(int dumps, int stacks, int warnings, int errors)
    {
        var ratio = stacks == 0 ? 0.0 : (double)dumps / stacks;

        return new PapyrusStats
        {
            Timestamp = DateTime.Now,
            Dumps = dumps,
            Stacks = stacks,
            Warnings = warnings,
            Errors = errors,
            Ratio = ratio,
            LogPath = LogPath
        };
    }

    /// <summary>
    ///     Equality operator
    /// </summary>
    public static bool operator ==(PapyrusStats? left, PapyrusStats? right)
    {
        if (left is null) return right is null;
        return left.Equals(right);
    }

    /// <summary>
    ///     Inequality operator
    /// </summary>
    public static bool operator !=(PapyrusStats? left, PapyrusStats? right)
    {
        return !(left == right);
    }
}

/// <summary>
///     Event args for Papyrus stats updates
/// </summary>
public class PapyrusStatsUpdatedEventArgs : EventArgs
{
    public PapyrusStatsUpdatedEventArgs(PapyrusStats stats, PapyrusStats? previousStats = null)
    {
        Stats = stats;
        PreviousStats = previousStats;
        IsFirstUpdate = previousStats == null;
    }

    /// <summary>
    ///     The current statistics
    /// </summary>
    public PapyrusStats Stats { get; }

    /// <summary>
    ///     The previous statistics (null for first update)
    /// </summary>
    public PapyrusStats? PreviousStats { get; }

    /// <summary>
    ///     Indicates if this is the first update
    /// </summary>
    public bool IsFirstUpdate { get; }

    /// <summary>
    ///     Gets the change in dumps since last update
    /// </summary>
    public int DumpsDelta => PreviousStats == null ? 0 : Stats.Dumps - PreviousStats.Dumps;

    /// <summary>
    ///     Gets the change in errors since last update
    /// </summary>
    public int ErrorsDelta => PreviousStats == null ? 0 : Stats.Errors - PreviousStats.Errors;

    /// <summary>
    ///     Gets the change in warnings since last update
    /// </summary>
    public int WarningsDelta => PreviousStats == null ? 0 : Stats.Warnings - PreviousStats.Warnings;
}