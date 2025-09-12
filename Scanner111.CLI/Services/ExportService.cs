using Microsoft.Extensions.Logging;
using Scanner111.Core.Analysis;
using Scanner111.Core.Reporting;

namespace Scanner111.CLI.Services;

/// <summary>
/// Service for exporting analysis results.
/// </summary>
public class ExportService : IExportService
{
    private readonly IReportGeneratorService _reportGenerator;
    private readonly ILogger<ExportService> _logger;
    
    /// <summary>
    /// Initializes a new instance of the <see cref="ExportService"/> class.
    /// </summary>
    public ExportService(
        IReportGeneratorService reportGenerator,
        ILogger<ExportService> logger)
    {
        _reportGenerator = reportGenerator ?? throw new ArgumentNullException(nameof(reportGenerator));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }
    
    /// <summary>
    /// Exports analysis results to a file.
    /// </summary>
    public async Task<string> ExportResultsAsync(
        IEnumerable<AnalysisResult> results,
        string fileName,
        ReportFormat format,
        CancellationToken cancellationToken = default)
    {
        try
        {
            _logger.LogInformation("Exporting results to {FileName} as {Format}", fileName, format);
            
            // Generate report content
            var report = await _reportGenerator.GenerateReportAsync(results, format, cancellationToken);
            
            // Determine file extension
            var extension = format switch
            {
                ReportFormat.Markdown => ".md",
                ReportFormat.Html => ".html",
                ReportFormat.Json => ".json",
                ReportFormat.PlainText => ".txt",
                _ => ".txt"
            };
            
            // Create full file path
            var fullPath = Path.GetFullPath(fileName);
            if (!Path.HasExtension(fullPath))
            {
                fullPath += extension;
            }
            
            // Ensure directory exists
            var directory = Path.GetDirectoryName(fullPath);
            if (!string.IsNullOrEmpty(directory))
            {
                Directory.CreateDirectory(directory);
            }
            
            // Write file
            await File.WriteAllTextAsync(fullPath, report, cancellationToken);
            
            _logger.LogInformation("Report exported successfully to {Path}", fullPath);
            return fullPath;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to export results");
            throw;
        }
    }
}