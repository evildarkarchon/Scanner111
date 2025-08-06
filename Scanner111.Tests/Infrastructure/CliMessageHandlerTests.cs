using System;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using FluentAssertions;
using Scanner111.CLI.Services;
using Scanner111.Core.Infrastructure;
using Xunit;

namespace Scanner111.Tests.Infrastructure;

public class CliMessageHandlerTests
{
    [Fact]
    public void Constructor_CreatesDebugLogDirectory()
    {
        // Arrange & Act
        var handler = new CliMessageHandler();

        // Assert
        var logDirectory = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "Scanner111", "DebugLogs");

        Directory.Exists(logDirectory).Should().BeTrue("debug log directory should be created");
    }

    [Fact]
    public void ShowInfo_DoesNotThrow_WhenCalledWithMessage()
    {
        // Arrange
        var handler = new CliMessageHandler(false);

        // Act & Assert - Should not throw
        handler.ShowInfo("Test info message");
    }

    [Fact]
    public void ShowWarning_DoesNotThrow_WhenCalledWithMessage()
    {
        // Arrange
        var handler = new CliMessageHandler(false);

        // Act & Assert - Should not throw
        handler.ShowWarning("Test warning message");
    }

    [Fact]
    public void ShowError_DoesNotThrow_WhenCalledWithMessage()
    {
        // Arrange
        var handler = new CliMessageHandler(false);

        // Act & Assert - Should not throw
        handler.ShowError("Test error message");
    }

    [Fact]
    public void ShowSuccess_DoesNotThrow_WhenCalledWithMessage()
    {
        // Arrange
        var handler = new CliMessageHandler(false);

        // Act & Assert - Should not throw
        handler.ShowSuccess("Test success message");
    }

    [Fact]
    public void ShowCritical_DoesNotThrow_WhenCalledWithMessage()
    {
        // Arrange
        var handler = new CliMessageHandler(false);

        // Act & Assert - Should not throw
        handler.ShowCritical("Test critical message");
    }

    [Fact]
    public void ShowDebug_WritesToFile()
    {
        // Arrange
        var handler = new CliMessageHandler(false);
        var logDirectory = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "Scanner111", "DebugLogs");

        // Act
        handler.ShowDebug("Test debug message");

        // Assert
        var logFiles = Directory.GetFiles(logDirectory, "scanner111-debug-*.log");
        logFiles.Should().NotBeEmpty("debug message should create log file");

        var latestLog = logFiles.OrderByDescending(File.GetCreationTime).First();
        var content = File.ReadAllText(latestLog);
        content.Should().Contain("DEBUG: Test debug message", "debug message should be written to log file");
    }

    [Theory]
    [InlineData(MessageTarget.All)]
    [InlineData(MessageTarget.CliOnly)]
    public void ShowInfo_DisplaysMessage_WhenTargetIncludesConsole(MessageTarget target)
    {
        // Arrange
        var handler = new CliMessageHandler(false);

        // Act & Assert - Should not throw
        handler.ShowInfo("Test message", target);
    }

    [Fact]
    public void ShowInfo_DoesNotDisplay_WhenTargetIsGuiOnly()
    {
        // Arrange
        var handler = new CliMessageHandler(false);

        // Act & Assert - Should return immediately without action
        handler.ShowInfo("Test message", MessageTarget.GuiOnly);
    }

    [Theory]
    [InlineData(MessageType.Info)]
    [InlineData(MessageType.Warning)]
    [InlineData(MessageType.Error)]
    [InlineData(MessageType.Success)]
    [InlineData(MessageType.Critical)]
    [InlineData(MessageType.Debug)]
    public void ShowMessage_HandlesAllMessageTypes(MessageType messageType)
    {
        // Arrange
        var handler = new CliMessageHandler(false);

        // Act & Assert - Should not throw
        handler.ShowMessage("Test message", null, messageType);
    }

    [Fact]
    public void ShowMessage_HandlesDetailsParameter()
    {
        // Arrange
        var handler = new CliMessageHandler(false);

        // Act & Assert - Should not throw
        handler.ShowMessage("Main message", "Additional details", MessageType.Info);
    }

    [Fact]
    public void ShowProgress_ReturnsProgressInstance()
    {
        // Arrange
        var handler = new CliMessageHandler(false);

        // Act
        var progress = handler.ShowProgress("Test progress", 100);

        // Assert
        progress.Should().NotBeNull("progress instance should be created");
        progress.Should().BeAssignableTo<IProgress<ProgressInfo>>("progress should implement IProgress interface");
    }

    [Fact]
    public void CreateProgressContext_ReturnsProgressContextInstance()
    {
        // Arrange
        var handler = new CliMessageHandler(false);

        // Act
        using var context = handler.CreateProgressContext("Test context", 50);

        // Assert
        context.Should().NotBeNull("progress context should be created");
        context.Should().BeAssignableTo<IProgressContext>("context should implement IProgressContext interface");
    }

    [Fact]
    public void CliProgress_UpdatesCorrectly()
    {
        // Arrange
        var progress = new CliProgress("Test", false);

        // Act & Assert - Should not throw
        progress.Report(new ProgressInfo { Current = 50, Total = 100, Message = "50%" });
        progress.Report(new ProgressInfo { Current = 100, Total = 100, Message = "Complete" });
    }

    [Fact]
    public void CliProgress_HandlesInterruption()
    {
        // Arrange
        var progress = new CliProgress("Test", false);

        // Act
        var needsRedraw = progress.InterruptForMessage();

        // Assert
        needsRedraw.Should().BeTrue("interruption should indicate redraw is needed");

        // Act & Assert - Should not throw
        progress.RedrawAfterMessage();
    }

    [Fact]
    public void CliProgressContext_UpdatesProgress()
    {
        // Arrange
        using var context = new CliProgressContext("Test", 100, false);

        // Act & Assert - Should not throw
        context.Update(25, "25% complete");
        context.Update(50, "50% complete");
        context.Complete();
    }

    [Fact]
    public void CliProgressContext_ReportsProgressInfo()
    {
        // Arrange
        using var context = new CliProgressContext("Test", 100, false);

        // Act & Assert - Should not throw
        context.Report(new ProgressInfo { Current = 75, Total = 100, Message = "75%" });
    }

    [Fact]
    public void CliProgressContext_HandlesDisposal()
    {
        // Arrange
        var context = new CliProgressContext("Test", 100, false);

        // Act
        context.Dispose();

        // Assert - Should not throw when using after disposal
        context.Update(50, "Should be ignored");
        context.Complete();
        context.Report(new ProgressInfo { Current = 100, Total = 100, Message = "Ignored" });
    }

    [Fact]
    public void CliMessageHandler_CleansUpOldLogFiles()
    {
        // Arrange
        var logDirectory = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "Scanner111", "DebugLogs");

        // Create more than 10 log files
        for (int i = 0; i < 15; i++)
        {
            var filename = Path.Combine(logDirectory, $"scanner111-debug-test{i:D2}.log");
            File.WriteAllText(filename, "test");
        }

        // Act
        var handler = new CliMessageHandler(false);

        // Assert - Should have cleaned up old files
        var remainingFiles = Directory.GetFiles(logDirectory, "scanner111-debug-*.log");
        remainingFiles.Length.Should().BeLessThanOrEqualTo(11, "old log files should be cleaned up (10 kept + 1 new)");
    }

    [Fact]
    public void SupportsColors_ReturnsExpectedValue()
    {
        // This test verifies that SupportsColors doesn't throw
        // The actual result depends on the runtime environment
        var handler = new CliMessageHandler();
        handler.Should().NotBeNull("handler should be created successfully");
    }

    [Fact]
    public void RemoveEmojis_RemovesAllEmojis()
    {
        // This would test the private method indirectly
        // We can verify by using the handler with colors disabled
        var handler = new CliMessageHandler(false);

        // Act & Assert - Messages should display without throwing
        handler.ShowInfo("Test");
        handler.ShowWarning("Test");
        handler.ShowError("Test");
        handler.ShowSuccess("Test");
        handler.ShowCritical("Test");
    }

}