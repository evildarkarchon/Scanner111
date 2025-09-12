using Microsoft.Extensions.Logging;
using Scanner111.CLI.Configuration;
using Scanner111.CLI.Models;
using Scanner111.Core.Models;
using Scanner111.Core.Analysis;
using Scanner111.Core.Configuration;
using Scanner111.Core.Orchestration;
using Scanner111.Core.Reporting;
using Scanner111.Core.Services;
using Spectre.Console;

namespace Scanner111.CLI.Services;

/// <summary>
/// Service for analyzing crash log files.
/// </summary>
public class AnalyzeService : IAnalyzeService
{
    private readonly IAnalyzerOrchestrator _orchestrator;
    private readonly IAnalyzerRegistry _registry;
    private readonly IReportGeneratorService _reportGenerator;
    private readonly ICliSettings _settings;
    private readonly IAsyncYamlSettingsCore _yamlCore;
    private readonly IAnsiConsole _console;
    private readonly ILogger<AnalyzeService> _logger;
    
    /// <summary>
    /// Initializes a new instance of the <see cref="AnalyzeService"/> class.
    /// </summary>
    public AnalyzeService(
        IAnalyzerOrchestrator orchestrator,
        IAnalyzerRegistry registry,
        IReportGeneratorService reportGenerator,
        ICliSettings settings,
        IAsyncYamlSettingsCore yamlCore,
        IAnsiConsole console,
        ILogger<AnalyzeService> logger)
    {
        _orchestrator = orchestrator ?? throw new ArgumentNullException(nameof(orchestrator));
        _registry = registry ?? throw new ArgumentNullException(nameof(registry));
        _reportGenerator = reportGenerator ?? throw new ArgumentNullException(nameof(reportGenerator));
        _settings = settings ?? throw new ArgumentNullException(nameof(settings));
        _yamlCore = yamlCore ?? throw new ArgumentNullException(nameof(yamlCore));
        _console = console ?? throw new ArgumentNullException(nameof(console));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }
    
    /// <summary>
    /// Analyzes a crash log file with the specified analyzers.
    /// </summary>
    public async Task AnalyzeFileAsync(
        string logFile,
        IEnumerable<string> analyzerNames,
        string? outputFile,
        ReportFormat format,
        CancellationToken cancellationToken = default)
    {
        try
        {
            _logger.LogInformation("Starting analysis of {LogFile}", logFile);
            
            // Validate file exists
            if (!File.Exists(logFile))
            {
                throw new FileNotFoundException($"Log file not found: {logFile}");
            }
            
            // Get analyzers
            var analyzers = await GetAnalyzersAsync(analyzerNames, cancellationToken);
            
            // Load settings to get default game type
            var settings = await _settings.LoadAsync(cancellationToken);
            
            // Create analysis context
            var context = new AnalysisContext(
                logFile,
                _yamlCore,
                AnalysisType.CrashLog);
            
            // Create analysis request
            var request = new AnalysisRequest
            {
                InputPath = logFile,
                AnalysisType = AnalysisType.CrashLog
            };
            
            // Run analysis
            _logger.LogInformation("Running {Count} analyzers", analyzers.Count);
            var orchestrationResult = await _orchestrator.RunAnalysisAsync(request, cancellationToken);
            var results = orchestrationResult.Results;
            
            // Generate report
            var report = await _reportGenerator.GenerateReportAsync(results, format, cancellationToken);
            
            // Save or display report
            if (!string.IsNullOrEmpty(outputFile))
            {
                await File.WriteAllTextAsync(outputFile, report, cancellationToken);
                _console.MarkupLine($"[green]✓[/] Report saved to: {outputFile}");
            }
            else
            {
                _console.WriteLine(report);
            }
            
            // Display summary
            DisplaySummary(results);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Analysis failed");
            _console.MarkupLine($"[red]✗[/] Analysis failed: {ex.Message}");
            throw;
        }
    }
    
    private async Task<List<IAnalyzer>> GetAnalyzersAsync(
        IEnumerable<string> analyzerNames,
        CancellationToken cancellationToken)
    {
        var allAnalyzers = await _registry.GetAllAsync(cancellationToken);
        
        if (!analyzerNames.Any())
        {
            // Use all analyzers if none specified
            return allAnalyzers.ToList();
        }
        
        // Filter to requested analyzers
        var analyzers = new List<IAnalyzer>();
        foreach (var name in analyzerNames)
        {
            var analyzer = allAnalyzers.FirstOrDefault(a => 
                a.Name.Equals(name, StringComparison.OrdinalIgnoreCase));
            
            if (analyzer == null)
            {
                _console.MarkupLine($"[yellow]⚠[/] Analyzer not found: {name}");
            }
            else
            {
                analyzers.Add(analyzer);
            }
        }
        
        if (!analyzers.Any())
        {
            throw new InvalidOperationException("No valid analyzers found");
        }
        
        return analyzers;
    }
    
    private void DisplaySummary(IEnumerable<AnalysisResult> results)
    {
        var resultList = results.ToList();
        var successCount = resultList.Count(r => r.Success);
        var failureCount = resultList.Count - successCount;
        
        _console.WriteLine();
        _console.MarkupLine("[yellow]Analysis Summary:[/]");
        _console.MarkupLine($"  • Total analyzers run: {resultList.Count}");
        _console.MarkupLine($"  • [green]Succeeded: {successCount}[/]");
        
        if (failureCount > 0)
        {
            _console.MarkupLine($"  • [red]Failed: {failureCount}[/]");
        }
        
        // Show severity counts
        var severityCounts = resultList
            .Where(r => r.Fragment != null)
            .GroupBy(r => r.Fragment!.GetSeverity())
            .OrderBy(g => g.Key);
        
        if (severityCounts.Any())
        {
            _console.WriteLine();
            _console.MarkupLine("[yellow]Issues Found:[/]");
            
            foreach (var group in severityCounts)
            {
                var color = group.Key switch
                {
                    Severity.Critical => "red",
                    Severity.Error => "red",
                    Severity.Warning => "yellow",
                    Severity.Info => "cyan",
                    _ => "white"
                };
                
                _console.MarkupLine($"  • [{color}]{group.Key}: {group.Count()}[/]");
            }
        }
    }
}