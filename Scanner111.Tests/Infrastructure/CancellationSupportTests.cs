using System.Diagnostics.CodeAnalysis;
using FluentAssertions;
using Scanner111.Core.Infrastructure;

namespace Scanner111.Tests.Infrastructure;

/// <summary>
/// Provides unit tests for cancellation-related utilities, including enhanced cancellation tokens,
/// cancellable progress reporting, cancellation token extensions, and cancellable semaphores.
/// </summary>
public class CancellationSupportTests : IDisposable
{
    private readonly List<IDisposable> _disposables = [];

    /// <summary>
    /// Releases all resources used by the <see cref="CancellationSupportTests"/> instance.
    /// </summary>
    /// <remarks>
    /// This method disposes all disposable resources contained within the instance
    /// and suppresses finalization to optimize garbage collection.
    /// </remarks>
    public void Dispose()
    {
        foreach (var disposable in _disposables) disposable.Dispose();
        _disposables.Clear();
        GC.SuppressFinalize(this);
    }

    /// <summary>
    /// Verifies that the <see cref="EnhancedCancellationTokenSource"/> creates a valid token
    /// which is not cancelled upon initialization.
    /// </summary>
    /// <remarks>
    /// This test ensures that a newly instantiated <see cref="EnhancedCancellationTokenSource"/>
    /// produces a cancellation token that is in a valid, non-cancelled state.
    /// </remarks>
    [Fact]
    public void EnhancedCancellationTokenSource_CreatesValidToken()
    {
        // Arrange & Act
        using var cts = new EnhancedCancellationTokenSource();
        _disposables.Add(cts);

        // Assert
        cts.IsCancellationRequested.Should().BeFalse("newly created token should not be cancelled");
    }

    /// <summary>
    /// Validates that the <see cref="EnhancedCancellationTokenSource"/> correctly cancels its token when the <see cref="EnhancedCancellationTokenSource.Cancel"/> method is invoked.
    /// </summary>
    /// <remarks>
    /// This test ensures that the cancellation state of both the <see cref="EnhancedCancellationTokenSource"/> and its associated <see cref="CancellationToken"/>
    /// reflect the expected behavior when cancellation is requested. It verifies that both <see cref="EnhancedCancellationTokenSource.IsCancellationRequested"/>
    /// and <see cref="CancellationToken.IsCancellationRequested"/> return true after invoking the <see cref="EnhancedCancellationTokenSource.Cancel"/> method.
    /// </remarks>
    [Fact]
    public void EnhancedCancellationTokenSource_CancelsToken()
    {
        // Arrange
        using var cts = new EnhancedCancellationTokenSource();
        _disposables.Add(cts);

        // Act
        cts.Cancel();

        // Assert
        cts.IsCancellationRequested.Should().BeTrue("cancellation should be requested after Cancel()");
        cts.Token.IsCancellationRequested.Should().BeTrue("token should reflect cancellation state");
    }

    /// <summary>
    /// Validates that an <see cref="EnhancedCancellationTokenSource"/> instance correctly cancels its token
    /// after a specified timeout period has elapsed.
    /// </summary>
    /// <remarks>
    /// This test ensures that, when an <see cref="EnhancedCancellationTokenSource"/> is created with a timeout value,
    /// the token is cancelled automatically once the timeout duration has expired. A sleep period longer than the timeout is used to verify this behavior.
    /// The test checks that the cancellation is properly requested.
    /// </remarks>
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
        cts.IsCancellationRequested.Should().BeTrue("token should be cancelled after timeout");
    }

    /// <summary>
    /// Verifies that the <see cref="EnhancedCancellationTokenSource.CancelAfter"/> method correctly cancels
    /// the token after the specified timeout duration.
    /// </summary>
    /// <remarks>
    /// This test ensures that an instance of <see cref="EnhancedCancellationTokenSource"/> cancels its token
    /// after the specified delay has elapsed, verifying the correct behavior of the <see cref="EnhancedCancellationTokenSource.IsCancellationRequested"/> property.
    /// </remarks>
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
        cts.IsCancellationRequested.Should().BeTrue("token should be cancelled after CancelAfter timeout");
    }

    /// <summary>
    /// Validates that the <see cref="CancellationToken.ThrowIfCancellationRequested"/> method
    /// throws an <see cref="OperationCanceledException"/> when the token is cancelled and
    /// includes the provided operation description in the exception message.
    /// </summary>
    /// <remarks>
    /// This test ensures that the cancellation helper correctly detects a requested cancellation
    /// and provides additional debugging information via the operation description in the exception message.
    /// </remarks>
    [Fact]
    public void CancellationHelper_ThrowsIfCancellationRequested()
    {
        // Arrange
        using var cts = new CancellationTokenSource();
        cts.Cancel();

        // Act & Assert
        var act = () => cts.Token.ThrowIfCancellationRequested("test operation");
        var exception = act.Should().Throw<OperationCanceledException>("cancelled token should throw")
            .Which;
        exception.Message.Should().Contain("test operation", "exception message should include operation description");
    }

    /// <summary>
    /// Tests the <see cref="CancellationHelper.ThrowIfCancellationRequested(CancellationToken, string?)"/>
    /// method to ensure that it does not throw an exception if the cancellation token is not in a canceled state.
    /// </summary>
    /// <remarks>
    /// This test verifies that the method behaves correctly by not raising an exception
    /// when invoked with a non-canceled token. It confirms that the behavior aligns
    /// with expected usage scenarios where the token remains un-canceled.
    /// </remarks>
    [Fact]
    public void CancellationHelper_DoesNotThrowIfNotCancelled()
    {
        // Arrange
        using var cts = new CancellationTokenSource();

        // Act & Assert (should not throw)
        cts.Token.ThrowIfCancellationRequested("test operation");
    }

    /// <summary>
    /// Validates that the <see cref="CancellationHelper.CheckpointAsync"/> method
    /// throws an <see cref="OperationCanceledException"/> when the associated <see cref="CancellationToken"/> is cancelled.
    /// </summary>
    /// <remarks>
    /// This test ensures that the checkpoint correctly propagates and reacts to cancellation requests
    /// for scenarios where asynchronous operations rely on cancellation support.
    /// </remarks>
    /// <returns>
    /// An asynchronous task representing the unit test execution. The test fails if the method does not throw
    /// the expected <see cref="OperationCanceledException"/>.
    /// </returns>
    [Fact]
    public async Task CancellationHelper_CheckpointAsync_ThrowsIfCancelled()
    {
        // Arrange
        using var cts = new CancellationTokenSource();
        await cts.CancelAsync();

        // Act & Assert
        var act = () => cts.Token.CheckpointAsync("test operation");
        await act.Should().ThrowAsync<OperationCanceledException>("checkpoint should throw when cancelled");
    }

    /// <summary>
    /// Verifies that <see cref="CancellationHelper.CheckpointAsync(CancellationToken, string, IProgress{string})" />
    /// reports progress correctly when provided with a progress instance.
    /// </summary>
    /// <remarks>
    /// This test ensures that the progress report functionality within
    /// <see cref="CancellationHelper.CheckpointAsync(CancellationToken, string, IProgress{string})" />
    /// is working as intended by sending a specific operation message to the progress reporter.
    /// </remarks>
    /// <returns>
    /// A task that represents the asynchronous operation of the test.
    /// </returns>
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
        reportedProgress.Should().Be("test operation", "progress should be reported with operation name");
    }

    /// <summary>
    /// Validates that the <see cref="CancellationHelper.CreateLinkedTokenSource"/> method correctly creates a linked
    /// <see cref="CancellationTokenSource"/> instance and handles token cancellation appropriately.
    /// </summary>
    /// <remarks>
    /// This test ensures that the linked token source triggers cancellation when one of the original tokens is cancelled.
    /// It verifies the functionality by asserting initial cancellation state and observing state changes upon cancellation.
    /// </remarks>
    [Fact]
    public void CancellationHelper_CreateLinkedTokenSource_Works()
    {
        // Arrange
        using var cts1 = new CancellationTokenSource();
        using var cts2 = new CancellationTokenSource();

        // Act
        using var linked = CancellationHelper.CreateLinkedTokenSource(cts1.Token, cts2.Token);

        // Assert
        linked.Token.IsCancellationRequested.Should().BeFalse("linked token should not be cancelled initially");

        // Cancel one source
        cts1.Cancel();
        linked.Token.IsCancellationRequested.Should().BeTrue("linked token should be cancelled when any source is cancelled");
    }

    /// <summary>
    /// Verifies that the <see cref="CancellationHelper.CreateTimeoutToken(System.TimeSpan)"/> method
    /// correctly creates a cancellation token that is canceled after the specified timeout has elapsed.
    /// </summary>
    /// <remarks>
    /// This test ensures the cancellation token starts in a non-cancelled state, transitions to a
    /// cancelled state after the timeout, and that the timeout duration is enforced appropriately.
    /// </remarks>
    [Fact]
    public void CancellationHelper_CreateTimeoutToken_CancelsAfterTimeout()
    {
        // Arrange & Act
        using var timeoutSource = CancellationHelper.CreateTimeoutToken(TimeSpan.FromMilliseconds(50));

        // Assert
        timeoutSource.Token.IsCancellationRequested.Should().BeFalse("token should not be cancelled initially");
        Thread.Sleep(100);
        timeoutSource.Token.IsCancellationRequested.Should().BeTrue("token should be cancelled after timeout");
    }

    /// <summary>
    /// Validates that <see cref="CancellationHelper.CreateCombinedToken"/> correctly combines a user-specified
    /// <see cref="CancellationToken"/> with a timeout-based token.
    /// </summary>
    /// <remarks>
    /// This test ensures that the combined cancellation token generated by <see cref="CancellationHelper.CreateCombinedToken"/>
    /// respects both the user-provided cancellation token and the timeout condition. It verifies that cancellation
    /// occurs when the user token is canceled.
    /// </remarks>
    [Fact]
    public void CancellationHelper_CreateCombinedToken_CombinesUserAndTimeout()
    {
        // Arrange
        using var userSource = new CancellationTokenSource();
        using var combinedSource = CancellationHelper.CreateCombinedToken(
            userSource.Token, TimeSpan.FromSeconds(1));

        // Act & Assert
        combinedSource.Token.IsCancellationRequested.Should().BeFalse("combined token should not be cancelled initially");

        // Cancel user token
        userSource.Cancel();
        combinedSource.Token.IsCancellationRequested.Should().BeTrue("combined token should be cancelled when user token is cancelled");
    }

    /// <summary>
    /// Verifies that <see cref="CancellableProgress{T}"/> correctly reports progress to the inner progress handler.
    /// </summary>
    /// <remarks>
    /// This test ensures that when a value is reported through a <see cref="CancellableProgress{T}"/> instance,
    /// it is passed to the wrapped progress handler without being affected by cancellation, as long as the associated
    /// cancellation token has not been cancelled.
    /// </remarks>
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
        reportedValue.Should().Be("test value", "progress should be reported");
    }

    /// <summary>
    /// Ensures that the <see cref="CancellableProgress{T}"/> instance does not report progress
    /// when the associated <see cref="CancellationToken"/> is cancelled.
    /// </summary>
    /// <remarks>
    /// This test verifies that the <see cref="CancellableProgress{T}.Report"/> method
    /// does not invoke the wrapped progress callback if the operation has been cancelled,
    /// aligning with the expected behavior for cancellation scenarios.
    /// </remarks>
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
        reportedValue.Should().BeNull("progress should not be reported when cancelled");
    }

    /// <summary>
    /// Verifies that the <see cref="CancellableSemaphore"/> can successfully acquire and release a semaphore slot.
    /// </summary>
    /// <remarks>
    /// This test ensures that a semaphore slot is properly acquired and subsequently released
    /// during the lifetime of the scoped <see cref="CancellableSemaphore"/> invocation.
    /// </remarks>
    /// <returns>
    /// A task that represents the asynchronous test operation.
    /// </returns>
    [Fact]
    public async Task CancellableSemaphore_AcquiresAndReleases()
    {
        // Arrange
        using var semaphore = new CancellableSemaphore(1, 1);

        // Act
        using var acquired = await semaphore.WaitAsync();

        // Assert
        acquired.Should().NotBeNull("semaphore should be successfully acquired");
    }

    /// <summary>
    /// Verifies that operations performed with <see cref="CancellableSemaphore"/> throw an
    /// <see cref="OperationCanceledException"/> when cancellation is requested.
    /// </summary>
    /// <returns>
    /// A task that represents the asynchronous test operation. The task will complete
    /// successfully if the exception is thrown, or fail if an unexpected behavior occurs.
    /// </returns>
    [Fact]
    public async Task CancellableSemaphore_ThrowsWhenCancelled()
    {
        // Arrange
        using var semaphore = new CancellableSemaphore(0, 1); // No permits available
        using var cts = new CancellationTokenSource();
        await cts.CancelAsync();

        // Act & Assert
        var act = () => semaphore.WaitAsync(cts.Token);
        await act.Should().ThrowAsync<OperationCanceledException>("semaphore wait should throw when cancelled");
    }

    /// <summary>
    /// Verifies that the <see cref="CancellableSemaphore"/> correctly times out when no permits are available.
    /// </summary>
    /// <remarks>
    /// This test ensures that if a semaphore is unable to acquire a permit within the specified timeout,
    /// it returns null as expected without throwing an exception.
    /// </remarks>
    /// <returns>
    /// This test does not return a value. Its success is determined by assertions within the test.
    /// </returns>
    [Fact]
    public async Task CancellableSemaphore_TimesOut()
    {
        // Arrange
        using var semaphore = new CancellableSemaphore(0, 1); // No permits available
        using var cts = new CancellationTokenSource();

        // Act
        var acquired = await semaphore.WaitAsync(TimeSpan.FromMilliseconds(50), cts.Token);

        // Assert
        acquired.Should().BeNull("semaphore should return null on timeout");
    }

    /// <summary>
    /// Verifies that the <see cref="CancellationTokenExtensions.RegisterCallback(CancellationToken, Action)"/> method
    /// correctly registers a callback that gets invoked when the associated <see cref="CancellationToken"/> is cancelled.
    /// </summary>
    /// <remarks>
    /// This test ensures that the registered callback is triggered appropriately when the cancellation is requested.
    /// Validation is performed by asserting that the callback has been executed as expected.
    /// </remarks>
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
        callbackCalled.Should().BeTrue("callback should be invoked when token is cancelled");
    }

    /// <summary>
    /// Verifies that the <see cref="CancellationTokenExtensions.RegisterAsyncCallback"/> method correctly registers
    /// an asynchronous callback and executes it upon cancellation of the associated <see cref="CancellationToken"/>.
    /// </summary>
    /// <returns>
    /// A task representing the asynchronous test operation.
    /// </returns>
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
        callbackCalled.Should().BeTrue("async callback should be invoked when token is cancelled");
    }

    /// <summary>
    /// Tests that the <see cref="CancellationTokenExtensions.WaitForCancellationAsync(CancellationToken)"/>
    /// method completes successfully when the associated <see cref="CancellationToken"/> is cancelled.
    /// </summary>
    /// <returns>
    /// Ensures that the task returned by the <see cref="CancellationTokenExtensions.WaitForCancellationAsync(CancellationToken)"/> method
    /// completes successfully upon cancellation of the token.
    /// </returns>
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
        waitTask.IsCompletedSuccessfully.Should().BeTrue("wait task should complete successfully when cancelled");
    }

    /// <summary>
    /// Verifies that the <see cref="Task"/> method utilizing cancellation through
    /// <see cref="CancellationTokenExtensions"/> throws a <see cref="TaskCanceledException"/>
    /// when the provided cancellation token is cancelled before the task completes.
    /// </summary>
    /// <remarks>
    /// This test ensures that tasks monitored by a cancellation token appropriately respect the cancellation request
    /// and provide the expected exception feedback when cancelled.
    /// </remarks>
    /// <returns>Asynchronous operation representing the execution of the test logic.</returns>
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
        var act = () => longRunningTask.WaitAsync(cts.Token);
        await act.Should().ThrowAsync<TaskCanceledException>("task should throw when cancellation is requested");
    }

    /// <summary>
    /// Verifies that the <see cref="CancellationTokenExtensions.WithCancellation{T}(Task{T}, CancellationToken)"/> method
    /// completes successfully when no cancellation is requested during its execution.
    /// </summary>
    /// <remarks>
    /// This test ensures that the <see cref="CancellationTokenExtensions.WithCancellation{T}(Task{T}, CancellationToken)"/>
    /// method correctly handles a task that completes normally without being cancelled, returning the expected result.
    /// </remarks>
    /// <returns>
    /// A task representing the asynchronous test operation.
    /// </returns>
    [Fact]
    public async Task CancellationTokenExtensions_WithCancellation_CompletesNormally()
    {
        // Arrange
        using var cts = new CancellationTokenSource();
        var shortTask = Task.FromResult("success");

        // Act
        var result = await shortTask.WithCancellation(cts.Token);

        // Assert
        result.Should().Be("success", "task should complete normally without cancellation");
    }

    /// <summary>
    /// Verifies that <see cref="CancellableProgress{T}"/> supports reporting progress asynchronously,
    /// ensuring the progress information is delivered as expected and handled correctly by the provided progress handler.
    /// </summary>
    /// <remarks>
    /// This test validates the integration of <see cref="CancellableProgress{T}"/> with asynchronous behavior,
    /// including confirming the correct propagation of progress values to the wrapped <see cref="IProgress{T}"/> implementation
    /// in the presence of an uncancelled <see cref="CancellationToken"/>. A timeout mechanism ensures the operation
    /// completes within a reasonable timeframe.
    /// </remarks>
    /// <returns>
    /// A task representing the asynchronous operation performed by the test.
    /// The task completes successfully if progress is reported properly and matches the expected value.
    /// </returns>
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
        completed.Should().BeTrue("progress report should complete");
        reportedValue.Should().Be("test value", "correct value should be reported");
    }

    /// <summary>
    /// Tests the asynchronous behavior of the <see cref="CancellableProgress{T}"/> class,
    /// ensuring that progress is not reported when the associated cancellation token has been cancelled
    /// prior to reporting.
    /// </summary>
    /// <returns>
    /// A task representing the asynchronous test execution.
    /// </returns>
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
        completedTask.Should().Be(timeoutTask, "should timeout rather than complete");
        reportedValue.Should().BeNull("value should not be reported when cancelled");
    }

    /// <summary>
    /// Tests the asynchronous behavior of <see cref="CancellableProgress{T}"/> when handling multiple progress reports.
    /// </summary>
    /// <remarks>
    /// This method evaluates whether multiple progress reports are correctly processed and delivered asynchronously
    /// without batching, even with small delays between reports. It ensures the reported values match the expected values
    /// and verifies that the progress reporter behaves as intended under these conditions.
    /// </remarks>
    /// <returns>
    /// A task that represents the asynchronous test. The test verifies that the expected number of progress reports
    /// are received and contain the correct values.
    /// </returns>
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
        completed.Should().BeTrue("all progress reports should complete");
        reportedValues.Should().HaveCount(expectedReports, "all reports should be received");
        reportedValues.Should().Contain("report 1", "first report should be received");
        reportedValues.Should().Contain("report 2", "second report should be received");
        reportedValues.Should().Contain("report 3", "third report should be received");
    }
}

/// <summary>
/// Represents a test implementation of progress reporting that invokes a provided callback synchronously
/// for reporting progress updates.
/// </summary>
/// <typeparam name="T">The type of the progress value.</typeparam>
public class TestProgress<T>(Action<T> callback) : IProgress<T>
{
    /// <summary>
    /// Reports a progress update by invoking the provided callback with the specified value.
    /// </summary>
    /// <typeparam name="T">The type of the value to report as progress.</typeparam>
    /// <param name="value">The progress value to report.</param>
    public void Report(T value)
    {
        callback(value);
    }
}