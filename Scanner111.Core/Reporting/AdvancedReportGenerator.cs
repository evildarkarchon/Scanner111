using System.Collections.Concurrent;
using System.Diagnostics;
using System.Text;
using Microsoft.Extensions.Logging;
using Scanner111.Core.Analysis;

namespace Scanner111.Core.Reporting;

/// <summary>
/// Provides advanced report generation with templates, standardized sections,
/// and intelligent ordering.
/// </summary>
public sealed class AdvancedReportGenerator : IAdvancedReportGenerator
{
    private readonly ILogger<AdvancedReportGenerator> _logger;
    private readonly IReportComposer _reportComposer;
    private readonly ConcurrentDictionary<string, ReportTemplate> _templates;

    public AdvancedReportGenerator(
        ILogger<AdvancedReportGenerator> logger,
        IReportComposer reportComposer)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _reportComposer = reportComposer ?? throw new ArgumentNullException(nameof(reportComposer));
        _templates = new ConcurrentDictionary<string, ReportTemplate>();

        // Register predefined templates
        RegisterPredefinedTemplates();
    }

    /// <inheritdoc />
    public async Task<string> GenerateReportAsync(
        IEnumerable<AnalysisResult> results,
        ReportTemplate template,
        AdvancedReportOptions? options = null,
        CancellationToken cancellationToken = default)
    {
        options ??= new AdvancedReportOptions();
        var resultsList = results?.ToList() ?? new List<AnalysisResult>();

        if (!resultsList.Any())
        {
            _logger.LogWarning("No analysis results provided for report generation");
            return GenerateEmptyReport(template, options);
        }

        var sw = Stopwatch.StartNew();

        // Generate statistics if required
        ReportStatistics? statistics = null;
        if (template.IncludeStatistics)
        {
            statistics = await GenerateStatisticsAsync(resultsList).ConfigureAwait(false);
        }

        // Build report fragments
        var fragments = new List<ReportFragment>();

        // Add header
        fragments.Add(CreateReportHeader(template, options));

        // Add table of contents if requested
        if (options.IncludeTableOfContents)
        {
            fragments.Add(CreateTableOfContents(template));
        }

        // Add executive summary if required
        if (template.IncludeExecutiveSummary)
        {
            fragments.Add(await CreateExecutiveSummaryAsync(resultsList, statistics).ConfigureAwait(false));
        }

        // Process each section in the template
        foreach (var section in template.Sections.OrderBy(s => s.Order))
        {
            cancellationToken.ThrowIfCancellationRequested();

            var sectionFragment = await CreateSectionFragmentAsync(
                section, resultsList, template, options).ConfigureAwait(false);

            if (sectionFragment != null && sectionFragment.HasContent())
            {
                fragments.Add(sectionFragment);
            }
        }

        // Add statistics if required
        if (template.IncludeStatistics && statistics != null)
        {
            fragments.Add(CreateStatisticsFragment(statistics));
        }

        // Add footer
        fragments.Add(CreateReportFooter(options, sw.Elapsed));

        // Compose the final report
        var composedOptions = new ReportOptions
        {
            Format = options.Format,
            Title = options.Title ?? template.Name,
            SortByOrder = template.UseSeverityOrdering,
            MinimumVisibility = FragmentVisibility.Always,
            IncludeTimingInfo = template.IncludeTimingInfo,
            IncludeMetadata = options.IncludeMetadata
        };

        var report = await _reportComposer.ComposeFromFragmentsAsync(
            fragments, composedOptions).ConfigureAwait(false);

        sw.Stop();
        _logger.LogInformation(
            "Generated {TemplateName} report with {FragmentCount} fragments in {Duration}ms",
            template.Name, fragments.Count, sw.ElapsedMilliseconds);

        return report;
    }

    /// <inheritdoc />
    public async Task<string> GenerateFromFragmentsAsync(
        IEnumerable<ReportFragment> fragments,
        ReportTemplate template,
        AdvancedReportOptions? options = null,
        CancellationToken cancellationToken = default)
    {
        options ??= new AdvancedReportOptions();
        var fragmentsList = fragments?.ToList() ?? new List<ReportFragment>();

        // Organize fragments by severity if template uses severity ordering
        if (template.UseSeverityOrdering)
        {
            fragmentsList = OrganizeFragmentsBySeverity(fragmentsList, template.MinimumSeverity);
        }

        // Filter by minimum severity
        if (template.MinimumSeverity > AnalysisSeverity.None)
        {
            fragmentsList = FilterFragmentsBySeverity(fragmentsList, template.MinimumSeverity);
        }

        // Add standardized wrapper sections
        var wrappedFragments = new List<ReportFragment>();

        // Add header
        wrappedFragments.Add(CreateReportHeader(template, options));

        // Group fragments by section if template has sections
        if (template.Sections.Any())
        {
            var groupedFragments = GroupFragmentsBySections(fragmentsList, template.Sections);
            wrappedFragments.AddRange(groupedFragments);
        }
        else
        {
            wrappedFragments.AddRange(fragmentsList);
        }

        // Add footer
        wrappedFragments.Add(CreateReportFooter(options, TimeSpan.Zero));

        // Compose the final report
        var composedOptions = new ReportOptions
        {
            Format = options.Format,
            Title = options.Title ?? template.Name,
            SortByOrder = template.UseSeverityOrdering,
            MinimumVisibility = FragmentVisibility.Always,
            IncludeTimingInfo = template.IncludeTimingInfo,
            IncludeMetadata = options.IncludeMetadata
        };

        return await _reportComposer.ComposeFromFragmentsAsync(
            wrappedFragments, composedOptions).ConfigureAwait(false);
    }

    /// <inheritdoc />
    public async Task<ReportStatistics> GenerateStatisticsAsync(IEnumerable<AnalysisResult> results)
    {
        var resultsList = results?.ToList() ?? new List<AnalysisResult>();

        var severityCounts = new Dictionary<AnalysisSeverity, int>
        {
            [AnalysisSeverity.None] = 0,
            [AnalysisSeverity.Info] = 0,
            [AnalysisSeverity.Warning] = 0,
            [AnalysisSeverity.Error] = 0,
            [AnalysisSeverity.Critical] = 0
        };

        foreach (var result in resultsList)
        {
            severityCounts[result.Severity]++;
        }

        var durations = resultsList
            .Where(r => r.Duration.HasValue)
            .Select(r => r.Duration!.Value)
            .ToList();

        var slowest = resultsList
            .Where(r => r.Duration.HasValue)
            .OrderByDescending(r => r.Duration!.Value)
            .FirstOrDefault();

        var fastest = resultsList
            .Where(r => r.Duration.HasValue)
            .OrderBy(r => r.Duration!.Value)
            .FirstOrDefault();

        return await Task.FromResult(new ReportStatistics
        {
            TotalAnalyzers = resultsList.Count,
            SuccessfulAnalyzers = resultsList.Count(r => r.Success),
            FailedAnalyzers = resultsList.Count(r => !r.Success),
            SkippedAnalyzers = resultsList.Count(r => r.SkipFurtherProcessing),
            SeverityCounts = severityCounts,
            TotalDuration = durations.Any() ? 
                TimeSpan.FromMilliseconds(durations.Sum(d => d.TotalMilliseconds)) : 
                TimeSpan.Zero,
            AverageDuration = durations.Any() ? 
                TimeSpan.FromMilliseconds(durations.Average(d => d.TotalMilliseconds)) : 
                TimeSpan.Zero,
            SlowestAnalyzer = slowest != null ? 
                (slowest.AnalyzerName, slowest.Duration!.Value) : 
                ("N/A", TimeSpan.Zero),
            FastestAnalyzer = fastest != null ? 
                (fastest.AnalyzerName, fastest.Duration!.Value) : 
                ("N/A", TimeSpan.Zero),
            TotalFragments = resultsList.Count(r => r.Fragment != null),
            TotalErrors = resultsList.Sum(r => r.Errors.Count),
            TotalWarnings = resultsList.Sum(r => r.Warnings.Count),
            GeneratedAt = DateTime.UtcNow
        });
    }

    /// <inheritdoc />
    public IReadOnlyCollection<ReportTemplate> GetAvailableTemplates()
    {
        return _templates.Values.ToList().AsReadOnly();
    }

    /// <inheritdoc />
    public void RegisterTemplate(ReportTemplate template)
    {
        ArgumentNullException.ThrowIfNull(template);
        
        if (string.IsNullOrWhiteSpace(template.Id))
        {
            throw new ArgumentException("Template must have a valid ID", nameof(template));
        }

        _templates.AddOrUpdate(template.Id, template, (_, _) => template);
        _logger.LogInformation("Registered report template: {TemplateId}", template.Id);
    }

    private void RegisterPredefinedTemplates()
    {
        RegisterTemplate(ReportTemplate.Predefined.Executive);
        RegisterTemplate(ReportTemplate.Predefined.Technical);
        RegisterTemplate(ReportTemplate.Predefined.Summary);
        RegisterTemplate(ReportTemplate.Predefined.Full);
    }

    private ReportFragment CreateReportHeader(ReportTemplate template, AdvancedReportOptions options)
    {
        var builder = ReportFragmentBuilder.Create()
            .WithTitle($"{template.Name} Report")
            .WithType(FragmentType.Header)
            .WithOrder(0);

        if (!string.IsNullOrWhiteSpace(options.ApplicationVersion))
        {
            builder.AppendLine($"Application Version: {options.ApplicationVersion}");
        }

        builder.AppendLine($"Generated: {DateTime.UtcNow.ToString(options.TimestampFormat)} UTC");
        
        if (options.CustomMetadata.Any())
        {
            builder.AppendSeparator();
            foreach (var (key, value) in options.CustomMetadata)
            {
                builder.AppendLine($"{key}: {value}");
            }
        }

        return builder.Build();
    }

    private ReportFragment CreateTableOfContents(ReportTemplate template)
    {
        var builder = ReportFragmentBuilder.Create()
            .WithTitle("Table of Contents")
            .WithType(FragmentType.Section)
            .WithOrder(5);

        foreach (var section in template.Sections.OrderBy(s => s.Order))
        {
            builder.AppendLine($"- {section.Title}");
        }

        return builder.Build();
    }

    private async Task<ReportFragment> CreateExecutiveSummaryAsync(
        List<AnalysisResult> results,
        ReportStatistics? statistics)
    {
        var builder = ReportFragmentBuilder.Create()
            .WithTitle("Executive Summary")
            .WithType(FragmentType.Section)
            .WithOrder(10);

        // Summary statistics
        if (statistics != null)
        {
            builder.AppendLine($"**Analysis Overview:**");
            builder.AppendLine($"- Total Analyzers Run: {statistics.TotalAnalyzers}");
            builder.AppendLine($"- Successful: {statistics.SuccessfulAnalyzers}");
            builder.AppendLine($"- Failed: {statistics.FailedAnalyzers}");
            builder.AppendLine();
        }

        // Critical findings
        var criticalCount = results.Count(r => r.Severity >= AnalysisSeverity.Error);
        var warningCount = results.Count(r => r.Severity == AnalysisSeverity.Warning);
        
        builder.AppendLine("**Key Findings:**");
        
        if (criticalCount > 0)
        {
            builder.AppendLine($"- ⚠️ {criticalCount} critical issue(s) requiring immediate attention");
        }
        
        if (warningCount > 0)
        {
            builder.AppendLine($"- ⚠️ {warningCount} warning(s) that should be addressed");
        }
        
        if (criticalCount == 0 && warningCount == 0)
        {
            builder.AppendLine("- ✅ No critical issues or warnings detected");
        }

        // Top recommendations
        var recommendations = ExtractRecommendations(results).Take(3).ToList();
        if (recommendations.Any())
        {
            builder.AppendLine();
            builder.AppendLine("**Top Recommendations:**");
            foreach (var rec in recommendations)
            {
                builder.AppendLine($"- {rec}");
            }
        }

        return await Task.FromResult(builder.Build());
    }

    private async Task<ReportFragment?> CreateSectionFragmentAsync(
        ReportSection section,
        List<AnalysisResult> results,
        ReportTemplate template,
        AdvancedReportOptions options)
    {
        // Filter results based on section requirements
        var filteredResults = results;

        if (section.MinimumSeverity.HasValue)
        {
            filteredResults = results
                .Where(r => r.Severity >= section.MinimumSeverity.Value)
                .ToList();
        }

        if (!filteredResults.Any() && !section.IsRequired)
        {
            return null;
        }

        // Create section based on its type
        return section.Id switch
        {
            "overview" => CreateOverviewSection(results),
            "critical-issues" => CreateCriticalIssuesSection(filteredResults),
            "warnings" => CreateWarningsSection(filteredResults),
            "information" => CreateInformationSection(filteredResults),
            "technical-details" => CreateTechnicalDetailsSection(filteredResults),
            "recommendations" => CreateRecommendationsSection(results),
            "performance" => CreatePerformanceSection(results),
            _ => await CreateGenericSectionAsync(section, filteredResults)
        };
    }

    private ReportFragment CreateOverviewSection(List<AnalysisResult> results)
    {
        var builder = ReportFragmentBuilder.Create()
            .WithTitle("Analysis Overview")
            .WithType(FragmentType.Section)
            .WithOrder(100);

        builder.AppendLine($"Analyzed {results.Count} component(s)");
        builder.AppendLine($"Success Rate: {results.Count(r => r.Success) * 100.0 / results.Count:F1}%");
        
        var topAnalyzers = results
            .Where(r => r.Success)
            .Take(5)
            .Select(r => r.AnalyzerName);
        
        builder.AppendLine();
        builder.AppendLine("**Components Analyzed:**");
        foreach (var analyzer in topAnalyzers)
        {
            builder.AppendLine($"- {analyzer}");
        }

        return builder.Build();
    }

    private ReportFragment CreateCriticalIssuesSection(List<AnalysisResult> results)
    {
        var criticalResults = results
            .Where(r => r.Severity >= AnalysisSeverity.Error)
            .OrderByDescending(r => r.Severity)
            .ToList();

        if (!criticalResults.Any())
        {
            return ReportFragment.CreateInfo("Critical Issues", "No critical issues detected.", 200);
        }

        var fragments = criticalResults
            .Select(r => r.Fragment)
            .Where(f => f != null)
            .Cast<ReportFragment>()
            .ToList();

        return ReportFragment.CreateWithChildren("Critical Issues", fragments, 200);
    }

    private ReportFragment CreateWarningsSection(List<AnalysisResult> results)
    {
        var warningResults = results
            .Where(r => r.Severity == AnalysisSeverity.Warning)
            .ToList();

        if (!warningResults.Any())
        {
            return ReportFragment.CreateInfo("Warnings", "No warnings detected.", 300);
        }

        var fragments = warningResults
            .Select(r => r.Fragment)
            .Where(f => f != null)
            .Cast<ReportFragment>()
            .ToList();

        return ReportFragment.CreateWithChildren("Warnings", fragments, 300);
    }

    private ReportFragment CreateInformationSection(List<AnalysisResult> results)
    {
        var infoResults = results
            .Where(r => r.Severity <= AnalysisSeverity.Info && r.Fragment != null)
            .ToList();

        if (!infoResults.Any())
        {
            return ReportFragment.Empty();
        }

        var fragments = infoResults
            .Select(r => r.Fragment!)
            .ToList();

        return ReportFragment.CreateWithChildren("Informational", fragments, 400);
    }

    private ReportFragment CreateTechnicalDetailsSection(List<AnalysisResult> results)
    {
        var builder = ReportFragmentBuilder.Create()
            .WithTitle("Technical Details")
            .WithType(FragmentType.Section)
            .WithOrder(500);

        foreach (var result in results.Where(r => r.Metadata.Any()))
        {
            builder.AppendLine($"**{result.AnalyzerName}:**");
            foreach (var (key, value) in result.Metadata)
            {
                builder.AppendLine($"  - {key}: {value}");
            }
            builder.AppendLine();
        }

        return builder.BuildIfNotEmpty() ?? ReportFragment.Empty();
    }

    private ReportFragment CreateRecommendationsSection(List<AnalysisResult> results)
    {
        var recommendations = ExtractRecommendations(results);

        if (!recommendations.Any())
        {
            return ReportFragment.Empty();
        }

        var builder = ReportFragmentBuilder.Create()
            .WithTitle("Recommendations")
            .WithType(FragmentType.Section)
            .WithOrder(600);

        foreach (var recommendation in recommendations)
        {
            builder.AppendLine($"• {recommendation}");
        }

        return builder.Build();
    }

    private ReportFragment CreatePerformanceSection(List<AnalysisResult> results)
    {
        var resultsWithDuration = results
            .Where(r => r.Duration.HasValue)
            .OrderByDescending(r => r.Duration!.Value)
            .ToList();

        if (!resultsWithDuration.Any())
        {
            return ReportFragment.Empty();
        }

        var builder = ReportFragmentBuilder.Create()
            .WithTitle("Performance Metrics")
            .WithType(FragmentType.Section)
            .WithOrder(700);

        builder.AppendLine("| Analyzer | Duration |");
        builder.AppendLine("|----------|----------|");

        foreach (var result in resultsWithDuration.Take(10))
        {
            builder.AppendLine($"| {result.AnalyzerName} | {result.Duration:mm\\:ss\\.fff} |");
        }

        return builder.Build();
    }

    private async Task<ReportFragment> CreateGenericSectionAsync(
        ReportSection section,
        List<AnalysisResult> results)
    {
        var fragments = results
            .Select(r => r.Fragment)
            .Where(f => f != null)
            .Cast<ReportFragment>()
            .ToList();

        if (!fragments.Any())
        {
            return await Task.FromResult(ReportFragment.Empty());
        }

        return ReportFragment.CreateWithChildren(section.Title, fragments, section.Order);
    }

    private ReportFragment CreateStatisticsFragment(ReportStatistics statistics)
    {
        var builder = ReportFragmentBuilder.Create()
            .WithTitle("Report Statistics")
            .WithType(FragmentType.Info)
            .WithOrder(800);

        builder.AppendLine("**Analysis Summary:**");
        builder.AppendLine($"- Total Analyzers: {statistics.TotalAnalyzers}");
        builder.AppendLine($"- Successful: {statistics.SuccessfulAnalyzers}");
        builder.AppendLine($"- Failed: {statistics.FailedAnalyzers}");
        builder.AppendLine($"- Skipped: {statistics.SkippedAnalyzers}");
        builder.AppendLine();

        builder.AppendLine("**Issue Distribution:**");
        foreach (var (severity, count) in statistics.SeverityCounts.Where(kvp => kvp.Value > 0))
        {
            builder.AppendLine($"- {severity}: {count}");
        }
        builder.AppendLine();

        builder.AppendLine("**Performance:**");
        builder.AppendLine($"- Total Duration: {statistics.TotalDuration:mm\\:ss\\.fff}");
        builder.AppendLine($"- Average Duration: {statistics.AverageDuration:mm\\:ss\\.fff}");
        builder.AppendLine($"- Slowest: {statistics.SlowestAnalyzer.Name} ({statistics.SlowestAnalyzer.Duration:mm\\:ss\\.fff})");
        builder.AppendLine($"- Fastest: {statistics.FastestAnalyzer.Name} ({statistics.FastestAnalyzer.Duration:mm\\:ss\\.fff})");

        return builder.Build();
    }

    private ReportFragment CreateReportFooter(AdvancedReportOptions options, TimeSpan generationTime)
    {
        var builder = ReportFragmentBuilder.Create()
            .WithTitle(string.Empty)
            .WithType(FragmentType.Info)
            .WithOrder(999);

        builder.AppendSeparator();
        builder.AppendLine($"Report generated by Scanner111 Advanced Report Generator");
        
        if (generationTime > TimeSpan.Zero)
        {
            builder.AppendLine($"Generation time: {generationTime:mm\\:ss\\.fff}");
        }

        builder.AppendLine($"Timestamp: {DateTime.UtcNow.ToString(options.TimestampFormat)} UTC");

        return builder.Build();
    }

    private List<ReportFragment> OrganizeFragmentsBySeverity(
        List<ReportFragment> fragments,
        AnalysisSeverity minimumSeverity)
    {
        // Group fragments by type and reorder based on severity
        var errorFragments = fragments.Where(f => f.Type == FragmentType.Error).ToList();
        var warningFragments = fragments.Where(f => f.Type == FragmentType.Warning).ToList();
        var infoFragments = fragments.Where(f => f.Type == FragmentType.Info).ToList();
        var otherFragments = fragments
            .Where(f => f.Type != FragmentType.Error && 
                       f.Type != FragmentType.Warning && 
                       f.Type != FragmentType.Info)
            .ToList();

        var organized = new List<ReportFragment>();
        organized.AddRange(errorFragments);
        organized.AddRange(warningFragments);
        organized.AddRange(infoFragments);
        organized.AddRange(otherFragments);

        return organized;
    }

    private List<ReportFragment> FilterFragmentsBySeverity(
        List<ReportFragment> fragments,
        AnalysisSeverity minimumSeverity)
    {
        // Map fragment types to severity levels
        return fragments.Where(f =>
        {
            var severity = f.Type switch
            {
                FragmentType.Error => AnalysisSeverity.Error,
                FragmentType.Warning => AnalysisSeverity.Warning,
                FragmentType.Info => AnalysisSeverity.Info,
                _ => AnalysisSeverity.None
            };

            return severity >= minimumSeverity;
        }).ToList();
    }

    private List<ReportFragment> GroupFragmentsBySections(
        List<ReportFragment> fragments,
        IReadOnlyList<ReportSection> sections)
    {
        var grouped = new List<ReportFragment>();

        foreach (var section in sections.OrderBy(s => s.Order))
        {
            var sectionFragments = fragments
                .Where(f => MatchesSection(f, section))
                .ToList();

            if (sectionFragments.Any())
            {
                grouped.Add(ReportFragment.CreateWithChildren(
                    section.Title, sectionFragments, section.Order));
            }
        }

        // Add any fragments that don't match a section at the end
        var unmatchedFragments = fragments
            .Where(f => !sections.Any(s => MatchesSection(f, s)))
            .ToList();

        grouped.AddRange(unmatchedFragments);

        return grouped;
    }

    private bool MatchesSection(ReportFragment fragment, ReportSection section)
    {
        // Match based on fragment type and section requirements
        if (section.MinimumSeverity.HasValue)
        {
            var fragmentSeverity = fragment.Type switch
            {
                FragmentType.Error => AnalysisSeverity.Critical,
                FragmentType.Warning => AnalysisSeverity.Warning,
                FragmentType.Info => AnalysisSeverity.Info,
                _ => AnalysisSeverity.None
            };

            return fragmentSeverity >= section.MinimumSeverity.Value;
        }

        // Default matching logic based on section ID
        return section.Id switch
        {
            "critical-issues" => fragment.Type == FragmentType.Error,
            "warnings" => fragment.Type == FragmentType.Warning,
            "information" => fragment.Type == FragmentType.Info,
            _ => true
        };
    }

    private List<string> ExtractRecommendations(List<AnalysisResult> results)
    {
        var recommendations = new List<string>();

        // Extract recommendations from metadata
        foreach (var result in results)
        {
            if (result.Metadata.TryGetValue("recommendation", out var value) && 
                value is string recommendation &&
                !string.IsNullOrWhiteSpace(recommendation))
            {
                recommendations.Add(recommendation);
            }
        }

        // Add generic recommendations based on severity
        if (results.Any(r => r.Severity >= AnalysisSeverity.Critical))
        {
            recommendations.Add("Address critical issues immediately to prevent system instability");
        }

        if (results.Count(r => r.Severity == AnalysisSeverity.Warning) > 5)
        {
            recommendations.Add("Review and resolve warnings to improve system reliability");
        }

        if (results.Any(r => !r.Success))
        {
            recommendations.Add("Investigate analyzer failures to ensure complete analysis coverage");
        }

        return recommendations.Distinct().ToList();
    }

    private string GenerateEmptyReport(ReportTemplate template, AdvancedReportOptions options)
    {
        var builder = new StringBuilder();
        builder.AppendLine($"# {template.Name} Report");
        builder.AppendLine();
        builder.AppendLine($"Generated: {DateTime.UtcNow.ToString(options.TimestampFormat)} UTC");
        builder.AppendLine();
        builder.AppendLine("No analysis results available.");
        return builder.ToString();
    }
}