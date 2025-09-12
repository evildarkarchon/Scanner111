# Scanner111 CLI Terminal User Interface Implementation Plan

## Executive Summary
Transform the existing Scanner111 CLI application from a simple demo into a fully-featured terminal user interface using Spectre.Console. The TUI will provide an interactive, user-friendly experience for analyzing crash logs with real-time progress feedback, intuitive navigation, and rich visual presentation.

## Architecture Overview

### Core Components

```
Scanner111.CLI/
├── Program.cs                     # Entry point with DI setup
├── Commands/                      # Command-line argument handling
│   ├── AnalyzeCommand.cs         # Main analysis command
│   ├── ConfigCommand.cs          # Configuration management
│   └── InteractiveCommand.cs    # Launch interactive TUI mode
├── UI/                           # Spectre.Console UI components
│   ├── Screens/                  # Individual TUI screens
│   │   ├── MainMenuScreen.cs
│   │   ├── AnalysisScreen.cs
│   │   ├── ResultsScreen.cs
│   │   ├── ConfigurationScreen.cs
│   │   └── HelpScreen.cs
│   ├── Components/               # Reusable UI components
│   │   ├── LogFileSelector.cs
│   │   ├── AnalyzerSelector.cs
│   │   ├── ProgressDisplay.cs
│   │   └── ReportViewer.cs
│   └── Theme/                    # Visual styling
│       ├── ColorScheme.cs
│       └── LayoutManager.cs
├── Services/                     # CLI-specific services
│   ├── FileWatcher.cs           # Monitor log files for changes
│   ├── SessionManager.cs        # Manage analysis sessions
│   └── ExportService.cs         # Export reports in various formats
└── Configuration/               # CLI configuration
    └── CliSettings.cs           # User preferences
```

## Phase 1: Foundation (Week 1)

### 1.1 Project Setup & Command Structure
- **Refactor Program.cs** to support both command-line and interactive modes
- **Implement CommandLineParser integration** for argument handling
- **Setup dependency injection** with Microsoft.Extensions.DependencyInjection
- **Configure Serilog** for structured logging with file and console sinks

### 1.2 Base UI Framework
```csharp
public interface IScreen
{
    string Title { get; }
    Task<ScreenResult> DisplayAsync(CancellationToken cancellationToken);
}

public abstract class BaseScreen : IScreen
{
    protected readonly IAnsiConsole Console;
    protected readonly IServiceProvider Services;
    
    public abstract string Title { get; }
    
    protected BaseScreen(IAnsiConsole console, IServiceProvider services)
    {
        Console = console;
        Services = services;
    }
    
    public abstract Task<ScreenResult> DisplayAsync(CancellationToken cancellationToken);
    
    protected void DrawHeader()
    {
        Console.Clear();
        var rule = new Rule($"[bold yellow]{Title}[/]")
            .RuleStyle("cyan")
            .LeftJustified();
        Console.Write(rule);
    }
}
```

### 1.3 Navigation System
```csharp
public class NavigationService
{
    private readonly Stack<IScreen> _screenStack = new();
    private readonly IAnsiConsole _console;
    
    public async Task NavigateToAsync<TScreen>() where TScreen : IScreen
    {
        var screen = _serviceProvider.GetRequiredService<TScreen>();
        _screenStack.Push(screen);
        await screen.DisplayAsync(_cancellationToken);
    }
    
    public async Task GoBackAsync()
    {
        if (_screenStack.Count > 1)
        {
            _screenStack.Pop();
            await _screenStack.Peek().DisplayAsync(_cancellationToken);
        }
    }
}
```

## Phase 2: Core Screens (Week 2)

### 2.1 Main Menu Screen
```csharp
public class MainMenuScreen : BaseScreen
{
    public override string Title => "Scanner111 - Crash Log Analyzer";
    
    public override async Task<ScreenResult> DisplayAsync(CancellationToken cancellationToken)
    {
        DrawHeader();
        
        var choice = AnsiConsole.Prompt(
            new SelectionPrompt<MenuOption>()
                .Title("[green]Select an option:[/]")
                .PageSize(10)
                .AddChoices(new[]
                {
                    new MenuOption("🔍 Analyze Crash Log", MenuAction.Analyze),
                    new MenuOption("📊 View Recent Results", MenuAction.ViewResults),
                    new MenuOption("⚙️ Configuration", MenuAction.Configure),
                    new MenuOption("📚 Help & Documentation", MenuAction.Help),
                    new MenuOption("🚪 Exit", MenuAction.Exit)
                })
                .UseConverter(option => option.Display));
        
        return new ScreenResult { NextAction = choice.Action };
    }
}
```

### 2.2 Analysis Screen with Live Progress
```csharp
public class AnalysisScreen : BaseScreen
{
    private readonly IAnalyzerOrchestrator _orchestrator;
    
    public override async Task<ScreenResult> DisplayAsync(CancellationToken cancellationToken)
    {
        DrawHeader();
        
        // File selection
        var logFile = await SelectLogFileAsync();
        if (logFile == null) return ScreenResult.Back;
        
        // Analyzer selection
        var analyzers = await SelectAnalyzersAsync();
        if (!analyzers.Any()) return ScreenResult.Back;
        
        // Run analysis with progress
        var results = await RunAnalysisWithProgressAsync(logFile, analyzers, cancellationToken);
        
        return new ScreenResult 
        { 
            NextAction = MenuAction.ViewResults,
            Data = results
        };
    }
    
    private async Task<IEnumerable<AnalysisResult>> RunAnalysisWithProgressAsync(
        string logFile, 
        IEnumerable<IAnalyzer> analyzers,
        CancellationToken cancellationToken)
    {
        return await AnsiConsole.Progress()
            .AutoClear(false)
            .Columns(new ProgressColumn[]
            {
                new TaskDescriptionColumn(),
                new ProgressBarColumn(),
                new PercentageColumn(),
                new RemainingTimeColumn(),
                new SpinnerColumn(),
            })
            .StartAsync(async ctx =>
            {
                var task = ctx.AddTask("[green]Analyzing log file[/]", maxValue: analyzers.Count());
                var results = new List<AnalysisResult>();
                
                foreach (var analyzer in analyzers)
                {
                    task.Description = $"[yellow]Running {analyzer.Name}[/]";
                    var result = await analyzer.AnalyzeAsync(context, cancellationToken);
                    results.Add(result);
                    task.Increment(1);
                }
                
                return results;
            });
    }
}
```

### 2.3 Interactive Results Viewer
```csharp
public class ResultsScreen : BaseScreen
{
    public override async Task<ScreenResult> DisplayAsync(CancellationToken cancellationToken)
    {
        DrawHeader();
        
        // Create tree view of results
        var tree = new Tree("[yellow]Analysis Results[/]")
            .Style(Style.Parse("cyan"));
        
        foreach (var result in _results.OrderBy(r => r.Severity))
        {
            var severityColor = GetSeverityColor(result.Severity);
            var node = tree.AddNode($"[{severityColor}]{result.AnalyzerName}[/]");
            
            if (result.Fragment != null)
            {
                AddFragmentToNode(node, result.Fragment);
            }
        }
        
        AnsiConsole.Write(tree);
        
        // Export options
        var action = AnsiConsole.Prompt(
            new SelectionPrompt<string>()
                .Title("What would you like to do?")
                .AddChoices("Export as Markdown", "Export as HTML", "Export as JSON", "Back"));
        
        if (action.StartsWith("Export"))
        {
            await ExportResultsAsync(action);
        }
        
        return ScreenResult.Back;
    }
}
```

## Phase 3: Advanced Features (Week 3)

### 3.1 Real-time Log Monitoring
```csharp
public class LogMonitorScreen : BaseScreen
{
    private readonly FileSystemWatcher _watcher;
    
    public override async Task<ScreenResult> DisplayAsync(CancellationToken cancellationToken)
    {
        DrawHeader();
        
        var layout = new Layout("Root")
            .SplitRows(
                new Layout("Header").Size(3),
                new Layout("Body").SplitColumns(
                    new Layout("LogContent"),
                    new Layout("Analysis").Size(40)),
                new Layout("Footer").Size(3));
        
        // Setup file watcher
        _watcher.Changed += async (sender, e) => 
        {
            await UpdateLogDisplayAsync(e.FullPath);
            await RunIncrementalAnalysisAsync(e.FullPath);
        };
        
        // Live update display
        await AnsiConsole.Live(layout)
            .StartAsync(async ctx =>
            {
                while (!cancellationToken.IsCancellationRequested)
                {
                    ctx.Refresh();
                    await Task.Delay(100, cancellationToken);
                }
            });
        
        return ScreenResult.Back;
    }
}
```

### 3.2 Configuration Management UI
```csharp
public class ConfigurationScreen : BaseScreen
{
    public override async Task<ScreenResult> DisplayAsync(CancellationToken cancellationToken)
    {
        DrawHeader();
        
        var settings = await LoadSettingsAsync();
        
        var form = new Form<CliSettings>()
            .AddField("Default Game", settings.DefaultGame, GameType.GetValues())
            .AddField("Auto-detect paths", settings.AutoDetectPaths)
            .AddField("Max parallel analyzers", settings.MaxParallelAnalyzers, 1, 10)
            .AddField("Report format", settings.DefaultReportFormat)
            .AddField("Theme", settings.Theme, new[] { "Default", "Dark", "Light", "High Contrast" })
            .AddField("Show timestamps", settings.ShowTimestamps)
            .AddField("Verbose output", settings.VerboseOutput);
        
        var updatedSettings = AnsiConsole.Prompt(form);
        await SaveSettingsAsync(updatedSettings);
        
        AnsiConsole.MarkupLine("[green]Settings saved successfully![/]");
        await Task.Delay(1500);
        
        return ScreenResult.Back;
    }
}
```

### 3.3 Interactive Analyzer Selection
```csharp
public class AnalyzerSelectorComponent
{
    public async Task<IEnumerable<IAnalyzer>> SelectAnalyzersAsync()
    {
        var availableAnalyzers = _analyzerRegistry.GetAll();
        
        var selected = AnsiConsole.Prompt(
            new MultiSelectionPrompt<AnalyzerInfo>()
                .Title("Select analyzers to run:")
                .Required()
                .PageSize(10)
                .MoreChoicesText("[grey](Move up and down to reveal more analyzers)[/]")
                .InstructionsText(
                    "[grey](Press [blue]<space>[/] to toggle an analyzer, " +
                    "[green]<enter>[/] to accept)[/]")
                .AddChoiceGroup(
                    new AnalyzerInfo("All Analyzers", "Run all available analyzers"),
                    availableAnalyzers.Select(a => new AnalyzerInfo(a.Name, a.Description)))
                .UseConverter(a => $"{GetIcon(a.Category)} {a.Name}"));
        
        return selected.Select(info => _analyzerRegistry.Get(info.Name));
    }
}
```

## Phase 4: Polish & Enhancement (Week 4)

### 4.1 Rich Data Visualization
```csharp
public class StatisticsDisplay
{
    public void ShowAnalysisStatistics(IEnumerable<AnalysisResult> results)
    {
        // Severity distribution chart
        var chart = new BarChart()
            .Width(60)
            .Label("[green bold]Severity Distribution[/]")
            .CenterLabel();
        
        var severityGroups = results.GroupBy(r => r.Severity);
        foreach (var group in severityGroups)
        {
            chart.AddItem(group.Key.ToString(), group.Count(), GetSeverityColor(group.Key));
        }
        
        AnsiConsole.Write(chart);
        
        // Performance metrics table
        var table = new Table()
            .Border(TableBorder.Rounded)
            .AddColumn("Analyzer")
            .AddColumn("Duration")
            .AddColumn("Status");
        
        foreach (var result in results.OrderBy(r => r.Duration))
        {
            table.AddRow(
                result.AnalyzerName,
                $"{result.Duration.TotalMilliseconds:F0}ms",
                result.Success ? "[green]✓[/]" : "[red]✗[/]");
        }
        
        AnsiConsole.Write(table);
    }
}
```

### 4.2 Keyboard Shortcuts & Navigation
```csharp
public class KeyboardHandler
{
    private readonly Dictionary<ConsoleKey, Action> _shortcuts = new()
    {
        { ConsoleKey.F1, ShowHelp },
        { ConsoleKey.F5, RefreshScreen },
        { ConsoleKey.Escape, GoBack },
        { ConsoleKey.S, QuickSave },
        { ConsoleKey.O, OpenFile },
        { ConsoleKey.Q, ConfirmExit }
    };
    
    public async Task HandleKeyPressAsync(ConsoleKeyInfo key)
    {
        if (_shortcuts.TryGetValue(key.Key, out var action))
        {
            await Task.Run(action);
        }
    }
}
```

### 4.3 Session Management
```csharp
public class SessionManager
{
    public async Task<Session> CreateSessionAsync(string logFile)
    {
        var session = new Session
        {
            Id = Guid.NewGuid(),
            LogFile = logFile,
            StartTime = DateTime.UtcNow,
            Results = new List<AnalysisResult>()
        };
        
        await SaveSessionAsync(session);
        return session;
    }
    
    public async Task<IEnumerable<Session>> GetRecentSessionsAsync(int count = 10)
    {
        var sessionFiles = Directory.GetFiles(_sessionPath, "*.session")
            .OrderByDescending(File.GetLastWriteTimeUtc)
            .Take(count);
        
        var sessions = new List<Session>();
        foreach (var file in sessionFiles)
        {
            sessions.Add(await LoadSessionAsync(file));
        }
        
        return sessions;
    }
}
```

## Phase 5: Integration & Testing

### 5.1 Command-Line Interface
```csharp
[Verb("analyze", HelpText = "Analyze a crash log file")]
public class AnalyzeOptions
{
    [Option('f', "file", Required = true, HelpText = "Path to the crash log file")]
    public string LogFile { get; set; }
    
    [Option('a', "analyzers", Separator = ',', HelpText = "Comma-separated list of analyzers")]
    public IEnumerable<string> Analyzers { get; set; }
    
    [Option('o', "output", HelpText = "Output file path")]
    public string OutputFile { get; set; }
    
    [Option('F', "format", Default = ReportFormat.Markdown, HelpText = "Output format")]
    public ReportFormat Format { get; set; }
    
    [Option('i', "interactive", Default = false, HelpText = "Launch interactive mode")]
    public bool Interactive { get; set; }
}
```

### 5.2 Error Handling & Recovery
```csharp
public class ErrorHandler
{
    public async Task<T> ExecuteWithRecoveryAsync<T>(
        Func<Task<T>> operation,
        string operationName)
    {
        try
        {
            return await operation();
        }
        catch (FileNotFoundException ex)
        {
            ShowError($"File not found: {ex.FileName}");
            return await PromptForRetryAsync(operation, operationName);
        }
        catch (UnauthorizedAccessException ex)
        {
            ShowError($"Access denied: {ex.Message}");
            return default;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unexpected error in {Operation}", operationName);
            
            var panel = new Panel(new Text(ex.Message))
                .Header("[red]Error[/]")
                .BorderStyle(new Style(Color.Red));
            
            AnsiConsole.Write(panel);
            
            if (AnsiConsole.Confirm("Would you like to report this issue?"))
            {
                await ReportIssueAsync(ex);
            }
            
            return default;
        }
    }
}
```

## Implementation Timeline

### Week 1: Foundation
- Day 1-2: Setup project structure, DI, and base classes
- Day 3-4: Implement navigation system and main menu
- Day 5: Create basic analysis screen with file selection

### Week 2: Core Features
- Day 1-2: Complete analysis screen with progress display
- Day 3-4: Implement results viewer with tree visualization
- Day 5: Add export functionality

### Week 3: Advanced Features
- Day 1-2: Add real-time log monitoring
- Day 3: Create configuration UI
- Day 4-5: Implement session management

### Week 4: Polish
- Day 1-2: Add keyboard shortcuts and improve navigation
- Day 3: Implement data visualization components
- Day 4-5: Testing and bug fixes

## Testing Strategy

### Unit Tests
- Test individual UI components in isolation
- Mock Spectre.Console interfaces for testing
- Verify navigation flow logic

### Integration Tests
- Test full screen workflows
- Verify analyzer integration
- Test file I/O operations

### Manual Testing Scenarios
1. **Happy Path**: Select file → Choose analyzers → View results → Export
2. **Error Recovery**: Invalid file → Retry → Success
3. **Performance**: Large log files (>100MB)
4. **Accessibility**: Keyboard-only navigation
5. **Cross-platform**: Windows, Linux, macOS

## Key Features

### User Experience
- **Rich Visual Feedback**: Progress bars, spinners, and status indicators
- **Intuitive Navigation**: Breadcrumbs, keyboard shortcuts, and consistent menu structure
- **Responsive Design**: Adapts to terminal size
- **Themed Interface**: Multiple color schemes including high contrast

### Functionality
- **Batch Processing**: Analyze multiple files in sequence
- **Watch Mode**: Monitor logs in real-time
- **Session History**: Resume previous analyses
- **Export Options**: Markdown, HTML, JSON, CSV
- **Configurable**: User preferences and analyzer settings

### Performance
- **Async Operations**: Non-blocking UI updates
- **Progress Tracking**: Real-time analysis progress
- **Cancellation**: Graceful operation cancellation
- **Memory Efficient**: Stream large files

## Dependencies

### Required NuGet Packages
- `Spectre.Console` (0.50.0) - Terminal UI framework
- `CommandLineParser` (2.9.1) - Command-line argument parsing
- `Serilog` (4.3.0) - Structured logging
- `Serilog.Sinks.Console` (6.0.0) - Console output
- `Serilog.Sinks.File` (7.0.0) - File logging

### Core Project References
- `Scanner111.Core` - Analysis engine and services

## Success Metrics

### Performance Targets
- Startup time: <500ms
- File loading: <2s for 100MB files
- Analysis completion: <10s for typical logs
- UI responsiveness: <100ms for user actions

### Quality Metrics
- Code coverage: >80%
- No blocking UI operations
- Graceful error handling
- Comprehensive help documentation

## Future Enhancements

### Phase 6: Advanced Features (Post-MVP)
- Plugin system for custom analyzers
- Remote log analysis via SSH/HTTP
- Comparison mode for multiple logs
- AI-powered issue recommendations
- Integration with issue tracking systems
- Automated report scheduling
- Multi-language support

### Phase 7: Cloud Integration
- Cloud storage support (Azure, AWS, GCP)
- Web dashboard companion
- Team collaboration features
- Analysis history synchronization
- Distributed analysis for large datasets

## Conclusion

This implementation plan transforms Scanner111's CLI into a professional-grade terminal application that balances power with usability. The phased approach ensures steady progress while maintaining code quality and test coverage throughout development.

The resulting TUI will provide users with an intuitive, efficient, and visually appealing interface for crash log analysis, setting a new standard for terminal-based diagnostic tools.