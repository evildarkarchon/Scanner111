using Spectre.Console;
using Spectre.Console.Testing;
using Scanner111.CLI.Services;
using Scanner111.Core.Infrastructure;
using Scanner111.Tests.CLI.TestHelpers;
using Xunit;

namespace Scanner111.Tests.CLI.Services;

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

    [Fact]
    public void ShowInfo_WritesInfoMessageWithCorrectFormatting()
    {
        // Act
        _handler.ShowInfo("This is an info message");

        // Assert
        var output = _console.Output;
        Assert.Contains("ℹ", output);
        Assert.Contains("This is an info message", output);
        // Note: [blue] markup is rendered as ANSI color codes, not literal text
    }

    [Fact]
    public void ShowWarning_WritesWarningMessageWithCorrectFormatting()
    {
        // Act
        _handler.ShowWarning("This is a warning");

        // Assert
        var output = _console.Output;
        Assert.Contains("⚠", output);
        Assert.Contains("This is a warning", output);
        // Note: [yellow] markup is rendered as ANSI color codes, not literal text
    }

    [Fact]
    public void ShowError_WritesErrorMessageWithCorrectFormatting()
    {
        // Act
        _handler.ShowError("This is an error");

        // Assert
        var output = _console.Output;
        Assert.Contains("✗", output);
        Assert.Contains("This is an error", output);
        // Note: [red] markup is rendered as ANSI color codes, not literal text
    }

    [Fact]
    public void ShowSuccess_WritesSuccessMessageWithCorrectFormatting()
    {
        // Act
        _handler.ShowSuccess("Operation successful");

        // Assert
        var output = _console.Output;
        Assert.Contains("✓", output);
        Assert.Contains("Operation successful", output);
        // Note: [green] markup is rendered as ANSI color codes, not literal text
    }

    [Fact]
    public void ShowDebug_WritesDebugMessageWithCorrectFormatting()
    {
        // Act
        _handler.ShowDebug("Debug information");

        // Assert
        var output = _console.Output;
        Assert.Contains("🐛", output);
        Assert.Contains("Debug information", output);
        // Note: [dim] markup is rendered as ANSI color codes, not literal text
    }

    [Fact]
    public void ShowCritical_WritesCriticalMessageWithCorrectFormatting()
    {
        // Act
        _handler.ShowCritical("Critical error!");

        // Assert
        var output = _console.Output;
        Assert.Contains("‼", output);
        Assert.Contains("Critical error!", output);
        // Note: [bold red] markup is rendered as ANSI color codes, not literal text
    }

    [Fact]
    public void ShowMessage_WithDetails_CallsCorrectMethodBasedOnType()
    {
        // Act
        _handler.ShowMessage("Test message", "Additional details", MessageType.Warning);

        // Assert
        var output = _console.Output;
        Assert.Contains("⚠", output);
        Assert.Contains("Test message", output);
        Assert.Contains("Additional details", output);
    }

    [Fact]
    public void Messages_WithGuiOnlyTarget_AreIgnored()
    {
        // Act
        _handler.ShowInfo("GUI only message", MessageTarget.GuiOnly);
        _handler.ShowWarning("GUI only warning", MessageTarget.GuiOnly);

        // Assert
        var output = _console.Output;
        Assert.DoesNotContain("GUI only message", output);
        Assert.DoesNotContain("GUI only warning", output);
    }

    [Fact]
    public void Messages_IncludeTimestamp()
    {
        // Act
        _handler.ShowInfo("Test message");

        // Assert
        var output = _console.Output;
        // Check for time format HH:mm:ss
        Assert.Matches(@"\d{2}:\d{2}:\d{2}", output);
    }

    [Fact]
    public void CreateProgressContext_ReturnsValidProgressContext()
    {
        // Act
        var context = _handler.CreateProgressContext("Test Progress", 100);

        // Assert
        Assert.NotNull(context);
        Assert.IsAssignableFrom<IProgressContext>(context);
    }

    [Fact]
    public void CreateProgressContext_CanUpdateProgress()
    {
        // Act
        var context = _handler.CreateProgressContext("Test Progress", 100);
        
        // Update progress
        context.Update(50, "Half way there");
        
        // Assert - no exceptions thrown
        Assert.NotNull(context);
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
        Assert.Null(exception);
        Assert.NotNull(context);
    }

    [Fact]
    public void ShowProgress_ReturnsValidProgress()
    {
        // Act
        var progress = _handler.ShowProgress("Test Progress", 100);

        // Assert
        Assert.NotNull(progress);
        Assert.IsAssignableFrom<IProgress<ProgressInfo>>(progress);
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
        var exception = Record.Exception(() =>
        {
            context.Report(progressInfo);
        });

        // Assert
        Assert.Null(exception);
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

        Assert.True(firstIndex < secondIndex);
        Assert.True(secondIndex < thirdIndex);
    }

    [Fact]
    public void Messages_EscapeSpecialCharacters()
    {
        // Act
        _handler.ShowInfo("Message with [brackets] and special chars: <>&");

        // Assert
        var output = _console.Output;
        // The message should be properly escaped
        Assert.Contains("Message with [brackets] and special chars: <>&", output);
    }
}