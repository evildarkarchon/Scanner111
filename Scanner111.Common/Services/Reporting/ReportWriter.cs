using Scanner111.Common.Models.Reporting;
using Scanner111.Common.Services.FileIO;

namespace Scanner111.Common.Services.Reporting;

/// <summary>
/// Writes crash log analysis reports to markdown files.
/// </summary>
public class ReportWriter : IReportWriter
{
    private readonly IFileIOService _fileIO;

    /// <summary>
    /// Initializes a new instance of the <see cref="ReportWriter"/> class.
    /// </summary>
    /// <param name="fileIO">The file I/O service for writing reports.</param>
    public ReportWriter(IFileIOService fileIO)
    {
        _fileIO = fileIO ?? throw new ArgumentNullException(nameof(fileIO));
    }

    /// <summary>
    /// Writes a report fragment to a markdown file corresponding to the crash log.
    /// </summary>
    /// <param name="crashLogPath">The path to the original crash log file.</param>
    /// <param name="report">The report fragment to write.</param>
    /// <param name="cancellationToken">Cancellation token for the async operation.</param>
    /// <returns>A task representing the async write operation.</returns>
    public async Task WriteReportAsync(
        string crashLogPath,
        ReportFragment report,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(crashLogPath))
        {
            throw new ArgumentException("Crash log path cannot be null or whitespace.", nameof(crashLogPath));
        }

        if (report == null)
        {
            throw new ArgumentNullException(nameof(report));
        }

        var reportPath = GetReportPath(crashLogPath);
        var content = string.Join("\n", report.Lines);

        await _fileIO.WriteFileAsync(reportPath, content, cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// Gets the report file path for a given crash log path.
    /// Converts "crash-12624.log" to "crash-12624-AUTOSCAN.md".
    /// </summary>
    /// <param name="crashLogPath">The path to the crash log file.</param>
    /// <returns>The path where the report should be written.</returns>
    public static string GetReportPath(string crashLogPath)
    {
        if (string.IsNullOrWhiteSpace(crashLogPath))
        {
            throw new ArgumentException("Crash log path cannot be null or whitespace.", nameof(crashLogPath));
        }

        var directory = Path.GetDirectoryName(crashLogPath) ?? string.Empty;
        var fileNameWithoutExt = Path.GetFileNameWithoutExtension(crashLogPath);
        var reportFileName = $"{fileNameWithoutExt}-AUTOSCAN.md";

        return Path.Combine(directory, reportFileName);
    }

    /// <summary>
    /// Checks if a report file already exists for a given crash log.
    /// </summary>
    /// <param name="crashLogPath">The path to the crash log file.</param>
    /// <param name="cancellationToken">Cancellation token for the async operation.</param>
    /// <returns>True if the report file exists, false otherwise.</returns>
    public async Task<bool> ReportExistsAsync(
        string crashLogPath,
        CancellationToken cancellationToken = default)
    {
        var reportPath = GetReportPath(crashLogPath);
        return await _fileIO.FileExistsAsync(reportPath).ConfigureAwait(false);
    }
}
