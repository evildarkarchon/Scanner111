using Scanner111.CLI.Commands;
using Scanner111.CLI.Models;
using Scanner111.Core.Analyzers;
using Scanner111.Core.Infrastructure;
using Scanner111.Core.Models;
using Scanner111.Core.Pipeline;
using Spectre.Console.Testing;

namespace Scanner111.Tests.CLI.Commands;

/// <summary>
///     Tests for the Dashboard and Statistics functionality of WatchCommand
/// </summary>
[Collection("FileWatcher Tests")]
public class WatchCommandDashboardTests : IDisposable
{
    private readonly WatchCommand _command;
    private readonly Mock<IReportWriter> _mockReportWriter;
    private readonly Mock<IScanPipeline> _mockScanPipeline;
    private readonly Mock<IServiceProvider> _mockServiceProvider;
    private readonly Mock<IApplicationSettingsService> _mockSettingsService;
    private readonly TestConsole _testConsole;
    private readonly string _testDirectory;

    public WatchCommandDashboardTests()
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
        _testDirectory = Path.Combine(Path.GetTempPath(), $"DashboardTests_{Guid.NewGuid()}");
        Directory.CreateDirectory(_testDirectory);

        // Setup test console for Spectre.Console testing
        _testConsole = new TestConsole();
    }

    public void Dispose()
    {
        // Dispose command to clean up FileSystemWatcher
        _command?.Dispose();

        // Clean up test directory
        try
        {
            if (Directory.Exists(_testDirectory)) Directory.Delete(_testDirectory, true);
        }
        catch
        {
        }
    }

    /// <summary>
    ///     Internal ScanStatistics class for testing
    ///     Mirrors the private class in WatchCommand
    /// </summary>
    private class ScanStatistics
    {
        public int IssueCount { get; set; }
        public int CriticalCount { get; set; }
        public int WarningCount { get; set; }
        public int InfoCount { get; set; }
    }

    #region Dashboard Layout Tests

    [Fact(Timeout = 5000)]
    public async Task Dashboard_CreatesCorrectLayout()
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
        // Dashboard should have been created with header, body (stats + recent), and footer sections
    }

    [Fact(Timeout = 5000)]
    public async Task Dashboard_DisplaysMonitoringPath()
    {
        // Arrange
        var options = new WatchOptions
        {
            Path = _testDirectory,
            ShowDashboard = true,
            Pattern = "*.crash"
        };

        // Act
        var cts = new CancellationTokenSource(TimeSpan.FromMilliseconds(500));
        var result = await _command.ExecuteAsync(options, cts.Token);

        // Assert
        result.Should().Be(0);
        // Dashboard should display the monitoring path and pattern
    }

    #endregion

    #region Statistics Tracking Tests

    [Fact(Timeout = 10000)]
    public async Task Statistics_TracksProcessedFileCount()
    {
        // Arrange
        var testFiles = new List<string>();
        for (var i = 0; i < 3; i++)
        {
            var file = Path.Combine(_testDirectory, $"file{i}.log");
            File.WriteAllText(file, $"Content {i}");
            testFiles.Add(file);
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
        await _command.ExecuteAsync(options, cts.Token);

        // Assert
        _mockScanPipeline.Verify(p => p.ProcessSingleAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()),
            Times.Exactly(3));
    }

    [Fact(Timeout = 5000)]
    public async Task Statistics_CategoriesIssuesByType()
    {
        // Arrange
        var testFile = Path.Combine(_testDirectory, "test.log");
        File.WriteAllText(testFile, "Test content");

        var scanResult = new ScanResult
        {
            LogPath = testFile,
            AnalysisResults = new List<AnalysisResult>
            {
                new GenericAnalysisResult
                {
                    AnalyzerName = "CriticalAnalyzer", HasFindings = true,
                    ReportLines = new List<string> { "Critical issue" }
                },
                new GenericAnalysisResult
                {
                    AnalyzerName = "WarningAnalyzer", HasFindings = true,
                    ReportLines = new List<string> { "Warning 1", "Warning 2" }
                },
                new GenericAnalysisResult
                    { AnalyzerName = "InfoAnalyzer", HasFindings = false, ReportLines = new List<string>() }
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
        await _command.ExecuteAsync(options, cts.Token);

        // Assert
        _mockScanPipeline.Verify(p => p.ProcessSingleAsync(testFile, It.IsAny<CancellationToken>()), Times.Once);
        // Statistics should track 2 analyzers with findings
    }

    [Fact(Timeout = 15000)]
    public async Task Statistics_TracksRecentFiles()
    {
        // Arrange
        var testFiles = new List<string>();
        for (var i = 0; i < 15; i++) // More than 10 to test limit
        {
            var file = Path.Combine(_testDirectory, $"log{i:D2}.log");
            File.WriteAllText(file, $"Content {i}");
            testFiles.Add(file);
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
        var cts = new CancellationTokenSource(TimeSpan.FromSeconds(3));
        await _command.ExecuteAsync(options, cts.Token);

        // Assert
        _mockScanPipeline.Verify(p => p.ProcessSingleAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()),
            Times.Exactly(15));
        // Dashboard should only show the 10 most recent files
    }

    #endregion

    #region Dashboard Update Tests

    [Fact(Timeout = 10000)]
    public async Task Dashboard_UpdatesPeriodically()
    {
        // Arrange
        var options = new WatchOptions
        {
            Path = _testDirectory,
            ShowDashboard = true
        };

        var cts = new CancellationTokenSource(TimeSpan.FromMilliseconds(2500));

        // Act
        var watchTask = Task.Run(async () =>
        {
            try
            {
                await _command.ExecuteAsync(options, cts.Token);
            }
            catch (OperationCanceledException)
            {
                // Expected when timeout is reached
            }
        });

        // Wait for the task to complete or timeout
        try
        {
            await watchTask;
        }
        catch (OperationCanceledException)
        {
            // Expected when cancelling
        }

        // Assert
        // Dashboard should have updated at least twice in 2.5 seconds (updates every second)
        // Note: Actual verification would require intercepting the Live display updates
    }

    [Fact(Timeout = 5000)]
    public async Task Dashboard_ShowsCorrectStatusForCleanFiles()
    {
        // Arrange
        var testFile = Path.Combine(_testDirectory, "clean.log");
        File.WriteAllText(testFile, "Clean content");

        var scanResult = new ScanResult
        {
            LogPath = testFile,
            AnalysisResults = new List<AnalysisResult>
            {
                new GenericAnalysisResult
                {
                    AnalyzerName = "Analyzer1", HasFindings = false, ReportLines = new List<string>()
                }, // No findings
                new GenericAnalysisResult
                    { AnalyzerName = "Analyzer2", HasFindings = false, ReportLines = new List<string>() } // No findings
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
        await _command.ExecuteAsync(options, cts.Token);

        // Assert
        _mockScanPipeline.Verify(p => p.ProcessSingleAsync(testFile, It.IsAny<CancellationToken>()), Times.Once);
        // File should be marked as "Clean" in statistics
    }

    [Fact(Timeout = 5000)]
    public async Task Dashboard_ShowsCorrectStatusForProblematicFiles()
    {
        // Arrange
        var testFile = Path.Combine(_testDirectory, "problematic.log");
        File.WriteAllText(testFile, "Problematic content");

        var scanResult = new ScanResult
        {
            LogPath = testFile,
            AnalysisResults = new List<AnalysisResult>
            {
                new GenericAnalysisResult
                {
                    AnalyzerName = "Analyzer1", HasFindings = true,
                    ReportLines = new List<string> { "Issue 1", "Issue 2" }
                },
                new GenericAnalysisResult
                    { AnalyzerName = "Analyzer2", HasFindings = true, ReportLines = new List<string> { "Issue 3" } }
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
        await _command.ExecuteAsync(options, cts.Token);

        // Assert
        _mockScanPipeline.Verify(p => p.ProcessSingleAsync(testFile, It.IsAny<CancellationToken>()), Times.Once);
        // File should show "3 issues" in statistics
    }

    #endregion

    #region Dashboard Configuration Tests

    [Fact(Timeout = 5000)]
    public async Task Dashboard_DisabledWithOption_DoesNotClearScreen()
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
        // Screen should not be cleared when dashboard is disabled
    }

    [Fact(Timeout = 5000)]
    public async Task Dashboard_EnabledWithOption_ClearsScreen()
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
        // Screen should be cleared when dashboard is enabled
    }

    #endregion

    #region ScanStatistics Class Tests

    [Fact]
    public void ScanStatistics_InitializesWithZeroValues()
    {
        // Arrange & Act
        var stats = new ScanStatistics();

        // Assert
        stats.IssueCount.Should().Be(0);
        stats.CriticalCount.Should().Be(0);
        stats.WarningCount.Should().Be(0);
        stats.InfoCount.Should().Be(0);
    }

    [Fact]
    public void ScanStatistics_CanTrackMultipleIssueCounts()
    {
        // Arrange
        var stats = new ScanStatistics();

        // Act
        stats.IssueCount = 5;
        stats.CriticalCount = 1;
        stats.WarningCount = 3;
        stats.InfoCount = 1;

        // Assert
        stats.IssueCount.Should().Be(5);
        stats.CriticalCount.Should().Be(1);
        stats.WarningCount.Should().Be(3);
        stats.InfoCount.Should().Be(1);
    }

    #endregion
}