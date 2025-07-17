namespace Scanner111.Core.Infrastructure;

/// <summary>
/// Interface for handling messages across different UI contexts
/// </summary>
public interface IMessageHandler
{
    /// <summary>
    /// Show an informational message
    /// </summary>
    void ShowInfo(string message, MessageTarget target = MessageTarget.All);
    
    /// <summary>
    /// Show a warning message
    /// </summary>
    void ShowWarning(string message);
    
    /// <summary>
    /// Show an error message
    /// </summary>
    void ShowError(string message);
    
    /// <summary>
    /// Show a progress indicator
    /// </summary>
    IProgress<ProgressInfo> ShowProgress(string title, int totalItems);
}

/// <summary>
/// Target for message display
/// </summary>
public enum MessageTarget
{
    /// <summary>
    /// Show in all available interfaces
    /// </summary>
    All,
    
    /// <summary>
    /// Show only in GUI interface
    /// </summary>
    GuiOnly,
    
    /// <summary>
    /// Show only in CLI interface
    /// </summary>
    CliOnly
}

/// <summary>
/// Progress information for long-running operations
/// </summary>
public class ProgressInfo
{
    /// <summary>
    /// Current progress value
    /// </summary>
    public int Current { get; init; }
    
    /// <summary>
    /// Total number of items
    /// </summary>
    public int Total { get; init; }
    
    /// <summary>
    /// Current operation message
    /// </summary>
    public string Message { get; init; } = string.Empty;
    
    /// <summary>
    /// Progress percentage (0-100)
    /// </summary>
    public double Percentage => Total > 0 ? (Current * 100.0) / Total : 0;
}

/// <summary>
/// Static message handler for global access
/// </summary>
public static class MessageHandler
{
    private static IMessageHandler? _handler;
    
    /// <summary>
    /// Initialize the message handler
    /// </summary>
    /// <param name="handler">Handler implementation</param>
    public static void Initialize(IMessageHandler handler)
    {
        _handler = handler ?? throw new ArgumentNullException(nameof(handler));
    }
    
    /// <summary>
    /// Show an informational message
    /// </summary>
    public static void MsgInfo(string message, MessageTarget target = MessageTarget.All)
    {
        _handler?.ShowInfo(message, target);
    }
    
    /// <summary>
    /// Show a warning message
    /// </summary>
    public static void MsgWarning(string message)
    {
        _handler?.ShowWarning(message);
    }
    
    /// <summary>
    /// Show an error message
    /// </summary>
    public static void MsgError(string message)
    {
        _handler?.ShowError(message);
    }
    
    /// <summary>
    /// Show a progress indicator
    /// </summary>
    public static IProgress<ProgressInfo> MsgProgress(string title, int totalItems)
    {
        return _handler?.ShowProgress(title, totalItems) ?? new NullProgress();
    }
    
    /// <summary>
    /// Check if a handler is initialized
    /// </summary>
    public static bool IsInitialized => _handler != null;
}

/// <summary>
/// Null progress implementation for safety
/// </summary>
internal class NullProgress : IProgress<ProgressInfo>
{
    public void Report(ProgressInfo value)
    {
        // Do nothing
    }
}