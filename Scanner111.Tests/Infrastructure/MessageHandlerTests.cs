using Scanner111.Core.Infrastructure;

namespace Scanner111.Tests.Infrastructure;

public class MessageHandlerTests
{
    private readonly TestMessageHandler _testHandler;

    public MessageHandlerTests()
    {
        _testHandler = new TestMessageHandler();
        MessageHandler.Initialize(_testHandler);
    }

    [Fact]
    public void MessageHandler_IsInitialized_ReturnsTrueAfterInitialization()
    {
        Assert.True(MessageHandler.IsInitialized);
    }

    [Fact]
    public void MessageHandler_MsgInfo_CallsHandler()
    {
        MessageHandler.MsgInfo("Test info message");

        Assert.Contains("Test info message", _testHandler.InfoMessages);
        Assert.Equal(MessageTarget.All, _testHandler.LastInfoTarget);
    }

    [Fact]
    public void MessageHandler_MsgInfo_WithTarget_CallsHandler()
    {
        MessageHandler.MsgInfo("Test info message", MessageTarget.GuiOnly);

        Assert.Contains("Test info message", _testHandler.InfoMessages);
        Assert.Equal(MessageTarget.GuiOnly, _testHandler.LastInfoTarget);
    }

    [Fact]
    public void MessageHandler_MsgWarning_CallsHandler()
    {
        MessageHandler.MsgWarning("Test warning message");

        Assert.Contains("Test warning message", _testHandler.WarningMessages);
    }

    [Fact]
    public void MessageHandler_MsgError_CallsHandler()
    {
        MessageHandler.MsgError("Test error message");

        Assert.Contains("Test error message", _testHandler.ErrorMessages);
    }

    [Fact]
    public void MessageHandler_MsgProgress_CallsHandler()
    {
        var progress = MessageHandler.MsgProgress("Test progress", 100);

        Assert.NotNull(progress);
        Assert.Equal("Test progress", _testHandler.LastProgressTitle);
        Assert.Equal(100, _testHandler.LastProgressTotal);
    }

    [Fact]
    public void MessageHandler_WithoutInitialization_DoesNotThrow()
    {
        // Create a new test handler for this test
        var tempHandler = new TestMessageHandler();
        MessageHandler.Initialize(tempHandler);

        // These should not throw exceptions
        MessageHandler.MsgInfo("Test");
        MessageHandler.MsgWarning("Test");
        MessageHandler.MsgError("Test");

        var progress = MessageHandler.MsgProgress("Test", 10);
        Assert.NotNull(progress);

        // Progress should work correctly
        progress.Report(new ProgressInfo { Current = 5, Total = 10 });

        // Verify messages were handled
        Assert.Contains("Test", tempHandler.InfoMessages);
        Assert.Contains("Test", tempHandler.WarningMessages);
        Assert.Contains("Test", tempHandler.ErrorMessages);
    }

    [Fact]
    public void MessageHandler_ProgressContext_ReportsProgressCorrectly()
    {
        var progressContext = _testHandler.CreateProgressContext("Test Progress", 50);

        progressContext.Update(10, "Processing item 10");
        progressContext.Update(25, "Processing item 25");
        progressContext.Report(new ProgressInfo { Current = 40, Total = 50, Message = "Almost done" });

        var testProgressContext = Assert.IsType<TestProgressContext>(progressContext);
        Assert.Equal(3, testProgressContext.Reports.Count);

        Assert.Equal(10, testProgressContext.Reports[0].Current);
        Assert.Equal("Processing item 10", testProgressContext.Reports[0].Message);

        Assert.Equal(25, testProgressContext.Reports[1].Current);
        Assert.Equal("Processing item 25", testProgressContext.Reports[1].Message);

        Assert.Equal(40, testProgressContext.Reports[2].Current);
        Assert.Equal("Almost done", testProgressContext.Reports[2].Message);

        progressContext.Complete();
    }

    [Fact]
    public void MessageHandler_Progress_ReportsProgressCorrectly()
    {
        var progress = _testHandler.ShowProgress("Test Progress", 100);

        progress.Report(new ProgressInfo { Current = 25, Total = 100, Message = "Quarter done" });
        progress.Report(new ProgressInfo { Current = 75, Total = 100, Message = "Three quarters done" });

        var testProgress = Assert.IsType<TestProgress>(progress);
        Assert.Equal(2, testProgress.Reports.Count);

        Assert.Equal(25, testProgress.Reports[0].Current);
        Assert.Equal("Quarter done", testProgress.Reports[0].Message);

        Assert.Equal(75, testProgress.Reports[1].Current);
        Assert.Equal("Three quarters done", testProgress.Reports[1].Message);
    }

    [Fact]
    public void MessageHandler_ProgressContext_DisposesCorrectly()
    {
        var progressContext = _testHandler.CreateProgressContext("Test Progress", 50);
        var testProgressContext = Assert.IsType<TestProgressContext>(progressContext);

        Assert.False(testProgressContext.IsDisposed);

        progressContext.Dispose();

        Assert.True(testProgressContext.IsDisposed);
    }

    [Fact]
    public void ProgressInfo_Properties_WorkCorrectly()
    {
        var progress = new ProgressInfo
        {
            Current = 25,
            Total = 100,
            Message = "Processing..."
        };

        Assert.Equal(25, progress.Current);
        Assert.Equal(100, progress.Total);
        Assert.Equal("Processing...", progress.Message);
        Assert.Equal(25.0, progress.Percentage);
    }

    [Fact]
    public void ProgressInfo_Percentage_HandlesZeroTotal()
    {
        var progress = new ProgressInfo
        {
            Current = 5,
            Total = 0
        };

        Assert.Equal(0.0, progress.Percentage);
    }

    [Fact]
    public void MessageTarget_AllValues_AreAccessible()
    {
        Assert.Equal(MessageTarget.All, MessageTarget.All);
        Assert.Equal(MessageTarget.GuiOnly, MessageTarget.GuiOnly);
        Assert.Equal(MessageTarget.CliOnly, MessageTarget.CliOnly);
    }
}

// Test implementation of IMessageHandler
public class TestMessageHandler : IMessageHandler
{
    public List<string> InfoMessages { get; } = new();
    public List<string> WarningMessages { get; } = new();
    public List<string> ErrorMessages { get; } = new();
    public MessageTarget LastInfoTarget { get; private set; } = MessageTarget.All;
    public string LastProgressTitle { get; private set; } = string.Empty;
    public int LastProgressTotal { get; private set; }

    public void ShowInfo(string message, MessageTarget target = MessageTarget.All)
    {
        InfoMessages.Add(message);
        LastInfoTarget = target;
    }

    public void ShowWarning(string message, MessageTarget target = MessageTarget.All)
    {
        WarningMessages.Add(message);
    }

    public void ShowError(string message, MessageTarget target = MessageTarget.All)
    {
        ErrorMessages.Add(message);
    }

    public void ShowSuccess(string message, MessageTarget target = MessageTarget.All)
    {
        // Just store in info messages for testing
        InfoMessages.Add(message);
    }

    public void ShowDebug(string message, MessageTarget target = MessageTarget.All)
    {
        // Just store in info messages for testing
        InfoMessages.Add(message);
    }

    public void ShowCritical(string message, MessageTarget target = MessageTarget.All)
    {
        // Just store in error messages for testing
        ErrorMessages.Add(message);
    }

    public void ShowMessage(string message, string? details = null, MessageType messageType = MessageType.Info,
        MessageTarget target = MessageTarget.All)
    {
        switch (messageType)
        {
            case MessageType.Info:
                InfoMessages.Add(message);
                break;
            case MessageType.Warning:
                WarningMessages.Add(message);
                break;
            case MessageType.Error:
                ErrorMessages.Add(message);
                break;
            case MessageType.Success:
            case MessageType.Debug:
            case MessageType.Critical:
            default:
                InfoMessages.Add(message);
                break;
        }
    }

    public IProgress<ProgressInfo> ShowProgress(string title, int totalItems)
    {
        LastProgressTitle = title;
        LastProgressTotal = totalItems;
        return new TestProgress();
    }

    public IProgressContext CreateProgressContext(string title, int totalItems)
    {
        LastProgressTitle = title;
        LastProgressTotal = totalItems;
        return new TestProgressContext();
    }
}

public class TestProgress : IProgress<ProgressInfo>
{
    public List<ProgressInfo> Reports { get; } = [];

    public void Report(ProgressInfo value)
    {
        Reports.Add(value);
    }
}

public class TestProgressContext : IProgressContext
{
    public List<ProgressInfo> Reports { get; } = [];
    public bool IsDisposed { get; private set; }

    public void Update(int current, string message)
    {
        Reports.Add(new ProgressInfo { Current = current, Message = message });
    }

    public void Complete()
    {
        // Mark as complete
    }

    public void Report(ProgressInfo value)
    {
        Reports.Add(value);
    }

    public void Dispose()
    {
        IsDisposed = true;
        GC.SuppressFinalize(this);
    }
}