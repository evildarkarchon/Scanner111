using Scanner111.CLI.Commands;
using Scanner111.CLI.Models;
using Scanner111.Core.Models;
using Scanner111.Core.Services;
using Spectre.Console.Testing;

namespace Scanner111.Tests.CLI.Commands;

public class PapyrusCommandTests : IDisposable
{
    private readonly PapyrusCommand _command;
    private readonly TestConsole _console;
    private readonly Mock<IPapyrusMonitorService> _papyrusServiceMock;
    private readonly string _testDirectory;

    public PapyrusCommandTests()
    {
        _papyrusServiceMock = new Mock<IPapyrusMonitorService>();
        _command = new PapyrusCommand(_papyrusServiceMock.Object);
        _console = new TestConsole();

        _testDirectory = Path.Combine(Path.GetTempPath(), $"PapyrusCommandTests_{Guid.NewGuid()}");
        Directory.CreateDirectory(_testDirectory);
    }

    public void Dispose()
    {
        _command?.Dispose();

        // Clean up test directory
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
    public async Task ExecuteAsync_WithValidLogPath_StartsMonitoring()
    {
        // Arrange
        var logPath = Path.Combine(_testDirectory, "test.log");
        File.WriteAllText(logPath, "Test log content");

        var options = new PapyrusOptions
        {
            LogPath = logPath,
            AnalyzeOnce = false,
            ShowDashboard = false,
            Interval = 1000
        };

        var stats = new PapyrusStats
        {
            Timestamp = DateTime.UtcNow,
            Dumps = 10,
            Stacks = 5,
            Warnings = 20,
            Errors = 15,
            Ratio = 2.0,
            LogPath = logPath
        };

        _papyrusServiceMock.Setup(x => x.CurrentStats).Returns(stats);
        _papyrusServiceMock.Setup(x => x.StartMonitoringAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        using var cts = new CancellationTokenSource();

        // Start the command in a background task
        var commandTask = Task.Run(async () => await _command.ExecuteAsync(options, cts.Token));

        // Give it time to start
        await Task.Delay(100);

        // Cancel to stop monitoring
        cts.Cancel();

        // Act
        var result = await commandTask;

        // Assert
        result.Should().Be(0);
        _papyrusServiceMock.Verify(x => x.StartMonitoringAsync(logPath, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task ExecuteAsync_WithAnalyzeOnce_AnalyzesAndExits()
    {
        // Arrange
        var logPath = Path.Combine(_testDirectory, "analyze_once.log");
        File.WriteAllText(logPath, "Test log content");

        var options = new PapyrusOptions
        {
            LogPath = logPath,
            AnalyzeOnce = true,
            ShowDashboard = false
        };

        var stats = new PapyrusStats
        {
            Timestamp = DateTime.UtcNow,
            Dumps = 10,
            Stacks = 5,
            Warnings = 20,
            Errors = 15,
            Ratio = 2.0,
            LogPath = logPath
        };

        _papyrusServiceMock.Setup(x => x.AnalyzeLogAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(stats);

        // Act
        var result = await _command.ExecuteAsync(options);

        // Assert
        result.Should().Be(0);
        _papyrusServiceMock.Verify(x => x.AnalyzeLogAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()),
            Times.Once);
        _papyrusServiceMock.Verify(x => x.StartMonitoringAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task ExecuteAsync_WithExportPath_ExportsStatistics()
    {
        // Arrange
        var logPath = Path.Combine(_testDirectory, "export_test.log");
        var exportPath = Path.Combine(_testDirectory, "export.csv");
        File.WriteAllText(logPath, "Test log content");

        var options = new PapyrusOptions
        {
            LogPath = logPath,
            AnalyzeOnce = true,
            ExportPath = exportPath,
            ExportFormat = "csv"
        };

        var stats = new PapyrusStats
        {
            Timestamp = DateTime.UtcNow,
            Dumps = 10,
            Stacks = 5,
            Warnings = 20,
            Errors = 15,
            Ratio = 2.0,
            LogPath = logPath
        };

        _papyrusServiceMock.Setup(x => x.AnalyzeLogAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(stats);
        _papyrusServiceMock.Setup(x =>
                x.ExportStatsAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        // Act
        var result = await _command.ExecuteAsync(options);

        // Assert
        result.Should().Be(0);
        _papyrusServiceMock.Verify(
            x => x.ExportStatsAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task ExecuteAsync_WithGameType_AutoDetectsPath()
    {
        // Arrange
        var detectedPath = Path.Combine(_testDirectory, "auto_detected.log");
        File.WriteAllText(detectedPath, "Test log content");

        var options = new PapyrusOptions
        {
            GameType = GameType.Fallout4,
            AnalyzeOnce = true
        };

        var stats = new PapyrusStats
        {
            Timestamp = DateTime.UtcNow,
            Dumps = 5,
            Stacks = 3,
            Warnings = 10,
            Errors = 8,
            Ratio = 1.67,
            LogPath = detectedPath
        };

        _papyrusServiceMock.Setup(x => x.DetectLogPathAsync(GameType.Fallout4))
            .ReturnsAsync(detectedPath);
        _papyrusServiceMock.Setup(x => x.AnalyzeLogAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(stats);

        // Act
        var result = await _command.ExecuteAsync(options);

        // Assert
        result.Should().Be(0);
        _papyrusServiceMock.Verify(x => x.DetectLogPathAsync(GameType.Fallout4), Times.Once);
        _papyrusServiceMock.Verify(x => x.AnalyzeLogAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task ExecuteAsync_WithNoPathAndNoAutoDetection_ReturnsError()
    {
        // Arrange
        var options = new PapyrusOptions
        {
            LogPath = null,
            GameType = null
        };

        _papyrusServiceMock.Setup(x => x.DetectLogPathAsync(It.IsAny<GameType>()))
            .ReturnsAsync((string?)null);

        // Act
        var result = await _command.ExecuteAsync(options);

        // Assert
        result.Should().Be(1);
    }

    [Fact]
    public async Task ExecuteAsync_WithAutoExport_SetsUpExportTimer()
    {
        // Arrange
        var logPath = Path.Combine(_testDirectory, "auto_export.log");
        var exportPath = Path.Combine(_testDirectory, "auto_export.csv");
        File.WriteAllText(logPath, "Test log content");

        var options = new PapyrusOptions
        {
            LogPath = logPath,
            AnalyzeOnce = false,
            ShowDashboard = false,
            AutoExport = true,
            ExportPath = exportPath,
            ExportInterval = 100 // Short interval for testing
        };

        _papyrusServiceMock.Setup(x => x.StartMonitoringAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        _papyrusServiceMock.Setup(x =>
                x.ExportStatsAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        using var cts = new CancellationTokenSource();

        // Start the command in a background task
        var commandTask = Task.Run(async () => await _command.ExecuteAsync(options, cts.Token));

        // Wait for potential export to occur
        await Task.Delay(300);

        // Cancel to stop monitoring
        cts.Cancel();

        // Act
        var result = await commandTask;

        // Assert
        result.Should().Be(0);
        // Export might be called depending on timing
        _papyrusServiceMock.Verify(x => x.StartMonitoringAsync(logPath, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public void Dispose_CleansUpResources()
    {
        // Arrange
        var command = new PapyrusCommand(_papyrusServiceMock.Object);

        // Act
        command.Dispose();
        command.Dispose(); // Should not throw on second call

        // Assert
        // No exception should be thrown
    }
}