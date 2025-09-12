using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Scanner111.CLI.Configuration;
using Scanner111.CLI.Models;
using Scanner111.CLI.Services;
using Scanner111.Core.Analysis;
using Scanner111.Core.Configuration;
using Scanner111.Core.Models;
using Scanner111.Core.Orchestration;
using Scanner111.Core.Reporting;
using Spectre.Console;

namespace Scanner111.CLI.UI.Screens;

/// <summary>
/// Screen for analyzing crash log files.
/// </summary>
public class AnalysisScreen : BaseScreen, IDataReceiver
{
    private readonly IAnalyzerOrchestrator _orchestrator;
    private readonly IAnalyzerRegistry _registry;
    private readonly ICliSettings _settings;
    private readonly IAsyncYamlSettingsCore _yamlCore;
    private string? _preselectedFile;
    
    /// <summary>
    /// Gets the title of the screen.
    /// </summary>
    public override string Title => "Analyze Crash Log";
    
    /// <summary>
    /// Initializes a new instance of the <see cref="AnalysisScreen"/> class.
    /// </summary>
    /// <param name="console">The Spectre.Console instance.</param>
    /// <param name="services">The service provider.</param>
    /// <param name="logger">The logger.</param>
    public AnalysisScreen(
        IAnsiConsole console,
        IServiceProvider services,
        ILogger<AnalysisScreen> logger)
        : base(console, services, logger)
    {
        _orchestrator = services.GetRequiredService<IAnalyzerOrchestrator>();
        _registry = services.GetRequiredService<IAnalyzerRegistry>();
        _settings = services.GetRequiredService<ICliSettings>();
        _yamlCore = services.GetRequiredService<IAsyncYamlSettingsCore>();
    }
    
    /// <summary>
    /// Sets pre-selected data for the screen.
    /// </summary>
    /// <param name="data">The data to set.</param>
    public void SetData(object data)
    {
        if (data is string filePath)
        {
            _preselectedFile = filePath;
        }
    }
    
    /// <summary>
    /// Displays the analysis screen and performs analysis.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The screen result with analysis results.</returns>
    public override async Task<ScreenResult> DisplayAsync(CancellationToken cancellationToken = default)
    {
        DrawHeader();
        
        try
        {
            // Step 1: Select log file
            var logFile = await SelectLogFileAsync(cancellationToken);
            if (string.IsNullOrEmpty(logFile))
            {
                return ScreenResult.Back;
            }
            
            // Step 2: Select analyzers
            var analyzers = await SelectAnalyzersAsync(cancellationToken);
            if (!analyzers.Any())
            {
                return ScreenResult.Back;
            }
            
            // Step 3: Run analysis with progress
            var results = await RunAnalysisWithProgressAsync(logFile, analyzers, cancellationToken);
            
            // Step 4: Show summary
            ShowAnalysisSummary(results);
            
            await WaitForKeyAsync("Press any key to view detailed results...", cancellationToken: cancellationToken);
            
            return new ScreenResult
            {
                NextAction = MenuAction.ViewResults,
                Data = results
            };
        }
        catch (OperationCanceledException)
        {
            ShowWarning("Analysis cancelled by user");
            await Task.Delay(1500, CancellationToken.None);
            return ScreenResult.Back;
        }
        catch (Exception ex)
        {
            ShowError($"Analysis failed: {ex.Message}");
            Logger.LogError(ex, "Analysis failed");
            await WaitForKeyAsync("Press any key to return...", cancellationToken: CancellationToken.None);
            return ScreenResult.Back;
        }
    }
    
    private async Task<string?> SelectLogFileAsync(CancellationToken cancellationToken)
    {
        if (!string.IsNullOrEmpty(_preselectedFile))
        {
            var file = _preselectedFile;
            _preselectedFile = null; // Clear after use
            ShowSuccess($"Using preselected file: {Path.GetFileName(file)}");
            return file;
        }
        
        Console.WriteLine();
        
        // Show file selection options
        var selectionType = Console.Prompt(
            new SelectionPrompt<string>()
                .Title("[green]How would you like to select the crash log file?[/]")
                .AddChoices(new[] {
                    "📝 Enter file path manually",
                    "📁 Browse from recent files",
                    "🔍 Search common locations"
                }));
        
        string? filePath = null;
        
        if (selectionType.Contains("manually"))
        {
            filePath = await PromptForFilePathAsync(cancellationToken);
        }
        else if (selectionType.Contains("recent"))
        {
            filePath = await SelectFromRecentFilesAsync(cancellationToken);
        }
        else if (selectionType.Contains("Search"))
        {
            filePath = await SearchCommonLocationsAsync(cancellationToken);
        }
        
        if (string.IsNullOrEmpty(filePath))
        {
            return null;
        }
        
        if (!File.Exists(filePath))
        {
            ShowError($"File not found: {filePath}");
            return null;
        }
        
        // Validate file format
        if (!IsValidLogFile(filePath))
        {
            ShowWarning($"File may not be a valid crash log: {Path.GetFileName(filePath)}");
            if (!Confirm("Continue anyway?"))
            {
                return null;
            }
        }
        
        ShowSuccess($"Selected file: {Path.GetFileName(filePath)}");
        return filePath;
    }
    
    private async Task<string?> PromptForFilePathAsync(CancellationToken cancellationToken)
    {
        var filePath = Console.Prompt(
            new TextPrompt<string>("[cyan]Enter the path to the crash log file:[/]")
                .PromptStyle("green")
                .ValidationErrorMessage("[red]Please enter a valid file path[/]")
                .Validate(path =>
                {
                    if (string.IsNullOrWhiteSpace(path))
                        return Spectre.Console.ValidationResult.Error("[red]File path cannot be empty[/]");
                    
                    if (path.IndexOfAny(Path.GetInvalidPathChars()) >= 0)
                        return Spectre.Console.ValidationResult.Error("[red]File path contains invalid characters[/]");
                        
                    return Spectre.Console.ValidationResult.Success();
                }));
        
        await Task.CompletedTask;
        return filePath;
    }
    
    private async Task<string?> SelectFromRecentFilesAsync(CancellationToken cancellationToken)
    {
        try
        {
            var recentSessions = await Services.GetRequiredService<ISessionManager>()
                .GetRecentSessionsAsync(10, cancellationToken);
            
            var recentFiles = recentSessions
                .Select(s => s.LogFile)
                .Where(File.Exists)
                .Distinct()
                .ToList();
            
            if (!recentFiles.Any())
            {
                ShowWarning("No recent files found");
                return null;
            }
            
            var selectedFile = Console.Prompt(
                new SelectionPrompt<string>()
                    .Title("[green]Select a recent file:[/]")
                    .PageSize(10)
                    .AddChoices(recentFiles)
                    .UseConverter(path => $"📄 {Path.GetFileName(path)} [dim]({Path.GetDirectoryName(path)})[/]"));
            
            return selectedFile;
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Failed to load recent files");
            ShowError("Failed to load recent files");
            return null;
        }
    }
    
    private async Task<string?> SearchCommonLocationsAsync(CancellationToken cancellationToken)
    {
        var commonPaths = new List<string>();
        
        // Add common log locations based on OS
        var userProfile = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        var appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
        
        commonPaths.AddRange(new[]
        {
            Path.Combine(userProfile, "Documents"),
            Path.Combine(userProfile, "Desktop"),
            Path.Combine(userProfile, "Downloads"),
            Path.Combine(appData, "Local"),
            Environment.CurrentDirectory
        });
        
        var foundFiles = new List<string>();
        
        await AnsiConsole.Status()
            .StartAsync("[yellow]Searching for crash log files...[/]", async ctx =>
            {
                foreach (var searchPath in commonPaths)
                {
                    if (Directory.Exists(searchPath))
                    {
                        ctx.Status($"[yellow]Searching {Path.GetFileName(searchPath)}...[/]");
                        
                        try
                        {
                            var files = Directory.GetFiles(searchPath, "*.log", SearchOption.TopDirectoryOnly)
                                .Concat(Directory.GetFiles(searchPath, "*.txt", SearchOption.TopDirectoryOnly))
                                .Where(IsValidLogFile)
                                .OrderByDescending(File.GetLastWriteTime)
                                .Take(5);
                            
                            foundFiles.AddRange(files);
                        }
                        catch (Exception ex)
                        {
                            Logger.LogDebug(ex, "Failed to search path: {Path}", searchPath);
                        }
                        
                        await Task.Delay(100, cancellationToken); // Small delay for UI
                    }
                }
            });
        
        if (!foundFiles.Any())
        {
            ShowWarning("No crash log files found in common locations");
            return null;
        }
        
        var selectedFile = Console.Prompt(
            new SelectionPrompt<string>()
                .Title("[green]Found crash log files:[/]")
                .PageSize(10)
                .AddChoices(foundFiles.Take(20)) // Limit to 20 results
                .UseConverter(path => $"📄 {Path.GetFileName(path)} [dim]({File.GetLastWriteTime(path):MMM dd, HH:mm})[/]"));
        
        return selectedFile;
    }
    
    private bool IsValidLogFile(string filePath)
    {
        try
        {
            var fileName = Path.GetFileName(filePath).ToLowerInvariant();
            var extension = Path.GetExtension(filePath).ToLowerInvariant();
            
            // Check for valid extensions
            if (extension != ".log" && extension != ".txt")
                return false;
            
            // Check for crash log indicators in filename
            var logIndicators = new[] { "crash", "error", "exception", "dump", "fatal" };
            if (logIndicators.Any(indicator => fileName.Contains(indicator)))
                return true;
            
            // Check file content for crash indicators (first few lines)
            var lines = File.ReadLines(filePath).Take(10).ToList();
            var crashKeywords = new[] { "exception", "error", "crash", "fatal", "stack trace" };
            
            return lines.Any(line => crashKeywords.Any(keyword => 
                line.ToLowerInvariant().Contains(keyword)));
        }
        catch
        {
            return false;
        }
    }
    
    private async Task<IEnumerable<IAnalyzer>> SelectAnalyzersAsync(CancellationToken cancellationToken)
    {
        Console.WriteLine();
        
        var availableAnalyzers = await _registry.GetAllAsync(cancellationToken);
        var analyzerInfos = availableAnalyzers.Select(a => new AnalyzerInfo(a)).ToList();
        
        // Show analyzer information table
        var infoTable = new Table()
            .Border(TableBorder.Rounded)
            .AddColumn("[bold]Analyzer[/]")
            .AddColumn("[bold]Description[/]")
            .AddColumn("[bold]Performance[/]");
        
        foreach (var info in analyzerInfos)
        {
            var description = GetAnalyzerDescription(info.Name);
            var performance = GetPerformanceIndicator(info.Name);
            
            infoTable.AddRow(
                $"{GetIcon(info.Analyzer)} {info.Name}",
                description,
                performance);
        }
        
        var infoPanel = new Panel(infoTable)
            .Header("[cyan]Available Analyzers[/]")
            .BorderStyle(new Style(Color.Blue));
            
        Console.Write(infoPanel);
        Console.WriteLine();
        
        // Quick selection options
        var presets = new[]
        {
            "🚀 Quick Analysis (Essential analyzers only)",
            "🔍 Comprehensive Analysis (All analyzers)",
            "🎯 Custom Selection (Choose specific analyzers)"
        };
        
        var analysisType = Console.Prompt(
            new SelectionPrompt<string>()
                .Title("[green]Select analysis type:[/]")
                .AddChoices(presets));
        
        if (analysisType.Contains("Quick"))
        {
            // Select essential analyzers
            var essentialNames = new[] { "PluginAnalyzer", "MemoryAnalyzer", "SettingsAnalyzer" };
            return analyzerInfos
                .Where(info => essentialNames.Contains(info.Name))
                .Select(info => info.Analyzer)
                .ToList();
        }
        else if (analysisType.Contains("Comprehensive"))
        {
            // Select all analyzers
            return analyzerInfos.Select(info => info.Analyzer).ToList();
        }
        else
        {
            // Custom selection
            var selectedInfos = Console.Prompt(
                new MultiSelectionPrompt<AnalyzerInfo>()
                    .Title("[green]Select analyzers to run:[/]")
                    .Required()
                    .PageSize(10)
                    .MoreChoicesText("[grey](Move up and down to reveal more analyzers)[/]")
                    .InstructionsText(
                        "[grey](Press [blue]<space>[/] to toggle an analyzer, " +
                        "[green]<enter>[/] to accept)[/]")
                    .AddChoices(analyzerInfos)
                    .UseConverter(info => $"{GetIcon(info.Analyzer)} {info.Name} [dim]- {GetAnalyzerDescription(info.Name)}[/]"));
            
            return selectedInfos.Select(info => info.Analyzer).ToList();
        }
    }
    
    private string GetAnalyzerDescription(string analyzerName)
    {
        return analyzerName switch
        {
            "PluginAnalyzer" => "Identifies problematic game plugins",
            "MemoryAnalyzer" => "Analyzes memory usage patterns",
            "SettingsAnalyzer" => "Checks game configuration settings",
            "PathAnalyzer" => "Validates file and directory paths",
            "FcxModeAnalyzer" => "Analyzes FCX mode compatibility",
            _ => "General crash log analysis"
        };
    }
    
    private string GetPerformanceIndicator(string analyzerName)
    {
        return analyzerName switch
        {
            "PluginAnalyzer" => "[yellow]●●○[/] Medium",
            "MemoryAnalyzer" => "[green]●○○[/] Fast",
            "SettingsAnalyzer" => "[green]●○○[/] Fast",
            "PathAnalyzer" => "[green]●○○[/] Fast",
            "FcxModeAnalyzer" => "[red]●●●[/] Slow",
            _ => "[yellow]●●○[/] Medium"
        };
    }
    
    private async Task<IEnumerable<AnalysisResult>> RunAnalysisWithProgressAsync(
        string logFile,
        IEnumerable<IAnalyzer> analyzers,
        CancellationToken cancellationToken)
    {
        var analyzerList = analyzers.ToList();
        var results = new List<AnalysisResult>();
        var liveOutput = new List<string>();
        
        // Show file information before starting
        var fileInfo = new FileInfo(logFile);
        var fileInfoPanel = new Panel(
            $"📄 [bold]{Path.GetFileName(logFile)}[/]\n" +
            $"📁 {Path.GetDirectoryName(logFile)}\n" +
            $"📊 Size: {fileInfo.Length / 1024:N0} KB\n" +
            $"📅 Modified: {fileInfo.LastWriteTime:MMM dd, yyyy HH:mm}")
            .Header("[cyan]Analyzing File[/]")
            .BorderStyle(new Style(Color.Cyan1));
        
        Console.Write(fileInfoPanel);
        Console.WriteLine();
        
        await Console.Progress()
            .AutoClear(false)
            .Columns(new ProgressColumn[]
            {
                new TaskDescriptionColumn(),
                new ProgressBarColumn(),
                new PercentageColumn(),
                new ElapsedTimeColumn(),
                new SpinnerColumn(Spinner.Known.Dots),
            })
            .StartAsync(async ctx =>
            {
                var mainTask = ctx.AddTask("[green]Starting analysis...[/]", maxValue: analyzerList.Count);
                var analyzerTasks = new Dictionary<IAnalyzer, ProgressTask>();
                
                // Create individual tasks for each analyzer
                foreach (var analyzer in analyzerList)
                {
                    var task = ctx.AddTask($"[dim]{GetIcon(analyzer)} {analyzer.Name}[/]", maxValue: 100);
                    analyzerTasks[analyzer] = task;
                }
                
                // Create analysis context
                var context = new AnalysisContext(
                    logFile,
                    _yamlCore,
                    AnalysisType.CrashLog);
                
                var analyzerIndex = 0;
                foreach (var analyzer in analyzerList)
                {
                    analyzerIndex++;
                    mainTask.Description = $"[yellow]Running {analyzer.Name} ({analyzerIndex}/{analyzerList.Count})[/]";
                    
                    var analyzerTask = analyzerTasks[analyzer];
                    analyzerTask.Description = $"[yellow]{GetIcon(analyzer)} {analyzer.Name} - Starting...[/]";
                    
                    try
                    {
                        // Simulate progress for visual feedback
                        analyzerTask.Value = 10;
                        analyzerTask.Description = $"[yellow]{GetIcon(analyzer)} {analyzer.Name} - Analyzing...[/]";
                        
                        var result = await analyzer.AnalyzeAsync(context, cancellationToken);
                        
                        analyzerTask.Value = 100;
                        var status = result.Success ? "[green]✓ Complete[/]" : "[red]✗ Failed[/]";
                        analyzerTask.Description = $"{GetIcon(analyzer)} {analyzer.Name} - {status}";
                        
                        results.Add(result);
                        
                        // Add to live output log
                        var statusText = result.Success ? "SUCCESS" : "FAILED";
                        liveOutput.Add($"[{DateTime.Now:HH:mm:ss}] {analyzer.Name}: {statusText}");
                    }
                    catch (Exception ex)
                    {
                        analyzerTask.Value = 100;
                        analyzerTask.Description = $"{GetIcon(analyzer)} {analyzer.Name} - [red]✗ Error[/]";
                        
                        Logger.LogError(ex, "Analyzer {Name} failed", analyzer.Name);
                        results.Add(new AnalysisResult(analyzer.Name)
                        {
                            Success = false,
                            Fragment = ReportFragment.CreateError(
                                analyzer.Name,
                                $"Analysis failed: {ex.Message}")
                        });
                        
                        liveOutput.Add($"[{DateTime.Now:HH:mm:ss}] {analyzer.Name}: ERROR - {ex.Message}");
                    }
                    
                    mainTask.Increment(1);
                    
                    // Small delay for better visual feedback
                    await Task.Delay(500, cancellationToken);
                }
                
                mainTask.Description = "[green]Analysis complete! ✨[/]";
            });
        
        // Show live output summary
        if (liveOutput.Any())
        {
            Console.WriteLine();
            var outputPanel = new Panel(string.Join("\n", liveOutput.TakeLast(5)))
                .Header("[cyan]Analysis Log[/]")
                .BorderStyle(new Style(Color.Blue));
            Console.Write(outputPanel);
        }
        
        return results;
    }
    
    private void ShowAnalysisSummary(IEnumerable<AnalysisResult> results)
    {
        Console.WriteLine();
        
        var resultList = results.ToList();
        var successCount = resultList.Count(r => r.Success);
        var failureCount = resultList.Count - successCount;
        
        var table = new Table()
            .Border(TableBorder.Rounded)
            .AddColumn("Analyzer")
            .AddColumn("Status")
            .AddColumn("Severity");
        
        foreach (var result in resultList)
        {
            var status = result.Success ? "[green]✓ Success[/]" : "[red]✗ Failed[/]";
            var severity = GetSeverityDisplay(result.Fragment?.GetSeverity() ?? Severity.Info);
            table.AddRow(result.AnalyzerName, status, severity);
        }
        
        Console.Write(table);
        
        Console.WriteLine();
        Console.MarkupLine($"Analysis complete: [green]{successCount}[/] succeeded, [red]{failureCount}[/] failed");
    }
    
    private string GetIcon(IAnalyzer analyzer)
    {
        return analyzer.Name switch
        {
            "PluginAnalyzer" => "🔌",
            "MemoryAnalyzer" => "💾",
            "SettingsAnalyzer" => "⚙️",
            "PathAnalyzer" => "📁",
            "FcxModeAnalyzer" => "🎮",
            _ => "📊"
        };
    }
    
    private string GetSeverityDisplay(Severity severity)
    {
        return severity switch
        {
            Severity.Critical => "[red]Critical[/]",
            Severity.Error => "[red]Error[/]",
            Severity.Warning => "[yellow]Warning[/]",
            Severity.Info => "[cyan]Info[/]",
            _ => "[grey]Unknown[/]"
        };
    }
    
    private class AnalyzerInfo
    {
        public IAnalyzer Analyzer { get; }
        public string Name => Analyzer.Name;
        public string Description => Analyzer.Name; // Using Name as Description is not available
        
        public AnalyzerInfo(IAnalyzer analyzer)
        {
            Analyzer = analyzer;
        }
    }
}