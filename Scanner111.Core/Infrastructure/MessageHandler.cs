namespace Scanner111.Core.Infrastructure;

/// <summary>
///     Interface for handling messages across different UI contexts
/// </summary>
public interface IMessageHandler
{
    /// <summary>
    ///     Show an informational message
    /// </summary>
    void ShowInfo(string message, MessageTarget target = MessageTarget.All);

    /// <summary>
    ///     Show a warning message
    /// </summary>
    void ShowWarning(string message, MessageTarget target = MessageTarget.All);

    /// <summary>
    ///     Show an error message
    /// </summary>
    void ShowError(string message, MessageTarget target = MessageTarget.All);

    /// <summary>
    ///     Show a success message
    /// </summary>
    void ShowSuccess(string message, MessageTarget target = MessageTarget.All);

    /// <summary>
    ///     Show a debug message
    /// </summary>
    void ShowDebug(string message, MessageTarget target = MessageTarget.All);

    /// <summary>
    ///     Show a critical error message
    /// </summary>
    void ShowCritical(string message, MessageTarget target = MessageTarget.All);

    /// <summary>
    ///     Show a message with details that can be expanded
    /// </summary>
    void ShowMessage(string message, string? details = null, MessageType messageType = MessageType.Info,
        MessageTarget target = MessageTarget.All);

    /// <summary>
    ///     Show a progress indicator
    /// </summary>
    IProgress<ProgressInfo> ShowProgress(string title, int totalItems);

    /// <summary>
    ///     Create a progress context for use with 'using' statements
    /// </summary>
    IProgressContext CreateProgressContext(string title, int totalItems);
}

/// <summary>
///     Type of message being displayed
/// </summary>
public enum MessageType
{
    Info,
    Warning,
    Error,
    Success,
    Debug,
    Critical
}

/// <summary>
///     Target for message display
/// </summary>
public enum MessageTarget
{
    /// <summary>
    ///     Show in all available interfaces
    /// </summary>
    All,

    /// <summary>
    ///     Show only in GUI interface
    /// </summary>
    GuiOnly,

    /// <summary>
    ///     Show only in CLI interface
    /// </summary>
    CliOnly,

    /// <summary>
    ///     Log only, don't show in UI
    /// </summary>
    LogOnly
}

/// <summary>
///     Progress information for long-running operations
/// </summary>
public class ProgressInfo
{
    /// <summary>
    ///     Current progress value
    /// </summary>
    public int Current { get; init; }

    /// <summary>
    ///     Total number of items
    /// </summary>
    public int Total { get; init; }

    /// <summary>
    ///     Current operation message
    /// </summary>
    public string Message { get; init; } = string.Empty;

    /// <summary>
    ///     Progress percentage (0-100)
    /// </summary>
    public double Percentage => Total > 0 ? Current * 100.0 / Total : 0;
}

/// <summary>
///     Progress context for use with 'using' statements - matches Python's context manager pattern
/// </summary>
public interface IProgressContext : IProgress<ProgressInfo>, IDisposable
{
    /// <summary>
    ///     Update progress with current count and message
    /// </summary>
    void Update(int current, string message);

    /// <summary>
    ///     Complete the progress operation
    /// </summary>
    void Complete();
}

/// <summary>
///     Static message handler for global access
/// </summary>
public static class MessageHandler
{
    private static IMessageHandler? _handler;

    /// <summary>
    ///     Check if a handler is initialized
    /// </summary>
    public static bool IsInitialized => _handler != null;

    /// <summary>
    ///     Initialize the message handler
    /// </summary>
    /// <param name="handler">Handler implementation</param>
    public static void Initialize(IMessageHandler handler)
    {
        _handler = handler ?? throw new ArgumentNullException(nameof(handler));
    }

    /// <summary>
    ///     Show an informational message
    /// </summary>
    public static void MsgInfo(string message, MessageTarget target = MessageTarget.All)
    {
        _handler?.ShowInfo(message, target);
    }

    /// <summary>
    ///     Show a warning message
    /// </summary>
    public static void MsgWarning(string message, MessageTarget target = MessageTarget.All)
    {
        _handler?.ShowWarning(message, target);
    }

    /// <summary>
    ///     Show an error message
    /// </summary>
    public static void MsgError(string message, MessageTarget target = MessageTarget.All)
    {
        _handler?.ShowError(message, target);
    }

    /// <summary>
    ///     Show a success message
    /// </summary>
    public static void MsgSuccess(string message, MessageTarget target = MessageTarget.All)
    {
        _handler?.ShowSuccess(message, target);
    }

    /// <summary>
    ///     Show a debug message
    /// </summary>
    public static void MsgDebug(string message, MessageTarget target = MessageTarget.All)
    {
        _handler?.ShowDebug(message, target);
    }

    /// <summary>
    ///     Show a critical message
    /// </summary>
    public static void MsgCritical(string message, MessageTarget target = MessageTarget.All)
    {
        _handler?.ShowCritical(message, target);
    }

    /// <summary>
    ///     Show a progress indicator
    /// </summary>
    public static IProgress<ProgressInfo> MsgProgress(string title, int totalItems)
    {
        return _handler?.ShowProgress(title, totalItems) ?? new NullProgress();
    }

    /// <summary>
    ///     Create a progress context for use with 'using' statements
    /// </summary>
    public static IProgressContext CreateProgressContext(string title, int totalItems)
    {
        return _handler?.CreateProgressContext(title, totalItems) ?? new NullProgressContext();
    }
}

/// <summary>
///     Null progress implementation for safety
/// </summary>
internal class NullProgress : IProgress<ProgressInfo>
{
    public void Report(ProgressInfo value)
    {
        // Do nothing
    }
}

/// <summary>
///     Null progress context implementation for safety
/// </summary>
internal class NullProgressContext : IProgressContext
{
    public void Update(int current, string message)
    {
        // Do nothing
    }

    public void Complete()
    {
        // Do nothing
    }

    public void Report(ProgressInfo value)
    {
        // Do nothing
    }

    public void Dispose()
    {
        // Do nothing
    }
}