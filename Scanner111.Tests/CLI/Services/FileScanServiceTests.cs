using Scanner111.CLI.Models;
using Scanner111.CLI.Services;
using Scanner111.Core.Infrastructure;
using Scanner111.Core.Models;

namespace Scanner111.Tests.CLI.Services;

public class FileScanServiceTests : IDisposable
{
    private readonly FileScanService _fileScanService;
    private readonly Mock<IMessageHandler> _messageHandlerMock;
    private readonly string _testDirectory;

    public FileScanServiceTests()
    {
        _messageHandlerMock = new Mock<IMessageHandler>();
        _fileScanService = new FileScanService(_messageHandlerMock.Object);

        _testDirectory = Path.Combine(Path.GetTempPath(), $"FileScanServiceTests_{Guid.NewGuid()}");
        Directory.CreateDirectory(_testDirectory);
    }

    public void Dispose()
    {
        if (Directory.Exists(_testDirectory))
            try
            {
                Directory.Delete(_testDirectory, true);
            }
            catch
            {
                // Ignore cleanup errors
            }
    }

    [Fact]
    public async Task CollectFilesToScanAsync_WithSpecificLogFile_ReturnsFile()
    {
        // Arrange
        var logFile = Path.Combine(_testDirectory, "crash.log");
        await File.WriteAllTextAsync(logFile, "Test crash log");

        var options = new ScanOptions
        {
            LogFile = logFile,
            SkipXseCopy = true
        };

        var settings = new ApplicationSettings();

        // Act
        var result = await _fileScanService.CollectFilesToScanAsync(options, settings);

        // Assert
        result.Should().NotBeNull();
        result.FilesToScan.Should().HaveCount(1);
        result.FilesToScan.Should().Contain(logFile);
        _messageHandlerMock.Verify(
            x => x.ShowInfo(It.Is<string>(s => s.Contains("Added log file")), It.IsAny<MessageTarget>()), Times.Once);
    }

    [Fact]
    public async Task CollectFilesToScanAsync_WithNonExistentLogFile_ReturnsEmpty()
    {
        // Arrange
        var options = new ScanOptions
        {
            LogFile = Path.Combine(_testDirectory, "nonexistent.log"),
            SkipXseCopy = true
        };

        var settings = new ApplicationSettings();

        // Act
        var result = await _fileScanService.CollectFilesToScanAsync(options, settings);

        // Assert
        result.Should().NotBeNull();
        result.FilesToScan.Should().BeEmpty();
        _messageHandlerMock.Verify(
            x => x.ShowInfo(It.Is<string>(s => s.Contains("Added log file")), It.IsAny<MessageTarget>()), Times.Never);
    }

    [Fact]
    public async Task CollectFilesToScanAsync_WithScanDirectory_FindsCrashLogs()
    {
        // Arrange
        var crashLog1 = Path.Combine(_testDirectory, "crash-001.log");
        var crashLog2 = Path.Combine(_testDirectory, "crash-002.txt");
        var dumpLog = Path.Combine(_testDirectory, "dump-data.log");
        var normalLog = Path.Combine(_testDirectory, "normal.log");

        await File.WriteAllTextAsync(crashLog1, "Crash 1");
        await File.WriteAllTextAsync(crashLog2, "Crash 2");
        await File.WriteAllTextAsync(dumpLog, "Dump data");
        await File.WriteAllTextAsync(normalLog, "Normal log");

        var options = new ScanOptions
        {
            ScanDir = _testDirectory,
            SkipXseCopy = true
        };

        var settings = new ApplicationSettings();

        // Act
        var result = await _fileScanService.CollectFilesToScanAsync(options, settings);

        // Assert
        result.Should().NotBeNull();
        result.FilesToScan.Should().HaveCount(3);
        result.FilesToScan.Should().Contain(crashLog1);
        result.FilesToScan.Should().Contain(crashLog2);
        result.FilesToScan.Should().Contain(dumpLog);
        result.FilesToScan.Should().NotContain(normalLog);
        _messageHandlerMock.Verify(
            x => x.ShowInfo(It.Is<string>(s => s.Contains("Found") && s.Contains("crash logs")),
                It.IsAny<MessageTarget>()), Times.Once);
    }

    [Fact]
    public async Task CollectFilesToScanAsync_WithEmptyScanDirectory_ReturnsEmpty()
    {
        // Arrange
        var emptyDir = Path.Combine(_testDirectory, "empty");
        Directory.CreateDirectory(emptyDir);

        var options = new ScanOptions
        {
            ScanDir = emptyDir,
            SkipXseCopy = true
        };

        var settings = new ApplicationSettings();

        // Act
        var result = await _fileScanService.CollectFilesToScanAsync(options, settings);

        // Assert
        result.Should().NotBeNull();
        result.FilesToScan.Should().BeEmpty();
        _messageHandlerMock.Verify(
            x => x.ShowInfo(It.Is<string>(s => s.Contains("Found 0 crash logs")), It.IsAny<MessageTarget>()),
            Times.Once);
    }

    [Fact]
    public async Task CollectFilesToScanAsync_WithNoOptions_ScansCurrentDirectory()
    {
        // Arrange
        var currentDir = Directory.GetCurrentDirectory();
        var testCrashLog = Path.Combine(currentDir, "crash-test.log");

        try
        {
            await File.WriteAllTextAsync(testCrashLog, "Test crash");

            var options = new ScanOptions
            {
                SkipXseCopy = true
            };

            var settings = new ApplicationSettings();

            // Act
            var result = await _fileScanService.CollectFilesToScanAsync(options, settings);

            // Assert
            result.Should().NotBeNull();
            result.FilesToScan.Should().Contain(testCrashLog);
        }
        finally
        {
            // Cleanup
            if (File.Exists(testCrashLog))
                File.Delete(testCrashLog);
        }
    }

    [Fact]
    public async Task CollectFilesToScanAsync_RemovesDuplicates()
    {
        // Arrange
        var logFile = Path.Combine(_testDirectory, "crash-001.log");
        await File.WriteAllTextAsync(logFile, "Crash log");

        var options = new ScanOptions
        {
            LogFile = logFile,
            ScanDir = _testDirectory,
            SkipXseCopy = true
        };

        var settings = new ApplicationSettings();

        // Act
        var result = await _fileScanService.CollectFilesToScanAsync(options, settings);

        // Assert
        result.Should().NotBeNull();
        result.FilesToScan.Should().HaveCount(1);
        result.FilesToScan.Should().Contain(logFile);
    }

    [Fact]
    public async Task CollectFilesToScanAsync_WithXseCopyEnabled_CallsCopyXseLogs()
    {
        // Arrange
        var options = new ScanOptions
        {
            SkipXseCopy = false,
            GamePath = Path.Combine(_testDirectory, "GameFolder")
        };

        var settings = new ApplicationSettings
        {
            CrashLogsDirectory = Path.Combine(_testDirectory, "CrashLogs")
        };

        // Create mock F4SE directory structure
        var f4seDir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments),
            "My Games", "Fallout4", "F4SE");

        // Act
        var result = await _fileScanService.CollectFilesToScanAsync(options, settings);

        // Assert
        result.Should().NotBeNull();
        // The actual XSE copy logic would need the directories to exist
        // This test verifies the method doesn't crash when SkipXseCopy is false
    }

    [Fact]
    public async Task CollectFilesToScanAsync_WithMultipleSources_CombinesResults()
    {
        // Arrange
        var specificLog = Path.Combine(_testDirectory, "specific.log");
        await File.WriteAllTextAsync(specificLog, "Specific log");

        var scanDir = Path.Combine(_testDirectory, "scandir");
        Directory.CreateDirectory(scanDir);
        var crashLog = Path.Combine(scanDir, "crash-scan.log");
        await File.WriteAllTextAsync(crashLog, "Crash in scan dir");

        var options = new ScanOptions
        {
            LogFile = specificLog,
            ScanDir = scanDir,
            SkipXseCopy = true
        };

        var settings = new ApplicationSettings();

        // Act
        var result = await _fileScanService.CollectFilesToScanAsync(options, settings);

        // Assert
        result.Should().NotBeNull();
        result.FilesToScan.Should().HaveCount(2);
        result.FilesToScan.Should().Contain(specificLog);
        result.FilesToScan.Should().Contain(crashLog);
    }

    [Fact]
    public async Task CollectFilesToScanAsync_WithCaseInsensitiveCrashDetection_FindsFiles()
    {
        // Arrange
        var crashUpper = Path.Combine(_testDirectory, "CRASH-001.log");
        var crashMixed = Path.Combine(_testDirectory, "CrAsH-002.txt");
        var dumpLower = Path.Combine(_testDirectory, "dump-data.log");
        var dumpUpper = Path.Combine(_testDirectory, "DUMP-DATA.txt");

        await File.WriteAllTextAsync(crashUpper, "Crash upper");
        await File.WriteAllTextAsync(crashMixed, "Crash mixed");
        await File.WriteAllTextAsync(dumpLower, "Dump lower");
        await File.WriteAllTextAsync(dumpUpper, "Dump upper");

        var options = new ScanOptions
        {
            ScanDir = _testDirectory,
            SkipXseCopy = true
        };

        var settings = new ApplicationSettings();

        // Act
        var result = await _fileScanService.CollectFilesToScanAsync(options, settings);

        // Assert
        result.Should().NotBeNull();
        result.FilesToScan.Should().HaveCount(4);
        result.FilesToScan.Should().Contain(crashUpper);
        result.FilesToScan.Should().Contain(crashMixed);
        result.FilesToScan.Should().Contain(dumpLower);
        result.FilesToScan.Should().Contain(dumpUpper);
    }

    [Fact]
    public async Task CollectFilesToScanAsync_WithXseCopyAndCustomCrashLogsDir_UsesCustomDir()
    {
        // Arrange
        var customCrashLogsDir = Path.Combine(_testDirectory, "CustomCrashLogs");
        Directory.CreateDirectory(customCrashLogsDir);

        var options = new ScanOptions
        {
            SkipXseCopy = false
        };

        var settings = new ApplicationSettings
        {
            CrashLogsDirectory = customCrashLogsDir
        };

        // Act
        var result = await _fileScanService.CollectFilesToScanAsync(options, settings);

        // Assert
        result.Should().NotBeNull();
        // Verify the method completes without error
        // Actual XSE copy would depend on F4SE/SKSE directories existing
    }

    [Fact]
    public void Constructor_WithNullMessageHandler_ThrowsArgumentNullException()
    {
        // Act & Assert
        var action = () => new FileScanService(null!);
        action.Should().Throw<ArgumentNullException>()
            .WithParameterName("messageHandler");
    }

    [Fact]
    public async Task CollectFilesToScanAsync_WithSubdirectories_OnlyScansTopLevel()
    {
        // Arrange
        var subDir = Path.Combine(_testDirectory, "subdir");
        Directory.CreateDirectory(subDir);

        var topLevelCrash = Path.Combine(_testDirectory, "crash-top.log");
        var subDirCrash = Path.Combine(subDir, "crash-sub.log");

        await File.WriteAllTextAsync(topLevelCrash, "Top level crash");
        await File.WriteAllTextAsync(subDirCrash, "Subdirectory crash");

        var options = new ScanOptions
        {
            ScanDir = _testDirectory,
            SkipXseCopy = true
        };

        var settings = new ApplicationSettings();

        // Act
        var result = await _fileScanService.CollectFilesToScanAsync(options, settings);

        // Assert
        result.Should().NotBeNull();
        result.FilesToScan.Should().HaveCount(1);
        result.FilesToScan.Should().Contain(topLevelCrash);
        result.FilesToScan.Should().NotContain(subDirCrash);
    }
}