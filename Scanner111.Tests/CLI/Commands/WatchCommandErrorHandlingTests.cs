using FluentAssertions;
using Moq;
using Scanner111.CLI.Commands;
using Scanner111.CLI.Models;
using Scanner111.Core.Analyzers;
using Scanner111.Core.Infrastructure;
using Scanner111.Core.Models;
using Scanner111.Core.Pipeline;

namespace Scanner111.Tests.CLI.Commands;

/// <summary>
///     Tests error handling scenarios for WatchCommand
/// </summary>
public class WatchCommandErrorHandlingTests : IDisposable
{
    private readonly WatchCommand _command;
    private readonly Mock<IReportWriter> _mockReportWriter;
    private readonly Mock<IScanPipeline> _mockScanPipeline;
    private readonly Mock<IServiceProvider> _mockServiceProvider;
    private readonly Mock<IApplicationSettingsService> _mockSettingsService;
    private readonly string _testDirectory;

    public WatchCommandErrorHandlingTests()
    {
        _mockServiceProvider = new Mock<IServiceProvider>();
        _mockSettingsService = new Mock<IApplicationSettingsService>();
        _mockScanPipeline = new Mock<IScanPipeline>();
        _mockReportWriter = new Mock<IReportWriter>();

        _command = new WatchCommand(
            _mockServiceProvider.Object,
            _mockSettingsService.Object,
            _mockScanPipeline.Object,
            _mockReportWriter.Object);

        // Create a temporary test directory
        _testDirectory = Path.Combine(Path.GetTempPath(), $"WatchErrorTests_{Guid.NewGuid()}");
        Directory.CreateDirectory(_testDirectory);
    }

    public void Dispose()
    {
        // Dispose command to clean up FileSystemWatcher
        _command?.Dispose();

        // Clean up test directory
        try
        {
            if (Directory.Exists(_testDirectory))
            {
                // Remove read-only attributes from all files
                var files = Directory.GetFiles(_testDirectory, "*", SearchOption.AllDirectories);
                foreach (var file in files)
                    try
                    {
                        File.SetAttributes(file, FileAttributes.Normal);
                    }
                    catch
                    {
                    }

                Directory.Delete(_testDirectory, true);
            }
        }
        catch
        {
        }
    }

    #region Auto-Move Error Handling Tests

    [Fact(Timeout = 5000)]
    public async Task AutoMove_WithIOException_ContinuesExecution()
    {
        // Arrange - Create a read-only directory to simulate move failure
        var testFile = Path.Combine(_testDirectory, "locked.log");
        File.WriteAllText(testFile, "test content");

        var scanResult = new ScanResult
        {
            LogPath = testFile,
            AnalysisResults = new List<AnalysisResult>() // No issues for auto-move
        };

        _mockScanPipeline.Setup(p => p.ProcessSingleAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(scanResult);
        _mockReportWriter.Setup(r => r.WriteReportAsync(It.IsAny<ScanResult>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        // Create a file with same name in the destination to cause move conflict
        var solvedDir = Path.Combine(_testDirectory, "Solved");
        Directory.CreateDirectory(solvedDir);
        var conflictFile = Path.Combine(solvedDir, "locked.log");
        File.WriteAllText(conflictFile, "existing content");
        File.SetAttributes(conflictFile, FileAttributes.ReadOnly);

        var options = new WatchOptions
        {
            Path = _testDirectory,
            ShowDashboard = false,
            AutoMove = true,
            ScanExisting = true,
            ShowNotifications = false
        };

        // Act
        var cts = new CancellationTokenSource(TimeSpan.FromSeconds(1));
        var result = await _command.ExecuteAsync(options, cts.Token);

        // Assert
        result.Should().Be(0); // Should continue despite move failure
        _mockScanPipeline.Verify(p => p.ProcessSingleAsync(testFile, It.IsAny<CancellationToken>()), Times.Once);

        // Cleanup
        try
        {
            File.SetAttributes(conflictFile, FileAttributes.Normal);
            File.Delete(conflictFile);
            File.Delete(testFile);
            Directory.Delete(solvedDir);
        }
        catch
        {
        }
    }

    #endregion

    #region Dashboard Error Handling Tests

    [Fact(Timeout = 5000)]
    public async Task RunWithDashboard_WithSpectreLiveDisplayException_FallsBackGracefully()
    {
        // Note: This test verifies that the dashboard mode doesn't crash on Live display errors
        // The actual Spectre.Console Live display may throw exceptions in certain environments

        // Arrange
        var options = new WatchOptions
        {
            Path = _testDirectory,
            ShowDashboard = true
        };

        // Act
        var cts = new CancellationTokenSource(TimeSpan.FromMilliseconds(100));
        var result = await _command.ExecuteAsync(options, cts.Token);

        // Assert
        result.Should().Be(0); // Should handle any display errors gracefully
    }

    #endregion

    #region Game Path Resolution Error Tests

    [Fact(Timeout = 5000)]
    public async Task DetermineWatchPath_WithInvalidGameType_UsesCurrentDirectory()
    {
        // Arrange
        var options = new WatchOptions
        {
            Game = "InvalidGame",
            ShowDashboard = false
        };

        // Act - This should fall back to current directory
        var cts = new CancellationTokenSource(TimeSpan.FromMilliseconds(100));
        var result = await _command.ExecuteAsync(options, cts.Token);

        // Assert
        result.Should().Be(0); // Should succeed with current directory fallback
    }

    #endregion

    #region File Pattern Error Tests

    [Fact(Timeout = 5000)]
    public async Task FileWatcher_WithInvalidPattern_HandlesGracefully()
    {
        // Arrange
        var options = new WatchOptions
        {
            Path = _testDirectory,
            Pattern = "[InvalidPattern", // Invalid regex pattern
            ShowDashboard = false
        };

        // Act
        var cts = new CancellationTokenSource(TimeSpan.FromMilliseconds(500));
        var result = await _command.ExecuteAsync(options, cts.Token);

        // Assert
        result.Should().Be(0); // FileSystemWatcher should handle invalid patterns gracefully
    }

    #endregion

    #region Path Validation Error Tests

    [Fact]
    public async Task ExecuteAsync_WithNullPath_ReturnsError()
    {
        // Arrange
        var options = new WatchOptions { Path = null, ShowDashboard = false };

        // Act
        var result = await _command.ExecuteAsync(options, CancellationToken.None);

        // Assert
        result.Should().Be(1);
    }

    [Fact]
    public async Task ExecuteAsync_WithEmptyPath_ReturnsError()
    {
        // Arrange
        var options = new WatchOptions { Path = "", ShowDashboard = false };

        // Act
        var result = await _command.ExecuteAsync(options, CancellationToken.None);

        // Assert
        result.Should().Be(1);
    }

    [Fact]
    public async Task ExecuteAsync_WithNonExistentPath_ReturnsError()
    {
        // Arrange
        var nonExistentPath = Path.Combine(_testDirectory, "does_not_exist");
        var options = new WatchOptions { Path = nonExistentPath, ShowDashboard = false };

        // Act
        var result = await _command.ExecuteAsync(options, CancellationToken.None);

        // Assert
        result.Should().Be(1);
    }

    [Fact]
    public async Task ExecuteAsync_WithFilePath_ReturnsError()
    {
        // Arrange
        var filePath = Path.Combine(_testDirectory, "test.txt");
        File.WriteAllText(filePath, "test content");
        var options = new WatchOptions { Path = filePath, ShowDashboard = false };

        // Act
        var result = await _command.ExecuteAsync(options, CancellationToken.None);

        // Assert
        result.Should().Be(1);

        // Cleanup
        File.Delete(filePath);
    }

    #endregion

    #region Pipeline Error Handling Tests

    [Fact(Timeout = 5000)]
    public async Task ProcessNewFile_WithPipelineException_ContinuesExecution()
    {
        // Arrange
        var testFile = Path.Combine(_testDirectory, "test.log");
        File.WriteAllText(testFile, "test content");

        _mockScanPipeline.Setup(p => p.ProcessSingleAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("Pipeline error"));

        var options = new WatchOptions
        {
            Path = _testDirectory,
            ShowDashboard = false,
            ScanExisting = true,
            ShowNotifications = false
        };

        // Act
        var cts = new CancellationTokenSource(TimeSpan.FromSeconds(1));
        var result = await _command.ExecuteAsync(options, cts.Token);

        // Assert
        result.Should().Be(0); // Should not return error, just continue

        // Verify pipeline was called despite error
        _mockScanPipeline.Verify(p => p.ProcessSingleAsync(testFile, It.IsAny<CancellationToken>()), Times.Once);

        // Cleanup
        File.Delete(testFile);
    }

    [Fact(Timeout = 5000)]
    public async Task ProcessNewFile_WithReportWriterException_ContinuesExecution()
    {
        // Arrange
        var testFile = Path.Combine(_testDirectory, "test.log");
        File.WriteAllText(testFile, "test content");

        var scanResult = new ScanResult
        {
            LogPath = testFile,
            AnalysisResults = new List<AnalysisResult>()
        };

        _mockScanPipeline.Setup(p => p.ProcessSingleAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(scanResult);
        _mockReportWriter.Setup(r => r.WriteReportAsync(It.IsAny<ScanResult>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new IOException("Write error"));

        var options = new WatchOptions
        {
            Path = _testDirectory,
            ShowDashboard = false,
            ScanExisting = true,
            ShowNotifications = false
        };

        // Act
        var cts = new CancellationTokenSource(TimeSpan.FromSeconds(1));
        var result = await _command.ExecuteAsync(options, cts.Token);

        // Assert
        result.Should().Be(0); // Should not return error, just continue

        // Verify both were called despite error
        _mockScanPipeline.Verify(p => p.ProcessSingleAsync(testFile, It.IsAny<CancellationToken>()), Times.Once);
        _mockReportWriter.Verify(r => r.WriteReportAsync(It.IsAny<ScanResult>(), It.IsAny<CancellationToken>()),
            Times.Once);

        // Cleanup
        File.Delete(testFile);
    }

    #endregion

    #region File System Error Handling Tests

    [Fact(Timeout = 5000)]
    public async Task ProcessNewFile_WithFileNotFoundException_HandlesGracefully()
    {
        // Arrange - Create file then delete it to simulate file disappearing
        var testFile = Path.Combine(_testDirectory, "disappearing.log");
        File.WriteAllText(testFile, "test content");

        // Setup pipeline to return a result even for missing file
        _mockScanPipeline.Setup(p => p.ProcessSingleAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new FileNotFoundException("File not found"));

        var options = new WatchOptions
        {
            Path = _testDirectory,
            ShowDashboard = false,
            ScanExisting = true,
            ShowNotifications = false
        };

        // Act
        var cts = new CancellationTokenSource(TimeSpan.FromSeconds(1));
        var result = await _command.ExecuteAsync(options, cts.Token);

        // Assert
        result.Should().Be(0); // Should continue execution
        _mockScanPipeline.Verify(p => p.ProcessSingleAsync(testFile, It.IsAny<CancellationToken>()), Times.Once);

        // Cleanup
        if (File.Exists(testFile)) File.Delete(testFile);
    }

    [Fact(Timeout = 5000)]
    public async Task ProcessNewFile_WithUnauthorizedAccessException_HandlesGracefully()
    {
        // Arrange
        var testFile = Path.Combine(_testDirectory, "restricted.log");
        File.WriteAllText(testFile, "test content");

        _mockScanPipeline.Setup(p => p.ProcessSingleAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new UnauthorizedAccessException("Access denied"));

        var options = new WatchOptions
        {
            Path = _testDirectory,
            ShowDashboard = false,
            ScanExisting = true,
            ShowNotifications = false
        };

        // Act
        var cts = new CancellationTokenSource(TimeSpan.FromSeconds(1));
        var result = await _command.ExecuteAsync(options, cts.Token);

        // Assert
        result.Should().Be(0); // Should continue execution
        _mockScanPipeline.Verify(p => p.ProcessSingleAsync(testFile, It.IsAny<CancellationToken>()), Times.Once);

        // Cleanup
        File.Delete(testFile);
    }

    #endregion

    #region Cancellation Handling Tests

    [Fact(Timeout = 5000)]
    public async Task ExecuteAsync_WithImmediateCancellation_ExitsGracefully()
    {
        // Arrange
        var options = new WatchOptions
        {
            Path = _testDirectory,
            ShowDashboard = false
        };

        var cts = new CancellationTokenSource();
        cts.Cancel(); // Cancel immediately

        // Act
        var result = await _command.ExecuteAsync(options, cts.Token);

        // Assert
        result.Should().Be(0); // Should exit gracefully
    }

    [Fact(Timeout = 10000)]
    public async Task ExecuteAsync_WithCancellationDuringExistingScan_StopsGracefully()
    {
        // Arrange
        var testFile = Path.Combine(_testDirectory, "test.log");
        File.WriteAllText(testFile, "test content");

        // Setup a slow-running pipeline to simulate work in progress
        _mockScanPipeline.Setup(p => p.ProcessSingleAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .Returns(async (string path, CancellationToken ct) =>
            {
                await Task.Delay(5000, ct); // 5 second delay
                return new ScanResult
                {
                    LogPath = path,
                    AnalysisResults = new List<AnalysisResult>()
                };
            });

        var options = new WatchOptions
        {
            Path = _testDirectory,
            ShowDashboard = false,
            ScanExisting = true
        };

        // Act
        var cts = new CancellationTokenSource(TimeSpan.FromMilliseconds(500)); // Cancel after 500ms
        var result = await _command.ExecuteAsync(options, cts.Token);

        // Assert
        result.Should().Be(0); // Should exit gracefully despite cancellation

        // Cleanup
        File.Delete(testFile);
    }

    #endregion
}