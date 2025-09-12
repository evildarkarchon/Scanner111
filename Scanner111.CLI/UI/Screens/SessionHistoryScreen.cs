using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Scanner111.CLI.Extensions;
using Scanner111.CLI.Services;
using Scanner111.CLI.UI.Components;
using Scanner111.Core.Analysis;
using Scanner111.Core.Orchestration;
using Spectre.Console;
using Spectre.Console.Rendering;

namespace Scanner111.CLI.UI.Screens;

/// <summary>
/// Screen for managing analysis session history.
/// </summary>
public class SessionHistoryScreen : BaseScreen
{
    private readonly ISessionManager _sessionManager;
    private readonly IAnalyzerOrchestrator _orchestrator;
    private readonly StatisticsDisplay _statisticsDisplay;
    private List<SessionMetadata> _sessions = new();
    private SessionMetadata? _selectedSession;
    private bool _showComparison = false;
    private SessionMetadata? _comparisonSession;
    
    /// <inheritdoc />
    public override string Title => "Session History";
    
    /// <summary>
    /// Initializes a new instance of the <see cref="SessionHistoryScreen"/> class.
    /// </summary>
    public SessionHistoryScreen(
        IAnsiConsole console, 
        IServiceProvider services, 
        ILogger<SessionHistoryScreen> logger)
        : base(console, services, logger)
    {
        _sessionManager = services.GetRequiredService<ISessionManager>();
        _orchestrator = services.GetRequiredService<IAnalyzerOrchestrator>();
        _statisticsDisplay = services.GetRequiredService<StatisticsDisplay>();
    }
    
    /// <inheritdoc />
    public override async Task<ScreenResult> DisplayAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            await LoadSessionsAsync(cancellationToken);
            
            while (!cancellationToken.IsCancellationRequested)
            {
                DrawHeader();
                
                if (_showComparison && _selectedSession != null && _comparisonSession != null)
                {
                    await DisplaySessionComparisonAsync(cancellationToken);
                }
                else if (_selectedSession != null)
                {
                    await DisplaySessionDetailsAsync(cancellationToken);
                }
                else
                {
                    DisplaySessionList();
                }
                
                DrawFooter();
                
                var action = await GetUserActionAsync(cancellationToken);
                var result = await ProcessActionAsync(action, cancellationToken);
                
                if (result != ScreenResult.Back)
                {
                    return result;
                }
            }
            
            return ScreenResult.Back;
        }
        catch (OperationCanceledException)
        {
            Logger.LogInformation("Session history screen was cancelled");
            return ScreenResult.Back;
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Error in session history screen");
            ShowError($"An error occurred: {ex.Message}");
            await WaitForKeyAsync(cancellationToken: cancellationToken);
            return ScreenResult.Back;
        }
    }
    
    private async Task LoadSessionsAsync(CancellationToken cancellationToken)
    {
        try
        {
            var sessions = await _sessionManager.GetSessionMetadataAsync(100, cancellationToken);
            _sessions = sessions.OrderByDescending(s => s.StartTime).ToList();
            Logger.LogDebug("Loaded {Count} sessions", _sessions.Count);
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Failed to load sessions");
            _sessions = new List<SessionMetadata>();
        }
    }
    
    private void DisplaySessionList()
    {
        if (!_sessions.Any())
        {
            Console.MarkupLine("[dim]No sessions found. Run some analyses to see them here.[/]");
            return;
        }
        
        var table = new Table()
            .Border(TableBorder.Rounded)
            .BorderColor(Color.Cyan1)
            .AddColumn(new TableColumn("#").Width(3).Centered())
            .AddColumn(new TableColumn("[bold]Session ID[/]").Width(12))
            .AddColumn(new TableColumn("[bold]Log File[/]").Width(40))
            .AddColumn(new TableColumn("[bold]Start Time[/]").Width(16).Centered())
            .AddColumn(new TableColumn("[bold]Duration[/]").Width(10).Centered())
            .AddColumn(new TableColumn("[bold]Results[/]").Width(8).Centered())
            .AddColumn(new TableColumn("[bold]Status[/]").Width(10).Centered());
        
        for (int i = 0; i < _sessions.Count; i++)
        {
            var session = _sessions[i];
            var shortId = session.Id.ToString("N")[..8];
            var fileName = Path.GetFileName(session.LogFile);
            var duration = session.Duration?.ToString(@"hh\:mm\:ss") ?? "--:--:--";
            var status = session.EndTime.HasValue ? "[green]Complete[/]" : "[yellow]Running[/]";
            
            if (fileName.Length > 37)
            {
                fileName = fileName[..34] + "...";
            }
            
            table.AddRow(
                (i + 1).ToString(),
                $"[cyan]{shortId}[/]",
                fileName,
                session.StartTime.ToString("MM/dd HH:mm"),
                duration,
                session.ResultCount.ToString(),
                status
            );
        }
        
        Console.Write(table);
        Console.WriteLine();
        
        // Show quick actions
        var panel = new Panel(new Markup(
            "[yellow]Enter session number[/] to view details • " +
            "[yellow]C[/] to compare sessions • " +
            "[yellow]R[/] to refresh • " +
            "[yellow]D[/] to delete session • " +
            "[yellow]X[/] to export session • " +
            "[yellow]I[/] to import session"))
        {
            Header = new PanelHeader("[cyan]Quick Actions[/]"),
            Border = BoxBorder.Rounded
        };
        Console.Write(panel);
    }
    
    private async Task DisplaySessionDetailsAsync(CancellationToken cancellationToken)
    {
        if (_selectedSession == null) return;
        
        try
        {
            var fullSession = await _sessionManager.LoadSessionAsync(_selectedSession.Id, cancellationToken);
            if (fullSession == null)
            {
                ShowError("Session not found or could not be loaded");
                _selectedSession = null;
                return;
            }
            
            // Create session info panel
            var infoTable = new Table()
                .Border(TableBorder.None)
                .HideHeaders()
                .AddColumn("Key")
                .AddColumn("Value");
            
            infoTable.AddRow("Session ID", fullSession.Id.ToString());
            infoTable.AddRow("Log File", fullSession.LogFile);
            infoTable.AddRow("Start Time", fullSession.StartTime.ToString("yyyy-MM-dd HH:mm:ss"));
            if (fullSession.EndTime.HasValue)
                infoTable.AddRow("End Time", fullSession.EndTime.Value.ToString("yyyy-MM-dd HH:mm:ss"));
            if (fullSession.Duration.HasValue)
                infoTable.AddRow("Duration", fullSession.Duration.Value.ToString(@"hh\:mm\:ss"));
            infoTable.AddRow("Results Count", fullSession.Results.Count.ToString());
            
            var infoPanel = new Panel(infoTable)
            {
                Header = new PanelHeader("[cyan]Session Information[/]"),
                Border = BoxBorder.Rounded
            };
            
            // Create results panel
            var resultsPanel = CreateResultsPanel(fullSession.Results);
            
            // Create statistics panel
            var statsPanel = _statisticsDisplay.CreateSeverityChart(fullSession.Results);
            
            // Layout
            var layout = new Layout("Root")
                .SplitRows(
                    new Layout("Top").SplitColumns(
                        new Layout("Info"),
                        new Layout("Stats")
                    ).Ratio(1),
                    new Layout("Results").Ratio(2)
                );
            
            layout["Info"].Update(infoPanel);
            layout["Stats"].Update(statsPanel);
            layout["Results"].Update(resultsPanel);
            
            Console.Write(layout);
            Console.WriteLine();
            
            // Show session actions
            var actionsPanel = new Panel(new Markup(
                "[yellow]R[/] to re-run analysis • " +
                "[yellow]E[/] to export session • " +
                "[yellow]C[/] to compare with another session • " +
                "[yellow]D[/] to delete session • " +
                "[yellow]B[/] to go back"))
            {
                Header = new PanelHeader("[cyan]Session Actions[/]"),
                Border = BoxBorder.Rounded
            };
            Console.Write(actionsPanel);
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Error displaying session details for {SessionId}", _selectedSession?.Id);
            ShowError($"Failed to load session details: {ex.Message}");
            _selectedSession = null;
        }
    }
    
    private async Task DisplaySessionComparisonAsync(CancellationToken cancellationToken)
    {
        if (_selectedSession == null || _comparisonSession == null) return;
        
        try
        {
            var comparison = await _sessionManager.CompareSessionsAsync(
                _selectedSession.Id, 
                _comparisonSession.Id, 
                cancellationToken);
            
            // Comparison header
            var headerTable = new Table()
                .Border(TableBorder.None)
                .HideHeaders()
                .AddColumn("Metric")
                .AddColumn("Session 1")
                .AddColumn("Session 2")
                .AddColumn("Difference");
            
            headerTable.AddRow(
                "Session ID",
                comparison.Session1Id.ToString("N")[..8],
                comparison.Session2Id.ToString("N")[..8],
                "");
            headerTable.AddRow(
                "Start Time",
                comparison.Session1StartTime.ToString("MM/dd HH:mm"),
                comparison.Session2StartTime.ToString("MM/dd HH:mm"),
                "");
            headerTable.AddRow(
                "Results",
                comparison.Session1ResultCount.ToString(),
                comparison.Session2ResultCount.ToString(),
                (comparison.Session1ResultCount - comparison.Session2ResultCount).ToString("+#;-#;0"));
            
            if (comparison.Session1Duration.HasValue && comparison.Session2Duration.HasValue)
            {
                var durationDiff = comparison.Session1Duration.Value - comparison.Session2Duration.Value;
                headerTable.AddRow(
                    "Duration",
                    comparison.Session1Duration.Value.ToString(@"hh\:mm\:ss"),
                    comparison.Session2Duration.Value.ToString(@"hh\:mm\:ss"),
                    durationDiff.ToString(@"hh\:mm\:ss"));
            }
            
            var headerPanel = new Panel(headerTable)
            {
                Header = new PanelHeader("[cyan]Session Comparison[/]"),
                Border = BoxBorder.Rounded
            };
            
            // Severity comparison
            var severityTable = new Table()
                .Border(TableBorder.Rounded)
                .BorderColor(Color.Yellow)
                .AddColumn("[bold]Severity[/]")
                .AddColumn("[bold]Session 1[/]")
                .AddColumn("[bold]Session 2[/]")
                .AddColumn("[bold]Change[/]");
            
            foreach (var kvp in comparison.SeverityComparison.OrderBy(x => x.Key))
            {
                var change = kvp.Value.Session1Count - kvp.Value.Session2Count;
                var changeColor = change switch
                {
                    > 0 => "red",
                    < 0 => "green",
                    _ => "dim"
                };
                
                severityTable.AddRow(
                    kvp.Key,
                    kvp.Value.Session1Count.ToString(),
                    kvp.Value.Session2Count.ToString(),
                    $"[{changeColor}]{change:+#;-#;0}[/]");
            }
            
            var severityPanel = new Panel(severityTable)
            {
                Header = new PanelHeader("[cyan]Severity Changes[/]"),
                Border = BoxBorder.Rounded
            };
            
            // Issue changes
            var changesLayout = new Layout("Changes")
                .SplitColumns(
                    new Layout("New"),
                    new Layout("Resolved")
                );
            
            var newIssuesPanel = CreateIssuesPanel(comparison.NewIssues, "[green]New Issues[/]", "green");
            var resolvedIssuesPanel = CreateIssuesPanel(comparison.ResolvedIssues, "[red]Resolved Issues[/]", "red");
            
            changesLayout["New"].Update(newIssuesPanel);
            changesLayout["Resolved"].Update(resolvedIssuesPanel);
            
            // Main layout
            var mainLayout = new Layout("Root")
                .SplitRows(
                    new Layout("Header").Ratio(1),
                    new Layout("Severity").Ratio(1),
                    new Layout("Changes").Ratio(2)
                );
            
            mainLayout["Header"].Update(headerPanel);
            mainLayout["Severity"].Update(severityPanel);
            mainLayout["Changes"].Update(changesLayout);
            
            Console.Write(mainLayout);
            Console.WriteLine();
            
            // Show comparison actions
            var actionsPanel = new Panel(new Markup(
                "[yellow]B[/] to go back • " +
                "[yellow]E[/] to export comparison • " +
                "[yellow]S[/] to switch sessions"))
            {
                Header = new PanelHeader("[cyan]Comparison Actions[/]"),
                Border = BoxBorder.Rounded
            };
            Console.Write(actionsPanel);
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Error displaying session comparison");
            ShowError($"Failed to compare sessions: {ex.Message}");
            _showComparison = false;
        }
    }
    
    private Panel CreateResultsPanel(List<AnalysisResult> results)
    {
        if (!results.Any())
        {
            return new Panel(new Text("No results in this session"))
            {
                Header = new PanelHeader("[cyan]Analysis Results[/]"),
                Border = BoxBorder.Rounded
            };
        }
        
        var table = new Table()
            .Border(TableBorder.Rounded)
            .BorderColor(Color.Cyan1)
            .AddColumn("[bold]Severity[/]")
            .AddColumn("[bold]Title[/]")
            .AddColumn("[bold]Summary[/]")
            .AddColumn("[bold]Analyzer[/]");
        
        foreach (var result in results.Take(20)) // Show first 20 results
        {
            var severityColor = GetSeverityColor(result.Severity);
            table.AddRow(
                $"[{severityColor}]{result.Severity}[/]",
                result.GetTitle() ?? "No title",
                result.Fragment?.GetSummary() ?? "No summary",
                result.AnalyzerName ?? "Unknown"
            );
        }
        
        if (results.Count > 20)
        {
            table.AddRow("[dim]...[/]", $"[dim]... and {results.Count - 20} more results[/]", "", "");
        }
        
        return new Panel(table)
        {
            Header = new PanelHeader("[cyan]Analysis Results[/]"),
            Border = BoxBorder.Rounded
        };
    }
    
    private Panel CreateIssuesPanel(List<AnalysisResult> issues, string title, string borderColor)
    {
        if (!issues.Any())
        {
            var borderColorObj = borderColor switch
            {
                "green" => Color.Green,
                "red" => Color.Red,
                "blue" => Color.Blue,
                "yellow" => Color.Yellow,
                "orange" => Color.Orange1,
                _ => Color.White
            };
            
            return new Panel(new Text("No issues"))
            {
                Header = new PanelHeader(title),
                Border = BoxBorder.Rounded,
                BorderStyle = new Style(borderColorObj)
            };
        }
        
        var list = new List<Text>();
        foreach (var issue in issues.Take(10))
        {
            list.Add(new Text($"• {issue.GetTitle() ?? "Unknown"}"));
        }
        
        if (issues.Count > 10)
        {
            list.Add(new Text($"[dim]... and {issues.Count - 10} more[/]"));
        }
        
        var borderColorObj2 = borderColor switch
        {
            "green" => Color.Green,
            "red" => Color.Red,
            "blue" => Color.Blue,
            "yellow" => Color.Yellow,
            "orange" => Color.Orange1,
            _ => Color.White
        };
        
        return new Panel(new Rows(list.Cast<IRenderable>()))
        {
            Header = new PanelHeader(title),
            Border = BoxBorder.Rounded,
            BorderStyle = new Style(borderColorObj2)
        };
    }
    
    private async Task<string> GetUserActionAsync(CancellationToken cancellationToken)
    {
        var key = await WaitForKeyAsync("", timeout: 100, cancellationToken);
        if (!key.HasValue) return "none";
        
        return key.Value.Key switch
        {
            ConsoleKey.Escape => "back",
            ConsoleKey.Q => "quit",
            ConsoleKey.R => "refresh",
            ConsoleKey.C => "compare",
            ConsoleKey.D => "delete",
            ConsoleKey.E => "export",
            ConsoleKey.X => "export",
            ConsoleKey.I => "import",
            ConsoleKey.B => "back",
            ConsoleKey.S => "switch",
            ConsoleKey.F5 => "refresh",
            ConsoleKey.D1 => "select_1",
            ConsoleKey.D2 => "select_2",
            ConsoleKey.D3 => "select_3",
            ConsoleKey.D4 => "select_4",
            ConsoleKey.D5 => "select_5",
            ConsoleKey.D6 => "select_6",
            ConsoleKey.D7 => "select_7",
            ConsoleKey.D8 => "select_8",
            ConsoleKey.D9 => "select_9",
            ConsoleKey.Enter => "enter",
            _ => key.Value.KeyChar.ToString().ToLowerInvariant()
        };
    }
    
    private async Task<ScreenResult> ProcessActionAsync(string action, CancellationToken cancellationToken)
    {
        try
        {
            switch (action)
            {
                case "back" or "quit":
                    return action == "quit" ? ScreenResult.Exit : ScreenResult.Back;
                
                case "refresh":
                    await LoadSessionsAsync(cancellationToken);
                    break;
                
                case "compare":
                    if (_selectedSession != null)
                    {
                        await SelectComparisonSessionAsync(cancellationToken);
                    }
                    else
                    {
                        await SelectSessionsForComparisonAsync(cancellationToken);
                    }
                    break;
                
                case "delete":
                    await DeleteSessionAsync(cancellationToken);
                    break;
                
                case "export":
                    await ExportSessionAsync(cancellationToken);
                    break;
                
                case "import":
                    await ImportSessionAsync(cancellationToken);
                    break;
                
                case "switch":
                    if (_showComparison)
                    {
                        (_selectedSession, _comparisonSession) = (_comparisonSession, _selectedSession);
                    }
                    break;
                
                case var s when s.StartsWith("select_") && int.TryParse(s[7..], out var index):
                    await SelectSessionByIndexAsync(index - 1, cancellationToken);
                    break;
                
                case var s when int.TryParse(s, out var number):
                    await SelectSessionByIndexAsync(number - 1, cancellationToken);
                    break;
                
                case "enter":
                    if (_selectedSession == null && _sessions.Any())
                    {
                        _selectedSession = _sessions[0];
                    }
                    break;
            }
            
            return ScreenResult.Back;
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Error processing action: {Action}", action);
            ShowError($"Action failed: {ex.Message}");
            await WaitForKeyAsync(cancellationToken: cancellationToken);
            return ScreenResult.Back;
        }
    }
    
    private async Task SelectSessionByIndexAsync(int index, CancellationToken cancellationToken)
    {
        if (index >= 0 && index < _sessions.Count)
        {
            _selectedSession = _sessions[index];
            _showComparison = false;
            _comparisonSession = null;
            Logger.LogDebug("Selected session {SessionId}", _selectedSession.Id);
        }
        else
        {
            ShowWarning("Invalid session number");
            await Task.Delay(1000, cancellationToken);
        }
    }
    
    private async Task SelectSessionsForComparisonAsync(CancellationToken cancellationToken)
    {
        if (_sessions.Count < 2)
        {
            ShowWarning("Need at least 2 sessions to compare");
            await Task.Delay(2000, cancellationToken);
            return;
        }
        
        try
        {
            Console.Clear();
            DrawHeader();
            Console.MarkupLine("[yellow]Select first session for comparison:[/]");
            DisplaySessionList();
            
            var firstChoice = Console.Prompt(
                new TextPrompt<int>("Enter session number:")
                    .ValidationErrorMessage("Please enter a valid session number")
                    .Validate(n => n > 0 && n <= _sessions.Count ? Spectre.Console.ValidationResult.Success() : Spectre.Console.ValidationResult.Error()));
            
            Console.Clear();
            DrawHeader();
            Console.MarkupLine("[yellow]Select second session for comparison:[/]");
            DisplaySessionList();
            
            var secondChoice = Console.Prompt(
                new TextPrompt<int>("Enter session number:")
                    .ValidationErrorMessage("Please enter a valid session number")
                    .Validate(n => n > 0 && n <= _sessions.Count && n != firstChoice ? 
                        Spectre.Console.ValidationResult.Success() : 
                        Spectre.Console.ValidationResult.Error("Must be different from first session")));
            
            _selectedSession = _sessions[firstChoice - 1];
            _comparisonSession = _sessions[secondChoice - 1];
            _showComparison = true;
            
            Logger.LogInformation("Selected sessions for comparison: {Session1} vs {Session2}", 
                _selectedSession.Id, _comparisonSession.Id);
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Error selecting sessions for comparison");
            ShowError("Failed to select sessions for comparison");
            await WaitForKeyAsync(cancellationToken: cancellationToken);
        }
    }
    
    private async Task SelectComparisonSessionAsync(CancellationToken cancellationToken)
    {
        try
        {
            Console.Clear();
            DrawHeader();
            Console.MarkupLine($"[yellow]Select session to compare with {_selectedSession!.Id.ToString("N")[..8]}:[/]");
            DisplaySessionList();
            
            var choice = Console.Prompt(
                new TextPrompt<int>("Enter session number:")
                    .ValidationErrorMessage("Please enter a valid session number")
                    .Validate(n => n > 0 && n <= _sessions.Count ? Spectre.Console.ValidationResult.Success() : Spectre.Console.ValidationResult.Error()));
            
            _comparisonSession = _sessions[choice - 1];
            
            if (_comparisonSession.Id == _selectedSession.Id)
            {
                ShowWarning("Cannot compare session with itself");
                await Task.Delay(2000, cancellationToken);
                return;
            }
            
            _showComparison = true;
            Logger.LogInformation("Selected comparison session: {SessionId}", _comparisonSession.Id);
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Error selecting comparison session");
            ShowError("Failed to select comparison session");
            await WaitForKeyAsync(cancellationToken: cancellationToken);
        }
    }
    
    private async Task DeleteSessionAsync(CancellationToken cancellationToken)
    {
        SessionMetadata? sessionToDelete = null;
        
        if (_selectedSession != null)
        {
            sessionToDelete = _selectedSession;
        }
        else if (_sessions.Any())
        {
            try
            {
                Console.Clear();
                DrawHeader();
                Console.MarkupLine("[red]Select session to delete:[/]");
                DisplaySessionList();
                
                var choice = Console.Prompt(
                    new TextPrompt<int>("Enter session number:")
                        .ValidationErrorMessage("Please enter a valid session number")
                        .Validate(n => n > 0 && n <= _sessions.Count ? Spectre.Console.ValidationResult.Success() : Spectre.Console.ValidationResult.Error()));
                
                sessionToDelete = _sessions[choice - 1];
            }
            catch (Exception ex)
            {
                Logger.LogError(ex, "Error selecting session for deletion");
                ShowError("Failed to select session for deletion");
                await WaitForKeyAsync(cancellationToken: cancellationToken);
                return;
            }
        }
        
        if (sessionToDelete == null)
        {
            ShowWarning("No session selected for deletion");
            await Task.Delay(2000, cancellationToken);
            return;
        }
        
        var confirm = Confirm($"Delete session {sessionToDelete.Id.ToString("N")[..8]}? This cannot be undone.");
        if (!confirm) return;
        
        try
        {
            var deleted = await _sessionManager.DeleteSessionAsync(sessionToDelete.Id, cancellationToken);
            if (deleted)
            {
                ShowSuccess($"Session {sessionToDelete.Id.ToString("N")[..8]} deleted successfully");
                
                // Clear selection if it was the deleted session
                if (_selectedSession?.Id == sessionToDelete.Id)
                {
                    _selectedSession = null;
                    _showComparison = false;
                    _comparisonSession = null;
                }
                if (_comparisonSession?.Id == sessionToDelete.Id)
                {
                    _comparisonSession = null;
                    _showComparison = false;
                }
                
                await LoadSessionsAsync(cancellationToken);
            }
            else
            {
                ShowWarning("Session not found or could not be deleted");
            }
            
            await Task.Delay(2000, cancellationToken);
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Error deleting session {SessionId}", sessionToDelete.Id);
            ShowError($"Failed to delete session: {ex.Message}");
            await WaitForKeyAsync(cancellationToken: cancellationToken);
        }
    }
    
    private async Task ExportSessionAsync(CancellationToken cancellationToken)
    {
        if (_selectedSession == null)
        {
            ShowWarning("No session selected for export");
            await Task.Delay(2000, cancellationToken);
            return;
        }
        
        try
        {
            var format = Console.Prompt(
                new SelectionPrompt<SessionExportFormat>()
                    .Title("Select export format:")
                    .AddChoices(SessionExportFormat.Json, SessionExportFormat.Csv, SessionExportFormat.Html));
            
            var extension = format switch
            {
                SessionExportFormat.Json => "json",
                SessionExportFormat.Csv => "csv",
                SessionExportFormat.Html => "html",
                _ => "json"
            };
            
            var defaultPath = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.Desktop),
                $"session_{_selectedSession.Id:N}_{DateTime.Now:yyyyMMdd_HHmmss}.{extension}");
            
            var exportPath = Console.Prompt(
                new TextPrompt<string>("Export file path:")
                    .DefaultValue(defaultPath)
                    .ValidationErrorMessage("Please enter a valid file path"));
            
            await _sessionManager.ExportSessionAsync(_selectedSession.Id, exportPath, format, cancellationToken);
            
            ShowSuccess($"Session exported to {exportPath}");
            await Task.Delay(2000, cancellationToken);
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Error exporting session {SessionId}", _selectedSession.Id);
            ShowError($"Failed to export session: {ex.Message}");
            await WaitForKeyAsync(cancellationToken: cancellationToken);
        }
    }
    
    private async Task ImportSessionAsync(CancellationToken cancellationToken)
    {
        try
        {
            var importPath = Console.Prompt(
                new TextPrompt<string>("Import file path:")
                    .ValidationErrorMessage("Please enter a valid file path")
                    .Validate(path => File.Exists(path) ? Spectre.Console.ValidationResult.Success() : Spectre.Console.ValidationResult.Error("File does not exist")));
            
            var importedSession = await _sessionManager.ImportSessionAsync(importPath, cancellationToken);
            
            ShowSuccess($"Session imported successfully with ID {importedSession.Id.ToString("N")[..8]}");
            await LoadSessionsAsync(cancellationToken);
            await Task.Delay(2000, cancellationToken);
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Error importing session");
            ShowError($"Failed to import session: {ex.Message}");
            await WaitForKeyAsync(cancellationToken: cancellationToken);
        }
    }
    
    private static string GetSeverityColor(AnalysisSeverity severity) => severity switch
    {
        AnalysisSeverity.Critical => "red",
        AnalysisSeverity.Error => "orange3",
        AnalysisSeverity.Warning => "yellow",
        AnalysisSeverity.Info => "green",
        AnalysisSeverity.None => "white",
        _ => "white"
    };
}