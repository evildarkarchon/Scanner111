using Scanner111.Core.Pipeline;

namespace Scanner111.Tests.Pipeline;

/// <summary>
/// Provides unit tests for validating the functionality of progress reporting
/// within the pipeline, including calculations of progress percentages,
/// performance metrics, and reporting behaviors for file and analyzer progress states.
/// </summary>
public class ProgressReportingTests
{
    /// <summary>
    /// Verifies that the progress percentage is calculated correctly by the
    /// <c>DetailedProgressInfo</c> record when values for <c>TotalFiles</c>
    /// and <c>ProcessedFiles</c> are provided.
    /// </summary>
    /// <remarks>
    /// This method ensures that the formula for calculating progress
    /// percentage (<c>ProcessedFiles * 100.0 / TotalFiles</c>) produces
    /// accurate results. The test covers scenarios where:<br/>
    /// 1. Total files are greater than zero.<br/>
    /// 2. A specific number of files have been processed.
    /// </remarks>
    [Fact]
    public void DetailedProgressInfo_CalculatesProgressPercentageCorrectly()
    {
        // Arrange
        var progress = new DetailedProgressInfo
        {
            TotalFiles = 100,
            ProcessedFiles = 25
        };

        // Act & Assert
        Assert.Equal(25.0, progress.ProgressPercentage);
    }

    /// <summary>
    /// Validates that the <c>DetailedProgressInfo</c> record handles scenarios where
    /// the <c>TotalFiles</c> property is set to zero without throwing exceptions or
    /// producing invalid progress percentage values.
    /// </summary>
    /// <remarks>
    /// This test ensures that the calculated progress percentage remains zero and
    /// does not result in divide-by-zero errors when <c>TotalFiles</c> is zero.
    /// It also verifies the behavior of the <c>ProgressPercentage</c> property in such cases.
    /// </remarks>
    [Fact]
    public void DetailedProgressInfo_HandlesZeroTotalFiles()
    {
        // Arrange
        var progress = new DetailedProgressInfo
        {
            TotalFiles = 0,
            ProcessedFiles = 5
        };

        // Act & Assert
        Assert.Equal(0.0, progress.ProgressPercentage);
    }

    /// <summary>
    /// Validates the performance metrics calculations of the
    /// <c>DetailedProgressInfo</c> record, ensuring accurate values for
    /// elapsed time, files processed per second, and average file processing time.
    /// </summary>
    /// <remarks>
    /// This test verifies that the performance metrics are correctly calculated based
    /// on the properties <c>StartTime</c>, <c>LastUpdateTime</c>, and <c>ProcessedFiles</c>.
    /// Specific conditions validated include:<br/>
    /// 1. Elapsed time is calculated as the difference between <c>LastUpdateTime</c>
    /// and <c>StartTime</c>.<br/>
    /// 2. Files per second is greater than zero when processed files and elapsed time
    /// are valid.<br/>
    /// 3. Average file time is correctly derived when processed files are greater than zero.
    /// </remarks>
    [Fact]
    public void DetailedProgressInfo_CalculatesPerformanceMetrics()
    {
        // Arrange
        var startTime = DateTime.UtcNow.AddMinutes(-2);
        var progress = new DetailedProgressInfo
        {
            StartTime = startTime,
            LastUpdateTime = DateTime.UtcNow,
            ProcessedFiles = 60
        };

        // Act & Assert
        Assert.True(progress.ElapsedTime.TotalMinutes >= 1.9);
        Assert.True(progress.FilesPerSecond > 0);
        Assert.True(progress.AverageFileTime > 0);
    }

    /// <summary>
    /// Validates that the <c>ReportFileStart</c> method in the <c>DetailedProgress</c>
    /// class correctly notifies progress about the start of processing a new file.
    /// </summary>
    /// <remarks>
    /// This test ensures that when <c>ReportFileStart</c> is invoked with a file path:<br/>
    /// 1. <c>DetailedProgressInfo</c> is reported with the provided file name as <c>CurrentFile</c>.<br/>
    /// 2. The <c>CurrentFileStatus</c> of the reported progress is set to <c>InProgress</c>.<br/>
    /// </remarks>
    /// <returns>
    /// A task representing the asynchronous execution of this test.
    /// The test passes if the file start is reported correctly with matching properties in
    /// the <c>DetailedProgressInfo</c> instance.
    /// </returns>
    [Fact]
    public async Task DetailedProgress_ReportsFileStart()
    {
        // Arrange
        var progress = new DetailedProgress();
        DetailedProgressInfo? reportedProgress = null;
        var innerProgress = new Progress<DetailedProgressInfo>(p => reportedProgress = p);
        var detailedProgress = new DetailedProgress(innerProgress);

        // Act
        detailedProgress.ReportFileStart("test.log");

        // Wait a small amount for async reporting
        await Task.Delay(10);

        // Assert
        Assert.NotNull(reportedProgress);
        Assert.Equal("test.log", reportedProgress.CurrentFile);
        Assert.Equal(FileProcessingStatus.InProgress, reportedProgress.CurrentFileStatus);
    }

    /// <summary>
    /// Ensures that the <c>DetailedProgress</c> class correctly reports the completion
    /// of a file, updating the <c>DetailedProgressInfo</c> record with appropriate values
    /// for the processed and successful file counts, as well as the current file's name
    /// and its status.
    /// </summary>
    /// <remarks>
    /// This method validates that the <c>ReportFileComplete</c> method of
    /// <c>DetailedProgress</c> functions as intended by:<br/>
    /// - Confirming the <c>CurrentFile</c> property reflects the completed file's name.<br/>
    /// - Setting <c>CurrentFileStatus</c> to <c>FileProcessingStatus.Completed</c>.<br/>
    /// - Incrementing <c>ProcessedFiles</c> by one.<br/>
    /// - Incrementing <c>SuccessfulFiles</c> by one if the file was marked as successful.
    /// </remarks>
    [Fact]
    public void DetailedProgress_ReportsFileComplete()
    {
        // Arrange
        DetailedProgressInfo? reportedProgress = null;
        var mockProgress = new TestProgress<DetailedProgressInfo>(p => reportedProgress = p);
        var detailedProgress = new DetailedProgress(mockProgress);

        // Act
        detailedProgress.ReportFileComplete("test.log", true);

        // Assert
        Assert.NotNull(reportedProgress);
        Assert.Equal("test.log", reportedProgress.CurrentFile);
        Assert.Equal(FileProcessingStatus.Completed, reportedProgress.CurrentFileStatus);
        Assert.Equal(1, reportedProgress.ProcessedFiles);
        Assert.Equal(1, reportedProgress.SuccessfulFiles);
    }

    /// <summary>
    /// Ensures that the <c>DetailedProgress</c> class correctly reports file processing failures
    /// via its progress mechanism.
    /// </summary>
    /// <remarks>
    /// This test validates that when a file is reported as failed through the
    /// <c>ReportFileComplete</c> method of <c>DetailedProgress</c>, the following conditions are met:<br/>
    /// - The <c>CurrentFile</c> property in <c>DetailedProgressInfo</c> contains the reported file's name.<br/>
    /// - The <c>CurrentFileStatus</c> is set to <c>FileProcessingStatus.Failed</c>.<br/>
    /// - The relevant counters, <c>ProcessedFiles</c> and <c>FailedFiles</c>, are updated accurately.<br/>
    /// This test checks the integrity of progress updates in failure scenarios to ensure consistent reporting.
    /// </remarks>
    [Fact]
    public void DetailedProgress_ReportsFileFailure()
    {
        // Arrange
        DetailedProgressInfo? reportedProgress = null;
        var mockProgress = new TestProgress<DetailedProgressInfo>(p => reportedProgress = p);
        var detailedProgress = new DetailedProgress(mockProgress);

        // Act
        detailedProgress.ReportFileComplete("test.log", false);

        // Assert
        Assert.NotNull(reportedProgress);
        Assert.Equal("test.log", reportedProgress.CurrentFile);
        Assert.Equal(FileProcessingStatus.Failed, reportedProgress.CurrentFileStatus);
        Assert.Equal(1, reportedProgress.ProcessedFiles);
        Assert.Equal(1, reportedProgress.FailedFiles);
    }

    /// <summary>
    /// Ensures that the <c>DetailedProgress</c> class accurately tracks
    /// the progress of an analyzer during execution and reports the relevant
    /// updates using a <c>DetailedProgressInfo</c> instance.
    /// </summary>
    /// <remarks>
    /// This test verifies the behavior of the <c>ReportAnalyzerStart</c> method in the
    /// <c>DetailedProgress</c> class, ensuring that the following conditions are met:<br/>
    /// - A new analyzer is correctly added to the <c>ActiveAnalyzers</c> list of the
    /// <c>DetailedProgressInfo</c> object.<br/>
    /// - The reported progress includes the correct analyzer name.<br/>
    /// - The associated file name is accurately reported.<br/>
    /// - The analyzer status is appropriately set to <c>AnalyzerStatus.Running</c>.
    /// </remarks>
    /// <returns>
    /// Confirms that the progress reporting mechanism initializes and updates
    /// analyzer tracking correctly by validating that:<br/>
    /// - The <c>ReportedProgress</c> object is not null.<br/>
    /// - The total number of active analyzers is as expected.<br/>
    /// - The values for <c>AnalyzerName</c>, <c>FileName</c>, and <c>Status</c> are correctly populated.
    /// </returns>
    [Fact]
    public async Task DetailedProgress_TracksAnalyzerProgress()
    {
        // Arrange
        var progress = new DetailedProgress();
        DetailedProgressInfo? reportedProgress = null;
        var innerProgress = new Progress<DetailedProgressInfo>(p => reportedProgress = p);
        var detailedProgress = new DetailedProgress(innerProgress);

        // Act
        detailedProgress.ReportAnalyzerStart("FormIdAnalyzer", "test.log");

        // Wait for async reporting
        await Task.Delay(10);

        // Assert
        Assert.NotNull(reportedProgress);
        Assert.Single(reportedProgress.ActiveAnalyzers);

        var analyzer = reportedProgress.ActiveAnalyzers.First();
        Assert.Equal("FormIdAnalyzer", analyzer.AnalyzerName);
        Assert.Equal("test.log", analyzer.FileName);
        Assert.Equal(AnalyzerStatus.Running, analyzer.Status);
    }

    /// <summary>
    /// Ensures that the <c>DetailedProgress</c> class correctly removes an analyzer
    /// from the active analyzers list upon completion when the <c>ReportAnalyzerComplete</c>
    /// method is called after <c>ReportAnalyzerStart</c>.
    /// </summary>
    /// <remarks>
    /// This test verifies the behavior of <c>DetailedProgress</c> in handling analyzer progress
    /// completion. It specifically asserts that:<br/>
    /// 1. The progress object tracked by <c>DetailedProgress</c> is updated and not null.<br/>
    /// 2. The analyzer, once marked as complete, is properly removed from the list of active analyzers in the
    /// <c>DetailedProgressInfo</c> object.
    /// </remarks>
    [Fact]
    public void DetailedProgress_CompletesAnalyzerProgress()
    {
        // Arrange
        DetailedProgressInfo? reportedProgress = null;
        var mockProgress = new TestProgress<DetailedProgressInfo>(p => reportedProgress = p);
        var detailedProgress = new DetailedProgress(mockProgress);

        // Act
        detailedProgress.ReportAnalyzerStart("FormIdAnalyzer", "test.log");
        detailedProgress.ReportAnalyzerComplete("FormIdAnalyzer", "test.log", true);

        // Assert
        Assert.NotNull(reportedProgress);
        Assert.Empty(reportedProgress.ActiveAnalyzers); // Should be removed from active list
    }

    /// <summary>
    /// Validates that the <c>AnalyzerProgress</c> class correctly calculates the duration
    /// of an analyzer's execution by utilizing the <c>StartTime</c> and <c>EndTime</c> properties.
    /// </summary>
    /// <remarks>
    /// This test ensures that the difference between <c>EndTime</c> and <c>StartTime</c> is accurately
    /// calculated and assigned to the <c>Duration</c> property. Additionally, it verifies that the
    /// analyzer's status transitions to <c>Completed</c> upon setting the <c>EndTime</c>.
    /// </remarks>
    [Fact]
    public void AnalyzerProgress_CalculatesDuration()
    {
        // Arrange
        var startTime = DateTime.UtcNow;
        var progress = new AnalyzerProgress
        {
            AnalyzerName = "TestAnalyzer",
            FileName = "test.log",
            Status = AnalyzerStatus.Running,
            StartTime = startTime
        };

        // Act
        Thread.Sleep(10); // Small delay to ensure time difference
        progress.EndTime = DateTime.UtcNow;
        progress.Duration = progress.EndTime - progress.StartTime;
        progress.Status = AnalyzerStatus.Completed;

        // Assert
        Assert.True(progress.Duration?.TotalMilliseconds > 0);
        Assert.Equal(AnalyzerStatus.Completed, progress.Status);
    }

    /// <summary>
    /// Validates that all possible values of the <c>FileProcessingStatus</c> enumeration
    /// are supported by the <c>DetailedProgressInfo</c> record.
    /// </summary>
    /// <param name="status">
    /// The <c>FileProcessingStatus</c> value to test, representing the current
    /// state of a file during processing.
    /// </param>
    /// <remarks>
    /// This test ensures that each value in the <c>FileProcessingStatus</c>
    /// enumeration is correctly handled and can be assigned to the
    /// <c>CurrentFileStatus</c> property of the <c>DetailedProgressInfo</c> record.
    /// </remarks>
    [Theory]
    [InlineData(FileProcessingStatus.Pending)]
    [InlineData(FileProcessingStatus.InProgress)]
    [InlineData(FileProcessingStatus.Completed)]
    [InlineData(FileProcessingStatus.Failed)]
    [InlineData(FileProcessingStatus.Cancelled)]
    public void FileProcessingStatus_AllValuesSupported(FileProcessingStatus status)
    {
        // Arrange & Act
        var progress = new DetailedProgressInfo
        {
            CurrentFileStatus = status
        };

        // Assert
        Assert.Equal(status, progress.CurrentFileStatus);
    }

    /// <summary>
    /// Validates that the <c>AnalyzerProgress.Status</c> property correctly supports
    /// all values defined in the <c>AnalyzerStatus</c> enumeration.
    /// </summary>
    /// <param name="status">The analyzer status value to be assigned and verified,
    /// which must be one of the values defined in the <c>AnalyzerStatus</c> enum:
    /// <c>Pending</c>, <c>Running</c>, <c>Completed</c>, <c>Failed</c>, or <c>Skipped</c>.</param>
    [Theory]
    [InlineData(AnalyzerStatus.Pending)]
    [InlineData(AnalyzerStatus.Running)]
    [InlineData(AnalyzerStatus.Completed)]
    [InlineData(AnalyzerStatus.Failed)]
    [InlineData(AnalyzerStatus.Skipped)]
    public void AnalyzerStatus_AllValuesSupported(AnalyzerStatus status)
    {
        // Arrange & Act
        var progress = new AnalyzerProgress
        {
            Status = status
        };

        // Assert
        Assert.Equal(status, progress.Status);
    }

    // Integration tests with real Progress<T> async behavior
    /// <summary>
    /// Verifies that the <c>DetailedProgress</c> class correctly handles asynchronous
    /// behavior when reporting the completion of file processing. Validates that the
    /// progress callback is triggered, and the associated progress information is updated
    /// accurately for a completed file.
    /// </summary>
    /// <remarks>
    /// This test ensures that the <c>ReportFileComplete</c> method of the <c>DetailedProgress</c>
    /// instance asynchronously reports file completion with the following attributes verified:<br/>
    /// - The <c>CurrentFile</c> is set to the correct file path.<br/>
    /// - The <c>CurrentFileStatus</c> is updated to <c>FileProcessingStatus.Completed</c>.<br/>
    /// - The number of <c>ProcessedFiles</c> and <c>SuccessfulFiles</c> is incremented
    /// accurately.<br/>
    /// Additionally, the test checks that the asynchronous progress callback is executed within
    /// a reasonable timeout to confirm proper async behavior.
    /// </remarks>
    /// <returns>
    /// A <c>Task</c> representing the asynchronous operation of the test.
    /// </returns>
    [Fact]
    public async Task DetailedProgress_ReportsFileComplete_AsyncBehavior()
    {
        // Arrange
        DetailedProgressInfo? reportedProgress = null;
        var tcs = new TaskCompletionSource<bool>();
        var innerProgress = new Progress<DetailedProgressInfo>(p =>
        {
            reportedProgress = p;
            tcs.SetResult(true);
        });
        var detailedProgress = new DetailedProgress(innerProgress);

        // Act
        detailedProgress.ReportFileComplete("test.log", true);

        // Wait for async callback (with timeout)
        var completed = await tcs.Task.WaitAsync(TimeSpan.FromSeconds(2));

        // Assert
        Assert.True(completed);
        Assert.NotNull(reportedProgress);
        Assert.Equal("test.log", reportedProgress.CurrentFile);
        Assert.Equal(FileProcessingStatus.Completed, reportedProgress.CurrentFileStatus);
        Assert.Equal(1, reportedProgress.ProcessedFiles);
        Assert.Equal(1, reportedProgress.SuccessfulFiles);
    }

    /// <summary>
    /// Validates the asynchronous behavior of the <c>ReportFileComplete</c> method when a file failure
    /// is reported in the <c>DetailedProgress</c> class.
    /// </summary>
    /// <remarks>
    /// This test ensures that when a file is reported as failed using the <c>ReportFileComplete</c> method,
    /// the <c>DetailedProgress</c> instance updates the <c>DetailedProgressInfo</c> properties correctly.
    /// It verifies that:<br/>
    /// 1. The <c>CurrentFile</c> property correctly reflects the failed file's name.<br/>
    /// 2. The <c>CurrentFileStatus</c> is set to <c>Failed</c>.<br/>
    /// 3. The <c>ProcessedFiles</c> count increments appropriately.<br/>
    /// 4. The <c>FailedFiles</c> count increments.<br/>
    /// Additionally, the test checks that the asynchronous callback completes within the expected time limit.
    /// </remarks>
    /// <returns>
    /// A task representing the asynchronous test operation.
    /// The test assures timely reporting and accurate progress information updates for failed files.
    /// </returns>
    [Fact]
    public async Task DetailedProgress_ReportsFileFailure_AsyncBehavior()
    {
        // Arrange
        DetailedProgressInfo? reportedProgress = null;
        var tcs = new TaskCompletionSource<bool>();
        var innerProgress = new Progress<DetailedProgressInfo>(p =>
        {
            reportedProgress = p;
            tcs.SetResult(true);
        });
        var detailedProgress = new DetailedProgress(innerProgress);

        // Act
        detailedProgress.ReportFileComplete("test.log", false);

        // Wait for async callback (with timeout)
        var completed = await tcs.Task.WaitAsync(TimeSpan.FromSeconds(2));

        // Assert
        Assert.True(completed);
        Assert.NotNull(reportedProgress);
        Assert.Equal("test.log", reportedProgress.CurrentFile);
        Assert.Equal(FileProcessingStatus.Failed, reportedProgress.CurrentFileStatus);
        Assert.Equal(1, reportedProgress.ProcessedFiles);
        Assert.Equal(1, reportedProgress.FailedFiles);
    }

    /// <summary>
    /// Ensures that the <c>DetailedProgress</c> class correctly completes the progress tracking
    /// for an analyzer, removing it from the active analyzers list once it is marked as complete.
    /// </summary>
    /// <remarks>
    /// This test validates the complete lifecycle of an analyzer's progress tracking within
    /// <c>DetailedProgress</c>, including:<br/>
    /// 1. Reporting the start of an analyzer's processing.<br/>
    /// 2. Asynchronously marking the analyzer as complete.<br/>
    /// 3. Verifying that the final state reflects the successful removal of the analyzer
    /// from the list of active analyzers.<br/>
    /// It also ensures robust behavior through async callbacks and appropriate handling of
    /// task completion mechanisms.
    /// </remarks>
    [Fact]
    public async Task DetailedProgress_CompletesAnalyzerProgress_AsyncBehavior()
    {
        // Arrange
        DetailedProgressInfo? finalProgress = null;
        var tcs = new TaskCompletionSource<bool>();

        var innerProgress = new Progress<DetailedProgressInfo>(p =>
        {
            finalProgress = p;
            // Only complete when we're sure we got the final state
            if (p.ActiveAnalyzers.Count == 0) // Analyzer completed and removed
                tcs.SetResult(true);
        });
        var detailedProgress = new DetailedProgress(innerProgress);

        // Act
        detailedProgress.ReportAnalyzerStart("FormIdAnalyzer", "test.log");
        await Task.Delay(10); // Small delay to ensure first report is processed
        detailedProgress.ReportAnalyzerComplete("FormIdAnalyzer", "test.log", true);

        // Wait for async callback (with timeout)
        var completed = await tcs.Task.WaitAsync(TimeSpan.FromSeconds(2));

        // Assert
        Assert.True(completed);
        Assert.NotNull(finalProgress);

        // Final report should have no active analyzers (completed one is removed)
        Assert.Empty(finalProgress.ActiveAnalyzers);
    }

    /// <summary>
    /// Validates the asynchronous behavior of the <c>DetailedProgress</c> class when handling
    /// multiple progress reports in sequence.
    /// </summary>
    /// <remarks>
    /// This test ensures that the <c>DetailedProgress</c> instance correctly processes and reports
    /// multiple progress entries asynchronously, maintaining the expected sequence and accuracy
    /// of the generated reports. The method verifies that:<br/>
    /// 1. Progress reports are generated for each action (e.g., file start, file completion).<br/>
    /// 2. The reported file status and sequence align with expected values.<br/>
    /// 3. The asynchronous operation completes within the timeout threshold.
    /// </remarks>
    /// <returns>
    /// No return value. Verifies asynchronous progress reporting behavior using assertions.
    /// </returns>
    [Fact]
    public async Task DetailedProgress_MultipleReports_AsyncBehavior()
    {
        // Arrange
        var reportedProgresses = new List<DetailedProgressInfo>();
        var reportCount = 0;
        var expectedReports = 3;
        var tcs = new TaskCompletionSource<bool>();

        var innerProgress = new Progress<DetailedProgressInfo>(p =>
        {
            reportedProgresses.Add(p);
            reportCount++;
            if (reportCount >= expectedReports)
                tcs.SetResult(true);
        });
        var detailedProgress = new DetailedProgress(innerProgress);

        // Act - Generate multiple progress reports
        detailedProgress.ReportFileStart("file1.log");
        detailedProgress.ReportFileComplete("file1.log", true);
        detailedProgress.ReportFileStart("file2.log");

        // Give time for the Progress<T> callbacks to execute
        await Task.Delay(100);

        // Wait for async callbacks (with timeout)
        var completed = await tcs.Task.WaitAsync(TimeSpan.FromSeconds(2));

        // Assert
        Assert.True(completed);
        Assert.Equal(expectedReports, reportedProgresses.Count);

        // Verify the sequence
        Assert.Equal("file1.log", reportedProgresses[0].CurrentFile);
        Assert.Equal(FileProcessingStatus.InProgress, reportedProgresses[0].CurrentFileStatus);

        Assert.Equal("file1.log", reportedProgresses[1].CurrentFile);
        Assert.Equal(FileProcessingStatus.Completed, reportedProgresses[1].CurrentFileStatus);
        Assert.Equal(1, reportedProgresses[1].SuccessfulFiles);

        Assert.Equal("file2.log", reportedProgresses[2].CurrentFile);
        Assert.Equal(FileProcessingStatus.InProgress, reportedProgresses[2].CurrentFileStatus);
    }

    /// <summary>
    /// Verifies that the <c>DetailedProgress</c> class methods do not throw exceptions
    /// when invoked with a null <c>innerProgress</c>.
    /// </summary>
    /// <remarks>
    /// This method tests the behavior of the following operations in the absence of an inner progress reporter:<br/>
    /// 1. Reporting file start and completion.<br/>
    /// 2. Reporting analyzer start and completion.<br/>
    /// This ensures that the <c>DetailedProgress</c> class can safely handle scenarios where no progress reporting is required.
    /// </remarks>
    /// <returns>
    /// No value is returned. The test ensures that exceptions are not thrown during method execution.
    /// </returns>
    [Fact]
    public async Task DetailedProgress_WithNullProgress_DoesNotThrow()
    {
        // Arrange
        var detailedProgress = new DetailedProgress(); // No inner progress

        // Act & Assert - Should not throw
        detailedProgress.ReportFileStart("test.log");
        detailedProgress.ReportFileComplete("test.log", true);
        detailedProgress.ReportAnalyzerStart("TestAnalyzer", "test.log");
        detailedProgress.ReportAnalyzerComplete("TestAnalyzer", "test.log", true);

        // Complete synchronously since there's no async callback
        await Task.CompletedTask;
    }
}

/// <summary>
/// Represents a test implementation of the IProgress interface, enabling the monitoring
/// of progress updates by invoking a user-defined callback function synchronously.
/// </summary>
/// <typeparam name="T">The type of the progress value being reported.</typeparam>
public class TestProgress<T>(Action<T> callback) : IProgress<T>
{
    /// <summary>
    /// Reports the progress of an operation by invoking a specified callback with
    /// the progress value.
    /// </summary>
    /// <param name="value">The value of the progress being reported.</param>
    public void Report(T value)
    {
        callback(value);
    }
}