using System.Collections.Concurrent;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Scanner111.CLI.Extensions;
using Scanner111.CLI.Services;
using Scanner111.Core.Analysis;
using Scanner111.Core.Orchestration;
using Spectre.Console;
using Spectre.Console.Rendering;

namespace Scanner111.CLI.UI.Screens;

/// <summary>
/// Screen for real-time log file monitoring and analysis.
/// </summary>
public class LogMonitorScreen : BaseScreen
{
    private readonly IFileWatcher _fileWatcher;
    private readonly IAnalyzerOrchestrator _orchestrator;
    private readonly IIncrementalAnalyzer _incrementalAnalyzer;
    private readonly ISessionManager _sessionManager;
    
    private readonly ConcurrentQueue<string> _logLines = new();
    private readonly object _layoutLock = new();
    private volatile bool _isPaused = false;
    private volatile bool _isMonitoring = false;
    private Session? _currentSession;
    private Layout? _layout;
    private Panel? _logPanel;
    private Panel? _analysisPanel;
    private Panel? _statusPanel;
    
    private const int MaxLogLines = 1000;
    private const int TailLines = 50;
    
    /// <inheritdoc />
    public override string Title => "Log File Monitor";
    
    /// <summary>
    /// Initializes a new instance of the <see cref="LogMonitorScreen"/> class.
    /// </summary>
    public LogMonitorScreen(
        IAnsiConsole console, 
        IServiceProvider services, 
        ILogger<LogMonitorScreen> logger)
        : base(console, services, logger)
    {
        _fileWatcher = services.GetRequiredService<IFileWatcher>();
        _orchestrator = services.GetRequiredService<IAnalyzerOrchestrator>();
        _incrementalAnalyzer = services.GetRequiredService<IIncrementalAnalyzer>();
        _sessionManager = services.GetRequiredService<ISessionManager>();
    }
    
    /// <inheritdoc />
    public override async Task<ScreenResult> DisplayAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            DrawHeader();
            
            // Get log file path from user
            var logFile = Console.Prompt(
                new TextPrompt<string>("[yellow]Enter log file path to monitor:[/]")
                    .ValidationErrorMessage("[red]Please enter a valid file path[/]")
                    .Validate(path => File.Exists(path) ? Spectre.Console.ValidationResult.Success() : Spectre.Console.ValidationResult.Error("File does not exist")));
            
            Console.Clear();
            DrawHeader();
            
            // Create session
            _currentSession = await _sessionManager.CreateSessionAsync(logFile, cancellationToken);
            Logger.LogInformation("Started monitoring session {SessionId} for {LogFile}", _currentSession.Id, logFile);
            
            // Setup layout
            SetupLayout();
            
            // Start monitoring
            await StartMonitoringAsync(logFile, cancellationToken);
            
            return ScreenResult.Back;
        }
        catch (OperationCanceledException)
        {
            Logger.LogInformation("Log monitoring was cancelled");
            return ScreenResult.Back;
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Error in log monitor screen");
            ShowError($"An error occurred: {ex.Message}");
            await WaitForKeyAsync(cancellationToken: cancellationToken);
            return ScreenResult.Back;
        }
        finally
        {
            await StopMonitoringAsync();
        }
    }
    
    private void SetupLayout()
    {
        // Create the main layout with three panels
        _layout = new Layout("Root")
            .SplitRows(
                new Layout("Main").SplitColumns(
                    new Layout("Log").Ratio(2),
                    new Layout("Analysis").Ratio(1)
                ).Ratio(4),
                new Layout("Status").Ratio(1)
            );
        
        _logPanel = new Panel(new Text("Waiting for log data..."))
        {
            Header = new PanelHeader("[yellow]Log Content (Live)[/]"),
            Border = BoxBorder.Rounded
        };
        
        _analysisPanel = new Panel(new Text("Analysis will appear here..."))
        {
            Header = new PanelHeader("[cyan]Analysis Results[/]"),
            Border = BoxBorder.Rounded
        };
        
        _statusPanel = new Panel(CreateStatusTable())
        {
            Header = new PanelHeader("[green]Monitor Status[/]"),
            Border = BoxBorder.Rounded
        };
        
        _layout["Log"].Update(_logPanel);
        _layout["Analysis"].Update(_analysisPanel);
        _layout["Status"].Update(_statusPanel);
        
        Console.Write(_layout);
    }
    
    private async Task StartMonitoringAsync(string logFile, CancellationToken cancellationToken)
    {
        _isMonitoring = true;
        
        // Load initial log content
        await LoadInitialLogContentAsync(logFile, cancellationToken);
        
        // Setup file watcher
        _fileWatcher.FileChanged += OnFileChanged;
        _fileWatcher.StartWatching(logFile, "*.log");
        
        // Start update loop
        var updateTask = UpdateDisplayLoopAsync(cancellationToken);
        
        // Handle keyboard input
        var keyboardTask = HandleKeyboardInputAsync(cancellationToken);
        
        try
        {
            await Task.WhenAny(updateTask, keyboardTask);
        }
        finally
        {
            _fileWatcher.FileChanged -= OnFileChanged;
            _fileWatcher.StopWatching();
        }
    }
    
    private async Task LoadInitialLogContentAsync(string logFile, CancellationToken cancellationToken)
    {
        try
        {
            var lines = await File.ReadAllLinesAsync(logFile, cancellationToken);
            var tailLines = lines.TakeLast(TailLines);
            
            foreach (var line in tailLines)
            {
                _logLines.Enqueue(line);
            }
            
            // Run initial analysis
            await RunIncrementalAnalysisAsync(logFile, cancellationToken);
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Failed to load initial log content from {LogFile}", logFile);
        }
    }
    
    private async void OnFileChanged(object? sender, FileChangedEventArgs e)
    {
        if (_isPaused) return;
        
        try
        {
            // Read new lines from file
            var newLines = await ReadNewLinesAsync(e.FullPath);
            foreach (var line in newLines)
            {
                _logLines.Enqueue(line);
                
                // Keep queue size manageable
                while (_logLines.Count > MaxLogLines)
                {
                    _logLines.TryDequeue(out _);
                }
            }
            
            // Trigger incremental analysis
            _ = Task.Run(async () => await RunIncrementalAnalysisAsync(e.FullPath, CancellationToken.None));
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Error handling file change for {FilePath}", e.FullPath);
        }
    }
    
    private async Task<IEnumerable<string>> ReadNewLinesAsync(string filePath)
    {
        try
        {
            var allLines = await File.ReadAllLinesAsync(filePath);
            var currentCount = _logLines.Count;
            return allLines.Skip(Math.Max(0, allLines.Length - 100)); // Get last 100 lines
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Failed to read new lines from {FilePath}", filePath);
            return Enumerable.Empty<string>();
        }
    }
    
    private async Task RunIncrementalAnalysisAsync(string logFile, CancellationToken cancellationToken)
    {
        try
        {
            if (_currentSession == null) return;
            
            var results = await _incrementalAnalyzer.AnalyzeIncrementalAsync(logFile, _logLines.ToArray(), cancellationToken);
            
            // Update session with new results
            await _sessionManager.UpdateSessionAsync(_currentSession.Id, results, cancellationToken);
            _currentSession.Results = results.ToList();
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Error during incremental analysis");
        }
    }
    
    private async Task UpdateDisplayLoopAsync(CancellationToken cancellationToken)
    {
        while (_isMonitoring && !cancellationToken.IsCancellationRequested)
        {
            try
            {
                lock (_layoutLock)
                {
                    UpdateLogPanel();
                    UpdateAnalysisPanel();
                    UpdateStatusPanel();
                }
                
                if (_layout != null)
                {
                    Console.Clear();
                    DrawHeader();
                    Console.Write(_layout);
                    DrawFooter();
                    DrawMonitoringFooter();
                }
                
                await Task.Delay(1000, cancellationToken); // Update every second
            }
            catch (OperationCanceledException)
            {
                break;
            }
            catch (Exception ex)
            {
                Logger.LogError(ex, "Error updating display");
                await Task.Delay(5000, cancellationToken); // Wait longer on error
            }
        }
    }
    
    private void UpdateLogPanel()
    {
        if (_layout == null) return;
        
        var lines = _logLines.TakeLast(TailLines).ToArray();
        var content = string.Join(Environment.NewLine, lines);
        
        if (string.IsNullOrEmpty(content))
        {
            content = "Waiting for log data...";
        }
        
        var scrollable = new Text(content);
        var logPanel = new Panel(scrollable)
        {
            Header = new PanelHeader("[yellow]Log Content (Live)[/]"),
            Border = BoxBorder.Rounded
        };
        
        _layout["Log"].Update(logPanel);
    }
    
    private void UpdateAnalysisPanel()
    {
        if (_layout == null || _currentSession?.Results == null) return;
        
        var results = _currentSession.Results;
        IRenderable content;
        
        if (!results.Any())
        {
            content = new Text("No analysis results yet...");
        }
        else
        {
            var tree = new Tree("Analysis Results");
            
            foreach (var result in results.Take(10)) // Show top 10 results
            {
                var node = tree.AddNode($"[{GetSeverityColor(result.Severity.ToString())}]{result.GetTitle()}[/]");
                var summary = result.Fragment?.GetSummary();
                if (!string.IsNullOrEmpty(summary))
                {
                    node.AddNode(new Text(summary).LeftJustified());
                }
            }
            content = tree;
        }
        
        var analysisPanel = new Panel(content)
        {
            Header = new PanelHeader("[cyan]Analysis Results[/]"),
            Border = BoxBorder.Rounded
        };
        
        _layout["Analysis"].Update(analysisPanel);
    }
    
    private void UpdateStatusPanel()
    {
        if (_layout == null) return;
        
        var statusPanel = new Panel(CreateStatusTable())
        {
            Header = new PanelHeader("[green]Monitor Status[/]"),
            Border = BoxBorder.Rounded
        };
        
        _layout["Status"].Update(statusPanel);
    }
    
    private Table CreateStatusTable()
    {
        var table = new Table()
            .Border(TableBorder.None)
            .HideHeaders()
            .AddColumn("Key")
            .AddColumn("Value");
        
        table.AddRow("Status", _isPaused ? "[yellow]Paused[/]" : "[green]Monitoring[/]");
        table.AddRow("Log Lines", _logLines.Count.ToString());
        table.AddRow("Session", _currentSession?.Id.ToString("N")[..8] ?? "None");
        table.AddRow("Start Time", _currentSession?.StartTime.ToString("HH:mm:ss") ?? "N/A");
        
        if (_currentSession?.Results != null)
        {
            table.AddRow("Results", _currentSession.Results.Count.ToString());
            table.AddRow("Last Analysis", DateTime.Now.ToString("HH:mm:ss"));
        }
        
        return table;
    }
    
    private async Task HandleKeyboardInputAsync(CancellationToken cancellationToken)
    {
        while (_isMonitoring && !cancellationToken.IsCancellationRequested)
        {
            var key = await WaitForKeyAsync("", timeout: 100, cancellationToken);
            if (key.HasValue)
            {
                switch (key.Value.Key)
                {
                    case ConsoleKey.Spacebar:
                        _isPaused = !_isPaused;
                        Logger.LogInformation("Monitoring {Status}", _isPaused ? "paused" : "resumed");
                        break;
                        
                    case ConsoleKey.Escape:
                    case ConsoleKey.Q:
                        _isMonitoring = false;
                        return;
                        
                    case ConsoleKey.S:
                        await SaveSessionAsync(cancellationToken);
                        break;
                        
                    case ConsoleKey.R:
                        // Force refresh
                        break;
                        
                    case ConsoleKey.F1:
                        ShowMonitoringHelp();
                        break;
                }
            }
        }
    }
    
    private async Task SaveSessionAsync(CancellationToken cancellationToken)
    {
        try
        {
            if (_currentSession == null) return;
            
            var fileName = $"session_{_currentSession.Id:N}_{DateTime.Now:yyyyMMdd_HHmmss}.json";
            var filePath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Desktop), fileName);
            
            var json = System.Text.Json.JsonSerializer.Serialize(_currentSession, new System.Text.Json.JsonSerializerOptions
            {
                WriteIndented = true
            });
            
            await File.WriteAllTextAsync(filePath, json, cancellationToken);
            Logger.LogInformation("Session saved to {FilePath}", filePath);
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Failed to save session");
        }
    }
    
    private void ShowMonitoringHelp()
    {
        var helpText = new Panel(new Markup("""
        [bold]Log Monitor Controls:[/]
        
        [yellow]SPACEBAR[/] - Pause/Resume monitoring
        [yellow]R[/] - Force refresh display
        [yellow]S[/] - Save current session
        [yellow]ESC/Q[/] - Exit monitor
        [yellow]F1[/] - Show this help
        """))
        {
            Header = new PanelHeader("[cyan]Help[/]"),
            Border = BoxBorder.Rounded
        };
        
        Console.Write(helpText);
    }
    
    private void DrawMonitoringFooter()
    {
        Console.MarkupLine("[dim]Monitor Controls: [yellow]SPACE[/] Pause/Resume • [yellow]S[/] Save • [yellow]R[/] Refresh • [yellow]ESC[/] Exit[/]");
    }
    
    private async Task StopMonitoringAsync()
    {
        _isMonitoring = false;
        _fileWatcher?.StopWatching();
        
        if (_currentSession != null)
        {
            try
            {
                await _sessionManager.UpdateSessionAsync(_currentSession.Id, _currentSession.Results, CancellationToken.None);
                Logger.LogInformation("Finalized monitoring session {SessionId}", _currentSession.Id);
            }
            catch (Exception ex)
            {
                Logger.LogError(ex, "Error finalizing session");
            }
        }
    }
    
    private static string GetSeverityColor(string severity) => severity.ToLowerInvariant() switch
    {
        "critical" => "red",
        "error" => "orange3", 
        "warning" => "yellow",
        "info" => "green",
        "none" => "blue",
        _ => "white"
    };
}