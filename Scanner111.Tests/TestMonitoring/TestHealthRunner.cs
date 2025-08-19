using System;
using System.Linq;
using System.Threading.Tasks;
using Spectre.Console;

namespace Scanner111.Tests.TestMonitoring;

public class TestHealthRunner
{
    public static async Task<int> Main(string[] args)
    {
        if (args.Length == 0)
        {
            ShowHelp();
            return 0;
        }

        var command = args[0].ToLower();
        
        try
        {
            switch (command)
            {
                case "dashboard":
                    var interactive = args.Contains("--interactive");
                    var dashboardOutput = GetArgValue(args, "--output") ?? "TestResults/Dashboard";
                    await RunDashboardAsync(interactive, dashboardOutput);
                    break;
                    
                case "validate":
                    var validateOutput = GetArgValue(args, "--output") ?? "TestResults/ValidationReport";
                    await RunValidationAsync(validateOutput);
                    break;
                    
                case "flaky":
                    var runs = int.Parse(GetArgValue(args, "--runs") ?? "10");
                    var flakyOutput = GetArgValue(args, "--output") ?? "TestResults/FlakyTests";
                    await RunFlakyDetectionAsync(runs, flakyOutput);
                    break;
                    
                case "coverage":
                    var coverageOutput = GetArgValue(args, "--output") ?? "TestResults/Coverage";
                    await RunCoverageReportAsync(coverageOutput);
                    break;
                    
                case "monitor":
                    var continuous = args.Contains("--continuous");
                    var interval = int.Parse(GetArgValue(args, "--interval") ?? "300");
                    await RunFullMonitoringAsync(continuous, interval);
                    break;
                    
                case "help":
                case "--help":
                case "-h":
                    ShowHelp();
                    break;
                    
                default:
                    AnsiConsole.MarkupLine($"[red]Unknown command: {command}[/]");
                    ShowHelp();
                    return 1;
            }
            
            return 0;
        }
        catch (Exception ex)
        {
            AnsiConsole.WriteException(ex);
            return 1;
        }
    }
    
    private static void ShowHelp()
    {
        AnsiConsole.Write(new FigletText("Test Health Monitor")
            .Color(Color.Cyan1)
            .Centered());
            
        AnsiConsole.MarkupLine("[cyan]Available Commands:[/]");
        AnsiConsole.MarkupLine("  [green]dashboard[/]    Generate test health dashboard");
        AnsiConsole.MarkupLine("               Options: --interactive, --output <dir>");
        AnsiConsole.MarkupLine("  [green]validate[/]     Validate success metrics");
        AnsiConsole.MarkupLine("               Options: --output <dir>");
        AnsiConsole.MarkupLine("  [green]flaky[/]        Detect flaky tests");
        AnsiConsole.MarkupLine("               Options: --runs <n>, --output <dir>");
        AnsiConsole.MarkupLine("  [green]coverage[/]     Generate coverage report");
        AnsiConsole.MarkupLine("               Options: --output <dir>");
        AnsiConsole.MarkupLine("  [green]monitor[/]      Run all monitoring tools");
        AnsiConsole.MarkupLine("               Options: --continuous, --interval <seconds>");
        AnsiConsole.MarkupLine("  [green]help[/]         Show this help message");
    }
    
    private static string? GetArgValue(string[] args, string argName)
    {
        var index = Array.IndexOf(args, argName);
        if (index >= 0 && index < args.Length - 1)
        {
            return args[index + 1];
        }
        return null;
    }

    private static async Task RunDashboardAsync(bool interactive, string outputDir)
    {
        AnsiConsole.Write(new FigletText("Test Health Dashboard")
            .Color(Color.Cyan1)
            .Centered());

        var dashboard = new TestHealthDashboard(outputDirectory: outputDir);
        
        await AnsiConsole.Progress()
            .AutoClear(false)
            .Columns(new ProgressColumn[]
            {
                new TaskDescriptionColumn(),
                new ProgressBarColumn(),
                new PercentageColumn(),
                new SpinnerColumn()
            })
            .StartAsync(async ctx =>
            {
                var task = ctx.AddTask("[cyan]Generating dashboard...[/]");
                
                await dashboard.GenerateDashboardAsync();
                task.Increment(100);
            });

        AnsiConsole.MarkupLine($"[green]✅ Dashboard generated in {outputDir}[/]");
        
        if (interactive)
        {
            AnsiConsole.MarkupLine("[yellow]Press any key to exit...[/]");
            Console.ReadKey();
        }
    }

    private static async Task RunValidationAsync(string outputDir)
    {
        var validator = new SuccessMetricsValidator(outputDir);
        var report = await validator.ValidateAllMetricsAsync();
        
        AnsiConsole.MarkupLine($"[green]✅ Validation report generated in {outputDir}[/]");
        
        if (!report.OverallSuccess)
        {
            Environment.Exit(1);
        }
    }

    private static async Task RunFlakyDetectionAsync(int runs, string outputDir)
    {
        AnsiConsole.Write(new FigletText("Flaky Test Detection")
            .Color(Color.Yellow)
            .Centered());

        var detector = new FlakyTestDetector();
        
        var analysis = await AnsiConsole.Progress()
            .AutoClear(false)
            .Columns(new ProgressColumn[]
            {
                new TaskDescriptionColumn(),
                new ProgressBarColumn(),
                new PercentageColumn(),
                new SpinnerColumn()
            })
            .StartAsync(async ctx =>
            {
                var task = ctx.AddTask($"[yellow]Analyzing {runs} test runs...[/]");
                var result = await detector.AnalyzeTestRunsAsync(runs);
                task.Increment(100);
                return result;
            });

        if (analysis.InsufficientData)
        {
            AnsiConsole.MarkupLine("[red]❌ Insufficient data for flaky test analysis[/]");
            AnsiConsole.MarkupLine("[yellow]Run tests multiple times to generate historical data[/]");
        }
        else
        {
            var table = new Table()
                .Border(TableBorder.Rounded)
                .BorderColor(Color.Yellow)
                .AddColumn("Test Name")
                .AddColumn("Flakiness Score")
                .AddColumn("Success Rate")
                .AddColumn("Recommended Action");

            foreach (var test in analysis.FlakyTests.Take(10))
            {
                var scoreColor = test.FlakinessScore switch
                {
                    < 0.3 => "green",
                    < 0.7 => "yellow",
                    _ => "red"
                };
                
                table.AddRow(
                    test.TestName,
                    $"[{scoreColor}]{test.FlakinessScore:F2}[/]",
                    $"{test.SuccessRate:P0}",
                    test.RecommendedAction
                );
            }

            AnsiConsole.Write(table);
            AnsiConsole.MarkupLine($"[green]✅ Found {analysis.FlakyTests.Count} flaky tests[/]");
        }
    }

    private static async Task RunCoverageReportAsync(string outputDir)
    {
        AnsiConsole.Write(new FigletText("Coverage Report")
            .Color(Color.Blue)
            .Centered());

        var reporter = new CoverageReporter(outputDirectory: outputDir);
        
        var report = await AnsiConsole.Progress()
            .AutoClear(false)
            .Columns(new ProgressColumn[]
            {
                new TaskDescriptionColumn(),
                new ProgressBarColumn(),
                new PercentageColumn(),
                new SpinnerColumn()
            })
            .StartAsync(async ctx =>
            {
                var task = ctx.AddTask("[blue]Analyzing coverage data...[/]");
                var result = await reporter.AnalyzeCoverageAsync();
                
                if (result.HasCoverageData)
                {
                    task.Description = "[blue]Generating reports...[/]";
                    await reporter.GenerateCoverageReportsAsync(result);
                }
                
                task.Increment(100);
                return result;
            });

        if (!report.HasCoverageData)
        {
            AnsiConsole.MarkupLine("[red]❌ No coverage data found[/]");
            AnsiConsole.MarkupLine("[yellow]Run tests with --collect:\"XPlat Code Coverage\" to generate coverage data[/]");
        }
        else
        {
            var panel = new Panel(new Rows(
                new Text($"Line Coverage: {report.LineRate:P1}", new Style(
                    report.LineRate >= 0.8 ? Color.Green : report.LineRate >= 0.6 ? Color.Yellow : Color.Red)),
                new Text($"Branch Coverage: {report.BranchRate:P1}", new Style(
                    report.BranchRate >= 0.7 ? Color.Green : report.BranchRate >= 0.5 ? Color.Yellow : Color.Red)),
                new Text($"Lines Covered: {report.LinesCovered}/{report.LinesValid}")
            ))
            .Header("[blue]Coverage Summary[/]")
            .BorderColor(Color.Blue);

            AnsiConsole.Write(panel);
            AnsiConsole.MarkupLine($"[green]✅ Coverage reports generated in {outputDir}[/]");
        }
    }

    private static async Task RunFullMonitoringAsync(bool continuous, int intervalSeconds)
    {
        AnsiConsole.Write(new FigletText("Full Test Monitoring")
            .Color(Color.Magenta1)
            .Centered());

        do
        {
            var layout = new Layout("Root")
                .SplitRows(
                    new Layout("Header"),
                    new Layout("Body")
                        .SplitColumns(
                            new Layout("Left"),
                            new Layout("Right")),
                    new Layout("Footer"));

            layout["Header"].Update(new Panel(new Text($"Test Health Monitor - {DateTime.Now:yyyy-MM-dd HH:mm:ss}", 
                new Style(Color.Magenta1, decoration: Decoration.Bold)).Centered())
                .BorderColor(Color.Magenta1));

            await AnsiConsole.Live(layout)
                .AutoClear(false)
                .Overflow(VerticalOverflow.Ellipsis)
                .Cropping(VerticalOverflowCropping.Top)
                .StartAsync(async ctx =>
                {
                    var performanceTracker = new TestPerformanceTracker();
                    var flakyDetector = new FlakyTestDetector();
                    var coverageReporter = new CoverageReporter();
                    
                    await performanceTracker.LoadMetricsAsync();
                    var healthReport = performanceTracker.GenerateHealthReport();
                    
                    var performanceTable = new Table()
                        .Border(TableBorder.Rounded)
                        .AddColumn("Metric")
                        .AddColumn("Value");
                    
                    performanceTable.AddRow("Total Tests", healthReport.TotalTests.ToString());
                    performanceTable.AddRow("Total Runs", healthReport.TotalRuns.ToString());
                    performanceTable.AddRow("Avg Execution", healthReport.AverageExecutionTime.ToString("mm\\:ss"));
                    performanceTable.AddRow("Failure Rate", $"{healthReport.FailureRate:P1}");
                    
                    layout["Left"].Update(new Panel(performanceTable)
                        .Header("[green]Performance Metrics[/]")
                        .BorderColor(Color.Green));

                    var flakyAnalysis = await flakyDetector.AnalyzeTestRunsAsync(5);
                    
                    var flakyTable = new Table()
                        .Border(TableBorder.Simple)
                        .AddColumn("Test")
                        .AddColumn("Score");
                    
                    foreach (var test in flakyAnalysis.FlakyTests.Take(5))
                    {
                        flakyTable.AddRow(
                            test.TestName.Length > 30 ? test.TestName.Substring(0, 27) + "..." : test.TestName,
                            $"{test.FlakinessScore:F2}"
                        );
                    }
                    
                    layout["Right"].Update(new Panel(flakyTable)
                        .Header("[yellow]Flaky Tests[/]")
                        .BorderColor(Color.Yellow));

                    var statusText = healthReport.FailureRate < 0.05 
                        ? "[green]✅ Healthy[/]" 
                        : healthReport.FailureRate < 0.15 
                            ? "[yellow]⚠ Warning[/]" 
                            : "[red]❌ Critical[/]";
                    
                    layout["Footer"].Update(new Panel(new Markup($"Status: {statusText}"))
                        .BorderColor(Color.Cyan1));
                    
                    ctx.Refresh();
                    
                    if (continuous)
                    {
                        await Task.Delay(TimeSpan.FromSeconds(intervalSeconds));
                    }
                });

            if (!continuous) break;
            
            AnsiConsole.Clear();
            
        } while (continuous);

        AnsiConsole.MarkupLine("[green]✅ Monitoring complete[/]");
    }
}

public static class TestHealthCLI
{
    public static async Task RunAsync(string[] args)
    {
        await TestHealthRunner.Main(args);
    }
}