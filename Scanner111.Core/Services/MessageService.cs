using System.Collections.Concurrent;
using Microsoft.Extensions.Logging;
using Scanner111.Core.Models;

namespace Scanner111.Core.Services;

/// <summary>
///     Provides messaging and progress reporting services.
///     Thread-safe for concurrent operations.
/// </summary>
public class MessageService : IMessageService, IDisposable
{
    private readonly ILogger<MessageService> _logger;
    private readonly SemaphoreSlim _progressSemaphore;
    private readonly ConcurrentDictionary<Guid, ProgressTracker> _progressTrackers;
    private bool _disposed;

    /// <summary>
    ///     Initializes a new instance of the <see cref="MessageService" /> class.
    /// </summary>
    /// <param name="logger">Logger for internal logging.</param>
    public MessageService(ILogger<MessageService> logger)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _progressTrackers = new ConcurrentDictionary<Guid, ProgressTracker>();
        _progressSemaphore = new SemaphoreSlim(1, 1);
    }

    /// <summary>
    ///     Disposes resources used by the message service.
    /// </summary>
    public void Dispose()
    {
        if (_disposed)
            return;

        _progressSemaphore?.Dispose();
        _progressTrackers.Clear();
        _disposed = true;

        GC.SuppressFinalize(this);
    }

    /// <inheritdoc />
    public event EventHandler<Message>? MessagePublished;

    /// <inheritdoc />
    public void Info(string content, string? title = null, string? details = null)
    {
        var message = new Message
        {
            Content = content,
            Type = MessageType.Info,
            Title = title,
            Details = details,
            Source = GetCallerInfo()
        };

        Publish(message);
    }

    /// <inheritdoc />
    public void Warning(string content, string? title = null, string? details = null)
    {
        var message = new Message
        {
            Content = content,
            Type = MessageType.Warning,
            Title = title,
            Details = details,
            Source = GetCallerInfo()
        };

        Publish(message);
    }

    /// <inheritdoc />
    public void Error(string content, string? title = null, Exception? exception = null, string? details = null)
    {
        var message = new Message
        {
            Content = content,
            Type = MessageType.Error,
            Title = title,
            Details = details ?? exception?.Message,
            Exception = exception,
            Source = GetCallerInfo()
        };

        Publish(message);
    }

    /// <inheritdoc />
    public void Success(string content, string? title = null, string? details = null)
    {
        var message = new Message
        {
            Content = content,
            Type = MessageType.Success,
            Title = title,
            Details = details,
            Source = GetCallerInfo()
        };

        Publish(message);
    }

    /// <inheritdoc />
    public void Debug(string content, string? title = null, string? details = null)
    {
        var message = new Message
        {
            Content = content,
            Type = MessageType.Debug,
            Title = title,
            Details = details,
            Source = GetCallerInfo()
        };

        Publish(message);
    }

    /// <inheritdoc />
    public void Critical(string content, string? title = null, Exception? exception = null, string? details = null)
    {
        var message = new Message
        {
            Content = content,
            Type = MessageType.Critical,
            Title = title,
            Details = details ?? exception?.Message,
            Exception = exception,
            Source = GetCallerInfo()
        };

        Publish(message);
    }

    /// <inheritdoc />
    public void Publish(Message message)
    {
        ArgumentNullException.ThrowIfNull(message);

        // Log the message
        LogMessage(message);

        // Raise the event
        try
        {
            MessagePublished?.Invoke(this, message);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error occurred while publishing message: {Content}", message.Content);
        }
    }

    /// <inheritdoc />
    public IProgress<ProgressReport> CreateProgressReporter(string description, int? total = null)
    {
        var trackerId = Guid.NewGuid();
        var tracker = new ProgressTracker(this, trackerId, description, total);
        _progressTrackers[trackerId] = tracker;

        return tracker;
    }

    /// <inheritdoc />
    public async Task<T> ExecuteWithProgressAsync<T>(
        string description,
        Func<IProgress<ProgressReport>, CancellationToken, Task<T>> operation,
        int? total = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(operation);

        var progress = CreateProgressReporter(description, total);

        try
        {
            Info($"Starting: {description}");
            var result = await operation(progress, cancellationToken).ConfigureAwait(false);
            Success($"Completed: {description}");
            return result;
        }
        catch (OperationCanceledException)
        {
            Warning($"Cancelled: {description}");
            throw;
        }
        catch (Exception ex)
        {
            Error($"Failed: {description}", exception: ex);
            throw;
        }
        finally
        {
            // Clean up the progress tracker
            if (progress is ProgressTracker tracker) _progressTrackers.TryRemove(tracker.Id, out _);
        }
    }

    /// <inheritdoc />
    public async Task ExecuteWithProgressAsync(
        string description,
        Func<IProgress<ProgressReport>, CancellationToken, Task> operation,
        int? total = null,
        CancellationToken cancellationToken = default)
    {
        await ExecuteWithProgressAsync(
            description,
            async (progress, ct) =>
            {
                await operation(progress, ct).ConfigureAwait(false);
                return 0; // Dummy return value
            },
            total,
            cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    ///     Logs a message using the configured logger.
    /// </summary>
    private void LogMessage(Message message)
    {
        var logLevel = message.Type switch
        {
            MessageType.Debug => LogLevel.Debug,
            MessageType.Info => LogLevel.Information,
            MessageType.Success => LogLevel.Information,
            MessageType.Warning => LogLevel.Warning,
            MessageType.Error => LogLevel.Error,
            MessageType.Critical => LogLevel.Critical,
            _ => LogLevel.Information
        };

        if (message.Exception != null)
            _logger.Log(logLevel, message.Exception, "{Content} - {Details}",
                message.Content, message.Details);
        else
            _logger.Log(logLevel, "{Content} - {Details}",
                message.Content, message.Details ?? string.Empty);
    }

    /// <summary>
    ///     Gets caller information for message source tracking.
    /// </summary>
    private static string GetCallerInfo()
    {
        // In production, you might want to use CallerMemberName attributes
        // For now, return the current thread name or ID
        var thread = Thread.CurrentThread;
        return thread.Name ?? $"Thread {thread.ManagedThreadId}";
    }

    /// <summary>
    ///     Internal progress tracker implementation.
    ///     Thread-safe for concurrent progress updates.
    /// </summary>
    private sealed class ProgressTracker : IProgress<ProgressReport>
    {
        private readonly string _description;
        private readonly MessageService _service;
        private readonly int? _total;
        private int _current;

        public ProgressTracker(MessageService service, Guid id, string description, int? total)
        {
            _service = service;
            Id = id;
            _description = description;
            _total = total;
            _current = 0;
        }

        public Guid Id { get; }

        public void Report(ProgressReport value)
        {
            // Thread-safe update of current value
            var newCurrent = Interlocked.Exchange(ref _current, value.Current);

            // Publish progress as a message
            var progressMessage = new Message
            {
                Content = value.Description ?? _description,
                Type = MessageType.Info,
                Title = "Progress",
                Details = value.IsIndeterminate
                    ? $"{value.Current} items processed"
                    : $"{value.PercentComplete:F1}% ({value.Current}/{value.Total})"
            };

            _service.Publish(progressMessage);
        }
    }
}