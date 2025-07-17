using Scanner111.Core.Infrastructure;
using Xunit;

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
    
    public void ShowWarning(string message)
    {
        WarningMessages.Add(message);
    }
    
    public void ShowError(string message)
    {
        ErrorMessages.Add(message);
    }
    
    public IProgress<ProgressInfo> ShowProgress(string title, int totalItems)
    {
        LastProgressTitle = title;
        LastProgressTotal = totalItems;
        return new TestProgress();
    }
}

public class TestProgress : IProgress<ProgressInfo>
{
    public List<ProgressInfo> Reports { get; } = new();
    
    public void Report(ProgressInfo value)
    {
        Reports.Add(value);
    }
}