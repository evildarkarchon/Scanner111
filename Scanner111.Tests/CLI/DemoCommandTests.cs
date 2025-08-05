using Scanner111.CLI.Commands;
using Scanner111.CLI.Models;
using Scanner111.Core.Infrastructure;
using Scanner111.Tests.TestHelpers;
using Xunit;

namespace Scanner111.Tests.CLI;

public class DemoCommandTests : IDisposable
{
    private readonly TestMessageCapture _messageCapture;

    public DemoCommandTests()
    {
        _messageCapture = new TestMessageCapture();
        MessageHandler.Initialize(_messageCapture);
    }
    [Fact]
    public async Task ExecuteAsync_DisplaysAllMessageTypesAndProgress()
    {
        // Arrange
        var command = new DemoCommand();
        var options = new DemoOptions();

        // Act
        var result = await command.ExecuteAsync(options);

        // Assert
        Assert.Equal(0, result);
        
        // Verify all message types were displayed
        Assert.Contains("This is an info message", _messageCapture.InfoMessages);
        Assert.Contains("This is a warning message", _messageCapture.WarningMessages);
        Assert.Contains("This is an error message", _messageCapture.ErrorMessages);
        Assert.Contains("This is a success message", _messageCapture.SuccessMessages);
        Assert.Contains("This is a debug message", _messageCapture.DebugMessages);
        Assert.Contains("This is a critical message", _messageCapture.CriticalMessages);
        
        // Verify completion message
        Assert.Contains("Demo complete!", _messageCapture.SuccessMessages);
        
        // Verify progress was created and completed
        Assert.NotNull(_messageCapture.LastProgressContext);
        Assert.Equal("Demo Progress", _messageCapture.LastProgressContext.Description);
        Assert.Equal(5, _messageCapture.LastProgressContext.Total);
        Assert.True(_messageCapture.LastProgressContext.IsCompleted);
    }

    public void Dispose()
    {
        MessageHandler.Initialize(new TestMessageHandler());
    }
}