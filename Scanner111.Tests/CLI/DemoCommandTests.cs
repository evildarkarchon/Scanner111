using Scanner111.CLI.Commands;
using Scanner111.CLI.Models;
using Scanner111.Core.Infrastructure;
using Scanner111.Tests.TestHelpers;

namespace Scanner111.Tests.CLI;

[Collection("Terminal UI Tests")]
public class DemoCommandTests : IDisposable
{
    private readonly TestMessageCapture _messageCapture;

    public DemoCommandTests()
    {
        _messageCapture = new TestMessageCapture();
        MessageHandler.Initialize(_messageCapture);
    }

    public void Dispose()
    {
        MessageHandler.Initialize(new TestMessageHandler());
    }

    [Fact]
    public async Task ExecuteAsync_DisplaysAllMessageTypesAndProgress()
    {
        // Arrange
        var command = new DemoCommand(_messageCapture);
        var options = new DemoOptions();

        // Act
        var result = await command.ExecuteAsync(options);

        // Assert
        result.Should().Be(0, "because the demo should complete successfully");

        // Verify all message types were displayed
        _messageCapture.InfoMessages.Should().Contain("This is an info message",
            "because demo should display info messages");
        _messageCapture.WarningMessages.Should().Contain("This is a warning message",
            "because demo should display warning messages");
        _messageCapture.ErrorMessages.Should().Contain("This is an error message",
            "because demo should display error messages");
        _messageCapture.SuccessMessages.Should().Contain("This is a success message",
            "because demo should display success messages");
        _messageCapture.DebugMessages.Should().Contain("This is a debug message",
            "because demo should display debug messages");
        _messageCapture.CriticalMessages.Should().Contain("This is a critical message",
            "because demo should display critical messages");

        // Verify completion message
        _messageCapture.SuccessMessages.Should().Contain("Demo complete!",
            "because demo should report completion");

        // Verify progress was created and completed
        _messageCapture.LastProgressContext.Should().NotBeNull("because progress context should be created");
        _messageCapture.LastProgressContext.Description.Should().Be("Demo Progress",
            "because correct progress description should be set");
        _messageCapture.LastProgressContext.Total.Should().Be(5,
            "because demo should have 5 total steps");
        _messageCapture.LastProgressContext.IsCompleted.Should().BeTrue(
            "because progress should be marked as completed");
    }
}