using System.IO;
using System.Linq;
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
    ///     Main description for the UI - shows filename
    /// </summary>
    public string Description => Path.GetFileName(ScanResult.LogPath);

    /// <summary>
    ///     Detailed information for the UI
    /// </summary>
    public string Details => ScanResult.HasErrors
        ? string.Join("; ", ScanResult.ErrorMessages)
        : $"Processing time: {ScanResult.ProcessingTime.TotalSeconds:F2}s";

    /// <summary>
    ///     Severity level for color coding
    /// </summary>
    public string Severity => ScanResult.Failed
        ? "ERROR"
        : ScanResult.HasErrors
            ? "WARNING"
            : "INFO";

    /// <summary>
    ///     Severity color for the UI
    /// </summary>
    public string SeverityColor => Severity switch
    {
        "ERROR" => "#FFE53E3E",
        "WARNING" => "#FFFF9500",
        "INFO" => "#FF0e639c",
        _ => "#FF666666"
    };

    /// <summary>
    ///     Category for grouping
    /// </summary>
    public string Category => ScanResult.Status.ToString();
    
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