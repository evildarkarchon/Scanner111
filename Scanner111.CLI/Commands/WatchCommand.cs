using System.Collections.Concurrent;
using Scanner111.CLI.Models;
using Scanner111.Core.Infrastructure;
using Scanner111.Core.Models;
using Scanner111.Core.Pipeline;
using Scanner111.Core.Services;

namespace Scanner111.CLI.Commands;

public class WatchCommand : ICommand<WatchOptions>, IDisposable
{
    private readonly IAudioNotificationService? _audioService;
    private readonly ConcurrentDictionary<string, DateTime> _processedFiles = new();
    private readonly IRecentItemsService? _recentItemsService;
    private readonly IReportWriter _reportWriter;
    private readonly IScanPipeline _scanPipeline;
    private readonly IServiceProvider _serviceProvider;
    private readonly IApplicationSettingsService _settingsService;
    private readonly ConcurrentDictionary<string, ScanStatistics> _statistics = new();
    private readonly IStatisticsService? _statisticsService;
    private CancellationTokenSource? _cancellationTokenSource;
    private FileSystemWatcher? _watcher;

    public WatchCommand(
        IServiceProvider serviceProvider,
        IApplicationSettingsService settingsService,
        IScanPipeline scanPipeline,
        IReportWriter reportWriter,
        IStatisticsService? statisticsService = null,
        IAudioNotificationService? audioService = null,
        IRecentItemsService? recentItemsService = null)
    {
        _serviceProvider = serviceProvider;
        _settingsService = settingsService;
        _scanPipeline = scanPipeline;
        _reportWriter = reportWriter;
        _statisticsService = statisticsService;
        _audioService = audioService;
        _recentItemsService = recentItemsService;
    }

    public async Task<int> ExecuteAsync(WatchOptions options)
    {
        return await ExecuteAsync(options, CancellationToken.None);
    }

    public void Dispose()
    {
        _watcher?.Dispose();
        _cancellationTokenSource?.Dispose();
    }

    public async Task<int> ExecuteAsync(WatchOptions options, CancellationToken cancellationToken)
    {
        _cancellationTokenSource = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);

        var watchPath = DetermineWatchPath(options);
        if (string.IsNullOrEmpty(watchPath) || !Directory.Exists(watchPath))
        {
            AnsiConsole.MarkupLine("[red]Error:[/] Invalid or missing watch path");
            return 1;
        }

        // Don't clear if not showing dashboard to preserve output
        if (options.ShowDashboard) AnsiConsole.Clear();

        if (options.ShowDashboard)
            await RunWithDashboard(watchPath, options, _cancellationTokenSource.Token);
        else
            await RunSimpleWatch(watchPath, options, _cancellationTokenSource.Token);

        return 0;
    }

    private async Task RunWithDashboard(string watchPath, WatchOptions options, CancellationToken cancellationToken)
    {
        var layout = CreateDashboardLayout();

        await AnsiConsole.Live(layout)
            .AutoClear(false)
            .Overflow(VerticalOverflow.Ellipsis)
            .StartAsync(async ctx =>
            {
                // Initialize file watcher
                InitializeWatcher(watchPath, options);

                // Scan existing files if requested
                if (options.ScanExisting) await ScanExistingFiles(watchPath, options, cancellationToken);

                UpdateDashboard(layout, watchPath, options);

                // Start monitoring
                _watcher!.EnableRaisingEvents = true;

                AnsiConsole.MarkupLine($"\n[green]Monitoring:[/] {watchPath}");
                AnsiConsole.MarkupLine("[dim]Press Ctrl+C to stop monitoring[/]\n");

                // Keep the dashboard running
                while (!cancellationToken.IsCancellationRequested)
                {
                    UpdateDashboard(layout, watchPath, options);
                    ctx.Refresh();
                    await Task.Delay(1000, cancellationToken);
                }
            });
    }

    private async Task RunSimpleWatch(string watchPath, WatchOptions options, CancellationToken cancellationToken)
    {
        AnsiConsole.MarkupLine($"[green]Monitoring:[/] {watchPath}");
        AnsiConsole.MarkupLine($"[dim]Pattern:[/] {options.Pattern}");
        AnsiConsole.MarkupLine($"[dim]Recursive:[/] {options.Recursive}");
        AnsiConsole.MarkupLine("[dim]Press Ctrl+C to stop monitoring[/]\n");

        InitializeWatcher(watchPath, options);

        if (options.ScanExisting) await ScanExistingFiles(watchPath, options, cancellationToken);

        _watcher!.EnableRaisingEvents = true;

        // Keep the process running
        var tcs = new TaskCompletionSource<bool>();
        cancellationToken.Register(() => tcs.TrySetResult(true));
        await tcs.Task;
    }

    private void InitializeWatcher(string watchPath, WatchOptions options)
    {
        _watcher = new FileSystemWatcher(watchPath)
        {
            Filter = options.Pattern,
            NotifyFilter = NotifyFilters.FileName | NotifyFilters.LastWrite | NotifyFilters.CreationTime,
            IncludeSubdirectories = options.Recursive
        };

        _watcher.Created += async (sender, e) => await OnFileCreated(e.FullPath, options);
        _watcher.Changed += async (sender, e) => await OnFileChanged(e.FullPath, options);
        _watcher.Error += OnWatcherError;
    }

    private async Task OnFileCreated(string filePath, WatchOptions options)
    {
        // Check if we've already processed this file recently (debounce)
        if (_processedFiles.TryGetValue(filePath, out var lastProcessed))
            if (DateTime.Now - lastProcessed < TimeSpan.FromSeconds(2))
                return;

        await ProcessNewFile(filePath, options);
    }

    private async Task OnFileChanged(string filePath, WatchOptions options)
    {
        // Check if we've already processed this file recently (debounce)
        if (_processedFiles.TryGetValue(filePath, out var lastProcessed))
            if (DateTime.Now - lastProcessed < TimeSpan.FromSeconds(2))
                return;

        await ProcessNewFile(filePath, options);
    }

    private async Task ProcessNewFile(string filePath, WatchOptions options)
    {
        var stopwatch = Stopwatch.StartNew();
        try
        {
            // Wait a moment for file write to complete
            await Task.Delay(500);

            if (!File.Exists(filePath))
                return;

            _processedFiles[filePath] = DateTime.Now;

            // Track recent file
            _recentItemsService?.AddRecentLogFile(filePath);

            if (options.ShowNotifications)
                AnsiConsole.MarkupLine($"\n[yellow]New crash log detected:[/] {Path.GetFileName(filePath)}");

            ScanResult? scanResult = null;
            try
            {
                // Run analysis using the simpler pipeline method
                scanResult = await _scanPipeline.ProcessSingleAsync(filePath, CancellationToken.None);

                // Update statistics
                UpdateStatistics(filePath, scanResult);
                stopwatch.Stop();

                // Record to statistics service
                await RecordStatisticsAsync(scanResult, stopwatch.Elapsed);

                // Generate report
                var reportWritten = await _reportWriter.WriteReportAsync(scanResult, CancellationToken.None);

                if (options.ShowNotifications)
                {
                    var hasIssues = scanResult.AnalysisResults.Any(r => r.HasFindings);
                    var issueCount = scanResult.AnalysisResults.Count(r => r.HasFindings);
                    var statusIcon = hasIssues ? "[red]✗[/]" : "[green]✓[/]";
                    AnsiConsole.MarkupLine($"{statusIcon} Analysis complete: {issueCount} issues found");
                    if (reportWritten)
                        AnsiConsole.MarkupLine($"[dim]Report saved to: {Path.GetFileName(scanResult.OutputPath)}[/]");
                }

                // Play audio notification
                await PlayNotificationAsync(scanResult);

                // Move file if requested
                var moveIssueCount = scanResult.AnalysisResults.Count(r => r.HasFindings);
                if (options.AutoMove && moveIssueCount == 0)
                {
                    var solvedDir = Path.Combine(Path.GetDirectoryName(filePath)!, "Solved");
                    Directory.CreateDirectory(solvedDir);
                    var destPath = Path.Combine(solvedDir, Path.GetFileName(filePath));
                    File.Move(filePath, destPath, true);

                    if (options.ShowNotifications)
                        AnsiConsole.MarkupLine($"[dim]Moved to: Solved/{Path.GetFileName(filePath)}[/]");
                }
            }
            catch (InvalidOperationException ex) when (ex.Message.Contains("interactive functions concurrently"))
            {
                // Fallback to simple console output if there's a conflict with live displays
                Console.WriteLine($"Analyzed: {Path.GetFileName(filePath)}");
            }
        }
        catch (Exception ex)
        {
            AnsiConsole.MarkupLine($"[red]Error processing {Path.GetFileName(filePath)}:[/] {ex.Message}");
            
            // Play error sound for operation failure
            if (_audioService != null && _audioService.IsEnabled)
            {
                try
                {
                    await _audioService.PlayErrorFoundAsync();
                }
                catch
                {
                    // Ignore audio playback errors
                }
            }
        }
    }

    private async Task RecordStatisticsAsync(ScanResult result, TimeSpan processingTime)
    {
        if (_statisticsService == null) return;

        try
        {
            var issuesByType = new Dictionary<string, int>();
            var criticalCount = 0;
            var warningCount = 0;
            var infoCount = 0;
            string? primaryIssueType = null;
            var maxIssueCount = 0;

            foreach (var analysis in result.AnalysisResults)
            {
                if (!analysis.HasFindings) continue;

                var analyzerName = analysis.AnalyzerName;
                var issueCount = analysis.HasFindings ? 1 : 0;

                if (issuesByType.ContainsKey(analyzerName))
                    issuesByType[analyzerName] += issueCount;
                else
                    issuesByType[analyzerName] = issueCount;

                if (issueCount > maxIssueCount)
                {
                    maxIssueCount = issueCount;
                    primaryIssueType = analyzerName;
                }

                // Since AnalysisResult doesn't have severity levels, count all findings as warnings
                if (analysis.HasFindings)
                    warningCount++;
            }

            var statistics = new Core.Services.ScanStatistics
            {
                Timestamp = DateTime.Now,
                LogFilePath = result.LogPath,
                GameType = DetectGameType(result.LogPath),
                TotalIssuesFound = criticalCount + warningCount + infoCount,
                CriticalIssues = criticalCount,
                WarningIssues = warningCount,
                InfoIssues = infoCount,
                ProcessingTime = processingTime,
                WasSolved = criticalCount == 0,
                PrimaryIssueType = primaryIssueType,
                IssuesByType = issuesByType
            };

            await _statisticsService.RecordScanAsync(statistics);
        }
        catch (Exception ex)
        {
            // Don't fail the scan if statistics recording fails
            AnsiConsole.MarkupLine($"[dim]Failed to record statistics: {ex.Message}[/]");
        }
    }

    private async Task PlayNotificationAsync(ScanResult result)
    {
        if (_audioService == null || !_audioService.IsEnabled) return;

        try
        {
            // Play success sound - the scan completed successfully regardless of findings
            // Finding issues in a crash log is actually a successful operation
            await _audioService.PlayScanCompleteAsync();
        }
        catch (Exception ex)
        {
            // Don't fail the scan if audio fails
            AnsiConsole.MarkupLine($"[dim]Failed to play audio notification: {ex.Message}[/]");
        }
    }

    private string DetectGameType(string logPath)
    {
        try
        {
            var content = File.ReadAllText(logPath).ToLowerInvariant();
            if (content.Contains("fallout4") || content.Contains("f4se"))
                return "Fallout4";
            if (content.Contains("skyrim") || content.Contains("skse"))
                return "Skyrim";
        }
        catch
        {
            // Ignore errors reading file
        }

        return "Unknown";
    }

    private void OnWatcherError(object sender, ErrorEventArgs e)
    {
        AnsiConsole.MarkupLine($"[red]Watcher error:[/] {e.GetException().Message}");
    }

    private async Task ScanExistingFiles(string watchPath, WatchOptions options, CancellationToken cancellationToken)
    {
        var searchOption = options.Recursive ? SearchOption.AllDirectories : SearchOption.TopDirectoryOnly;
        var existingFiles = Directory.GetFiles(watchPath, options.Pattern, searchOption);

        if (existingFiles.Length == 0)
        {
            if (!options.ShowDashboard && options.ShowNotifications)
                AnsiConsole.MarkupLine("[yellow]No existing crash logs found[/]");
            return;
        }

        // Skip progress display when notifications are disabled (typically in tests)
        if (!options.ShowNotifications)
        {
            foreach (var file in existingFiles)
            {
                if (cancellationToken.IsCancellationRequested)
                    break;

                await ProcessNewFile(file, options);
            }
        }
        else
        {
            AnsiConsole.MarkupLine($"[cyan]Scanning {existingFiles.Length} existing files...[/]");

            await AnsiConsole.Progress()
                .AutoClear(false)
                .HideCompleted(false)
                .StartAsync(async ctx =>
                {
                    var task = ctx.AddTask("[cyan]Scanning existing logs[/]", maxValue: existingFiles.Length);

                    foreach (var file in existingFiles)
                    {
                        if (cancellationToken.IsCancellationRequested)
                            break;

                        await ProcessNewFile(file, options);
                        task.Increment(1);
                    }
                });
        }
    }

    private string DetermineWatchPath(WatchOptions options)
    {
        if (!string.IsNullOrEmpty(options.Path))
            return options.Path;

        // Try to determine from game type
        if (!string.IsNullOrEmpty(options.Game))
        {
            var documentsPath = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);
            return options.Game.ToLower() switch
            {
                "fallout4" => Path.Combine(documentsPath, "My Games", "Fallout4", "F4SE"),
                "skyrim" => Path.Combine(documentsPath, "My Games", "Skyrim Special Edition", "SKSE"),
                _ => Directory.GetCurrentDirectory()
            };
        }

        return Directory.GetCurrentDirectory();
    }

    private Layout CreateDashboardLayout()
    {
        return new Layout()
            .SplitRows(
                new Layout("header").Size(5),
                new Layout("body").SplitColumns(
                    new Layout("stats").Size(40),
                    new Layout("recent").Size(60)
                ),
                new Layout("footer").Size(3)
            );
    }

    private void UpdateDashboard(Layout layout, string watchPath, WatchOptions options)
    {
        // Header
        var headerPanel = new Panel(
                Align.Center(
                    new FigletText("Scanner111")
                        .Color(Color.Cyan1),
                    VerticalAlignment.Middle))
            .Border(BoxBorder.Rounded);
        layout["header"].Update(headerPanel);

        // Statistics
        var statsTable = new Table()
            .Border(TableBorder.Rounded)
            .Title("[bold]Statistics[/]")
            .AddColumn("Metric")
            .AddColumn("Value");

        statsTable.AddRow("Monitoring", Path.GetFileName(watchPath));
        statsTable.AddRow("Pattern", options.Pattern);
        statsTable.AddRow("Files Processed", _processedFiles.Count.ToString());
        statsTable.AddRow("Total Issues", _statistics.Values.Sum(s => s.IssueCount).ToString());
        statsTable.AddRow("Critical Issues", _statistics.Values.Sum(s => s.CriticalCount).ToString());
        statsTable.AddRow("Warnings", _statistics.Values.Sum(s => s.WarningCount).ToString());

        layout["stats"].Update(new Panel(statsTable).Header("[yellow]Live Statistics[/]"));

        // Recent files
        var recentTable = new Table()
            .Border(TableBorder.Rounded)
            .Title("[bold]Recent Files[/]")
            .AddColumn("Time")
            .AddColumn("File")
            .AddColumn("Status");

        foreach (var file in _processedFiles.OrderByDescending(f => f.Value).Take(10))
        {
            var stats = _statistics.GetValueOrDefault(file.Key);
            var status = stats?.IssueCount > 0
                ? $"[red]{stats.IssueCount} issues[/]"
                : "[green]Clean[/]";

            recentTable.AddRow(
                file.Value.ToString("HH:mm:ss"),
                Path.GetFileName(file.Key),
                status);
        }

        layout["recent"].Update(new Panel(recentTable).Header("[cyan]Recent Scans[/]"));

        // Footer
        var footerText = new Markup($"[dim]Watching: {watchPath} | Press Ctrl+C to stop[/]");
        layout["footer"].Update(new Panel(footerText).Border(BoxBorder.None));
    }

    private void UpdateStatistics(string filePath, ScanResult scanResult)
    {
        var stats = new ScanStatistics
        {
            IssueCount = scanResult.AnalysisResults.Count(r => r.HasFindings),
            CriticalCount = 0, // Severity not available in AnalysisResult
            WarningCount = 0,
            InfoCount = 0
        };

        _statistics[filePath] = stats;
    }

    private class ScanStatistics
    {
        public int IssueCount { get; set; }
        public int CriticalCount { get; set; }
        public int WarningCount { get; set; }
        public int InfoCount { get; set; }
    }
}