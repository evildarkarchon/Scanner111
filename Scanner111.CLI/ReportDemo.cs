using Scanner111.Core.Reporting;
using Scanner111.Core.Analysis;
using Microsoft.Extensions.Logging;

namespace Scanner111.CLI;

/// <summary>
/// Demo to showcase Report Generation System implementation including advanced features
/// </summary>
public static class ReportDemo
{
    public static async Task RunDemo()
    {
        Console.WriteLine("=== Report Generation System Demo ===\n");

        // Test 1: Basic Fragment Creation
        Console.WriteLine("Test 1: Basic Fragment Creation");
        var header = ReportFragment.CreateHeader("Analysis Report");
        var info = ReportFragment.CreateInfo("System Status", "All systems operational");
        var warning = ReportFragment.CreateWarning("Memory Warning", "High memory usage detected");
        var error = ReportFragment.CreateError("Critical Error", "Database connection failed");

        Console.WriteLine($"Created {header.Type} fragment: {header.Title}");
        Console.WriteLine($"Created {info.Type} fragment: {info.Title}");
        Console.WriteLine($"Created {warning.Type} fragment: {warning.Title}");
        Console.WriteLine($"Created {error.Type} fragment: {error.Title}");

        // Test 2: Operator Overloading (+)
        Console.WriteLine("\nTest 2: Fragment Composition with + operator");
        var combined = header + info + warning + error;
        Console.WriteLine($"Combined fragment has {combined.Children.Count} children");

        // Test 3: Empty Fragment Handling
        Console.WriteLine("\nTest 3: Empty Fragment Handling");
        var empty = ReportFragment.Empty();
        var result = info + empty;
        Console.WriteLine($"Info + Empty = Same as Info: {ReferenceEquals(result, info)}");

        // Test 4: Compose Method
        Console.WriteLine("\nTest 4: Compose Method");
        var composed = ReportFragment.Compose(header, info, warning, error, null);
        Console.WriteLine($"Composed fragment has {composed.Children.Count} children (null filtered)");

        // Test 5: Conditional Sections
        Console.WriteLine("\nTest 5: Conditional Sections");
        var conditionalWithContent = ReportFragment.ConditionalSection(
            () => ReportFragment.CreateInfo("Has Content", "Some content"),
            () => "Conditional Header Applied");
        var conditionalEmpty = ReportFragment.ConditionalSection(
            () => ReportFragment.Empty(),
            () => "Should Not Appear");
        
        Console.WriteLine($"Conditional with content: Title = '{conditionalWithContent.Title}'");
        Console.WriteLine($"Conditional empty has content: {conditionalEmpty.HasContent()}");

        // Test 6: WithHeader Method
        Console.WriteLine("\nTest 6: WithHeader Method");
        var content = ReportFragment.CreateSection("Content", "Some important data");
        var withHeader = content.WithHeader("Added Header");
        Console.WriteLine($"Original title: '{content.Title}'");
        Console.WriteLine($"With header title: '{withHeader.Title}'");
        Console.WriteLine($"With header has child: {withHeader.Children.Any()}");

        // Test 7: Report Builder
        Console.WriteLine("\nTest 7: Report Fragment Builder");
        var built = ReportFragmentBuilder.Create()
            .WithTitle("Built Report")
            .WithType(FragmentType.Section)
            .WithOrder(100)
            .AppendLine("First line")
            .AppendSuccess("Operation successful")
            .AppendWarning("Potential issue")
            .AppendFix("Apply this fix")
            .AppendSeparator()
            .AppendNotice("Important notice")
            .Build();
        
        Console.WriteLine($"Built fragment title: '{built.Title}'");
        Console.WriteLine($"Built fragment has content: {built.HasContent()}");

        // Test 8: Report Composer
        Console.WriteLine("\nTest 8: Report Composer");
        using var loggerFactory = LoggerFactory.Create(builder => { });
        var logger = loggerFactory.CreateLogger<ReportComposer>();
        var composer = new ReportComposer(logger);

        var fragments = new[] { header, info, warning, error };
        var markdown = await composer.ComposeFromFragmentsAsync(fragments, 
            new ReportOptions { Format = ReportFormat.Markdown, Title = "Demo Report" });
        
        Console.WriteLine($"Generated Markdown report length: {markdown.Length} characters");
        Console.WriteLine("\nFirst 500 characters of report:");
        Console.WriteLine(markdown.Substring(0, Math.Min(500, markdown.Length)));

        // Test 9: Performance Test
        Console.WriteLine("\nTest 9: Performance Test - Creating 1000 fragments");
        var stopwatch = System.Diagnostics.Stopwatch.StartNew();
        var manyFragments = Enumerable.Range(0, 1000)
            .Select(i => ReportFragment.CreateInfo($"Fragment {i}", $"Content {i}"))
            .ToArray();
        
        var largeComposed = ReportFragment.Compose(manyFragments);
        stopwatch.Stop();
        
        Console.WriteLine($"Created and composed 1000 fragments in {stopwatch.ElapsedMilliseconds}ms");
        Console.WriteLine($"Large composed fragment has {largeComposed.Children.Count} children");

        // Test 10: Advanced Report Generator
        Console.WriteLine("\n=== Advanced Report Generator Demo ===");
        await RunAdvancedReportDemo();

        Console.WriteLine("\n=== Demo Complete ===");
    }

    private static async Task RunAdvancedReportDemo()
    {
        Console.WriteLine("\nTest 10: Advanced Report Generator with Templates");
        
        // Create mock analysis results with varying severity
        var analysisResults = new List<AnalysisResult>
        {
            CreateMockAnalysisResult("SecurityAnalyzer", AnalysisSeverity.Critical, true,
                ReportFragment.CreateError("Security Vulnerability", "SQL injection detected in user input")),
            
            CreateMockAnalysisResult("PerformanceAnalyzer", AnalysisSeverity.Error, true,
                ReportFragment.CreateError("Performance Issue", "Memory leak detected in main loop")),
            
            CreateMockAnalysisResult("CodeQualityAnalyzer", AnalysisSeverity.Warning, true,
                ReportFragment.CreateWarning("Code Quality", "10 methods exceed complexity threshold")),
            
            CreateMockAnalysisResult("DocumentationAnalyzer", AnalysisSeverity.Info, true,
                ReportFragment.CreateInfo("Documentation", "85% of public APIs are documented")),
            
            CreateMockAnalysisResult("TestCoverageAnalyzer", AnalysisSeverity.None, true,
                ReportFragment.CreateInfo("Test Coverage", "Code coverage is at 78%")),
            
            CreateMockAnalysisResult("DependencyAnalyzer", AnalysisSeverity.Warning, false,
                null) // Failed analyzer
        };

        // Setup advanced report generator
        using var loggerFactory = LoggerFactory.Create(builder => { });
        var reportLogger = loggerFactory.CreateLogger<AdvancedReportGenerator>();
        var composerLogger = loggerFactory.CreateLogger<ReportComposer>();
        var composer = new ReportComposer(composerLogger);
        var advancedGenerator = new AdvancedReportGenerator(reportLogger, composer);

        // Test different templates
        var templates = new[]
        {
            ("Executive", ReportTemplate.Predefined.Executive),
            ("Technical", ReportTemplate.Predefined.Technical),
            ("Summary", ReportTemplate.Predefined.Summary)
        };

        foreach (var (name, template) in templates)
        {
            Console.WriteLine($"\n--- {name} Template ---");
            
            var options = new AdvancedReportOptions
            {
                Title = $"{name} Analysis Report",
                Format = ReportFormat.Markdown,
                ApplicationVersion = "1.0.0",
                Audience = name == "Executive" ? ReportAudience.Management : ReportAudience.Technical,
                IncludeRecommendations = true,
                CustomMetadata = new Dictionary<string, string>
                {
                    { "Project", "Scanner111" },
                    { "Environment", "Production" }
                }
            };

            var report = await advancedGenerator.GenerateReportAsync(
                analysisResults, template, options);

            Console.WriteLine($"Generated {name} report:");
            Console.WriteLine($"- Length: {report.Length} characters");
            Console.WriteLine($"- Lines: {report.Split('\n').Length}");
            
            // Show first few lines of each template
            var preview = string.Join("\n", report.Split('\n').Take(10));
            Console.WriteLine($"Preview:\n{preview}...");
        }

        // Generate and display statistics
        Console.WriteLine("\n--- Report Statistics ---");
        var stats = await advancedGenerator.GenerateStatisticsAsync(analysisResults);
        
        Console.WriteLine($"Total Analyzers: {stats.TotalAnalyzers}");
        Console.WriteLine($"Successful: {stats.SuccessfulAnalyzers}");
        Console.WriteLine($"Failed: {stats.FailedAnalyzers}");
        Console.WriteLine($"Severity Distribution:");
        foreach (var (severity, count) in stats.SeverityCounts.Where(kvp => kvp.Value > 0))
        {
            Console.WriteLine($"  - {severity}: {count}");
        }
        Console.WriteLine($"Total Errors: {stats.TotalErrors}");
        Console.WriteLine($"Total Warnings: {stats.TotalWarnings}");

        // Test custom template registration
        Console.WriteLine("\n--- Custom Template ---");
        var customTemplate = new ReportTemplate
        {
            Id = "custom",
            Name = "Custom Minimal",
            Description = "Minimal report with only critical issues",
            IncludeExecutiveSummary = false,
            IncludeStatistics = false,
            UseSeverityOrdering = true,
            MinimumSeverity = AnalysisSeverity.Error,
            Sections = new[] { ReportSection.CriticalIssues }
        };

        advancedGenerator.RegisterTemplate(customTemplate);
        
        var customReport = await advancedGenerator.GenerateReportAsync(
            analysisResults, customTemplate, new AdvancedReportOptions { Format = ReportFormat.Markdown });
        
        Console.WriteLine($"Custom template report generated:");
        Console.WriteLine($"- Only shows {customTemplate.MinimumSeverity}+ severity items");
        Console.WriteLine($"- Length: {customReport.Length} characters");
        
        // Performance test
        Console.WriteLine("\n--- Performance Test ---");
        var largeResultSet = Enumerable.Range(0, 100)
            .Select(i => CreateMockAnalysisResult(
                $"Analyzer{i}",
                (AnalysisSeverity)(i % 5),
                i % 10 != 0,
                ReportFragment.CreateInfo($"Result {i}", $"Content for analyzer {i}")))
            .ToList();

        var perfStopwatch = System.Diagnostics.Stopwatch.StartNew();
        var perfReport = await advancedGenerator.GenerateReportAsync(
            largeResultSet,
            ReportTemplate.Predefined.Full,
            new AdvancedReportOptions { IncludeTableOfContents = true });
        perfStopwatch.Stop();

        Console.WriteLine($"Generated full report for 100 analyzers:");
        Console.WriteLine($"- Generation time: {perfStopwatch.ElapsedMilliseconds}ms");
        Console.WriteLine($"- Report size: {perfReport.Length / 1024.0:F1} KB");
        Console.WriteLine($"- Performance target (<100ms): {(perfStopwatch.ElapsedMilliseconds < 100 ? "✅ PASSED" : "❌ FAILED")}");
    }

    private static AnalysisResult CreateMockAnalysisResult(
        string analyzerName,
        AnalysisSeverity severity,
        bool success,
        ReportFragment? fragment)
    {
        var result = new AnalysisResult(analyzerName)
        {
            Success = success,
            Severity = severity,
            Fragment = fragment,
            Duration = TimeSpan.FromMilliseconds(Random.Shared.Next(10, 500))
        };

        if (!success)
        {
            result.AddError("Analyzer failed to complete");
        }

        // Add some metadata for technical details
        result.AddMetadata("version", "1.0");
        result.AddMetadata("timestamp", DateTime.UtcNow.ToString("O"));

        if (severity >= AnalysisSeverity.Error)
        {
            result.AddMetadata("recommendation", $"Urgently address {analyzerName} findings");
        }

        return result;
    }
}