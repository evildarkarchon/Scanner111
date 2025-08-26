namespace Scanner111.Core.Models;

/// <summary>
///     Represents a progress update in an operation.
/// </summary>
public record ProgressReport
{
    /// <summary>
    ///     Gets the description of the current operation.
    /// </summary>
    public required string Description { get; init; }

    /// <summary>
    ///     Gets the current progress value.
    /// </summary>
    public int Current { get; init; }

    /// <summary>
    ///     Gets the total value for progress calculation.
    ///     Null indicates indeterminate progress.
    /// </summary>
    public int? Total { get; init; }

    /// <summary>
    ///     Gets the percentage of completion (0-100).
    ///     Null when Total is null (indeterminate progress).
    /// </summary>
    public double? PercentComplete => Total.HasValue && Total.Value > 0
        ? (double)Current / Total.Value * 100
        : null;

    /// <summary>
    ///     Gets a value indicating whether the progress is indeterminate.
    /// </summary>
    public bool IsIndeterminate => !Total.HasValue;
}