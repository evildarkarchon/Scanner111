using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Scanner111.CLI.Models;
using Scanner111.CLI.Services;
using Scanner111.Core.Analyzers;
using Scanner111.Core.Infrastructure;
using Scanner111.Core.Models;
using Scanner111.Core.Pipeline;
using Scanner111.Core.Services;
using Spectre.Console;

namespace Scanner111.CLI.Commands;

public class WatchCommand : ICommand<WatchOptions>, IDisposable
{
    private readonly IServiceProvider _serviceProvider;
    private readonly IApplicationSettingsService _settingsService;
    private readonly IScanPipeline _scanPipeline;
    private readonly IReportWriter _reportWriter;
    private readonly ConcurrentDictionary<string, DateTime> _processedFiles = new();
    private readonly ConcurrentDictionary<string, ScanStatistics> _statistics = new();
    private FileSystemWatcher? _watcher;
    private CancellationTokenSource? _cancellationTokenSource;

    public WatchCommand(
        IServiceProvider serviceProvider,
        IApplicationSettingsService settingsService,
        IScanPipeline scanPipeline,
        IReportWriter reportWriter)
    {
        _serviceProvider = serviceProvider;
        _settingsService = settingsService;
        _scanPipeline = scanPipeline;
        _reportWriter = reportWriter;
    }

    public async Task<int> ExecuteAsync(WatchOptions options)
    {
        return await ExecuteAsync(options, CancellationToken.None);
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
        if (options.ShowDashboard)
        {
            AnsiConsole.Clear();
        }
        
        if (options.ShowDashboard)
        {
            await RunWithDashboard(watchPath, options, _cancellationTokenSource.Token);
        }
        else
        {
            await RunSimpleWatch(watchPath, options, _cancellationTokenSource.Token);
        }

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
                if (options.ScanExisting)
                {
                    await ScanExistingFiles(watchPath, options, cancellationToken);
                }

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

        if (options.ScanExisting)
        {
            await ScanExistingFiles(watchPath, options, cancellationToken);
        }

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
        {
            if (DateTime.Now - lastProcessed < TimeSpan.FromSeconds(2))
                return;
        }

        await ProcessNewFile(filePath, options);
    }

    private async Task OnFileChanged(string filePath, WatchOptions options)
    {
        // Check if we've already processed this file recently (debounce)
        if (_processedFiles.TryGetValue(filePath, out var lastProcessed))
        {
            if (DateTime.Now - lastProcessed < TimeSpan.FromSeconds(2))
                return;
        }

        await ProcessNewFile(filePath, options);
    }

    private async Task ProcessNewFile(string filePath, WatchOptions options)
    {
        try
        {
            // Wait a moment for file write to complete
            await Task.Delay(500);

            if (!File.Exists(filePath))
                return;

            _processedFiles[filePath] = DateTime.Now;

            if (options.ShowNotifications)
            {
                AnsiConsole.MarkupLine($"\n[yellow]New crash log detected:[/] {Path.GetFileName(filePath)}");
            }

            ScanResult? scanResult = null;
            try
            {
                // Run analysis using the simpler pipeline method
                scanResult = await _scanPipeline.ProcessSingleAsync(filePath, CancellationToken.None);

                // Update statistics
                UpdateStatistics(filePath, scanResult);

                // Generate report
                var reportWritten = await _reportWriter.WriteReportAsync(scanResult, CancellationToken.None);

                if (options.ShowNotifications)
                {
                    var hasIssues = scanResult.AnalysisResults.Any(r => r.HasFindings);
                    var issueCount = scanResult.AnalysisResults.Count(r => r.HasFindings);
                    var statusIcon = hasIssues ? "[red]✗[/]" : "[green]✓[/]";
                    AnsiConsole.MarkupLine($"{statusIcon} Analysis complete: {issueCount} issues found");
                    if (reportWritten)
                    {
                        AnsiConsole.MarkupLine($"[dim]Report saved to: {Path.GetFileName(scanResult.OutputPath)}[/]");
                    }
                }
                
                // Move file if requested
                var moveIssueCount = scanResult.AnalysisResults.Count(r => r.HasFindings);
                if (options.AutoMove && moveIssueCount == 0)
                {
                    var solvedDir = Path.Combine(Path.GetDirectoryName(filePath)!, "Solved");
                    Directory.CreateDirectory(solvedDir);
                    var destPath = Path.Combine(solvedDir, Path.GetFileName(filePath));
                    File.Move(filePath, destPath, true);
                    
                    if (options.ShowNotifications)
                    {
                        AnsiConsole.MarkupLine($"[dim]Moved to: Solved/{Path.GetFileName(filePath)}[/]");
                    }
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
        }
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
            AnsiConsole.MarkupLine("[yellow]No existing crash logs found[/]");
            return;
        }

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

    public void Dispose()
    {
        _watcher?.Dispose();
        _cancellationTokenSource?.Dispose();
    }
}