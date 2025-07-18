using Microsoft.Extensions.Logging.Abstractions;
using Scanner111.Core.Infrastructure;
using Xunit;

namespace Scanner111.Tests.Infrastructure;

public class CancellationSupportTests : IDisposable
{
    private readonly List<IDisposable> _disposables = new();

    [Fact]
    public void EnhancedCancellationTokenSource_CreatesValidToken()
    {
        // Arrange & Act
        using var cts = new EnhancedCancellationTokenSource();
        _disposables.Add(cts);

        // Assert
        Assert.NotNull(cts.Token);
        Assert.False(cts.IsCancellationRequested);
    }

    [Fact]
    public void EnhancedCancellationTokenSource_CancelsToken()
    {
        // Arrange
        using var cts = new EnhancedCancellationTokenSource();
        _disposables.Add(cts);

        // Act
        cts.Cancel();

        // Assert
        Assert.True(cts.IsCancellationRequested);
        Assert.True(cts.Token.IsCancellationRequested);
    }

    [Fact]
    public void EnhancedCancellationTokenSource_CancelsAfterTimeout()
    {
        // Arrange
        var timeout = TimeSpan.FromMilliseconds(50);
        using var cts = new EnhancedCancellationTokenSource(timeout);
        _disposables.Add(cts);

        // Act
        Thread.Sleep(100); // Wait longer than timeout

        // Assert
        Assert.True(cts.IsCancellationRequested);
    }

    [Fact]
    public void EnhancedCancellationTokenSource_CancelAfterWorks()
    {
        // Arrange
        using var cts = new EnhancedCancellationTokenSource();
        _disposables.Add(cts);

        // Act
        cts.CancelAfter(TimeSpan.FromMilliseconds(50));
        Thread.Sleep(100);

        // Assert
        Assert.True(cts.IsCancellationRequested);
    }

    [Fact]
    public void CancellationHelper_ThrowsIfCancellationRequested()
    {
        // Arrange
        using var cts = new CancellationTokenSource();
        cts.Cancel();

        // Act & Assert
        var exception = Assert.Throws<OperationCanceledException>(() =>
        {
            cts.Token.ThrowIfCancellationRequested("test operation");
        });

        Assert.Contains("test operation", exception.Message);
    }

    [Fact]
    public void CancellationHelper_DoesNotThrowIfNotCancelled()
    {
        // Arrange
        using var cts = new CancellationTokenSource();

        // Act & Assert (should not throw)
        cts.Token.ThrowIfCancellationRequested("test operation");
    }

    [Fact]
    public async Task CancellationHelper_CheckpointAsync_ThrowsIfCancelled()
    {
        // Arrange
        using var cts = new CancellationTokenSource();
        cts.Cancel();

        // Act & Assert
        await Assert.ThrowsAsync<OperationCanceledException>(async () =>
        {
            await cts.Token.CheckpointAsync("test operation");
        });
    }

    [Fact]
    public async Task CancellationHelper_CheckpointAsync_ReportsProgress()
    {
        // Arrange
        using var cts = new CancellationTokenSource();
        string? reportedProgress = null;
        var progress = new Progress<string>(p => reportedProgress = p);

        // Act
        await cts.Token.CheckpointAsync("test operation", progress);

        // Assert
        Assert.Equal("test operation", reportedProgress);
    }

    [Fact]
    public void CancellationHelper_CreateLinkedTokenSource_Works()
    {
        // Arrange
        using var cts1 = new CancellationTokenSource();
        using var cts2 = new CancellationTokenSource();

        // Act
        using var linked = CancellationHelper.CreateLinkedTokenSource(cts1.Token, cts2.Token);

        // Assert
        Assert.False(linked.Token.IsCancellationRequested);

        // Cancel one source
        cts1.Cancel();
        Assert.True(linked.Token.IsCancellationRequested);
    }

    [Fact]
    public void CancellationHelper_CreateTimeoutToken_CancelsAfterTimeout()
    {
        // Arrange & Act
        using var timeoutSource = CancellationHelper.CreateTimeoutToken(TimeSpan.FromMilliseconds(50));

        // Assert
        Assert.False(timeoutSource.Token.IsCancellationRequested);
        Thread.Sleep(100);
        Assert.True(timeoutSource.Token.IsCancellationRequested);
    }

    [Fact]
    public void CancellationHelper_CreateCombinedToken_CombinesUserAndTimeout()
    {
        // Arrange
        using var userSource = new CancellationTokenSource();
        using var combinedSource = CancellationHelper.CreateCombinedToken(
            userSource.Token, TimeSpan.FromSeconds(1));

        // Act & Assert
        Assert.False(combinedSource.Token.IsCancellationRequested);

        // Cancel user token
        userSource.Cancel();
        Assert.True(combinedSource.Token.IsCancellationRequested);
    }

    [Fact]
    public void CancellableProgress_ReportsProgress()
    {
        // Arrange
        string? reportedValue = null;
        var innerProgress = new Progress<string>(value => reportedValue = value);
        using var cts = new CancellationTokenSource();
        using var cancellableProgress = new CancellableProgress<string>(innerProgress, cts.Token);

        // Act
        cancellableProgress.Report("test value");

        // Assert
        Assert.Equal("test value", reportedValue);
    }

    [Fact]
    public void CancellableProgress_DoesNotReportWhenCancelled()
    {
        // Arrange
        string? reportedValue = null;
        var innerProgress = new Progress<string>(value => reportedValue = value);
        using var cts = new CancellationTokenSource();
        cts.Cancel();
        using var cancellableProgress = new CancellableProgress<string>(innerProgress, cts.Token);

        // Act
        cancellableProgress.Report("test value");

        // Assert
        Assert.Null(reportedValue); // Should not report when cancelled
    }

    [Fact]
    public async Task CancellableSemaphore_AcquiresAndReleases()
    {
        // Arrange
        using var semaphore = new CancellableSemaphore(1, 1);

        // Act
        using var acquired = await semaphore.WaitAsync();

        // Assert
        Assert.NotNull(acquired);
    }

    [Fact]
    public async Task CancellableSemaphore_ThrowsWhenCancelled()
    {
        // Arrange
        using var semaphore = new CancellableSemaphore(0, 1); // No permits available
        using var cts = new CancellationTokenSource();
        cts.Cancel();

        // Act & Assert
        await Assert.ThrowsAsync<OperationCanceledException>(async () =>
        {
            await semaphore.WaitAsync(cts.Token);
        });
    }

    [Fact]
    public async Task CancellableSemaphore_TimesOut()
    {
        // Arrange
        using var semaphore = new CancellableSemaphore(0, 1); // No permits available
        using var cts = new CancellationTokenSource();

        // Act
        var acquired = await semaphore.WaitAsync(TimeSpan.FromMilliseconds(50), cts.Token);

        // Assert
        Assert.Null(acquired); // Should timeout
    }

    [Fact]
    public void CancellationTokenExtensions_RegisterCallback_Works()
    {
        // Arrange
        using var cts = new CancellationTokenSource();
        var callbackCalled = false;

        // Act
        using var registration = cts.Token.RegisterCallback(() => callbackCalled = true);
        cts.Cancel();

        // Assert
        Assert.True(callbackCalled);
    }

    [Fact]
    public async Task CancellationTokenExtensions_RegisterAsyncCallback_Works()
    {
        // Arrange
        using var cts = new CancellationTokenSource();
        var callbackCalled = false;

        // Act
        using var registration = cts.Token.RegisterAsyncCallback(async () =>
        {
            await Task.Delay(1);
            callbackCalled = true;
        });
        cts.Cancel();
        
        // Wait a moment for async callback to complete
        await Task.Delay(50);

        // Assert
        Assert.True(callbackCalled);
    }

    [Fact]
    public async Task CancellationTokenExtensions_WaitForCancellationAsync_CompletesWhenCancelled()
    {
        // Arrange
        using var cts = new CancellationTokenSource();

        // Act
        var waitTask = cts.Token.WaitForCancellationAsync();
        cts.Cancel();
        await waitTask;

        // Assert - Should complete without throwing
        Assert.True(waitTask.IsCompletedSuccessfully);
    }

    [Fact]
    public async Task CancellationTokenExtensions_WithCancellation_ThrowsWhenCancelled()
    {
        // Arrange
        using var cts = new CancellationTokenSource();
        var longRunningTask = Task.Delay(TimeSpan.FromSeconds(10));

        // Act
        cts.Cancel();

        // Assert
        await Assert.ThrowsAsync<TaskCanceledException>(async () =>
        {
            await longRunningTask.WaitAsync(cts.Token);
        });
    }

    [Fact]
    public async Task CancellationTokenExtensions_WithCancellation_CompletesNormally()
    {
        // Arrange
        using var cts = new CancellationTokenSource();
        var shortTask = Task.FromResult("success");

        // Act
        var result = await shortTask.WithCancellation(cts.Token);

        // Assert
        Assert.Equal("success", result);
    }

    public void Dispose()
    {
        foreach (var disposable in _disposables)
        {
            disposable?.Dispose();
        }
        _disposables.Clear();
    }
}