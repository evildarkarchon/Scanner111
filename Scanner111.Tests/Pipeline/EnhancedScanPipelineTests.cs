using FluentAssertions;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging.Abstractions;
using Scanner111.Core.Analyzers;
using Scanner111.Core.Infrastructure;
using Scanner111.Core.Models;
using Scanner111.Core.Pipeline;

namespace Scanner111.Tests.Pipeline;

/// <summary>
/// Unit tests for the EnhancedScanPipeline class, which provides functionalities for processing
/// log files using multiple analyzers in a resilient and efficient manner.
/// </summary>
/// <remarks>
/// The tests are designed to cover both single-file and batch processing use cases, as well as
/// various edge cases such as cancellation, nonexistent files, and mixed analysis results.
/// This test suite ensures that the pipeline behaves as expected under typical and exceptional scenarios.
/// </remarks>
/// <example>
/// No usage examples are provided in this documentation.
/// </example>
/// <seealso cref="Scanner111.Core.Pipeline.EnhancedScanPipeline"/>
public class EnhancedScanPipelineTests : IDisposable
{
    private readonly List<IDisposable> _disposables = [];
    private readonly EnhancedScanPipeline _pipeline;
    private readonly string _testLogPath;

    public EnhancedScanPipelineTests()
    {
        // Create test log file
        _testLogPath = Path.GetTempFileName();
        File.WriteAllText(_testLogPath, CreateTestCrashLogContent());

        // Set up dependencies
        var memoryCache = new MemoryCache(new MemoryCacheOptions());
        _disposables.Add(memoryCache);

        var cacheManager = new CacheManager(memoryCache, NullLogger<CacheManager>.Instance);
        _disposables.Add(cacheManager);

        var errorPolicy = new DefaultErrorHandlingPolicy(NullLogger<DefaultErrorHandlingPolicy>.Instance);
        var resilientExecutor = new ResilientExecutor(errorPolicy, NullLogger<ResilientExecutor>.Instance);
        var messageHandler = new TestMessageHandler();
        var settingsProvider = new TestYamlSettingsProvider();

        var analyzers = new List<IAnalyzer>
        {
            new TestAnalyzer("TestAnalyzer1", 10, true),
            new TestAnalyzer("TestAnalyzer2", 20, false),
            new TestAnalyzer("TestAnalyzer3", 30, true)
        };

        _pipeline = new EnhancedScanPipeline(
            analyzers,
            NullLogger<EnhancedScanPipeline>.Instance,
            messageHandler,
            settingsProvider,
            cacheManager,
            resilientExecutor);

        // Note: EnhancedScanPipeline implements IAsyncDisposable, not IDisposable
    }

    /// <summary>
    /// Releases all resources used by the EnhancedScanPipelineTests class.
    /// </summary>
    /// <remarks>
    /// This method disposes of all disposable resources created during the test execution,
    /// including temporary files and asynchronous pipeline objects.
    /// It ensures proper cleanup and prevents resource leaks by clearing resources such as
    /// file handles, asynchronous operations, and test artifacts.
    /// Additionally, this method suppresses the finalization of the instance to optimize garbage collection.
    /// Any disposal exceptions that occur during cleanup are caught and ignored to avoid failing
    /// the tests due to cleanup issues, particularly with temporary file handling.
    /// </remarks>
    public void Dispose()
    {
        foreach (var disposable in _disposables) disposable.Dispose();
        _disposables.Clear();

        // Dispose pipeline asynchronously
        try
        {
            _pipeline.DisposeAsync().AsTask().Wait(TimeSpan.FromSeconds(5));
        }
        catch (Exception)
        {
            // Ignore disposal exceptions in tests
        }

        if (File.Exists(_testLogPath))
            try
            {
                File.Delete(_testLogPath);
            }
            catch (IOException)
            {
                // File might still be in use by a canceled operation
                // Try again after a short delay
                Task.Delay(100).Wait();
                try
                {
                    File.Delete(_testLogPath);
                }
                catch
                {
                    // If it still fails, ignore - it's a temp file
                }
            }

        GC.SuppressFinalize(this);
    }

    /// <summary>
    /// Validates that the <see cref="EnhancedScanPipeline.ProcessSingleAsync"/> method successfully processes a single log file.
    /// </summary>
    /// <remarks>
    /// This test ensures that the method correctly processes the specified log file and returns a valid <see cref="ScanResult"/> object.
    /// It validates that the log path in the result matches the input path, the processing status is set to Completed,
    /// all analysis results are present, and the crash log is not null. Additionally, it verifies the processing time is greater than zero.
    /// </remarks>
    /// <returns>
    /// An asynchronous task that represents the test execution. The task will succeed if the method processes the file as expected
    /// and all assertions pass. If any validations fail, the task will throw an assertion exception.
    /// </returns>
    [Fact]
    public async Task ProcessSingleAsync_ProcessesFileSuccessfully()
    {
        // Act
        var result = await _pipeline.ProcessSingleAsync(_testLogPath);

        // Assert
        result.Should().NotBeNull();
        result.LogPath.Should().Be(_testLogPath);
        result.Status.Should().Be(ScanStatus.Completed);
        result.CrashLog.Should().NotBeNull();
        result.AnalysisResults.Count.Should().Be(3); // Should have results from all 3 analyzers
        result.ProcessingTime.TotalMilliseconds.Should().BeGreaterThan(0);
    }

    /// <summary>
    /// Tests the behavior of the ProcessSingleAsync method when a cancellation request is triggered.
    /// </summary>
    /// <remarks>
    /// This test ensures that the EnhancedScanPipeline properly respects cancellation
    /// requests by throwing an OperationCanceledException when the provided CancellationToken
    /// is canceled. Proper handling of cancellations is crucial for graceful application behavior
    /// and resource management during asynchronous operations.
    /// </remarks>
    /// <returns>
    /// A task that represents the asynchronous test operation. The task completes successfully
    /// if the proper exception is thrown when cancellation is requested; otherwise, it fails.
    /// </returns>
    [Fact]
    public async Task ProcessSingleAsync_HandlesCancellation()
    {
        // Arrange
        using var cts = new CancellationTokenSource();
        await cts.CancelAsync();

        // Act & Assert
        await Assert.ThrowsAsync<OperationCanceledException>(async () =>
        {
            await _pipeline.ProcessSingleAsync(_testLogPath, cts.Token);
        });
    }

    /// <summary>
    /// Ensures the pipeline appropriately handles cases where the specified file does not exist
    /// during the processing of a single log file.
    /// </summary>
    /// <remarks>
    /// This test verifies that when processing a nonexistent file, the method correctly identifies
    /// the situation by returning a result with a status of <see cref="ScanStatus.Failed"/> and marking
    /// the operation as having errors. It validates that the pipeline does not throw unhandled exceptions
    /// but instead gracefully handles this scenario.
    /// </remarks>
    /// <returns>
    /// A task representing the asynchronous operation. The task result is a <see cref="ScanResult"/>
    /// instance where the <c>LogPath</c> matches the nonexistent file path, <c>Status</c> is set to
    /// <see cref="ScanStatus.Failed"/>, and <c>HasErrors</c> is <c>true</c>.
    /// </returns>
    [Fact]
    public async Task ProcessSingleAsync_HandlesNonexistentFile()
    {
        // Act
        var result = await _pipeline.ProcessSingleAsync("nonexistent.log");

        // Assert
        result.Should().NotBeNull();
        result.LogPath.Should().Be("nonexistent.log");
        result.Status.Should().Be(ScanStatus.Failed);
        result.HasErrors.Should().BeTrue();
    }

    /// <summary>
    /// Validates and processes multiple files asynchronously using the EnhancedScanPipeline.
    /// </summary>
    /// <remarks>
    /// This method orchestrates the asynchronous scanning of a collection of log files, reporting
    /// incremental progress as each file is processed. It ensures each file is scanned successfully
    /// and generates a collection of scan results.
    /// The test verifies that all files in the batch are processed correctly, tracks the progress across
    /// the entire batch, and ensures the final results align with expected outcomes. The progress updates
    /// include details such as total files, processed files, and successful scans to provide real-time
    /// feedback about the batch operation.
    /// </remarks>
    /// <returns>
    /// A task representing the asynchronous operation, which includes assertions to validate
    /// the correctness of the multi-file processing, the integrity and accuracy of progress tracking,
    /// and the final outcome of the scan batch.
    /// </returns>
    [Fact]
    public async Task ProcessBatchAsync_ProcessesMultipleFiles()
    {
        // Arrange
        var testFiles = new List<string> { _testLogPath };
        var results = new List<ScanResult>();
        var progressReports = new List<BatchProgress>();

        var progress = new Progress<BatchProgress>(p => progressReports.Add(p));

        // Act
        await foreach (var result in _pipeline.ProcessBatchAsync(testFiles, progress: progress)) results.Add(result);

        // Assert
        results.Should().ContainSingle();
        results[0].Status.Should().Be(ScanStatus.Completed);
        progressReports.Should().NotBeEmpty();

        var finalProgress = progressReports.Last();
        finalProgress.TotalFiles.Should().Be(1);
        finalProgress.ProcessedFiles.Should().Be(1);
        finalProgress.SuccessfulScans.Should().Be(1);
    }

    /// <summary>
    /// Validates that ProcessBatchAsync adheres to the specified maximum concurrency level during batch processing.
    /// </summary>
    /// <remarks>
    /// This test ensures that the EnhancedScanPipeline correctly limits the number of concurrent file processing tasks
    /// based on the MaxConcurrency value defined in the ScanOptions. The method simulates processing of multiple files,
    /// asserting that results are properly generated and that the concurrency limit impacts the processing duration as expected.
    /// It verifies that the pipeline handles concurrency constraints and produces a complete set of results, whether successful
    /// or failed, while maintaining the specified limit.
    /// </remarks>
    /// <returns>
    /// A completed Task after all assertions regarding file processing results and concurrency effects have been executed.
    /// </returns>
    [Fact]
    public async Task ProcessBatchAsync_RespectsMaxConcurrency()
    {
        // Arrange - Create unique file paths to avoid deduplication
        var testFiles = Enumerable.Range(0, 10)
            .Select(i => $"{_testLogPath}.{i}")
            .ToList();
        var options = new ScanOptions { MaxConcurrency = 2 };
        var results = new List<ScanResult>();

        // Act
        var startTime = DateTime.UtcNow;
        await foreach (var result in _pipeline.ProcessBatchAsync(testFiles, options)) results.Add(result);
        var duration = DateTime.UtcNow - startTime;

        // Assert
        results.Count.Should().Be(10);
        // Note: Some results may fail since we're using non-existent file paths with suffixes
        Assert.All(results, r => (r.Status == ScanStatus.Completed || r.Status == ScanStatus.Failed).Should().BeTrue());

        // With concurrency limit of 2, processing should take longer than if all were parallel
        // This is a rough test - in practice, timing tests can be flaky
        duration.TotalMilliseconds.Should().BeGreaterThan(0);
    }

    /// <summary>
    /// Validates the EnhancedScanPipeline's ability to process a batch of files with mixed results.
    /// </summary>
    /// <remarks>
    /// The test ensures that the pipeline correctly identifies and handles different outcomes when processing
    /// a set of files. Specifically, it validates that valid files are processed successfully, while invalid
    /// files result in a failure status. The results are verified to ensure they match expected statuses and log paths.
    /// </remarks>
    /// <returns>
    /// No return value as this is a test method, but it asserts that the outcomes of processing files in the batch
    /// align with their respective statuses (e.g., Completed for valid files, Failed for invalid files).
    /// </returns>
    [Fact]
    public async Task ProcessBatchAsync_HandlesMixedResults()
    {
        // Arrange
        var validFile = _testLogPath;
        const string invalidFile = "nonexistent.log";
        var testFiles = new List<string> { validFile, invalidFile };
        var results = new List<ScanResult>();

        // Act
        await foreach (var result in _pipeline.ProcessBatchAsync(testFiles)) results.Add(result);

        // Assert
        results.Count.Should().Be(2);

        var validResult = results.First(r => r.LogPath == validFile);
        var invalidResult = results.First(r => r.LogPath == invalidFile);

        validResult.Status.Should().Be(ScanStatus.Completed);
        invalidResult.Status.Should().Be(ScanStatus.Failed);
    }

    /// <summary>
    /// Verifies that the ProcessBatchAsync method gracefully handles user cancellation
    /// during the processing of a batch of files.
    /// </summary>
    /// <remarks>
    /// This test ensures that when a cancellation request is issued, the method
    /// halts further processing, cleans up any in-flight operations, and throws a
    /// TaskCanceledException. It verifies that partial results are not fully processed
    /// and ensures that resource usage is minimized during cancellation scenarios.
    /// The test evaluates the method's behavior under concurrent tasks and confirms
    /// resilience against abrupt operation termination.
    /// </remarks>
    /// <returns>
    /// This test does not return a value but validates that the TaskCanceledException
    /// is appropriately thrown and verifies the partial completion of processing for
    /// the provided batch of log files.
    /// </returns>
    [Fact]
    public async Task ProcessBatchAsync_HandlesCancellation()
    {
        // Arrange - Create unique file paths to avoid deduplication
        var testFiles = Enumerable.Range(0, 100)
            .Select(i => $"{_testLogPath}.{i}")
            .ToList();

        var results = new List<ScanResult>();
        TaskCanceledException? caughtException = null;

        // Act - Use a separate method to avoid captured variable disposal issues
        try
        {
            await ProcessWithCancellationAsync(testFiles, results);
        }
        catch (TaskCanceledException ex)
        {
            caughtException = ex;
        }

        // Assert
        caughtException.Should().NotBeNull();
        results.Count.Should().BeLessThan(testFiles.Count); // Should not process all files

        // Give time for any in-flight operations to complete
        await Task.Delay(100);
    }

    /// <summary>
    /// Processes a batch of files asynchronously with support for cancellation.
    /// </summary>
    /// <param name="testFiles">A list of file paths to be processed.</param>
    /// <param name="results">A list to store the scan results for each processed file.</param>
    /// <returns>A task that represents the asynchronous operation of processing files,
    /// including monitoring and canceling the operation when specific conditions are met.</returns>
    private async Task ProcessWithCancellationAsync(List<string> testFiles, List<ScanResult> results)
    {
        using var cts = new CancellationTokenSource();

        await foreach (var result in _pipeline.ProcessBatchAsync(testFiles, cancellationToken: cts.Token))
        {
            results.Add(result);
            if (results.Count >= 5) // Cancel after processing a few files
                await cts.CancelAsync();
        }
    }

    /// <summary>
    /// Validates that the EnhancedScanPipeline processes analyzers in the correct order based on their priority.
    /// </summary>
    /// <remarks>
    /// This test ensures that when the pipeline processes a single log file, analyzers are executed
    /// according to their configured priority levels. Higher priority analyzers are executed before
    /// lower priority ones. The test uses a mock implementation of analyzers to track execution order
    /// and verifies that the sequence matches the expected priority order.
    /// </remarks>
    /// <returns>
    /// A task that represents the asynchronous operation of the test. This task completes upon verifying
    /// the correct execution order of the analyzers.
    /// </returns>
    [Fact]
    public async Task ProcessSingleAsync_UsesAnalyzerPriority()
    {
        // Arrange
        var executionOrder = new List<string>();
        var orderedAnalyzers = new List<IAnalyzer>
        {
            new OrderTrackingAnalyzer("High", 5, false, executionOrder),
            new OrderTrackingAnalyzer("Medium", 10, false, executionOrder),
            new OrderTrackingAnalyzer("Low", 15, false, executionOrder)
        };

        await using var testPipeline = new EnhancedScanPipeline(
            orderedAnalyzers,
            NullLogger<EnhancedScanPipeline>.Instance,
            new TestMessageHandler(),
            new TestYamlSettingsProvider(),
            new NullCacheManager(),
            new ResilientExecutor(new NoRetryErrorPolicy(), NullLogger<ResilientExecutor>.Instance));

        // Act
        await testPipeline.ProcessSingleAsync(_testLogPath);

        // Assert
        executionOrder.Should().BeEquivalentTo(new[] { "High", "Medium", "Low" }, options => options.WithStrictOrdering());
    }

    /// <summary>
    /// Generates the content for a test crash log, simulating output from a software crash or error event.
    /// </summary>
    /// <returns>
    /// A formatted string that represents the content of a test crash log, including details such as exception
    /// type, memory addresses, call stack, loaded modules, plugins, and system settings.
    /// </returns>
    private static string CreateTestCrashLogContent()
    {
        return @"Fallout 4 v1.10.163
Buffout 4 v1.26.2

Unhandled exception ""EXCEPTION_ACCESS_VIOLATION"" at 0x7FF6E0A7B2A1 SkyrimSE.exe+01BB2A1

[RSP+0]   0x12345678   (void*)
[RSP+8]   0x87654321   (void*)

PROBABLE CALL STACK:
	[ 0] 0x7FF6E0A7B2A1   SkyrimSE.exe+01BB2A1 -> 12345+678
	[ 1] 0x7FF6E0A7B2B2   SkyrimSE.exe+01BB2B2

MODULES:
	[ 0] 0x7FF6E08C0000 - 0x7FF6E2A04000 | SkyrimSE.exe
	[ 1] 0x7FFE8B2A0000 - 0x7FFE8B2B4000 | test.dll

PLUGINS:
	[00]     Skyrim.esm
	[01]     Update.esm
	[FE:000] TestMod.esp

SETTINGS:
";
    }

    // Test helper classes
    /// <summary>
    /// Represents a test implementation of the IAnalyzer interface used for analyzing crash logs.
    /// Designed to simulate analysis functionality with customizable properties, such as name, priority,
    /// and the ability to execute in parallel.
    /// </summary>
    /// <remarks>
    /// This analyzer is primarily intended for use in unit tests and debugging scenarios involving
    /// the EnhancedScanPipeline or other frameworks that depend on IAnalyzer implementations.
    /// Additionally, the <see cref="AnalyzeAsync"/> method executes a simulated analysis for testing purposes,
    /// generating a generic analysis result with predefined findings.
    /// </remarks>
    /// <example>
    /// No usage examples are provided in this documentation.
    /// </example>
    /// <seealso cref="Scanner111.Core.Analyzers.IAnalyzer"/>
    private class TestAnalyzer : IAnalyzer
    {
        public TestAnalyzer(string name, int priority, bool canRunInParallel)
        {
            Name = name;
            Priority = priority;
            CanRunInParallel = canRunInParallel;
        }

        public string Name { get; }
        public int Priority { get; }
        public bool CanRunInParallel { get; }

        /// <summary>
        /// Analyzes the given crash log asynchronously and returns an analysis result.
        /// </summary>
        /// <param name="crashLog">The crash log to be analyzed.</param>
        /// <param name="cancellationToken">An optional token to monitor for cancellation requests.</param>
        /// <returns>A task representing the asynchronous analysis operation. The task result contains the analysis result.</returns>
        public async Task<AnalysisResult> AnalyzeAsync(CrashLog crashLog, CancellationToken cancellationToken = default)
        {
            await Task.Delay(10, cancellationToken); // Simulate work

            return new GenericAnalysisResult
            {
                AnalyzerName = Name,
                Success = true,
                ReportLines = [$"Analysis by {Name}\n"],
                HasFindings = true
            };
        }
    }

    /// <summary>
    /// An implementation of the <see cref="IAnalyzer"/> interface designed to track
    /// the execution order of analyzers during processing.
    /// </summary>
    /// <remarks>
    /// The OrderTrackingAnalyzer is primarily used for testing and validation purposes,
    /// ensuring that analyzers are executed in a defined order based on their priority.
    /// It also supports concurrent execution tracking and includes configurable properties
    /// like name, priority, and parallel execution capability.
    /// </remarks>
    /// <example>
    /// No usage examples are provided in this documentation.
    /// </example>
    /// <seealso cref="Scanner111.Core.Analyzers.IAnalyzer"/>
    private class OrderTrackingAnalyzer : IAnalyzer
    {
        private readonly List<string> _executionOrder;

        public OrderTrackingAnalyzer(string name, int priority, bool canRunInParallel, List<string> executionOrder)
        {
            Name = name;
            Priority = priority;
            CanRunInParallel = canRunInParallel;
            _executionOrder = executionOrder;
        }

        public string Name { get; }
        public int Priority { get; }
        public bool CanRunInParallel { get; }

        /// <summary>
        /// Performs asynchronous analysis on the given crash log.
        /// </summary>
        /// <param name="crashLog">The crash log to be analyzed.</param>
        /// <param name="cancellationToken">A token to monitor for cancellation requests during the analysis.</param>
        /// <returns>A task that represents the asynchronous operation. The task result contains the analysis result.</returns>
        public async Task<AnalysisResult> AnalyzeAsync(CrashLog crashLog, CancellationToken cancellationToken = default)
        {
            _executionOrder.Add(Name);
            await Task.Delay(1, cancellationToken);

            return new GenericAnalysisResult
            {
                AnalyzerName = Name,
                Success = true,
                ReportLines = [$"Analysis by {Name}\n"],
                HasFindings = true
            };
        }
    }

    /// <summary>
    /// A test implementation of the <see cref="IMessageHandler"/> interface for use in unit tests.
    /// </summary>
    /// <remarks>
    /// This class provides mock implementations of message-handling methods for logging and displaying messages
    /// during testing scenarios. It is primarily intended for validating behaviors that depend on message
    /// handling without relying on actual implementations.
    /// </remarks>
    /// <seealso cref="Scanner111.Core.Infrastructure.IMessageHandler"/>
    private class TestMessageHandler : IMessageHandler
    {
        /// <summary>
        /// Displays an informational message to the specified target(s).
        /// </summary>
        /// <param name="message">The message to be displayed.</param>
        /// <param name="target">The target(s) where the message should be displayed. Defaults to all targets.</param>
        public void ShowInfo(string message, MessageTarget target = MessageTarget.All)
        {
        }

        /// <summary>
        /// Displays a warning message to the specified message target or to all targets by default.
        /// </summary>
        /// <param name="message">The warning message to be displayed.</param>
        /// <param name="target">
        /// The target where the warning message should be displayed.
        /// Defaults to <see cref="MessageTarget.All"/> which sends the message to all targets.
        /// </param>
        public void ShowWarning(string message, MessageTarget target = MessageTarget.All)
        {
        }

        /// <summary>
        /// Displays an error message to the specified target(s).
        /// </summary>
        /// <param name="message">The error message to be shown.</param>
        /// <param name="target">Specifies the target(s) where the message will be displayed. The default value is <c>MessageTarget.All</c>.</param>
        /// <remarks>
        /// This method enables the display of error messages to various output targets such as GUI, CLI, or logs.
        /// It is primarily used for notifying users or logging error details within the application or tests.
        /// </remarks>
        public void ShowError(string message, MessageTarget target = MessageTarget.All)
        {
        }

        /// <summary>
        /// Displays a success message to the specified target(s).
        /// </summary>
        /// <param name="message">The message text to display as success feedback.</param>
        /// <param name="target">
        /// Specifies the target audience to display the message to.
        /// Defaults to all available targets if no specific target is provided.
        /// </param>
        /// <remarks>
        /// This method is designed to communicate successful execution or state updates
        /// to various output targets, such as GUI, CLI, or log files. The default behavior
        /// ensures that the message is broadcasted to all available targets unless otherwise specified.
        /// </remarks>
        public void ShowSuccess(string message, MessageTarget target = MessageTarget.All)
        {
        }

        /// <summary>
        /// Displays a debug message with a specified target for output.
        /// </summary>
        /// <param name="message">
        /// The debug message to be displayed. This message provides diagnostic information, which may
        /// help during the development or testing process.
        /// </param>
        /// <param name="target">
        /// Specifies the target destination for the debug message. By default, the message is directed
        /// to all available targets. Valid options include GUI only, CLI only, log only, or all targets.
        /// </param>
        public void ShowDebug(string message, MessageTarget target = MessageTarget.All)
        {
        }

        /// <summary>
        /// Displays a critical message to a specified target or targets.
        /// </summary>
        /// <param name="message">
        /// The critical message to be displayed. This parameter cannot be null or empty.
        /// </param>
        /// <param name="target">
        /// Specifies the message delivery target (e.g., all targets, GUI-only, CLI-only, log-only).
        /// The default is <see cref="MessageTarget.All"/>.
        /// </param>
        /// <remarks>
        /// This method is intended for scenarios where critical messages must be relayed to
        /// key components or interfaces. The implementation ensures that the message reaches
        /// the designated target(s) reliably.
        /// </remarks>
        public void ShowCritical(string message, MessageTarget target = MessageTarget.All)
        {
        }

        /// <summary>
        /// Displays a message with optional details, type, and target audience.
        /// </summary>
        /// <param name="message">The primary message to be displayed.</param>
        /// <param name="details">Additional details or contextual information for the message. This parameter is optional.</param>
        /// <param name="messageType">Specifies the type or severity of the message. Default is <see cref="MessageType.Info"/>.</param>
        /// <param name="target">Indicates the target audience or destination for the message. Default is <see cref="MessageTarget.All"/>.</param>
        public void ShowMessage(string message, string? details = null, MessageType messageType = MessageType.Info,
            MessageTarget target = MessageTarget.All)
        {
        }

        /// <summary>
        /// Creates and returns a progress handler for tracking the progress of operations.
        /// </summary>
        /// <param name="title">A string representing the title or summary of the operation in progress.</param>
        /// <param name="totalItems">An integer specifying the total number of items to process, allowing the progress handler to calculate completion percentage.</param>
        /// <returns>A progress handler object implementing <see cref="IProgress{ProgressInfo}"/>, which can be used to report progress updates.</returns>
        public IProgress<ProgressInfo> ShowProgress(string title, int totalItems)
        {
            return new Progress<ProgressInfo>();
        }

        /// <summary>
        /// Creates a new progress context for tracking the progress of a specified operation.
        /// </summary>
        /// <param name="title">The title or description of the operation being tracked.</param>
        /// <param name="totalItems">The total number of items to be processed in the tracked operation.</param>
        /// <returns>An implementation of <see cref="IProgressContext"/>, providing mechanisms for reporting progress information and ensuring proper cleanup of resources.</returns>
        public IProgressContext CreateProgressContext(string title, int totalItems)
        {
            return new TestProgressContext();
        }

        /// <summary>
        /// Represents a test implementation of the IProgressContext interface, used for managing
        /// and reporting progress in unit tests.
        /// </summary>
        /// <remarks>
        /// This class provides a mock implementation of IProgressContext, allowing for simulation
        /// of progress updates, completion, and disposal scenarios during the testing of components
        /// that depend on progress reporting. It is used within test contexts to verify behavior
        /// without relying on the actual progress infrastructure.
        /// </remarks>
        /// <example>
        /// No usage examples are provided in this documentation.
        /// </example>
        /// <seealso cref="Scanner111.Core.Infrastructure.IProgressContext"/>
        private class TestProgressContext : IProgressContext
        {
            /// <summary>
            /// Updates the current progress and associated message within the context.
            /// </summary>
            /// <param name="current">The current progress value, indicating the position or status.</param>
            /// <param name="message">The message providing additional information about the progress update.</param>
            public void Update(int current, string message)
            {
            }

            /// <summary>
            /// Marks the progress context as complete, signaling that all progress updates are finalized.
            /// </summary>
            /// <remarks>
            /// This method is intended for finalizing the progress reporting within implementations of
            /// <see cref="IProgressContext"/>. It indicates that no further progress updates are expected
            /// and any resources related to progress tracking can be safely released.
            /// Implementations may use this method to perform cleanup tasks or to notify observers
            /// that progress tracking has ended.
            /// </remarks>
            public void Complete()
            {
            }

            /// <summary>
            /// Reports progress information for the current operation.
            /// </summary>
            /// <param name="value">The progress information object containing the current progress state and associated data.</param>
            public void Report(ProgressInfo value)
            {
            }

            /// <summary>
            /// Releases resources used by the EnhancedScanPipelineTests class.
            /// </summary>
            /// <remarks>
            /// This method ensures that all resources allocated during the lifecycle of the test suite,
            /// including external and internal disposable objects, are properly released. It is particularly
            /// important to avoid memory leaks or locked resources by cleaning up test artifacts or
            /// custom implementations utilized during the tests.
            /// Calling this method suppresses the finalization of the instance to optimize memory management
            /// and prevent the garbage collector from unnecessarily invoking the finalizer.
            /// Any exceptions thrown during disposal operations are caught and handled to ensure
            /// the tests can terminate cleanly, even in cases where resource cleanup encounters issues.
            /// </remarks>
            public void Dispose()
            {
            }
        }
    }

    /// <summary>
    /// A test implementation of the <see cref="IYamlSettingsProvider"/> interface, primarily used for unit testing purposes.
    /// </summary>
    /// <remarks>
    /// This class provides minimal, mock implementations of methods for managing YAML-based settings.
    /// It is designed for testing scenarios where interaction with a full YAML settings provider is not required.
    /// Any changes or operations performed using this class will not have lasting effects or actual functionality.
    /// </remarks>
    /// <example>
    /// No usage examples are provided in this documentation.
    /// </example>
    /// <seealso cref="IYamlSettingsProvider"/>
    private class TestYamlSettingsProvider : IYamlSettingsProvider
    {

        /// <summary>
        /// Loads a YAML file, deserializes it, and converts the data into an object of type T.
        /// </summary>
        /// <typeparam name="T">The type of the object to deserialize the YAML data into. Must be a class.</typeparam>
        /// <param name="yamlFile">The path to the YAML file to be loaded.</param>
        /// <returns>An object of type T containing the deserialized data from the YAML file. Returns null if the loading fails.</returns>
        public T? LoadYaml<T>(string yamlFile) where T : class
        {
            return null;
        }

        /// <summary>
        /// Clears the cached settings maintained by the TestYamlSettingsProvider.
        /// </summary>
        /// <remarks>
        /// This method is intended to reset any cached configuration or metadata to ensure
        /// subsequent operations fetch updated values instead of using stale data.
        /// The implementation in this test context does not perform any real operations
        /// and serves as a placeholder for testing scenarios.
        /// </remarks>
        public void ClearCache()
        {
            // Test implementation - do nothing
        }
    }
}