using Microsoft.Extensions.Logging;
using Moq;
using Scanner111.Models;
using Scanner111.Services;

namespace Scanner111.Tests.Services;

public class ScanGameServiceTests
{
    private readonly Mock<IGameFileManagementService> _mockGameFileManagementService;
    private readonly Mock<ILogger<ScanGameService>> _mockLogger;
    private readonly Mock<IModScanningService> _mockModScanningService;
    private readonly ScanGameService _scanGameService;

    public ScanGameServiceTests()
    {
        _mockGameFileManagementService = new Mock<IGameFileManagementService>();
        _mockModScanningService = new Mock<IModScanningService>();
        _mockLogger = new Mock<ILogger<ScanGameService>>();

        _scanGameService = new ScanGameService(
            _mockGameFileManagementService.Object,
            _mockModScanningService.Object,
            _mockLogger.Object);
    }

    [Fact]
    public async Task PerformCompleteScanAsync_ShouldCombineResults()
    {
        // Arrange
        _mockGameFileManagementService.Setup(m => m.GetGameCombinedResultAsync(
                It.IsAny<IProgress<ScanProgress>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync("Game Results");

        _mockModScanningService.Setup(m => m.GetModsCombinedResultAsync(
                It.IsAny<IProgress<ScanProgress>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync("Mod Results");

        // Act
        var result = await _scanGameService.PerformCompleteScanAsync();

        // Assert
        Assert.Equal("Game ResultsMod Results", result);
    }

    [Fact]
    public async Task PerformCompleteScanAsync_ShouldReportProgress()
    {
        // Arrange
        var progressReports = new List<ScanProgress>();
        var progress = new Progress<ScanProgress>(p => progressReports.Add(p));

        _mockGameFileManagementService.Setup(m => m.GetGameCombinedResultAsync(
                It.IsAny<IProgress<ScanProgress>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync("Game Results");

        _mockModScanningService.Setup(m => m.GetModsCombinedResultAsync(
                It.IsAny<IProgress<ScanProgress>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync("Mod Results");

        // Act
        await _scanGameService.PerformCompleteScanAsync(progress);

        // Assert
        Assert.Contains(progressReports, p => p.PercentComplete == 0);
        Assert.Contains(progressReports, p => p.PercentComplete == 10);
        Assert.Contains(progressReports, p => p.PercentComplete == 50);
        Assert.Contains(progressReports, p => p.PercentComplete == 100);
    }

    [Fact]
    public async Task PerformCompleteScanAsync_ShouldRespectCancellation()
    {
        // Arrange
        var cts = new CancellationTokenSource();
        cts.Cancel();

        // Act & Assert
        await Assert.ThrowsAsync<OperationCanceledException>(() =>
            _scanGameService.PerformCompleteScanAsync(cancellationToken: cts.Token));
    }

    [Fact]
    public async Task ScanGameFilesOnlyAsync_ShouldReturnGameResults()
    {
        // Arrange
        _mockGameFileManagementService.Setup(m => m.GetGameCombinedResultAsync(
                It.IsAny<IProgress<ScanProgress>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync("Game Results");

        // Act
        var result = await _scanGameService.ScanGameFilesOnlyAsync();

        // Assert
        Assert.Equal("Game Results", result);
    }

    [Fact]
    public async Task ScanModsOnlyAsync_ShouldReturnModResults()
    {
        // Arrange
        _mockModScanningService.Setup(m => m.GetModsCombinedResultAsync(
                It.IsAny<IProgress<ScanProgress>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync("Mod Results");

        // Act
        var result = await _scanGameService.ScanModsOnlyAsync();

        // Assert
        Assert.Equal("Mod Results", result);
    }

    [Fact]
    public async Task ManageGameFilesAsync_ShouldCallGameFileManagementService()
    {
        // Arrange
        var classicList = "TestList";
        var mode = "BACKUP";

        // Act
        await _scanGameService.ManageGameFilesAsync(classicList, mode);

        // Assert
        _mockGameFileManagementService.Verify(m => m.GameFilesManageAsync(
            classicList, mode, It.IsAny<IProgress<ScanProgress>>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task WriteReportAsync_ShouldCallGameFileManagementService()
    {
        // Act
        await _scanGameService.WriteReportAsync();

        // Assert
        _mockGameFileManagementService.Verify(m => m.WriteCombinedResultsAsync(
            It.IsAny<IProgress<ScanProgress>>(), It.IsAny<CancellationToken>()), Times.Once);
    }
}