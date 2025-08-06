using System.Collections.Concurrent;
using Spectre.Console;
using Scanner111.Core.Infrastructure;

namespace Scanner111.CLI.Services;

public class SpectreMessageHandler : IMessageHandler
{
    private readonly Layout _layout;
    private readonly Table _logTable;
    private readonly ConcurrentQueue<(DateTime timestamp, string message, Markup markup)> _messages;
    private readonly object _updateLock = new();

    public SpectreMessageHandler()
    {
        _messages = new ConcurrentQueue<(DateTime, string, Markup)>();
        
        _logTable = new Table()
            .Border(TableBorder.None)
            .HideHeaders()
            .AddColumn(new TableColumn("Icon").Width(3))
            .AddColumn(new TableColumn("Time").Width(10))
            .AddColumn(new TableColumn("Message"));

        _layout = new Layout()
            .SplitRows(
                new Layout("messages").Ratio(1)
            );
    }

    public void ShowInfo(string message, MessageTarget target = MessageTarget.All)
    {
        if (target == MessageTarget.GuiOnly) return;
        
        lock (_updateLock)
        {
            var markup = new Markup($"[blue]{Markup.Escape(message)}[/]");
            AddMessage("ℹ", message, markup, "blue");
        }
    }

    public void ShowWarning(string message, MessageTarget target = MessageTarget.All)
    {
        if (target == MessageTarget.GuiOnly) return;
        
        lock (_updateLock)
        {
            var markup = new Markup($"[yellow]{Markup.Escape(message)}[/]");
            AddMessage("⚠", message, markup, "yellow");
        }
    }

    public void ShowError(string message, MessageTarget target = MessageTarget.All)
    {
        if (target == MessageTarget.GuiOnly) return;
        
        lock (_updateLock)
        {
            var markup = new Markup($"[red]{Markup.Escape(message)}[/]");
            AddMessage("✗", message, markup, "red");
        }
    }

    public void ShowSuccess(string message, MessageTarget target = MessageTarget.All)
    {
        if (target == MessageTarget.GuiOnly) return;
        
        lock (_updateLock)
        {
            var markup = new Markup($"[green]{Markup.Escape(message)}[/]");
            AddMessage("✓", message, markup, "green");
        }
    }

    public void ShowDebug(string message, MessageTarget target = MessageTarget.All)
    {
        if (target == MessageTarget.GuiOnly) return;
        
        lock (_updateLock)
        {
            var markup = new Markup($"[dim]{Markup.Escape(message)}[/]");
            AddMessage("🐛", message, markup, "dim");
        }
    }

    public void ShowCritical(string message, MessageTarget target = MessageTarget.All)
    {
        if (target == MessageTarget.GuiOnly) return;
        
        lock (_updateLock)
        {
            var markup = new Markup($"[bold red]{Markup.Escape(message)}[/]");
            AddMessage("‼", message, markup, "bold red");
        }
    }

    public void ShowMessage(string message, string? details = null, MessageType messageType = MessageType.Info,
        MessageTarget target = MessageTarget.All)
    {
        if (target == MessageTarget.GuiOnly) return;

        var fullMessage = details != null ? $"{message}\n{details}" : message;
        
        switch (messageType)
        {
            case MessageType.Info:
                ShowInfo(fullMessage, target);
                break;
            case MessageType.Warning:
                ShowWarning(fullMessage, target);
                break;
            case MessageType.Error:
                ShowError(fullMessage, target);
                break;
            case MessageType.Success:
                ShowSuccess(fullMessage, target);
                break;
            case MessageType.Debug:
                ShowDebug(fullMessage, target);
                break;
            case MessageType.Critical:
                ShowCritical(fullMessage, target);
                break;
        }
    }

    public IProgress<ProgressInfo> ShowProgress(string title, int totalItems)
    {
        return CreateProgressContext(title, totalItems);
    }

    public Core.Infrastructure.IProgressContext CreateProgressContext(string title, int totalItems)
    {
        var progressContext = new SpectreProgressAdapter(title, totalItems);
        
        // Start a background task to display the progress
        Task.Run(async () =>
        {
            await AnsiConsole.Progress()
                .AutoClear(false)
                .HideCompleted(false)
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
                    var task = ctx.AddTask(title, maxValue: totalItems);
                    progressContext.SetProgressTask(task);
                    
                    // Wait until the progress is completed or disposed
                    while (!progressContext.IsCompleted && !progressContext.IsDisposed)
                    {
                        await Task.Delay(100);
                    }
                });
        });

        return progressContext;
    }

    private void AddMessage(string icon, string message, Markup markup, string color)
    {
        var timestamp = DateTime.Now;
        _messages.Enqueue((timestamp, message, markup));
        
        // Keep only last 100 messages
        while (_messages.Count > 100)
        {
            _messages.TryDequeue(out _);
        }

        // Just write to console directly
        AnsiConsole.MarkupLine($"[dim]{timestamp:HH:mm:ss}[/] {icon} [{color}]{Markup.Escape(message)}[/]");
    }

    private void UpdateLiveDisplay()
    {
        _logTable.Rows.Clear();
        
        foreach (var (timestamp, _, markup) in _messages.TakeLast(20))
        {
            _logTable.AddRow(
                new Text(""),
                new Text(timestamp.ToString("HH:mm:ss")),
                markup
            );
        }
        
        _layout["messages"].Update(_logTable);
    }
}

internal class SpectreProgressAdapter : Core.Infrastructure.IProgressContext
{
    private ProgressTask? _task;
    private bool _disposed;
    private readonly string _title;
    private readonly int _totalItems;
    
    public bool IsCompleted { get; private set; }
    public bool IsDisposed => _disposed;

    public SpectreProgressAdapter(string title, int totalItems)
    {
        _title = title;
        _totalItems = totalItems;
    }

    public void SetProgressTask(ProgressTask task)
    {
        _task = task;
    }

    public void Update(int current, string message)
    {
        if (_disposed || _task == null) return;
        
        _task.Value = current;
        _task.Description = message;
    }

    public void Complete()
    {
        if (_disposed || _task == null) return;
        
        _task.Value = _task.MaxValue;
        _task.StopTask();
        IsCompleted = true;
    }

    public void Report(ProgressInfo value)
    {
        Update(value.Current, value.Message);
    }

    public void Dispose()
    {
        if (!_disposed)
        {
            Complete();
            _disposed = true;
        }
    }
}