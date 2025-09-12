using Microsoft.Extensions.Logging;
using Scanner111.Core.Analysis;
using Scanner111.Core.Reporting;

namespace Scanner111.CLI.Services;

/// <summary>
/// Service for generating reports from analysis results.
/// </summary>
public class ReportGeneratorService : IReportGeneratorService
{
    private readonly IAdvancedReportGenerator _reportGenerator;
    private readonly ILogger<ReportGeneratorService> _logger;
    
    /// <summary>
    /// Initializes a new instance of the <see cref="ReportGeneratorService"/> class.
    /// </summary>
    public ReportGeneratorService(
        IAdvancedReportGenerator reportGenerator,
        ILogger<ReportGeneratorService> logger)
    {
        _reportGenerator = reportGenerator ?? throw new ArgumentNullException(nameof(reportGenerator));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }
    
    /// <summary>
    /// Generates a report from analysis results.
    /// </summary>
    public async Task<string> GenerateReportAsync(
        IEnumerable<AnalysisResult> results,
        ReportFormat format,
        CancellationToken cancellationToken = default)
    {
        try
        {
            _logger.LogInformation("Generating report in {Format} format", format);
            
            // Convert ReportFormat to ReportTemplate
            var template = format switch
            {
                ReportFormat.Markdown => ReportTemplate.Predefined.Technical,
                ReportFormat.PlainText => ReportTemplate.Predefined.Technical,
                ReportFormat.Html => ReportTemplate.Predefined.Technical,
                ReportFormat.Json => ReportTemplate.Predefined.Technical,
                _ => ReportTemplate.Predefined.Technical
            };
            
            var options = new AdvancedReportOptions
            {
                Format = format
            };
            
            // The IAdvancedReportGenerator generates reports from AnalysisResults
            var report = await _reportGenerator.GenerateReportAsync(results, template, options, cancellationToken);
            
            _logger.LogInformation("Report generated successfully");
            return report;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to generate report");
            throw;
        }
    }
}