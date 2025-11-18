using FluentAssertions;
using Scanner111.Common.Models.Reporting;
using Scanner111.Common.Services.FileIO;
using Scanner111.Common.Services.Reporting;

namespace Scanner111.Common.Tests.Services.Reporting;

/// <summary>
/// Tests for ReportWriter.
/// </summary>
public class ReportWriterTests
{
    private readonly ReportWriter _writer;
    private readonly FileIOService _fileIO;

    public ReportWriterTests()
    {
        _fileIO = new FileIOService();
        _writer = new ReportWriter(_fileIO);
    }

    [Fact]
    public async Task WriteReportAsync_CreatesFileWithCorrectPath()
    {
        // Arrange
        var tempDir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
        Directory.CreateDirectory(tempDir);
        var logPath = Path.Combine(tempDir, "crash-12624.log");
        var expectedReportPath = Path.Combine(tempDir, "crash-12624-AUTOSCAN.md");

        var report = ReportFragment.FromLines("# Test Report", "Content here");

        try
        {
            // Act
            await _writer.WriteReportAsync(logPath, report);

            // Assert
            File.Exists(expectedReportPath).Should().BeTrue();
            var content = await File.ReadAllTextAsync(expectedReportPath);
            content.Should().Contain("# Test Report");
            content.Should().Contain("Content here");
        }
        finally
        {
            if (Directory.Exists(tempDir))
            {
                Directory.Delete(tempDir, true);
            }
        }
    }

    [Fact]
    public async Task WriteReportAsync_OverwritesExistingFile()
    {
        // Arrange
        var tempDir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
        Directory.CreateDirectory(tempDir);
        var logPath = Path.Combine(tempDir, "crash-test.log");
        var reportPath = Path.Combine(tempDir, "crash-test-AUTOSCAN.md");

        // Create existing file
        await File.WriteAllTextAsync(reportPath, "Old content");

        var report = ReportFragment.FromLines("New content");

        try
        {
            // Act
            await _writer.WriteReportAsync(logPath, report);

            // Assert
            var content = await File.ReadAllTextAsync(reportPath);
            content.Should().Be("New content");
            content.Should().NotContain("Old content");
        }
        finally
        {
            if (Directory.Exists(tempDir))
            {
                Directory.Delete(tempDir, true);
            }
        }
    }

    [Fact]
    public async Task WriteReportAsync_WithNullCrashLogPath_ThrowsArgumentException()
    {
        // Arrange
        var report = ReportFragment.FromLines("Test");

        // Act
        var act = () => _writer.WriteReportAsync(null!, report);

        // Assert
        await act.Should().ThrowAsync<ArgumentException>()
            .WithParameterName("crashLogPath");
    }

    [Fact]
    public async Task WriteReportAsync_WithEmptyCrashLogPath_ThrowsArgumentException()
    {
        // Arrange
        var report = ReportFragment.FromLines("Test");

        // Act
        var act = () => _writer.WriteReportAsync(string.Empty, report);

        // Assert
        await act.Should().ThrowAsync<ArgumentException>()
            .WithParameterName("crashLogPath");
    }

    [Fact]
    public async Task WriteReportAsync_WithNullReport_ThrowsArgumentNullException()
    {
        // Act
        var act = () => _writer.WriteReportAsync("test.log", null!);

        // Assert
        await act.Should().ThrowAsync<ArgumentNullException>()
            .WithParameterName("report");
    }

    [Theory]
    [InlineData("crash-12624.log", "crash-12624-AUTOSCAN.md")]
    [InlineData("crash-test-file.log", "crash-test-file-AUTOSCAN.md")]
    [InlineData("C:\\Logs\\crash-0DB9300.log", "C:\\Logs\\crash-0DB9300-AUTOSCAN.md")]
    public void GetReportPath_WithVariousInputs_ReturnsCorrectPath(string logPath, string expectedPath)
    {
        // Act
        var result = ReportWriter.GetReportPath(logPath);

        // Assert
        result.Should().Be(expectedPath);
    }

    [Fact]
    public void GetReportPath_WithNullPath_ThrowsArgumentException()
    {
        // Act
        var act = () => ReportWriter.GetReportPath(null!);

        // Assert
        act.Should().Throw<ArgumentException>()
            .WithParameterName("crashLogPath");
    }

    [Fact]
    public void GetReportPath_WithEmptyPath_ThrowsArgumentException()
    {
        // Act
        var act = () => ReportWriter.GetReportPath(string.Empty);

        // Assert
        act.Should().Throw<ArgumentException>()
            .WithParameterName("crashLogPath");
    }

    [Fact]
    public async Task ReportExistsAsync_WithExistingReport_ReturnsTrue()
    {
        // Arrange
        var tempDir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
        Directory.CreateDirectory(tempDir);
        var logPath = Path.Combine(tempDir, "crash-exists.log");
        var reportPath = Path.Combine(tempDir, "crash-exists-AUTOSCAN.md");

        await File.WriteAllTextAsync(reportPath, "Existing report");

        try
        {
            // Act
            var exists = await _writer.ReportExistsAsync(logPath);

            // Assert
            exists.Should().BeTrue();
        }
        finally
        {
            if (Directory.Exists(tempDir))
            {
                Directory.Delete(tempDir, true);
            }
        }
    }

    [Fact]
    public async Task ReportExistsAsync_WithNonExistentReport_ReturnsFalse()
    {
        // Arrange
        var tempDir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
        Directory.CreateDirectory(tempDir);
        var logPath = Path.Combine(tempDir, "crash-nonexistent.log");

        try
        {
            // Act
            var exists = await _writer.ReportExistsAsync(logPath);

            // Assert
            exists.Should().BeFalse();
        }
        finally
        {
            if (Directory.Exists(tempDir))
            {
                Directory.Delete(tempDir, true);
            }
        }
    }

    [Fact]
    public void Constructor_WithNullFileIO_ThrowsArgumentNullException()
    {
        // Act
        var act = () => new ReportWriter(null!);

        // Assert
        act.Should().Throw<ArgumentNullException>()
            .WithParameterName("fileIO");
    }

    [Fact]
    public async Task WriteReportAsync_WithMultipleLines_JoinsWithNewlines()
    {
        // Arrange
        var tempDir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
        Directory.CreateDirectory(tempDir);
        var logPath = Path.Combine(tempDir, "crash-multiline.log");
        var reportPath = Path.Combine(tempDir, "crash-multiline-AUTOSCAN.md");

        var report = ReportFragment.FromLines("Line 1", "Line 2", "Line 3");

        try
        {
            // Act
            await _writer.WriteReportAsync(logPath, report);

            // Assert
            var content = await File.ReadAllTextAsync(reportPath);
            content.Should().Be("Line 1\nLine 2\nLine 3");
        }
        finally
        {
            if (Directory.Exists(tempDir))
            {
                Directory.Delete(tempDir, true);
            }
        }
    }
}
