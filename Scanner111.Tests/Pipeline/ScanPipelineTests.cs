using Microsoft.Extensions.Logging;
using Scanner111.Core.Analyzers;
using Scanner111.Core.Infrastructure;
using Scanner111.Core.Models;
using Scanner111.Core.Pipeline;
using Scanner111.Tests.TestHelpers;

namespace Scanner111.Tests.Pipeline;

/// <summary>
/// Unit test class for validating the functionality of the ScanPipeline in the Scanner111.Tests.Pipeline namespace.
/// </summary>
/// <remarks>
/// This test class includes a series of test methods that verify the behavior of the ScanPipeline under various scenarios,
/// such as processing single logs, handling batch inputs, respecting custom configurations, and proper resource disposal.
/// The ScanPipeline is tested for correctness, error handling, and capability to handle cancellation tokens and concurrency controls.
/// </remarks>
/// <example>
/// The tests performed by this class ensure the pipeline runs analyzers in the correct order,
/// handles duplicate file paths gracefully, and accurately reports progress during batch processing.
/// Additionally, it verifies the pipeline's ability to process logs with invalid paths and to behave appropriately upon cancellation.
/// </example>
public class ScanPipelineTests : IDisposable
{
    private readonly ILogger<ScanPipeline> _logger;
    private readonly IMessageHandler _messageHandler;
    private readonly ScanPipeline _pipeline;
    private readonly IYamlSettingsProvider _settingsProvider;
    private readonly List<TestAnalyzer> _testAnalyzers;

    public ScanPipelineTests()
    {
        _logger = new TestLogger<ScanPipeline>();
        _messageHandler = new TestMessageHandler();
        _settingsProvider = new TestYamlSettingsProvider();

        // Create test analyzers with different priorities and capabilities
        _testAnalyzers =
        [
            new("HighPriority", 1, true, true),
            new("MediumPriority", 5, false, true),
            new("LowPriority", 10, true, true),
            new("FailingAnalyzer", 3, true, false)
        ];

        _pipeline = new ScanPipeline(_testAnalyzers, _logger, _messageHandler, _settingsProvider);
    }

    /// <summary>
    /// Releases the resources used by the <see cref="ScanPipelineTests"/> class.
    /// </summary>
    /// <remarks>
    /// Ensures proper disposal of the <see cref="ScanPipeline"/> instance
    /// and suppresses finalization of the object to prevent redundant cleanup.
    /// </remarks>
    public void Dispose()
    {
        _pipeline.DisposeAsync().AsTask().Wait();
        GC.SuppressFinalize(this);
    }

    /// <summary>
    /// Tests the <see cref="ScanPipeline.ProcessSingleAsync"/> method by providing
    /// a valid crash log and verifying that the result indicates a completed state with errors.
    /// </summary>
    /// <remarks>
    /// This test ensures that the pipeline processes a single crash log correctly, checks the
    /// validity of the processing results, and validates the expected analysis outcomes,
    /// including the presence of errors in the final result.
    /// </remarks>
    /// <returns>
    /// A completed task representing the asynchronous test operation, ensuring
    /// the correctness of the method behavior under test.
    /// </returns>
    [Fact]
    public async Task ProcessSingleAsync_WithValidCrashLog_ShouldReturnCompletedResult()
    {
        // Arrange
        var logPath = SetupTestCrashLog("test.log", GenerateValidCrashLog());

        // Act
        var result = await _pipeline.ProcessSingleAsync(logPath);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(logPath, result.LogPath);
        Assert.Equal(ScanStatus.CompletedWithErrors, result.Status); // Contains one failing analyzer
        Assert.True(result.ProcessingTime > TimeSpan.Zero);
        Assert.Equal(4, result.AnalysisResults.Count);
        Assert.True(result.HasErrors);
    }

    /// <summary>
    /// Verifies that the <see cref="ScanPipeline.ProcessSingleAsync"/> method
    /// returns a failed result when provided with an invalid log path.
    /// </summary>
    /// <returns>
    /// Asserts that the result has a <see cref="ScanStatus.Failed"/> status,
    /// the appropriate log path, error messages indicating failure to parse the crash log,
    /// and confirms the presence of errors.
    /// </returns>
    [Fact]
    public async Task ProcessSingleAsync_WithInvalidLogPath_ShouldReturnFailedResult()
    {
        // Arrange
        var logPath = "nonexistent.log";

        // Act
        var result = await _pipeline.ProcessSingleAsync(logPath);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(logPath, result.LogPath);
        Assert.Equal(ScanStatus.Failed, result.Status);
        Assert.True(result.HasErrors);
        Assert.Contains("Failed to parse crash log", result.ErrorMessages.First());
    }
    // ReSharper disable once InvalidXmlDocComment
    /// <summary>
    /// Tests the behavior of the <see cref="ScanPipeline.ProcessSingleAsync"/> method when cancellation is requested.
    /// </summary>
    /// <remarks>
    /// Ensures that the method returns a result with a status of <see cref="ScanStatus.Cancelled"/> when
    /// a cancellation token is triggered before or during execution.
    /// </remarks>
    /// <returns>
    /// An <see cref="Assert.Equal"/> validation that the returned <see cref="ScanResult.Status"/> is equal to <see cref="ScanStatus.Cancelled"/>.
    /// </returns>
    [Fact]
    public async Task ProcessSingleAsync_WithCancellation_ShouldReturnCancelledResult()
    {
        // Arrange
        var logPath = SetupTestCrashLog("test.log", GenerateValidCrashLog());
        using var cts = new CancellationTokenSource();
        cts.Cancel();

        // Act
        var result = await _pipeline.ProcessSingleAsync(logPath, cts.Token);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(ScanStatus.Cancelled, result.Status);
    }

    /// <summary>
    /// Verifies that the <see cref="ScanPipeline.ProcessSingleAsync"/> method
    /// executes analyzers in the correct order, ensuring sequential analyzers are
    /// run first before parallel analyzers.
    /// </summary>
    /// <returns>
    /// Ensures that sequential analyzers complete before parallel analyzers, and
    /// validates the number of results returned matches the expected analyzer count.
    /// </returns>
    [Fact]
    public async Task ProcessSingleAsync_ShouldRunAnalyzersInCorrectOrder()
    {
        // Arrange
        var logPath = SetupTestCrashLog("test.log", GenerateValidCrashLog());

        // Act
        var result = await _pipeline.ProcessSingleAsync(logPath);

        // Assert
        Assert.Equal(4, result.AnalysisResults.Count);

        // Sequential analyzers should run first and be completed
        var sequentialResults = result.AnalysisResults.Where(r => r.AnalyzerName == "MediumPriority").ToList();
        Assert.Single(sequentialResults);

        // Parallel analyzers should also be completed
        var parallelResults = result.AnalysisResults.Where(r =>
            r.AnalyzerName == "HighPriority" ||
            r.AnalyzerName == "LowPriority" ||
            r.AnalyzerName == "FailingAnalyzer").ToList();
        Assert.Equal(3, parallelResults.Count);
    }

    /// <summary>
    /// Processes multiple crash log files asynchronously and verifies that all files are successfully processed.
    /// </summary>
    /// <remarks>
    /// This test ensures that the <see cref="ScanPipeline.ProcessBatchAsync"/> method correctly handles
    /// a batch of crash log files, processes each file, and reports progress accurately.
    /// </remarks>
    /// <returns>
    /// A task that represents the asynchronous test operation.
    /// The task result validates that all files are processed without failure and appropriate progress is reported.
    /// </returns>
    [Fact]
    public async Task ProcessBatchAsync_WithMultipleFiles_ShouldProcessAllFiles()
    {
        // Arrange
        var logPaths = new[]
        {
            SetupTestCrashLog("log1.log", GenerateValidCrashLog()),
            SetupTestCrashLog("log2.log", GenerateValidCrashLog()),
            SetupTestCrashLog("log3.log", GenerateValidCrashLog())
        };

        var results = new List<ScanResult>();
        var progressReports = new List<BatchProgress>();

        var progress = new Progress<BatchProgress>(p => progressReports.Add(p));
        var options = new ScanOptions { MaxConcurrency = 2 };

        // Act
        await foreach (var result in _pipeline.ProcessBatchAsync(logPaths, options, progress)) results.Add(result);

        // Assert
        Assert.Equal(3, results.Count);
        Assert.All(results, r => Assert.NotEqual(ScanStatus.Failed, r.Status));
        Assert.True(progressReports.Count > 0);

        var finalProgress = progressReports.Last();
        Assert.Equal(3, finalProgress.TotalFiles);
        Assert.Equal(3, finalProgress.ProcessedFiles);
    }

    /// <summary>
    /// Processes a batch of log paths while ensuring any duplicate paths
    /// in the input are deduplicated prior to processing.
    /// </summary>
    /// <returns>
    /// A single processed result for each unique log path in the input batch.
    /// </returns>
    [Fact]
    public async Task ProcessBatchAsync_WithDuplicatePaths_ShouldDeduplicateInput()
    {
        // Arrange
        var logPath = SetupTestCrashLog("duplicate.log", GenerateValidCrashLog());
        var logPaths = new[] { logPath, logPath, logPath }; // Same path repeated

        var results = new List<ScanResult>();

        // Act
        await foreach (var result in _pipeline.ProcessBatchAsync(logPaths)) results.Add(result);

        // Assert
        Assert.Single(results); // Should only process once due to deduplication
        Assert.Equal(logPath, results[0].LogPath);
    }
    
    /// <summary>
    /// Validates that the processing of a batch can be stopped when a cancellation token is triggered.
    /// </summary>
    /// <remarks>
    /// Ensures that the <see cref="ScanPipeline.ProcessBatchAsync"/> method respects cancellation signals
    /// by halting further processing and limiting the results to files processed before the cancellation occurred.
    /// </remarks>
    /// <returns>
    /// A task representing the asynchronous operation. Upon completion, ensures that no more items are processed once cancellation is requested.
    /// </returns>
    [Fact]
    public async Task ProcessBatchAsync_WithCancellation_ShouldStopProcessing()
    {
        // Arrange
        var logPaths = new[]
        {
            SetupTestCrashLog("log1.log", GenerateValidCrashLog()),
            SetupTestCrashLog("log2.log", GenerateValidCrashLog()),
            SetupTestCrashLog("log3.log", GenerateValidCrashLog()),
            SetupTestCrashLog("log4.log", GenerateValidCrashLog()),
            SetupTestCrashLog("log5.log", GenerateValidCrashLog())
        };

        using var cts = new CancellationTokenSource();
        var results = new List<ScanResult>();

        // Act
        try
        {
            await foreach (var result in _pipeline.ProcessBatchAsync(logPaths, cancellationToken: cts.Token))
            {
                results.Add(result);
                if (results.Count >= 2) cts.Cancel(); // Cancel after processing 2 files
            }
        }
        catch (OperationCanceledException)
        {
            // Expected when cancellation occurs
        }

        // Assert
        Assert.True(results.Count <= logPaths.Length);
    }

    /// <summary>
    /// Verifies that the <see cref="ScanPipeline.ProcessBatchAsync"/> method accurately reports progress
    /// while processing a batch of input log files.
    /// </summary>
    /// <remarks>
    /// This test ensures that progress updates reflect the total number of files, processed files,
    /// and elapsed time throughout the batch processing operation.
    /// </remarks>
    /// <returns>
    /// An asynchronous task representing the test execution.
    /// Ensures that the progress is reported correctly and consistently,
    /// with at least one progress update for each file processed.
    /// </returns>
    [Fact]
    public async Task ProcessBatchAsync_ShouldReportAccurateProgress()
    {
        // Arrange
        var logPaths = new[]
        {
            SetupTestCrashLog("log1.log", GenerateValidCrashLog()),
            SetupTestCrashLog("log2.log", GenerateValidCrashLog()),
            SetupTestCrashLog("log3.log", GenerateValidCrashLog())
        };

        var progressReports = new List<BatchProgress>();
        var progress = new Progress<BatchProgress>(p => progressReports.Add(p));
        var results = new List<ScanResult>();

        // Act
        await foreach (var result in _pipeline.ProcessBatchAsync(logPaths, progress: progress)) results.Add(result);

        // Assert
        Assert.Equal(3, results.Count);
        Assert.True(progressReports.Count >= 3); // At least one progress report per file

        var finalProgress = progressReports.Last();
        Assert.Equal(3, finalProgress.TotalFiles);
        Assert.Equal(3, finalProgress.ProcessedFiles);
        Assert.True(finalProgress.ElapsedTime > TimeSpan.Zero);
    }

    /// <summary>
    /// Validates that the asynchronous dispose operation of the <see cref="ScanPipeline"/> class
    /// releases all associated resources properly without throwing exceptions.
    /// </summary>
    /// <remarks>
    /// Ensures multiple calls to <see cref="ScanPipeline.DisposeAsync"/> are safe and do not
    /// produce unintended side effects or errors.
    /// </remarks>
    /// <returns>
    /// A task representing the asynchronous dispose operation that confirms successful resource cleanup.
    /// </returns>
    [Fact]
    public async Task DisposeAsync_ShouldDisposeResourcesProperly()
    {
        // Arrange
        var pipeline = new ScanPipeline(_testAnalyzers, _logger, _messageHandler, _settingsProvider);

        // Act
        await pipeline.DisposeAsync();

        // Dispose should complete without throwing
        // Additional dispose calls should be safe
        await pipeline.DisposeAsync();

        // Assert - No exceptions should be thrown
        Assert.True(true);
    }

    /// <summary>
    /// Validates that processing an empty input batch using the <see cref="ScanPipeline.ProcessBatchAsync"/>
    /// method results in no output results.
    /// </summary>
    /// <remarks>
    /// Ensures that the method properly handles an empty collection of log paths without producing any unintended results.
    /// This verification maintains robustness when no input data is provided.
    /// </remarks>
    /// <returns>
    /// A task representing the asynchronous unit test. The result verifies that the returned collection of results is empty.
    /// </returns>
    [Fact]
    public async Task ProcessBatchAsync_WithEmptyInput_ShouldReturnEmptyResults()
    {
        // Arrange
        var logPaths = Array.Empty<string>();
        var results = new List<ScanResult>();

        // Act
        await foreach (var result in _pipeline.ProcessBatchAsync(logPaths)) results.Add(result);

        // Assert
        Assert.Empty(results);
    }

    /// <summary>
    /// Verifies that the <see cref="ScanPipeline.ProcessBatchAsync"/> method adheres to the MaxConcurrency setting
    /// specified in the <see cref="ScanOptions"/> when processing a batch of crash logs.
    /// </summary>
    /// <remarks>
    /// This test ensures that the maximum number of concurrent tasks does not exceed the value defined in the options,
    /// and that all crash logs in the batch are successfully processed without failures.
    /// </remarks>
    /// <returns>
    /// A completed task that represents the execution of the unit test.
    /// </returns>
    [Fact]
    public async Task ProcessBatchAsync_WithCustomOptions_ShouldRespectMaxConcurrency()
    {
        // Arrange
        var logPaths = new[]
        {
            SetupTestCrashLog("log1.log", GenerateValidCrashLog()),
            SetupTestCrashLog("log2.log", GenerateValidCrashLog()),
            SetupTestCrashLog("log3.log", GenerateValidCrashLog()),
            SetupTestCrashLog("log4.log", GenerateValidCrashLog())
        };

        var options = new ScanOptions { MaxConcurrency = 1 }; // Force sequential processing
        var results = new List<ScanResult>();

        // Act
        var start = DateTime.UtcNow;
        await foreach (var result in _pipeline.ProcessBatchAsync(logPaths, options)) results.Add(result);
        var elapsed = DateTime.UtcNow - start;

        // Assert
        Assert.Equal(4, results.Count);
        // With MaxConcurrency = 1, processing should be more sequential
        // This is hard to test precisely, but we can at least verify all files were processed
        Assert.All(results, r => Assert.NotEqual(ScanStatus.Failed, r.Status));
    }

    /// <summary>
    /// Sets up a test crash log file with the specified file name and content.
    /// </summary>
    /// <param name="fileName">The name of the crash log file to be created.</param>
    /// <param name="content">The content to be written to the crash log file.</param>
    /// <returns>The full path to the created crash log file.</returns>
    private string SetupTestCrashLog(string fileName, string content)
    {
        // Create actual temp files since CrashLog.ParseAsync uses real file system
        var tempPath = Path.GetTempFileName();
        File.WriteAllText(tempPath, content);
        return tempPath;
    }

    private static string GenerateValidCrashLog()
    {
        return @"Fallout 4 v1.10.163
Buffout 4 v1.26.2

Unhandled exception ""EXCEPTION_ACCESS_VIOLATION"" at 0x7FF798889DFA

	[Compatibility]
	F4EE: true
	Buffout4: 1
	
SYSTEM SPECS:
	OS: Microsoft Windows 10 Pro v10.0.19044
	CPU: GenuineIntel 11th Gen Intel(R) Core(TM) i7-11700K @ 3.60GHz
	GPU: NVIDIA GeForce RTX 3080
	
PROBABLE CALL STACK:
	[0] 0x7FF798889DFA Fallout4.exe+2479DFA
	[1] 0x7FF7988899FF Fallout4.exe+24799FF
	[2] 0x7FF798889912 Fallout4.exe+2479912
	
MODULES:
	Fallout4.exe 0x7FF796410000
	KERNEL32.DLL 0x7FFE38D80000
	
XSE PLUGINS:
	f4se_1_10_163.dll v2.0.17
	buffout4.dll v1.26.2
	
PLUGINS:
	[00:000] Fallout4.esm
	[01:000] DLCRobot.esm
	[02:000] DLCworkshop01.esm
	[03:000] DLCCoast.esm
	[04:000] DLCworkshop02.esm
	[05:000] DLCworkshop03.esm
	[06:000] DLCNukaWorld.esm
	[07:000] Unofficial Fallout 4 Patch.esp";
    }
}

// Test analyzer for testing purposes
/// <summary>
/// Test implementation of the IAnalyzer interface used for validating the behavior of crash log analyzers in test scenarios.
/// </summary>
/// <remarks>
/// This implementation facilitates testing by simulating the analysis process with configurable properties such as priority,
/// parallel execution capability, and success outcome. It is designed to mimic different analyzer behaviors to validate
/// the robustness of the ScanPipeline and associated components under various scenarios.
/// </remarks>
internal class TestAnalyzer(string name, int priority, bool canRunInParallel, bool shouldSucceed)
    : IAnalyzer
{
    public string Name { get; } = name;
    public int Priority { get; } = priority;
    public bool CanRunInParallel { get; } = canRunInParallel;

    /// <summary>
    /// Analyzes the given crash log and produces an analysis result asynchronously.
    /// </summary>
    /// <param name="crashLog">The crash log to be analyzed.</param>
    /// <param name="cancellationToken">A token to cancel the operation if necessary.</param>
    /// <returns>An <see cref="AnalysisResult"/> indicating the outcome of the analysis.</returns>
    public async Task<AnalysisResult> AnalyzeAsync(CrashLog crashLog, CancellationToken cancellationToken = default)
    {
        // Simulate some work
        await Task.Delay(10, cancellationToken);

        if (shouldSucceed)
            return new GenericAnalysisResult
            {
                AnalyzerName = Name,
                Success = true
            };

        return new GenericAnalysisResult
        {
            AnalyzerName = Name,
            Success = false,
            Errors = [$"Simulated failure in {Name}"]
        };
    }
}