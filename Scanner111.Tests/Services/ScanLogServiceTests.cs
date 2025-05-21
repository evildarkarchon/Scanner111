using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using Moq;
using Scanner111.Models;
using Scanner111.Services;
using Xunit;

namespace Scanner111.Tests.Services
{
    public class ScanLogServiceTests
    {
        private readonly Mock<CrashLogParserService> _mockParserService;
        private readonly Mock<PluginDetectionService> _mockPluginDetectionService;
        private readonly Mock<CrashAnalysisService> _mockCrashAnalysisService;
        private readonly Mock<YamlSettingsCacheService> _mockYamlSettingsCache;
        private readonly Mock<CrashStackAnalysis> _mockCrashStackAnalysis;
        private readonly Mock<CrashLogFormattingService> _mockFormattingService;
        private readonly AppSettings _appSettings;
        private readonly WarningDatabase _warningDatabase;
        private readonly ScanLogService _service;

        public ScanLogServiceTests()
        {
            _appSettings = new AppSettings
            {
                SimplifyLogs = true,
                SimplifyRemoveStrings = new List<string> { "ntdll.dll", "Steam.dll" }
            }; _mockParserService = new Mock<CrashLogParserService>();
            _mockPluginDetectionService = new Mock<PluginDetectionService>();
            _mockCrashAnalysisService = new Mock<CrashAnalysisService>();
            _mockYamlSettingsCache = new Mock<YamlSettingsCacheService>();
            _mockCrashStackAnalysis = new Mock<CrashStackAnalysis>();
            _mockFormattingService = new Mock<CrashLogFormattingService>();
            _warningDatabase = new WarningDatabase(_mockYamlSettingsCache.Object);

            _service = new ScanLogService(
                _appSettings,
                _warningDatabase,
                _mockParserService.Object,
                _mockPluginDetectionService.Object,
                _mockCrashAnalysisService.Object,
                _mockYamlSettingsCache.Object,
                _mockCrashStackAnalysis.Object,
                _mockFormattingService.Object
            );
        }

        [Fact]
        public async Task PreprocessCrashLogsAsync_CallsFormattingService()
        {
            // Arrange
            var testFiles = new[] { "file1.log", "file2.log" };
            _mockFormattingService
                .Setup(x => x.ReformatCrashLogsAsync(testFiles, _appSettings.SimplifyRemoveStrings))
                .ReturnsAsync(2);

            // Act
            var result = await _service.PreprocessCrashLogsAsync(testFiles);

            // Assert
            Assert.Equal(2, result);
            _mockFormattingService.Verify(
                x => x.ReformatCrashLogsAsync(testFiles, _appSettings.SimplifyRemoveStrings),
                Times.Once
            );
        }

        [Fact]
        public async Task ScanLogFileAsync_FormatsBeforeParsing()
        {
            // Arrange
            var testFile = "test.log";
            _mockFormattingService
                .Setup(x => x.ReformatCrashLogsAsync(It.Is<IEnumerable<string>>(f => f.Contains(testFile)), _appSettings.SimplifyRemoveStrings))
                .ReturnsAsync(1);

            var parsedLog = new ParsedCrashLog(testFile, new List<string> { "Test log content" });
            _mockParserService
                .Setup(x => x.ParseCrashLogContentAsync(testFile))
                .ReturnsAsync(parsedLog);

            // Act
            var result = await _service.ScanLogFileAsync(testFile);

            // Assert
            _mockFormattingService.Verify(
                x => x.ReformatCrashLogsAsync(It.Is<IEnumerable<string>>(f => f.Contains(testFile)), _appSettings.SimplifyRemoveStrings),
                Times.Once
            );
            _mockParserService.Verify(
                x => x.ParseCrashLogContentAsync(testFile),
                Times.Once
            );
        }

        [Fact]
        public async Task ScanMultipleLogFilesAsync_FormatsAllBeforeParsing()
        {
            // Arrange
            var testFiles = new[] { "file1.log", "file2.log" };
            _mockFormattingService
                .Setup(x => x.ReformatCrashLogsAsync(testFiles, _appSettings.SimplifyRemoveStrings))
                .ReturnsAsync(2);

            var parsedLog1 = new ParsedCrashLog(testFiles[0], new List<string> { "Test log content 1" });
            var parsedLog2 = new ParsedCrashLog(testFiles[1], new List<string> { "Test log content 2" });

            _mockParserService
                .Setup(x => x.ParseCrashLogContentAsync(testFiles[0]))
                .ReturnsAsync(parsedLog1);

            _mockParserService
                .Setup(x => x.ParseCrashLogContentAsync(testFiles[1]))
                .ReturnsAsync(parsedLog2);

            // Act
            var result = await _service.ScanMultipleLogFilesAsync(testFiles);

            // Assert
            _mockFormattingService.Verify(
                x => x.ReformatCrashLogsAsync(testFiles, _appSettings.SimplifyRemoveStrings),
                Times.Once
            );
            _mockParserService.Verify(
                x => x.ParseCrashLogContentAsync(testFiles[0]),
                Times.Once
            );
            _mockParserService.Verify(
                x => x.ParseCrashLogContentAsync(testFiles[1]),
                Times.Once
            );
        }
    }
}
