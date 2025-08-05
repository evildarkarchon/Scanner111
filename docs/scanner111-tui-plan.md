# Scanner111.CLI Terminal UI Implementation Plan

## Executive Summary

This plan outlines the transformation of Scanner111.CLI from a basic console application with problematic progress bars to a full-featured Terminal User Interface (TUI) application. The implementation will use Spectre.Console for rich terminal capabilities and provide both interactive and non-interactive modes.

## Current Issues

1. **Progress Bar Problems**
   - Messages interrupt progress bars, forcing redraw on new lines
   - No proper coordination between progress updates and message output
   - Basic `\r` carriage return approach doesn't handle terminal resizing
   - No support for multiple concurrent progress bars

2. **No Interactive Mode**
   - CLI only works with command-line arguments
   - No menu system for users who launch without parameters
   - No real-time monitoring capabilities

3. **Limited Visual Feedback**
   - Basic colored text output
   - No status panels or live updates
   - No visual hierarchy for different types of information

## Proposed Solution: Spectre.Console Integration

### Why Spectre.Console?

- **Rich Terminal UI**: Provides tables, panels, progress bars, and live displays
- **Cross-platform**: Works on Windows, Linux, and macOS
- **Thread-safe**: Built-in support for concurrent updates
- **Modern API**: Clean, fluent API design
- **Active Development**: Well-maintained with regular updates

### Alternative Considered: Terminal.Gui

While Terminal.Gui offers full TUI capabilities with windows and dialogs, it's more heavyweight than needed for this use case. Spectre.Console provides the right balance of features and simplicity.

## Implementation Phases

### Phase 1: Core Infrastructure (Week 1)

#### 1.1 Create Terminal UI Service
```csharp
// Scanner111.CLI/Services/ITerminalUIService.cs
public interface ITerminalUIService
{
    void ShowInteractiveMenu();
    IProgressContext CreateProgressContext(string title, int totalItems);
    void DisplayResults(ScanResult results);
    void ShowLiveStatus(string status);
    Task<T> PromptAsync<T>(string prompt, T defaultValue = default);
}
```

#### 1.2 Update Message Handler
Create a new `SpectreMessageHandler` that implements `IMessageHandler` using Spectre.Console's rendering capabilities:

```csharp
// Scanner111.CLI/Services/SpectreMessageHandler.cs
public class SpectreMessageHandler : IMessageHandler
{
    private readonly Layout _layout;
    private readonly Table _logTable;
    private readonly Live _liveDisplay;
    
    public void MsgInfo(string message, string? details = null)
    {
        _logTable.AddRow(
            new Markup($"[blue]ℹ[/]"),
            new Text(DateTime.Now.ToString("HH:mm:ss")),
            new Text(message)
        );
    }
}
```

### Phase 2: Interactive Mode (Week 1-2)

#### 2.1 Main Menu System
When launched without arguments, display an interactive menu:

```
┌─────────────────────────────────────────────────────┐
│          Scanner111 - Crash Log Analyzer            │
├─────────────────────────────────────────────────────┤
│                                                     │
│  [1] Quick Scan (Current Directory)                 │
│  [2] Scan Specific File/Directory                   │
│  [3] FCX Mode - File Integrity Check               │
│  [4] Configuration Settings                         │
│  [5] View Recent Scan Results                       │
│  [6] About Scanner111                               │
│                                                     │
│  [Q] Quit                                           │
│                                                     │
└─────────────────────────────────────────────────────┘
```

#### 2.2 Interactive Prompts
Use Spectre.Console's prompt system for user input:

```csharp
var scanPath = AnsiConsole.Ask<string>("Enter path to scan:");
var options = AnsiConsole.Prompt(
    new MultiSelectionPrompt<string>()
        .Title("Select scan options:")
        .AddChoices(new[] {
            "Simplify Logs",
            "Move Unsolved Logs",
            "FCX Mode",
            "Verbose Output"
        }));
```

### Phase 3: Enhanced Progress Display (Week 2)

#### 3.1 Multi-Progress Support
Implement concurrent progress tracking for different operations:

```
┌─────────────────────────────────────────────────────┐
│                  Scan Progress                       │
├─────────────────────────────────────────────────────┤
│                                                     │
│  File Collection    [████████░░] 80% (1234/1543)   │
│  Analysis           [██░░░░░░░░] 20% (247/1234)    │
│  Report Generation  [░░░░░░░░░░] 0%  (0/247)       │
│                                                     │
│  Current: crash-2024-01-15-12-34-56.log            │
│  Status: Analyzing stack frames...                  │
│                                                     │
└─────────────────────────────────────────────────────┘
```

#### 3.2 Live Updates Panel
Create a split-screen layout with progress and live log messages:

```csharp
var layout = new Layout()
    .SplitRows(
        new Layout("header").Size(3),
        new Layout("body").SplitColumns(
            new Layout("progress").Size(60),
            new Layout("logs").Size(40)
        ),
        new Layout("footer").Size(3)
    );
```

### Phase 4: Real-time Monitoring (Week 2-3)

#### 4.1 Watch Mode
Implement a file system watcher for continuous monitoring:

```csharp
public class WatchCommand : ICommand<WatchOptions>
{
    public async Task<int> ExecuteAsync(WatchOptions options)
    {
        await AnsiConsole.Live(_statusPanel)
            .StartAsync(async ctx =>
            {
                using var watcher = new FileSystemWatcher(options.Path);
                watcher.Created += OnNewCrashLog;
                // Auto-scan new crash logs as they appear
            });
    }
}
```

#### 4.2 Dashboard View
Create a real-time dashboard showing:
- Recent crash logs
- Analysis statistics
- Common issues found
- System status

### Phase 5: Enhanced Features (Week 3)

#### 5.1 Result Browser
Interactive result viewer with:
- Searchable log entries
- Expandable/collapsible sections
- Syntax highlighting for stack traces
- Quick navigation between findings

#### 5.2 Configuration Editor
Visual configuration editor:
```
┌─── Configuration Settings ──────────────────────────┐
│                                                     │
│  General Settings:                                  │
│  ├─ [✓] FCX Mode                                   │
│  ├─ [✓] Simplify Logs                              │
│  ├─ [ ] Move Unsolved Logs                         │
│  └─ [✓] Audio Notifications                        │
│                                                     │
│  Paths:                                             │
│  ├─ Crash Logs: C:\Users\...\Documents\My Games    │
│  └─ MODS Folder: C:\Games\Fallout4\Data           │
│                                                     │
│  [Save] [Cancel] [Reset to Defaults]               │
│                                                     │
└─────────────────────────────────────────────────────┘
```

## Implementation Details

### Program.cs Modifications
```csharp
// Check if running in interactive mode
if (args.Length == 0 && Environment.UserInteractive)
{
    var uiService = serviceProvider.GetRequiredService<ITerminalUIService>();
    return await uiService.RunInteractiveMode();
}

// Otherwise, use existing command-line parser
var result = parser.ParseArguments<ScanOptions, ...>(args);
```

### Progress Context Implementation
```csharp
public class SpectreProgressContext : IProgressContext
{
    private readonly ProgressTask _task;
    
    public void Update(int current, string message)
    {
        _task.Value = current;
        _task.Description = message;
    }
}
```

### Thread Safety
Ensure all UI updates happen on the main thread:
```csharp
public class ThreadSafeUIService : ITerminalUIService
{
    private readonly Channel<UIUpdate> _updateChannel;
    
    public async Task RunAsync()
    {
        await foreach (var update in _updateChannel.Reader.ReadAllAsync())
        {
            // Apply UI updates on main thread
        }
    }
}
```

## Migration Strategy

1. **Maintain Backward Compatibility**
   - Keep existing command-line interface working
   - Add `--no-ui` flag to force simple output
   - Detect non-interactive environments (CI/CD)

2. **Gradual Rollout**
   - Start with basic Spectre.Console integration
   - Add interactive mode as opt-in feature
   - Gather user feedback before making default

3. **Testing Approach**
   - Unit tests for UI service logic
   - Integration tests with mock console
   - Manual testing on Windows, Linux, macOS

## Benefits

1. **Improved User Experience**
   - No more progress bar interruption issues
   - Clear visual hierarchy of information
   - Interactive exploration of results

2. **Better Performance Monitoring**
   - Real-time progress for long operations
   - Concurrent operation tracking
   - Memory and CPU usage display

3. **Enhanced Productivity**
   - Quick access to common operations
   - Visual configuration management
   - Integrated help system

## Timeline

- **Week 1**: Core infrastructure and basic Spectre.Console integration
- **Week 2**: Interactive mode and enhanced progress display
- **Week 3**: Real-time monitoring and advanced features
- **Week 4**: Testing, documentation, and refinement

## Example Code Structure

```
Scanner111.CLI/
├── Services/
│   ├── ITerminalUIService.cs
│   ├── SpectreTerminalUIService.cs
│   └── SpectreProgressContext.cs
├── UI/
│   ├── Components/
│   │   ├── MenuComponent.cs
│   │   ├── ProgressPanel.cs
│   │   └── ResultBrowser.cs
│   ├── Layouts/
│   │   ├── MainLayout.cs
│   │   └── DashboardLayout.cs
│   └── Themes/
│       └── Scanner111Theme.cs
└── Commands/
    ├── InteractiveCommand.cs
    └── WatchCommand.cs
```

## Conclusion

This implementation plan transforms Scanner111.CLI into a modern, user-friendly terminal application while maintaining its core functionality. The phased approach ensures steady progress with minimal disruption to existing users.