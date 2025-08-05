using Scanner111.Core.Infrastructure;

namespace Scanner111.Tests.TestHelpers;

public class TestMessageCapture : IMessageHandler
{
    public List<string> InfoMessages { get; } = new();
    public List<string> WarningMessages { get; } = new();
    public List<string> ErrorMessages { get; } = new();
    public List<string> SuccessMessages { get; } = new();
    public List<string> DebugMessages { get; } = new();
    public List<string> CriticalMessages { get; } = new();
    public TestProgressContext? LastProgressContext { get; private set; }
    public List<(string message, string? details, MessageType type)> DetailedMessages { get; } = new();

    public void ShowInfo(string message, MessageTarget target = MessageTarget.All) => InfoMessages.Add(message);
    public void ShowWarning(string message, MessageTarget target = MessageTarget.All) => WarningMessages.Add(message);
    public void ShowError(string message, MessageTarget target = MessageTarget.All) => ErrorMessages.Add(message);
    public void ShowSuccess(string message, MessageTarget target = MessageTarget.All) => SuccessMessages.Add(message);
    public void ShowDebug(string message, MessageTarget target = MessageTarget.All) => DebugMessages.Add(message);
    public void ShowCritical(string message, MessageTarget target = MessageTarget.All) => CriticalMessages.Add(message);

    public void ShowMessage(string message, string? details = null, MessageType messageType = MessageType.Info,
        MessageTarget target = MessageTarget.All)
    {
        DetailedMessages.Add((message, details, messageType));
        
        switch (messageType)
        {
            case MessageType.Info:
                ShowInfo(message, target);
                break;
            case MessageType.Warning:
                ShowWarning(message, target);
                break;
            case MessageType.Error:
                ShowError(message, target);
                break;
            case MessageType.Success:
                ShowSuccess(message, target);
                break;
            case MessageType.Debug:
                ShowDebug(message, target);
                break;
            case MessageType.Critical:
                ShowCritical(message, target);
                break;
        }
    }

    public IProgress<ProgressInfo> ShowProgress(string title, int totalItems)
    {
        LastProgressContext = new TestProgressContext(title, totalItems);
        return LastProgressContext;
    }

    public IProgressContext CreateProgressContext(string description, int total)
    {
        LastProgressContext = new TestProgressContext(description, total);
        return LastProgressContext;
    }

    public void Clear() 
    {
        InfoMessages.Clear();
        WarningMessages.Clear();
        ErrorMessages.Clear();
        SuccessMessages.Clear();
        DebugMessages.Clear();
        CriticalMessages.Clear();
        DetailedMessages.Clear();
    }

    public bool IsUserInteractive => false;
}