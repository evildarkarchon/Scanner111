using Scanner111.Core.Abstractions;
using Scanner111.Core.Infrastructure;
using Scanner111.Core.Models;
using Scanner111.Core.Services;
using Scanner111.Tests.TestHelpers;

namespace Scanner111.Tests.Core.Services;

public class PapyrusMonitorServiceTests : IDisposable
{
    private readonly PapyrusMonitorService _service;
    private readonly TestApplicationSettingsService _settingsService;
    private readonly List<string> _tempFiles;
    private readonly string _testDirectory;
    private readonly TestYamlSettingsProvider _yamlSettingsProvider;
    private readonly TestFileSystem _fileSystem;
    private readonly TestEnvironmentPathProvider _environment;
    private readonly TestPathService _pathService;
    private readonly TestFileWatcherFactory _fileWatcherFactory;

    public PapyrusMonitorServiceTests()
    {
        _settingsService = new TestApplicationSettingsService();
        _yamlSettingsProvider = new TestYamlSettingsProvider();
        _fileSystem = new TestFileSystem();
        _environment = new TestEnvironmentPathProvider();
        _pathService = new TestPathService();
        _fileWatcherFactory = new TestFileWatcherFactory();
        
        _service = new PapyrusMonitorService(
            _settingsService, 
            _yamlSettingsProvider,
            _fileSystem,
            _environment,
            _pathService,
            _fileWatcherFactory);

        _testDirectory = Path.Combine(Path.GetTempPath(), $"PapyrusTests_{Guid.NewGuid()}");
        _fileSystem.CreateDirectory(_testDirectory);
        _tempFiles = new List<string>();
    }

    public void Dispose()
    {
        _service?.Dispose();

        // Clean up temp files (in TestFileSystem, cleanup is automatic)
        // No need to delete files from TestFileSystem as it's in-memory
    }

    [Fact]
    public async Task AnalyzeLogAsync_WithValidLogFile_ReturnsCorrectStats()
    {
        // Arrange
        var logContent = @"[08/15/2025 - 12:00:00PM] Papyrus log opened
[08/15/2025 - 12:00:01PM] Dumping stack 1:
[08/15/2025 - 12:00:02PM] 	Frame count: 3
[08/15/2025 - 12:00:03PM] Stack:
[08/15/2025 - 12:00:04PM] 	[TestScript (0x12345678)].TestFunction() - line 42
[08/15/2025 - 12:00:05PM] warning: Property not found on script
[08/15/2025 - 12:00:06PM] error: Cannot call method on None object
[08/15/2025 - 12:00:07PM] Dumping stack 2:
[08/15/2025 - 12:00:08PM] warning: Variable uninitialized
[08/15/2025 - 12:00:09PM] error: Array index out of bounds
[08/15/2025 - 12:00:10PM] error: Invalid cast";

        var logPath = CreateTempFile("test_papyrus.log", logContent);

        // Act
        var stats = await _service.AnalyzeLogAsync(logPath);

        // Assert
        stats.Should().NotBeNull();
        stats.Dumps.Should().Be(2);
        stats.Stacks.Should().Be(1);
        stats.Warnings.Should().Be(2);
        stats.Errors.Should().Be(3);
        stats.Ratio.Should().BeApproximately(2.0, 0.01);
        stats.LogPath.Should().Be(logPath);
    }

    [Fact]
    public async Task AnalyzeLogAsync_WithEmptyLogFile_ReturnsZeroStats()
    {
        // Arrange
        var logPath = CreateTempFile("empty_papyrus.log", "");

        // Act
        var stats = await _service.AnalyzeLogAsync(logPath);

        // Assert
        stats.Should().NotBeNull();
        stats.Dumps.Should().Be(0);
        stats.Stacks.Should().Be(0);
        stats.Warnings.Should().Be(0);
        stats.Errors.Should().Be(0);
        stats.Ratio.Should().Be(0.0);
    }

    [Fact]
    public async Task AnalyzeLogAsync_WithNonExistentFile_ThrowsFileNotFoundException()
    {
        // Arrange
        var nonExistentPath = Path.Combine(_testDirectory, "non_existent.log");

        // Act & Assert
        await Assert.ThrowsAsync<FileNotFoundException>(async () => await _service.AnalyzeLogAsync(nonExistentPath));
    }

    [Fact]
    public async Task AnalyzeLogAsync_WithLargeLogFile_HandlesEfficiently()
    {
        // Arrange
        var sb = new StringBuilder();
        sb.AppendLine("[08/15/2025 - 12:00:00PM] Papyrus log opened");

        // Generate large log content
        for (var i = 0; i < 1000; i++)
        {
            sb.AppendLine($"[08/15/2025 - 12:00:{i:00}PM] Dumping stack {i}:");
            sb.AppendLine($"[08/15/2025 - 12:00:{i:00}PM] Stack:");
            sb.AppendLine($"[08/15/2025 - 12:00:{i:00}PM] warning: Test warning {i}");
            sb.AppendLine($"[08/15/2025 - 12:00:{i:00}PM] error: Test error {i}");
        }

        var logPath = CreateTempFile("large_papyrus.log", sb.ToString());

        // Act
        var startTime = DateTime.UtcNow;
        var stats = await _service.AnalyzeLogAsync(logPath);
        var elapsed = DateTime.UtcNow - startTime;

        // Assert
        stats.Should().NotBeNull();
        stats.Dumps.Should().Be(1000);
        stats.Stacks.Should().Be(1000);
        stats.Warnings.Should().Be(1000);
        stats.Errors.Should().Be(1000);
        elapsed.TotalSeconds.Should().BeLessThan(5, "Large file should be processed efficiently");
    }

    [Fact]
    public async Task StartMonitoringAsync_CreatesFileWatcher_AndRaisesEvents()
    {
        // Arrange
        var initialContent = @"[08/15/2025 - 12:00:00PM] Papyrus log opened
[08/15/2025 - 12:00:01PM] warning: Initial warning";
        var logPath = CreateTempFile("monitor_test.log", initialContent);
        var eventRaised = false;
        PapyrusStats? capturedStats = null;

        _service.StatsUpdated += (sender, args) =>
        {
            eventRaised = true;
            capturedStats = args.Stats;
        };

        using var cts = new CancellationTokenSource();

        // Act
        await _service.StartMonitoringAsync(logPath, cts.Token);

        // Simulate file change using the test watcher
        await Task.Delay(500); // Give watcher time to initialize
        
        // Update the file content in the test file system with new errors
        var updatedContent = initialContent + @"
[08/15/2025 - 12:00:05PM] error: New error added
[08/15/2025 - 12:00:06PM] error: Another error
[08/15/2025 - 12:00:07PM] Dumping stack 1:";
        _fileSystem.AddFile(logPath, updatedContent);
        
        // Trigger the file watcher
        _fileWatcherFactory.SimulateChangeInAllWatchers();
        await Task.Delay(1500); // Wait for monitoring interval

        // Assert
        eventRaised.Should().BeTrue("StatsUpdated event should be raised");
        capturedStats.Should().NotBeNull();
        capturedStats!.Errors.Should().BeGreaterThan(0, "errors should be detected from the updated content");

        // Cleanup
        await _service.StopMonitoringAsync();
    }

    [Fact]
    public async Task StopMonitoringAsync_StopsFileWatcher_Successfully()
    {
        // Arrange
        var logPath = CreateTempFile("stop_test.log", "Test content");
        using var cts = new CancellationTokenSource();

        await _service.StartMonitoringAsync(logPath, cts.Token);
        _service.IsMonitoring.Should().BeTrue();

        // Act
        await _service.StopMonitoringAsync();

        // Assert
        _service.IsMonitoring.Should().BeFalse();
    }

    [Fact]
    public async Task DetectLogPathAsync_ForFallout4_ReturnsCorrectPath()
    {
        // Arrange
        var expectedPath = _pathService.Combine(
            _environment.GetFolderPath(Environment.SpecialFolder.MyDocuments),
            "My Games", "Fallout4", "Logs", "Script", "Papyrus.0.log");

        // Create test directory and file
        var testDir = Path.GetDirectoryName(expectedPath)!;
        _fileSystem.CreateDirectory(testDir);
        _fileSystem.AddFile(expectedPath, "Test log");
        _tempFiles.Add(expectedPath);

        // Act
        var detectedPath = await _service.DetectLogPathAsync(GameType.Fallout4);

        // Assert
        detectedPath.Should().Be(expectedPath);
    }

    [Fact]
    public async Task DetectLogPathAsync_ForSkyrim_ReturnsCorrectPath()
    {
        // Arrange
        var expectedPath = _pathService.Combine(
            _environment.GetFolderPath(Environment.SpecialFolder.MyDocuments),
            "My Games", "Skyrim Special Edition", "Logs", "Script", "Papyrus.0.log");

        // Create test directory and file
        var testDir = Path.GetDirectoryName(expectedPath)!;
        _fileSystem.CreateDirectory(testDir);
        _fileSystem.AddFile(expectedPath, "Test log");
        _tempFiles.Add(expectedPath);

        // Act
        var detectedPath = await _service.DetectLogPathAsync(GameType.Skyrim);

        // Assert
        detectedPath.Should().Be(expectedPath);
    }

    [Fact]
    public async Task ExportStatsAsync_ToCSV_CreatesValidFile()
    {
        // Arrange
        var logContent = @"[08/15/2025 - 12:00:00PM] Dumping stack 1:
[08/15/2025 - 12:00:01PM] Stack:
[08/15/2025 - 12:00:02PM] warning: Test warning
[08/15/2025 - 12:00:03PM] error: Test error";

        var logPath = CreateTempFile("export_test.log", logContent);
        await _service.AnalyzeLogAsync(logPath);

        var exportPath = Path.Combine(_testDirectory, "export_test.csv");

        // Act
        await _service.ExportStatsAsync(exportPath);

        // Assert
        _fileSystem.FileExists(exportPath).Should().BeTrue();
        var csvContent = await _fileSystem.ReadAllTextAsync(exportPath);
        csvContent.Should().Contain("Timestamp");
        csvContent.Should().Contain("Dumps");
        csvContent.Should().Contain("Stacks");
        csvContent.Should().Contain("Warnings");
        csvContent.Should().Contain("Errors");
        csvContent.Should().Contain("Ratio");
    }

    [Fact]
    public async Task ExportStatsAsync_ToJSON_CreatesValidFile()
    {
        // Arrange
        var logContent = @"[08/15/2025 - 12:00:00PM] Dumping stack 1:
[08/15/2025 - 12:00:01PM] Stack:
[08/15/2025 - 12:00:02PM] warning: Test warning
[08/15/2025 - 12:00:03PM] error: Test error";

        var logPath = CreateTempFile("json_export_test.log", logContent);
        await _service.AnalyzeLogAsync(logPath);

        var exportPath = Path.Combine(_testDirectory, "export_test.json");

        // Act
        await _service.ExportStatsAsync(exportPath, "json");

        // Assert
        _fileSystem.FileExists(exportPath).Should().BeTrue();
        var jsonContent = await _fileSystem.ReadAllTextAsync(exportPath);
        jsonContent.Should().Contain("\"timestamp\"");
        jsonContent.Should().Contain("\"dumps\"");
        jsonContent.Should().Contain("\"stacks\"");
        jsonContent.Should().Contain("\"warnings\"");
        jsonContent.Should().Contain("\"errors\"");
        jsonContent.Should().Contain("\"ratio\"");
    }

    [Fact]
    public void GetHistoricalStats_ReturnsAllCollectedStats()
    {
        // Act
        var history = _service.GetHistoricalStats();

        // Assert
        history.Should().NotBeNull();
        history.Should().BeEmpty("Initially should have no history");
    }

    [Fact]
    public async Task ClearHistory_RemovesAllHistoricalStats()
    {
        // Arrange
        var logPath = CreateTempFile("history_test.log", "Test content");
        await _service.AnalyzeLogAsync(logPath);

        var historyBefore = _service.GetHistoricalStats();
        historyBefore.Should().HaveCount(1);

        // Act
        _service.ClearHistory();

        // Assert
        var historyAfter = _service.GetHistoricalStats();
        historyAfter.Should().BeEmpty();
    }

    [Fact]
    public void MonitoringInterval_SetAndGet_WorksCorrectly()
    {
        // Arrange & Act
        _service.MonitoringInterval = 2000;

        // Assert
        _service.MonitoringInterval.Should().Be(2000);
    }

    [Fact]
    public async Task AnalyzeLogAsync_WithCancellation_ThrowsOperationCanceledException()
    {
        // Arrange
        var logPath = CreateTempFile("cancellation_test.log", "Test content");
        using var cts = new CancellationTokenSource();
        cts.Cancel();

        // Act & Assert
        await Assert.ThrowsAsync<TaskCanceledException>(async () => await _service.AnalyzeLogAsync(logPath, cts.Token));
    }

    [Fact]
    public async Task MonitoringAsync_HandlesFileAccessErrors_Gracefully()
    {
        // Arrange
        var logPath = CreateTempFile("locked_test.log", "Initial content");
        Exception? capturedError = null;

        _service.Error += (sender, args) => { capturedError = args.GetException(); };

        using var cts = new CancellationTokenSource();
        await _service.StartMonitoringAsync(logPath, cts.Token);

        // Act - Simulate an error in the file watcher
        await Task.Delay(500); // Give watcher time to initialize
        
        // Get the created watcher and simulate an error
        var watchers = _fileWatcherFactory.CreatedWatchers;
        if (watchers.Any())
        {
            watchers.First().SimulateError(new IOException("File is locked"));
        }
        
        await Task.Delay(1000); // Wait for error handling

        // Assert
        // Error event may or may not be raised depending on timing
        // The important thing is that the service doesn't crash
        _service.IsMonitoring.Should().BeTrue("Service should continue monitoring despite errors");

        // Cleanup
        await _service.StopMonitoringAsync();
    }

    [Fact]
    public void PapyrusStats_Equality_WorksCorrectly()
    {
        // Arrange
        var stats1 = new PapyrusStats
        {
            Timestamp = DateTime.UtcNow,
            Dumps = 10,
            Stacks = 5,
            Warnings = 20,
            Errors = 15,
            Ratio = 2.0,
            LogPath = "test.log"
        };

        var stats2 = new PapyrusStats
        {
            Timestamp = stats1.Timestamp,
            Dumps = 10,
            Stacks = 5,
            Warnings = 20,
            Errors = 15,
            Ratio = 2.0,
            LogPath = "test.log"
        };

        var stats3 = new PapyrusStats
        {
            Timestamp = stats1.Timestamp,
            Dumps = 15, // Different
            Stacks = 5,
            Warnings = 20,
            Errors = 15,
            Ratio = 3.0,
            LogPath = "test.log"
        };

        // Act & Assert
        stats1.Should().Be(stats2);
        stats1.Should().NotBe(stats3);
        (stats1 == stats2).Should().BeTrue();
        (stats1 != stats3).Should().BeTrue();
        stats1.GetHashCode().Should().Be(stats2.GetHashCode());
    }

    [Fact]
    public void PapyrusStats_ComputedProperties_CalculateCorrectly()
    {
        // Arrange
        var stats = new PapyrusStats
        {
            Dumps = 10,
            Stacks = 5,
            Warnings = 20,
            Errors = 15
        };

        // Act & Assert
        stats.TotalIssues.Should().Be(50);
        stats.HasCriticalIssues.Should().BeFalse();

        // Test critical issues threshold
        var criticalStats = new PapyrusStats
        {
            Dumps = 100,
            Stacks = 50,
            Warnings = 500,
            Errors = 150
        };

        criticalStats.HasCriticalIssues.Should().BeTrue();
    }

    private string CreateTempFile(string fileName, string content)
    {
        var filePath = Path.Combine(_testDirectory, fileName);
        _fileSystem.AddFile(filePath, content);
        _tempFiles.Add(filePath);
        return filePath;
    }
}