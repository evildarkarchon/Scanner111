using Spectre.Console;
using Scanner111.Core.Models;
using Scanner111.Core.Infrastructure;
using Scanner111.CLI.Commands;
using Scanner111.CLI.Models;
using Microsoft.Extensions.DependencyInjection;

namespace Scanner111.CLI.Services;

public class SpectreTerminalUIService : ITerminalUIService
{
    private readonly IServiceProvider _serviceProvider;
    private readonly IApplicationSettingsService _settingsService;

    public SpectreTerminalUIService(
        IServiceProvider serviceProvider,
        IApplicationSettingsService settingsService)
    {
        _serviceProvider = serviceProvider;
        _settingsService = settingsService;
    }

    public async Task<int> RunInteractiveMode()
    {
        // Check if terminal is interactive
        if (!AnsiConsole.Profile.Capabilities.Interactive)
        {
            AnsiConsole.MarkupLine("[red]Error:[/] Interactive mode requires an interactive terminal.");
            AnsiConsole.MarkupLine("[yellow]Tip:[/] Run this application directly in a terminal window, not through IDE output.");
            AnsiConsole.WriteLine();
            AnsiConsole.MarkupLine("Available commands:");
            AnsiConsole.MarkupLine("  [blue]scan[/]         - Scan crash logs");
            AnsiConsole.MarkupLine("  [blue]fcx[/]          - Run file integrity checks");
            AnsiConsole.MarkupLine("  [blue]config[/]       - View/edit configuration");
            AnsiConsole.MarkupLine("  [blue]about[/]        - About Scanner111");
            AnsiConsole.MarkupLine("  [blue]interactive[/]  - Launch interactive mode (requires terminal)");
            return 1;
        }
        
        while (true)
        {
            ShowInteractiveMenu();
            var choice = AnsiConsole.Prompt(
                new SelectionPrompt<string>()
                    .Title("[bold yellow]Select an option:[/]")
                    .PageSize(10)
                    .HighlightStyle(new Style(Color.Cyan1, decoration: Decoration.Bold))
                    .AddChoices(new[]
                    {
                        "[1] Quick Scan (Current Directory)",
                        "[2] Scan Specific File/Directory",
                        "[3] FCX Mode - File Integrity Check",
                        "[4] Configuration Settings",
                        "[5] View Recent Scan Results",
                        "[6] Watch Mode - Monitor for New Logs",
                        "[7] About Scanner111",
                        "[Q] Quit"
                    }));

            switch (choice)
            {
                case "[1] Quick Scan (Current Directory)":
                    await RunQuickScan();
                    break;
                case "[2] Scan Specific File/Directory":
                    await RunCustomScan();
                    break;
                case "[3] FCX Mode - File Integrity Check":
                    await RunFcxMode();
                    break;
                case "[4] Configuration Settings":
                    await ShowConfiguration();
                    break;
                case "[5] View Recent Scan Results":
                    await ShowRecentResults();
                    break;
                case "[6] Watch Mode - Monitor for New Logs":
                    await RunWatchMode();
                    break;
                case "[7] About Scanner111":
                    ShowAbout();
                    break;
                case "[Q] Quit":
                    return 0;
            }

            AnsiConsole.WriteLine();
            AnsiConsole.WriteLine("Press any key to continue...");
            Console.ReadKey();
            AnsiConsole.Clear();
        }
    }

    public void ShowInteractiveMenu()
    {
        AnsiConsole.Clear();
        
        // Create the main header panel
        var headerContent = new Rows(
            new FigletText("Scanner111")
                .Centered()
                .Color(Color.Cyan1),
            new Text("Crash Log Analyzer for Bethesda Games")
                .Centered()
        );
        
        var headerPanel = new Panel(headerContent)
            .Header("[bold cyan]═══ Terminal UI Mode ═══[/]", Justify.Center)
            .BorderColor(Color.Cyan1)
            .Border(BoxBorder.Rounded)
            .Expand();

        AnsiConsole.Write(headerPanel);
        AnsiConsole.WriteLine();
        
        // Display system information
        var infoTable = new Table()
            .Border(TableBorder.None)
            .AddColumn("Property", c => c.NoWrap())
            .AddColumn("Value")
            .HideHeaders();
            
        infoTable.AddRow("[dim]Current Directory:[/]", $"[blue]{Directory.GetCurrentDirectory()}[/]");
        infoTable.AddRow("[dim]Time:[/]", $"[green]{DateTime.Now:HH:mm:ss}[/]");
        
        AnsiConsole.Write(infoTable);
        AnsiConsole.WriteLine();
    }

    public IProgressContext CreateProgressContext(string title, int totalItems)
    {
        // Return the Core infrastructure progress context
        return _serviceProvider.GetRequiredService<IMessageHandler>().CreateProgressContext(title, totalItems);
    }

    public void DisplayResults(ScanResult results)
    {
        var table = new Table()
            .Border(TableBorder.Rounded)
            .BorderColor(Color.Blue)
            .AddColumn("[bold]Category[/]")
            .AddColumn("[bold]Status[/]")
            .AddColumn("[bold]Details[/]");

        // Status
        table.AddRow(
            "Scan Status",
            results.Status switch
            {
                ScanStatus.Completed => "[green]Completed[/]",
                ScanStatus.CompletedWithErrors => "[yellow]Completed with errors[/]",
                ScanStatus.Failed => "[red]Failed[/]",
                _ => results.Status.ToString()
            },
            results.ProcessingTime.ToString(@"mm\:ss"));

        // Errors
        if (results.ErrorMessages.Any())
        {
            table.AddRow(
                "[red]Errors[/]",
                $"[red]{results.ErrorMessages.Count}[/]",
                string.Join(", ", results.ErrorMessages.Take(2)));
        }

        // Analysis Results
        var findingsCount = results.AnalysisResults.Count(r => r.HasFindings);
        if (findingsCount > 0)
        {
            table.AddRow(
                "[yellow]Findings[/]",
                $"[yellow]{findingsCount}[/]",
                string.Join(", ", results.AnalysisResults.Where(r => r.HasFindings).Take(3).Select(r => r.AnalyzerName)));
        }

        // Statistics
        table.AddRow(
            "Statistics",
            "[blue]Info[/]",
            $"Scanned: {results.Statistics.Scanned}, Failed: {results.Statistics.Failed}");

        AnsiConsole.Write(table);
    }

    public void ShowLiveStatus(string status)
    {
        AnsiConsole.MarkupLine($"[dim]{DateTime.Now:HH:mm:ss}[/] {Markup.Escape(status)}");
    }

    public async Task<T> PromptAsync<T>(string prompt, T? defaultValue = default) where T : notnull
    {
        var textPrompt = new TextPrompt<T>(prompt);
        if (defaultValue != null)
        {
            textPrompt.DefaultValue(defaultValue);
        }
        return await Task.FromResult(AnsiConsole.Prompt(textPrompt));
    }

    private async Task RunQuickScan()
    {
        AnsiConsole.MarkupLine("[yellow]Starting quick scan of current directory...[/]");
        
        var scanCommand = _serviceProvider.GetRequiredService<ICommand<Models.ScanOptions>>();
        var options = new Models.ScanOptions
        {
            ScanDir = Directory.GetCurrentDirectory()
        };
        
        await scanCommand.ExecuteAsync(options);
    }

    private async Task RunCustomScan()
    {
        var path = AnsiConsole.Ask<string>("Enter path to scan:");
        
        if (!File.Exists(path) && !Directory.Exists(path))
        {
            AnsiConsole.MarkupLine("[red]Path does not exist![/]");
            return;
        }

        var options = AnsiConsole.Prompt(
            new MultiSelectionPrompt<string>()
                .Title("Select scan options:")
                .NotRequired()
                .AddChoices(new[]
                {
                    "Simplify Logs",
                    "Move Unsolved Logs",
                    "FCX Mode",
                    "Verbose Output",
                    "Show FormID Values"
                }));

        var scanCommand = _serviceProvider.GetRequiredService<ICommand<Models.ScanOptions>>();
        var scanOptions = new Models.ScanOptions
        {
            LogFile = File.Exists(path) ? path : null,
            ScanDir = Directory.Exists(path) ? path : null,
            SimplifyLogs = options.Contains("Simplify Logs"),
            MoveUnsolved = options.Contains("Move Unsolved Logs"),
            FcxMode = options.Contains("FCX Mode"),
            Verbose = options.Contains("Verbose Output"),
            ShowFidValues = options.Contains("Show FormID Values")
        };

        await scanCommand.ExecuteAsync(scanOptions);
    }

    private async Task RunFcxMode()
    {
        AnsiConsole.MarkupLine("[yellow]Starting FCX (File Integrity Check) mode...[/]");
        
        var fcxCommand = _serviceProvider.GetRequiredService<ICommand<FcxOptions>>();
        var options = new FcxOptions();
        
        await fcxCommand.ExecuteAsync(options);
    }

    private async Task ShowConfiguration()
    {
        var configCommand = _serviceProvider.GetRequiredService<ICommand<ConfigOptions>>();
        await configCommand.ExecuteAsync(new ConfigOptions());
    }

    private async Task ShowRecentResults()
    {
        AnsiConsole.MarkupLine("[yellow]Recent scan results feature coming soon![/]");
        await Task.CompletedTask;
    }

    private void ShowAbout()
    {
        var aboutCommand = _serviceProvider.GetRequiredService<ICommand<AboutOptions>>();
        aboutCommand.ExecuteAsync(new AboutOptions()).Wait();
    }
    
    private async Task RunWatchMode()
    {
        AnsiConsole.MarkupLine("[yellow]Watch Mode - Monitoring for new crash logs[/]");
        AnsiConsole.WriteLine();
        
        var watchPath = AnsiConsole.Ask<string>("Enter directory to watch:", Directory.GetCurrentDirectory());
        
        if (!Directory.Exists(watchPath))
        {
            AnsiConsole.MarkupLine("[red]Directory does not exist![/]");
            return;
        }
        
        var cts = new CancellationTokenSource();
        
        AnsiConsole.MarkupLine($"[green]Watching:[/] {watchPath}");
        AnsiConsole.MarkupLine("[dim]Press 'Q' to stop watching...[/]");
        AnsiConsole.WriteLine();
        
        using var watcher = new FileSystemWatcher(watchPath)
        {
            Filter = "*.log",
            NotifyFilter = NotifyFilters.FileName | NotifyFilters.CreationTime | NotifyFilters.LastWrite
        };
        
        watcher.Created += async (sender, e) =>
        {
            AnsiConsole.MarkupLine($"[yellow]New log detected:[/] {e.Name}");
            
            // Wait a moment for file to be fully written
            await Task.Delay(1000);
            
            // Auto-scan the new log
            var scanCommand = _serviceProvider.GetRequiredService<ICommand<Models.ScanOptions>>();
            var options = new Models.ScanOptions
            {
                LogFile = e.FullPath
            };
            
            AnsiConsole.MarkupLine("[cyan]Auto-scanning new crash log...[/]");
            await scanCommand.ExecuteAsync(options);
        };
        
        watcher.EnableRaisingEvents = true;
        
        // Wait for user to press Q
        await Task.Run(() =>
        {
            while (!cts.Token.IsCancellationRequested)
            {
                if (Console.KeyAvailable)
                {
                    var key = Console.ReadKey(true);
                    if (key.Key == ConsoleKey.Q)
                    {
                        cts.Cancel();
                        break;
                    }
                }
                Thread.Sleep(100);
            }
        }, cts.Token);
        
        watcher.EnableRaisingEvents = false;
        AnsiConsole.MarkupLine("[yellow]Watch mode stopped.[/]");
    }
}