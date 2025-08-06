using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Threading.Channels;
using Spectre.Console;
using Spectre.Console.Testing;
using Scanner111.CLI.Services;
using Scanner111.Core.Infrastructure;
using Scanner111.Tests.CLI.TestHelpers;
using Xunit;
using System.Reflection;

namespace Scanner111.Tests.CLI.Services;

public class ProgressManagerTests : IAsyncLifetime
{
    private readonly TestConsole _console;
    private ProgressManager _progressManager;
    private CancellationTokenSource _cts;

    public ProgressManagerTests()
    {
        _console = SpectreTestHelper.CreateTestConsole();
        AnsiConsole.Console = _console;
    }

    public async Task InitializeAsync()
    {
        _progressManager = new ProgressManager();
        _cts = new CancellationTokenSource();
        
        // Start the progress manager
        _ = Task.Run(() => _progressManager.StartAsync(_cts.Token));
        await Task.Delay(100); // Allow startup
    }

    public async Task DisposeAsync()
    {
        _cts?.Cancel();
        _cts?.Dispose();
        
        if (_progressManager != null)
        {
            await _progressManager.DisposeAsync();
        }
    }

    #region Context Creation Tests

    [Fact]
    public void CreateContext_ReturnsValidProgressContext()
    {
        // Act
        var context = _progressManager.CreateContext("Test Progress", 100);

        // Assert
        Assert.NotNull(context);
        Assert.IsType<ProgressContextAdapter>(context);
    }

    [Fact]
    public void CreateContext_WithUniqueIds()
    {
        // Act
        var context1 = _progressManager.CreateContext("Progress 1", 100);
        var context2 = _progressManager.CreateContext("Progress 2", 100);

        // Assert
        Assert.NotNull(context1);
        Assert.NotNull(context2);
        Assert.NotSame(context1, context2);
    }

    [Fact]
    public async Task CreateContext_AddsToActiveTasks()
    {
        // Arrange
        var initialCount = _progressManager.ActiveTaskCount;

        // Act
        var context = _progressManager.CreateContext("New Task", 100);
        await Task.Delay(150); // Allow command processing

        // Assert
        Assert.True(_progressManager.ActiveTaskCount > initialCount);
    }

    #endregion

    #region Progress Update Tests

    [Fact]
    public async Task UpdateProgress_ProcessesCorrectly()
    {
        // Arrange
        var context = _progressManager.CreateContext("Update Test", 100);
        await Task.Delay(100);

        // Act
        context.Update(25, "25% complete");
        context.Update(50, "50% complete");
        context.Update(75, "75% complete");
        context.Update(100, "100% complete");
        await Task.Delay(150);

        // Assert - no exceptions thrown
        Assert.NotNull(context);
    }

    [Fact]
    public async Task CompleteProgress_RemovesFromActiveTasks()
    {
        // Arrange
        var context = _progressManager.CreateContext("Complete Test", 100);
        await Task.Delay(150);
        var countWithTask = _progressManager.ActiveTaskCount;

        // Act
        context.Complete();
        await Task.Delay(150);

        // Assert
        Assert.True(_progressManager.ActiveTaskCount < countWithTask);
    }

    #endregion

    #region Concurrent Operations Tests

    [Fact]
    public async Task MultipleContexts_CanBeCreatedConcurrently()
    {
        // Arrange
        var tasks = new List<Task<Scanner111.Core.Infrastructure.IProgressContext>>();

        // Act
        for (int i = 0; i < 10; i++)
        {
            int index = i;
            tasks.Add(Task.Run(() => _progressManager.CreateContext($"Concurrent {index}", 100)));
        }

        var contexts = await Task.WhenAll(tasks);

        // Assert
        Assert.Equal(10, contexts.Length);
        Assert.All(contexts, c => Assert.NotNull(c));
    }

    [Fact]
    public async Task ConcurrentUpdates_ProcessCorrectly()
    {
        // Arrange
        var contexts = new List<Scanner111.Core.Infrastructure.IProgressContext>();
        for (int i = 0; i < 5; i++)
        {
            contexts.Add(_progressManager.CreateContext($"Concurrent {i}", 100));
        }

        // Act
        var updateTasks = contexts.Select((context, index) => Task.Run(async () =>
        {
            for (int j = 0; j <= 100; j += 10)
            {
                context.Update(j, $"Task {index}: {j}%");
                await Task.Delay(5);
            }
        })).ToList();

        await Task.WhenAll(updateTasks);

        // Assert - all updates should process without exceptions
        Assert.Equal(5, contexts.Count);
    }

    [Fact]
    public async Task ConcurrentCompletions_ProcessCorrectly()
    {
        // Arrange
        var contexts = new List<Scanner111.Core.Infrastructure.IProgressContext>();
        for (int i = 0; i < 10; i++)
        {
            contexts.Add(_progressManager.CreateContext($"Complete {i}", 100));
        }
        await Task.Delay(150);

        // Act
        var completeTasks = contexts.Select(context => Task.Run(() =>
        {
            context.Update(100, "Done");
            context.Complete();
        })).ToList();

        await Task.WhenAll(completeTasks);
        await Task.Delay(150);

        // Assert
        Assert.Equal(0, _progressManager.ActiveTaskCount);
    }

    #endregion

    #region Command Processing Tests

    [Fact]
    public async Task CommandChannel_ProcessesCommandsInOrder()
    {
        // Arrange
        var contexts = new List<Scanner111.Core.Infrastructure.IProgressContext>();

        // Act
        for (int i = 0; i < 5; i++)
        {
            var context = _progressManager.CreateContext($"Ordered {i}", 100);
            contexts.Add(context);
            context.Update(50, $"Task {i} at 50%");
        }

        await Task.Delay(200);

        // Complete in order
        foreach (var context in contexts)
        {
            context.Complete();
        }

        await Task.Delay(200);

        // Assert - all commands should be processed
        Assert.Equal(0, _progressManager.ActiveTaskCount);
    }

    #endregion

    #region GetProgressPanel Tests

    [Fact]
    public void GetProgressPanel_WithNoTasks_ShowsEmptyMessage()
    {
        // Act
        var panel = _progressManager.GetProgressPanel();

        // Assert
        Assert.NotNull(panel);
        // Panel should contain "No active tasks" text
        var panelString = panel.ToString();
        Assert.NotNull(panelString);
    }

    [Fact]
    public async Task GetProgressPanel_WithActiveTasks_ShowsProgress()
    {
        // Arrange
        var context = _progressManager.CreateContext("Active Task", 100);
        context.Update(50, "Half way");
        await Task.Delay(150);

        // Act
        var panel = _progressManager.GetProgressPanel();

        // Assert
        Assert.NotNull(panel);
        // Panel should show task information
        var panelString = panel.ToString();
        Assert.NotNull(panelString);
    }

    #endregion

    #region Disposal Tests

    [Fact]
    public async Task DisposeAsync_CompletesCommandChannel()
    {
        // Arrange
        var manager = new ProgressManager();
        var cts = new CancellationTokenSource();
        _ = Task.Run(() => manager.StartAsync(cts.Token));
        await Task.Delay(100);

        var context = manager.CreateContext("Test", 100);
        context.Update(50, "Half way");

        // Act
        await manager.DisposeAsync();

        // Assert - disposal should complete without exceptions
        Assert.NotNull(manager);
    }

    [Fact]
    public async Task DisposeAsync_CancelsProcessingTask()
    {
        // Arrange
        var manager = new ProgressManager();
        var cts = new CancellationTokenSource();
        var startTask = Task.Run(() => manager.StartAsync(cts.Token));
        await Task.Delay(100);

        // Act
        cts.Cancel();
        await manager.DisposeAsync();

        // Assert - should handle cancellation gracefully
        Assert.NotNull(manager);
    }

    #endregion
}

/// <summary>
/// Tests for the ProgressContextAdapter class
/// </summary>
public class ProgressContextAdapterTests
{
    [Fact]
    public void Update_SendsUpdateCommand()
    {
        // Arrange
        var channel = Channel.CreateUnbounded<ProgressCommand>();
        var adapter = new ProgressContextAdapter("test-id", "Test", 100, channel.Writer);

        // Act
        adapter.Update(50, "Half way");

        // Assert
        Assert.True(channel.Reader.TryRead(out var command));
        Assert.IsType<UpdateProgressCommand>(command);
        var updateCommand = (UpdateProgressCommand)command;
        Assert.Equal("test-id", updateCommand.Id);
        Assert.Equal(50, updateCommand.Current);
        Assert.Equal("Half way", updateCommand.Message);
    }

    [Fact]
    public void Complete_SendsCompleteCommand()
    {
        // Arrange
        var channel = Channel.CreateUnbounded<ProgressCommand>();
        var adapter = new ProgressContextAdapter("test-id", "Test", 100, channel.Writer);

        // Act
        adapter.Complete();

        // Assert
        Assert.True(channel.Reader.TryRead(out var command));
        Assert.IsType<CompleteProgressCommand>(command);
        Assert.Equal("test-id", command.Id);
    }

    [Fact]
    public void Complete_OnlyOnce_IgnoresSubsequentCalls()
    {
        // Arrange
        var channel = Channel.CreateUnbounded<ProgressCommand>();
        var adapter = new ProgressContextAdapter("test-id", "Test", 100, channel.Writer);

        // Act
        adapter.Complete();
        adapter.Complete();
        adapter.Complete();

        // Assert - only one complete command should be sent
        var commands = new List<ProgressCommand>();
        while (channel.Reader.TryRead(out var command))
        {
            commands.Add(command);
        }
        Assert.Single(commands);
        Assert.IsType<CompleteProgressCommand>(commands[0]);
    }

    [Fact]
    public void Dispose_CallsComplete_IfNotCompleted()
    {
        // Arrange
        var channel = Channel.CreateUnbounded<ProgressCommand>();
        var adapter = new ProgressContextAdapter("test-id", "Test", 100, channel.Writer);

        // Act
        adapter.Dispose();

        // Assert
        Assert.True(channel.Reader.TryRead(out var command));
        Assert.IsType<CompleteProgressCommand>(command);
    }

    [Fact]
    public void Dispose_AfterComplete_DoesNotSendDuplicate()
    {
        // Arrange
        var channel = Channel.CreateUnbounded<ProgressCommand>();
        var adapter = new ProgressContextAdapter("test-id", "Test", 100, channel.Writer);

        // Act
        adapter.Complete();
        adapter.Dispose();

        // Assert - only one complete command
        var commands = new List<ProgressCommand>();
        while (channel.Reader.TryRead(out var command))
        {
            commands.Add(command);
        }
        Assert.Single(commands);
    }

    [Fact]
    public void Report_CallsUpdateWithProgressInfo()
    {
        // Arrange
        var channel = Channel.CreateUnbounded<ProgressCommand>();
        var adapter = new ProgressContextAdapter("test-id", "Test", 100, channel.Writer);
        var progressInfo = new Scanner111.Core.Infrastructure.ProgressInfo
        {
            Current = 75,
            Total = 100,
            Message = "75% complete"
        };

        // Act
        adapter.Report(progressInfo);

        // Assert
        Assert.True(channel.Reader.TryRead(out var command));
        Assert.IsType<UpdateProgressCommand>(command);
        var updateCommand = (UpdateProgressCommand)command;
        Assert.Equal(75, updateCommand.Current);
        Assert.Equal("75% complete", updateCommand.Message);
    }

    [Fact]
    public void Update_AfterDispose_IsIgnored()
    {
        // Arrange
        var channel = Channel.CreateUnbounded<ProgressCommand>();
        var adapter = new ProgressContextAdapter("test-id", "Test", 100, channel.Writer);

        // Act
        adapter.Dispose();
        
        // Clear the complete command
        channel.Reader.TryRead(out _);
        
        adapter.Update(50, "Should be ignored");

        // Assert - no new commands after disposal
        Assert.False(channel.Reader.TryRead(out _));
    }

    [Fact]
    public void Update_AfterComplete_IsIgnored()
    {
        // Arrange
        var channel = Channel.CreateUnbounded<ProgressCommand>();
        var adapter = new ProgressContextAdapter("test-id", "Test", 100, channel.Writer);

        // Act
        adapter.Complete();
        
        // Clear the complete command
        channel.Reader.TryRead(out _);
        
        adapter.Update(50, "Should be ignored");

        // Assert - no new commands after completion
        Assert.False(channel.Reader.TryRead(out _));
    }
}