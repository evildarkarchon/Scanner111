using Scanner111.Core.Models;

namespace Scanner111.GUI.Models;

/// <summary>
///     View model wrapper for ScanResult to provide UI-friendly properties
/// </summary>
public class ScanResultViewModel
{
    public ScanResultViewModel(ScanResult scanResult)
    {
        ScanResult = scanResult;
    }

    public ScanResult ScanResult { get; }

    /// <summary>
    ///     Gets the filename extracted from the log file's path for display purposes.
    /// </summary>
    public string Description => Path.GetFileName(ScanResult.LogPath);

    /// <summary>
    ///     Gets the detailed information about the scan result.
    ///     If there are errors, returns a concatenated string of error messages.
    ///     Otherwise, displays the total processing time in seconds.
    /// </summary>
    public string Details => ScanResult.HasErrors
        ? string.Join("; ", ScanResult.ErrorMessages)
        : $"Processing time: {ScanResult.ProcessingTime.TotalSeconds:F2}s";

    /// <summary>
    ///     Gets the severity level of the scan result, representing its status as "ERROR", "WARNING", or "INFO".
    /// </summary>
    public string Severity => ScanResult.Failed
        ? "ERROR"
        : ScanResult.HasErrors
            ? "WARNING"
            : "INFO";

    /// <summary>
    ///     Gets the color associated with the severity level of the scan result,
    ///     represented as a hexadecimal ARGB color code string.
    /// </summary>
    public string SeverityColor => Severity switch
    {
        "ERROR" => "#FFE53E3E",
        "WARNING" => "#FFFF9500",
        "INFO" => "#FF0e639c",
        _ => "#FF666666"
    };

    /// <summary>
    ///     Gets the category of the scan result, which represents the current status of the scan.
    /// </summary>
    public string Category => ScanResult.Status.ToString();

    /// <summary>
    ///     Retrieves the first cleaned and potentially truncated line from the report contained in the ScanResult.
    ///     If the report is empty, returns a default message indicating no issues were found.
    /// </summary>
    /// <returns>
    ///     A string representing the first line of the report, cleaned for display purposes, or a default message
    ///     if the report is empty.
    /// </returns>
    public string GetFirstReportLine()
    {
        var firstLine = ScanResult.Report.FirstOrDefault();
        if (string.IsNullOrEmpty(firstLine))
            return "No issues found";

        // Clean up the line for display
        var cleaned = firstLine.Trim().Replace("- ", "").Replace("* ", "");
        return cleaned.Length > 100 ? cleaned.Substring(0, 97) + "..." : cleaned;
    }
}