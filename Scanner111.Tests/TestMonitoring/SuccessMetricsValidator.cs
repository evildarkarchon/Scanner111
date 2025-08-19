using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using Spectre.Console;

namespace Scanner111.Tests.TestMonitoring;

public class SuccessMetricsValidator
{
    private readonly TestPerformanceTracker _performanceTracker;
    private readonly FlakyTestDetector _flakyDetector;
    private readonly CoverageReporter _coverageReporter;
    private readonly TestHealthDashboard _dashboard;
    private readonly string _outputPath;

    public SuccessMetricsValidator(string outputPath = "TestResults/ValidationReport")
    {
        _performanceTracker = new TestPerformanceTracker();
        _flakyDetector = new FlakyTestDetector();
        _coverageReporter = new CoverageReporter();
        _dashboard = new TestHealthDashboard(_performanceTracker, _flakyDetector);
        _outputPath = outputPath;
        Directory.CreateDirectory(_outputPath);
    }

    public async Task<ValidationReport> ValidateAllMetricsAsync()
    {
        var report = new ValidationReport
        {
            ValidatedAt = DateTime.UtcNow,
            BaselineMetrics = await GetBaselineMetricsAsync()
        };

        AnsiConsole.Write(new FigletText("Success Metrics Validation")
            .Color(Color.Cyan1)
            .Centered());

        await AnsiConsole.Status()
            .Spinner(Spinner.Known.Star)
            .SpinnerStyle(Style.Parse("cyan"))
            .StartAsync("Validating success metrics...", async ctx =>
            {
                ctx.Status("Checking test pass rate on Windows...");
                report.TestsPassConsistently = await ValidateTestsPassConsistentlyAsync();
                
                ctx.Status("Measuring test execution time...");
                report.ExecutionTimeReduced = await ValidateExecutionTimeReductionAsync();
                
                ctx.Status("Analyzing path handling issues...");
                report.NoPathHandlingFailures = await ValidateNoPathHandlingFailuresAsync();
                
                ctx.Status("Checking external dependencies...");
                report.NoExternalDependencies = await ValidateNoExternalDependenciesAsync();
                
                ctx.Status("Calculating test coverage...");
                report.CoverageAbove80 = await ValidateCoverageAbove80Async();
            });

        report.OverallSuccess = DetermineOverallSuccess(report);
        report.Recommendations = GenerateRecommendations(report);
        
        await GenerateValidationReportsAsync(report);
        DisplayValidationSummary(report);
        
        return report;
    }

    private async Task<BaselineMetrics> GetBaselineMetricsAsync()
    {
        var baseline = new BaselineMetrics
        {
            RecordedAt = DateTime.UtcNow
        };

        var testResultFiles = Directory.GetFiles("TestResults", "*.trx", SearchOption.TopDirectoryOnly)
            .OrderByDescending(f => File.GetLastWriteTime(f))
            .FirstOrDefault();

        if (testResultFiles != null)
        {
            var trxContent = await File.ReadAllTextAsync(testResultFiles);
            var doc = System.Xml.Linq.XDocument.Parse(trxContent);
            var ns = doc.Root?.GetDefaultNamespace();
            
            if (ns != null)
            {
                var counters = doc.Root?.Element(ns + "ResultSummary")?.Element(ns + "Counters");
                if (counters != null)
                {
                    baseline.TotalTests = int.Parse(counters.Attribute("total")?.Value ?? "0");
                    baseline.PassedTests = int.Parse(counters.Attribute("passed")?.Value ?? "0");
                    baseline.FailedTests = int.Parse(counters.Attribute("failed")?.Value ?? "0");
                    baseline.SkippedTests = int.Parse(counters.Attribute("notExecuted")?.Value ?? "0");
                }

                var times = doc.Root?.Element(ns + "Times");
                if (times != null)
                {
                    var start = DateTime.Parse(times.Attribute("start")?.Value ?? DateTime.Now.ToString());
                    var finish = DateTime.Parse(times.Attribute("finish")?.Value ?? DateTime.Now.ToString());
                    baseline.ExecutionTime = finish - start;
                }
            }
        }

        return baseline;
    }

    private async Task<MetricValidation> ValidateTestsPassConsistentlyAsync()
    {
        var validation = new MetricValidation
        {
            MetricName = "All tests pass consistently on Windows",
            Target = "100% pass rate (or >98% with known flaky tests documented)"
        };

        var baseline = await GetBaselineMetricsAsync();
        var passRate = baseline.TotalTests > 0 
            ? (double)baseline.PassedTests / baseline.TotalTests 
            : 0;

        validation.CurrentValue = $"{passRate:P1} ({baseline.PassedTests}/{baseline.TotalTests})";
        validation.Passed = passRate >= 0.98;

        if (!validation.Passed)
        {
            var flakyAnalysis = await _flakyDetector.AnalyzeTestRunsAsync();
            var flakyCount = flakyAnalysis.FlakyTests.Count;
            
            if (flakyCount > 0)
            {
                validation.Details = $"Found {flakyCount} flaky tests that may be affecting pass rate";
                var adjustedFailures = baseline.FailedTests - flakyCount;
                var adjustedPassRate = (double)(baseline.TotalTests - adjustedFailures) / baseline.TotalTests;
                
                if (adjustedPassRate >= 0.98)
                {
                    validation.Passed = true;
                    validation.Details += $". Adjusted pass rate (excluding flaky): {adjustedPassRate:P1}";
                }
            }
        }

        return validation;
    }

    private async Task<MetricValidation> ValidateExecutionTimeReductionAsync()
    {
        var validation = new MetricValidation
        {
            MetricName = "Test execution time reduced by 50%",
            Target = "50% reduction from baseline"
        };

        var currentBaseline = await GetBaselineMetricsAsync();
        
        var historicalBaseline = new TimeSpan(0, 1, 36);
        
        if (currentBaseline.ExecutionTime.TotalSeconds > 0)
        {
            var reduction = 1 - (currentBaseline.ExecutionTime.TotalSeconds / historicalBaseline.TotalSeconds);
            validation.CurrentValue = $"{reduction:P0} reduction (current: {currentBaseline.ExecutionTime:mm\\:ss}, baseline: {historicalBaseline:mm\\:ss})";
            validation.Passed = reduction >= 0.5;
            
            if (!validation.Passed)
            {
                validation.Details = $"Need {(0.5 - reduction):P0} more reduction to meet target";
            }
        }
        else
        {
            validation.CurrentValue = "Unable to measure";
            validation.Passed = false;
            validation.Details = "No execution time data available";
        }

        return validation;
    }

    private async Task<MetricValidation> ValidateNoPathHandlingFailuresAsync()
    {
        var validation = new MetricValidation
        {
            MetricName = "No test failures due to path handling",
            Target = "Zero path-related failures"
        };

        await _performanceTracker.LoadMetricsAsync();
        var healthReport = _performanceTracker.GenerateHealthReport();
        
        var pathIssueCount = healthReport.TestsWithPathIssues.Count;
        validation.CurrentValue = $"{pathIssueCount} tests with path issues";
        validation.Passed = pathIssueCount == 0;

        if (!validation.Passed)
        {
            validation.Details = $"Tests with path issues: {string.Join(", ", healthReport.TestsWithPathIssues.Take(5))}";
            
            if (healthReport.TestsWithPathIssues.Count > 5)
            {
                validation.Details += $" and {healthReport.TestsWithPathIssues.Count - 5} more";
            }
        }

        return validation;
    }

    private async Task<MetricValidation> ValidateNoExternalDependenciesAsync()
    {
        var validation = new MetricValidation
        {
            MetricName = "Zero dependencies on external system resources in unit tests",
            Target = "No external dependencies in unit tests"
        };

        await _performanceTracker.LoadMetricsAsync();
        var healthReport = _performanceTracker.GenerateHealthReport();
        
        var externalDepsCount = healthReport.TestsWithExternalDependencies.Count;
        validation.CurrentValue = $"{externalDepsCount} tests with external dependencies";
        validation.Passed = externalDepsCount == 0;

        if (!validation.Passed)
        {
            validation.Details = $"Tests with external dependencies: {string.Join(", ", healthReport.TestsWithExternalDependencies.Take(5))}";
            
            if (healthReport.TestsWithExternalDependencies.Count > 5)
            {
                validation.Details += $" and {healthReport.TestsWithExternalDependencies.Count - 5} more";
            }
        }

        return validation;
    }

    private async Task<MetricValidation> ValidateCoverageAbove80Async()
    {
        var validation = new MetricValidation
        {
            MetricName = "Test coverage above 80%",
            Target = ">80% code coverage"
        };

        var coverageReport = await _coverageReporter.AnalyzeCoverageAsync();
        
        if (!coverageReport.HasCoverageData)
        {
            validation.CurrentValue = "No coverage data available";
            validation.Passed = false;
            validation.Details = "Run tests with --collect:\"XPlat Code Coverage\" to generate coverage data";
        }
        else
        {
            var coveragePercent = coverageReport.LineRate * 100;
            validation.CurrentValue = $"{coveragePercent:F1}% line coverage";
            validation.Passed = coveragePercent >= 80;
            
            if (!validation.Passed)
            {
                validation.Details = $"Need {80 - coveragePercent:F1}% more coverage to meet target";
                
                var criticalUncovered = coverageReport.UncoveredAreas
                    .Where(a => a.Priority == CoveragePriority.Critical)
                    .Take(3)
                    .Select(a => a.Name);
                    
                if (criticalUncovered.Any())
                {
                    validation.Details += $". Critical areas needing coverage: {string.Join(", ", criticalUncovered)}";
                }
            }
        }

        return validation;
    }

    private bool DetermineOverallSuccess(ValidationReport report)
    {
        var criticalMetrics = new[]
        {
            report.TestsPassConsistently,
            report.NoPathHandlingFailures,
            report.NoExternalDependencies
        };

        var allCriticalPassed = criticalMetrics.All(m => m.Passed);
        
        var bonusMetrics = new[]
        {
            report.ExecutionTimeReduced,
            report.CoverageAbove80
        };
        
        var bonusMetricsPassed = bonusMetrics.Count(m => m.Passed);

        return allCriticalPassed && bonusMetricsPassed >= 1;
    }

    private List<string> GenerateRecommendations(ValidationReport report)
    {
        var recommendations = new List<string>();

        if (!report.TestsPassConsistently.Passed)
        {
            recommendations.Add("🔴 CRITICAL: Fix failing tests immediately. Use flaky test detection to identify intermittent failures.");
        }

        if (!report.NoPathHandlingFailures.Passed)
        {
            recommendations.Add("🔴 CRITICAL: Implement file system abstraction layer (Phases 1-3 of test fix plan).");
        }

        if (!report.NoExternalDependencies.Passed)
        {
            recommendations.Add("🔴 CRITICAL: Mock all external dependencies in unit tests. Move integration tests to separate category.");
        }

        if (!report.ExecutionTimeReduced.Passed)
        {
            recommendations.Add("🟡 Optimize slow tests identified in performance report. Consider parallel execution.");
        }

        if (!report.CoverageAbove80.Passed)
        {
            recommendations.Add("🟡 Increase test coverage, focusing on critical components first.");
        }

        if (report.OverallSuccess)
        {
            recommendations.Add("✅ SUCCESS: All critical metrics met! Consider setting more ambitious targets.");
        }

        return recommendations;
    }

    private void DisplayValidationSummary(ValidationReport report)
    {
        var table = new Table()
            .Border(TableBorder.Rounded)
            .BorderColor(report.OverallSuccess ? Color.Green : Color.Red)
            .AddColumn("Metric")
            .AddColumn("Target")
            .AddColumn("Current")
            .AddColumn("Status");

        AddValidationRow(table, report.TestsPassConsistently);
        AddValidationRow(table, report.ExecutionTimeReduced);
        AddValidationRow(table, report.NoPathHandlingFailures);
        AddValidationRow(table, report.NoExternalDependencies);
        AddValidationRow(table, report.CoverageAbove80);

        AnsiConsole.Write(table);

        var panel = new Panel(new Rows(
            new Text($"Overall Success: {(report.OverallSuccess ? "✅ PASSED" : "❌ FAILED")}", 
                new Style(report.OverallSuccess ? Color.Green : Color.Red, decoration: Decoration.Bold)),
            new Rule(),
            new Text("Recommendations:", new Style(Color.Cyan1, decoration: Decoration.Bold)),
            new Rows(report.Recommendations.Select(r => new Text(r)).ToArray())
        ))
        .Header("[cyan]Validation Summary[/]")
        .BorderColor(report.OverallSuccess ? Color.Green : Color.Yellow);

        AnsiConsole.Write(panel);
    }

    private void AddValidationRow(Table table, MetricValidation validation)
    {
        var status = validation.Passed ? "[green]✅ PASS[/]" : "[red]❌ FAIL[/]";
        var currentValue = validation.Passed 
            ? $"[green]{validation.CurrentValue}[/]" 
            : $"[red]{validation.CurrentValue}[/]";
            
        table.AddRow(
            validation.MetricName,
            validation.Target,
            currentValue,
            status
        );

        if (!string.IsNullOrEmpty(validation.Details))
        {
            table.AddRow(
                "",
                "",
                $"[grey]{validation.Details}[/]",
                ""
            );
        }
    }

    private async Task GenerateValidationReportsAsync(ValidationReport report)
    {
        var json = JsonSerializer.Serialize(report, new JsonSerializerOptions
        {
            WriteIndented = true
        });
        
        await File.WriteAllTextAsync(Path.Combine(_outputPath, "validation-report.json"), json);

        var md = new StringBuilder();
        md.AppendLine("# Success Metrics Validation Report");
        md.AppendLine($"*Generated: {report.ValidatedAt:yyyy-MM-dd HH:mm:ss}*");
        md.AppendLine();
        md.AppendLine($"## Overall Result: {(report.OverallSuccess ? "✅ **PASSED**" : "❌ **FAILED**")}");
        md.AppendLine();
        
        md.AppendLine("## Baseline Metrics");
        md.AppendLine($"- Total Tests: {report.BaselineMetrics.TotalTests}");
        md.AppendLine($"- Passed: {report.BaselineMetrics.PassedTests}");
        md.AppendLine($"- Failed: {report.BaselineMetrics.FailedTests}");
        md.AppendLine($"- Execution Time: {report.BaselineMetrics.ExecutionTime:mm\\:ss}");
        md.AppendLine();
        
        md.AppendLine("## Success Criteria Validation");
        md.AppendLine();
        md.AppendLine("| Metric | Target | Current | Status |");
        md.AppendLine("|--------|--------|---------|--------|");
        
        AddMarkdownRow(md, report.TestsPassConsistently);
        AddMarkdownRow(md, report.ExecutionTimeReduced);
        AddMarkdownRow(md, report.NoPathHandlingFailures);
        AddMarkdownRow(md, report.NoExternalDependencies);
        AddMarkdownRow(md, report.CoverageAbove80);
        
        md.AppendLine();
        md.AppendLine("## Recommendations");
        foreach (var rec in report.Recommendations)
        {
            md.AppendLine($"- {rec}");
        }

        await File.WriteAllTextAsync(Path.Combine(_outputPath, "validation-report.md"), md.ToString());
    }

    private void AddMarkdownRow(StringBuilder md, MetricValidation validation)
    {
        var status = validation.Passed ? "✅ PASS" : "❌ FAIL";
        md.AppendLine($"| {validation.MetricName} | {validation.Target} | {validation.CurrentValue} | {status} |");
    }
}

public class ValidationReport
{
    public DateTime ValidatedAt { get; set; }
    public BaselineMetrics BaselineMetrics { get; set; } = new();
    public MetricValidation TestsPassConsistently { get; set; } = new();
    public MetricValidation ExecutionTimeReduced { get; set; } = new();
    public MetricValidation NoPathHandlingFailures { get; set; } = new();
    public MetricValidation NoExternalDependencies { get; set; } = new();
    public MetricValidation CoverageAbove80 { get; set; } = new();
    public bool OverallSuccess { get; set; }
    public List<string> Recommendations { get; set; } = new();
}

public class BaselineMetrics
{
    public DateTime RecordedAt { get; set; }
    public int TotalTests { get; set; }
    public int PassedTests { get; set; }
    public int FailedTests { get; set; }
    public int SkippedTests { get; set; }
    public TimeSpan ExecutionTime { get; set; }
}

public class MetricValidation
{
    public string MetricName { get; set; } = string.Empty;
    public string Target { get; set; } = string.Empty;
    public string CurrentValue { get; set; } = string.Empty;
    public bool Passed { get; set; }
    public string? Details { get; set; }
}