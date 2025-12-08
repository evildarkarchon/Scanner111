using FluentAssertions;
using Moq;
using Scanner111.Common.Models.Analysis;
using Scanner111.Common.Models.Configuration;
using Scanner111.Common.Models.Reporting;
using Scanner111.Common.Services.Analysis;
using Scanner111.Common.Services.Configuration;
using Scanner111.Common.Services.FileIO;
using Scanner111.Common.Services.Orchestration;
using Scanner111.Common.Services.Parsing;
using Scanner111.Common.Services.Reporting;

namespace Scanner111.Common.Tests.Services.Orchestration;

public class LogOrchestratorTests
{
    private readonly Mock<IFileIOService> _fileIO;
    private readonly Mock<ILogParser> _parser;
    private readonly Mock<IPluginAnalyzer> _pluginAnalyzer;
    private readonly Mock<ISuspectScanner> _suspectScanner;
    private readonly Mock<ISettingsScanner> _settingsScanner;
    private readonly Mock<IReportWriter> _reportWriter;
    private readonly Mock<IConfigurationCache> _configCache;
    private readonly LogOrchestrator _orchestrator;

    public LogOrchestratorTests()
    {
        _fileIO = new Mock<IFileIOService>();
        _parser = new Mock<ILogParser>();
        _pluginAnalyzer = new Mock<IPluginAnalyzer>();
        _suspectScanner = new Mock<ISuspectScanner>();
        _settingsScanner = new Mock<ISettingsScanner>();
        _reportWriter = new Mock<IReportWriter>();
        _configCache = new Mock<IConfigurationCache>();

        _orchestrator = new LogOrchestrator(
            _fileIO.Object,
            _parser.Object,
            _pluginAnalyzer.Object,
            _suspectScanner.Object,
            _settingsScanner.Object,
            _reportWriter.Object,
            _configCache.Object);
    }

    [Fact]
    public async Task ProcessLogAsync_WithValidLog_OrchestratesAnalysisCorrectly()
    {
        // Arrange
        var logPath = "test.log";
        var config = new ScanConfig();
        var logContent = "LOG CONTENT";

        var header = new CrashHeader { GameVersion = "Fallout 4 v1.10.163", MainError = "Test Error" };
        var segments = new List<LogSegment> { new() { Name = "PLUGINS" } };
        
        var parseResult = new LogParseResult { IsValid = true, Header = header, Segments = segments };
        var pluginResult = new PluginAnalysisResult();
        var suspectResult = new SuspectScanResult();
        var settingsResult = new SettingsScanResult();

        _fileIO.Setup(x => x.ReadFileAsync(logPath, It.IsAny<CancellationToken>()))
            .ReturnsAsync(logContent);
        
        _parser.Setup(x => x.ParseAsync(logContent, It.IsAny<CancellationToken>()))
            .ReturnsAsync(parseResult);
        
        _configCache.Setup(x => x.GetSuspectPatternsAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new SuspectPatterns());
        
        _configCache.Setup(x => x.GetGameSettingsAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new GameSettings());

        _pluginAnalyzer.Setup(x => x.AnalyzeAsync(segments, It.IsAny<CancellationToken>()))
            .ReturnsAsync(pluginResult);
            
        _suspectScanner.Setup(x => x.ScanAsync(header, segments, It.IsAny<SuspectPatterns>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(suspectResult);

        // Act
        var result = await _orchestrator.ProcessLogAsync(logPath, config);

        // Assert
        result.Should().NotBeNull();
        result.IsComplete.Should().BeTrue();
        result.Report.HasContent.Should().BeTrue();
        
        _reportWriter.Verify(x => x.WriteReportAsync(logPath, It.IsAny<ReportFragment>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task ProcessLogAsync_WithInvalidLog_ReturnsEarly()
    {
        // Arrange
        var logPath = "invalid.log";
        var config = new ScanConfig();
        
        _fileIO.Setup(x => x.ReadFileAsync(logPath, It.IsAny<CancellationToken>()))
            .ReturnsAsync("bad content");
            
        _parser.Setup(x => x.ParseAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new LogParseResult { IsValid = false, ErrorMessage = "Invalid format" });

        // Act
        var result = await _orchestrator.ProcessLogAsync(logPath, config);

        // Assert
        result.IsComplete.Should().BeFalse();
        result.Warnings.Should().Contain("Invalid format");
        
        _pluginAnalyzer.Verify(x => x.AnalyzeAsync(It.IsAny<IReadOnlyList<LogSegment>>(), It.IsAny<CancellationToken>()), Times.Never);
        _reportWriter.Verify(x => x.WriteReportAsync(It.IsAny<string>(), It.IsAny<ReportFragment>(), It.IsAny<CancellationToken>()), Times.Once);
    }
}
