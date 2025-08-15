using System.Collections.Concurrent;
using System.Threading.Channels;
using Scanner111.Core.Infrastructure;
using Spectre.Console;
using Spectre.Console.Rendering;

namespace Scanner111.CLI.Services;

/// <summary>
///     Enhanced message handler with multi-progress support and split-screen layout
/// </summary>
public class EnhancedSpectreMessageHandler : IMessageHandler, IAsyncDisposable
{
    private readonly Layout _mainLayout;
    private readonly MessageLogger _messageLogger;
    private readonly ProgressManager _progressManager;
    private readonly Task _renderTask;
    private readonly CancellationTokenSource _shutdownCts;

    public EnhancedSpectreMessageHandler()
    {
        _shutdownCts = new CancellationTokenSource();
        _progressManager = new ProgressManager();
        _messageLogger = new MessageLogger();

        // Create split-screen layout
        _mainLayout = new Layout()
            .SplitRows(
                new Layout("header").Size(3),
                new Layout("body").SplitColumns(
                    new Layout("progress").Ratio(3),
                    new Layout("logs").Ratio(2)
                ),
                new Layout("status").Size(2)
            );

        // Initialize header
        _mainLayout["header"].Update(
            new Panel(new Text("Scanner111 - Enhanced Progress Display")
                    .Centered())
                .BorderColor(Color.Cyan1)
                .Border(BoxBorder.Rounded));

        // Start the render loop
        _renderTask = Task.Run(RenderLoopAsync);
    }

    public async ValueTask DisposeAsync()
    {
        _shutdownCts.Cancel();

        try
        {
            await _renderTask.ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            // Expected
        }

        await _progressManager.DisposeAsync();
        _shutdownCts.Dispose();
    }

    public void ShowInfo(string message, MessageTarget target = MessageTarget.All)
    {
        if (target == MessageTarget.GuiOnly) return;
        _messageLogger.AddMessage(MessageType.Info, message);
    }

    public void ShowWarning(string message, MessageTarget target = MessageTarget.All)
    {
        if (target == MessageTarget.GuiOnly) return;
        _messageLogger.AddMessage(MessageType.Warning, message);
    }

    public void ShowError(string message, MessageTarget target = MessageTarget.All)
    {
        if (target == MessageTarget.GuiOnly) return;
        _messageLogger.AddMessage(MessageType.Error, message);
    }

    public void ShowSuccess(string message, MessageTarget target = MessageTarget.All)
    {
        if (target == MessageTarget.GuiOnly) return;
        _messageLogger.AddMessage(MessageType.Success, message);
    }

    public void ShowDebug(string message, MessageTarget target = MessageTarget.All)
    {
        if (target == MessageTarget.GuiOnly) return;
        _messageLogger.AddMessage(MessageType.Debug, message);
    }

    public void ShowCritical(string message, MessageTarget target = MessageTarget.All)
    {
        if (target == MessageTarget.GuiOnly) return;
        _messageLogger.AddMessage(MessageType.Critical, message);
    }

    public void ShowMessage(string message, string? details = null, MessageType messageType = MessageType.Info,
        MessageTarget target = MessageTarget.All)
    {
        if (target == MessageTarget.GuiOnly) return;

        var fullMessage = details != null ? $"{message}\n{details}" : message;
        _messageLogger.AddMessage(messageType, fullMessage);
    }

    public IProgress<ProgressInfo> ShowProgress(string title, int totalItems)
    {
        return CreateProgressContext(title, totalItems);
    }

    public IProgressContext CreateProgressContext(string title, int totalItems)
    {
        return _progressManager.CreateContext(title, totalItems);
    }

    private async Task RenderLoopAsync()
    {
        try
        {
            // Start progress manager in background
            _ = Task.Run(() => _progressManager.StartAsync(_shutdownCts.Token));

            // Use Live display for the layout
            await AnsiConsole.Live(_mainLayout)
                .AutoClear(false)
                .StartAsync(async ctx =>
                {
                    // Update loop
                    while (!_shutdownCts.Token.IsCancellationRequested)
                    {
                        UpdateDisplay(ctx);
                        await Task.Delay(50, _shutdownCts.Token).ConfigureAwait(false);
                    }
                }).ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            // Expected during shutdown
        }
    }

    private void UpdateDisplay(LiveDisplayContext ctx)
    {
        // Update progress panel
        _mainLayout["progress"].Update(_progressManager.GetProgressPanel());

        // Update logs panel
        _mainLayout["logs"].Update(_messageLogger.GetLogsPanel());

        // Update status bar
        _mainLayout["status"].Update(GetStatusBar());

        ctx.Refresh();
    }

    private Panel GetStatusBar()
    {
        var grid = new Grid()
            .AddColumn()
            .AddColumn()
            .AddColumn();

        grid.AddRow(
            new Text($"Time: {DateTime.Now:HH:mm:ss}"),
            new Text($"Active Tasks: {_progressManager.ActiveTaskCount}").Centered(),
            new Text($"Memory: {GC.GetTotalMemory(false) / 1_048_576:N0} MB").RightJustified()
        );

        return new Panel(grid)
            .Border(BoxBorder.None)
            .BorderColor(Color.Grey);
    }
}

/// <summary>
///     Manages multiple concurrent progress contexts
/// </summary>
internal class ProgressManager : IAsyncDisposable
{
    private readonly ConcurrentDictionary<string, ProgressTask> _activeTasks;
    private readonly Channel<ProgressCommand> _commandChannel;
    private readonly ConcurrentDictionary<string, ProgressContextAdapter> _contexts;
    private CancellationTokenSource? _cts;
    private Task? _processTask;
    private ProgressContext? _spectreContext;

    public ProgressManager()
    {
        _commandChannel = Channel.CreateUnbounded<ProgressCommand>(new UnboundedChannelOptions
        {
            SingleReader = true,
            SingleWriter = false
        });

        _contexts = new ConcurrentDictionary<string, ProgressContextAdapter>();
        _activeTasks = new ConcurrentDictionary<string, ProgressTask>();
    }

    public int ActiveTaskCount => _activeTasks.Count;

    public async ValueTask DisposeAsync()
    {
        _commandChannel.Writer.TryComplete();

        if (_processTask != null)
            try
            {
                await _processTask.ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
                // Expected
            }

        _cts?.Dispose();
    }

    public async Task StartAsync(CancellationToken cancellationToken)
    {
        _cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);

        _processTask = AnsiConsole.Progress()
            .AutoClear(false)
            .HideCompleted(false)
            .Columns(new TaskDescriptionColumn { Alignment = Justify.Left }, new ProgressBarColumn { Width = 40 },
                new PercentageColumn(), new RemainingTimeColumn(), new SpinnerColumn())
            .StartAsync(async ctx =>
            {
                _spectreContext = ctx;
                await ProcessCommandsAsync(_cts.Token).ConfigureAwait(false);
            });
    }

    private async Task ProcessCommandsAsync(CancellationToken cancellationToken)
    {
        await foreach (var command in _commandChannel.Reader.ReadAllAsync(cancellationToken))
            try
            {
                switch (command)
                {
                    case CreateProgressCommand create:
                        var task = _spectreContext!.AddTask(create.Title, maxValue: create.MaxValue);
                        _activeTasks[create.Id] = task;
                        create.TaskCreated.SetResult(task);
                        break;

                    case UpdateProgressCommand update:
                        if (_activeTasks.TryGetValue(update.Id, out var existingTask))
                        {
                            existingTask.Value = update.Current;
                            existingTask.Description = update.Message ?? existingTask.Description;
                        }

                        break;

                    case CompleteProgressCommand complete:
                        if (_activeTasks.TryRemove(complete.Id, out var completedTask))
                        {
                            completedTask.Value = completedTask.MaxValue;
                            completedTask.StopTask();
                        }

                        _contexts.TryRemove(complete.Id, out _);
                        break;
                }
            }
            catch (Exception ex)
            {
                // Log error but continue processing
                AnsiConsole.WriteException(ex);
            }
    }

    public IProgressContext CreateContext(string title, int totalItems)
    {
        var id = Guid.NewGuid().ToString();
        var context = new ProgressContextAdapter(id, title, totalItems, _commandChannel.Writer);
        _contexts[id] = context;

        // Send create command
        var createCommand = new CreateProgressCommand(id, title, totalItems);
        _commandChannel.Writer.TryWrite(createCommand);

        // Use async wait with timeout to avoid blocking threads
        try
        {
            createCommand.TaskCreated.Task.WaitAsync(TimeSpan.FromSeconds(1)).ConfigureAwait(false).GetAwaiter()
                .GetResult();
        }
        catch (TimeoutException)
        {
            // Handle timeout gracefully - progress context will still work, just might not show immediately
            // This prevents resource leaks from blocking waits
        }

        return context;
    }

    public Panel GetProgressPanel()
    {
        IRenderable content;

        if (_activeTasks.IsEmpty)
        {
            content = new Text("No active tasks", new Style(Color.Grey)).Centered();
        }
        else
        {
            var table = new Table()
                .Border(TableBorder.None)
                .HideHeaders()
                .AddColumn("Task")
                .AddColumn("Progress");

            foreach (var (id, task) in _activeTasks)
            {
                var percentage = task.MaxValue > 0 ? task.Value / task.MaxValue * 100 : 0;
                var filled = (int)(percentage / 10);
                var empty = 10 - filled;
                var progressBar = new string('█', filled) + new string('░', empty);
                var progressText = $"{progressBar} {percentage:0}%";

                table.AddRow(
                    new Text(task.Description.Replace("[", "[[").Replace("]", "]]")),
                    new Text(progressText)
                );
            }

            content = table;
        }

        return new Panel(content)
            .Header("Progress", Justify.Center)
            .BorderColor(Color.Blue)
            .Border(BoxBorder.Rounded);
    }
}

/// <summary>
///     Manages log messages with circular buffer
/// </summary>
internal class MessageLogger
{
    private readonly object _lock = new();
    private readonly CircularBuffer<LogMessage> _messages;

    public MessageLogger(int capacity = 100)
    {
        _messages = new CircularBuffer<LogMessage>(capacity);
    }

    public void AddMessage(MessageType type, string message)
    {
        lock (_lock)
        {
            _messages.Add(new LogMessage(DateTime.Now, type, message));
        }
    }

    public Panel GetLogsPanel()
    {
        var table = new Table()
            .Border(TableBorder.None)
            .HideHeaders()
            .AddColumn("Time", c => c.Width(8))
            .AddColumn("Type", c => c.Width(3))
            .AddColumn("Message");

        lock (_lock)
        {
            foreach (var msg in _messages.GetItems().TakeLast(20))
            {
                var (icon, color) = GetMessageStyle(msg.Type);
                table.AddRow(
                    new Text(msg.Timestamp.ToString("HH:mm:ss"), new Style(Color.Grey)),
                    new Text(icon, new Style(color)),
                    new Text(msg.Message.Replace("[", "[[").Replace("]", "]]"), new Style(color))
                );
            }
        }

        if (table.Rows.Count == 0)
            table.AddRow(
                new Text(""),
                new Text(""),
                new Text("No messages", new Style(Color.Grey))
            );

        return new Panel(table)
            .Header("Logs", Justify.Center)
            .BorderColor(Color.Green)
            .Border(BoxBorder.Rounded);
    }

    private static (string icon, Color color) GetMessageStyle(MessageType type)
    {
        return type switch
        {
            MessageType.Info => ("ℹ", Color.Blue),
            MessageType.Warning => ("⚠", Color.Yellow),
            MessageType.Error => ("✗", Color.Red),
            MessageType.Success => ("✓", Color.Green),
            MessageType.Debug => ("🐛", Color.Grey),
            MessageType.Critical => ("‼", Color.Red1),
            _ => ("•", Color.White)
        };
    }

    private record LogMessage(DateTime Timestamp, MessageType Type, string Message);
}

/// <summary>
///     Circular buffer implementation for efficient memory usage
/// </summary>
internal class CircularBuffer<T>
{
    private readonly T[] _buffer;
    private readonly object _lock = new();
    private int _count;
    private int _writeIndex;

    public CircularBuffer(int capacity)
    {
        _buffer = new T[capacity];
    }

    public void Add(T item)
    {
        lock (_lock)
        {
            _buffer[_writeIndex] = item;
            _writeIndex = (_writeIndex + 1) % _buffer.Length;
            if (_count < _buffer.Length)
                _count++;
        }
    }

    public IEnumerable<T> GetItems()
    {
        lock (_lock)
        {
            if (_count == 0)
                yield break;

            var startIndex = _count < _buffer.Length ? 0 : _writeIndex;

            for (var i = 0; i < _count; i++)
            {
                var index = (startIndex + i) % _buffer.Length;
                if (_buffer[index] != null)
                    yield return _buffer[index];
            }
        }
    }
}

/// <summary>
///     Adapter for individual progress contexts
/// </summary>
internal class ProgressContextAdapter : IProgressContext
{
    private readonly ChannelWriter<ProgressCommand> _commandWriter;
    private readonly string _id;
    private readonly string _title;
    private readonly int _totalItems;
    private volatile bool _completed;
    private volatile bool _disposed;

    public ProgressContextAdapter(string id, string title, int totalItems, ChannelWriter<ProgressCommand> commandWriter)
    {
        _id = id;
        _title = title;
        _totalItems = totalItems;
        _commandWriter = commandWriter;
    }

    public void Update(int current, string message)
    {
        if (_disposed || _completed) return;

        _commandWriter.TryWrite(new UpdateProgressCommand(_id, current, message));
    }

    public void Complete()
    {
        if (_disposed || _completed) return;

        _completed = true;
        _commandWriter.TryWrite(new CompleteProgressCommand(_id));
    }

    public void Report(ProgressInfo value)
    {
        Update(value.Current, value.Message);
    }

    public void Dispose()
    {
        if (!_disposed)
        {
            _disposed = true;
            if (!_completed) Complete();
        }
    }
}

/// <summary>
///     Command types for progress updates
/// </summary>
internal abstract record ProgressCommand(string Id);

internal record CreateProgressCommand(string Id, string Title, int MaxValue) : ProgressCommand(Id)
{
    public TaskCompletionSource<ProgressTask> TaskCreated { get; } = new();
}

internal record UpdateProgressCommand(string Id, int Current, string? Message) : ProgressCommand(Id);

internal record CompleteProgressCommand(string Id) : ProgressCommand(Id);