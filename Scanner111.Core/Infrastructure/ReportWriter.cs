using System.Collections.Concurrent;
using System.Text;
using Microsoft.Extensions.Logging;
using Scanner111.Core.Models;

namespace Scanner111.Core.Infrastructure;

/// <summary>
/// Provides functionality for writing scan reports to a file system, with support for concurrent write prevention
/// and logging of operations. Implements the IReportWriter interface.
/// </summary>
public class ReportWriter : IReportWriter
{
    private static readonly ConcurrentDictionary<string, SemaphoreSlim> FileLocks = new();
    private readonly ILogger<ReportWriter> _logger;

    public ReportWriter(ILogger<ReportWriter> logger)
    {
        _logger = logger;
    }

    /// <summary>
    /// Writes a report based on the provided scan result.
    /// </summary>
    /// <param name="scanResult">The scan result containing the data for the report.</param>
    /// <param name="cancellationToken">An optional cancellation token to cancel the operation.</param>
    /// <returns>A task that represents the asynchronous write operation. The task result contains a boolean indicating whether the report was written successfully.</returns>
    public async Task<bool> WriteReportAsync(ScanResult scanResult, CancellationToken cancellationToken = default)
    {
        return await WriteReportAsync(scanResult, scanResult.OutputPath, cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// Writes a report to a specified output path based on the provided scan result.
    /// </summary>
    /// <param name="scanResult">The scan result containing data to generate the report content.</param>
    /// <param name="outputPath">The file path where the report will be written.</param>
    /// <param name="cancellationToken">An optional token to observe while waiting for the task to complete.</param>
    /// <returns>A task that represents the asynchronous write operation. The task result contains a boolean indicating whether the operation was successful.</returns>
    public async Task<bool> WriteReportAsync(ScanResult scanResult, string outputPath,
        CancellationToken cancellationToken = default)
    {
        // Get or create a semaphore for this specific file path to prevent concurrent writes
        var normalizedPath = Path.GetFullPath(outputPath).ToLowerInvariant();
        var fileLock = FileLocks.GetOrAdd(normalizedPath, _ => new SemaphoreSlim(1, 1));

        await fileLock.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            var reportContent = FilterReportContent(scanResult.ReportText);

            var directory = Path.GetDirectoryName(outputPath);
            if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory)) Directory.CreateDirectory(directory);

            await File.WriteAllTextAsync(outputPath, reportContent, Encoding.UTF8, cancellationToken).ConfigureAwait(false);

            _logger.LogInformation("Report written successfully to: {OutputPath}", outputPath);
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to write report to: {OutputPath}", outputPath);
            return false;
        }
        finally
        {
            fileLock.Release();
        }
    }

    /// <summary>
    /// Filters the provided report content to remove OPC-related sections.
    /// </summary>
    /// <param name="reportText">The raw text of the report to be filtered.</param>
    /// <returns>The filtered text with OPC-related sections removed.</returns>
    private static string FilterReportContent(string reportText)
    {
        if (string.IsNullOrEmpty(reportText))
            return reportText;

        var lines = reportText.Split('\n');
        var filteredLines = new List<string>();
        var skipSection = false;

        for (var i = 0; i < lines.Length; i++)
        {
            var line = lines[i];

            // Check if this is the start of an OPC section
            if (line.Contains("CHECKING FOR MODS THAT ARE PATCHED THROUGH OPC INSTALLER") ||
                line.Contains("MODS PATCHED THROUGH OPC INSTALLER"))
            {
                skipSection = true;
                // Skip the separator line before this section if it exists
                if (filteredLines.Count > 0 && filteredLines[^1].StartsWith("===="))
                    filteredLines.RemoveAt(filteredLines.Count - 1);
                continue;
            }

            // Check if we're at the end of the OPC section (next section starts)
            if (skipSection && line.StartsWith("====") && i + 1 < lines.Length)
            {
                var nextLine = lines[i + 1];
                if (!nextLine.Contains("OPC") &&
                    !nextLine.Contains("FOUND NO PROBLEMATIC MODS THAT ARE ALREADY PATCHED"))
                {
                    skipSection = false;
                    // Include this separator line for the next section
                    filteredLines.Add(line);
                    continue;
                }
            }

            // Skip lines while in OPC section
            if (skipSection)
                continue;

            filteredLines.Add(line);
        }

        return string.Join('\n', filteredLines);
    }
}