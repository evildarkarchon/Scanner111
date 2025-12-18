using System.Collections.Concurrent;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
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

namespace Scanner111.Common.Tests.Integration;

public class EndToEndScanTests
{
    private readonly string _sampleLogsRoot;

    public EndToEndScanTests()
    {
        // Find the root directory (Scanner111) from the test execution directory
        // bin/Debug/net9.0-windows.../ -> ../../../
        var currentDir = AppDomain.CurrentDomain.BaseDirectory;
        var rootDir = Directory.GetParent(currentDir)?.Parent?.Parent?.Parent?.Parent?.FullName;
        
        // Fallback if null (shouldn't happen in typical dev env)
        _sampleLogsRoot = rootDir != null 
            ? Path.Combine(rootDir, "sample_logs", "FO4") 
            : Path.Combine("J:", "Scanner111", "sample_logs", "FO4"); // Hard fallback for this env
            
        if (!Directory.Exists(_sampleLogsRoot))
        {
            // Try finding it via relative path if we are in the project folder
             _sampleLogsRoot = Path.GetFullPath(Path.Combine("..", "..", "..", "..", "sample_logs", "FO4"));
        }
    }

    [Theory]
    [MemberData(nameof(GetFO4SampleLogs), 5)] // Test 5 random FO4 logs
    public async Task ScanPipeline_WithSampleLogs_ProducesValidReports(string logPath)
    {
        // Arrange
        if (!File.Exists(logPath))
        {
            // Skip if file not found (e.g. environment difference)
            return; 
        }

        var executor = CreateScanExecutor();
        var config = new ScanConfig 
        { 
            ScanPath = Path.GetDirectoryName(logPath) ?? string.Empty,
            CustomPaths = new Dictionary<string, string>() 
        };

        // Create temp directory for output to avoid writing to source
        var tempDir = Path.Combine(Path.GetTempPath(), "Scanner111_Tests", Guid.NewGuid().ToString());
        Directory.CreateDirectory(tempDir);

        try
        {
            // Copy log to temp directory
            var tempLogPath = Path.Combine(tempDir, Path.GetFileName(logPath));
            File.Copy(logPath, tempLogPath);

            // We need to point the executor to scan the temp file/dir
            // But ScanExecutor scans a *Directory*. 
            // So we point it to tempDir.
            var testConfig = config with { ScanPath = tempDir };

            // Act
            var result = await executor.ExecuteScanAsync(testConfig);

            // Assert
            result.Statistics.Scanned.Should().Be(1);
            result.Statistics.Failed.Should().Be(0);

            var reportPath = tempLogPath.Replace(".log", "-AUTOSCAN.md");
            File.Exists(reportPath).Should().BeTrue($"Report should exist at {reportPath}");

            var reportContent = await File.ReadAllTextAsync(reportPath);
            reportContent.Should().Contain("# Crash Log Analysis");
            reportContent.Should().NotBeEmpty();
        }
        finally
        {
            if (Directory.Exists(tempDir))
            {
                Directory.Delete(tempDir, recursive: true);
            }
        }
    }

    public static IEnumerable<object[]> GetFO4SampleLogs(int count)
    {
        // This runs before constructor, so we need robust path finding logic again or hardcode for this env
        var possiblePaths = new[]
        {
            Path.Combine("J:", "Scanner111", "sample_logs", "FO4"),
            Path.Combine("..", "..", "..", "..", "sample_logs", "FO4"),
            "sample_logs/FO4"
        };

        string? logDir = null;
        foreach (var path in possiblePaths)
        {
            if (Directory.Exists(path))
            {
                logDir = path;
                break;
            }
        }

        if (logDir == null)
        {
            return Enumerable.Empty<object[]>();
        }

        var logs = Directory.GetFiles(logDir, "*.log")
            .OrderBy(_ => Random.Shared.Next())
            .Take(count)
            .Select(log => new object[] { log })
            .ToList();
            
        return logs;
    }

    private IScanExecutor CreateScanExecutor()
    {
        // Real Services
        var fileIO = new FileIOService();
        var parser = new LogParser();
        var pluginAnalyzer = new PluginAnalyzer();
        var reportWriter = new ReportWriter(fileIO);
        
        // Mocked Data Services
        var suspectScanner = new SuspectScanner(); // It has no state, just logic?
        // Wait, SuspectScanner passes patterns in ScanAsync.
        // So we need to mock ConfigurationCache to return patterns.
        
        var configCacheMock = new Mock<IConfigurationCache>();
        configCacheMock.Setup(x => x.GetSuspectPatternsAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new SuspectPatterns 
            { 
                ErrorPatterns = new List<SuspectPattern> 
                { 
                    new() { Pattern = "EXCEPTION_ACCESS_VIOLATION", Message = "Access Violation" } 
                } 
            });
            
        configCacheMock.Setup(x => x.GetGameSettingsAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new GameSettings());

        var settingsScannerMock = new Mock<ISettingsScanner>();
        settingsScannerMock.Setup(x => x.ScanAsync(It.IsAny<LogSegment>(), It.IsAny<GameSettings>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new SettingsScanResult());

        // Mock DB - FormIdAnalyzer handles DB failure gracefully, but let's mock the factory to be safe
        var dbFactoryMock = new Mock<IDatabaseConnectionFactory>();
        // We can let it throw or return null, FormIdAnalyzer should handle it.
        // Or providing a real FormIdAnalyzer with a mocked factory that throws is fine.
        var formIdAnalyzer = new FormIdAnalyzer(NullLogger<FormIdAnalyzer>.Instance, dbFactoryMock.Object); 

        var orchestrator = new LogOrchestrator(
            NullLogger<LogOrchestrator>.Instance,
            fileIO,
            parser,
            pluginAnalyzer,
            suspectScanner,
            settingsScannerMock.Object,
            reportWriter,
            configCacheMock.Object
        );

        return new ScanExecutor(NullLogger<ScanExecutor>.Instance, () => orchestrator);
    }
}
