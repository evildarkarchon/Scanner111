using FluentAssertions;
using Moq;
using Scanner111.Common.Models.Analysis;
using Scanner111.Common.Models.Configuration;
using Scanner111.Common.Services.Analysis;
using Scanner111.Common.Services.Configuration;
using Scanner111.Common.Services.Database;
using Scanner111.Common.Services.FileIO;
using Scanner111.Common.Services.Orchestration;
using Scanner111.Common.Services.Parsing;
using Scanner111.Common.Services.Reporting;

namespace Scanner111.Common.Tests.EdgeCases;

public class EdgeCaseTests : IDisposable
{
    private readonly string _tempDir;
    private readonly IScanExecutor _executor;

    public EdgeCaseTests()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), "Scanner111_EdgeCases", Guid.NewGuid().ToString());
        Directory.CreateDirectory(_tempDir);
        _executor = CreateScanExecutor();
    }

    public void Dispose()
    {
        if (Directory.Exists(_tempDir))
        {
            Directory.Delete(_tempDir, recursive: true);
        }
    }

    [Fact]
    public async Task Scan_EmptyFile_HandlesGracefully()
    {
        // Arrange
        var logPath = Path.Combine(_tempDir, "crash-empty.log");
        await File.WriteAllTextAsync(logPath, "");

        var config = new ScanConfig { ScanPath = _tempDir };

        // Act
        var result = await _executor.ExecuteScanAsync(config);

        // Assert
        // An empty file usually parses as invalid or empty result.
        // LogOrchestrator catches invalid parses.
        // It typically returns a result with IsComplete=false and warnings.
        // ScanExecutor counts it as "Scanned" (processedFiles) unless it threw exception.
        result.Statistics.Scanned.Should().Be(1);
        result.Statistics.Failed.Should().Be(0); // "Failed" in ScanExecutor means Exception thrown.
        
        var reportPath = logPath.Replace(".log", "-AUTOSCAN.md");
        File.Exists(reportPath).Should().BeTrue();
        var content = await File.ReadAllTextAsync(reportPath);
        content.Should().Contain("Invalid or incomplete");
    }

    [Fact]
    public async Task Scan_GarbageContent_HandlesGracefully()
    {
        // Arrange
        var logPath = Path.Combine(_tempDir, "crash-garbage.log");
        await File.WriteAllTextAsync(logPath, "This is just some random text\nNot a real log.");

        var config = new ScanConfig { ScanPath = _tempDir };

        // Act
        var result = await _executor.ExecuteScanAsync(config);

        // Assert
        result.Statistics.Scanned.Should().Be(1);
        
        var reportPath = logPath.Replace(".log", "-AUTOSCAN.md");
        File.Exists(reportPath).Should().BeTrue();
    }

    private IScanExecutor CreateScanExecutor()
    {
        // Minimal setup
        var fileIO = new FileIOService();
        var parser = new LogParser();
        var pluginAnalyzer = new PluginAnalyzer();
        var reportWriter = new ReportWriter(fileIO);
        
        var configCacheMock = new Mock<IConfigurationCache>();
        configCacheMock.Setup(x => x.GetSuspectPatternsAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new SuspectPatterns());
        configCacheMock.Setup(x => x.GetGameSettingsAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new GameSettings());

        var settingsScannerMock = new Mock<ISettingsScanner>();
        settingsScannerMock.Setup(x => x.ScanAsync(It.IsAny<LogSegment>(), It.IsAny<GameSettings>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new SettingsScanResult());

        var dbFactoryMock = new Mock<IDatabaseConnectionFactory>();
        
        // Mock Orchestrator dependencies
        var orchestrator = new LogOrchestrator(
            fileIO,
            parser,
            pluginAnalyzer,
            new SuspectScanner(),
            settingsScannerMock.Object,
            reportWriter,
            configCacheMock.Object
        );

        return new ScanExecutor(() => orchestrator);
    }
}
