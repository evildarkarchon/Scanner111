using System.Diagnostics.CodeAnalysis;
using Scanner111.Core.Infrastructure;

namespace Scanner111.Tests.Infrastructure;

public class CancellationSupportTests : IDisposable
{
    private readonly List<IDisposable> _disposables = new();

    public void Dispose()
    {
        foreach (var disposable in _disposables) disposable.Dispose();
        _disposables.Clear();
        GC.SuppressFinalize(this);
    }

    [Fact]
    public void EnhancedCancellationTokenSource_CreatesValidToken()
    {
        // Arrange & Act
        using var cts = new EnhancedCancellationTokenSource();
        _disposables.Add(cts);

        // Assert
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
        await cts.CancelAsync();

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
        var innerProgress = new TestProgress<string>(value => reportedValue = value);
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
        await cts.CancelAsync();

        // Act & Assert
        await Assert.ThrowsAsync<OperationCanceledException>(async () => { await semaphore.WaitAsync(cts.Token); });
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
            // Don't pass cancellation token - we want this to complete even during cancellation
            // ReSharper disable once MethodSupportsCancellation
            await Task.Delay(1);
            callbackCalled = true;
        });
        await cts.CancelAsync();

        // Wait a moment for async callback to complete (short delay, no cancellation needed)
        // ReSharper disable once MethodSupportsCancellation
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
        await cts.CancelAsync();
        await waitTask;

        // Assert - Should complete without throwing
        Assert.True(waitTask.IsCompletedSuccessfully);
    }

    [Fact]
    public async Task CancellationTokenExtensions_WithCancellation_ThrowsWhenCancelled()
    {
        // Arrange
        using var cts = new CancellationTokenSource();
        // ReSharper disable once MethodSupportsCancellation
        var longRunningTask = Task.Delay(TimeSpan.FromSeconds(10));

        // Act
        await cts.CancelAsync();

        // Assert
        await Assert.ThrowsAsync<TaskCanceledException>(async () => { await longRunningTask.WaitAsync(cts.Token); });
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

    // Integration tests with real Progress<T> async behavior
    [Fact]
    public async Task CancellableProgress_ReportsProgress_AsyncBehavior()
    {
        // Arrange
        string? reportedValue = null;
        var tcs = new TaskCompletionSource<bool>();
        var innerProgress = new Progress<string>(value =>
        {
            reportedValue = value;
            tcs.SetResult(true);
        });
        using var cts = new CancellationTokenSource();
        using var cancellableProgress = new CancellableProgress<string>(innerProgress, cts.Token);

        // Act
        cancellableProgress.Report("test value");

        // Wait for async callback (with timeout)
        // ReSharper disable once MethodSupportsCancellation
        var completed = await tcs.Task.WaitAsync(TimeSpan.FromSeconds(2));

        // Assert
        Assert.True(completed);
        Assert.Equal("test value", reportedValue);
    }

    [Fact]
    public async Task CancellableProgress_DoesNotReportWhenCancelled_AsyncBehavior()
    {
        // Arrange
        string? reportedValue = null;
        var tcs = new TaskCompletionSource<bool>();
        var innerProgress = new Progress<string>(value =>
        {
            reportedValue = value;
            tcs.SetResult(true);
        });
        using var cts = new CancellationTokenSource();
        using var cancellableProgress = new CancellableProgress<string>(innerProgress, cts.Token);

        // Pre-cancel the token
        await cts.CancelAsync();

        // Act
        cancellableProgress.Report("test value");

        // Wait a short time to ensure no callback occurs
        // ReSharper disable once MethodSupportsCancellation
        var timeoutTask = Task.Delay(TimeSpan.FromMilliseconds(100));
        var completedTask = await Task.WhenAny(tcs.Task, timeoutTask);

        // Assert
        Assert.Equal(timeoutTask, completedTask); // Should timeout, not complete
        Assert.Null(reportedValue); // Should not report when cancelled
    }

    [Fact]
    [SuppressMessage("ReSharper", "MethodSupportsCancellation")]
    public async Task CancellableProgress_MultipleReports_AsyncBehavior()
    {
        // Arrange
        var reportedValues = new List<string>();
        var reportCount = 0;
        var expectedReports = 3;
        var tcs = new TaskCompletionSource<bool>();

        var innerProgress = new Progress<string>(value =>
        {
            reportedValues.Add(value);
            reportCount++;
            if (reportCount >= expectedReports)
                tcs.SetResult(true);
        });
        using var cts = new CancellationTokenSource();
        using var cancellableProgress = new CancellableProgress<string>(innerProgress, cts.Token);

        // Act - Generate multiple progress reports with small delays to avoid batching
        cancellableProgress.Report("report 1");
        await Task.Delay(10); // Small delay to avoid Progress<T> batching
        cancellableProgress.Report("report 2");
        await Task.Delay(10);
        cancellableProgress.Report("report 3");

        // Wait for async callbacks (with timeout)
        var completed = await tcs.Task.WaitAsync(TimeSpan.FromSeconds(2));

        // Assert
        Assert.True(completed);
        Assert.Equal(expectedReports, reportedValues.Count);
        Assert.Contains("report 1", reportedValues);
        Assert.Contains("report 2", reportedValues);
        Assert.Contains("report 3", reportedValues);
    }
}

/// <summary>
///     Test progress implementation that calls the callback synchronously
/// </summary>
/// <typeparam name="T">Progress value type</typeparam>
public class TestProgress<T> : IProgress<T>
{
    private readonly Action<T> _callback;

    public TestProgress(Action<T> callback)
    {
        _callback = callback;
    }

    public void Report(T value)
    {
        _callback(value);
    }
}