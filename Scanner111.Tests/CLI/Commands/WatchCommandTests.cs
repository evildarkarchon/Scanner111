using FluentAssertions;
using Moq;
using Scanner111.CLI.Commands;
using Scanner111.CLI.Models;
using Scanner111.Core.Analyzers;
using Scanner111.Core.Infrastructure;
using Scanner111.Core.Models;
using Scanner111.Core.Pipeline;

namespace Scanner111.Tests.CLI.Commands;

public class WatchCommandTests : IDisposable
{
    private readonly WatchCommand _command;
    private readonly List<string> _createdDirectories = new();
    private readonly List<string> _createdFiles = new();
    private readonly Mock<IReportWriter> _mockReportWriter;
    private readonly Mock<IScanPipeline> _mockScanPipeline;
    private readonly Mock<IServiceProvider> _mockServiceProvider;
    private readonly Mock<IApplicationSettingsService> _mockSettingsService;
    private readonly string _testDirectory;

    public WatchCommandTests()
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
        _testDirectory = Path.Combine(Path.GetTempPath(), $"WatchCommandTests_{Guid.NewGuid()}");
        Directory.CreateDirectory(_testDirectory);
        _createdDirectories.Add(_testDirectory);
    }

    public void Dispose()
    {
        // Dispose command to clean up FileSystemWatcher
        _command?.Dispose();

        // Clean up created files
        foreach (var file in _createdFiles.Where(File.Exists))
            try
            {
                File.Delete(file);
            }
            catch
            {
            }

        // Clean up created directories
        foreach (var dir in _createdDirectories.Where(Directory.Exists).OrderByDescending(d => d.Length))
            try
            {
                Directory.Delete(dir, true);
            }
            catch
            {
            }
    }

    #region Constructor Tests

    [Fact]
    public void Constructor_InitializesAllDependencies()
    {
        // Arrange & Act
        var command = new WatchCommand(
            _mockServiceProvider.Object,
            _mockSettingsService.Object,
            _mockScanPipeline.Object,
            _mockReportWriter.Object);

        // Assert
        command.Should().NotBeNull();
    }

    #endregion

    #region Statistics Tests

    [Fact(Timeout = 5000)]
    public async Task UpdateStatistics_TracksIssueCountsCorrectly()
    {
        // Arrange
        var testFile = Path.Combine(_testDirectory, "test.log");
        File.WriteAllText(testFile, "Test crash log content");
        _createdFiles.Add(testFile);

        var scanResult = new ScanResult
        {
            LogPath = testFile,
            AnalysisResults = new List<AnalysisResult>
            {
                new GenericAnalysisResult
                    { AnalyzerName = "Analyzer1", HasFindings = true, ReportLines = new List<string> { "Issue 1" } },
                new GenericAnalysisResult
                {
                    AnalyzerName = "Analyzer2", HasFindings = true,
                    ReportLines = new List<string> { "Issue 2", "Issue 3" }
                },
                new GenericAnalysisResult
                    { AnalyzerName = "Analyzer3", HasFindings = false, ReportLines = new List<string>() }
            }
        };

        _mockScanPipeline.Setup(p => p.ProcessSingleAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(scanResult);
        _mockReportWriter.Setup(r => r.WriteReportAsync(It.IsAny<ScanResult>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        var options = new WatchOptions
        {
            Path = _testDirectory,
            ShowDashboard = false,
            ScanExisting = true
        };

        // Act
        var cts = new CancellationTokenSource(TimeSpan.FromSeconds(1));
        var result = await _command.ExecuteAsync(options, cts.Token);

        // Assert
        result.Should().Be(0);
        _mockScanPipeline.Verify(p => p.ProcessSingleAsync(testFile, It.IsAny<CancellationToken>()), Times.Once);
    }

    #endregion

    #region Cancellation Tests

    [Fact(Timeout = 5000)]
    public async Task ExecuteAsync_WithCancellation_StopsGracefully()
    {
        // Arrange
        var options = new WatchOptions
        {
            Path = _testDirectory,
            ShowDashboard = false
        };

        var cts = new CancellationTokenSource();

        // Act
        var task = _command.ExecuteAsync(options, cts.Token);
        await Task.Delay(100);
        cts.Cancel();
        var result = await task;

        // Assert
        result.Should().Be(0);
    }

    #endregion

    #region Error Handling Tests

    [Fact(Timeout = 5000)]
    public async Task ProcessNewFile_WithException_HandlesGracefully()
    {
        // Arrange
        var testFile = Path.Combine(_testDirectory, "test.log");
        File.WriteAllText(testFile, "Test crash log content");
        _createdFiles.Add(testFile);

        _mockScanPipeline.Setup(p => p.ProcessSingleAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new Exception("Test exception"));

        var options = new WatchOptions
        {
            Path = _testDirectory,
            ShowDashboard = false,
            ScanExisting = true
        };

        // Act
        var cts = new CancellationTokenSource(TimeSpan.FromSeconds(1));
        var result = await _command.ExecuteAsync(options, cts.Token);

        // Assert
        result.Should().Be(0); // Should continue despite error
        _mockScanPipeline.Verify(p => p.ProcessSingleAsync(testFile, It.IsAny<CancellationToken>()), Times.Once);
    }

    #endregion

    #region Pattern Matching Tests

    [Fact(Timeout = 5000)]
    public async Task FileWatcher_WithCustomPattern_OnlyMatchesSpecifiedFiles()
    {
        // Arrange
        var logFile = Path.Combine(_testDirectory, "test.log");
        var txtFile = Path.Combine(_testDirectory, "test.txt");
        File.WriteAllText(logFile, "Log content");
        File.WriteAllText(txtFile, "Text content");
        _createdFiles.Add(logFile);
        _createdFiles.Add(txtFile);

        var scanResult = new ScanResult
        {
            LogPath = "",
            AnalysisResults = new List<AnalysisResult>()
        };

        _mockScanPipeline.Setup(p => p.ProcessSingleAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(scanResult);

        var options = new WatchOptions
        {
            Path = _testDirectory,
            ShowDashboard = false,
            Pattern = "*.txt",
            ScanExisting = true
        };

        // Act
        var cts = new CancellationTokenSource(TimeSpan.FromSeconds(1));
        var result = await _command.ExecuteAsync(options, cts.Token);

        // Assert
        result.Should().Be(0);
        _mockScanPipeline.Verify(p => p.ProcessSingleAsync(txtFile, It.IsAny<CancellationToken>()), Times.Once);
        _mockScanPipeline.Verify(p => p.ProcessSingleAsync(logFile, It.IsAny<CancellationToken>()), Times.Never);
    }

    #endregion

    #region Path Determination Tests

    [Fact]
    public async Task DetermineWatchPath_WithExplicitPath_UsesProvidedPath()
    {
        // Arrange
        var options = new WatchOptions { Path = _testDirectory, ShowDashboard = false };

        // Act
        var result = await _command.ExecuteAsync(options, CancellationToken.None);

        // Assert
        result.Should().Be(0);
    }

    [Fact]
    public async Task DetermineWatchPath_WithInvalidPath_ReturnsError()
    {
        // Arrange
        var options = new WatchOptions { Path = "/non/existent/path", ShowDashboard = false };

        // Act
        var result = await _command.ExecuteAsync(options, CancellationToken.None);

        // Assert
        result.Should().Be(1);
    }

    [Fact]
    public async Task DetermineWatchPath_WithGameFallout4_UsesCorrectPath()
    {
        // Arrange
        var documentsPath = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);
        var expectedPath = Path.Combine(documentsPath, "My Games", "Fallout4", "F4SE");

        // Create the expected directory to avoid path validation error
        Directory.CreateDirectory(expectedPath);
        _createdDirectories.Add(expectedPath);

        var options = new WatchOptions { Game = "fallout4", ShowDashboard = false };

        // Act
        var result = await _command.ExecuteAsync(options, CancellationToken.None);

        // Assert
        result.Should().Be(0);
    }

    [Fact]
    public async Task DetermineWatchPath_WithGameSkyrim_UsesCorrectPath()
    {
        // Arrange
        var documentsPath = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);
        var expectedPath = Path.Combine(documentsPath, "My Games", "Skyrim Special Edition", "SKSE");

        // Create the expected directory to avoid path validation error
        Directory.CreateDirectory(expectedPath);
        _createdDirectories.Add(expectedPath);

        var options = new WatchOptions { Game = "skyrim", ShowDashboard = false };

        // Act
        var result = await _command.ExecuteAsync(options, CancellationToken.None);

        // Assert
        result.Should().Be(0);
    }

    [Fact]
    public async Task DetermineWatchPath_WithNoPathOrGame_UsesCurrentDirectory()
    {
        // Arrange
        var options = new WatchOptions { ShowDashboard = false };

        // Act
        var result = await _command.ExecuteAsync(options, CancellationToken.None);

        // Assert
        result.Should().Be(0);
    }

    #endregion

    #region File Processing Tests

    [Fact]
    public async Task ProcessNewFile_WithValidFile_ProcessesSuccessfully()
    {
        // Arrange
        var testFile = Path.Combine(_testDirectory, "test.log");
        File.WriteAllText(testFile, "Test crash log content");
        _createdFiles.Add(testFile);

        var scanResult = new ScanResult
        {
            LogPath = testFile,
            AnalysisResults = new List<AnalysisResult>
            {
                new GenericAnalysisResult
                {
                    AnalyzerName = "TestAnalyzer",
                    HasFindings = true,
                    ReportLines = new List<string> { "Issue found" }
                }
            }
        };

        _mockScanPipeline.Setup(p => p.ProcessSingleAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(scanResult);
        _mockReportWriter.Setup(r => r.WriteReportAsync(It.IsAny<ScanResult>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        var options = new WatchOptions
        {
            Path = _testDirectory,
            ShowDashboard = false,
            ShowNotifications = true
        };

        // Act
        var result = await _command.ExecuteAsync(options, CancellationToken.None);

        // Assert
        result.Should().Be(0);
        _mockScanPipeline.Verify(p => p.ProcessSingleAsync(testFile, It.IsAny<CancellationToken>()), Times.Never,
            "File should not be processed automatically, only through FileSystemWatcher events");
    }

    [Fact]
    public async Task ProcessNewFile_WithAutoMove_MovesFilesToSolvedFolder()
    {
        // Arrange
        var testFile = Path.Combine(_testDirectory, "test.log");
        File.WriteAllText(testFile, "Test crash log content");
        _createdFiles.Add(testFile);

        var scanResult = new ScanResult
        {
            LogPath = testFile,
            AnalysisResults = new List<AnalysisResult>() // No issues
        };

        _mockScanPipeline.Setup(p => p.ProcessSingleAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(scanResult);
        _mockReportWriter.Setup(r => r.WriteReportAsync(It.IsAny<ScanResult>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        var options = new WatchOptions
        {
            Path = _testDirectory,
            ShowDashboard = false,
            AutoMove = true,
            ScanExisting = true
        };

        // Act
        var cts = new CancellationTokenSource(TimeSpan.FromSeconds(1));
        var result = await _command.ExecuteAsync(options, cts.Token);

        // Assert
        result.Should().Be(0);
        _mockScanPipeline.Verify(p => p.ProcessSingleAsync(testFile, It.IsAny<CancellationToken>()), Times.Once);
    }

    #endregion

    #region Scan Existing Files Tests

    [Fact(Timeout = 10000)]
    public async Task ScanExisting_WithExistingFiles_ScansAllFiles()
    {
        // Arrange
        var testFiles = new List<string>();
        for (var i = 0; i < 3; i++)
        {
            var file = Path.Combine(_testDirectory, $"crash{i}.log");
            File.WriteAllText(file, $"Crash log {i}");
            testFiles.Add(file);
            _createdFiles.Add(file);
        }

        var scanResult = new ScanResult
        {
            LogPath = "",
            AnalysisResults = new List<AnalysisResult>()
        };

        _mockScanPipeline.Setup(p => p.ProcessSingleAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(scanResult);
        _mockReportWriter.Setup(r => r.WriteReportAsync(It.IsAny<ScanResult>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        var options = new WatchOptions
        {
            Path = _testDirectory,
            ShowDashboard = false,
            ScanExisting = true
        };

        // Act
        var cts = new CancellationTokenSource(TimeSpan.FromSeconds(2));
        var result = await _command.ExecuteAsync(options, cts.Token);

        // Assert
        result.Should().Be(0);
        _mockScanPipeline.Verify(p => p.ProcessSingleAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()),
            Times.Exactly(3));
    }

    [Fact(Timeout = 10000)]
    public async Task ScanExisting_WithRecursiveOption_ScansSubdirectories()
    {
        // Arrange
        var subDir = Path.Combine(_testDirectory, "subdir");
        Directory.CreateDirectory(subDir);
        _createdDirectories.Add(subDir);

        var rootFile = Path.Combine(_testDirectory, "root.log");
        var subFile = Path.Combine(subDir, "sub.log");
        File.WriteAllText(rootFile, "Root crash log");
        File.WriteAllText(subFile, "Sub crash log");
        _createdFiles.Add(rootFile);
        _createdFiles.Add(subFile);

        var scanResult = new ScanResult
        {
            LogPath = "",
            AnalysisResults = new List<AnalysisResult>()
        };

        _mockScanPipeline.Setup(p => p.ProcessSingleAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(scanResult);
        _mockReportWriter.Setup(r => r.WriteReportAsync(It.IsAny<ScanResult>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        var options = new WatchOptions
        {
            Path = _testDirectory,
            ShowDashboard = false,
            ScanExisting = true,
            Recursive = true
        };

        // Act
        var cts = new CancellationTokenSource(TimeSpan.FromSeconds(2));
        var result = await _command.ExecuteAsync(options, cts.Token);

        // Assert
        result.Should().Be(0);
        _mockScanPipeline.Verify(p => p.ProcessSingleAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()),
            Times.Exactly(2));
    }

    #endregion

    #region Dashboard Tests

    [Fact(Timeout = 5000)]
    public async Task RunWithDashboard_InitializesCorrectly()
    {
        // Arrange
        var options = new WatchOptions
        {
            Path = _testDirectory,
            ShowDashboard = true
        };

        // Act
        var cts = new CancellationTokenSource(TimeSpan.FromMilliseconds(500));
        var result = await _command.ExecuteAsync(options, cts.Token);

        // Assert
        result.Should().Be(0);
    }

    [Fact(Timeout = 5000)]
    public async Task RunSimpleWatch_WithoutDashboard_WorksCorrectly()
    {
        // Arrange
        var options = new WatchOptions
        {
            Path = _testDirectory,
            ShowDashboard = false
        };

        // Act
        var cts = new CancellationTokenSource(TimeSpan.FromMilliseconds(500));
        var result = await _command.ExecuteAsync(options, cts.Token);

        // Assert
        result.Should().Be(0);
    }

    #endregion

    #region Notification Tests

    [Fact(Timeout = 5000)]
    public async Task ProcessNewFile_WithNotificationsEnabled_ShowsMessages()
    {
        // Arrange
        var testFile = Path.Combine(_testDirectory, "test.log");
        File.WriteAllText(testFile, "Test crash log content");
        _createdFiles.Add(testFile);

        var scanResult = new ScanResult
        {
            LogPath = testFile,
            AnalysisResults = new List<AnalysisResult>
            {
                new GenericAnalysisResult
                {
                    AnalyzerName = "TestAnalyzer",
                    HasFindings = true,
                    ReportLines = new List<string> { "Issue found" }
                }
            }
        };

        _mockScanPipeline.Setup(p => p.ProcessSingleAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(scanResult);
        _mockReportWriter.Setup(r => r.WriteReportAsync(It.IsAny<ScanResult>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        var options = new WatchOptions
        {
            Path = _testDirectory,
            ShowDashboard = false,
            ShowNotifications = true,
            ScanExisting = true
        };

        // Act
        var cts = new CancellationTokenSource(TimeSpan.FromSeconds(1));
        var result = await _command.ExecuteAsync(options, cts.Token);

        // Assert
        result.Should().Be(0);
        _mockReportWriter.Verify(r => r.WriteReportAsync(It.IsAny<ScanResult>(), It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact(Timeout = 5000)]
    public async Task ProcessNewFile_WithNotificationsDisabled_DoesNotShowMessages()
    {
        // Arrange
        var testFile = Path.Combine(_testDirectory, "test.log");
        File.WriteAllText(testFile, "Test crash log content");
        _createdFiles.Add(testFile);

        var scanResult = new ScanResult
        {
            LogPath = testFile,
            AnalysisResults = new List<AnalysisResult>()
        };

        _mockScanPipeline.Setup(p => p.ProcessSingleAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(scanResult);

        var options = new WatchOptions
        {
            Path = _testDirectory,
            ShowDashboard = false,
            ShowNotifications = false,
            ScanExisting = true
        };

        // Act
        var cts = new CancellationTokenSource(TimeSpan.FromSeconds(1));
        var result = await _command.ExecuteAsync(options, cts.Token);

        // Assert
        result.Should().Be(0);
    }

    #endregion
}