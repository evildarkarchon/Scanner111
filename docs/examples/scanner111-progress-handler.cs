using Spectre.Console;
using Scanner111.Core.Infrastructure;
using Scanner111.Core.Models;
using System.Collections.Concurrent;

namespace Scanner111.Core.Infrastructure;

/// <summary>
/// Message handler using Spectre.Console for rich terminal output with proper progress management
/// </summary>
public class SpectreMessageHandler : IMessageHandler
{
    private readonly bool _useColors;
    private readonly ConcurrentDictionary<string, ProgressTask> _activeTasks = new();
    private readonly object _consoleLock = new();
    private Live? _liveContext;
    private Table? _messageTable;
    private Layout? _layout;
    private readonly Queue<(DateTime timestamp, string prefix, string message, Color color)> _messageQueue = new();
    private const int MaxMessages = 20;

    public SpectreMessageHandler(bool useColors = true)
    {
        _useColors = useColors;
        InitializeLayout();
    }

    private void InitializeLayout()
    {
        _layout = new Layout()
            .SplitRows(
                new Layout("messages").Ratio(3),
                new Layout("progress").Ratio(1)
            );

        _messageTable = new Table()
            .Border(TableBorder.None)
            .HideHeaders()
            .AddColumn("Icon", c => c.Width(3))
            .AddColumn("Time", c => c.Width(10))
            .AddColumn("Message");
    }

    public void MsgInfo(string message, string? details = null)
    {
        WriteMessage("ℹ", message, Color.Blue, details);
    }

    public void MsgSuccess(string message, string? details = null)
    {
        WriteMessage("✓", message, Color.Green, details);
    }

    public void MsgWarning(string message, string? details = null)
    {
        WriteMessage("⚠", message, Color.Yellow, details);
    }

    public void MsgError(string message, string? details = null)
    {
        WriteMessage("✗", message, Color.Red, details);
    }

    public void MsgDebug(string message, string? details = null)
    {
        if (!System.Diagnostics.Debugger.IsAttached) return;
        WriteMessage("🐛", message, Color.Grey, details);
    }

    public void MsgCritical(string message, string? details = null)
    {
        WriteMessage("🚨", message, Color.Red, details);
    }

    /// <summary>
    /// Create a managed progress context that integrates with Spectre.Console
    /// </summary>
    public IProgressContext CreateProgressContext(string title, int totalItems)
    {
        return new SpectreProgressContext(this, title, totalItems);
    }

    /// <summary>
    /// Start a live display context for real-time updates
    /// </summary>
    public async Task WithLiveDisplay(Func<Task> action)
    {
        await AnsiConsole.Live(_layout!)
            .AutoClear(false)
            .StartAsync(async ctx =>
            {
                _liveContext = ctx;
                UpdateDisplay();
                await action();
                _liveContext = null;
            });
    }

    private void WriteMessage(string icon, string message, Color color, string? details)
    {
        lock (_consoleLock)
        {
            var timestamp = DateTime.Now;
            
            // Add to message queue
            _messageQueue.Enqueue((timestamp, icon, message, color));
            
            // Keep only the last N messages
            while (_messageQueue.Count > MaxMessages)
            {
                _messageQueue.Dequeue();
            }

            // If we have a live context, update it
            if (_liveContext != null)
            {
                UpdateDisplay();
            }
            else
            {
                // Fallback to direct console output
                if (_useColors)
                {
                    AnsiConsole.MarkupLine($"[grey]{timestamp:HH:mm:ss}[/] [{color}]{icon}[/] {Markup.Escape(message)}");
                }
                else
                {
                    Console.WriteLine($"{timestamp:HH:mm:ss} {icon} {message}");
                }
                
                if (!string.IsNullOrEmpty(details))
                {
                    AnsiConsole.MarkupLine($"[grey]         {Markup.Escape(details)}[/]");
                }
            }
        }
    }

    private void UpdateDisplay()
    {
        if (_messageTable == null || _layout == null) return;

        // Clear and rebuild message table
        _messageTable.Rows.Clear();
        
        foreach (var (timestamp, prefix, message, color) in _messageQueue)
        {
            _messageTable.AddRow(
                new Markup($"[{color}]{prefix}[/]"),
                new Markup($"[grey]{timestamp:HH:mm:ss}[/]"),
                new Text(message)
            );
        }

        _layout["messages"].Update(new Panel(_messageTable)
            .Header("[yellow]Messages[/]")
            .Border(BoxBorder.Rounded));

        // Update progress section
        var progressPanel = BuildProgressPanel();
        _layout["progress"].Update(progressPanel);

        _liveContext?.Refresh();
    }

    private Panel BuildProgressPanel()
    {
        var progressGrid = new Grid();
        progressGrid.AddColumn();

        if (_activeTasks.Any())
        {
            foreach (var task in _activeTasks.Values)
            {
                var bar = new ProgressBar()
                    .Value(task.Value)
                    .MaxValue(task.MaxValue);
                    
                var row = new Grid();
                row.AddColumn(new GridColumn().Width(20));
                row.AddColumn();
                row.AddRow(
                    new Text(task.Description),
                    bar
                );
                
                progressGrid.AddRow(row);
            }
        }
        else
        {
            progressGrid.AddRow(new Text("No active operations", new Style(Color.Grey)));
        }

        return new Panel(progressGrid)
            .Header("[blue]Progress[/]")
            .Border(BoxBorder.Rounded);
    }

    internal void RegisterProgressTask(string id, ProgressTask task)
    {
        _activeTasks[id] = task;
        UpdateDisplay();
    }

    internal void UnregisterProgressTask(string id)
    {
        _activeTasks.TryRemove(id, out _);
        UpdateDisplay();
    }
}

/// <summary>
/// Progress context that integrates with Spectre.Console's progress system
/// </summary>
public class SpectreProgressContext : IProgressContext
{
    private readonly SpectreMessageHandler _handler;
    private readonly string _id;
    private readonly string _title;
    private readonly int _totalItems;
    private ProgressTask? _task;
    private bool _disposed;

    public SpectreProgressContext(SpectreMessageHandler handler, string title, int totalItems)
    {
        _handler = handler;
        _id = Guid.NewGuid().ToString();
        _title = title;
        _totalItems = totalItems;
        
        // In a real implementation, this would create a ProgressTask
        // For now, we'll simulate it
        _task = new ProgressTask(1, title, totalItems);
        _handler.RegisterProgressTask(_id, _task);
    }

    public void Update(int current, string message)
    {
        if (_disposed) return;
        
        _task!.Value = current;
        _task.Description = $"{_title}: {message}";
        
        // Calculate percentage
        var percentage = _totalItems > 0 ? (double)current / _totalItems * 100 : 0;
        
        // If we're at 100%, automatically complete
        if (current >= _totalItems && _totalItems > 0)
        {
            _task.Value = _totalItems;
        }
    }

    public void Dispose()
    {
        if (_disposed) return;
        
        _disposed = true;
        _handler.UnregisterProgressTask(_id);
        _task = null;
    }
}

/// <summary>
/// Example of how scanning would work with the new system
/// </summary>
public class ScannerWithSpectreUI
{
    private readonly SpectreMessageHandler _messageHandler;
    
    public async Task PerformScan(string directory)
    {
        await _messageHandler.WithLiveDisplay(async () =>
        {
            _messageHandler.MsgInfo($"Starting scan of directory: {directory}");
            
            // Collect files
            using (var collectProgress = _messageHandler.CreateProgressContext("Collecting files", 100))
            {
                for (int i = 0; i <= 100; i += 10)
                {
                    collectProgress.Update(i, $"Scanning folder {i / 10 + 1} of 10");
                    await Task.Delay(200);
                    
                    // Messages don't interrupt progress!
                    if (i == 50)
                    {
                        _messageHandler.MsgWarning("Large number of files detected");
                    }
                }
            }
            
            _messageHandler.MsgSuccess("File collection complete! Found 1,234 crash logs");
            
            // Analyze files
            using (var analyzeProgress = _messageHandler.CreateProgressContext("Analyzing crash logs", 1234))
            {
                for (int i = 0; i <= 1234; i += 50)
                {
                    analyzeProgress.Update(i, $"crash-2024-{i:D4}.log");
                    await Task.Delay(100);
                    
                    // Simulate finding issues
                    if (i % 200 == 0 && i > 0)
                    {
                        _messageHandler.MsgError($"Critical issue found in log #{i}");
                    }
                }
            }
            
            _messageHandler.MsgSuccess("Analysis complete!");
            
            // Generate reports
            using (var reportProgress = _messageHandler.CreateProgressContext("Generating reports", 50))
            {
                for (int i = 0; i <= 50; i += 5)
                {
                    reportProgress.Update(i, $"Report {i / 5 + 1} of 10");
                    await Task.Delay(150);
                }
            }
            
            _messageHandler.MsgInfo("All operations completed successfully");
        });
    }
}

/// <summary>
/// Integration example for Program.cs
/// </summary>
public static class ProgramIntegration
{
    public static IServiceCollection AddSpectreUI(this IServiceCollection services)
    {
        // Register the new message handler
        services.AddSingleton<IMessageHandler>(provider =>
        {
            var settings = provider.GetRequiredService<IApplicationSettingsService>()
                .LoadSettingsAsync().Result;
                
            return new SpectreMessageHandler(!settings.DisableColors);
        });
        
        // Register terminal UI service
        services.AddSingleton<ITerminalUIService, SpectreTerminalUIService>();
        
        return services;
    }
    
    public static async Task<int> RunWithUI(string[] args, IServiceProvider services)
    {
        // Check if we should run in interactive mode
        if (args.Length == 0 && 
            Environment.UserInteractive && 
            !Console.IsInputRedirected &&
            !Console.IsOutputRedirected)
        {
            var uiService = services.GetRequiredService<ITerminalUIService>();
            return await uiService.RunInteractiveMode();
        }
        
        // Otherwise use command line parser as normal
        return await RunCommandLine(args, services);
    }
    
    private static Task<int> RunCommandLine(string[] args, IServiceProvider services)
    {
        // Existing command line parsing logic
        return Task.FromResult(0);
    }
}