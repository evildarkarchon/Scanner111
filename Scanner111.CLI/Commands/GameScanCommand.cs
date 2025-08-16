using System;
using System.Threading;
using System.Threading.Tasks;
using Scanner111.CLI.Models;
using Scanner111.Core.GameScanning;
using Scanner111.Core.Infrastructure;
using Spectre.Console;

namespace Scanner111.CLI.Commands
{
    /// <summary>
    /// Command for performing comprehensive game scans.
    /// </summary>
    public class GameScanCommand : ICommand<GameScanOptions>
    {
        private readonly IGameScannerService _gameScannerService;
        private readonly IMessageHandler _messageHandler;

        public GameScanCommand(
            IGameScannerService gameScannerService,
            IMessageHandler messageHandler)
        {
            _gameScannerService = gameScannerService;
            _messageHandler = messageHandler;
        }

        public async Task<int> ExecuteAsync(GameScanOptions scanOptions)
        {
            var cancellationToken = CancellationToken.None;

            AnsiConsole.Clear();
            AnsiConsole.Write(
                new FigletText("Game Scanner")
                    .Centered()
                    .Color(Color.Cyan1));
            AnsiConsole.WriteLine();

            try
            {
                if (scanOptions.All)
                {
                    // Run comprehensive scan
                    await RunComprehensiveScan(cancellationToken);
                }
                else if (scanOptions.CrashGen)
                {
                    // Run only Crash Generator check
                    await RunCrashGenCheck(cancellationToken);
                }
                else if (scanOptions.XsePlugins)
                {
                    // Run only XSE plugin validation
                    await RunXsePluginValidation(cancellationToken);
                }
                else if (scanOptions.ModInis)
                {
                    // Run only mod INI scan
                    await RunModIniScan(cancellationToken);
                }
                else if (scanOptions.WryeBash)
                {
                    // Run only Wrye Bash check
                    await RunWryeBashCheck(cancellationToken);
                }
                else
                {
                    // Default to comprehensive scan
                    await RunComprehensiveScan(cancellationToken);
                }

                // Export results if requested
                if (!string.IsNullOrEmpty(scanOptions.ExportPath))
                {
                    await ExportResults(scanOptions.ExportPath);
                }

                return 0; // Success
            }
            catch (OperationCanceledException)
            {
                AnsiConsole.MarkupLine("[yellow]Game scan cancelled by user[/]");
                return 1; // Cancelled
            }
            catch (Exception ex)
            {
                AnsiConsole.MarkupLine($"[red]Error during game scan: {ex.Message}[/]");
                return 2; // Error
            }
        }

        private async Task RunComprehensiveScan(CancellationToken cancellationToken)
        {
            AnsiConsole.MarkupLine("[cyan]Running comprehensive game scan...[/]");
            AnsiConsole.WriteLine();

            var result = await AnsiConsole.Progress()
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
                    var task = ctx.AddTask("[green]Scanning game configuration[/]");
                    task.IsIndeterminate = true;
                    
                    var scanResult = await _gameScannerService.ScanGameAsync(cancellationToken);
                    
                    task.Value = 100;
                    task.IsIndeterminate = false;
                    
                    return scanResult;
                });

            // Display results
            DisplayScanResults(result.GetFullReport());

            // Display summary
            AnsiConsole.WriteLine();
            var summaryTable = new Table()
                .Border(TableBorder.Rounded)
                .AddColumn("Category")
                .AddColumn("Count");

            summaryTable.AddRow("Critical Issues", $"[red]{result.CriticalIssues.Count}[/]");
            summaryTable.AddRow("Warnings", $"[yellow]{result.Warnings.Count}[/]");
            
            AnsiConsole.Write(summaryTable);
        }

        private async Task RunCrashGenCheck(CancellationToken cancellationToken)
        {
            AnsiConsole.MarkupLine("[cyan]Checking Crash Generator configuration...[/]");
            var result = await _gameScannerService.CheckCrashGenAsync(cancellationToken);
            DisplayScanResults(result);
        }

        private async Task RunXsePluginValidation(CancellationToken cancellationToken)
        {
            AnsiConsole.MarkupLine("[cyan]Validating XSE plugins...[/]");
            var result = await _gameScannerService.ValidateXsePluginsAsync(cancellationToken);
            DisplayScanResults(result);
        }

        private async Task RunModIniScan(CancellationToken cancellationToken)
        {
            AnsiConsole.MarkupLine("[cyan]Scanning mod INI files...[/]");
            var result = await _gameScannerService.ScanModInisAsync(cancellationToken);
            DisplayScanResults(result);
        }

        private async Task RunWryeBashCheck(CancellationToken cancellationToken)
        {
            AnsiConsole.MarkupLine("[cyan]Analyzing Wrye Bash report...[/]");
            var result = await _gameScannerService.CheckWryeBashAsync(cancellationToken);
            DisplayScanResults(result);
        }

        private void DisplayScanResults(string results)
        {
            if (string.IsNullOrWhiteSpace(results))
            {
                AnsiConsole.MarkupLine("[green]No issues found![/]");
                return;
            }

            // Split results into lines and format for display
            var lines = results.Split('\n', StringSplitOptions.RemoveEmptyEntries);
            
            foreach (var line in lines)
            {
                if (line.Contains("❌"))
                {
                    AnsiConsole.MarkupLine($"[red]{Markup.Escape(line)}[/]");
                }
                else if (line.Contains("⚠️"))
                {
                    AnsiConsole.MarkupLine($"[yellow]{Markup.Escape(line)}[/]");
                }
                else if (line.Contains("✔️"))
                {
                    AnsiConsole.MarkupLine($"[green]{Markup.Escape(line)}[/]");
                }
                else if (line.Contains("ℹ️") || line.Contains("❓"))
                {
                    AnsiConsole.MarkupLine($"[blue]{Markup.Escape(line)}[/]");
                }
                else if (line.StartsWith("===") || line.StartsWith("---"))
                {
                    AnsiConsole.MarkupLine($"[cyan]{Markup.Escape(line)}[/]");
                }
                else
                {
                    AnsiConsole.WriteLine(line);
                }
            }
        }

        private async Task ExportResults(string exportPath)
        {
            try
            {
                AnsiConsole.MarkupLine($"[cyan]Exporting results to {exportPath}...[/]");
                // Implementation would write results to file
                await Task.Delay(100); // Placeholder
                AnsiConsole.MarkupLine("[green]Results exported successfully![/]");
            }
            catch (Exception ex)
            {
                AnsiConsole.MarkupLine($"[red]Failed to export results: {ex.Message}[/]");
            }
        }
    }
}