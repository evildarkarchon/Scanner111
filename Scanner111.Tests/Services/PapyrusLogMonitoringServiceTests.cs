using Microsoft.Extensions.Logging;
using Moq;
using Scanner111.Models;
using Scanner111.Services;

namespace Scanner111.Tests.Services;

public class PapyrusLogMonitoringServiceTests
{
    private readonly AppSettings _appSettings;
    private readonly Mock<ILogger<PapyrusLogMonitoringService>> _mockLogger;
    private readonly Mock<IYamlSettingsCacheService> _mockYamlSettingsCache;
    private readonly PapyrusLogMonitoringService _service;

    public PapyrusLogMonitoringServiceTests()
    {
        _mockYamlSettingsCache = new Mock<IYamlSettingsCacheService>();
        _mockLogger = new Mock<ILogger<PapyrusLogMonitoringService>>();
        _appSettings = new AppSettings { GameName = "TestGame" };

        _service = new PapyrusLogMonitoringService(
            _mockYamlSettingsCache.Object,
            _appSettings,
            _mockLogger.Object);
    }

    [Fact]
    public async Task AnalyzePapyrusLogAsync_ShouldReturnEmptyAnalysis_WhenLogPathNotFound()
    {
        // Arrange
        _mockYamlSettingsCache.Setup(m => m.GetSetting<string>(Yaml.GameLocal, "Game_Info.Docs_File_PapyrusLog"))
            .Returns((string)null);

        // Act
        var result = await _service.AnalyzePapyrusLogAsync();

        // Assert
        Assert.NotNull(result);
        Assert.Null(result.LogFilePath);
        Assert.Equal(0, result.ErrorCount);
        Assert.Equal(0, result.WarningCount);
        Assert.Equal(0, result.StackCount);
        Assert.Equal(0, result.DumpCount);
    }

    [Fact]
    public async Task AnalyzePapyrusLogAsync_ShouldReportProgress()
    {
        // Arrange
        var progressReports = new List<ScanProgress>();
        var progress = new Progress<ScanProgress>(p => progressReports.Add(p));

        _mockYamlSettingsCache.Setup(m => m.GetSetting<string>(Yaml.GameLocal, "Game_Info.Docs_File_PapyrusLog"))
            .Returns((string)null);

        // Act
        await _service.AnalyzePapyrusLogAsync(progress);

        // Assert
        Assert.Contains(progressReports, p => p.PercentComplete == 0);
        Assert.Contains(progressReports, p => p.PercentComplete == 10);
        Assert.Contains(progressReports, p => p.CurrentOperation.Contains("Starting"));
    }

    [Fact]
    public async Task AnalyzePapyrusLogAsync_ShouldRespectCancellation()
    {
        // Arrange
        var cts = new CancellationTokenSource();
        cts.Cancel();

        // Act & Assert
        await Assert.ThrowsAsync<OperationCanceledException>(() =>
            _service.AnalyzePapyrusLogAsync(cancellationToken: cts.Token));
    }

    [Fact]
    public async Task StartMonitoringAsync_ShouldCallCallback_WithInitialAnalysis()
    {
        // Arrange
        PapyrusLogAnalysis? callbackResult = null;
        Action<PapyrusLogAnalysis> callback = analysis => callbackResult = analysis;

        _mockYamlSettingsCache.Setup(m => m.GetSetting<string>(Yaml.GameLocal, "Game_Info.Docs_File_PapyrusLog"))
            .Returns((string)null);

        // Act
        await _service.StartMonitoringAsync(callback, CancellationToken.None);

        // Assert
        Assert.NotNull(callbackResult);
    }

    [Fact]
    public void StopMonitoring_ShouldCleanupResources()
    {
        // Arrange - Start monitoring first
        PapyrusLogAnalysis? callbackResult = null;
        Action<PapyrusLogAnalysis> callback = analysis => callbackResult = analysis;

        _mockYamlSettingsCache.Setup(m => m.GetSetting<string>(Yaml.GameLocal, "Game_Info.Docs_File_PapyrusLog"))
            .Returns((string)null);

        _service.StartMonitoringAsync(callback, CancellationToken.None).Wait();

        // Act
        _service.StopMonitoring();

        // Assert - No direct way to verify, but we can check that the service doesn't throw
        // when we call StopMonitoring multiple times
        _service.StopMonitoring();
    }

    [Fact]
    public void GetPapyrusLogPath_ShouldReturnCorrectPath()
    {
        // Arrange
        var expectedPath = "C:\\TestPath\\Papyrus.log";
        _mockYamlSettingsCache.Setup(m => m.GetSetting<string>(Yaml.GameLocal, "Game_Info.Docs_File_PapyrusLog"))
            .Returns(expectedPath);

        // Act
        var result = _service.GetPapyrusLogPath();

        // Assert
        Assert.Equal(expectedPath, result);
    }

    [Fact]
    public void GetPapyrusLogPath_ShouldHandleVrMode()
    {
        // Arrange
        var expectedPath = "C:\\TestPath\\PapyrusVR.log";
        _appSettings.GameName = "TestGame VR";

        _mockYamlSettingsCache.Setup(m => m.GetSetting<string>(Yaml.GameLocal, "Game_VR_Info.Docs_File_PapyrusLog"))
            .Returns(expectedPath);

        // Act
        var result = _service.GetPapyrusLogPath();

        // Assert
        Assert.Equal(expectedPath, result);
    }
}