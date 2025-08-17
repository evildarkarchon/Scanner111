using System.Collections.Concurrent;
using Scanner111.CLI.Models;
using Scanner111.Core.Models;
using Scanner111.Core.Services;

namespace Scanner111.CLI.Commands;

public class PapyrusCommand : ICommand<PapyrusOptions>, IDisposable
{
    private readonly ConcurrentQueue<string> _logMessages = new();
    private readonly IPapyrusMonitorService _papyrusService;
    private CancellationTokenSource? _cancellationTokenSource;
    private bool _disposed;
    private Timer? _exportTimer;

    public PapyrusCommand(IPapyrusMonitorService papyrusService)
    {
        _papyrusService = papyrusService;
    }

    public async Task<int> ExecuteAsync(PapyrusOptions options)
    {
        return await ExecuteAsync(options, CancellationToken.None);
    }

    public void Dispose()
    {
        if (_disposed) return;

        _exportTimer?.Dispose();
        _cancellationTokenSource?.Cancel();
        _cancellationTokenSource?.Dispose();
        _papyrusService?.Dispose();
        _disposed = true;
    }

    public async Task<int> ExecuteAsync(PapyrusOptions options, CancellationToken cancellationToken)
    {
        _cancellationTokenSource = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);

        try
        {
            // Determine log path
            var logPath = await DetermineLogPathAsync(options).ConfigureAwait(false);
            if (string.IsNullOrEmpty(logPath))
            {
                AnsiConsole.MarkupLine("[red]Error:[/] Could not determine Papyrus log path");
                AnsiConsole.MarkupLine("[yellow]Tip:[/] Use -p to specify the path or -g to specify the game type");
                return 1;
            }

            AnsiConsole.MarkupLine($"[green]Papyrus Log:[/] {logPath}");

            // One-time analysis
            if (options.AnalyzeOnce) return await AnalyzeOnceAsync(logPath, options);

            // Continuous monitoring
            if (options.ShowDashboard)
                return await RunWithDashboardAsync(logPath, options, _cancellationTokenSource.Token);

            return await RunSimpleMonitoringAsync(logPath, options, _cancellationTokenSource.Token);
        }
        catch (Exception ex)
        {
            AnsiConsole.WriteException(ex);
            return 1;
        }
    }

    private async Task<string?> DetermineLogPathAsync(PapyrusOptions options)
    {
        if (!string.IsNullOrEmpty(options.LogPath)) return options.LogPath;

        if (options.GameType.HasValue)
            return await _papyrusService.DetectLogPathAsync(options.GameType.Value).ConfigureAwait(false);

        // Try to auto-detect for both games
        var fallout4Path = await _papyrusService.DetectLogPathAsync(GameType.Fallout4).ConfigureAwait(false);
        if (!string.IsNullOrEmpty(fallout4Path))
        {
            AnsiConsole.MarkupLine("[dim]Auto-detected Fallout 4 Papyrus log[/]");
            return fallout4Path;
        }

        var skyrimPath = await _papyrusService.DetectLogPathAsync(GameType.Skyrim).ConfigureAwait(false);
        if (!string.IsNullOrEmpty(skyrimPath))
        {
            AnsiConsole.MarkupLine("[dim]Auto-detected Skyrim Papyrus log[/]");
            return skyrimPath;
        }

        return null;
    }

    private async Task<int> AnalyzeOnceAsync(string logPath, PapyrusOptions options)
    {
        await AnsiConsole.Status()
            .StartAsync("Analyzing Papyrus log...", async ctx =>
            {
                var stats = await _papyrusService.AnalyzeLogAsync(logPath).ConfigureAwait(false);
                DisplayStats(stats, options);

                if (!string.IsNullOrEmpty(options.ExportPath))
                {
                    ctx.Status("Exporting statistics...");
                    await _papyrusService.ExportStatsAsync(options.ExportPath, options.ExportFormat)
                        .ConfigureAwait(false);
                    AnsiConsole.MarkupLine($"[green]✓[/] Statistics exported to: {options.ExportPath}");
                }
            });

        return 0;
    }

    private async Task<int> RunWithDashboardAsync(string logPath, PapyrusOptions options,
        CancellationToken cancellationToken)
    {
        var layout = CreateDashboardLayout();

        // Set up auto-export if requested
        if (options.AutoExport && !string.IsNullOrEmpty(options.ExportPath))
            _exportTimer = new Timer(
                async _ => await ExportStatsAsync(options.ExportPath, options.ExportFormat),
                null,
                TimeSpan.FromMilliseconds(options.ExportInterval),
                TimeSpan.FromMilliseconds(options.ExportInterval));

        await AnsiConsole.Live(layout)
            .AutoClear(false)
            .Overflow(VerticalOverflow.Ellipsis)
            .StartAsync(async ctx =>
            {
                // Subscribe to events
                _papyrusService.StatsUpdated += (sender, e) => OnStatsUpdated(e, options);
                _papyrusService.Error += (sender, e) => OnError(e);

                // Set monitoring interval
                _papyrusService.MonitoringInterval = options.Interval;

                // Start monitoring
                await _papyrusService.StartMonitoringAsync(logPath, cancellationToken).ConfigureAwait(false);

                AnsiConsole.MarkupLine($"\n[green]Monitoring:[/] {logPath}");
                AnsiConsole.MarkupLine($"[dim]Update interval: {options.Interval}ms[/]");
                AnsiConsole.MarkupLine("[dim]Press Ctrl+C to stop monitoring[/]\n");

                // Keep the dashboard running
                while (!cancellationToken.IsCancellationRequested)
                {
                    UpdateDashboard(layout, options);
                    ctx.Refresh();
                    await Task.Delay(100, cancellationToken).ConfigureAwait(false);
                }
            });

        return 0;
    }

    private async Task<int> RunSimpleMonitoringAsync(string logPath, PapyrusOptions options,
        CancellationToken cancellationToken)
    {
        // Subscribe to events
        _papyrusService.StatsUpdated += (sender, e) => OnStatsUpdated(e, options);
        _papyrusService.Error += (sender, e) => OnError(e);

        // Set monitoring interval
        _papyrusService.MonitoringInterval = options.Interval;

        // Set up auto-export if requested
        if (options.AutoExport && !string.IsNullOrEmpty(options.ExportPath))
            _exportTimer = new Timer(
                async _ => await ExportStatsAsync(options.ExportPath, options.ExportFormat),
                null,
                TimeSpan.FromMilliseconds(options.ExportInterval),
                TimeSpan.FromMilliseconds(options.ExportInterval));

        // Start monitoring
        await _papyrusService.StartMonitoringAsync(logPath, cancellationToken).ConfigureAwait(false);

        AnsiConsole.MarkupLine($"[green]Monitoring:[/] {logPath}");
        AnsiConsole.MarkupLine($"[dim]Update interval: {options.Interval}ms[/]");
        AnsiConsole.MarkupLine("[dim]Press Ctrl+C to stop monitoring[/]\n");

        // Wait for cancellation
        try
        {
            await Task.Delay(Timeout.Infinite, cancellationToken).ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            // Expected when cancellation is requested
        }

        AnsiConsole.MarkupLine("\n[yellow]Monitoring stopped[/]");
        return 0;
    }

    private Layout CreateDashboardLayout()
    {
        var statsPanel = new Panel(new Text("Waiting for data..."))
            .Header("[yellow]Papyrus Statistics[/]")
            .BorderColor(Color.Grey);

        var historyPanel = new Panel(new Text("No history yet..."))
            .Header("[cyan]Recent Changes[/]")
            .BorderColor(Color.Grey);

        var alertsPanel = new Panel(new Text("No alerts"))
            .Header("[red]Alerts[/]")
            .BorderColor(Color.Grey);

        var logPanel = new Panel(new Text("Waiting for messages..."))
            .Header("[blue]Log Messages[/]")
            .BorderColor(Color.Grey);

        return new Layout("Root")
            .SplitRows(
                new Layout("Top")
                    .SplitColumns(
                        new Layout("Stats", statsPanel).Ratio(2),
                        new Layout("History", historyPanel).Ratio(1)),
                new Layout("Bottom")
                    .SplitColumns(
                        new Layout("Alerts", alertsPanel).Ratio(1),
                        new Layout("Log", logPanel).Ratio(2)));
    }

    private void UpdateDashboard(Layout layout, PapyrusOptions options)
    {
        var stats = _papyrusService.CurrentStats;

        // Update stats panel
        if (stats != null)
        {
            var statsTable = new Table()
                .AddColumn("Metric")
                .AddColumn("Value")
                .AddColumn("Status");

            statsTable.AddRow("Dumps", stats.Dumps.ToString(), GetStatusEmoji(stats.Dumps, 50, 100));
            statsTable.AddRow("Stacks", stats.Stacks.ToString(), GetStatusEmoji(stats.Stacks, 100, 500));
            statsTable.AddRow("Ratio", stats.Ratio.ToString("F3"), GetRatioStatus(stats.Ratio));
            statsTable.AddRow("Warnings", stats.Warnings.ToString(),
                GetStatusEmoji(stats.Warnings, options.WarningThreshold / 2, options.WarningThreshold));
            statsTable.AddRow("Errors", stats.Errors.ToString(),
                GetStatusEmoji(stats.Errors, options.ErrorThreshold / 2, options.ErrorThreshold));
            statsTable.AddRow("Total Issues", stats.TotalIssues.ToString(),
                GetStatusEmoji(stats.TotalIssues, 500, 1000));

            layout["Stats"].Update(new Panel(statsTable)
                .Header($"[yellow]Papyrus Statistics[/] - {stats.Timestamp:HH:mm:ss}")
                .BorderColor(Color.Grey));
        }

        // Update history panel
        var history = _papyrusService.GetHistoricalStats();
        if (history.Count > 1)
        {
            var recentStats = history.TakeLast(5).ToList();
            var historyTable = new Table()
                .AddColumn("Time")
                .AddColumn("Δ Dumps")
                .AddColumn("Δ Errors");

            for (var i = 1; i < recentStats.Count; i++)
            {
                var current = recentStats[i];
                var previous = recentStats[i - 1];
                var dumpsDelta = current.Dumps - previous.Dumps;
                var errorsDelta = current.Errors - previous.Errors;

                historyTable.AddRow(
                    current.Timestamp.ToString("HH:mm:ss"),
                    FormatDelta(dumpsDelta),
                    FormatDelta(errorsDelta));
            }

            layout["History"].Update(new Panel(historyTable)
                .Header("[cyan]Recent Changes[/]")
                .BorderColor(Color.Grey));
        }

        // Update alerts panel
        if (stats != null)
        {
            var alerts = new List<string>();

            if (stats.Errors > options.ErrorThreshold)
                alerts.Add($"[red]⚠ High error count: {stats.Errors}[/]");

            if (stats.Warnings > options.WarningThreshold)
                alerts.Add($"[yellow]⚠ High warning count: {stats.Warnings}[/]");

            if (stats.Ratio > 0.5)
                alerts.Add($"[orange1]⚠ High dumps/stacks ratio: {stats.Ratio:F3}[/]");

            if (stats.HasCriticalIssues)
                alerts.Add("[red]⚠ CRITICAL ISSUES DETECTED[/]");

            var alertsContent = alerts.Count > 0
                ? string.Join("\n", alerts)
                : "[green]✓ No alerts[/]";

            layout["Alerts"].Update(new Panel(new Markup(alertsContent))
                .Header("[red]Alerts[/]")
                .BorderColor(alerts.Count > 0 ? Color.Red : Color.Grey));
        }

        // Update log panel
        var messages = _logMessages.TakeLast(10).ToList();
        if (messages.Count > 0)
        {
            var logContent = string.Join("\n", messages);
            layout["Log"].Update(new Panel(new Markup(logContent))
                .Header("[blue]Log Messages[/]")
                .BorderColor(Color.Grey));
        }
    }

    private void DisplayStats(PapyrusStats stats, PapyrusOptions options)
    {
        var table = new Table()
            .Title("Papyrus Log Analysis")
            .AddColumn("Metric")
            .AddColumn("Value")
            .AddColumn("Status");

        table.AddRow("Dumps", stats.Dumps.ToString(), GetStatusEmoji(stats.Dumps, 50, 100));
        table.AddRow("Stacks", stats.Stacks.ToString(), GetStatusEmoji(stats.Stacks, 100, 500));
        table.AddRow("Dumps/Stacks Ratio", stats.Ratio.ToString("F3"), GetRatioStatus(stats.Ratio));
        table.AddRow("Warnings", stats.Warnings.ToString(),
            GetStatusEmoji(stats.Warnings, options.WarningThreshold / 2, options.WarningThreshold));
        table.AddRow("Errors", stats.Errors.ToString(),
            GetStatusEmoji(stats.Errors, options.ErrorThreshold / 2, options.ErrorThreshold));
        table.AddRow("Total Issues", stats.TotalIssues.ToString(), GetStatusEmoji(stats.TotalIssues, 500, 1000));

        AnsiConsole.Write(table);

        if (stats.HasCriticalIssues)
        {
            AnsiConsole.MarkupLine("\n[red bold]⚠ CRITICAL ISSUES DETECTED![/]");
            AnsiConsole.MarkupLine("[yellow]Consider reviewing your mod configuration and script performance.[/]");
        }
    }

    private async void OnStatsUpdated(PapyrusStatsUpdatedEventArgs e, PapyrusOptions options)
    {
        var message = $"[dim]{e.Stats.Timestamp:HH:mm:ss}[/] Stats updated: {e.Stats}";
        _logMessages.Enqueue(message);

        // Limit message queue size
        while (_logMessages.Count > 100) _logMessages.TryDequeue(out _);

        if (!options.ShowDashboard) AnsiConsole.MarkupLine(message);

        // Check for alerts
        if (e.Stats.Errors > options.ErrorThreshold)
        {
            var alert = $"[red]Alert:[/] Error count exceeded threshold ({e.Stats.Errors} > {options.ErrorThreshold})";
            _logMessages.Enqueue(alert);

            if (!options.ShowDashboard) AnsiConsole.MarkupLine(alert);
        }

        if (e.Stats.Warnings > options.WarningThreshold)
        {
            var alert =
                $"[yellow]Alert:[/] Warning count exceeded threshold ({e.Stats.Warnings} > {options.WarningThreshold})";
            _logMessages.Enqueue(alert);

            if (!options.ShowDashboard) AnsiConsole.MarkupLine(alert);
        }
    }

    private void OnError(ErrorEventArgs e)
    {
        var errorMessage = $"[red]Error:[/] {e.GetException()?.Message ?? "Unknown error"}";
        _logMessages.Enqueue(errorMessage);
        AnsiConsole.MarkupLine(errorMessage);
    }

    private async Task ExportStatsAsync(string path, string format)
    {
        try
        {
            await _papyrusService.ExportStatsAsync(path, format).ConfigureAwait(false);
            var message = $"[green]✓[/] Auto-exported statistics to: {path}";
            _logMessages.Enqueue(message);
        }
        catch (Exception ex)
        {
            var message = $"[red]Failed to auto-export:[/] {ex.Message}";
            _logMessages.Enqueue(message);
        }
    }

    private static string GetStatusEmoji(int value, int warningThreshold, int errorThreshold)
    {
        if (value >= errorThreshold) return "[red]❌[/]";
        if (value >= warningThreshold) return "[yellow]⚠[/]";
        return "[green]✓[/]";
    }

    private static string GetRatioStatus(double ratio)
    {
        if (ratio > 0.5) return "[red]❌ High[/]";
        if (ratio > 0.3) return "[yellow]⚠ Elevated[/]";
        return "[green]✓ Normal[/]";
    }

    private static string FormatDelta(int delta)
    {
        if (delta > 0) return $"[red]+{delta}[/]";
        if (delta < 0) return $"[green]{delta}[/]";
        return "[dim]0[/]";
    }
}