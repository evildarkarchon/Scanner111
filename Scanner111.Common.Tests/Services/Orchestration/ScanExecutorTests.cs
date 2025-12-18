using FluentAssertions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Scanner111.Common.Models.Analysis;
using Scanner111.Common.Models.Configuration;
using Scanner111.Common.Models.Reporting;
using Scanner111.Common.Services.Orchestration;

namespace Scanner111.Common.Tests.Services.Orchestration;

public class ScanExecutorTests : IDisposable
{
    private readonly Mock<ILogOrchestrator> _orchestrator;
    private readonly ScanExecutor _executor;
    private readonly string _tempDir;

    public ScanExecutorTests()
    {
        _orchestrator = new Mock<ILogOrchestrator>();
        _executor = new ScanExecutor(NullLogger<ScanExecutor>.Instance, () => _orchestrator.Object);
        _tempDir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
        Directory.CreateDirectory(_tempDir);
    }

    public void Dispose()
    {
        if (Directory.Exists(_tempDir))
        {
            Directory.Delete(_tempDir, recursive: true);
        }
    }

    [Fact]
    public async Task ExecuteScanAsync_WithMultipleLogs_ProcessesAllLogs()
    {
        // Arrange
        var log1 = Path.Combine(_tempDir, "crash-1.log");
        var log2 = Path.Combine(_tempDir, "crash-2.log");
        File.WriteAllText(log1, "log1");
        File.WriteAllText(log2, "log2");

        var config = new ScanConfig { ScanPath = _tempDir, MaxConcurrent = 2 };

        _orchestrator.Setup(x => x.ProcessLogAsync(It.IsAny<string>(), config, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new LogAnalysisResult 
            { 
                IsComplete = true, 
                Report = new ReportFragment(),
                Header = new CrashHeader() 
            });

        // Act
        var result = await _executor.ExecuteScanAsync(config);

        // Assert
        result.Statistics.TotalFiles.Should().Be(2);
        result.Statistics.Scanned.Should().Be(2);
        result.Statistics.Failed.Should().Be(0);
        result.ProcessedFiles.Should().Contain(log1);
        result.ProcessedFiles.Should().Contain(log2);
        
        _orchestrator.Verify(x => x.ProcessLogAsync(log1, config, It.IsAny<CancellationToken>()), Times.Once);
        _orchestrator.Verify(x => x.ProcessLogAsync(log2, config, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task ExecuteScanAsync_WithFailure_TracksFailedLogs()
    {
        // Arrange
        var log1 = Path.Combine(_tempDir, "crash-1.log");
        File.WriteAllText(log1, "log1");

        var config = new ScanConfig { ScanPath = _tempDir };

        _orchestrator.Setup(x => x.ProcessLogAsync(log1, config, It.IsAny<CancellationToken>()))
            .ThrowsAsync(new Exception("Processing failed"));

        // Act
        var result = await _executor.ExecuteScanAsync(config);

        // Assert
        result.Statistics.Failed.Should().Be(1);
        result.FailedLogs.Should().Contain(log1);
        result.ErrorMessages.Should().ContainMatch("*Processing failed*");
    }
    
    [Fact]
    public async Task ExecuteScanAsync_WithProgress_ReportsProgress()
    {
        // Arrange
        var log1 = Path.Combine(_tempDir, "crash-1.log");
        File.WriteAllText(log1, "log1");

        var config = new ScanConfig { ScanPath = _tempDir };
        var progressMock = new Mock<IProgress<ScanProgress>>();

        _orchestrator.Setup(x => x.ProcessLogAsync(It.IsAny<string>(), config, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new LogAnalysisResult 
            { 
                IsComplete = true, 
                Report = new ReportFragment(),
                Header = new CrashHeader() 
            });

        // Act
        await _executor.ExecuteScanAsync(config, progressMock.Object);

        // Assert
        progressMock.Verify(x => x.Report(It.Is<ScanProgress>(p => p.FilesProcessed == 1)), Times.Once);
    }
}
