namespace Scanner111.Core.Models;

/// <summary>
///     Contains the results of a mod file scanning operation.
/// </summary>
public sealed class ModScanResult
{
    private ModScanResult(
        bool success,
        IReadOnlyList<ModScanIssue> issues,
        string scanType,
        string? errorMessage = null,
        TimeSpan? duration = null)
    {
        Success = success;
        Issues = issues ?? throw new ArgumentNullException(nameof(issues));
        ScanType = scanType ?? throw new ArgumentNullException(nameof(scanType));
        ErrorMessage = errorMessage;
        Duration = duration;
        Timestamp = DateTimeOffset.UtcNow;
    }

    /// <summary>
    ///     Gets whether the scan completed successfully.
    /// </summary>
    public bool Success { get; }

    /// <summary>
    ///     Gets the list of issues found during the scan.
    /// </summary>
    public IReadOnlyList<ModScanIssue> Issues { get; }

    /// <summary>
    ///     Gets the type of scan performed (e.g., "Unpacked", "Archived", "LogErrors").
    /// </summary>
    public string ScanType { get; }

    /// <summary>
    ///     Gets the error message if the scan failed.
    /// </summary>
    public string? ErrorMessage { get; }

    /// <summary>
    ///     Gets the duration of the scan operation.
    /// </summary>
    public TimeSpan? Duration { get; }

    /// <summary>
    ///     Gets the timestamp when the scan was performed.
    /// </summary>
    public DateTimeOffset Timestamp { get; }

    /// <summary>
    ///     Gets the count of issues by type.
    /// </summary>
    public IReadOnlyDictionary<ModIssueType, int> IssuesByType
    {
        get
        {
            return Issues.GroupBy(i => i.IssueType)
                        .ToDictionary(g => g.Key, g => g.Count());
        }
    }

    /// <summary>
    ///     Creates a successful scan result.
    /// </summary>
    public static ModScanResult CreateSuccess(string scanType, IReadOnlyList<ModScanIssue> issues, TimeSpan? duration = null)
    {
        return new ModScanResult(true, issues, scanType, null, duration);
    }

    /// <summary>
    ///     Creates a failed scan result.
    /// </summary>
    public static ModScanResult CreateFailure(string scanType, string errorMessage, TimeSpan? duration = null)
    {
        return new ModScanResult(false, Array.Empty<ModScanIssue>(), scanType, errorMessage, duration);
    }

    /// <summary>
    ///     Gets issues of a specific type.
    /// </summary>
    public IEnumerable<ModScanIssue> GetIssuesOfType(ModIssueType issueType)
    {
        return Issues.Where(i => i.IssueType == issueType);
    }

    /// <summary>
    ///     Checks if any issues of the specified type were found.
    /// </summary>
    public bool HasIssuesOfType(ModIssueType issueType)
    {
        return Issues.Any(i => i.IssueType == issueType);
    }

    public override string ToString()
    {
        if (!Success)
            return $"{ScanType} scan failed: {ErrorMessage}";
            
        return $"{ScanType} scan completed with {Issues.Count} issues in {Duration?.TotalSeconds:F2}s";
    }
}