using Moq;
using Scanner111.Models;
using Scanner111.Services;

namespace Scanner111.Tests.Services;

public class LogErrorCheckServiceTests
{
    private readonly Mock<IYamlSettingsCacheService> _mockYamlSettingsCache;
    private readonly LogErrorCheckService _service;
    private readonly string _tempPath;

    public LogErrorCheckServiceTests()
    {
        _mockYamlSettingsCache = new Mock<IYamlSettingsCacheService>();
        _service = new LogErrorCheckService(_mockYamlSettingsCache.Object);
        _tempPath = Path.GetTempPath();
    }

    [Fact]
    public async Task ScanGameLogsAsync_WithMissingGameDir_ReturnsError()
    {
        // Arrange
        string nullString = null;
        _mockYamlSettingsCache
            .Setup(x => x.GetSetting<string>(Yaml.Game, "game_dir", It.IsAny<string>()))
            .Returns(nullString);

        // Act
        var result = await _service.ScanGameLogsAsync();

        // Assert
        Assert.Equal("❌ ERROR : Game directory not configured in settings", result);
    }

    [Fact]
    public async Task ScanGameLogsAsync_WithNonExistentLogsDir_ReturnsError()
    {
        // Arrange
        var nonExistentPath = Path.Combine(_tempPath, Guid.NewGuid().ToString());
        _mockYamlSettingsCache
            .Setup(x => x.GetSetting<string>(Yaml.Game, "game_dir"))
            .Returns(nonExistentPath);

        _mockYamlSettingsCache
            .Setup(x => x.GetSetting<string>(Yaml.Game, "logs_path"))
            .Returns("logs");

        // Act
        var result = await _service.ScanGameLogsAsync();

        // Assert
        Assert.Contains("❌ ERROR : Logs directory not found at", result);
    }

    [Fact]
    public async Task ScanGameLogsAsync_WithValidLogsDir_CallsCheckLogErrors()
    {
        // Arrange
        var gameDir = Path.Combine(_tempPath, Guid.NewGuid().ToString());
        var logsDir = Path.Combine(gameDir, "logs");

        try
        {
            // Create test directories
            Directory.CreateDirectory(logsDir);

            _mockYamlSettingsCache
                .Setup(x => x.GetSetting<string>(Yaml.Game, "game_dir"))
                .Returns(gameDir);

            _mockYamlSettingsCache
                .Setup(x => x.GetSetting<string>(Yaml.Game, "logs_path"))
                .Returns("logs");

            _mockYamlSettingsCache
                .Setup(x => x.GetSetting<List<string>>(Yaml.Main, "catch_log_errors"))
                .Returns(new List<string> { "error", "exception" });

            _mockYamlSettingsCache
                .Setup(x => x.GetSetting<List<string>>(Yaml.Main, "exclude_log_files"))
                .Returns(new List<string> { "exclude" });

            _mockYamlSettingsCache
                .Setup(x => x.GetSetting<List<string>>(Yaml.Main, "exclude_log_errors"))
                .Returns(new List<string> { "ignore" });

            // Act
            var result = await _service.ScanGameLogsAsync();

            // Assert
            Assert.Contains("================== GAME LOGS ANALYSIS ==================", result);
        }
        finally
        {
            // Cleanup
            if (Directory.Exists(gameDir)) Directory.Delete(gameDir, true);
        }
    }

    [Fact]
    public async Task CheckLogErrorsAsync_WithNullFolderPath_ReturnsEmptyString()
    {
        // Act
        var result = await _service.CheckLogErrorsAsync(null);

        // Assert
        Assert.Equal(string.Empty, result);
    }

    [Fact]
    public async Task CheckLogErrorsAsync_WithNonExistentFolderPath_ReturnsEmptyString()
    {
        // Arrange
        var nonExistentPath = Path.Combine(_tempPath, Guid.NewGuid().ToString());
        var directoryInfo = new DirectoryInfo(nonExistentPath);

        // Act
        var result = await _service.CheckLogErrorsAsync(directoryInfo);

        // Assert
        Assert.Equal(string.Empty, result);
    }

    [Fact]
    public async Task CheckLogErrorsAsync_WithNoLogFiles_ReturnsEmptyReport()
    {
        // Arrange
        var testDir = Path.Combine(_tempPath, Guid.NewGuid().ToString());

        try
        {
            // Create test directory
            Directory.CreateDirectory(testDir);
            var directoryInfo = new DirectoryInfo(testDir);

            _mockYamlSettingsCache
                .Setup(x => x.GetSetting<List<string>>(Yaml.Main, "catch_log_errors"))
                .Returns(new List<string> { "error", "exception" });

            _mockYamlSettingsCache
                .Setup(x => x.GetSetting<List<string>>(Yaml.Main, "exclude_log_files"))
                .Returns(new List<string> { "exclude" });

            _mockYamlSettingsCache
                .Setup(x => x.GetSetting<List<string>>(Yaml.Main, "exclude_log_errors"))
                .Returns(new List<string> { "ignore" });

            // Act
            var result = await _service.CheckLogErrorsAsync(directoryInfo);

            // Assert
            Assert.Equal(string.Empty, result);
        }
        finally
        {
            // Cleanup
            if (Directory.Exists(testDir)) Directory.Delete(testDir, true);
        }
    }

    [Fact]
    public async Task CheckLogErrorsAsync_WithLogFilesContainingErrors_ReportsErrors()
    {
        // Arrange
        var testDir = Path.Combine(_tempPath, Guid.NewGuid().ToString());
        var logFilePath = Path.Combine(testDir, "test.log");

        try
        {
            // Create test directory and log file
            Directory.CreateDirectory(testDir);
            await File.WriteAllTextAsync(logFilePath,
                "Normal log line\n" +
                "This line has an error in it\n" +
                "Another normal line\n" +
                "This line has an exception but should be ignored\n");

            var directoryInfo = new DirectoryInfo(testDir);

            _mockYamlSettingsCache
                .Setup(x => x.GetSetting<List<string>>(Yaml.Main, "catch_log_errors"))
                .Returns(new List<string> { "error", "exception" });

            _mockYamlSettingsCache
                .Setup(x => x.GetSetting<List<string>>(Yaml.Main, "exclude_log_files"))
                .Returns(new List<string> { "exclude" });

            _mockYamlSettingsCache
                .Setup(x => x.GetSetting<List<string>>(Yaml.Main, "exclude_log_errors"))
                .Returns(new List<string> { "ignore" });

            // Act
            var result = await _service.CheckLogErrorsAsync(directoryInfo);

            // Assert
            Assert.Contains("[!] CAUTION : THE FOLLOWING LOG FILE REPORTS ONE OR MORE ERRORS!", result);
            Assert.Contains("This line has an error in it", result);
            Assert.DoesNotContain("Normal log line", result);
            Assert.Contains("* TOTAL NUMBER OF DETECTED LOG ERRORS * :", result);
        }
        finally
        {
            // Cleanup
            if (Directory.Exists(testDir)) Directory.Delete(testDir, true);
        }
    }

    [Fact]
    public async Task CheckLogErrorsAsync_WithExcludedLogFile_SkipsFile()
    {
        // Arrange
        var testDir = Path.Combine(_tempPath, Guid.NewGuid().ToString());
        var logFilePath = Path.Combine(testDir, "exclude-test.log");

        try
        {
            // Create test directory and log file
            Directory.CreateDirectory(testDir);
            await File.WriteAllTextAsync(logFilePath, "This line has an error in it\n");

            var directoryInfo = new DirectoryInfo(testDir);

            _mockYamlSettingsCache
                .Setup(x => x.GetSetting<List<string>>(Yaml.Main, "catch_log_errors"))
                .Returns(new List<string> { "error" });

            _mockYamlSettingsCache
                .Setup(x => x.GetSetting<List<string>>(Yaml.Main, "exclude_log_files"))
                .Returns(new List<string> { "exclude" });

            _mockYamlSettingsCache
                .Setup(x => x.GetSetting<List<string>>(Yaml.Main, "exclude_log_errors"))
                .Returns(new List<string>());

            // Act
            var result = await _service.CheckLogErrorsAsync(directoryInfo);

            // Assert
            Assert.Equal(string.Empty, result);
        }
        finally
        {
            // Cleanup
            if (Directory.Exists(testDir)) Directory.Delete(testDir, true);
        }
    }

    [Fact]
    public async Task CheckLogErrorsAsync_WithIgnoredErrorPattern_SkipsLine()
    {
        // Arrange
        var testDir = Path.Combine(_tempPath, Guid.NewGuid().ToString());
        var logFilePath = Path.Combine(testDir, "test.log");

        try
        {
            // Create test directory and log file
            Directory.CreateDirectory(testDir);
            await File.WriteAllTextAsync(logFilePath,
                "This line has an error in it\n" +
                "This line has an error to ignore\n");

            var directoryInfo = new DirectoryInfo(testDir);

            _mockYamlSettingsCache
                .Setup(x => x.GetSetting<List<string>>(Yaml.Main, "catch_log_errors"))
                .Returns(new List<string> { "error" });

            _mockYamlSettingsCache
                .Setup(x => x.GetSetting<List<string>>(Yaml.Main, "exclude_log_files"))
                .Returns(new List<string>());

            _mockYamlSettingsCache
                .Setup(x => x.GetSetting<List<string>>(Yaml.Main, "exclude_log_errors"))
                .Returns(new List<string> { "ignore" });

            // Act
            var result = await _service.CheckLogErrorsAsync(directoryInfo);

            // Assert
            Assert.Contains("This line has an error in it", result);
            Assert.DoesNotContain("This line has an error to ignore", result);
        }
        finally
        {
            // Cleanup
            if (Directory.Exists(testDir)) Directory.Delete(testDir, true);
        }
    }

    [Fact]
    public async Task CheckLogErrorsAsync_WithCrashLogFile_SkipsFile()
    {
        // Arrange
        var testDir = Path.Combine(_tempPath, Guid.NewGuid().ToString());
        var logFilePath = Path.Combine(testDir, "crash-123456.log");

        try
        {
            // Create test directory and log file
            Directory.CreateDirectory(testDir);
            await File.WriteAllTextAsync(logFilePath, "This line has an error in it\n");

            var directoryInfo = new DirectoryInfo(testDir);

            _mockYamlSettingsCache
                .Setup(x => x.GetSetting<List<string>>(Yaml.Main, "catch_log_errors"))
                .Returns(new List<string> { "error" });

            _mockYamlSettingsCache
                .Setup(x => x.GetSetting<List<string>>(Yaml.Main, "exclude_log_files"))
                .Returns(new List<string>());

            _mockYamlSettingsCache
                .Setup(x => x.GetSetting<List<string>>(Yaml.Main, "exclude_log_errors"))
                .Returns(new List<string>());

            // Act
            var result = await _service.CheckLogErrorsAsync(directoryInfo);

            // Assert
            Assert.Equal(string.Empty, result);
        }
        finally
        {
            // Cleanup
            if (Directory.Exists(testDir)) Directory.Delete(testDir, true);
        }
    }
}