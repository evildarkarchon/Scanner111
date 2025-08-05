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
        while (true)
        {
            ShowInteractiveMenu();
            var choice = AnsiConsole.Prompt(
                new SelectionPrompt<string>()
                    .Title("[bold yellow]Select an option:[/]")
                    .PageSize(10)
                    .AddChoices(new[]
                    {
                        "Quick Scan (Current Directory)",
                        "Scan Specific File/Directory",
                        "FCX Mode - File Integrity Check",
                        "Configuration Settings",
                        "View Recent Scan Results",
                        "About Scanner111",
                        "Quit"
                    }));

            switch (choice)
            {
                case "Quick Scan (Current Directory)":
                    await RunQuickScan();
                    break;
                case "Scan Specific File/Directory":
                    await RunCustomScan();
                    break;
                case "FCX Mode - File Integrity Check":
                    await RunFcxMode();
                    break;
                case "Configuration Settings":
                    await ShowConfiguration();
                    break;
                case "View Recent Scan Results":
                    await ShowRecentResults();
                    break;
                case "About Scanner111":
                    ShowAbout();
                    break;
                case "Quit":
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
        
        var panel = new Panel(
            new FigletText("Scanner111")
                .Centered()
                .Color(Color.Blue))
            .Header("[bold yellow]Crash Log Analyzer[/]")
            .BorderColor(Color.Blue)
            .Expand();

        AnsiConsole.Write(panel);
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
        AnsiConsole.MarkupLine($"[dim]{DateTime.Now:HH:mm:ss}[/] {status}");
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
}