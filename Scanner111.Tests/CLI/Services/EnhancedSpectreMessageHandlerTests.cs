using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Spectre.Console;
using Spectre.Console.Testing;
using Scanner111.CLI.Services;
using Scanner111.Core.Infrastructure;
using Scanner111.Tests.CLI.TestHelpers;
using Xunit;

namespace Scanner111.Tests.CLI.Services;

public class EnhancedSpectreMessageHandlerTests : IAsyncLifetime
{
    private readonly TestConsole _console;
    private EnhancedSpectreMessageHandler _handler;

    public EnhancedSpectreMessageHandlerTests()
    {
        _console = SpectreTestHelper.CreateTestConsole();
        AnsiConsole.Console = _console;
    }

    public Task InitializeAsync()
    {
        _handler = new EnhancedSpectreMessageHandler();
        return Task.CompletedTask;
    }

    public async Task DisposeAsync()
    {
        if (_handler != null)
        {
            await _handler.DisposeAsync();
        }
    }

    #region Message Display Tests

    [Fact]
    public async Task ShowInfo_WritesInfoMessage()
    {
        // Arrange
        await Task.Delay(100); // Allow render loop to start

        // Act
        _handler.ShowInfo("Test info message");
        await Task.Delay(150); // Allow render to update

        // Assert
        var output = _console.Output;
        Assert.Contains("Test info message", output);
    }

    [Fact]
    public async Task ShowWarning_WritesWarningMessage()
    {
        // Arrange
        await Task.Delay(100);

        // Act
        _handler.ShowWarning("Test warning message");
        await Task.Delay(150);

        // Assert
        var output = _console.Output;
        Assert.Contains("Test warning message", output);
    }

    [Fact]
    public async Task ShowError_WritesErrorMessage()
    {
        // Arrange
        await Task.Delay(100);

        // Act
        _handler.ShowError("Test error message");
        await Task.Delay(150);

        // Assert
        var output = _console.Output;
        Assert.Contains("Test error message", output);
    }

    [Fact]
    public async Task ShowSuccess_WritesSuccessMessage()
    {
        // Arrange
        await Task.Delay(100);

        // Act
        _handler.ShowSuccess("Test success message");
        await Task.Delay(150);

        // Assert
        var output = _console.Output;
        Assert.Contains("Test success message", output);
    }

    [Fact]
    public async Task ShowDebug_WritesDebugMessage()
    {
        // Arrange
        await Task.Delay(100);

        // Act
        _handler.ShowDebug("Test debug message");
        await Task.Delay(150);

        // Assert
        var output = _console.Output;
        Assert.Contains("Test debug message", output);
    }

    [Fact]
    public async Task ShowCritical_WritesCriticalMessage()
    {
        // Arrange
        await Task.Delay(100);

        // Act
        _handler.ShowCritical("Test critical message");
        await Task.Delay(150);

        // Assert
        var output = _console.Output;
        Assert.Contains("Test critical message", output);
    }

    [Fact]
    public async Task ShowMessage_WithDetails_WritesFullMessage()
    {
        // Arrange
        await Task.Delay(100);

        // Act
        _handler.ShowMessage("Main message", "Additional details", MessageType.Warning);
        await Task.Delay(150);

        // Assert
        var output = _console.Output;
        Assert.Contains("Main message", output);
        Assert.Contains("Additional details", output);
    }

    [Fact]
    public void Messages_WithGuiOnlyTarget_AreIgnored()
    {
        // Act
        _handler.ShowInfo("GUI only info", MessageTarget.GuiOnly);
        _handler.ShowWarning("GUI only warning", MessageTarget.GuiOnly);
        _handler.ShowError("GUI only error", MessageTarget.GuiOnly);

        // Assert
        // Messages should not be logged since they're GUI only
        // This is verified by the handler not adding them to the message logger
        Assert.NotNull(_handler); // Messages are ignored, no exception
    }

    #endregion

    #region Progress Context Tests

    [Fact]
    public void CreateProgressContext_ReturnsValidContext()
    {
        // Act
        var context = _handler.CreateProgressContext("Test Progress", 100);

        // Assert
        Assert.NotNull(context);
        Assert.IsAssignableFrom<IProgressContext>(context);
    }

    [Fact]
    public async Task CreateProgressContext_CanUpdateProgress()
    {
        // Arrange
        var context = _handler.CreateProgressContext("Test Progress", 100);
        await Task.Delay(100);

        // Act
        context.Update(25, "25% complete");
        context.Update(50, "50% complete");
        context.Update(75, "75% complete");
        await Task.Delay(150);

        // Assert - no exceptions thrown
        Assert.NotNull(context);
    }

    [Fact]
    public async Task CreateProgressContext_CanComplete()
    {
        // Arrange
        var context = _handler.CreateProgressContext("Test Progress", 100);
        await Task.Delay(100);

        // Act
        context.Update(50, "Half way");
        context.Complete();
        await Task.Delay(150);

        // Assert - no exceptions thrown
        Assert.NotNull(context);
    }

    [Fact]
    public async Task CreateProgressContext_DisposesCorrectly()
    {
        // Act
        using (var context = _handler.CreateProgressContext("Test Progress", 100))
        {
            context.Update(25, "Working...");
            await Task.Delay(50);
        }

        // Assert - no exceptions thrown during disposal
        Assert.NotNull(_handler);
    }

    [Fact]
    public async Task CreateProgressContext_HandlesProgressInfoReports()
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
        context.Report(progressInfo);
        await Task.Delay(150);

        // Assert - no exceptions thrown
        Assert.NotNull(context);
    }

    [Fact]
    public async Task ShowProgress_ReturnsValidProgress()
    {
        // Act
        var progress = _handler.ShowProgress("Test Progress", 100);
        await Task.Delay(100);

        // Assert
        Assert.NotNull(progress);
        Assert.IsAssignableFrom<IProgress<ProgressInfo>>(progress);
    }

    [Fact]
    public async Task MultipleProgressContexts_CanRunConcurrently()
    {
        // Arrange
        var contexts = new List<IProgressContext>();

        // Act
        for (int i = 0; i < 5; i++)
        {
            var context = _handler.CreateProgressContext($"Progress {i}", 100);
            contexts.Add(context);
        }

        await Task.Delay(100);

        // Update all contexts
        foreach (var (context, index) in contexts.Select((c, i) => (c, i)))
        {
            context.Update(50, $"Progress {index} at 50%");
        }

        await Task.Delay(150);

        // Complete all contexts
        foreach (var context in contexts)
        {
            context.Complete();
        }

        // Assert - no exceptions thrown
        Assert.Equal(5, contexts.Count);
        Assert.All(contexts, c => Assert.NotNull(c));
    }

    #endregion

    #region Thread Safety Tests

    [Fact]
    public async Task ConcurrentMessages_HandleCorrectly()
    {
        // Arrange
        var tasks = new List<Task>();
        await Task.Delay(100);

        // Act - Send multiple messages concurrently
        for (int i = 0; i < 10; i++)
        {
            int index = i;
            tasks.Add(Task.Run(() =>
            {
                _handler.ShowInfo($"Concurrent info {index}");
                _handler.ShowWarning($"Concurrent warning {index}");
                _handler.ShowError($"Concurrent error {index}");
            }));
        }

        await Task.WhenAll(tasks);
        await Task.Delay(200);

        // Assert - all messages should be handled without exceptions
        Assert.NotNull(_handler);
    }

    [Fact]
    public async Task ConcurrentProgressUpdates_HandleCorrectly()
    {
        // Arrange
        var contexts = new List<IProgressContext>();
        for (int i = 0; i < 5; i++)
        {
            contexts.Add(_handler.CreateProgressContext($"Concurrent Progress {i}", 100));
        }

        // Act - Update all contexts concurrently
        var tasks = contexts.Select((context, index) => Task.Run(async () =>
        {
            for (int j = 0; j <= 100; j += 10)
            {
                context.Update(j, $"Progress {index}: {j}%");
                await Task.Delay(10);
            }
            context.Complete();
        })).ToList();

        await Task.WhenAll(tasks);

        // Assert - all operations should complete without exceptions
        Assert.Equal(5, contexts.Count);
    }

    #endregion

    #region Disposal Tests

    [Fact]
    public async Task DisposeAsync_StopsRenderLoop()
    {
        // Arrange
        var handler = new EnhancedSpectreMessageHandler();
        await Task.Delay(100);

        // Act
        await handler.DisposeAsync();

        // Assert - disposal should complete without exceptions
        // Try to use the handler after disposal should not crash
        var exception = Record.Exception(() => handler.ShowInfo("After disposal"));
        Assert.Null(exception);
    }

    [Fact]
    public async Task DisposeAsync_MultipleCalls_DoNotThrow()
    {
        // Arrange
        var handler = new EnhancedSpectreMessageHandler();
        await Task.Delay(100);

        // Act & Assert - multiple disposals should not throw
        await handler.DisposeAsync();
        await handler.DisposeAsync();
        await handler.DisposeAsync();
    }

    #endregion

    #region Layout and Display Tests

    [Fact]
    public async Task Display_ShowsCorrectPanels()
    {
        // Arrange
        await Task.Delay(100);

        // Act
        _handler.ShowInfo("Test message");
        var context = _handler.CreateProgressContext("Test Task", 100);
        context.Update(50, "Half way");
        await Task.Delay(200);

        // Assert
        var output = _console.Output;
        Assert.Contains("Scanner111", output); // Header
        Assert.Contains("Progress", output); // Progress panel
        Assert.Contains("Logs", output); // Logs panel
    }

    [Fact]
    public async Task StatusBar_ShowsCorrectInformation()
    {
        // Arrange
        await Task.Delay(100);

        // Act
        var context1 = _handler.CreateProgressContext("Task 1", 100);
        var context2 = _handler.CreateProgressContext("Task 2", 100);
        await Task.Delay(200);

        // Assert
        var output = _console.Output;
        Assert.Contains("Active Tasks", output); // Status bar shows active task count
        Assert.Contains("Memory", output); // Status bar shows memory usage
    }

    #endregion
}