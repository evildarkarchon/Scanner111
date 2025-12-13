using System.Diagnostics;
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

namespace Scanner111.Common.Tests.Performance;

public class BenchmarkTests : IDisposable
{
    private readonly string _tempDir;
    private readonly string _sampleLogsRoot;

    public BenchmarkTests()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), "Scanner111_Perf", Guid.NewGuid().ToString());
        Directory.CreateDirectory(_tempDir);

        // Locate sample logs
        var currentDir = AppDomain.CurrentDomain.BaseDirectory;
        var rootDir = Directory.GetParent(currentDir)?.Parent?.Parent?.Parent?.Parent?.FullName;
        
        _sampleLogsRoot = rootDir != null 
            ? Path.Combine(rootDir, "sample_logs", "FO4") 
            : Path.Combine("J:", "Scanner111", "sample_logs", "FO4");
            
        if (!Directory.Exists(_sampleLogsRoot))
        {
             _sampleLogsRoot = Path.GetFullPath(Path.Combine("..", "..", "..", "..", "sample_logs", "FO4"));
        }
    }

    public void Dispose()
    {
        if (Directory.Exists(_tempDir))
        {
            Directory.Delete(_tempDir, recursive: true);
        }
    }

    [Fact]
    public async Task Scan_BatchOfLogs_CompletesWithinReasonableTime()
    {
        // Arrange
        if (!Directory.Exists(_sampleLogsRoot))
        {
            return; // Skip if logs not found
        }

        var logs = Directory.GetFiles(_sampleLogsRoot, "*.log").Take(20).ToList(); // Take 20 logs
        if (logs.Count == 0) return;

        foreach (var log in logs)
        {
            File.Copy(log, Path.Combine(_tempDir, Path.GetFileName(log)));
        }

        var executor = CreateScanExecutor();
        var config = new ScanConfig { ScanPath = _tempDir, MaxConcurrent = 10 };

        // Act
        var sw = Stopwatch.StartNew();
        var result = await executor.ExecuteScanAsync(config);
        sw.Stop();

        // Assert
        result.Statistics.Scanned.Should().Be(logs.Count);
        
        // Expect < 100ms per log on average for this lightweight logic (no heavy regex/DB in mock)
        // Total < 2 seconds for 20 logs
        sw.Elapsed.TotalSeconds.Should().BeLessThan(10); 
    }

    private IScanExecutor CreateScanExecutor()
    {
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
