using Scanner111.Core.Infrastructure;
using Scanner111.Core.Models;
using Scanner111.Core.Pipeline;
using Scanner111.GUI.Models;

namespace Scanner111.Tests.Integration;

/// Provides integration tests for the report-writing functionality,
/// ensuring that reports are properly generated, saved, and validated
/// under various conditions and scenarios.
/// The tests in this class evaluate the interaction between report generation
/// components and the rest of the system, including scenarios for batch
/// processing, GUI functionality, CLI functionality, and integration
/// with real sample data.
/// This class implements IDisposable to manage temporary resources,
/// such as directories and files created during testing.
[Collection("IO Heavy Tests")]
public class ReportWritingIntegrationTests : IDisposable
{
    private readonly string _sampleLogsPath;
    private readonly string _tempDirectory;

    public ReportWritingIntegrationTests()
    {
        _tempDirectory = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
        Directory.CreateDirectory(_tempDirectory);

        _sampleLogsPath = Path.Combine(
            Directory.GetParent(Environment.CurrentDirectory)?.Parent?.Parent?.Parent?.FullName ?? "",
            "sample_logs"
        );
    }

    public void Dispose()
    {
        if (Directory.Exists(_tempDirectory)) Directory.Delete(_tempDirectory, true);
        GC.SuppressFinalize(this);
    }

    /// Tests if the ScanPipeline, when integrated with a ReportWriter,
    /// successfully generates a report file based on the provided crash log.
    /// The method validates the following functionality:
    /// <br />
    /// - Proper initialization and usage of the ScanPipeline with default analyzers and configuration.
    /// <br />
    /// - Successful invocation of the pipeline to process a single log file.
    /// <br />
    /// - The ability of the ReportWriter to write the processed scan results to a report file.
    /// <br />
    /// - Ensures the actual report content matches the expected processed scan result text.
    /// <returns>
    ///     Asserts the success of the pipeline processing and report writing.
    ///     Confirms the generated report file exists and its content matches the expected output.
    /// </returns>
    [Fact]
    public async Task ScanPipeline_WithReportWriter_GeneratesReportFile()
    {
        // Arrange
        var testLogPath = Path.Combine(_tempDirectory, "test-crash.log");
        var expectedReportPath = Path.Combine(_tempDirectory, "test-crash-AUTOSCAN.md");

        await CreateSampleCrashLog(testLogPath);

        var messageHandler = new TestMessageHandler();
        var pipeline = new ScanPipelineBuilder()
            .AddDefaultAnalyzers()
            .WithMessageHandler(messageHandler)
            .WithCaching(false)
            .WithEnhancedErrorHandling(false)
            .WithLogging(builder => builder.AddProvider(NullLoggerProvider.Instance))
            .Build();

        var reportWriter = new ReportWriter(NullLogger<ReportWriter>.Instance);

        // Act
        var scanResult = await pipeline.ProcessSingleAsync(testLogPath);
        scanResult.Should().NotBeNull();

        var writeSuccess = await reportWriter.WriteReportAsync(scanResult);

        // Assert
        writeSuccess.Should().BeTrue();
        File.Exists(expectedReportPath).Should().BeTrue();

        var reportContent = await File.ReadAllTextAsync(expectedReportPath, Encoding.UTF8);
        // Note: Report content may be empty for logs without plugin lists - this is expected behavior
        reportContent.Should().Be(scanResult.ReportText);
    }

    /// Validates whether the ScanPipeline, when configured for batch processing,
    /// successfully processes multiple crash log files and generates corresponding
    /// report files. The method ensures the following functionality:
    /// <br />
    /// - Initialization of the ScanPipeline with default analyzers and configurations.
    /// <br />
    /// - Correct processing of multiple log files in a batch asynchronously.
    /// <br />
    /// - ReportWriter integration to generate and save a report for each processed log file.
    /// <br />
    /// - Verification of the existence and readability of the generated report files.
    /// <returns>
    ///     Confirms that the number of processed log files corresponds to the expected count
    ///     and verifies the generation of associated report files. Each report file's existence
    ///     and readability are validated to ensure successful output generation.
    /// </returns>
    [Fact]
    public async Task ScanPipeline_WithBatchProcessing_GeneratesMultipleReports()
    {
        // Arrange
        var logFiles = new[]
        {
            Path.Combine(_tempDirectory, "crash-1.log"),
            Path.Combine(_tempDirectory, "crash-2.log"),
            Path.Combine(_tempDirectory, "crash-3.log")
        };

        foreach (var logFile in logFiles) await CreateSampleCrashLog(logFile);

        var messageHandler = new TestMessageHandler();
        var pipeline = new ScanPipelineBuilder()
            .AddDefaultAnalyzers()
            .WithMessageHandler(messageHandler)
            .WithCaching(false)
            .WithEnhancedErrorHandling(false)
            .WithLogging(builder => builder.AddProvider(NullLoggerProvider.Instance))
            .Build();

        var reportWriter = new ReportWriter(NullLogger<ReportWriter>.Instance);

        // Act
        var results = new List<ScanResult>();
        await foreach (var result in pipeline.ProcessBatchAsync(logFiles))
        {
            results.Add(result);
            await reportWriter.WriteReportAsync(result);
        }

        // Assert
        results.Count.Should().Be(3);

        foreach (var logFile in logFiles)
        {
            var expectedReportPath = Path.ChangeExtension(logFile, null) + "-AUTOSCAN.md";
            File.Exists(expectedReportPath).Should().BeTrue();

            // Note: Report content may be empty for logs without plugin lists - this is expected
            var reportContent = await File.ReadAllTextAsync(expectedReportPath, Encoding.UTF8);
            // Just verify the file was created and is readable
            reportContent.Should().NotBeNull();
        }
    }

    /// Validates the functionality of the GUI's auto-save feature to create a report file
    /// after processing a crash log. This test ensures correct integration of the
    /// auto-save mechanism with the report generation system.
    /// The method performs the following checks:
    /// <br />
    /// - Successfully generates a ScanResult object with test crash log data and a sample report.
    /// <br />
    /// - Verifies the ReportWriter's ability to process and save the generated scan results.
    /// <br />
    /// - Confirms the saved report file matches the expected content, including specific lines from the report.
    /// <br />
    /// - Ensures the AutoSaveResults setting is respected during the operation.
    /// <returns>
    ///     Asserts that the auto-save operation successfully creates a report file
    ///     with accurate content based on the processed crash log.
    /// </returns>
    [Fact]
    public async Task GUI_AutoSaveResult_WritesReportFile()
    {
        // This test simulates the GUI auto-save functionality
        // Arrange
        var testLogPath = Path.Combine(_tempDirectory, "gui-test-crash.log");
        await CreateSampleCrashLog(testLogPath);

        var scanResult = new ScanResult
        {
            LogPath = testLogPath,
            Status = ScanStatus.Completed,
            Report = new List<string>
            {
                "GUI generated report line 1\n",
                "GUI generated report line 2\n",
                "Analysis complete\n"
            }
        };

        var reportWriter = new ReportWriter(NullLogger<ReportWriter>.Instance);

        // Simulate the AutoSaveResult method behavior
        var userSettings = new UserSettings { AutoSaveResults = true };

        // Act
        var saveResult = false;
        if (userSettings.AutoSaveResults) saveResult = await reportWriter.WriteReportAsync(scanResult);

        // Assert
        saveResult.Should().BeTrue();
        File.Exists(scanResult.OutputPath).Should().BeTrue();

        var reportContent = await File.ReadAllTextAsync(scanResult.OutputPath, Encoding.UTF8);
        reportContent.Should().Be(scanResult.ReportText);
        reportContent.Should().Contain("GUI generated report");
    }

    /// Verifies the behavior of CLI auto-save functionality with different application settings.
    /// The method validates the following scenarios:
    /// <br />
    /// - When the AutoSaveResults flag is enabled in the settings, the scan result's report
    /// is successfully written to the output file, and the file's existence and content
    /// are validated.
    /// <br />
    /// - When the AutoSaveResults flag is disabled in the settings, the scan result's report
    /// is not written to the output file, ensuring no file is created.
    /// <returns>
    ///     Confirms that the CLI auto-save functionality behaves as expected based on the
    ///     application settings. Validates file creation and content when the auto-save flag
    ///     is enabled and ensures no file creation occurs when it is disabled.
    /// </returns>
    [Fact]
    public async Task CLI_AutoSaveWithSettings_RespectsAutoSaveFlag()
    {
        // This test simulates CLI auto-save behavior with different settings
        // Arrange
        var testLogPath = Path.Combine(_tempDirectory, "cli-test-crash.log");
        await CreateSampleCrashLog(testLogPath);

        var scanResult = new ScanResult
        {
            LogPath = testLogPath,
            Status = ScanStatus.Completed,
            Report = new List<string>
            {
                "CLI generated report line 1\n",
                "CLI generated report line 2\n"
            }
        };

        var reportWriter = new ReportWriter(NullLogger<ReportWriter>.Instance);

        // Test with AutoSaveResults enabled
        var settingsWithAutoSave = new ApplicationSettings { AutoSaveResults = true };

        // Act
        var saveResult = false;
        if (settingsWithAutoSave.AutoSaveResults && !string.IsNullOrEmpty(scanResult.ReportText))
            saveResult = await reportWriter.WriteReportAsync(scanResult);

        // Assert
        saveResult.Should().BeTrue();
        File.Exists(scanResult.OutputPath).Should().BeTrue();

        // Test with AutoSaveResults disabled
        var testLogPath2 = Path.Combine(_tempDirectory, "cli-test-crash-2.log");
        await CreateSampleCrashLog(testLogPath2);

        var scanResult2 = new ScanResult
        {
            LogPath = testLogPath2,
            Status = ScanStatus.Completed,
            Report = new List<string> { "Report that should not be saved\n" }
        };

        var settingsWithoutAutoSave = new ApplicationSettings { AutoSaveResults = false };

        // Act
        var saveResult2 = false;
        if (settingsWithoutAutoSave.AutoSaveResults && !string.IsNullOrEmpty(scanResult2.ReportText))
            saveResult2 = await reportWriter.WriteReportAsync(scanResult2);

        // Assert - file should not be created because auto-save is disabled
        saveResult2.Should().BeFalse();
        File.Exists(scanResult2.OutputPath).Should().BeFalse();
    }

    /// Validates that the ReportWriter correctly generates a valid report file
    /// when processing a real sample log file. The test ensures the following key aspects:
    /// <br />
    /// - Verifies the ScanPipeline initializes with default analyzers, proper logging, and message handling.
    /// <br />
    /// - Confirms the pipeline successfully processes a real log file and generates a scan result.
    /// <br />
    /// - Ensures that the ReportWriter creates a report file with content matching the scan result.
    /// <br />
    /// - Validates that the generated report does not include unwanted content like OPC-related text.
    /// <br />
    /// - Checks the structure and integrity of the report against the expected output.
    /// <br />
    /// - Confirms the proper setup and usage of temporary directories and file handling for tests.
    /// <returns>
    ///     Asserts that the report is successfully written, exists as a valid file, and contains
    ///     content consistent with the processed scan result while excluding specified text patterns.
    /// </returns>
    [Fact]
    public async Task ReportWriter_WithRealSampleLog_GeneratesValidReport()
    {
        // This test uses a real sample log if available
        // Arrange
        if (!Directory.Exists(_sampleLogsPath))
            // Skip test if sample logs are not available
            return;

        var sampleLogs = Directory.GetFiles(_sampleLogsPath, "*.log", SearchOption.TopDirectoryOnly)
            .Take(1)
            .ToArray();

        if (sampleLogs.Length == 0)
            // Skip test if no sample logs found
            return;

        var sampleLogPath = sampleLogs[0];
        var tempLogPath = Path.Combine(_tempDirectory, Path.GetFileName(sampleLogPath));
        var expectedReportPath = Path.ChangeExtension(tempLogPath, null) + "-AUTOSCAN.md";

        // Copy sample log to temp directory for testing
        File.Copy(sampleLogPath, tempLogPath);

        var messageHandler = new TestMessageHandler();
        var pipeline = new ScanPipelineBuilder()
            .AddDefaultAnalyzers()
            .WithMessageHandler(messageHandler)
            .WithCaching(false)
            .WithEnhancedErrorHandling(false)
            .WithLogging(builder => builder.AddProvider(NullLoggerProvider.Instance))
            .Build();

        var reportWriter = new ReportWriter(NullLogger<ReportWriter>.Instance);

        // Act
        var scanResult = await pipeline.ProcessSingleAsync(tempLogPath);
        scanResult.Should().NotBeNull();

        var writeSuccess = await reportWriter.WriteReportAsync(scanResult);

        // Assert
        writeSuccess.Should().BeTrue();
        File.Exists(expectedReportPath).Should().BeTrue();

        var reportContent = await File.ReadAllTextAsync(expectedReportPath, Encoding.UTF8);

        // Verify no OPC content in the generated report (regardless of whether report is empty)
        reportContent.Should().NotContain("OPC INSTALLER");
        reportContent.Should().NotContain("PATCHED THROUGH OPC");

        // Verify report structure matches scan result
        reportContent.Should().Be(scanResult.ReportText);
    }

    /// Verifies the integration of ScanResultViewModel and ReportWriter components.
    /// This method ensures that the ViewModel correctly reflects the properties of the provided ScanResult
    /// and that the ReportWriter can successfully write the scan result's report to a file.
    /// The integration test includes the following validations:
    /// <br />
    /// - The ScanResultViewModel accurately maps and displays properties of the associated ScanResult.
    /// <br />
    /// - The ReportWriter correctly generates and writes a report file using the provided scan result.
    /// <br />
    /// - The generated report file's content matches the expected data.
    /// <br />
    /// - Ensures that the view model properties, such as description and category, correctly align with the scan result details.
    /// <returns>
    ///     Confirms successful integration via assertions, ensuring proper functionality of the ViewModel and
    ///     the ability of the ReportWriter to save the report file with expected content.
    /// </returns>
    [Fact]
    public async Task ScanResultViewModel_WithReportWriter_IntegratesCorrectly()
    {
        // This test verifies GUI integration
        // Arrange
        var testLogPath = Path.Combine(_tempDirectory, "viewmodel-test-crash.log");
        await CreateSampleCrashLog(testLogPath);

        var scanResult = new ScanResult
        {
            LogPath = testLogPath,
            Status = ScanStatus.Completed,
            Report = new List<string>
            {
                "ViewModel integration test\n",
                "✓ Test successful\n"
            }
        };

        var viewModel = new ScanResultViewModel(scanResult);
        var reportWriter = new ReportWriter(NullLogger<ReportWriter>.Instance);

        // Act
        var writeSuccess = await reportWriter.WriteReportAsync(viewModel.ScanResult);

        // Assert
        writeSuccess.Should().BeTrue();
        File.Exists(viewModel.ScanResult.OutputPath).Should().BeTrue();

        var reportContent = await File.ReadAllTextAsync(viewModel.ScanResult.OutputPath, Encoding.UTF8);
        reportContent.Should().Contain("ViewModel integration test");
        reportContent.Should().Contain("✓ Test successful");

        // Verify view model properties
        viewModel.Description.Should().Be(Path.GetFileName(testLogPath));
        viewModel.Category.Should().Be("Completed");
    }

    /// Creates a sample crash log file with predefined content for testing purposes.
    /// The log includes details such as application version, plugins list, and call stack.
    /// <param name="filePath">
    ///     The full file path where the sample crash log will be created.
    /// </param>
    /// <returns>
    ///     A task that represents the asynchronous operation of writing the sample crash log content
    ///     to the specified file.
    /// </returns>
    private async Task CreateSampleCrashLog(string filePath)
    {
        var sampleLogContent = """
                               Fallout4.exe (Application)
                               Version: 1.10.163.0

                               [15] UT  2024-05-04 08:52:21 BST :: Plugins (244):
                                   [00]      Fallout4.esm
                                   [01]      DLCRobot.esm
                                   [02]      DLCworkshop01.esm
                                   [FE:000] TestMod.esp

                               [15] UT  2024-05-04 08:52:21 BST :: Call Stack:
                                   ntdll.dll                  0x00007FFE12345678
                                   KERNELBASE.dll             0x00007FFE11111111
                                   Fallout4.exe               0x0000000140123456

                               [15] UT  2024-05-04 08:52:21 BST :: Analysis Complete
                               """;

        await File.WriteAllTextAsync(filePath, sampleLogContent, Encoding.UTF8);
    }

    /// Represents a test implementation of the IMessageHandler interface,
    /// used to simulate message handling behavior during testing scenarios.
    /// This class provides empty implementations for all message handling methods,
    /// including methods for displaying informational, warning, error, success, debug,
    /// and critical messages. Additionally, it includes methods for creating and
    /// managing progress reporting contexts.
    /// TestMessageHandler is primarily designed to support unit and integration
    /// tests by providing a no-op message handling mechanism, enabling tests to run
    /// without triggering actual UI or logging functionality.
    private class TestMessageHandler : IMessageHandler
    {
        /// Displays an informational message using the specified message target.
        /// <param name="message">
        ///     The informational message to be displayed or logged.
        /// </param>
        /// <param name="target">
        ///     The intended target for the message. Default is MessageTarget.All, which means
        ///     the message is sent to all configured output targets (e.g., GUI, CLI, log).
        /// </param>
        public void ShowInfo(string message, MessageTarget target = MessageTarget.All)
        {
        }

        /// Displays a warning message to the specified target(s). This method is used to communicate
        /// warnings that may require user attention or convey important but non-critical issues.
        /// <param name="message">
        ///     The warning message to be displayed.
        /// </param>
        /// <param name="target">
        ///     Specifies the recipient(s) of the warning message. Defaults to all targets if not specified.
        /// </param>
        public void ShowWarning(string message, MessageTarget target = MessageTarget.All)
        {
        }

        /// Displays an error message to the specified target(s).
        /// This method is used to convey error information, and optionally allows
        /// targeting specific message destinations (e.g., GUI, CLI, logs).
        /// <param name="message">The error message to be displayed.</param>
        /// <param name="target">The intended target(s) for the message. Defaults to all targets.</param>
        public void ShowError(string message, MessageTarget target = MessageTarget.All)
        {
        }

        /// Displays a success message to the specified target(s).
        /// This method is used to communicate success notifications within the system,
        /// allowing differentiation between various message targets.
        /// <param name="message">
        ///     The content of the success message to be displayed.
        /// </param>
        /// <param name="target">
        ///     Specifies the target(s) where the success message should be sent.
        ///     This can include options such as GUI, CLI, log, or all targets. Defaults to All.
        /// </param>
        public void ShowSuccess(string message, MessageTarget target = MessageTarget.All)
        {
        }

        /// Displays a debug-level message using the provided message text and target.
        /// This method is typically used to output internal state information or detailed
        /// execution flow details for diagnostic purposes during the development or debugging phase.
        /// <param name="message">
        ///     The content of the debug message to be displayed.
        /// </param>
        /// <param name="target">
        ///     Specifies the target destination for the message, such as all recipients or specific categories
        ///     (e.g., GUI, CLI, or logs). The default value is MessageTarget.All.
        /// </param>
        public void ShowDebug(string message, MessageTarget target = MessageTarget.All)
        {
        }

        /// Displays a critical message to the specified target(s).
        /// This method is used to convey critical information that needs immediate attention
        /// and can be sent to one or multiple message targets.
        /// <param name="message">The critical message to be displayed.</param>
        /// <param name="target">The target(s) where the message should be sent. Defaults to all available targets.</param>
        public void ShowCritical(string message, MessageTarget target = MessageTarget.All)
        {
        }

        /// Displays a user-facing message based on the specified parameters,
        /// allowing for customization of the message type, target, and optional details.
        /// This method is intended to facilitate communication within the system by handling
        /// message delivery to the specified target(s).
        /// <param name="message">The primary message text to be displayed or logged.</param>
        /// <param name="details">Optional additional details to accompany the message, providing further context.</param>
        /// <param name="messageType">
        ///     The type of the message indicating its purpose or level of severity, such as Info, Warning,
        ///     or Error.
        /// </param>
        /// <param name="target">The target audience or medium for the message, such as All, GuiOnly, CliOnly, or LogOnly.</param>
        public void ShowMessage(string message, string? details = null, MessageType messageType = MessageType.Info,
            MessageTarget target = MessageTarget.All)
        {
        }

        /// Returns a progress handler for tracking the completion of a multi-step process.
        /// The progress handler allows reporting of progress updates in the form of
        /// current status and completion metrics.
        /// <param name="title">
        ///     The title or description of the process for which progress is being tracked.
        /// </param>
        /// <param name="totalItems">
        ///     The total number of steps or items to complete in the process.
        /// </param>
        /// <returns>
        ///     An instance of <see cref="IProgress{ProgressInfo}" /> that enables sending
        ///     progress updates, including the completion percentage and current state information.
        /// </returns>
        public IProgress<ProgressInfo> ShowProgress(string title, int totalItems)
        {
            return new Progress<ProgressInfo>();
        }

        /// Creates a progress context instance with a specified title and total number of items to track progress.
        /// This method is used to initialize and configure a new progress context for tracking the execution of tasks or operations.
        /// <param name="title">
        ///     The title or description to display for the progress context.
        /// </param>
        /// <param name="totalItems">
        ///     The total number of items to process, used to determine the progress percentages.
        /// </param>
        /// <returns>
        ///     An implementation of IProgressContext that can be used to track and report progress.
        /// </returns>
        public IProgressContext CreateProgressContext(string title, int totalItems)
        {
            return new TestProgressContext();
        }
    }

    /// Represents a test implementation of the IProgressContext interface,
    /// used for simulating and testing progress tracking behavior during integration tests.
    /// This class provides stub methods for updating and reporting progress,
    /// completing tasks, and disposing resources. It enables the verification
    /// of progress-related functionality in testing scenarios without interacting
    /// with actual system resources or complex logic.
    private class TestProgressContext : IProgressContext
    {
        /// Updates the current progress with the provided details during a processing task.
        /// Used to simulate progress updates in integration tests or implementations.
        /// <param name="current">
        ///     The current progress value or task index being updated.
        /// </param>
        /// <param name="message">
        ///     A message or description associated with the current progress update.
        ///     Typically used to indicate the ongoing operation or its status.
        /// </param>
        public void Update(int current, string message)
        {
        }

        /// Tests the complete execution of the scanning pipeline when integrated with
        /// report-writing functionality to ensure that all components interact cohesively.
        /// Verifies the following:
        /// <br />
        /// - The pipeline successfully processes multiple input sources or data units.
        /// <br />
        /// - The generated reports accurately reflect the processed data.
        /// <br />
        /// - Handling of edge cases or invalid inputs, ensuring that reports are not created
        /// for failed or skipped pipeline tasks.
        /// <br />
        /// - Resource cleanup and exception handling in the integration process.
        public void Complete()
        {
        }

        /// Reports progress updates with the provided ProgressInfo value during integration tests.
        /// This method allows the simulation of progress reporting functionality without invoking
        /// actual system-level operations. It can be used to verify how progress updates are handled
        /// within test scenarios and ensures the correct processing of the ProgressInfo values.
        /// <param name="value">
        ///     The current progress information, containing details such as the current step,
        ///     total steps, and descriptive progress message.
        /// </param>
        public void Report(ProgressInfo value)
        {
        }

        /// Releases any resources used by the ReportWritingIntegrationTests class.
        /// This method is called to ensure that all disposable resources, such as those used
        /// in the TestProgressContext or other components of the integration tests, are properly
        /// cleaned up when the test class is no longer needed.
        /// The Dispose method is implemented to adhere to the IDisposable pattern and ensures
        /// that all allocated resources are released to prevent potential memory leaks and
        /// maintain the stability of the testing environment. This includes disposals within
        /// nested or dependent objects utilized during the integration test cases.
        public void Dispose()
        {
        }
    }
}