using System;
using System.IO;
using System.Threading.Tasks;
using Moq;
using Scanner111.Services;
using Xunit;

namespace Scanner111.Tests.Services
{
    public class ScanGameServiceTests
    {
        private readonly Mock<IGameFileManagementService> _mockGameFileManagementService;
        private readonly Mock<IModScanningService> _mockModScanningService;
        private readonly ScanGameService _scanGameService;

        public ScanGameServiceTests()
        {
            _mockGameFileManagementService = new Mock<IGameFileManagementService>();
            _mockModScanningService = new Mock<IModScanningService>();
            _scanGameService = new ScanGameService(_mockGameFileManagementService.Object, _mockModScanningService.Object);
        }

        [Fact]
        public async Task PerformCompleteScanAsync_ShouldCombineResults()
        {
            // Arrange
            var gameResult = "Game scan results";
            var modResult = "Mod scan results";
            _mockGameFileManagementService.Setup(x => x.GetGameCombinedResultAsync()).ReturnsAsync(gameResult);
            _mockModScanningService.Setup(x => x.GetModsCombinedResultAsync()).ReturnsAsync(modResult);

            // Act
            var result = await _scanGameService.PerformCompleteScanAsync();

            // Assert
            Assert.Equal(gameResult + modResult, result);
            _mockGameFileManagementService.Verify(x => x.GetGameCombinedResultAsync(), Times.Once);
            _mockModScanningService.Verify(x => x.GetModsCombinedResultAsync(), Times.Once);
        }

        [Fact]
        public async Task ScanGameFilesOnlyAsync_ShouldReturnGameResults()
        {
            // Arrange
            var gameResult = "Game scan results";
            _mockGameFileManagementService.Setup(x => x.GetGameCombinedResultAsync()).ReturnsAsync(gameResult);

            // Act
            var result = await _scanGameService.ScanGameFilesOnlyAsync();

            // Assert
            Assert.Equal(gameResult, result);
            _mockGameFileManagementService.Verify(x => x.GetGameCombinedResultAsync(), Times.Once);
            _mockModScanningService.Verify(x => x.GetModsCombinedResultAsync(), Times.Never);
        }

        [Fact]
        public async Task ScanModsOnlyAsync_ShouldReturnModResults()
        {
            // Arrange
            var modResult = "Mod scan results";
            _mockModScanningService.Setup(x => x.GetModsCombinedResultAsync()).ReturnsAsync(modResult);

            // Act
            var result = await _scanGameService.ScanModsOnlyAsync();

            // Assert
            Assert.Equal(modResult, result);
            _mockGameFileManagementService.Verify(x => x.GetGameCombinedResultAsync(), Times.Never);
            _mockModScanningService.Verify(x => x.GetModsCombinedResultAsync(), Times.Once);
        }

        [Fact]
        public async Task ManageGameFilesAsync_ShouldCallGameFileManagementService()
        {
            // Arrange
            var classicList = "test_list";
            var mode = "BACKUP";

            // Act
            await _scanGameService.ManageGameFilesAsync(classicList, mode);

            // Assert
            _mockGameFileManagementService.Verify(x => x.GameFilesManageAsync(classicList, mode), Times.Once);
        }

        [Fact]
        public async Task WriteReportAsync_ShouldCallGameFileManagementService()
        {
            // Act
            await _scanGameService.WriteReportAsync();

            // Assert
            _mockGameFileManagementService.Verify(x => x.WriteCombinedResultsAsync(), Times.Once);
        }
    }
}
