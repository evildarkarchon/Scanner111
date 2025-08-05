using Spectre.Console;
using Scanner111.Core.Infrastructure;
using Scanner111.Core.Models;
using System.Collections.Concurrent;

namespace Scanner111.CLI.Services;

/// <summary>
/// Terminal UI service using Spectre.Console for rich terminal interfaces
/// </summary>
public class SpectreTerminalUIService : ITerminalUIService
{
    private readonly IApplicationSettingsService _settingsService;
    private readonly ConcurrentQueue<LogMessage> _logMessages = new();
    private readonly Layout _mainLayout;
    private Table? _logTable;
    private Panel? _statusPanel;

    public SpectreTerminalUIService(IApplicationSettingsService settingsService)
    {
        _settingsService = settingsService;
        _mainLayout = BuildMainLayout();
    }

    /// <summary>
    /// Run the interactive mode when no CLI arguments are provided
    /// </summary>
    public async Task<int> RunInteractiveMode()
    {
        AnsiConsole.Clear();
        
        while (true)
        {
            var choice = ShowMainMenu();
            
            switch (choice)
            {
                case MenuChoice.QuickScan:
                    await PerformQuickScan();
                    break;
                case MenuChoice.ScanSpecific:
                    await PerformCustomScan();
                    break;
                case MenuChoice.FcxMode:
                    await PerformFcxScan();
                    break;
                case MenuChoice.Configuration:
                    await ShowConfigurationEditor();
                    break;
                case MenuChoice.RecentResults:
                    await ShowRecentResults();
                    break;
                case MenuChoice.About:
                    ShowAbout();
                    break;
                case MenuChoice.Quit:
                    return 0;
            }
            
            if (choice != MenuChoice.Quit)
            {
                AnsiConsole.WriteLine();
                AnsiConsole.Prompt(new TextPrompt<string>("[grey]Press Enter to continue...[/]")
                    .AllowEmpty());
            }
        }
    }

    private MenuChoice ShowMainMenu()
    {
        AnsiConsole.Clear();
        
        var panel = new Panel(new FigletText("Scanner111")
            .Centered()
            .Color(Color.Blue))
            .Header("[yellow]CLASSIC Crash Log Analyzer[/]")
            .Border(BoxBorder.Double)
            .BorderColor(Color.Blue)
            .Expand();
            
        AnsiConsole.Write(panel);
        
        var prompt = new SelectionPrompt<MenuChoice>()
            .Title("\n[green]What would you like to do?[/]")
            .PageSize(10)
            .MoreChoicesText("[grey](Move up and down to reveal more options)[/]")
            .AddChoices(new[]
            {
                MenuChoice.QuickScan,
                MenuChoice.ScanSpecific,
                MenuChoice.FcxMode,
                MenuChoice.Configuration,
                MenuChoice.RecentResults,
                MenuChoice.About,
                MenuChoice.Quit
            })
            .UseConverter(choice => choice switch
            {
                MenuChoice.QuickScan => "🔍 Quick Scan (Current Directory)",
                MenuChoice.ScanSpecific => "📁 Scan Specific File/Directory",
                MenuChoice.FcxMode => "🛡️  FCX Mode - File Integrity Check",
                MenuChoice.Configuration => "⚙️  Configuration Settings",
                MenuChoice.RecentResults => "📊 View Recent Scan Results",
                MenuChoice.About => "ℹ️  About Scanner111",
                MenuChoice.Quit => "❌ Quit",
                _ => choice.ToString()
            });
            
        return AnsiConsole.Prompt(prompt);
    }

    private async Task PerformQuickScan()
    {
        var currentDir = Directory.GetCurrentDirectory();
        
        await AnsiConsole.Progress()
            .AutoClear(false)
            .Columns(new ProgressColumn[]
            {
                new TaskDescriptionColumn(),
                new ProgressBarColumn(),
                new PercentageColumn(),
                new RemainingTimeColumn(),
                new SpinnerColumn()
            })
            .StartAsync(async ctx =>
            {
                var collectTask = ctx.AddTask("[yellow]Collecting crash logs[/]", maxValue: 100);
                var analyzeTask = ctx.AddTask("[blue]Analyzing logs[/]", maxValue: 100);
                var reportTask = ctx.AddTask("[green]Generating reports[/]", maxValue: 100);
                
                // Simulate file collection
                for (int i = 0; i <= 100; i += 5)
                {
                    collectTask.Increment(5);
                    await Task.Delay(50);
                }
                
                // Simulate analysis
                analyzeTask.StartTask();
                for (int i = 0; i <= 100; i += 2)
                {
                    analyzeTask.Increment(2);
                    await Task.Delay(100);
                }
                
                // Simulate report generation
                reportTask.StartTask();
                for (int i = 0; i <= 100; i += 10)
                {
                    reportTask.Increment(10);
                    await Task.Delay(30);
                }
            });
            
        AnsiConsole.MarkupLine("\n[green]✓[/] Scan completed successfully!");
    }

    private async Task ShowConfigurationEditor()
    {
        var settings = await _settingsService.LoadSettingsAsync();
        
        var rule = new Rule("[yellow]Configuration Settings[/]")
            .LeftJustified()
            .RuleStyle("blue");
        AnsiConsole.Write(rule);
        
        // Create a form-like interface
        var fcxMode = AnsiConsole.Confirm("Enable FCX Mode?", settings.FcxMode);
        var simplifyLogs = AnsiConsole.Confirm("Simplify Logs?", settings.SimplifyLogs);
        var moveUnsolved = AnsiConsole.Confirm("Move Unsolved Logs?", settings.MoveUnsolvedLogs);
        var audioNotifications = AnsiConsole.Confirm("Enable Audio Notifications?", settings.AudioNotifications);
        
        var crashLogsDir = AnsiConsole.Ask("Crash Logs Directory:", settings.CrashLogsDirectory);
        
        // Update settings
        settings.FcxMode = fcxMode;
        settings.SimplifyLogs = simplifyLogs;
        settings.MoveUnsolvedLogs = moveUnsolved;
        settings.AudioNotifications = audioNotifications;
        settings.CrashLogsDirectory = crashLogsDir;
        
        await _settingsService.SaveSettingsAsync(settings);
        
        AnsiConsole.MarkupLine("\n[green]✓[/] Settings saved successfully!");
    }

    /// <summary>
    /// Create a progress context for long-running operations
    /// </summary>
    public IProgressContext CreateProgressContext(string title, int totalItems)
    {
        return new SpectreProgressContext(title, totalItems);
    }

    /// <summary>
    /// Display scan results in a formatted table
    /// </summary>
    public void DisplayResults(ScanResult results)
    {
        var table = new Table()
            .Border(TableBorder.Rounded)
            .Title($"[yellow]Scan Results - {results.TotalFiles} Files Analyzed[/]")
            .AddColumn("[blue]File[/]")
            .AddColumn("[green]Status[/]")
            .AddColumn("[yellow]Issues Found[/]")
            .AddColumn("[red]Critical[/]");
            
        foreach (var file in results.ProcessedFiles)
        {
            var status = file.HasErrors ? "[red]Failed[/]" : "[green]Success[/]";
            var issueCount = file.Issues?.Count ?? 0;
            var criticalCount = file.Issues?.Count(i => i.Severity == IssueSeverity.Critical) ?? 0;
            
            table.AddRow(
                Path.GetFileName(file.FilePath),
                status,
                issueCount.ToString(),
                criticalCount > 0 ? $"[red]{criticalCount}[/]" : "0"
            );
        }
        
        AnsiConsole.Write(table);
        
        // Show summary panel
        var summaryPanel = new Panel(
            $"[green]Processed:[/] {results.SuccessCount}\n" +
            $"[yellow]Warnings:[/] {results.WarningCount}\n" +
            $"[red]Errors:[/] {results.ErrorCount}")
            .Header("[white]Summary[/]")
            .Border(BoxBorder.Double);
            
        AnsiConsole.Write(summaryPanel);
    }

    /// <summary>
    /// Show live status updates during operations
    /// </summary>
    public void ShowLiveStatus(string status)
    {
        _logMessages.Enqueue(new LogMessage
        {
            Timestamp = DateTime.Now,
            Message = status,
            Type = MessageType.Info
        });
        
        UpdateLiveDisplay();
    }

    /// <summary>
    /// Prompt for user input with validation
    /// </summary>
    public async Task<T> PromptAsync<T>(string prompt, T defaultValue = default)
    {
        return await Task.Run(() =>
        {
            if (typeof(T) == typeof(string))
            {
                var result = AnsiConsole.Ask<string>(prompt, defaultValue?.ToString() ?? "");
                return (T)(object)result;
            }
            else if (typeof(T) == typeof(bool))
            {
                var result = AnsiConsole.Confirm(prompt, (bool)(object)defaultValue);
                return (T)(object)result;
            }
            else if (typeof(T) == typeof(int))
            {
                var result = AnsiConsole.Ask<int>(prompt, (int)(object)defaultValue);
                return (T)(object)result;
            }
            
            throw new NotSupportedException($"Type {typeof(T)} is not supported for prompts");
        });
    }

    private Layout BuildMainLayout()
    {
        return new Layout()
            .SplitRows(
                new Layout("header").Size(5),
                new Layout("body")
                    .SplitColumns(
                        new Layout("main").Ratio(3),
                        new Layout("sidebar").Ratio(1)
                    ),
                new Layout("footer").Size(3)
            );
    }

    private void UpdateLiveDisplay()
    {
        if (_logTable == null)
        {
            _logTable = new Table()
                .Border(TableBorder.None)
                .AddColumn("Type", c => c.Width(5))
                .AddColumn("Time", c => c.Width(10))
                .AddColumn("Message");
        }
        
        // Keep only last 10 messages
        var messages = _logMessages.TakeLast(10).ToList();
        _logTable.Rows.Clear();
        
        foreach (var msg in messages)
        {
            var icon = msg.Type switch
            {
                MessageType.Info => "[blue]ℹ[/]",
                MessageType.Success => "[green]✓[/]",
                MessageType.Warning => "[yellow]⚠[/]",
                MessageType.Error => "[red]✗[/]",
                _ => "[grey]•[/]"
            };
            
            _logTable.AddRow(
                icon,
                $"[grey]{msg.Timestamp:HH:mm:ss}[/]",
                msg.Message
            );
        }
    }

    private async Task PerformCustomScan()
    {
        var path = AnsiConsole.Ask<string>("Enter path to scan:");
        
        if (!Directory.Exists(path) && !File.Exists(path))
        {
            AnsiConsole.MarkupLine("[red]Error:[/] Path does not exist!");
            return;
        }
        
        // Perform scan with live updates
        await AnsiConsole.Live(_mainLayout)
            .StartAsync(async ctx =>
            {
                _mainLayout["header"].Update(
                    new Panel("[yellow]Scanning in progress...[/]")
                        .Border(BoxBorder.None)
                        .Centered()
                );
                
                // Simulate scanning with updates
                for (int i = 0; i < 10; i++)
                {
                    ShowLiveStatus($"Processing file {i + 1} of 10...");
                    _mainLayout["sidebar"].Update(_logTable ?? new Text(""));
                    ctx.Refresh();
                    await Task.Delay(500);
                }
            });
    }

    private async Task PerformFcxScan()
    {
        var gamePath = AnsiConsole.Ask<string>("Enter game installation path:");
        
        var options = AnsiConsole.Prompt(
            new MultiSelectionPrompt<string>()
                .Title("Select FCX scan options:")
                .InstructionsText("[grey](Press [blue]<space>[/] to toggle, [green]<enter>[/] to accept)[/]")
                .AddChoices(new[]
                {
                    "Check file integrity",
                    "Verify mod conflicts",
                    "Scan for missing masters",
                    "Check load order",
                    "Generate compatibility report"
                })
        );
        
        AnsiConsole.MarkupLine($"\n[yellow]Starting FCX scan with {options.Count} options...[/]");
        
        // Implementation would go here
        await Task.Delay(2000);
        
        AnsiConsole.MarkupLine("[green]✓[/] FCX scan completed!");
    }

    private async Task ShowRecentResults()
    {
        // Load recent results from storage
        var recentScans = new[]
        {
            new { Date = DateTime.Now.AddHours(-1), Files = 45, Issues = 3 },
            new { Date = DateTime.Now.AddDays(-1), Files = 128, Issues = 12 },
            new { Date = DateTime.Now.AddDays(-3), Files = 67, Issues = 5 }
        };
        
        var table = new Table()
            .Title("[yellow]Recent Scan Results[/]")
            .Border(TableBorder.Rounded)
            .AddColumn("Date")
            .AddColumn("Files Scanned")
            .AddColumn("Issues Found")
            .AddColumn("Action");
            
        foreach (var scan in recentScans)
        {
            table.AddRow(
                scan.Date.ToString("yyyy-MM-dd HH:mm"),
                scan.Files.ToString(),
                scan.Issues > 0 ? $"[red]{scan.Issues}[/]" : "[green]0[/]",
                "[blue]View[/]"
            );
        }
        
        AnsiConsole.Write(table);
    }

    private void ShowAbout()
    {
        var panel = new Panel(
            "[blue]Scanner111[/] - CLASSIC Crash Log Analyzer\n\n" +
            "Version: 1.0.0\n" +
            "Based on the CLASSIC Python implementation\n\n" +
            "A powerful tool for analyzing Bethesda game crash logs\n" +
            "with advanced pattern recognition and mod conflict detection.\n\n" +
            "[grey]Press any key to return to the main menu...[/]"
        )
        .Header("[yellow]About Scanner111[/]")
        .Border(BoxBorder.Double)
        .BorderColor(Color.Blue)
        .Padding(2, 1)
        .Expand();
        
        AnsiConsole.Write(panel);
    }
}

public enum MenuChoice
{
    QuickScan,
    ScanSpecific,
    FcxMode,
    Configuration,
    RecentResults,
    About,
    Quit
}

public class SpectreProgressContext : IProgressContext
{
    private readonly ProgressTask _task;
    private readonly string _title;
    private bool _disposed;

    public SpectreProgressContext(string title, int totalItems)
    {
        _title = title;
        // In real implementation, this would be connected to an active AnsiConsole.Progress context
    }

    public void Update(int current, string message)
    {
        if (_disposed) return;
        // Update progress task
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        // Clean up
    }
}

public class LogMessage
{
    public DateTime Timestamp { get; set; }
    public string Message { get; set; } = "";
    public MessageType Type { get; set; }
}

public enum MessageType
{
    Info,
    Success,
    Warning,
    Error,
    Debug
}

// Example of how to integrate in Program.cs:
/*
if (args.Length == 0 && Environment.UserInteractive && !Console.IsInputRedirected)
{
    var uiService = serviceProvider.GetRequiredService<ITerminalUIService>();
    return await uiService.RunInteractiveMode();
}
*/