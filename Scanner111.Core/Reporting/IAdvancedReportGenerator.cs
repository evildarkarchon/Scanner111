using Scanner111.Core.Analysis;

namespace Scanner111.Core.Reporting;

/// <summary>
/// Provides advanced report generation capabilities with templates,
/// standardized sections, and intelligent ordering.
/// </summary>
public interface IAdvancedReportGenerator
{
    /// <summary>
    /// Generates a report using the specified template.
    /// </summary>
    /// <param name="results">Analysis results to include in the report.</param>
    /// <param name="template">The template to use for generation.</param>
    /// <param name="options">Additional report options.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The generated report as a string.</returns>
    Task<string> GenerateReportAsync(
        IEnumerable<AnalysisResult> results,
        ReportTemplate template,
        AdvancedReportOptions? options = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Generates a report from fragments using advanced features.
    /// </summary>
    /// <param name="fragments">Report fragments to compose.</param>
    /// <param name="template">The template to use for generation.</param>
    /// <param name="options">Additional report options.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The generated report as a string.</returns>
    Task<string> GenerateFromFragmentsAsync(
        IEnumerable<ReportFragment> fragments,
        ReportTemplate template,
        AdvancedReportOptions? options = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Generates report statistics from analysis results.
    /// </summary>
    /// <param name="results">Analysis results to analyze.</param>
    /// <returns>Statistics about the analysis.</returns>
    Task<ReportStatistics> GenerateStatisticsAsync(IEnumerable<AnalysisResult> results);

    /// <summary>
    /// Gets available report templates.
    /// </summary>
    IReadOnlyCollection<ReportTemplate> GetAvailableTemplates();

    /// <summary>
    /// Registers a custom report template.
    /// </summary>
    /// <param name="template">The template to register.</param>
    void RegisterTemplate(ReportTemplate template);
}

/// <summary>
/// Defines a report template with standardized sections and formatting.
/// </summary>
public class ReportTemplate
{
    /// <summary>
    /// Gets the unique identifier for this template.
    /// </summary>
    public string Id { get; init; } = string.Empty;

    /// <summary>
    /// Gets the display name of this template.
    /// </summary>
    public string Name { get; init; } = string.Empty;

    /// <summary>
    /// Gets the description of this template.
    /// </summary>
    public string Description { get; init; } = string.Empty;

    /// <summary>
    /// Gets the sections to include in this template.
    /// </summary>
    public IReadOnlyList<ReportSection> Sections { get; init; } = new List<ReportSection>();

    /// <summary>
    /// Gets whether to include an executive summary.
    /// </summary>
    public bool IncludeExecutiveSummary { get; init; }

    /// <summary>
    /// Gets whether to include statistics.
    /// </summary>
    public bool IncludeStatistics { get; init; }

    /// <summary>
    /// Gets whether to include timing information.
    /// </summary>
    public bool IncludeTimingInfo { get; init; }

    /// <summary>
    /// Gets whether to use severity-based ordering.
    /// </summary>
    public bool UseSeverityOrdering { get; init; } = true;

    /// <summary>
    /// Gets the minimum severity to include in the report.
    /// </summary>
    public AnalysisSeverity MinimumSeverity { get; init; } = AnalysisSeverity.None;

    /// <summary>
    /// Gets predefined templates.
    /// </summary>
    public static class Predefined
    {
        /// <summary>
        /// Executive summary template with only critical information.
        /// </summary>
        public static ReportTemplate Executive => new()
        {
            Id = "executive",
            Name = "Executive Summary",
            Description = "High-level overview for management",
            IncludeExecutiveSummary = true,
            IncludeStatistics = true,
            IncludeTimingInfo = false,
            UseSeverityOrdering = true,
            MinimumSeverity = AnalysisSeverity.Warning,
            Sections = new[]
            {
                ReportSection.ExecutiveSummary,
                ReportSection.CriticalIssues,
                ReportSection.Recommendations,
                ReportSection.Statistics
            }
        };

        /// <summary>
        /// Technical report with all details.
        /// </summary>
        public static ReportTemplate Technical => new()
        {
            Id = "technical",
            Name = "Technical Report",
            Description = "Detailed technical analysis",
            IncludeExecutiveSummary = false,
            IncludeStatistics = true,
            IncludeTimingInfo = true,
            UseSeverityOrdering = true,
            MinimumSeverity = AnalysisSeverity.None,
            Sections = new[]
            {
                ReportSection.Overview,
                ReportSection.CriticalIssues,
                ReportSection.Warnings,
                ReportSection.Information,
                ReportSection.TechnicalDetails,
                ReportSection.Performance,
                ReportSection.Statistics
            }
        };

        /// <summary>
        /// Summary report with key findings.
        /// </summary>
        public static ReportTemplate Summary => new()
        {
            Id = "summary",
            Name = "Summary Report",
            Description = "Concise summary of findings",
            IncludeExecutiveSummary = true,
            IncludeStatistics = false,
            IncludeTimingInfo = false,
            UseSeverityOrdering = true,
            MinimumSeverity = AnalysisSeverity.Info,
            Sections = new[]
            {
                ReportSection.ExecutiveSummary,
                ReportSection.CriticalIssues,
                ReportSection.Warnings,
                ReportSection.Recommendations
            }
        };

        /// <summary>
        /// Full report with all information.
        /// </summary>
        public static ReportTemplate Full => new()
        {
            Id = "full",
            Name = "Full Report",
            Description = "Complete analysis with all details",
            IncludeExecutiveSummary = true,
            IncludeStatistics = true,
            IncludeTimingInfo = true,
            UseSeverityOrdering = false,
            MinimumSeverity = AnalysisSeverity.None,
            Sections = new[]
            {
                ReportSection.ExecutiveSummary,
                ReportSection.Overview,
                ReportSection.CriticalIssues,
                ReportSection.Warnings,
                ReportSection.Information,
                ReportSection.TechnicalDetails,
                ReportSection.Recommendations,
                ReportSection.Performance,
                ReportSection.Statistics,
                ReportSection.Appendix
            }
        };
    }
}

/// <summary>
/// Defines a section within a report template.
/// </summary>
public class ReportSection
{
    /// <summary>
    /// Gets the section identifier.
    /// </summary>
    public string Id { get; init; } = string.Empty;

    /// <summary>
    /// Gets the section title.
    /// </summary>
    public string Title { get; init; } = string.Empty;

    /// <summary>
    /// Gets the display order for this section.
    /// </summary>
    public int Order { get; init; }

    /// <summary>
    /// Gets whether this section is required.
    /// </summary>
    public bool IsRequired { get; init; }

    /// <summary>
    /// Gets the minimum severity for items in this section.
    /// </summary>
    public AnalysisSeverity? MinimumSeverity { get; init; }

    // Predefined sections
    public static readonly ReportSection ExecutiveSummary = new()
    {
        Id = "executive-summary",
        Title = "Executive Summary",
        Order = 0,
        IsRequired = false
    };

    public static readonly ReportSection Overview = new()
    {
        Id = "overview",
        Title = "Analysis Overview",
        Order = 10,
        IsRequired = false
    };

    public static readonly ReportSection CriticalIssues = new()
    {
        Id = "critical-issues",
        Title = "Critical Issues",
        Order = 20,
        IsRequired = false,
        MinimumSeverity = AnalysisSeverity.Error
    };

    public static readonly ReportSection Warnings = new()
    {
        Id = "warnings",
        Title = "Warnings",
        Order = 30,
        IsRequired = false,
        MinimumSeverity = AnalysisSeverity.Warning
    };

    public static readonly ReportSection Information = new()
    {
        Id = "information",
        Title = "Informational",
        Order = 40,
        IsRequired = false,
        MinimumSeverity = AnalysisSeverity.Info
    };

    public static readonly ReportSection TechnicalDetails = new()
    {
        Id = "technical-details",
        Title = "Technical Details",
        Order = 50,
        IsRequired = false
    };

    public static readonly ReportSection Recommendations = new()
    {
        Id = "recommendations",
        Title = "Recommendations",
        Order = 60,
        IsRequired = false
    };

    public static readonly ReportSection Performance = new()
    {
        Id = "performance",
        Title = "Performance Metrics",
        Order = 70,
        IsRequired = false
    };

    public static readonly ReportSection Statistics = new()
    {
        Id = "statistics",
        Title = "Report Statistics",
        Order = 80,
        IsRequired = false
    };

    public static readonly ReportSection Appendix = new()
    {
        Id = "appendix",
        Title = "Appendix",
        Order = 90,
        IsRequired = false
    };
}

/// <summary>
/// Advanced options for report generation.
/// </summary>
public class AdvancedReportOptions
{
    /// <summary>
    /// Gets or sets the title for the report.
    /// </summary>
    public string? Title { get; set; }

    /// <summary>
    /// Gets or sets whether to include timing information.
    /// </summary>
    public bool IncludeTimingInfo { get; set; } = true;

    /// <summary>
    /// Gets or sets whether to include metadata.
    /// </summary>
    public bool IncludeMetadata { get; set; } = false;

    /// <summary>
    /// Gets or sets the report format.
    /// </summary>
    public ReportFormat Format { get; set; } = ReportFormat.Markdown;
    /// <summary>
    /// Gets or sets the application version for version-aware messaging.
    /// </summary>
    public string? ApplicationVersion { get; set; }

    /// <summary>
    /// Gets or sets the target audience for the report.
    /// </summary>
    public ReportAudience Audience { get; set; } = ReportAudience.Technical;

    /// <summary>
    /// Gets or sets whether to include recommendations.
    /// </summary>
    public bool IncludeRecommendations { get; set; } = true;

    /// <summary>
    /// Gets or sets whether to group by analyzer.
    /// </summary>
    public bool GroupByAnalyzer { get; set; } = false;

    /// <summary>
    /// Gets or sets the maximum depth for nested fragments.
    /// </summary>
    public int MaxNestingDepth { get; set; } = 5;

    /// <summary>
    /// Gets or sets custom metadata to include in the report.
    /// </summary>
    public Dictionary<string, string> CustomMetadata { get; set; } = new();

    /// <summary>
    /// Gets or sets whether to include a table of contents.
    /// </summary>
    public bool IncludeTableOfContents { get; set; } = false;

    /// <summary>
    /// Gets or sets the timestamp format.
    /// </summary>
    public string TimestampFormat { get; set; } = "yyyy-MM-dd HH:mm:ss";
}

/// <summary>
/// Defines the target audience for a report.
/// </summary>
public enum ReportAudience
{
    /// <summary>
    /// Technical audience (developers, engineers).
    /// </summary>
    Technical,

    /// <summary>
    /// Management audience (executives, managers).
    /// </summary>
    Management,

    /// <summary>
    /// End users.
    /// </summary>
    EndUser,

    /// <summary>
    /// Mixed audience.
    /// </summary>
    Mixed
}

/// <summary>
/// Statistics about a generated report.
/// </summary>
public class ReportStatistics
{
    /// <summary>
    /// Gets the total number of analyzers run.
    /// </summary>
    public int TotalAnalyzers { get; init; }

    /// <summary>
    /// Gets the number of successful analyzers.
    /// </summary>
    public int SuccessfulAnalyzers { get; init; }

    /// <summary>
    /// Gets the number of failed analyzers.
    /// </summary>
    public int FailedAnalyzers { get; init; }

    /// <summary>
    /// Gets the number of skipped analyzers.
    /// </summary>
    public int SkippedAnalyzers { get; init; }

    /// <summary>
    /// Gets the count of issues by severity.
    /// </summary>
    public Dictionary<AnalysisSeverity, int> SeverityCounts { get; init; } = new();

    /// <summary>
    /// Gets the total analysis duration.
    /// </summary>
    public TimeSpan TotalDuration { get; init; }

    /// <summary>
    /// Gets the average analyzer duration.
    /// </summary>
    public TimeSpan AverageDuration { get; init; }

    /// <summary>
    /// Gets the slowest analyzer name and duration.
    /// </summary>
    public (string Name, TimeSpan Duration) SlowestAnalyzer { get; init; }

    /// <summary>
    /// Gets the fastest analyzer name and duration.
    /// </summary>
    public (string Name, TimeSpan Duration) FastestAnalyzer { get; init; }

    /// <summary>
    /// Gets the timestamp when the report was generated.
    /// </summary>
    public DateTime GeneratedAt { get; init; } = DateTime.UtcNow;

    /// <summary>
    /// Gets the total number of report fragments.
    /// </summary>
    public int TotalFragments { get; init; }

    /// <summary>
    /// Gets the total number of errors detected.
    /// </summary>
    public int TotalErrors { get; init; }

    /// <summary>
    /// Gets the total number of warnings detected.
    /// </summary>
    public int TotalWarnings { get; init; }
}