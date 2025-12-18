namespace Scanner111.Common.Models.Papyrus;

/// <summary>
/// Represents statistics from a Papyrus log file analysis.
/// </summary>
/// <remarks>
/// This record captures key metrics from Papyrus.0.log that indicate
/// script activity and potential issues in the game's scripting system.
/// Only tracks new entries written after monitoring started.
/// </remarks>
public record PapyrusStats
{
    /// <summary>
    /// Gets the timestamp when these statistics were collected.
    /// </summary>
    public required DateTime Timestamp { get; init; }

    /// <summary>
    /// Gets the number of stack dump events ("Dumping Stacks").
    /// </summary>
    public required int Dumps { get; init; }

    /// <summary>
    /// Gets the number of individual stack frames ("Dumping Stack").
    /// </summary>
    public required int Stacks { get; init; }

    /// <summary>
    /// Gets the number of warning messages.
    /// </summary>
    public required int Warnings { get; init; }

    /// <summary>
    /// Gets the number of error messages.
    /// </summary>
    public required int Errors { get; init; }

    /// <summary>
    /// Gets the ratio of dumps to stacks.
    /// </summary>
    /// <remarks>
    /// Returns 0 if there are no stacks to avoid division by zero.
    /// </remarks>
    public double Ratio => Stacks > 0 ? (double)Dumps / Stacks : 0.0;

    /// <summary>
    /// Creates an empty stats instance representing initial/no data state.
    /// </summary>
    public static PapyrusStats Empty => new()
    {
        Timestamp = DateTime.Now,
        Dumps = 0,
        Stacks = 0,
        Warnings = 0,
        Errors = 0
    };

    /// <summary>
    /// Determines equality based on metric values (excluding timestamp).
    /// </summary>
    public virtual bool Equals(PapyrusStats? other)
    {
        if (other is null) return false;
        return Dumps == other.Dumps &&
               Stacks == other.Stacks &&
               Warnings == other.Warnings &&
               Errors == other.Errors;
    }

    /// <summary>
    /// Returns hash code based on metric values (excluding timestamp).
    /// </summary>
    public override int GetHashCode() =>
        HashCode.Combine(Dumps, Stacks, Warnings, Errors);
}
