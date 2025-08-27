using FluentAssertions;
using Microsoft.Extensions.Logging;
using NSubstitute;
using Scanner111.Core.Analysis;
using Scanner111.Core.Models;
using Scanner111.Core.Reporting;

namespace Scanner111.Test.Analysis;

public class FcxModeHandlerTests : IDisposable
{
    private readonly List<FcxModeHandler> _handlers = new();
    private readonly ILogger<FcxModeHandler> _logger;

    public FcxModeHandlerTests()
    {
        _logger = Substitute.For<ILogger<FcxModeHandler>>();
        // Reset static state before each test
        CreateHandler(false).ResetFcxChecks();
    }

    public void Dispose()
    {
        // Clean up handlers
        foreach (var handler in _handlers) handler.Dispose();
        _handlers.Clear();
    }

    private FcxModeHandler CreateHandler(bool? fcxMode = true)
    {
        var modSettings = new ModDetectionSettings
        {
            FcxMode = fcxMode,
            XseModules = new HashSet<string> { "test.dll" }
        };

        var handler = new FcxModeHandler(_logger, modSettings);
        _handlers.Add(handler);
        return handler;
    }

    [Fact]
    public async Task CheckFcxModeAsync_ShouldRunChecks_WhenFcxModeEnabled()
    {
        // Arrange
        var handler = CreateHandler();

        // Act
        await handler.CheckFcxModeAsync();

        // Assert
        handler.MainFilesCheck.Should().NotBeNullOrEmpty();
        handler.GameFilesCheck.Should().NotBeNullOrEmpty();
        handler.MainFilesCheck.Should().Contain("✔️");
    }

    [Fact]
    public async Task CheckFcxModeAsync_ShouldSkipChecks_WhenFcxModeDisabled()
    {
        // Arrange
        var handler = CreateHandler(false);

        // Act
        await handler.CheckFcxModeAsync();

        // Assert
        handler.MainFilesCheck.Should().Contain("FCX Mode is disabled");
        handler.GameFilesCheck.Should().BeEmpty();
    }

    [Fact]
    public async Task CheckFcxModeAsync_ShouldUseCachedResults_OnSubsequentCalls()
    {
        // Arrange
        var handler1 = CreateHandler();
        var handler2 = CreateHandler();

        // Act - First call
        await handler1.CheckFcxModeAsync();
        var firstMainResult = handler1.MainFilesCheck;
        var firstGameResult = handler1.GameFilesCheck;

        // Act - Second call from different instance
        await handler2.CheckFcxModeAsync();

        // Assert - Should get same cached results
        handler2.MainFilesCheck.Should().Be(firstMainResult);
        handler2.GameFilesCheck.Should().Be(firstGameResult);
    }

    [Fact]
    public async Task CheckFcxModeAsync_ShouldBeThreadSafe_WithConcurrentCalls()
    {
        // Arrange
        const int concurrentTasks = 10;
        var handlers = Enumerable.Range(0, concurrentTasks)
            .Select(_ => CreateHandler())
            .ToList();

        // Act - Run checks concurrently
        var tasks = handlers.Select(h => h.CheckFcxModeAsync()).ToList();
        await Task.WhenAll(tasks);

        // Assert - All handlers should have the same results
        var expectedMain = handlers[0].MainFilesCheck;
        var expectedGame = handlers[0].GameFilesCheck;

        foreach (var handler in handlers)
        {
            handler.MainFilesCheck.Should().Be(expectedMain);
            handler.GameFilesCheck.Should().Be(expectedGame);
        }
    }

    [Fact]
    public async Task ResetFcxChecks_ShouldClearCachedResults()
    {
        // Arrange
        var handler = CreateHandler();

        // Act - Run checks first
        await handler.CheckFcxModeAsync();
        handler.MainFilesCheck.Should().NotBeNullOrEmpty();

        // Act - Reset
        handler.ResetFcxChecks();

        // Assert - Internal state should be cleared (next check will run fresh)
        var handler2 = CreateHandler();
        await handler2.CheckFcxModeAsync();
        // The check should run fresh (we can't directly test internal state, but behavior should be consistent)
        handler2.MainFilesCheck.Should().NotBeNullOrEmpty();
    }

    [Fact]
    public async Task GetFcxMessages_ShouldReturnEnabledMessage_WhenFcxModeIsTrue()
    {
        // Arrange
        var handler = CreateHandler();
        await handler.CheckFcxModeAsync();

        // Act
        var fragment = handler.GetFcxMessages();

        // Assert
        fragment.Should().NotBeNull();
        fragment.Type.Should().Be(FragmentType.Info);
        fragment.Content.Should().Contain("FCX MODE IS ENABLED");
        fragment.Content.Should().Contain("Scanner111 MUST BE RUN BY THE ORIGINAL USER");
    }

    [Fact]
    public async Task GetFcxMessages_ShouldReturnDisabledMessage_WhenFcxModeIsFalse()
    {
        // Arrange
        var handler = CreateHandler(false);
        await handler.CheckFcxModeAsync();

        // Act
        var fragment = handler.GetFcxMessages();

        // Assert
        fragment.Should().NotBeNull();
        fragment.Type.Should().Be(FragmentType.Info);
        fragment.Content.Should().Contain("FCX MODE IS DISABLED");
        fragment.Content.Should().Contain("YOU CAN ENABLE IT");
    }

    [Fact]
    public async Task CheckFcxModeAsync_ShouldHandleCancellation()
    {
        // Arrange
        var handler = CreateHandler();
        using var cts = new CancellationTokenSource();
        cts.Cancel();

        // Act & Assert
        await Assert.ThrowsAsync<TaskCanceledException>(() => handler.CheckFcxModeAsync(cts.Token));
    }

    [Fact]
    public void Dispose_ShouldNotThrow_WhenCalledMultipleTimes()
    {
        // Arrange
        var handler = CreateHandler();

        // Act & Assert - Should not throw
        handler.Dispose();
        handler.Dispose();
        handler.Dispose();
    }

    [Fact]
    public async Task CheckFcxModeAsync_ShouldThrow_WhenDisposed()
    {
        // Arrange
        var handler = CreateHandler();
        handler.Dispose();

        // Act & Assert
        await Assert.ThrowsAsync<ObjectDisposedException>(() => handler.CheckFcxModeAsync());
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    [InlineData(null)]
    public void FcxMode_ShouldReturnCorrectValue(bool? fcxMode)
    {
        // Arrange & Act
        var handler = CreateHandler(fcxMode);

        // Assert
        handler.FcxMode.Should().Be(fcxMode);
    }

    [Fact]
    public async Task CheckFcxModeAsync_ShouldNotDeadlock_WithNestedCalls()
    {
        // Arrange
        var handler1 = CreateHandler();
        var handler2 = CreateHandler();

        // Act - Nested async calls
        var task1 = Task.Run(async () =>
        {
            await handler1.CheckFcxModeAsync();
            await handler2.CheckFcxModeAsync();
        });

        var task2 = Task.Run(async () =>
        {
            await handler2.CheckFcxModeAsync();
            await handler1.CheckFcxModeAsync();
        });

        // Assert - Should complete without deadlock
        var allTasks = Task.WhenAll(task1, task2);
        var completedTask = await Task.WhenAny(allTasks, Task.Delay(TimeSpan.FromSeconds(5)));

        completedTask.Should().Be(allTasks, "Tasks should complete without deadlock");
    }
}