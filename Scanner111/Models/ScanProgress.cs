using System;

namespace Scanner111.Models;

/// <summary>
///     Represents progress information for scanning operations
/// </summary>
public class ScanProgress
{
    /// <summary>
    ///     Creates a new instance of the ScanProgress class
    /// </summary>
    /// <param name="percentComplete">The percentage of completion (0-100)</param>
    /// <param name="currentOperation">The current operation being performed</param>
    /// <param name="currentItem">The current item being processed (optional)</param>
    public ScanProgress(int percentComplete, string currentOperation, string? currentItem = null)
    {
        PercentComplete = Math.Clamp(percentComplete, 0, 100);
        CurrentOperation = currentOperation ?? throw new ArgumentNullException(nameof(currentOperation));
        CurrentItem = currentItem;
        Timestamp = DateTime.Now;
    }

    /// <summary>
    ///     Gets or sets the percentage of completion (0-100)
    /// </summary>
    public int PercentComplete { get; set; }

    /// <summary>
    ///     Gets or sets the current operation being performed
    /// </summary>
    public string CurrentOperation { get; set; }

    /// <summary>
    ///     Gets or sets the current item being processed
    /// </summary>
    public string? CurrentItem { get; set; }

    /// <summary>
    ///     Gets or sets the timestamp when this progress was reported
    /// </summary>
    public DateTime Timestamp { get; set; }
}