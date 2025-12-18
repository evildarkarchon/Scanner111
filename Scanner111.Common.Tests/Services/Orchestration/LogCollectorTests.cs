using FluentAssertions;
using Scanner111.Common.Models.Orchestration;
using Scanner111.Common.Services.Orchestration;

namespace Scanner111.Common.Tests.Services.Orchestration;

/// <summary>
/// Tests for LogCollector.
/// </summary>
public class LogCollectorTests : IDisposable
{
    private readonly LogCollector _collector;
    private readonly string _testDirectory;
    private readonly string _backupDirectory;

    public LogCollectorTests()
    {
        _collector = new LogCollector();
        _testDirectory = Path.Combine(Path.GetTempPath(), $"LogCollectorTests_{Guid.NewGuid():N}");
        _backupDirectory = Path.Combine(_testDirectory, "Backup");
        Directory.CreateDirectory(_testDirectory);

        _collector.Configuration = new LogCollectorConfiguration
        {
            BackupBasePath = _backupDirectory,
            UnsolvedLogsSubdirectory = "Unsolved Logs",
            ReportFileSuffix = "-AUTOSCAN.md",
            CreateDirectoryIfNotExists = true,
            OverwriteExisting = false
        };
    }

    public void Dispose()
    {
        // Clean up test directory
        if (Directory.Exists(_testDirectory))
        {
            try
            {
                Directory.Delete(_testDirectory, recursive: true);
            }
            catch
            {
                // Ignore cleanup errors
            }
        }
    }

    [Fact]
    public async Task MoveUnsolvedLogAsync_WithNullPath_ReturnsEmpty()
    {
        // Act
        var result = await _collector.MoveUnsolvedLogAsync(null!);

        // Assert
        result.Should().Be(LogCollectionResult.Empty);
    }

    [Fact]
    public async Task MoveUnsolvedLogAsync_WithEmptyPath_ReturnsEmpty()
    {
        // Act
        var result = await _collector.MoveUnsolvedLogAsync(string.Empty);

        // Assert
        result.Should().Be(LogCollectionResult.Empty);
    }

    [Fact]
    public async Task MoveUnsolvedLogAsync_WithNoBackupPath_ReturnsError()
    {
        // Arrange
        _collector.Configuration = LogCollectorConfiguration.Empty;
        var crashLogPath = Path.Combine(_testDirectory, "crash.log");

        // Act
        var result = await _collector.MoveUnsolvedLogAsync(crashLogPath);

        // Assert
        result.HasErrors.Should().BeTrue();
        result.Errors[0].ErrorMessage.Should().Contain("Backup path not configured");
    }

    [Fact]
    public async Task MoveUnsolvedLogAsync_WithNonExistentFile_ReturnsEmptyResult()
    {
        // Arrange
        var crashLogPath = Path.Combine(_testDirectory, "nonexistent.log");

        // Act
        var result = await _collector.MoveUnsolvedLogAsync(crashLogPath);

        // Assert
        result.HasMovedFiles.Should().BeFalse();
        result.HasErrors.Should().BeFalse();
    }

    [Fact]
    public async Task MoveUnsolvedLogAsync_WithValidFile_MovesFile()
    {
        // Arrange
        var crashLogPath = Path.Combine(_testDirectory, "crash-2024-01-01.log");
        await File.WriteAllTextAsync(crashLogPath, "Crash log content");

        // Act
        var result = await _collector.MoveUnsolvedLogAsync(crashLogPath);

        // Assert
        result.HasMovedFiles.Should().BeTrue();
        result.MovedCrashLogs.Should().Contain("crash-2024-01-01.log");
        File.Exists(crashLogPath).Should().BeFalse();

        var expectedBackupPath = Path.Combine(_backupDirectory, "Unsolved Logs", "crash-2024-01-01.log");
        File.Exists(expectedBackupPath).Should().BeTrue();
    }

    [Fact]
    public async Task MoveUnsolvedLogAsync_WithAssociatedReport_MovesBothFiles()
    {
        // Arrange
        var crashLogPath = Path.Combine(_testDirectory, "crash-2024-01-01.log");
        var reportPath = Path.Combine(_testDirectory, "crash-2024-01-01-AUTOSCAN.md");
        await File.WriteAllTextAsync(crashLogPath, "Crash log content");
        await File.WriteAllTextAsync(reportPath, "Report content");

        // Act
        var result = await _collector.MoveUnsolvedLogAsync(crashLogPath);

        // Assert
        result.MovedCrashLogs.Should().HaveCount(1);
        result.MovedReports.Should().HaveCount(1);
        result.TotalMoved.Should().Be(2);

        File.Exists(crashLogPath).Should().BeFalse();
        File.Exists(reportPath).Should().BeFalse();

        var backupDir = Path.Combine(_backupDirectory, "Unsolved Logs");
        File.Exists(Path.Combine(backupDir, "crash-2024-01-01.log")).Should().BeTrue();
        File.Exists(Path.Combine(backupDir, "crash-2024-01-01-AUTOSCAN.md")).Should().BeTrue();
    }

    [Fact]
    public async Task MoveUnsolvedLogAsync_WithExistingDestination_ReturnsError()
    {
        // Arrange
        var crashLogPath = Path.Combine(_testDirectory, "crash-existing.log");
        await File.WriteAllTextAsync(crashLogPath, "Crash log content");

        var backupDir = Path.Combine(_backupDirectory, "Unsolved Logs");
        Directory.CreateDirectory(backupDir);
        var existingBackup = Path.Combine(backupDir, "crash-existing.log");
        await File.WriteAllTextAsync(existingBackup, "Existing backup");

        // Act
        var result = await _collector.MoveUnsolvedLogAsync(crashLogPath);

        // Assert
        result.HasErrors.Should().BeTrue();
        result.Errors[0].ErrorMessage.Should().Contain("already exists");
    }

    [Fact]
    public async Task MoveUnsolvedLogAsync_WithOverwriteEnabled_OverwritesExisting()
    {
        // Arrange
        _collector.Configuration = _collector.Configuration with { OverwriteExisting = true };

        var crashLogPath = Path.Combine(_testDirectory, "crash-overwrite.log");
        await File.WriteAllTextAsync(crashLogPath, "New content");

        var backupDir = Path.Combine(_backupDirectory, "Unsolved Logs");
        Directory.CreateDirectory(backupDir);
        var existingBackup = Path.Combine(backupDir, "crash-overwrite.log");
        await File.WriteAllTextAsync(existingBackup, "Old content");

        // Act
        var result = await _collector.MoveUnsolvedLogAsync(crashLogPath);

        // Assert
        result.HasErrors.Should().BeFalse();
        result.MovedCrashLogs.Should().Contain("crash-overwrite.log");

        var backupContent = await File.ReadAllTextAsync(existingBackup);
        backupContent.Should().Be("New content");
    }

    [Fact]
    public async Task MoveUnsolvedLogAsync_CreatesBackupDirectory()
    {
        // Arrange
        var crashLogPath = Path.Combine(_testDirectory, "crash-newdir.log");
        await File.WriteAllTextAsync(crashLogPath, "Content");

        var expectedBackupDir = Path.Combine(_backupDirectory, "Unsolved Logs");
        Directory.Exists(expectedBackupDir).Should().BeFalse();

        // Act
        var result = await _collector.MoveUnsolvedLogAsync(crashLogPath);

        // Assert
        result.HasMovedFiles.Should().BeTrue();
        Directory.Exists(expectedBackupDir).Should().BeTrue();
    }

    [Fact]
    public async Task MoveUnsolvedLogsAsync_WithMultipleFiles_MovesAll()
    {
        // Arrange
        var logPaths = new List<string>();
        for (int i = 0; i < 3; i++)
        {
            var path = Path.Combine(_testDirectory, $"crash-{i}.log");
            await File.WriteAllTextAsync(path, $"Content {i}");
            logPaths.Add(path);
        }

        // Act
        var result = await _collector.MoveUnsolvedLogsAsync(logPaths);

        // Assert
        result.MovedCrashLogs.Should().HaveCount(3);
        result.HasErrors.Should().BeFalse();
    }

    [Fact]
    public async Task MoveUnsolvedLogsAsync_WithEmptyList_ReturnsEmpty()
    {
        // Act
        var result = await _collector.MoveUnsolvedLogsAsync(Array.Empty<string>());

        // Assert
        result.HasMovedFiles.Should().BeFalse();
        result.HasErrors.Should().BeFalse();
    }

    [Fact]
    public void GetBackupPath_ReturnsCorrectPath()
    {
        // Arrange
        var crashLogPath = Path.Combine(_testDirectory, "crash-2024.log");

        // Act
        var backupPath = _collector.GetBackupPath(crashLogPath);

        // Assert
        var expected = Path.Combine(_backupDirectory, "Unsolved Logs", "crash-2024.log");
        backupPath.Should().Be(expected);
    }

    [Fact]
    public void GetReportPath_ReturnsCorrectPath()
    {
        // Arrange
        var crashLogPath = Path.Combine(_testDirectory, "crash-2024.log");

        // Act
        var reportPath = _collector.GetReportPath(crashLogPath);

        // Assert
        var expected = Path.Combine(_testDirectory, "crash-2024-AUTOSCAN.md");
        reportPath.Should().Be(expected);
    }

    [Fact]
    public void GetReportPath_WithCustomSuffix_UsesConfiguredSuffix()
    {
        // Arrange
        _collector.Configuration = _collector.Configuration with { ReportFileSuffix = "-REPORT.txt" };
        var crashLogPath = Path.Combine(_testDirectory, "crash.log");

        // Act
        var reportPath = _collector.GetReportPath(crashLogPath);

        // Assert
        reportPath.Should().EndWith("crash-REPORT.txt");
    }

    [Fact]
    public void LogCollectionResult_Empty_HasCorrectDefaults()
    {
        // Assert
        LogCollectionResult.Empty.HasMovedFiles.Should().BeFalse();
        LogCollectionResult.Empty.HasErrors.Should().BeFalse();
        LogCollectionResult.Empty.TotalMoved.Should().Be(0);
        LogCollectionResult.Empty.MovedCrashLogs.Should().BeEmpty();
        LogCollectionResult.Empty.MovedReports.Should().BeEmpty();
        LogCollectionResult.Empty.Errors.Should().BeEmpty();
    }

    [Fact]
    public void LogCollectorConfiguration_Empty_HasCorrectDefaults()
    {
        // Assert
        LogCollectorConfiguration.Empty.BackupBasePath.Should().BeEmpty();
        LogCollectorConfiguration.Empty.UnsolvedLogsSubdirectory.Should().Be("Unsolved Logs");
        LogCollectorConfiguration.Empty.ReportFileSuffix.Should().Be("-AUTOSCAN.md");
        LogCollectorConfiguration.Empty.CreateDirectoryIfNotExists.Should().BeTrue();
        LogCollectorConfiguration.Empty.OverwriteExisting.Should().BeFalse();
    }
}
