using Scanner111.CLI.Models;
using Scanner111.Core.Infrastructure;
using Scanner111.Core.Services;

namespace Scanner111.CLI.Commands;

public class StatsCommand : ICommand<StatsOptions>
{
    private readonly IConsoleService _consoleService;
    private readonly IMessageHandler _messageHandler;
    private readonly IStatisticsService? _statisticsService;

    public StatsCommand(
        IStatisticsService? statisticsService,
        IMessageHandler messageHandler,
        IConsoleService? consoleService = null)
    {
        _statisticsService = statisticsService;
        _messageHandler = Guard.NotNull(messageHandler, nameof(messageHandler));
        _consoleService = consoleService ?? new ConsoleService();
    }

    public async Task<int> ExecuteAsync(StatsOptions options)
    {
        try
        {
            if (_statisticsService == null)
            {
                _messageHandler.ShowError("Statistics service is not available");
                return 1;
            }

            // Handle clear option
            if (options.Clear) return await ClearStatisticsAsync();

            // Handle export option
            if (!string.IsNullOrEmpty(options.ExportPath)) return await ExportStatisticsAsync(options.ExportPath);

            // Display statistics
            await DisplayStatisticsAsync(options);

            return 0;
        }
        catch (Exception ex)
        {
            _messageHandler.ShowCritical($"Error accessing statistics: {ex.Message}");
            return 1;
        }
    }

    private async Task<int> ClearStatisticsAsync()
    {
        AnsiConsole.MarkupLine("[yellow]Are you sure you want to clear all statistics?[/] [dim](y/N)[/]");
        var response = _consoleService.ReadLine()?.Trim().ToLowerInvariant();

        if (response == "y" || response == "yes")
        {
            await _statisticsService!.ClearStatisticsAsync();
            _messageHandler.ShowSuccess("Statistics cleared successfully");
            return 0;
        }

        _messageHandler.ShowInfo("Clear operation cancelled");
        return 0;
    }

    private async Task<int> ExportStatisticsAsync(string exportPath)
    {
        try
        {
            var allScans = await _statisticsService!.GetRecentScansAsync(int.MaxValue);
            var csv = new StringBuilder();

            csv.AppendLine(
                "Timestamp,LogFile,GameType,TotalIssues,Critical,Warning,Info,ProcessingTime,Solved,PrimaryIssueType");

            foreach (var scan in allScans)
                csv.AppendLine($"{scan.Timestamp:yyyy-MM-dd HH:mm:ss}," +
                               $"\"{Path.GetFileName(scan.LogFilePath)}\"," +
                               $"{scan.GameType}," +
                               $"{scan.TotalIssuesFound}," +
                               $"{scan.CriticalIssues}," +
                               $"{scan.WarningIssues}," +
                               $"{scan.InfoIssues}," +
                               $"{scan.ProcessingTime.TotalMilliseconds:F0}," +
                               $"{scan.WasSolved}," +
                               $"{scan.PrimaryIssueType}");

            await File.WriteAllTextAsync(exportPath, csv.ToString());
            _messageHandler.ShowSuccess($"Statistics exported to {exportPath}");
            return 0;
        }
        catch (Exception ex)
        {
            _messageHandler.ShowError($"Failed to export statistics: {ex.Message}");
            return 1;
        }
    }

    private async Task DisplayStatisticsAsync(StatsOptions options)
    {
        // Get date range based on period
        var (startDate, endDate) = GetDateRange(options.Period);

        // Get statistics
        var summary = await _statisticsService!.GetSummaryAsync();
        var scansInRange = await _statisticsService.GetScansInDateRangeAsync(startDate, endDate);
        var recentScans = await _statisticsService.GetRecentScansAsync(options.RecentScans);
        var issueTypeStats = await _statisticsService.GetIssueTypeStatisticsAsync();
        var dailyStats = await _statisticsService.GetDailyStatisticsAsync(GetDaysForPeriod(options.Period));

        // Filter by game type if specified
        if (!string.IsNullOrEmpty(options.GameType))
            scansInRange = scansInRange.Where(s =>
                s.GameType?.Equals(options.GameType, StringComparison.OrdinalIgnoreCase) == true);

        // Create main table
        var table = new Table()
            .Border(TableBorder.Rounded)
            .Title($"[bold cyan]Scanner111 Statistics - {options.Period.ToUpper()}[/]");

        table.AddColumn("[bold]Metric[/]");
        table.AddColumn("[bold]Value[/]");

        // Summary stats
        table.AddRow("Total Scans", summary.TotalScans.ToString());
        table.AddRow("Successful Scans", $"[green]{summary.SuccessfulScans}[/]");
        table.AddRow("Failed Scans", $"[red]{summary.FailedScans}[/]");
        table.AddRow("Solve Rate", $"[cyan]{summary.SolveRate:F1}%[/]");
        table.AddRow("Total Issues Found", summary.TotalIssuesFound.ToString());
        table.AddRow("Average Processing Time", FormatTimeSpan(summary.AverageProcessingTime));
        table.AddRow("Total Processing Time", FormatTimeSpan(summary.TotalProcessingTime));

        AnsiConsole.Write(table);

        // Issue type breakdown
        if (issueTypeStats.Any())
        {
            var issueTable = new Table()
                .Border(TableBorder.Rounded)
                .Title($"[bold yellow]Top {options.TopIssues} Issue Types[/]");

            issueTable.AddColumn("[bold]Issue Type[/]");
            issueTable.AddColumn("[bold]Count[/]");
            issueTable.AddColumn("[bold]Percentage[/]");

            foreach (var issue in issueTypeStats.Take(options.TopIssues))
                issueTable.AddRow(
                    issue.IssueType ?? "Unknown",
                    issue.Count.ToString(),
                    $"{issue.Percentage:F1}%");

            AnsiConsole.Write(issueTable);
        }

        // Daily statistics chart
        if (dailyStats.Any() && options.Detailed)
        {
            var chart = new BarChart()
                .Width(60)
                .Label("[bold underline]Daily Scan Activity[/]")
                .CenterLabel();

            foreach (var day in dailyStats.OrderBy(d => d.Date))
                chart.AddItem(day.Date.ToString("MMM dd"), day.ScanCount, Color.Cyan1);

            AnsiConsole.Write(chart);
        }

        // Recent scans
        if (recentScans.Any())
        {
            var recentTable = new Table()
                .Border(TableBorder.Rounded)
                .Title($"[bold magenta]Recent {options.RecentScans} Scans[/]");

            recentTable.AddColumn("[bold]Time[/]");
            recentTable.AddColumn("[bold]File[/]");
            recentTable.AddColumn("[bold]Game[/]");
            recentTable.AddColumn("[bold]Issues[/]");
            recentTable.AddColumn("[bold]Status[/]");

            foreach (var scan in recentScans.Take(options.RecentScans))
            {
                var status = scan.WasSolved ? "[green]Solved[/]" : "[red]Unsolved[/]";
                var issues = scan.TotalIssuesFound > 0
                    ? $"[yellow]{scan.TotalIssuesFound}[/] ([red]{scan.CriticalIssues}[/]/[yellow]{scan.WarningIssues}[/]/[dim]{scan.InfoIssues}[/])"
                    : "[green]0[/]";

                recentTable.AddRow(
                    scan.Timestamp.ToString("HH:mm:ss"),
                    Path.GetFileName(scan.LogFilePath),
                    scan.GameType ?? "Unknown",
                    issues,
                    status);
            }

            AnsiConsole.Write(recentTable);
        }

        // Period-specific stats
        if (scansInRange.Any())
        {
            var periodScans = scansInRange.ToList();
            AnsiConsole.MarkupLine($"\n[bold]Period Statistics ({options.Period}):[/]");
            AnsiConsole.MarkupLine($"  Scans: {periodScans.Count}");
            AnsiConsole.MarkupLine($"  Issues Found: {periodScans.Sum(s => s.TotalIssuesFound)}");
            AnsiConsole.MarkupLine($"  Critical Issues: [red]{periodScans.Sum(s => s.CriticalIssues)}[/]");
            AnsiConsole.MarkupLine($"  Warnings: [yellow]{periodScans.Sum(s => s.WarningIssues)}[/]");
            AnsiConsole.MarkupLine($"  Info: [dim]{periodScans.Sum(s => s.InfoIssues)}[/]");
        }
    }

    private (DateTime startDate, DateTime endDate) GetDateRange(string period)
    {
        var now = DateTime.Now;
        var endDate = now;
        DateTime startDate;

        switch (period.ToLowerInvariant())
        {
            case "today":
                startDate = now.Date;
                break;
            case "week":
                startDate = now.AddDays(-7);
                break;
            case "month":
                startDate = now.AddMonths(-1);
                break;
            case "year":
                startDate = now.AddYears(-1);
                break;
            case "all":
                startDate = DateTime.MinValue;
                break;
            default:
                // Try to parse as number of days
                if (int.TryParse(period, out var days))
                    startDate = now.AddDays(-days);
                else
                    startDate = now.AddDays(-7); // Default to week
                break;
        }

        return (startDate, endDate);
    }

    private int GetDaysForPeriod(string period)
    {
        switch (period.ToLowerInvariant())
        {
            case "today": return 1;
            case "week": return 7;
            case "month": return 30;
            case "year": return 365;
            case "all": return 365;
            default:
                return int.TryParse(period, out var days) ? days : 7;
        }
    }

    private string FormatTimeSpan(TimeSpan timeSpan)
    {
        if (timeSpan.TotalHours >= 1)
            return $"{timeSpan.TotalHours:F1} hours";
        if (timeSpan.TotalMinutes >= 1)
            return $"{timeSpan.TotalMinutes:F1} minutes";
        if (timeSpan.TotalSeconds >= 1)
            return $"{timeSpan.TotalSeconds:F1} seconds";
        return $"{timeSpan.TotalMilliseconds:F0} ms";
    }
}