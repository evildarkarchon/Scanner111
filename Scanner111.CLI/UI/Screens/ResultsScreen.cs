using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Scanner111.CLI.Models;
using Scanner111.CLI.Services;
using Scanner111.Core.Models;
using Scanner111.Core.Analysis;
using Scanner111.Core.Reporting;
using Spectre.Console;

namespace Scanner111.CLI.UI.Screens;

/// <summary>
/// Screen for viewing analysis results.
/// </summary>
public class ResultsScreen : BaseScreen, IDataReceiver
{
    private readonly IExportService _exportService;
    private IEnumerable<AnalysisResult>? _results;
    private List<AnalysisResult>? _filteredResults;
    private int _currentPage = 0;
    private const int ItemsPerPage = 5;
    private string _searchFilter = string.Empty;
    
    /// <summary>
    /// Gets the title of the screen.
    /// </summary>
    public override string Title => "Analysis Results";
    
    /// <summary>
    /// Initializes a new instance of the <see cref="ResultsScreen"/> class.
    /// </summary>
    /// <param name="console">The Spectre.Console instance.</param>
    /// <param name="services">The service provider.</param>
    /// <param name="logger">The logger.</param>
    public ResultsScreen(
        IAnsiConsole console,
        IServiceProvider services,
        ILogger<ResultsScreen> logger)
        : base(console, services, logger)
    {
        _exportService = services.GetRequiredService<IExportService>();
    }
    
    /// <summary>
    /// Sets the analysis results to display.
    /// </summary>
    /// <param name="data">The analysis results.</param>
    public void SetData(object data)
    {
        if (data is IEnumerable<AnalysisResult> results)
        {
            _results = results;
        }
    }
    
    /// <summary>
    /// Displays the analysis results.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The screen result.</returns>
    public override async Task<ScreenResult> DisplayAsync(CancellationToken cancellationToken = default)
    {
        if (_results == null || !_results.Any())
        {
            DrawHeader();
            ShowWarning("No results to display");
            await WaitForKeyAsync(cancellationToken: cancellationToken);
            return ScreenResult.Back;
        }
        
        // Initialize filtered results if not set
        _filteredResults ??= _results.ToList();
        
        while (!cancellationToken.IsCancellationRequested)
        {
            DrawHeader();
            
            // Display summary statistics
            DisplayResultsSummary();
            
            // Display search and filter information
            DisplaySearchInfo();
            
            // Display paginated results
            DisplayPaginatedResults();
            
            // Display navigation options
            var action = await GetUserActionAsync();
            
            switch (action)
            {
                case "search":
                    await HandleSearchAsync();
                    break;
                    
                case "filter":
                    await HandleFilterAsync();
                    break;
                    
                case "details":
                    await ShowDetailedResults(cancellationToken);
                    break;
                    
                case "next":
                    if ((_currentPage + 1) * ItemsPerPage < _filteredResults.Count)
                        _currentPage++;
                    break;
                    
                case "prev":
                    if (_currentPage > 0)
                        _currentPage--;
                    break;
                    
                case "export":
                    await HandleExportAsync(cancellationToken);
                    break;
                    
                case "back":
                    return ScreenResult.Back;
            }
        }
        
        return ScreenResult.Back;
    }
    
    private void AddFragmentToNode(TreeNode node, ReportFragment fragment)
    {
        if (!string.IsNullOrWhiteSpace(fragment.Content))
        {
            var content = fragment.Content
                .Split('\n')
                .Take(5) // Show first 5 lines
                .Select(line => line.Length > 80 ? line.Substring(0, 77) + "..." : line);
            
            foreach (var line in content)
            {
                node.AddNode($"[grey]{line}[/]");
            }
            
            if (fragment.Content.Split('\n').Length > 5)
            {
                node.AddNode("[dim]... (more)[/]");
            }
        }
        
        if (fragment.Children != null && fragment.Children.Any())
        {
            foreach (var child in fragment.Children)
            {
                var childNode = node.AddNode($"[cyan]{child.Title}[/]");
                AddFragmentToNode(childNode, child);
            }
        }
    }
    
    private void DisplayResultsSummary()
    {
        var total = _results!.Count();
        var success = _results?.Count(r => r.Success) ?? 0;
        var failed = total - success;
        var filtered = _filteredResults?.Count ?? 0;
        
        var severityStats = _filteredResults?
            .Where(r => r.Fragment != null)
            .GroupBy(r => r.Fragment!.GetSeverity())
            .OrderBy(g => g.Key)
            .Select(g => $"{GetSeverityIcon(g.Key)} {g.Count()}")
            .ToList() ?? new List<string>();
        
        var statsTable = new Table()
            .Border(TableBorder.None)
            .HideHeaders()
            .AddColumn("Stat")
            .AddColumn("Value");
        
        statsTable.AddRow("[cyan]Total Results:[/]", $"{total} ({success} success, {failed} failed)");
        statsTable.AddRow("[cyan]Filtered Results:[/]", $"{filtered}");
        
        if (severityStats.Any())
        {
            statsTable.AddRow("[cyan]Severity Breakdown:[/]", string.Join(" | ", severityStats));
        }
        
        var summaryPanel = new Panel(statsTable)
            .Header("[yellow]Results Summary[/]")
            .BorderStyle(new Style(Color.Cyan1));
        
        Console.Write(summaryPanel);
        Console.WriteLine();
    }
    
    private void DisplaySearchInfo()
    {
        if (!string.IsNullOrEmpty(_searchFilter))
        {
            Console.MarkupLine($"[dim]🔍 Search filter: '{_searchFilter}' | Press 'C' to clear[/]");
        }
        else
        {
            Console.MarkupLine("[dim]💡 Press 'S' to search, 'F' to filter by severity[/]");
        }
        Console.WriteLine();
    }
    
    private void DisplayPaginatedResults()
    {
        var startIndex = _currentPage * ItemsPerPage;
        var endIndex = Math.Min(startIndex + ItemsPerPage, _filteredResults!.Count);
        var pageResults = _filteredResults.Skip(startIndex).Take(ItemsPerPage);
        
        var resultsTable = new Table()
            .Border(TableBorder.Rounded)
            .AddColumn("[bold]#[/]")
            .AddColumn("[bold]Analyzer[/]")
            .AddColumn("[bold]Status[/]")
            .AddColumn("[bold]Severity[/]")
            .AddColumn("[bold]Summary[/]");
        
        var index = startIndex + 1;
        foreach (var result in pageResults)
        {
            var status = result.Success ? "[green]✓[/]" : "[red]✗[/]";
            var severity = result.Fragment != null ? GetSeverityDisplay(result.Fragment.GetSeverity()) : "[grey]N/A[/]";
            var summary = GetResultSummary(result);
            
            resultsTable.AddRow(
                index.ToString(),
                $"{GetAnalyzerIcon(result.AnalyzerName)} {result.AnalyzerName}",
                status,
                severity,
                summary);
            
            index++;
        }
        
        Console.Write(resultsTable);
        
        // Pagination info
        var totalPages = (int)Math.Ceiling((double)_filteredResults.Count / ItemsPerPage);
        if (totalPages > 1)
        {
            Console.WriteLine();
            Console.MarkupLine($"[dim]Page {_currentPage + 1} of {totalPages} | Showing {startIndex + 1}-{endIndex} of {_filteredResults.Count} results[/]");
        }
        
        Console.WriteLine();
    }
    
    private async Task<string> GetUserActionAsync()
    {
        await Task.CompletedTask;
        var choices = new List<string> { "🔍 Search Results", "🎯 Filter by Severity", "📋 View Details" };
        
        // Add pagination options if needed
        var totalPages = (int)Math.Ceiling((double)_filteredResults!.Count / ItemsPerPage);
        if (totalPages > 1)
        {
            if (_currentPage > 0)
                choices.Add("⬅️ Previous Page");
            if ((_currentPage + 1) * ItemsPerPage < _filteredResults.Count)
                choices.Add("➡️ Next Page");
        }
        
        choices.AddRange(new[] { "💾 Export Results", "🔄 Clear Filters", "🔙 Back to Menu" });
        
        var action = Console.Prompt(
            new SelectionPrompt<string>()
                .Title("[green]Select an action:[/]")
                .AddChoices(choices));
        
        return action switch
        {
            var s when s.Contains("Search") => "search",
            var s when s.Contains("Filter") => "filter",
            var s when s.Contains("Details") => "details",
            var s when s.Contains("Previous") => "prev",
            var s when s.Contains("Next") => "next",
            var s when s.Contains("Export") => "export",
            var s when s.Contains("Clear") => "clear",
            _ => "back"
        };
    }
    
    private async Task HandleSearchAsync()
    {
        var searchTerm = Console.Prompt(
            new TextPrompt<string>("[cyan]Enter search term (analyzer name, content, or empty to clear):[/]")
                .AllowEmpty());
        
        _searchFilter = searchTerm;
        
        if (string.IsNullOrWhiteSpace(searchTerm))
        {
            _filteredResults = _results!.ToList();
        }
        else
        {
            _filteredResults = _results!
                .Where(r => 
                    r.AnalyzerName.Contains(searchTerm, StringComparison.OrdinalIgnoreCase) ||
                    (r.Fragment?.Content?.Contains(searchTerm, StringComparison.OrdinalIgnoreCase) ?? false) ||
                    (r.Fragment?.Title?.Contains(searchTerm, StringComparison.OrdinalIgnoreCase) ?? false))
                .ToList();
        }
        
        _currentPage = 0;
        await Task.CompletedTask;
    }
    
    private async Task HandleFilterAsync()
    {
        var severities = Enum.GetValues<Severity>()
            .Select(s => new { Severity = s, Display = $"{GetSeverityIcon(s)} {s}" })
            .ToList();
        
        severities.Insert(0, new { Severity = (Severity)(-1), Display = "🔄 Show All" });
        
        var selectedSeverity = Console.Prompt(
            new SelectionPrompt<Severity>()
                .Title("[green]Filter by severity:[/]")
                .AddChoices(severities.Select(s => s.Severity))
                .UseConverter(s => s == (Severity)(-1) ? "🔄 Show All" : $"{GetSeverityIcon(s)} {s}"));
        
        if (selectedSeverity == (Severity)(-1))
        {
            _filteredResults = _results!.ToList();
            _searchFilter = string.Empty;
        }
        else
        {
            _filteredResults = _results!
                .Where(r => r.Fragment?.GetSeverity() == selectedSeverity)
                .ToList();
            _searchFilter = $"Severity: {selectedSeverity}";
        }
        
        _currentPage = 0;
        await Task.CompletedTask;
    }
    
    private async Task ShowDetailedResults(CancellationToken cancellationToken)
    {
        if (!_filteredResults!.Any())
        {
            ShowWarning("No results to display in detail");
            await WaitForKeyAsync(cancellationToken: cancellationToken);
            return;
        }
        
        var selectedResult = Console.Prompt(
            new SelectionPrompt<AnalysisResult>()
                .Title("[green]Select result to view in detail:[/]")
                .PageSize(10)
                .AddChoices(_filteredResults ?? new List<AnalysisResult>())
                .UseConverter(r => $"{GetAnalyzerIcon(r.AnalyzerName)} {r.AnalyzerName} - {GetSeverityIcon(r.Fragment?.GetSeverity() ?? Severity.Info)}"));
        
        Console.Clear();
        DrawHeader();
        
        if (selectedResult.Fragment != null)
        {
            // Display with syntax highlighting for different content types
            var content = selectedResult.Fragment.Content ?? "No content available";
            var highlightedContent = ApplySyntaxHighlighting(content, selectedResult.AnalyzerName);
            
            var panel = new Panel(highlightedContent)
                .Header($"[yellow]{selectedResult.AnalyzerName} - Detailed View[/]")
                .BorderStyle(new Style(GetSeverityConsoleColor(selectedResult.Fragment.GetSeverity())))
                .Padding(1, 1)
                .Expand();
            
            Console.Write(panel);
            
            // Show fragment children if any
            if (selectedResult.Fragment.Children?.Any() == true)
            {
                Console.WriteLine();
                var childTree = new Tree("[cyan]Additional Information[/]");
                
                foreach (var child in selectedResult.Fragment.Children)
                {
                    var childNode = childTree.AddNode($"[yellow]{child.Title}[/]");
                    if (!string.IsNullOrWhiteSpace(child.Content))
                    {
                        var childLines = child.Content.Split('\n').Take(3);
                        foreach (var line in childLines)
                        {
                            childNode.AddNode($"[grey]{line}[/]");
                        }
                        if (child.Content.Split('\n').Length > 3)
                        {
                            childNode.AddNode("[dim]... (truncated)[/]");
                        }
                    }
                }
                
                Console.Write(childTree);
            }
        }
        else
        {
            Console.MarkupLine("[red]No detailed information available for this result.[/]");
        }
        
        Console.WriteLine();
        await WaitForKeyAsync("Press any key to return...", cancellationToken: cancellationToken);
    }
    
    private async Task HandleExportAsync(CancellationToken cancellationToken)
    {
        var formats = new[]
        {
            "📝 Markdown (.md)",
            "🌐 HTML (.html)",
            "📊 JSON (.json)",
            "📄 Plain Text (.txt)"
        };
        
        var selectedFormat = Console.Prompt(
            new SelectionPrompt<string>()
                .Title("[green]Select export format:[/]")
                .AddChoices(formats));
        
        var format = selectedFormat switch
        {
            var s when s.Contains("Markdown") => ReportFormat.Markdown,
            var s when s.Contains("HTML") => ReportFormat.Html,
            var s when s.Contains("JSON") => ReportFormat.Json,
            _ => ReportFormat.PlainText
        };
        
        var fileName = Console.Ask<string>("Enter filename (without extension):");
        
        try
        {
            var resultsToExport = !string.IsNullOrEmpty(_searchFilter) ? _filteredResults : _results;
            
            var exportPath = await _exportService.ExportResultsAsync(
                resultsToExport!,
                fileName,
                format,
                cancellationToken);
            
            ShowSuccess($"Results exported to: {exportPath}");
            
            if (Confirm("Open exported file?"))
            {
                try
                {
                    System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
                    {
                        FileName = exportPath,
                        UseShellExecute = true
                    });
                }
                catch (Exception ex)
                {
                    Logger.LogWarning(ex, "Failed to open exported file");
                    ShowWarning("Could not open file automatically. Please open manually.");
                }
            }
        }
        catch (Exception ex)
        {
            ShowError($"Export failed: {ex.Message}");
            Logger.LogError(ex, "Export failed");
        }
        
        await WaitForKeyAsync(cancellationToken: cancellationToken);
    }
    
    private async Task ExportResultsAsync(ReportFormat format, CancellationToken cancellationToken)
    {
        try
        {
            var fileName = Console.Ask<string>($"Enter filename for {format} export (without extension):");
            
            var exportPath = await _exportService.ExportResultsAsync(
                _results!,
                fileName,
                format,
                cancellationToken);
            
            ShowSuccess($"Results exported to: {exportPath}");
        }
        catch (Exception ex)
        {
            ShowError($"Export failed: {ex.Message}");
            Logger.LogError(ex, "Export failed");
        }
        
        await WaitForKeyAsync(cancellationToken: cancellationToken);
    }
    
    private string GetSeverityColor(Severity severity)
    {
        return severity switch
        {
            Severity.Critical => "red bold",
            Severity.Error => "red",
            Severity.Warning => "yellow",
            Severity.Info => "cyan",
            _ => "white"
        };
    }
    
    private string GetSeverityIcon(Severity severity)
    {
        return severity switch
        {
            Severity.Critical => "🚨",
            Severity.Error => "❌",
            Severity.Warning => "⚠️",
            Severity.Info => "ℹ️",
            _ => "❓"
        };
    }
    
    private string GetSeverityDisplay(Severity severity)
    {
        var color = GetSeverityColor(severity);
        var icon = GetSeverityIcon(severity);
        return $"[{color}]{icon} {severity}[/]";
    }
    
    private string GetAnalyzerIcon(string analyzerName)
    {
        return analyzerName switch
        {
            "PluginAnalyzer" => "🔌",
            "MemoryAnalyzer" => "💾",
            "SettingsAnalyzer" => "⚙️",
            "PathAnalyzer" => "📁",
            "FcxModeAnalyzer" => "🎮",
            _ => "📊"
        };
    }
    
    private string GetResultSummary(AnalysisResult result)
    {
        if (result.Fragment?.Content == null)
            return "[dim]No details available[/]";
        
        var content = result.Fragment.Content;
        var firstLine = content.Split('\n').FirstOrDefault() ?? "";
        
        if (firstLine.Length > 50)
            firstLine = firstLine[..47] + "...";
        
        return firstLine.Length > 0 ? $"[grey]{firstLine}[/]" : "[dim]No summary[/]";
    }
    
    private Markup ApplySyntaxHighlighting(string content, string analyzerName)
    {
        // Apply basic syntax highlighting based on analyzer type and content patterns
        var highlightedContent = content;
        
        // Highlight file paths
        highlightedContent = System.Text.RegularExpressions.Regex.Replace(
            highlightedContent,
            @"[A-Za-z]:\\[^\s]+\.[a-zA-Z0-9]+",
            "[blue]$0[/]");
        
        // Highlight error/exception patterns
        highlightedContent = System.Text.RegularExpressions.Regex.Replace(
            highlightedContent,
            @"\b(Error|Exception|Failed|Critical)\b",
            "[red bold]$0[/]",
            System.Text.RegularExpressions.RegexOptions.IgnoreCase);
        
        // Highlight success patterns
        highlightedContent = System.Text.RegularExpressions.Regex.Replace(
            highlightedContent,
            @"\b(Success|Passed|OK|Complete)\b",
            "[green bold]$0[/]",
            System.Text.RegularExpressions.RegexOptions.IgnoreCase);
        
        // Highlight warnings
        highlightedContent = System.Text.RegularExpressions.Regex.Replace(
            highlightedContent,
            @"\b(Warning|Caution|Note)\b",
            "[yellow bold]$0[/]",
            System.Text.RegularExpressions.RegexOptions.IgnoreCase);
        
        // Highlight numbers and percentages
        highlightedContent = System.Text.RegularExpressions.Regex.Replace(
            highlightedContent,
            @"\b\d+(?:\.\d+)?(?:%|MB|KB|GB)?\b",
            "[cyan]$0[/]");
        
        return new Markup(highlightedContent);
    }
    
    private Color GetSeverityConsoleColor(Severity severity)
    {
        return severity switch
        {
            Severity.Critical => Color.Red,
            Severity.Error => Color.DarkRed,
            Severity.Warning => Color.Yellow,
            Severity.Info => Color.Cyan1,
            _ => Color.White
        };
    }
}