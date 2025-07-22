using Scanner111.Core.Infrastructure;
using Scanner111.Core.Models;
using CliScanOptions = Scanner111.CLI.Models.ScanOptions;

namespace Scanner111.CLI.Services;

public class ScanResultProcessor : IScanResultProcessor
{
    /// <summary>
    /// Processes the scan result and performs operations such as displaying findings,
    /// and auto-saving reports based on the provided options and application settings.
    /// </summary>
    /// <param name="result">The result of the scan containing analysis and log details.</param>
    /// <param name="options">The scan options specifying how the results should be handled.</param>
    /// <param name="reportWriter">The writer used to persist the scan report to a file.</param>
    /// <param name="xseCopiedFiles">A set of file paths representing logs copied from XSE logs.</param>
    /// <param name="settings">The application settings controlling behavior such as report auto-saving.</param>
    /// <returns>A task that represents the asynchronous operation.</returns>
    public async Task ProcessScanResultAsync(ScanResult result, CliScanOptions options, IReportWriter reportWriter,
        HashSet<string> xseCopiedFiles, ApplicationSettings settings)
    {
        if (options.OutputFormat == "summary")
            // Just collect for summary at the end
            return;

        // Show scan status without printing full report (reports are auto-saved to files)
        if (result.AnalysisResults.Any(r => r.HasFindings))
            MessageHandler.MsgWarning(
                $"Found {result.AnalysisResults.Count(r => r.HasFindings)} issues in {Path.GetFileName(result.LogPath)}");
        else
            MessageHandler.MsgSuccess($"No issues found in {Path.GetFileName(result.LogPath)}");

        // Auto-save report to file (unless disabled by setting)
        var autoSaveReports = settings.AutoSaveResults;
        if (autoSaveReports && !string.IsNullOrEmpty(result.ReportText))
            try
            {
                string outputPath;
                if (result.WasCopiedFromXse)
                    // For XSE copied files, use the default OutputPath (alongside the copied log)
                    outputPath = result.OutputPath;
                else
                    // For non-XSE files (directly scanned), write report alongside the source log
                    outputPath = Path.ChangeExtension(result.LogPath, null) + "-AUTOSCAN.md";

                var success = await reportWriter.WriteReportAsync(result, outputPath);
                if (!success)
                    MessageHandler.MsgWarning($"Failed to save report for {Path.GetFileName(result.LogPath)}");
            }
            catch (Exception ex)
            {
                MessageHandler.MsgError($"Error saving report: {ex.Message}");
            }
    }
}