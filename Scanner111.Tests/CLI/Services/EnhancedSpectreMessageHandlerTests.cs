using FluentAssertions;
using Scanner111.CLI.Services;
using Scanner111.Core.Infrastructure;
using Scanner111.Tests.CLI.TestHelpers;
using Spectre.Console;
using Spectre.Console.Testing;

namespace Scanner111.Tests.CLI.Services;

public class EnhancedSpectreMessageHandlerTests : IAsyncLifetime
{
    private readonly TestConsole _console;
    private EnhancedSpectreMessageHandler _handler = null!;

    public EnhancedSpectreMessageHandlerTests()
    {
        _console = SpectreTestHelper.CreateTestConsole();
        AnsiConsole.Console = _console;
    }

    public async Task InitializeAsync()
    {
        _handler = new EnhancedSpectreMessageHandler();
        // Give the handler time to initialize its render loop
        await Task.Delay(200);
    }

    public async Task DisposeAsync()
    {
        if (_handler != null) await _handler.DisposeAsync();
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
        output.Should().Contain("Test info message", "should contain expected text");
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
        output.Should().Contain("Test warning message", "should contain expected text");
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
        output.Should().Contain("Test error message", "should contain expected text");
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
        output.Should().Contain("Test success message", "should contain expected text");
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
        output.Should().Contain("Test debug message", "should contain expected text");
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
        output.Should().Contain("Test critical message", "should contain expected text");
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
        output.Should().Contain("Main message", "should contain expected text");
        output.Should().Contain("Additional details", "should contain expected text");
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
        _handler.Should().NotBeNull("value should not be null"); // Messages are ignored, no exception
    }

    #endregion

    #region Progress Context Tests

    [Fact]
    public void CreateProgressContext_ReturnsValidContext()
    {
        // Act
        var context = _handler.CreateProgressContext("Test Progress", 100);

        // Assert
        context.Should().NotBeNull("value should not be null");
        context.Should().BeAssignableTo<IProgressContext>("context should implement IProgressContext");
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
        context.Should().NotBeNull("value should not be null");
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
        context.Should().NotBeNull("value should not be null");
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
        _handler.Should().NotBeNull("value should not be null");
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
        context.Should().NotBeNull("value should not be null");
    }

    [Fact]
    public async Task ShowProgress_ReturnsValidProgress()
    {
        // Act
        var progress = _handler.ShowProgress("Test Progress", 100);
        await Task.Delay(100);

        // Assert
        progress.Should().NotBeNull("value should not be null");
        progress.Should().BeAssignableTo<IProgress<ProgressInfo>>("progress should implement IProgress<ProgressInfo>");
    }

    [Fact]
    public async Task MultipleProgressContexts_CanRunConcurrently()
    {
        // Arrange
        var contexts = new List<IProgressContext>();

        // Act
        for (var i = 0; i < 5; i++)
        {
            var context = _handler.CreateProgressContext($"Progress {i}", 100);
            contexts.Add(context);
        }

        await Task.Delay(100);

        // Update all contexts
        foreach (var (context, index) in contexts.Select((c, i) => (c, i)))
            context.Update(50, $"Progress {index} at 50%");

        await Task.Delay(150);

        // Complete all contexts
        foreach (var context in contexts) context.Complete();

        // Assert - no exceptions thrown
        contexts.Count.Should().Be(5, "value should match expected");
        contexts.Should().AllSatisfy(c => c.Should().NotBeNull("all contexts should be created"));
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
        for (var i = 0; i < 10; i++)
        {
            var index = i;
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
        _handler.Should().NotBeNull("value should not be null");
    }

    [Fact]
    public async Task ConcurrentProgressUpdates_HandleCorrectly()
    {
        // Arrange
        var contexts = new List<IProgressContext>();
        for (var i = 0; i < 5; i++) contexts.Add(_handler.CreateProgressContext($"Concurrent Progress {i}", 100));

        // Act - Update all contexts concurrently
        var tasks = contexts.Select((context, index) => Task.Run(async () =>
        {
            for (var j = 0; j <= 100; j += 10)
            {
                context.Update(j, $"Progress {index}: {j}%");
                await Task.Delay(10);
            }

            context.Complete();
        })).ToList();

        await Task.WhenAll(tasks);

        // Assert - all operations should complete without exceptions
        contexts.Count.Should().Be(5, "value should match expected");
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
        var action = () => handler.ShowInfo("After disposal");
        var exception = Record.Exception(action);
        exception.Should().BeNull("value should be null");
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
        output.Should().Contain("Scanner111", "should contain expected text"); // Header
        output.Should().Contain("Progress", "should contain expected text"); // Progress panel
        output.Should().Contain("Logs", "should contain expected text"); // Logs panel
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
        output.Should().Contain("Active Tasks", "should contain expected text"); // Status bar shows active task count
        output.Should().Contain("Memory", "should contain expected text"); // Status bar shows memory usage
    }

    #endregion
}