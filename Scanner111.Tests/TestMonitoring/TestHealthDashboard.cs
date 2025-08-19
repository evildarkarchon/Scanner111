using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using Spectre.Console;

namespace Scanner111.Tests.TestMonitoring;

public class TestHealthDashboard
{
    private readonly TestPerformanceTracker _performanceTracker;
    private readonly FlakyTestDetector _flakyDetector;
    private readonly string _outputDirectory;

    public TestHealthDashboard(
        TestPerformanceTracker? performanceTracker = null,
        FlakyTestDetector? flakyDetector = null,
        string outputDirectory = "TestResults/Dashboard")
    {
        _performanceTracker = performanceTracker ?? new TestPerformanceTracker();
        _flakyDetector = flakyDetector ?? new FlakyTestDetector();
        _outputDirectory = outputDirectory;
        Directory.CreateDirectory(_outputDirectory);
    }

    public async Task GenerateDashboardAsync()
    {
        AnsiConsole.Clear();
        
        var layout = new Layout("Root")
            .SplitRows(
                new Layout("Header"),
                new Layout("Body")
                    .SplitColumns(
                        new Layout("Left"),
                        new Layout("Right")),
                new Layout("Footer"));

        layout["Header"].Update(GenerateHeader());
        
        var healthReport = _performanceTracker.GenerateHealthReport();
        var flakyAnalysis = await _flakyDetector.AnalyzeTestRunsAsync();
        
        layout["Left"].Update(GeneratePerformancePanel(healthReport));
        layout["Right"].Update(GenerateFlakyTestPanel(flakyAnalysis));
        layout["Footer"].Update(GenerateRecommendationsPanel(healthReport, flakyAnalysis));

        AnsiConsole.Write(layout);
        
        await GenerateDetailedReportsAsync(healthReport, flakyAnalysis);
    }

    private Panel GenerateHeader()
    {
        var headerTable = new Table()
            .Border(TableBorder.None)
            .AddColumn("")
            .HideHeaders();

        headerTable.AddRow(new FigletText("Test Health Dashboard")
            .Color(Color.Cyan1)
            .Centered());

        headerTable.AddRow(new Text($"Generated: {DateTime.Now:yyyy-MM-dd HH:mm:ss}", new Style(Color.Grey))
            .Centered());

        return new Panel(headerTable)
            .Header("Scanner111 Test Suite Monitor")
            .Expand()
            .BorderColor(Color.Blue);
    }

    private Panel GeneratePerformancePanel(TestHealthReport report)
    {
        var table = new Table()
            .Border(TableBorder.Rounded)
            .BorderColor(Color.Green)
            .AddColumn("Metric")
            .AddColumn("Value");

        table.AddRow("Total Tests", report.TotalTests.ToString());
        table.AddRow("Total Runs", report.TotalRuns.ToString());
        table.AddRow("Average Execution Time", FormatTimeSpan(report.AverageExecutionTime));
        table.AddRow("Failure Rate", $"{report.FailureRate:P2}");

        if (report.SlowestTests.Any())
        {
            var slowTestList = string.Join("\n", 
                report.SlowestTests.Take(5).Select(t => 
                    $"• {TruncateTestName(t.TestName)}: {FormatTimeSpan(t.AverageTime)}"));
            table.AddRow("Slowest Tests", slowTestList);
        }

        var statusColor = report.FailureRate switch
        {
            < 0.05 => Color.Green,
            < 0.15 => Color.Yellow,
            _ => Color.Red
        };

        var chart = GeneratePerformanceChart(report);

        var content = new Rows(
            table,
            new Rule(),
            chart
        );

        return new Panel(content)
            .Header($"[{statusColor}]Performance Metrics[/]")
            .BorderColor(statusColor);
    }

    private Panel GenerateFlakyTestPanel(FlakyTestAnalysis analysis)
    {
        var content = new List<Spectre.Console.Rendering.IRenderable>();

        if (analysis.InsufficientData)
        {
            content.Add(new Text("Insufficient data for flaky test analysis", new Style(Color.Yellow)));
        }
        else
        {
            var table = new Table()
                .Border(TableBorder.Simple)
                .AddColumn("Test Name")
                .AddColumn("Flakiness")
                .AddColumn("Success Rate");

            foreach (var test in analysis.FlakyTests.Take(10))
            {
                var flakinessBar = GenerateFlakinessBar(test.FlakinessScore);
                table.AddRow(
                    TruncateTestName(test.TestName),
                    flakinessBar,
                    $"{test.SuccessRate:P0}"
                );
            }

            content.Add(table);

            if (analysis.EnvironmentFactors.Any())
            {
                content.Add(new Rule());
                content.Add(new Text("Environment Factors:", new Style(Color.Orange1, decoration: Decoration.Bold)));
                foreach (var factor in analysis.EnvironmentFactors)
                {
                    content.Add(new Text($"• {factor}", new Style(Color.Grey)));
                }
            }
        }

        return new Panel(new Rows(content.ToArray()))
            .Header("[yellow]Flaky Test Analysis[/]")
            .BorderColor(Color.Yellow);
    }

    private Panel GenerateRecommendationsPanel(TestHealthReport healthReport, FlakyTestAnalysis flakyAnalysis)
    {
        var recommendations = new List<string>();

        if (healthReport.FailureRate > 0.1)
        {
            recommendations.Add("[red]⚠[/] High failure rate detected - investigate failing tests");
        }

        if (healthReport.TestsWithPathIssues.Count > 5)
        {
            recommendations.Add("[yellow]📁[/] Multiple path-related failures - implement file system abstraction");
        }

        if (healthReport.TestsWithExternalDependencies.Any())
        {
            recommendations.Add("[blue]🌐[/] External dependencies detected - consider mocking");
        }

        if (flakyAnalysis.FlakyTests.Count > 10)
        {
            recommendations.Add("[orange1]🔄[/] High number of flaky tests - review test isolation");
        }

        recommendations.AddRange(flakyAnalysis.RecommendedActions.Select(r => $"[cyan]💡[/] {r}"));

        var tree = new Tree("Recommendations")
            .Style(Style.Parse("cyan"));

        foreach (var rec in recommendations)
        {
            tree.AddNode(new Markup(rec));
        }

        return new Panel(tree)
            .Header("[cyan]Action Items[/]")
            .BorderColor(Color.Cyan1);
    }

    private BarChart GeneratePerformanceChart(TestHealthReport report)
    {
        var chart = new BarChart()
            .Width(60)
            .Label("[green]Test Execution Distribution[/]")
            .CenterLabel();

        var buckets = new Dictionary<string, int>
        {
            ["< 100ms"] = 0,
            ["100-500ms"] = 0,
            ["500ms-1s"] = 0,
            ["1s-5s"] = 0,
            ["> 5s"] = 0
        };

        foreach (var test in report.SlowestTests)
        {
            var ms = test.AverageTime.TotalMilliseconds;
            if (ms < 100) buckets["< 100ms"]++;
            else if (ms < 500) buckets["100-500ms"]++;
            else if (ms < 1000) buckets["500ms-1s"]++;
            else if (ms < 5000) buckets["1s-5s"]++;
            else buckets["> 5s"]++;
        }

        foreach (var bucket in buckets)
        {
            chart.AddItem(bucket.Key, bucket.Value, Color.Green);
        }

        return chart;
    }

    private string GenerateFlakinessBar(double score)
    {
        var barLength = 10;
        var filledLength = (int)(score * barLength);
        var bar = new string('█', filledLength) + new string('░', barLength - filledLength);
        
        var color = score switch
        {
            < 0.3 => "green",
            < 0.7 => "yellow",
            _ => "red"
        };

        return $"[{color}]{bar}[/]";
    }

    private async Task GenerateDetailedReportsAsync(TestHealthReport healthReport, FlakyTestAnalysis flakyAnalysis)
    {
        await GenerateHtmlReportAsync(healthReport, flakyAnalysis);
        await GenerateJsonReportAsync(healthReport, flakyAnalysis);
        await GenerateMarkdownReportAsync(healthReport, flakyAnalysis);
    }

    private async Task GenerateHtmlReportAsync(TestHealthReport healthReport, FlakyTestAnalysis flakyAnalysis)
    {
        var html = new StringBuilder();
        html.AppendLine("<!DOCTYPE html>");
        html.AppendLine("<html><head>");
        html.AppendLine("<title>Scanner111 Test Health Report</title>");
        html.AppendLine(@"<style>
            body { font-family: 'Segoe UI', Tahoma, Geneva, Verdana, sans-serif; margin: 20px; background: #f5f5f5; }
            .container { max-width: 1200px; margin: 0 auto; background: white; padding: 20px; border-radius: 8px; box-shadow: 0 2px 4px rgba(0,0,0,0.1); }
            h1 { color: #333; border-bottom: 3px solid #007acc; padding-bottom: 10px; }
            h2 { color: #555; margin-top: 30px; }
            .metric { display: inline-block; padding: 10px 20px; margin: 10px; background: #f0f7ff; border-left: 4px solid #007acc; }
            .metric-label { font-size: 12px; color: #666; }
            .metric-value { font-size: 24px; font-weight: bold; color: #333; }
            .success { background: #f0fff0; border-color: #28a745; }
            .warning { background: #fff8f0; border-color: #ffc107; }
            .danger { background: #fff0f0; border-color: #dc3545; }
            table { width: 100%; border-collapse: collapse; margin: 20px 0; }
            th { background: #007acc; color: white; padding: 10px; text-align: left; }
            td { padding: 8px; border-bottom: 1px solid #ddd; }
            tr:hover { background: #f5f5f5; }
            .flakiness-bar { display: inline-block; width: 100px; height: 20px; background: #e0e0e0; border-radius: 10px; overflow: hidden; }
            .flakiness-fill { height: 100%; background: linear-gradient(90deg, #28a745, #ffc107, #dc3545); }
        </style>");
        html.AppendLine("</head><body>");
        html.AppendLine("<div class='container'>");
        
        html.AppendLine($"<h1>Test Health Report - {DateTime.Now:yyyy-MM-dd HH:mm}</h1>");
        
        html.AppendLine("<div class='metrics'>");
        html.AppendLine($"<div class='metric'><div class='metric-label'>Total Tests</div><div class='metric-value'>{healthReport.TotalTests}</div></div>");
        html.AppendLine($"<div class='metric'><div class='metric-label'>Total Runs</div><div class='metric-value'>{healthReport.TotalRuns}</div></div>");
        html.AppendLine($"<div class='metric {GetMetricClass(healthReport.FailureRate)}'><div class='metric-label'>Failure Rate</div><div class='metric-value'>{healthReport.FailureRate:P1}</div></div>");
        html.AppendLine("</div>");

        html.AppendLine("<h2>Slowest Tests</h2>");
        html.AppendLine("<table>");
        html.AppendLine("<tr><th>Test Name</th><th>Average Time</th><th>Max Time</th></tr>");
        foreach (var test in healthReport.SlowestTests.Take(10))
        {
            html.AppendLine($"<tr><td>{test.TestName}</td><td>{test.AverageTime:g}</td><td>{test.MaxTime:g}</td></tr>");
        }
        html.AppendLine("</table>");

        if (flakyAnalysis.FlakyTests.Any())
        {
            html.AppendLine("<h2>Flaky Tests</h2>");
            html.AppendLine("<table>");
            html.AppendLine("<tr><th>Test Name</th><th>Success Rate</th><th>Flakiness Score</th><th>Recommended Action</th></tr>");
            foreach (var test in flakyAnalysis.FlakyTests.Take(10))
            {
                var barWidth = (int)(test.FlakinessScore * 100);
                html.AppendLine($"<tr><td>{test.TestName}</td><td>{test.SuccessRate:P0}</td>");
                html.AppendLine($"<td><div class='flakiness-bar'><div class='flakiness-fill' style='width:{barWidth}%'></div></div></td>");
                html.AppendLine($"<td>{test.RecommendedAction}</td></tr>");
            }
            html.AppendLine("</table>");
        }

        html.AppendLine("</div></body></html>");

        await File.WriteAllTextAsync(Path.Combine(_outputDirectory, "test-health-report.html"), html.ToString());
    }

    private async Task GenerateJsonReportAsync(TestHealthReport healthReport, FlakyTestAnalysis flakyAnalysis)
    {
        var report = new
        {
            GeneratedAt = DateTime.UtcNow,
            HealthReport = healthReport,
            FlakyAnalysis = flakyAnalysis,
            Summary = new
            {
                TotalTests = healthReport.TotalTests,
                FailureRate = healthReport.FailureRate,
                FlakyTestCount = flakyAnalysis.FlakyTests.Count,
                RecommendationCount = flakyAnalysis.RecommendedActions.Count
            }
        };

        var json = JsonSerializer.Serialize(report, new JsonSerializerOptions
        {
            WriteIndented = true
        });

        await File.WriteAllTextAsync(Path.Combine(_outputDirectory, "test-health-report.json"), json);
    }

    private async Task GenerateMarkdownReportAsync(TestHealthReport healthReport, FlakyTestAnalysis flakyAnalysis)
    {
        var md = new StringBuilder();
        
        md.AppendLine("# Test Health Report");
        md.AppendLine($"*Generated: {DateTime.Now:yyyy-MM-dd HH:mm:ss}*");
        md.AppendLine();
        
        md.AppendLine("## Summary");
        md.AppendLine($"- **Total Tests**: {healthReport.TotalTests}");
        md.AppendLine($"- **Total Runs**: {healthReport.TotalRuns}");
        md.AppendLine($"- **Average Execution Time**: {healthReport.AverageExecutionTime:g}");
        md.AppendLine($"- **Failure Rate**: {healthReport.FailureRate:P2}");
        md.AppendLine();

        md.AppendLine("## Performance Analysis");
        md.AppendLine();
        md.AppendLine("### Slowest Tests");
        md.AppendLine("| Test Name | Average Time | Max Time |");
        md.AppendLine("|-----------|-------------|----------|");
        foreach (var test in healthReport.SlowestTests.Take(10))
        {
            md.AppendLine($"| {test.TestName} | {test.AverageTime:g} | {test.MaxTime:g} |");
        }
        md.AppendLine();

        if (flakyAnalysis.FlakyTests.Any())
        {
            md.AppendLine("## Flaky Test Analysis");
            md.AppendLine();
            md.AppendLine("### Top Flaky Tests");
            md.AppendLine("| Test Name | Success Rate | Flakiness Score | Recommended Action |");
            md.AppendLine("|-----------|-------------|-----------------|-------------------|");
            foreach (var test in flakyAnalysis.FlakyTests.Take(10))
            {
                md.AppendLine($"| {test.TestName} | {test.SuccessRate:P0} | {test.FlakinessScore:F2} | {test.RecommendedAction} |");
            }
            md.AppendLine();
        }

        if (healthReport.TestsWithPathIssues.Any())
        {
            md.AppendLine("## Tests with Path Issues");
            foreach (var test in healthReport.TestsWithPathIssues.Take(10))
            {
                md.AppendLine($"- {test}");
            }
            md.AppendLine();
        }

        md.AppendLine("## Recommendations");
        foreach (var rec in flakyAnalysis.RecommendedActions)
        {
            md.AppendLine($"- {rec}");
        }

        await File.WriteAllTextAsync(Path.Combine(_outputDirectory, "test-health-report.md"), md.ToString());
    }

    private string GetMetricClass(double failureRate)
    {
        return failureRate switch
        {
            < 0.05 => "success",
            < 0.15 => "warning",
            _ => "danger"
        };
    }

    private string TruncateTestName(string testName, int maxLength = 50)
    {
        if (testName.Length <= maxLength) return testName;
        
        var parts = testName.Split('.');
        if (parts.Length > 2)
        {
            return $"...{parts[^2]}.{parts[^1]}";
        }
        
        return $"...{testName.Substring(testName.Length - maxLength + 3)}";
    }

    private string FormatTimeSpan(TimeSpan timeSpan)
    {
        if (timeSpan.TotalSeconds < 1)
            return $"{timeSpan.TotalMilliseconds:F0}ms";
        if (timeSpan.TotalMinutes < 1)
            return $"{timeSpan.TotalSeconds:F1}s";
        return $"{timeSpan.TotalMinutes:F1}m";
    }
}