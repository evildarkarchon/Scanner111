using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Scanner111.ClassicLib.ScanLog.Models;
using Scanner111.ClassicLib.ScanLog.Services.Interfaces;

namespace Scanner111.ClassicLib.ScanLog.Services;

/// <summary>
///     Implementation of report writer service.
/// </summary>
public class ReportWriterService : IReportWriterService
{
    private readonly ILogger<ReportWriterService> _logger;

    public ReportWriterService(ILogger<ReportWriterService> logger)
    {
        _logger = logger;
    }

    /// <summary>
    ///     Writes a crash log scan report to file.
    /// </summary>
    public async Task WriteReportToFileAsync(string logFileName, List<string> report, bool scanFailed)
    {
        try
        {
            var reportFileName = Path.GetFileName(logFileName).Replace(".log", "-AUTOSCAN.md");
            var reportDir = Path.Combine(Directory.GetCurrentDirectory(), "Crash Logs");

            // Create directory if it doesn't exist
            if (!Directory.Exists(reportDir)) Directory.CreateDirectory(reportDir);

            var reportPath = Path.Combine(reportDir, reportFileName);

            await File.WriteAllLinesAsync(reportPath, report);

            // Move failed scans to separate folder if enabled
            if (scanFailed) await MoveFailedLogAsync(logFileName);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error writing report for {LogFile}", logFileName);
        }
    }

    /// <summary>
    ///     Writes combined results from all scans.
    /// </summary>
    public async Task WriteCombinedResultsAsync(List<CrashLogProcessResult> results, ScanStatistics statistics)
    {
        var reportDir = Path.Combine(Directory.GetCurrentDirectory(), "Crash Logs");

        // Create directory if it doesn't exist
        if (!Directory.Exists(reportDir)) Directory.CreateDirectory(reportDir);

        var combinedReport = new List<string>
        {
            "===============================================",
            "            CRASH LOG SCAN SUMMARY",
            "===============================================",
            "",
            $"Total Files Processed: {statistics.Scanned}",
            $"Successful Scans: {statistics.Scanned - statistics.Failed}",
            $"Failed Scans: {statistics.Failed}",
            $"Incomplete Logs: {statistics.Incomplete}",
            "",
            "===============================================",
            ""
        };

        if (statistics.FailedFiles.Count > 0)
        {
            combinedReport.Add("FAILED SCANS:");
            combinedReport.AddRange(statistics.FailedFiles.Select(f => $"  - {f}"));
            combinedReport.Add("");
        }

        // Add summary of common issues found
        var commonIssues = AnalyzeCommonIssues(results);
        if (commonIssues.Count > 0)
        {
            combinedReport.Add("COMMON ISSUES DETECTED:");
            combinedReport.AddRange(commonIssues);
            combinedReport.Add("");
        }

        var summaryPath = Path.Combine(reportDir, "SCAN_SUMMARY.md");
        await File.WriteAllLinesAsync(summaryPath, combinedReport);
    }

    /// <summary>
    ///     Moves failed log to appropriate folder.
    /// </summary>
    private async Task MoveFailedLogAsync(string logFileName)
    {
        try
        {
            var sourceFile = logFileName;
            var targetDir = Path.Combine(Directory.GetCurrentDirectory(), "Crash Logs", "Failed Scans");

            Directory.CreateDirectory(targetDir);

            var targetFile = Path.Combine(targetDir, Path.GetFileName(logFileName));

            if (File.Exists(sourceFile) && !File.Exists(targetFile))
                await Task.Run(() => File.Copy(sourceFile, targetFile));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error moving failed log {LogFile}", logFileName);
        }
    }

    /// <summary>
    ///     Analyzes common issues across all scan results.
    /// </summary>
    private List<string> AnalyzeCommonIssues(List<CrashLogProcessResult> results)
    {
        var issues = new List<string>();
        var incompleteCount = results.Count(r => r.Statistics.GetValueOrDefault("incomplete", 0) > 0);
        var noPluginsCount = results.Count(r => r.Report.Any(line => line.Contains("NO PLUGINS FOUND")));

        if (incompleteCount > 0) issues.Add($"  - {incompleteCount} logs appear to be incomplete");

        if (noPluginsCount > 0) issues.Add($"  - {noPluginsCount} logs have no plugins detected");

        // Add more analysis as needed

        return issues;
    }
}