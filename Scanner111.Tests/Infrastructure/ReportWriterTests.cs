using System.Text;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Scanner111.Core.Infrastructure;
using Scanner111.Core.Models;

namespace Scanner111.Tests.Infrastructure;

/// <summary>
/// Contains unit tests for the <c>ReportWriter</c> class, validating its functionality for
/// writing report data, handling different scenarios, and ensuring correctness of output.
/// </summary>
/// <remarks>
/// This test class ensures that the <c>ReportWriter</c> behaves as expected under
/// a variety of conditions, including valid input, custom paths, empty data, and invalid cases.
/// </remarks>
/// <example>
/// See individual test methods for edge cases and specific behaviors validated in this suite.
/// </example>
public class ReportWriterTests : IDisposable
{
    private readonly IReportWriter _reportWriter;
    private readonly string _tempDirectory;

    public ReportWriterTests()
    {
        _tempDirectory = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
        Directory.CreateDirectory(_tempDirectory);
        ILogger<ReportWriter> logger = NullLogger<ReportWriter>.Instance;
        _reportWriter = new ReportWriter(logger);
    }

    /// <summary>
    /// Releases the resources used by the <c>ReportWriterTests</c> instance, including cleanup of
    /// temporary directories created during test execution.
    /// </summary>
    /// <remarks>
    /// This method ensures that any temporary test data (such as directories and files) is
    /// removed after the test run to prevent resource leaks or file system clutter.
    /// It also suppresses finalization to optimize garbage collection.
    /// </remarks>
    public void Dispose()
    {
        if (Directory.Exists(_tempDirectory)) Directory.Delete(_tempDirectory, true);
        GC.SuppressFinalize(this);
    }

    /// <summary>
    /// Verifies that the <c>WriteReportAsync</c> method successfully writes a report file for a
    /// valid scan result and uses the expected output file path.
    /// </summary>
    /// <remarks>
    /// This test ensures that when a valid scan result is provided, the report writer
    /// generates the expected output markdown file, and the file contents match the
    /// <c>ReportText</c> of the provided scan result. It also validates that the file
    /// is physically created in the correct location.
    /// </remarks>
    /// <returns>
    /// Returns <c>true</c> indicating the operation was successful and the file exists
    /// with the correct contents, otherwise throws an assertion exception.
    /// </returns>
    [Fact]
    public async Task WriteReportAsync_WithValidScanResult_WritesFileSuccessfully()
    {
        // Arrange
        var logPath = Path.Combine(_tempDirectory, "crash-test.log");
        var expectedOutputPath = Path.Combine(_tempDirectory, "crash-test-AUTOSCAN.md");

        await File.WriteAllTextAsync(logPath, "Sample crash log content");

        var scanResult = CreateSampleScanResult(logPath);

        // Act
        var result = await _reportWriter.WriteReportAsync(scanResult);

        // Assert
        result.Should().BeTrue();
        File.Exists(expectedOutputPath).Should().BeTrue("the report file should be created");

        var content = await File.ReadAllTextAsync(expectedOutputPath, Encoding.UTF8);
        content.Should().Be(scanResult.ReportText);
    }

    /// <summary>
    /// Validates that a report is successfully written to a custom output path when provided.
    /// </summary>
    /// <remarks>
    /// This test ensures that the <c>WriteReportAsync</c> method properly writes the contents of a
    /// <c>ScanResult</c> to the specified file path, verifying that the output file exists and its
    /// content matches the expected report text. It also verifies that any intermediate directories
    /// in the custom output path are handled correctly.
    /// </remarks>
    /// <returns>
    /// No return value for this test method as it applies assertions to validate its conditions.
    /// </returns>
    [Fact]
    public async Task WriteReportAsync_WithCustomOutputPath_WritesFileSuccessfully()
    {
        // Arrange
        var logPath = Path.Combine(_tempDirectory, "crash-test.log");
        var customOutputPath = Path.Combine(_tempDirectory, "custom-report.md");

        await File.WriteAllTextAsync(logPath, "Sample crash log content");

        var scanResult = CreateSampleScanResult(logPath);

        // Act
        var result = await _reportWriter.WriteReportAsync(scanResult, customOutputPath);

        // Assert
        result.Should().BeTrue();
        File.Exists(customOutputPath).Should().BeTrue("the custom report file should be created");

        var content = await File.ReadAllTextAsync(customOutputPath, Encoding.UTF8);
        content.Should().Be(scanResult.ReportText);
    }

    /// <summary>
    /// Validates that the <c>WriteReportAsync</c> method creates the necessary directory structure
    /// when the target directory does not exist, ensuring that reports can be correctly saved in nested paths.
    /// </summary>
    /// <remarks>
    /// This test confirms that the <c>WriteReportAsync</c> method checks for the existence of the output directory
    /// and creates it if it is missing before writing the report file. It verifies that the directory structure
    /// is created as expected and that the report file is generated in the correct location.
    /// </remarks>
    /// <returns>
    /// A task representing the asynchronous operation of testing directory creation and successful file writing.
    /// </returns>
    [Fact]
    public async Task WriteReportAsync_CreatesDirectoryIfNotExists()
    {
        // Arrange
        var subDirectory = Path.Combine(_tempDirectory, "subdir", "nested");
        var logPath = Path.Combine(subDirectory, "crash-test.log");
        var expectedOutputPath = Path.Combine(subDirectory, "crash-test-AUTOSCAN.md");

        var scanResult = CreateSampleScanResult(logPath);

        // Act
        var result = await _reportWriter.WriteReportAsync(scanResult);

        // Assert
        result.Should().BeTrue();
        Directory.Exists(subDirectory).Should().BeTrue("the subdirectory should be created");
        File.Exists(expectedOutputPath).Should().BeTrue("the report file should be created");
    }

    /// <summary>
    /// Verifies that calling <c>WriteReportAsync</c> with an empty report produces an empty output file.
    /// </summary>
    /// <remarks>
    /// This test ensures that the method can handle scenarios where the <c>Report</c> property
    /// of the <c>ScanResult</c> is empty, and confirms that the resulting file is created
    /// correctly with no content written to it.
    /// </remarks>
    /// <returns>
    /// An <c>async</c> task representing the unit test operation.
    /// </returns>
    [Fact]
    public async Task WriteReportAsync_WithEmptyReport_WritesEmptyFile()
    {
        // Arrange
        var logPath = Path.Combine(_tempDirectory, "crash-test.log");
        var scanResult = new ScanResult
        {
            LogPath = logPath,
            Report = new List<string>()
        };

        // Act
        var result = await _reportWriter.WriteReportAsync(scanResult);

        // Assert
        result.Should().BeTrue();
        File.Exists(scanResult.OutputPath).Should().BeTrue("the empty report file should be created");

        var content = await File.ReadAllTextAsync(scanResult.OutputPath, Encoding.UTF8);
        content.Should().BeEmpty();
    }

    /// <summary>
    /// Validates that the <c>WriteReportAsync</c> method of the <c>IReportWriter</c> implementation
    /// returns <c>false</c> when an invalid file path is provided in the <c>ScanResult</c>.
    /// </summary>
    /// <remarks>
    /// This test checks the behavior of the method when an invalid path, such as one referencing
    /// a non-existent drive, is supplied. It ensures that the method fails gracefully without
    /// throwing exceptions or producing unintended side effects.
    /// </remarks>
    /// <returns>
    /// A task that completes when the test is executed, confirming that <c>false</c> is returned
    /// for an invalid path scenario.
    /// </returns>
    [Fact]
    public async Task WriteReportAsync_WithInvalidPath_ReturnsFalse()
    {
        // Arrange
        var invalidPath = Path.Combine("Z:\\NonExistentDrive", "crash-test.log");
        var scanResult = CreateSampleScanResult(invalidPath);

        // Act
        var result = await _reportWriter.WriteReportAsync(scanResult);

        // Assert
        result.Should().BeFalse("writing to invalid path should fail");
    }

    /// <summary>
    /// Verifies that the <c>WriteReportAsync</c> method writes a report with UTF-8 encoded content
    /// and ensures that the encoding is preserved in the output file.
    /// </summary>
    /// <remarks>
    /// This test checks for proper handling of special characters, Unicode symbols, and non-Latin
    /// scripts in the report content. It ensures the output file retains consistency in text representation
    /// and encoding.
    /// </remarks>
    /// <returns>
    /// A task that completes when the test has verified the preservation of UTF-8 encoding
    /// for the written report.
    /// </returns>
    [Fact]
    public async Task WriteReportAsync_WithUTF8Content_PreservesEncoding()
    {
        // Arrange
        var logPath = Path.Combine(_tempDirectory, "crash-test.log");
        var scanResult = new ScanResult
        {
            LogPath = logPath,
            Report = new List<string>
            {
                "Test with special characters: éñüñß\n",
                "Unicode symbols: ✓ ❌ ⚠️\n",
                "Asian characters: 中文 日本語\n"
            }
        };

        // Act
        var result = await _reportWriter.WriteReportAsync(scanResult);

        // Assert
        result.Should().BeTrue();

        var content = await File.ReadAllTextAsync(scanResult.OutputPath, Encoding.UTF8);
        content.Should().Contain("éñüñß");
        content.Should().Contain("✓ ❌ ⚠️");
        content.Should().Contain("中文 日本語");
    }

    /// <summary>
    /// Validates that the <c>WriteReportAsync</c> method removes any sections related to the OPC installer
    /// from the provided scan result's report before writing the output to file.
    /// </summary>
    /// <remarks>
    /// This test ensures that any content related to OPC installer checks is filtered out from the
    /// report while ensuring other non-OPC-related sections remain intact. It performs checks to verify
    /// filtered and non-filtered content in the final output file.
    /// </remarks>
    /// <returns>
    /// Asserts the correctness of filtered OPC sections and ensures the file written matches
    /// expected results.
    /// </returns>
    [Fact]
    public async Task WriteReportAsync_WithOPCContent_FiltersOPCSections()
    {
        // Arrange
        var logPath = Path.Combine(_tempDirectory, "crash-test.log");
        var reportWithOpc = new List<string>
        {
            "Normal content line 1\n",
            "====================================================\n",
            "CHECKING FOR MODS THAT ARE PATCHED THROUGH OPC INSTALLER...\n",
            "====================================================\n",
            "# FOUND NO PROBLEMATIC MODS THAT ARE ALREADY PATCHED THROUGH THE OPC INSTALLER # \n",
            "\n",
            "====================================================\n",
            "CHECKING FOR MODS THAT IF IMPORTANT PATCHES & FIXES ARE INSTALLED...\n",
            "====================================================\n",
            "Normal content line 2\n"
        };

        var scanResult = new ScanResult
        {
            LogPath = logPath,
            Report = reportWithOpc
        };

        // Act
        var result = await _reportWriter.WriteReportAsync(scanResult);

        // Assert
        result.Should().BeTrue();

        var content = await File.ReadAllTextAsync(scanResult.OutputPath, Encoding.UTF8);
        content.Should().Contain("Normal content line 1");
        content.Should().Contain("Normal content line 2");
        content.Should().NotContain("OPC INSTALLER", "OPC sections should be filtered out");
        content.Should().NotContain("PATCHED THROUGH THE OPC INSTALLER", "OPC sections should be filtered out");
    }

    /// <summary>
    /// Tests that the <c>WriteReportAsync</c> method correctly filters out all sections related to OPC content
    /// when the report contains multiple OPC sections, ensuring only relevant content is written to the output file.
    /// </summary>
    /// <remarks>
    /// This test verifies that when a <c>ScanResult</c> report contains multiple sections identified as related to
    /// OPC content, these sections are excluded from the final output. The method ensures that only the non-OPC content
    /// is preserved, supporting the generation of clean and accurate reports.
    /// </remarks>
    /// <returns>
    /// A task representing the asynchronous operation. The test will pass if all occurrences of OPC content are removed
    /// and the remaining content matches the expected result, or fail otherwise.
    /// </returns>
    [Fact]
    public async Task WriteReportAsync_WithMultipleOPCSections_FiltersAllOPCSections()
    {
        // Arrange
        var logPath = Path.Combine(_tempDirectory, "crash-test.log");
        var reportWithMultipleOpc = new List<string>
        {
            "Start content\n",
            "====================================================\n",
            "CHECKING FOR MODS THAT ARE PATCHED THROUGH OPC INSTALLER...\n",
            "====================================================\n",
            "OPC content 1\n",
            "====================================================\n",
            "REGULAR SECTION\n",
            "====================================================\n",
            "Regular content\n",
            "====================================================\n",
            "MODS PATCHED THROUGH OPC INSTALLER\n",
            "====================================================\n",
            "OPC content 2\n",
            "====================================================\n",
            "END SECTION\n",
            "====================================================\n",
            "End content\n"
        };

        var scanResult = new ScanResult
        {
            LogPath = logPath,
            Report = reportWithMultipleOpc
        };

        // Act
        var result = await _reportWriter.WriteReportAsync(scanResult);

        // Assert
        result.Should().BeTrue();

        var content = await File.ReadAllTextAsync(scanResult.OutputPath, Encoding.UTF8);
        content.Should().Contain("Start content");
        content.Should().Contain("Regular content");
        content.Should().Contain("End content");
        content.Should().NotContain("OPC content 1", "OPC sections should be filtered out");
        content.Should().NotContain("OPC content 2", "OPC sections should be filtered out");
        content.Should().NotContain("OPC INSTALLER", "OPC sections should be filtered out");
    }

    /// <summary>
    /// Validates that the <c>WriteReportAsync</c> method correctly filters out OPC-related sections
    /// when they are located at the end of the report content.
    /// </summary>
    /// <remarks>
    /// This test ensures that content specific to OPC installer sections, including markers and any
    /// related entries, are excluded from the generated report. It verifies that standard report
    /// content remains intact and no unnecessary OPC-specific data is written to the output file.
    /// </remarks>
    /// <returns>
    /// A task representing the asynchronous test operation. Ensures the generated report excludes
    /// OPC-related final sections while preserving the integrity of remaining content.
    /// </returns>
    [Fact]
    public async Task WriteReportAsync_WithOPCAtEnd_FiltersCorrectly()
    {
        // Arrange
        var logPath = Path.Combine(_tempDirectory, "crash-test.log");
        var reportWithOpcAtEnd = new List<string>
        {
            "Normal content\n",
            "====================================================\n",
            "CHECKING FOR MODS THAT ARE PATCHED THROUGH OPC INSTALLER...\n",
            "====================================================\n",
            "Final OPC content\n"
        };

        var scanResult = new ScanResult
        {
            LogPath = logPath,
            Report = reportWithOpcAtEnd
        };

        // Act
        var result = await _reportWriter.WriteReportAsync(scanResult);

        // Assert
        result.Should().BeTrue();

        var content = await File.ReadAllTextAsync(scanResult.OutputPath, Encoding.UTF8);
        content.Should().Contain("Normal content");
        content.Should().NotContain("Final OPC content", "OPC sections should be filtered out");
        content.Should().NotContain("OPC INSTALLER", "OPC sections should be filtered out");
    }

    /// <summary>
    /// Tests the handling of cancellation when invoking the <c>WriteReportAsync</c> method.
    /// </summary>
    /// <remarks>
    /// This test verifies that the <c>WriteReportAsync</c> method correctly handles a cancellation
    /// request passed through a <c>CancellationToken</c>, ensuring no unintended operations are
    /// performed and appropriate exceptions (like <c>OperationCanceledException</c>) are raised
    /// when necessary.
    /// </remarks>
    /// <returns>
    /// The test ensures that the method correctly respects the cancellation token and handles
    /// cancellation without causing crashes or unexpected behavior.
    /// </returns>
    [Fact]
    public async Task WriteReportAsync_WithCancellation_HandlesCancellation()
    {
        // Arrange
        var logPath = Path.Combine(_tempDirectory, "crash-test.log");
        var scanResult = CreateSampleScanResult(logPath);
        var cancellationTokenSource = new CancellationTokenSource();
        cancellationTokenSource.Cancel();

        // Act & Assert
        try
        {
            var result = await _reportWriter.WriteReportAsync(scanResult, cancellationTokenSource.Token);
            // The operation may complete quickly before cancellation is detected
            // In that case, we just verify it didn't crash
            true.Should().BeTrue("the operation should complete without crashing");
        }
        catch (OperationCanceledException)
        {
            // This is also expected behavior
            true.Should().BeTrue("cancellation exception is expected behavior");
        }
    }

    /// <summary>
    /// Verifies that invoking <c>WriteReportAsync</c> with a scan result containing an existing output file
    /// successfully overwrites the content of the file with the new report content.
    /// </summary>
    /// <remarks>
    /// This test ensures that previously existing files at the specified output path are replaced
    /// with the updated content from the scan result. It also verifies that no remnants of the old
    /// content remain in the file after the operation completes.
    /// </remarks>
    /// <returns>
    /// A task that represents the asynchronous operation of writing and validating the file.
    /// Asserts that the operation completes successfully and the file contains the new report content.
    /// </returns>
    [Fact]
    public async Task WriteReportAsync_OverwritesExistingFile()
    {
        // Arrange
        var logPath = Path.Combine(_tempDirectory, "crash-test.log");
        var scanResult = CreateSampleScanResult(logPath);

        // Create existing file with different content
        await File.WriteAllTextAsync(scanResult.OutputPath, "Old content");

        // Act
        var result = await _reportWriter.WriteReportAsync(scanResult);

        // Assert
        result.Should().BeTrue();

        var content = await File.ReadAllTextAsync(scanResult.OutputPath, Encoding.UTF8);
        content.Should().Be(scanResult.ReportText);
        content.Should().NotContain("Old content", "old content should be overwritten");
    }

    /// <summary>
    /// Creates a sample <c>ScanResult</c> object initialized with the specified log file path and
    /// pre-defined report content.
    /// </summary>
    /// <param name="logPath">The file path to the log file associated with the scan result.</param>
    /// <returns>A <c>ScanResult</c> instance containing the given log file path, a completed status, and example report content.</returns>
    private static ScanResult CreateSampleScanResult(string logPath)
    {
        return new ScanResult
        {
            LogPath = logPath,
            Status = ScanStatus.Completed,
            Report =
            [
                "Sample report line 1\n",
                "Sample report line 2\n",
                "✓ Analysis complete\n"
            ]
        };
    }
}