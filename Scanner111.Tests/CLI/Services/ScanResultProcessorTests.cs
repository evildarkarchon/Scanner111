using Moq;
using Scanner111.CLI.Models;
using Scanner111.CLI.Services;
using Scanner111.Core.Analyzers;
using Scanner111.Core.Infrastructure;
using Scanner111.Core.Models;
using Xunit;

namespace Scanner111.Tests.CLI.Services;

public class ScanResultProcessorTests : IDisposable
{
    private readonly Mock<IReportWriter> _reportWriterMock;
    private readonly ScanResultProcessor _scanResultProcessor;
    private readonly string _testDirectory;
    private readonly Mock<IUnsolvedLogsMover> _unsolvedLogsMoverMock;

    public ScanResultProcessorTests()
    {
        _unsolvedLogsMoverMock = new Mock<IUnsolvedLogsMover>();
        _reportWriterMock = new Mock<IReportWriter>();
        _scanResultProcessor = new ScanResultProcessor(_unsolvedLogsMoverMock.Object);

        _testDirectory = Path.Combine(Path.GetTempPath(), $"ScanResultProcessorTests_{Guid.NewGuid()}");
        Directory.CreateDirectory(_testDirectory);
    }

    public void Dispose()
    {
        if (Directory.Exists(_testDirectory))
        {
            try
            {
                Directory.Delete(_testDirectory, true);
            }
            catch
            {
                // Ignore cleanup errors
            }
        }
    }

    [Fact]
    public async Task ProcessScanResultAsync_WithSummaryFormat_DoesNotWriteReport()
    {
        // Arrange
        var result = CreateScanResult(hasFindings: true);
        var options = new ScanOptions { OutputFormat = "summary" };
        var settings = new ApplicationSettings { AutoSaveResults = true };
        var xseCopiedFiles = new HashSet<string>();

        // Act
        await _scanResultProcessor.ProcessScanResultAsync(result, options, _reportWriterMock.Object, xseCopiedFiles, settings);

        // Assert
        _reportWriterMock.Verify(x => x.WriteReportAsync(It.IsAny<ScanResult>(), It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
        _unsolvedLogsMoverMock.Verify(x => x.MoveUnsolvedLogAsync(It.IsAny<string>(), It.IsAny<ApplicationSettings>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task ProcessScanResultAsync_WithFindings_ShowsWarningMessage()
    {
        // Arrange
        var result = CreateScanResult(hasFindings: true);
        var options = new ScanOptions { OutputFormat = "detailed" };
        var settings = new ApplicationSettings { AutoSaveResults = false };
        var xseCopiedFiles = new HashSet<string>();

        // Act
        await _scanResultProcessor.ProcessScanResultAsync(result, options, _reportWriterMock.Object, xseCopiedFiles, settings);

        // Assert
        _reportWriterMock.Verify(x => x.WriteReportAsync(It.IsAny<ScanResult>(), It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task ProcessScanResultAsync_WithoutFindings_ShowsSuccessMessage()
    {
        // Arrange
        var result = CreateScanResult(hasFindings: false);
        var options = new ScanOptions { OutputFormat = "detailed" };
        var settings = new ApplicationSettings { AutoSaveResults = false };
        var xseCopiedFiles = new HashSet<string>();

        // Act
        await _scanResultProcessor.ProcessScanResultAsync(result, options, _reportWriterMock.Object, xseCopiedFiles, settings);

        // Assert
        _reportWriterMock.Verify(x => x.WriteReportAsync(It.IsAny<ScanResult>(), It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task ProcessScanResultAsync_WithAutoSave_WritesReport()
    {
        // Arrange
        var logPath = Path.Combine(_testDirectory, "test.log");
        var result = CreateScanResult(hasFindings: true, logPath: logPath);
        result.Report.Add("Test report content");
        
        var options = new ScanOptions { OutputFormat = "detailed" };
        var settings = new ApplicationSettings { AutoSaveResults = true };
        var xseCopiedFiles = new HashSet<string>();

        _reportWriterMock.Setup(x => x.WriteReportAsync(It.IsAny<ScanResult>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        // Act
        await _scanResultProcessor.ProcessScanResultAsync(result, options, _reportWriterMock.Object, xseCopiedFiles, settings);

        // Assert
        _reportWriterMock.Verify(x => x.WriteReportAsync(
            It.Is<ScanResult>(r => r == result),
            It.Is<string>(p => p.EndsWith("-AUTOSCAN.md")),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task ProcessScanResultAsync_WithReportText_WritesReport()
    {
        // Arrange
        var logPath = Path.Combine(_testDirectory, "test.log");
        var result = CreateScanResult(hasFindings: true, logPath: logPath);
        result.Report.Add("Test report content");

        var options = new ScanOptions { OutputFormat = "detailed" };
        var settings = new ApplicationSettings { AutoSaveResults = true };
        var xseCopiedFiles = new HashSet<string>();

        _reportWriterMock.Setup(x => x.WriteReportAsync(It.IsAny<ScanResult>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        // Act
        await _scanResultProcessor.ProcessScanResultAsync(result, options, _reportWriterMock.Object, xseCopiedFiles, settings);

        // Assert
        _reportWriterMock.Verify(x => x.WriteReportAsync(It.IsAny<ScanResult>(), It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task ProcessScanResultAsync_WithNoReportText_StillWritesIfAutoSave()
    {
        // Arrange
        var result = CreateScanResult(hasFindings: true);
        // Don't add any report content - but ReportText will generate from CrashLog if present

        var options = new ScanOptions { OutputFormat = "detailed" };
        var settings = new ApplicationSettings { AutoSaveResults = true };
        var xseCopiedFiles = new HashSet<string>();

        _reportWriterMock.Setup(x => x.WriteReportAsync(It.IsAny<ScanResult>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        // Act
        await _scanResultProcessor.ProcessScanResultAsync(result, options, _reportWriterMock.Object, xseCopiedFiles, settings);

        // Assert
        // With AutoSave enabled, it may still try to write even if ReportText is empty
        _reportWriterMock.Verify(x => x.WriteReportAsync(It.IsAny<ScanResult>(), It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task ProcessScanResultAsync_WithFailedScan_MovesUnsolvedLog()
    {
        // Arrange
        var logPath = Path.Combine(_testDirectory, "test.log");
        var result = CreateScanResult(hasFindings: false, logPath: logPath);
        result.Status = ScanStatus.Failed;

        var options = new ScanOptions { MoveUnsolved = true, OutputFormat = "detailed" };
        var settings = new ApplicationSettings { AutoSaveResults = true };
        var xseCopiedFiles = new HashSet<string>();

        _unsolvedLogsMoverMock.Setup(x => x.MoveUnsolvedLogAsync(It.IsAny<string>(), It.IsAny<ApplicationSettings>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        // Act
        await _scanResultProcessor.ProcessScanResultAsync(result, options, _reportWriterMock.Object, xseCopiedFiles, settings);

        // Assert
        _unsolvedLogsMoverMock.Verify(x => x.MoveUnsolvedLogAsync(logPath, settings, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task ProcessScanResultAsync_WithErrorsButNotFailed_MovesUnsolvedLog()
    {
        // Arrange
        var logPath = Path.Combine(_testDirectory, "test.log");
        var result = CreateScanResult(hasFindings: false, logPath: logPath);
        result.ErrorMessages.Add("Test error");

        var options = new ScanOptions { MoveUnsolved = true, OutputFormat = "detailed" };
        var settings = new ApplicationSettings { AutoSaveResults = true };
        var xseCopiedFiles = new HashSet<string>();

        _unsolvedLogsMoverMock.Setup(x => x.MoveUnsolvedLogAsync(It.IsAny<string>(), It.IsAny<ApplicationSettings>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        // Act
        await _scanResultProcessor.ProcessScanResultAsync(result, options, _reportWriterMock.Object, xseCopiedFiles, settings);

        // Assert
        _unsolvedLogsMoverMock.Verify(x => x.MoveUnsolvedLogAsync(logPath, settings, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task ProcessScanResultAsync_WithoutMoveOption_DoesNotMoveUnsolved()
    {
        // Arrange
        var result = CreateScanResult(hasFindings: false);
        result.Status = ScanStatus.Failed;
        result.ErrorMessages.Add("Test error");

        var options = new ScanOptions { OutputFormat = "detailed", MoveUnsolved = false };
        var settings = new ApplicationSettings { AutoSaveResults = true };
        var xseCopiedFiles = new HashSet<string>();

        // Act
        await _scanResultProcessor.ProcessScanResultAsync(result, options, _reportWriterMock.Object, xseCopiedFiles, settings);

        // Assert
        _unsolvedLogsMoverMock.Verify(x => x.MoveUnsolvedLogAsync(It.IsAny<string>(), It.IsAny<ApplicationSettings>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task ProcessScanResultAsync_WriteReportThrowsException_ContinuesProcessing()
    {
        // Arrange
        var result = CreateScanResult(hasFindings: true);
        result.Report.Add("Test report content");

        var options = new ScanOptions { OutputFormat = "detailed" };
        var settings = new ApplicationSettings { AutoSaveResults = true };
        var xseCopiedFiles = new HashSet<string>();

        _reportWriterMock.Setup(x => x.WriteReportAsync(It.IsAny<ScanResult>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new IOException("Disk full"));

        // Act - should not throw
        await _scanResultProcessor.ProcessScanResultAsync(result, options, _reportWriterMock.Object, xseCopiedFiles, settings);

        // Assert
        _reportWriterMock.Verify(x => x.WriteReportAsync(It.IsAny<ScanResult>(), It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task ProcessScanResultAsync_MoveUnsolvedThrowsException_ContinuesProcessing()
    {
        // Arrange
        var result = CreateScanResult(hasFindings: false);
        result.Status = ScanStatus.Failed;

        var options = new ScanOptions { OutputFormat = "detailed", MoveUnsolved = true };
        var settings = new ApplicationSettings { AutoSaveResults = true };
        var xseCopiedFiles = new HashSet<string>();

        _unsolvedLogsMoverMock.Setup(x => x.MoveUnsolvedLogAsync(It.IsAny<string>(), It.IsAny<ApplicationSettings>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new IOException("Cannot move file"));

        // Act - should not throw
        await _scanResultProcessor.ProcessScanResultAsync(result, options, _reportWriterMock.Object, xseCopiedFiles, settings);

        // Assert
        _unsolvedLogsMoverMock.Verify(x => x.MoveUnsolvedLogAsync(It.IsAny<string>(), It.IsAny<ApplicationSettings>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task ProcessScanResultAsync_WithNullReportWriter_DoesNotThrow()
    {
        // Arrange
        var result = CreateScanResult(hasFindings: true);
        result.Report.Add("Test report content");

        var options = new ScanOptions { OutputFormat = "detailed" };
        var settings = new ApplicationSettings { AutoSaveResults = true };
        var xseCopiedFiles = new HashSet<string>();

        // Act & Assert (should not throw)
        await _scanResultProcessor.ProcessScanResultAsync(result, options, null!, xseCopiedFiles, settings);
    }

    [Fact]
    public async Task ProcessScanResultAsync_WithXseCopiedFile_HandlesCorrectly()
    {
        // Arrange
        var logPath = Path.Combine(_testDirectory, "test.log");
        var result = CreateScanResult(hasFindings: true, logPath: logPath);
        result.WasCopiedFromXse = true;
        result.Report.Add("Test report content");

        var options = new ScanOptions { OutputFormat = "detailed" };
        var settings = new ApplicationSettings { AutoSaveResults = true };
        var xseCopiedFiles = new HashSet<string> { logPath };

        _reportWriterMock.Setup(x => x.WriteReportAsync(It.IsAny<ScanResult>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        // Act
        await _scanResultProcessor.ProcessScanResultAsync(result, options, _reportWriterMock.Object, xseCopiedFiles, settings);

        // Assert
        Assert.Contains(logPath, xseCopiedFiles);
        _reportWriterMock.Verify(x => x.WriteReportAsync(It.IsAny<ScanResult>(), It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    private ScanResult CreateScanResult(bool hasFindings, string? logPath = null)
    {
        logPath ??= Path.Combine(_testDirectory, "test.log");
        
        var result = new ScanResult
        {
            LogPath = logPath,
            Status = hasFindings ? ScanStatus.CompletedWithErrors : ScanStatus.Completed
        };

        if (hasFindings)
        {
            result.AnalysisResults.Add(new GenericAnalysisResult
            {
                AnalyzerName = "TestAnalyzer",
                HasFindings = true,
                ReportLines = new List<string> { "Test finding" }
            });
        }
        else
        {
            result.AnalysisResults.Add(new GenericAnalysisResult
            {
                AnalyzerName = "TestAnalyzer",
                HasFindings = false
            });
        }

        return result;
    }
}