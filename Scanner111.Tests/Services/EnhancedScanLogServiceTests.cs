using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using Moq;
using Scanner111.Models;
using Scanner111.Services;
using Xunit;

namespace Scanner111.Tests.Services
{
    public class EnhancedScanLogServiceTests
    {
        private readonly Mock<AppSettings> _mockAppSettings;
        private readonly Mock<WarningDatabase> _mockWarningDatabase;
        private readonly Mock<CrashLogParserService> _mockParserService;
        private readonly Mock<PluginDetectionService> _mockPluginDetectionService;
        private readonly Mock<CrashAnalysisService> _mockCrashAnalysisService; private readonly Mock<IYamlSettingsCacheService> _mockYamlSettingsCache;
        private readonly Mock<CrashStackAnalysis> _mockCrashStackAnalysis;
        private readonly Mock<CrashLogFormattingService> _mockFormattingService;
        private readonly Mock<ModDetectionService> _mockModDetection;
        private readonly Mock<SpecializedSettingsCheckService> _mockSpecializedSettingsCheck;
        private readonly Mock<CrashReportGenerator> _mockReportGenerator;
        private readonly ScanLogService _service;

        public EnhancedScanLogServiceTests()
        {
            _mockAppSettings = new Mock<AppSettings>();
            _mockWarningDatabase = new Mock<WarningDatabase>();
            _mockParserService = new Mock<CrashLogParserService>();
            _mockPluginDetectionService = new Mock<PluginDetectionService>();
            _mockCrashAnalysisService = new Mock<CrashAnalysisService>();
            _mockYamlSettingsCache = new Mock<IYamlSettingsCacheService>();
            _mockCrashStackAnalysis = new Mock<CrashStackAnalysis>();
            _mockFormattingService = new Mock<CrashLogFormattingService>();
            _mockModDetection = new Mock<ModDetectionService>();
            _mockSpecializedSettingsCheck = new Mock<SpecializedSettingsCheckService>();
            _mockReportGenerator = new Mock<CrashReportGenerator>();

            _service = new ScanLogService(
                _mockAppSettings.Object,
                _mockWarningDatabase.Object,
                _mockParserService.Object,
                _mockPluginDetectionService.Object,
                _mockCrashAnalysisService.Object,
                _mockYamlSettingsCache.Object,
                _mockCrashStackAnalysis.Object,
                _mockFormattingService.Object,
                _mockModDetection.Object,
                _mockSpecializedSettingsCheck.Object,
                _mockReportGenerator.Object
            );
        }

        [Fact]
        public async Task ScanLogFileAsync_CallsAllDetectionServices()
        {
            // Arrange
            var testFile = "test.log";
            var parsedLog = new ParsedCrashLog(testFile, new List<string> { "Test content" });

            _mockFormattingService
                .Setup(x => x.ReformatCrashLogsAsync(It.IsAny<IEnumerable<string>>(), It.IsAny<List<string>>()))
                .ReturnsAsync(1);

            _mockParserService
                .Setup(x => x.ParseCrashLogContentAsync(testFile))
                .ReturnsAsync(parsedLog);

            // Act
            var result = await _service.ScanLogFileAsync(testFile);

            // Assert
            _mockPluginDetectionService.Verify(x => x.DetectModIssues(parsedLog, It.IsAny<List<LogIssue>>()), Times.Once);
            _mockCrashAnalysisService.Verify(x => x.AnalyzeCrashLog(parsedLog, It.IsAny<List<LogIssue>>()), Times.Once);
            _mockModDetection.Verify(x => x.DetectSingleMods(parsedLog, It.IsAny<List<LogIssue>>()), Times.Once);
            _mockModDetection.Verify(x => x.DetectModConflicts(parsedLog, It.IsAny<List<LogIssue>>()), Times.Once);
            _mockModDetection.Verify(x => x.DetectImportantMods(parsedLog, It.IsAny<List<LogIssue>>()), Times.Once);
            _mockModDetection.Verify(x => x.CheckPluginLimits(parsedLog, It.IsAny<List<LogIssue>>()), Times.Once);
            _mockSpecializedSettingsCheck.Verify(x => x.CheckAllSettings(parsedLog, It.IsAny<List<LogIssue>>()), Times.Once);
        }

        [Fact]
        public async Task ScanAndGenerateReportAsync_GeneratesReport()
        {
            // Arrange
            var testFile = "test.log";
            var parsedLog = new ParsedCrashLog(testFile, new List<string> { "Test content" });
            var issues = new List<LogIssue>
            {
                new LogIssue
                {
                    Title = "Test Issue",
                    Message = "This is a test issue",
                    Severity = SeverityLevel.Warning
                }
            };
            var reportContent = "Test Report Content";

            _mockFormattingService
                .Setup(x => x.ReformatCrashLogsAsync(It.IsAny<IEnumerable<string>>(), It.IsAny<List<string>>()))
                .ReturnsAsync(1);

            _mockParserService
                .Setup(x => x.ParseCrashLogContentAsync(testFile))
                .ReturnsAsync(parsedLog);

            _mockReportGenerator
                .Setup(x => x.GenerateReport(testFile, It.IsAny<List<LogIssue>>()))
                .Returns(reportContent);

            // Act
            var result = await _service.ScanAndGenerateReportAsync(testFile);

            // Assert
            Assert.Equal(reportContent, result);
            _mockReportGenerator.Verify(x => x.GenerateReport(testFile, It.IsAny<List<LogIssue>>()), Times.Once);
        }

        [Fact]
        public async Task ScanAndGenerateReportAsync_SavesReportIfDirectorySpecified()
        {
            // Arrange
            var testFile = "test.log";
            var reportsDir = "reports";
            var parsedLog = new ParsedCrashLog(testFile, new List<string> { "Test content" });
            var issues = new List<LogIssue>
            {
                new LogIssue
                {
                    Title = "Test Issue",
                    Message = "This is a test issue",
                    Severity = SeverityLevel.Warning
                }
            };
            var reportContent = "Test Report Content";

            _mockFormattingService
                .Setup(x => x.ReformatCrashLogsAsync(It.IsAny<IEnumerable<string>>(), It.IsAny<List<string>>()))
                .ReturnsAsync(1);

            _mockParserService
                .Setup(x => x.ParseCrashLogContentAsync(testFile))
                .ReturnsAsync(parsedLog);

            _mockReportGenerator
                .Setup(x => x.GenerateReport(testFile, It.IsAny<List<LogIssue>>()))
                .Returns(reportContent);

            // Act
            var result = await _service.ScanAndGenerateReportAsync(testFile, reportsDir);

            // Assert
            Assert.Equal(reportContent, result);
            _mockReportGenerator.Verify(x => x.SaveReportToFile(reportContent, testFile, reportsDir), Times.Once);
        }

        [Fact]
        public async Task ProcessCrashLogsWithReportingAsync_ProcessesMultipleLogs()
        {
            // Arrange
            var testFiles = new[] { "file1.log", "file2.log" };
            var reportsDir = "reports";
            var unsolvedDir = "unsolved";

            var parsedLog1 = new ParsedCrashLog(testFiles[0], new List<string> { "Content 1" });
            var parsedLog2 = new ParsedCrashLog(testFiles[1], new List<string> { "Content 2" });

            var issues1 = new List<LogIssue>
            {
                new LogIssue
                {
                    Title = "Critical Issue",
                    Message = "This is a critical issue",
                    Severity = SeverityLevel.Critical
                }
            };

            var issues2 = new List<LogIssue>
            {
                new LogIssue
                {
                    Title = "Info Issue",
                    Message = "This is an info issue",
                    Severity = SeverityLevel.Information
                }
            };

            _mockFormattingService
                .Setup(x => x.ReformatCrashLogsAsync(It.IsAny<IEnumerable<string>>(), It.IsAny<List<string>>()))
                .ReturnsAsync(2);

            _mockParserService
                .Setup(x => x.ParseCrashLogContentAsync(testFiles[0]))
                .ReturnsAsync(parsedLog1);

            _mockParserService
                .Setup(x => x.ParseCrashLogContentAsync(testFiles[1]))
                .ReturnsAsync(parsedLog2);

            _mockPluginDetectionService
                .Setup(x => x.DetectModIssues(parsedLog1, It.IsAny<List<LogIssue>>()))
                .Callback<ParsedCrashLog, List<LogIssue>>((_, list) => list.AddRange(issues1));

            _mockPluginDetectionService
                .Setup(x => x.DetectModIssues(parsedLog2, It.IsAny<List<LogIssue>>()))
                .Callback<ParsedCrashLog, List<LogIssue>>((_, list) => list.AddRange(issues2));

            _mockReportGenerator
                .Setup(x => x.GenerateReport(It.IsAny<string>(), It.IsAny<List<LogIssue>>()))
                .Returns("Report Content");

            // Act
            var result = await _service.ProcessCrashLogsWithReportingAsync(testFiles, reportsDir, unsolvedDir, true);

            // Assert
            Assert.Equal(2, result); // 2 logs processed

            // First log has critical issues, so it shouldn't be moved
            _mockReportGenerator.Verify(x => x.MoveUnsolvedLog(testFiles[0], unsolvedDir), Times.Never);

            // Second log only has info issues, so it should be moved as "unsolved"
            _mockReportGenerator.Verify(x => x.MoveUnsolvedLog(testFiles[1], unsolvedDir), Times.Once);
        }
    }
}
