using System;
using System.IO;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;
using Scanner111.Models;
using Scanner111.Services;

namespace Scanner111.Tests.Services
{
    public class PapyrusLogMonitoringServiceTests
    {
        private readonly Mock<IYamlSettingsCacheService> _mockYamlService;
        private readonly AppSettings _appSettings;
        private readonly Mock<ILogger<PapyrusLogMonitoringService>> _mockLogger;
        private readonly IPapyrusLogMonitoringService _service;

        public PapyrusLogMonitoringServiceTests()
        {
            _mockYamlService = new Mock<IYamlSettingsCacheService>();
            _appSettings = new AppSettings();
            _mockLogger = new Mock<ILogger<PapyrusLogMonitoringService>>();

            _service = new PapyrusLogMonitoringService(
                _mockYamlService.Object,
                _appSettings,
                _mockLogger.Object
            );
        }

        [Fact]
        public void GetPapyrusLogPath_ReturnsPathFromYaml()
        {
            // Arrange
            string expectedPath = @"C:\Games\Fallout4\Papyrus.0.log";
            _appSettings.GameName = "Fallout4";
            _mockYamlService.Setup(y => y.GetSetting<string>(YAML.Game_Local, "Game_Info.Docs_File_PapyrusLog", It.IsAny<string?>()))
                .Returns(expectedPath);

            // Act
            var result = _service.GetPapyrusLogPath();

            // Assert
            Assert.Equal(expectedPath, result);
        }

        [Fact]
        public void GetPapyrusLogPath_HandlesVrMode()
        {
            // Arrange
            string expectedPath = @"C:\Games\Fallout4VR\Papyrus.0.log";
            _appSettings.GameName = "Fallout4VR";
            _mockYamlService.Setup(y => y.GetSetting<string>(YAML.Game_Local, "Game_VR_Info.Docs_File_PapyrusLog", It.IsAny<string?>()))
                .Returns(expectedPath);

            // Act
            var result = _service.GetPapyrusLogPath();

            // Assert
            Assert.Equal(expectedPath, result);
        }

        [Fact]
        public void GetPapyrusLogPath_HandlesExceptions()
        {
            // Arrange
            _appSettings.GameName = "Fallout4";
            _mockYamlService.Setup(y => y.GetSetting<string>(YAML.Game_Local, "Game_Info.Docs_File_PapyrusLog", It.IsAny<string?>()))
                .Throws(new Exception("YAML error"));

            // Act
            var result = _service.GetPapyrusLogPath();

            // Assert
            Assert.Null(result);
        }

        [Fact]
        public async Task AnalyzePapyrusLogAsync_FileDoesNotExist_ReturnsEmptyAnalysis()
        {
            // Arrange
            string nonExistentPath = @"C:\NonExistent\Papyrus.0.log";
            _mockYamlService.Setup(y => y.GetSetting<string>(YAML.Game_Local, "Game_Info.Docs_File_PapyrusLog", It.IsAny<string?>()))
                .Returns(nonExistentPath);

            // Act
            var result = await _service.AnalyzePapyrusLogAsync();

            // Assert
            Assert.Equal(nonExistentPath, result.LogFilePath);
            Assert.Equal(0, result.DumpCount);
            Assert.Equal(0, result.StackCount);
            Assert.Equal(0, result.WarningCount);
            Assert.Equal(0, result.ErrorCount);
        }

        [Fact]
        public async Task AnalyzePapyrusLogAsync_ValidFile_CountsCorrectly()
        {
            // Arrange - Create a temp file with known content
            string tempFile = Path.GetTempFileName();
            try
            {
                // Create test log content
                var content = new StringBuilder();
                content.AppendLine("Some log line");
                content.AppendLine("[11/22/2020 - 10:10:11] Dumping Stacks"); // Should count as dump
                content.AppendLine("[11/22/2020 - 10:10:12] Dumping Stack of something"); // Should count as stack
                content.AppendLine("[11/22/2020 - 10:10:13] warning: Something is wrong"); // Should count as warning
                content.AppendLine("[11/22/2020 - 10:10:14] error: Critical issue"); // Should count as error
                content.AppendLine("[11/22/2020 - 10:10:15] Dumping Stacks again"); // Another dump
                File.WriteAllText(tempFile, content.ToString());

                _mockYamlService.Setup(y => y.GetSetting<string>(YAML.Game_Local, "Game_Info.Docs_File_PapyrusLog", It.IsAny<string?>()))
                    .Returns(tempFile);

                // Act
                var result = await _service.AnalyzePapyrusLogAsync();

                // Assert
                Assert.Equal(2, result.DumpCount); // 2 dumps
                Assert.Equal(1, result.StackCount); // 1 stack
                Assert.Equal(1, result.WarningCount); // 1 warning
                Assert.Equal(1, result.ErrorCount); // 1 error
            }
            finally
            {
                // Clean up
                if (File.Exists(tempFile))
                    File.Delete(tempFile);
            }
        }

        [Fact]
        public async Task StartMonitoringAsync_NonExistentFile_ProvidesEmptyAnalysis()
        {
            // Arrange
            string nonExistentPath = @"C:\NonExistent\Papyrus.0.log";
            _mockYamlService.Setup(y => y.GetSetting<string>(YAML.Game_Local, "Game_Info.Docs_File_PapyrusLog", It.IsAny<string?>()))
                .Returns(nonExistentPath);

            PapyrusLogAnalysis capturedAnalysis = null;
            Action<PapyrusLogAnalysis> callback = analysis => capturedAnalysis = analysis;
            var cts = new CancellationTokenSource();

            // Act
            await _service.StartMonitoringAsync(callback, cts.Token);

            // Assert
            Assert.NotNull(capturedAnalysis);
            Assert.Equal(nonExistentPath, capturedAnalysis.LogFilePath);
            Assert.Equal(0, capturedAnalysis.DumpCount);

            // Clean up
            _service.StopMonitoring();
            cts.Dispose();
        }

        // Note: Testing file system watcher functionality would require additional mocking
        // of the FileSystemWatcher, which is challenging as it's a sealed class.
        // Consider using a file system abstraction library for more comprehensive testing.
    }
}