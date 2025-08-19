using Scanner111.CLI.Services;
using Scanner111.Core.Infrastructure;
using Scanner111.Tests.CLI.TestHelpers;
using Spectre.Console;
using Spectre.Console.Testing;

namespace Scanner111.Tests.CLI.Services;

[Collection("Terminal UI Tests")]
public class SpectreMessageHandlerTests
{
    private readonly TestConsole _console;
    private readonly SpectreMessageHandler _handler;

    public SpectreMessageHandlerTests()
    {
        _console = SpectreTestHelper.CreateTestConsole();
        AnsiConsole.Console = _console;
        _handler = new SpectreMessageHandler();
    }

    [Theory]
    [InlineData("ShowInfo", "This is an info message", "ℹ", "info messages include the info icon")]
    [InlineData("ShowWarning", "This is a warning", "⚠", "warning messages include the warning icon")]
    [InlineData("ShowError", "This is an error", "✗", "error messages include the error icon")]
    [InlineData("ShowSuccess", "Operation successful", "✓", "success messages include the success icon")]
    [InlineData("ShowDebug", "Debug information", "🐛", "debug messages include the debug icon")]
    [InlineData("ShowCritical", "Critical error!", "‼", "critical messages include the critical icon")]
    public void ShowMessage_WritesMessageWithCorrectFormatting(string methodName, string message, string expectedIcon,
        string reason)
    {
        // Act - Use reflection to call the appropriate method
        var method = _handler.GetType().GetMethod(methodName);
        method.Should().NotBeNull($"because {methodName} method should exist");
        method!.Invoke(_handler, new object[] { message, MessageTarget.All });

        // Assert
        var output = _console.Output;
        output.Should().Contain(expectedIcon, $"because {reason}");
        output.Should().Contain(message);
        // Note: Color markup is rendered as ANSI color codes, not literal text
    }

    [Fact]
    public void ShowMessage_WithDetails_CallsCorrectMethodBasedOnType()
    {
        // Act
        _handler.ShowMessage("Test message", "Additional details", MessageType.Warning);

        // Assert
        var output = _console.Output;
        output.Should().Contain("⚠", "because it's a warning type");
        output.Should().Contain("Test message");
        output.Should().Contain("Additional details");
    }

    [Fact]
    public void Messages_WithGuiOnlyTarget_AreIgnored()
    {
        // Act
        _handler.ShowInfo("GUI only message", MessageTarget.GuiOnly);
        _handler.ShowWarning("GUI only warning", MessageTarget.GuiOnly);

        // Assert
        var output = _console.Output;
        output.Should().NotContain("GUI only message", "because GUI-only messages should be ignored");
        output.Should().NotContain("GUI only warning");
    }

    [Fact]
    public void Messages_IncludeTimestamp()
    {
        // Act
        _handler.ShowInfo("Test message");

        // Assert
        var output = _console.Output;
        // Check for time format HH:mm:ss
        output.Should().MatchRegex(@"\d{2}:\d{2}:\d{2}", "because messages include timestamps");
    }

    [Fact]
    public void CreateProgressContext_ReturnsValidProgressContext()
    {
        // Act
        var context = _handler.CreateProgressContext("Test Progress", 100);

        // Assert
        context.Should().NotBeNull("because CreateProgressContext should return a valid context");
        context.Should().BeAssignableTo<IProgressContext>();
    }

    [Fact]
    public void CreateProgressContext_CanUpdateProgress()
    {
        // Act
        var context = _handler.CreateProgressContext("Test Progress", 100);

        // Update progress
        context.Update(50, "Half way there");

        // Assert - no exceptions thrown
        context.Should().NotBeNull("because context should remain valid after updates");
    }

    [Fact]
    public void CreateProgressContext_CanBeDisposed()
    {
        // Act
        IProgressContext? context = null;
        var exception = Record.Exception(() =>
        {
            using (context = _handler.CreateProgressContext("Test Progress", 100))
            {
                context.Update(25, "Quarter done");
            }
        });

        // Assert
        exception.Should().BeNull("because no exception should be thrown during disposal");
        context.Should().NotBeNull("because context was created successfully");
    }

    [Fact]
    public void ShowProgress_ReturnsValidProgress()
    {
        // Act
        var progress = _handler.ShowProgress("Test Progress", 100);

        // Assert
        progress.Should().NotBeNull("because ShowProgress should return a valid progress");
        progress.Should().BeAssignableTo<IProgress<ProgressInfo>>();
    }

    [Fact]
    public void ProgressContext_HandlesProgressInfoReports()
    {
        // Arrange
        var context = _handler.CreateProgressContext("Test Progress", 100);
        var progressInfo = new ProgressInfo
        {
            Current = 75,
            Total = 100,
            Message = "Almost done"
        };

        // Act
        var exception = Record.Exception(() => { context.Report(progressInfo); });

        // Assert
        exception.Should().BeNull("because reporting progress should not throw exceptions");
    }

    [Fact]
    public void MultipleMessages_MaintainOrder()
    {
        // Act
        _handler.ShowInfo("First message");
        _handler.ShowWarning("Second message");
        _handler.ShowError("Third message");

        // Assert
        var output = _console.Output;
        var firstIndex = output.IndexOf("First message");
        var secondIndex = output.IndexOf("Second message");
        var thirdIndex = output.IndexOf("Third message");

        firstIndex.Should().BeLessThan(secondIndex, "because first message should appear before second");
        secondIndex.Should().BeLessThan(thirdIndex, "because second message should appear before third");
    }

    [Fact]
    public void Messages_EscapeSpecialCharacters()
    {
        // Act
        _handler.ShowInfo("Message with [brackets] and special chars: <>&");

        // Assert
        var output = _console.Output;
        // The message should be properly escaped
        output.Should().Contain("Message with [brackets] and special chars: <>&",
            "because special characters should be escaped");
    }
}