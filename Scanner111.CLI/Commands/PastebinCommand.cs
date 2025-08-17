using Scanner111.CLI.Models;
using Scanner111.CLI.Services;
using Scanner111.Core.Infrastructure;
using Scanner111.Core.Services;

namespace Scanner111.CLI.Commands;

public class PastebinCommand : ICommand<PastebinOptions>
{
    private readonly IAudioNotificationService? _audioService;
    private readonly IFileScanService? _fileScanService;
    private readonly IMessageHandler _messageHandler;
    private readonly IPastebinService _pastebinService;
    private readonly IScanResultProcessor? _scanResultProcessor;
    private readonly ICliSettingsService? _settingsService;
    private readonly IStatisticsService? _statisticsService;

    public PastebinCommand(
        IPastebinService pastebinService,
        IMessageHandler messageHandler,
        ICliSettingsService? settingsService = null,
        IFileScanService? fileScanService = null,
        IScanResultProcessor? scanResultProcessor = null,
        IStatisticsService? statisticsService = null,
        IAudioNotificationService? audioService = null)
    {
        _pastebinService = Guard.NotNull(pastebinService, nameof(pastebinService));
        _messageHandler = Guard.NotNull(messageHandler, nameof(messageHandler));
        _settingsService = settingsService;
        _fileScanService = fileScanService;
        _scanResultProcessor = scanResultProcessor;
        _statisticsService = statisticsService;
        _audioService = audioService;
    }

    public async Task<int> ExecuteAsync(PastebinOptions options)
    {
        return await ExecuteAsync(options, CancellationToken.None);
    }

    public async Task<int> ExecuteAsync(PastebinOptions options, CancellationToken cancellationToken)
    {
        var stopwatch = Stopwatch.StartNew();

        try
        {
            // Configure colors
            if (options.NoColors)
                AnsiConsole.Profile.Capabilities.ColorSystem = ColorSystem.NoColors;

            // Gather all URLs/IDs to fetch
            var urlsToFetch = await GatherUrlsAsync(options);
            if (!urlsToFetch.Any())
            {
                AnsiConsole.MarkupLine("[red]Error:[/] No Pastebin URLs or IDs provided");
                AnsiConsole.MarkupLine("[yellow]Tip:[/] Use -u for single URL, -m for multiple, or -f for file input");
                AnsiConsole.MarkupLine("\n[dim]Examples:[/]");
                AnsiConsole.MarkupLine("  scanner111 pastebin -u AbCd1234");
                AnsiConsole.MarkupLine("  scanner111 pastebin -u https://pastebin.com/AbCd1234");
                AnsiConsole.MarkupLine("  scanner111 pastebin -m AbCd1234,XyZ5678 --scan");
                return 1;
            }

            AnsiConsole.MarkupLine($"[cyan]Fetching {urlsToFetch.Count} log(s) from Pastebin...[/]");

            // Set custom output directory if specified
            var outputDir = options.OutputDirectory ?? "Crash Logs/Pastebin";
            if (!string.IsNullOrWhiteSpace(options.OutputDirectory))
            {
                Directory.CreateDirectory(outputDir);
                AnsiConsole.MarkupLine($"[dim]Output directory:[/] {outputDir}");
            }

            // Fetch logs from Pastebin
            var fetchedFiles = new List<string>();
            var failedUrls = new List<string>();

            if (urlsToFetch.Count == 1)
            {
                // Single file - simple output
                var url = urlsToFetch[0];
                AnsiConsole.MarkupLine($"[cyan]Fetching:[/] {url}");

                var filePath = await _pastebinService.FetchAndSaveAsync(url, cancellationToken)
                    .ConfigureAwait(false);

                if (!string.IsNullOrEmpty(filePath))
                {
                    fetchedFiles.Add(filePath);
                    AnsiConsole.MarkupLine($"[green]✓ Success:[/] Saved to {Path.GetFileName(filePath)}");
                }
                else
                {
                    failedUrls.Add(url);
                    AnsiConsole.MarkupLine($"[red]✗ Failed:[/] Could not fetch {url}");
                }
            }
            else
            {
                // Multiple files - progress bar
                await AnsiConsole.Progress()
                    .Columns(
                        new TaskDescriptionColumn(),
                        new ProgressBarColumn(),
                        new PercentageColumn(),
                        new RemainingTimeColumn(),
                        new SpinnerColumn())
                    .StartAsync(async ctx =>
                    {
                        var task = ctx.AddTask("[green]Fetching from Pastebin[/]", maxValue: urlsToFetch.Count);

                        // Use parallel downloads with limit
                        var parallelLimit = Math.Min(options.ParallelDownloads, 10);
                        parallelLimit = Math.Max(1, parallelLimit);

                        var semaphore = new SemaphoreSlim(parallelLimit, parallelLimit);
                        var tasks = urlsToFetch.Select(async url =>
                        {
                            await semaphore.WaitAsync(cancellationToken);
                            try
                            {
                                var filePath = await _pastebinService.FetchAndSaveAsync(url, cancellationToken)
                                    .ConfigureAwait(false);

                                if (!string.IsNullOrEmpty(filePath))
                                    lock (fetchedFiles)
                                    {
                                        fetchedFiles.Add(filePath);
                                    }
                                else
                                    lock (failedUrls)
                                    {
                                        failedUrls.Add(url);
                                    }

                                task.Increment(1);
                            }
                            finally
                            {
                                semaphore.Release();
                            }
                        });

                        await Task.WhenAll(tasks);
                    });
            }

            // Display summary
            AnsiConsole.WriteLine();
            if (fetchedFiles.Any())
            {
                AnsiConsole.MarkupLine($"[green]✓ Successfully fetched {fetchedFiles.Count} file(s)[/]");

                if (options.Verbose)
                {
                    AnsiConsole.MarkupLine("\n[cyan]Downloaded files:[/]");
                    foreach (var file in fetchedFiles) AnsiConsole.MarkupLine($"  • {Path.GetFileName(file)}");
                }
            }

            if (failedUrls.Any())
            {
                AnsiConsole.MarkupLine($"[red]✗ Failed to fetch {failedUrls.Count} file(s)[/]");

                if (options.Verbose)
                {
                    AnsiConsole.MarkupLine("\n[red]Failed URLs:[/]");
                    foreach (var url in failedUrls) AnsiConsole.MarkupLine($"  • {url}");
                }
            }

            if (!fetchedFiles.Any())
            {
                AnsiConsole.MarkupLine("[red]Error:[/] No files were successfully fetched");
                return 1;
            }

            // Optional: Scan fetched files if requested
            if (options.ScanAfterDownload)
            {
                if (_fileScanService == null || _scanResultProcessor == null || _settingsService == null)
                {
                    AnsiConsole.MarkupLine("\n[yellow]Warning:[/] Scanning is not available (services not configured)");
                }
                else
                {
                    AnsiConsole.MarkupLine("\n[cyan]───────────────────────────────────────────────────────[/]");
                    AnsiConsole.MarkupLine("[cyan]Starting scan of fetched logs...[/]");

                    await ScanFetchedFilesAsync(fetchedFiles, options, cancellationToken);
                }
            }
            else
            {
                AnsiConsole.MarkupLine($"\n[dim]Files saved to:[/] {outputDir}");
                AnsiConsole.MarkupLine("[dim]Tip: Use --scan flag to automatically analyze fetched logs[/]");
            }

            stopwatch.Stop();
            AnsiConsole.MarkupLine($"\n[dim]Total time: {stopwatch.Elapsed:mm\\:ss\\.fff}[/]");

            // Play completion sound if available
            if (_audioService != null)
                await _audioService.PlayScanCompleteAsync();

            return failedUrls.Any() ? 2 : 0; // Return 2 for partial success
        }
        catch (Exception ex)
        {
            AnsiConsole.WriteException(ex);

            // Play error sound for operation failure
            if (_audioService != null && _audioService.IsEnabled)
                try
                {
                    await _audioService.PlayErrorFoundAsync();
                }
                catch
                {
                    // Ignore audio playback errors
                }

            return 1;
        }
    }

    private async Task<List<string>> GatherUrlsAsync(PastebinOptions options)
    {
        var urls = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        // Single URL
        if (!string.IsNullOrWhiteSpace(options.UrlOrId)) urls.Add(options.UrlOrId.Trim());

        // Multiple URLs
        if (options.Multiple?.Any() == true)
            foreach (var url in options.Multiple)
                if (!string.IsNullOrWhiteSpace(url))
                    urls.Add(url.Trim());

        // File input
        if (!string.IsNullOrWhiteSpace(options.InputFile) && File.Exists(options.InputFile))
            try
            {
                var lines = await File.ReadAllLinesAsync(options.InputFile);
                foreach (var line in lines)
                {
                    var trimmed = line.Trim();
                    // Skip empty lines and comments
                    if (!string.IsNullOrWhiteSpace(trimmed) && !trimmed.StartsWith("#") && !trimmed.StartsWith("//"))
                        urls.Add(trimmed);
                }

                AnsiConsole.MarkupLine($"[dim]Loaded {urls.Count} URL(s) from file: {options.InputFile}[/]");
            }
            catch (Exception ex)
            {
                AnsiConsole.MarkupLine($"[yellow]Warning:[/] Could not read input file: {ex.Message}");
            }
        else if (!string.IsNullOrWhiteSpace(options.InputFile))
            AnsiConsole.MarkupLine($"[yellow]Warning:[/] Input file not found: {options.InputFile}");

        return urls.ToList();
    }

    private async Task ScanFetchedFilesAsync(
        List<string> fetchedFiles,
        PastebinOptions options,
        CancellationToken cancellationToken)
    {
        // For now, we'll just inform the user how to scan the files manually
        // Full integration requires refactoring the scan pipeline to accept individual files

        AnsiConsole.MarkupLine("\n[cyan]To scan the fetched files, you can use:[/]");
        foreach (var file in fetchedFiles.Take(3)) // Show first 3 as examples
        {
            var fileName = Path.GetFileName(file);
            AnsiConsole.MarkupLine($"  scanner111 scan -l \"{file}\"");
        }

        if (fetchedFiles.Count > 3) AnsiConsole.MarkupLine($"  [dim]... and {fetchedFiles.Count - 3} more files[/]");

        AnsiConsole.MarkupLine("\n[dim]Or scan all files in the directory:[/]");
        AnsiConsole.MarkupLine($"  scanner111 scan -d \"{options.OutputDirectory ?? "Crash Logs/Pastebin"}\"");

        await Task.CompletedTask; // Keep async signature
    }
}