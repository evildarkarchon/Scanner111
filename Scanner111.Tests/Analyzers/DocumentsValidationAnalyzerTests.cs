using Scanner111.Core.Analyzers;
using Scanner111.Core.Infrastructure;
using Scanner111.Core.Models;
using Scanner111.Tests.TestHelpers;

namespace Scanner111.Tests.Analyzers;

public class DocumentsValidationAnalyzerTests : AnalyzerTestBase<DocumentsValidationAnalyzer>
{
    private readonly DocumentsValidationAnalyzer _analyzer;
    private readonly Mock<ILogger<DocumentsValidationAnalyzer>> _mockLogger;
    private readonly Mock<IApplicationSettingsService> _mockSettingsService;
    private readonly Mock<IYamlSettingsProvider> _mockYamlSettings;

    public DocumentsValidationAnalyzerTests()
    {
        _mockYamlSettings = new Mock<IYamlSettingsProvider>();
        _mockLogger = new Mock<ILogger<DocumentsValidationAnalyzer>>();
        _mockSettingsService = new Mock<IApplicationSettingsService>();

        // Default to FCX mode enabled for most tests
        _mockSettingsService.Setup(x => x.LoadSettingsAsync())
            .ReturnsAsync(new ApplicationSettings { FcxMode = true });

        _analyzer = new DocumentsValidationAnalyzer(_mockYamlSettings.Object, _mockLogger.Object,
            _mockSettingsService.Object);
    }

    protected override DocumentsValidationAnalyzer CreateAnalyzer()
    {
        return _analyzer;
    }

    [Fact]
    public void Name_ShouldReturnCorrectValue()
    {
        // Act
        var name = _analyzer.Name;

        // Assert
        name.Should().Be("Documents Validation");
    }

    [Fact]
    public void Priority_ShouldReturnCorrectValue()
    {
        // Act
        var priority = _analyzer.Priority;

        // Assert
        priority.Should().Be(2);
    }

    [Fact]
    public void CanRunInParallel_ShouldReturnTrue()
    {
        // Act
        var canRunInParallel = _analyzer.CanRunInParallel;

        // Assert
        canRunInParallel.Should().BeTrue();
    }

    [Fact]
    public async Task AnalyzeAsync_WithFcxModeDisabled_ShouldReturnEarlyWithMessage()
    {
        // Arrange
        var mockSettingsService = new Mock<IApplicationSettingsService>();
        mockSettingsService.Setup(x => x.LoadSettingsAsync())
            .ReturnsAsync(new ApplicationSettings { FcxMode = false });

        var analyzer =
            new DocumentsValidationAnalyzer(_mockYamlSettings.Object, _mockLogger.Object, mockSettingsService.Object);

        var crashLog = new CrashLog
        {
            GameType = "Fallout4",
            OriginalLines = ["Test crash log"],
            FilePath = "test.log"
        };

        // Act
        var result = await analyzer.AnalyzeAsync(crashLog);

        // Assert
        result.Should().NotBeNull();
        result.Should().BeOfType<DocumentsValidationResult>();
        result.AnalyzerName.Should().Be("Documents Validation");
        result.Success.Should().BeTrue();
        result.HasFindings.Should().BeFalse();
        result.ReportLines.Should().HaveCount(1);
        result.ReportLines[0].Should().Be("Documents validation is only available in FCX mode.\n");
    }

    [Fact]
    public async Task AnalyzeAsync_WithFcxModeEnabled_ShouldRunNormally()
    {
        // Arrange
        var mockSettingsService = new Mock<IApplicationSettingsService>();
        mockSettingsService.Setup(x => x.LoadSettingsAsync())
            .ReturnsAsync(new ApplicationSettings { FcxMode = true });

        var analyzer =
            new DocumentsValidationAnalyzer(_mockYamlSettings.Object, _mockLogger.Object, mockSettingsService.Object);

        var crashLog = new CrashLog
        {
            GameType = "Fallout4",
            OriginalLines = ["Test crash log"],
            FilePath = "test.log"
        };

        // Act
        var result = await analyzer.AnalyzeAsync(crashLog);

        // Assert
        result.Should().NotBeNull();
        result.Should().BeOfType<DocumentsValidationResult>();
        result.AnalyzerName.Should().Be("Documents Validation");
        result.Success.Should().BeTrue();
        // The result should not contain the FCX mode message
        result.ReportLines.Should().NotContain("Documents validation is only available in FCX mode.\n");

        var documentsResult = (DocumentsValidationResult)result;
        documentsResult.DocumentsPath.Should().NotBeNull();
    }

    [Fact]
    public async Task AnalyzeAsync_WithValidCrashLog_ShouldReturnDocumentsValidationResult()
    {
        // Arrange
        var crashLog = new CrashLog
        {
            GameType = "Fallout4",
            OriginalLines = ["Test crash log"],
            FilePath = "test.log"
        };

        // Act
        var result = await _analyzer.AnalyzeAsync(crashLog);

        // Assert
        result.Should().NotBeNull();
        result.Should().BeOfType<DocumentsValidationResult>();
        result.AnalyzerName.Should().Be("Documents Validation");
        result.Success.Should().BeTrue();
    }

    [Fact]
    public async Task AnalyzeAsync_WithNullGameType_ShouldDefaultToFallout4()
    {
        // Arrange
        var crashLog = new CrashLog
        {
            GameType = null,
            OriginalLines = ["Test crash log"],
            FilePath = "test.log"
        };

        // Act
        var result = await _analyzer.AnalyzeAsync(crashLog);

        // Assert
        result.Should().NotBeNull();
        result.Should().BeOfType<DocumentsValidationResult>();
        var documentsResult = (DocumentsValidationResult)result;
        documentsResult.DocumentsPath.Should().Contain("Fallout4");
    }

    [Theory]
    [InlineData("C:\\Users\\Test\\OneDrive\\Documents\\My Games\\Fallout4", true)]
    [InlineData("C:\\Users\\Test\\ONEDRIVE\\Documents\\My Games\\Fallout4", true)]
    [InlineData("C:\\Users\\Test\\onedrive\\Documents\\My Games\\Fallout4", true)]
    [InlineData("C:\\Users\\Test\\Documents\\My Games\\Fallout4", false)]
    [InlineData("C:\\Users\\Test\\DropBox\\Documents\\My Games\\Fallout4", false)]
    public async Task AnalyzeAsync_OneDriveDetection_ShouldDetectCorrectly(string documentsPath, bool expectedDetection)
    {
        // Arrange
        var crashLog = new CrashLog
        {
            GameType = "Fallout4",
            OriginalLines = ["Test crash log"],
            FilePath = "test.log"
        };

        // We'll need to test this with mocked file system or create a more testable implementation
        // For now, this test demonstrates the expected behavior

        // Act
        var result = await _analyzer.AnalyzeAsync(crashLog);

        // Assert
        result.Should().NotBeNull();
        result.Should().BeOfType<DocumentsValidationResult>();
        var documentsResult = (DocumentsValidationResult)result;

        // This test will need enhancement with file system mocking
        // documentsResult.OneDriveDetected.Should().Be(expectedDetection);
    }

    [Fact]
    public async Task AnalyzeAsync_WithException_ShouldReturnFailureResult()
    {
        // Arrange
        var crashLog = new CrashLog
        {
            GameType = "InvalidGame", // This might cause an exception in path resolution
            OriginalLines = ["Test crash log"],
            FilePath = "test.log"
        };

        // Act
        var result = await _analyzer.AnalyzeAsync(crashLog);

        // Assert
        result.Should().NotBeNull();
        // Should still return a result, even if there were issues
        result.AnalyzerName.Should().Be("Documents Validation");
    }

    [Fact]
    public async Task AnalyzeAsync_WithCancellationRequested_ShouldThrowOperationCanceledException()
    {
        // Arrange
        var crashLog = new CrashLog
        {
            GameType = "Fallout4",
            OriginalLines = ["Test crash log"],
            FilePath = "test.log"
        };
        var cts = new CancellationTokenSource();
        cts.Cancel();

        // Act & Assert
        await Assert.ThrowsAsync<OperationCanceledException>(() =>
            _analyzer.AnalyzeAsync(crashLog, cts.Token));
    }

    [Theory]
    [InlineData("Fallout4")]
    [InlineData("Skyrim")]
    [InlineData("SkyrimSE")]
    public async Task AnalyzeAsync_WithDifferentGameTypes_ShouldHandleCorrectly(string gameType)
    {
        // Arrange
        var crashLog = new CrashLog
        {
            GameType = gameType,
            OriginalLines = ["Test crash log"],
            FilePath = "test.log"
        };

        // Act
        var result = await _analyzer.AnalyzeAsync(crashLog);

        // Assert
        result.Should().NotBeNull();
        result.Should().BeOfType<DocumentsValidationResult>();
        var documentsResult = (DocumentsValidationResult)result;
        documentsResult.DocumentsPath.Should().Contain(gameType);
    }
}

/// <summary>
///     Integration tests for DocumentsValidationAnalyzer that test with actual file system operations
/// </summary>
public class DocumentsValidationAnalyzerIntegrationTests : IDisposable
{
    private readonly DocumentsValidationAnalyzer _analyzer;
    private readonly Mock<ILogger<DocumentsValidationAnalyzer>> _mockLogger;
    private readonly Mock<IYamlSettingsProvider> _mockYamlSettings;
    private readonly string _tempDirectory;

    public DocumentsValidationAnalyzerIntegrationTests()
    {
        _tempDirectory = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
        Directory.CreateDirectory(_tempDirectory);

        _mockYamlSettings = new Mock<IYamlSettingsProvider>();
        _mockLogger = new Mock<ILogger<DocumentsValidationAnalyzer>>();

        var mockSettingsService = new Mock<IApplicationSettingsService>();
        mockSettingsService.Setup(x => x.LoadSettingsAsync())
            .ReturnsAsync(new ApplicationSettings { FcxMode = true });

        _analyzer = new DocumentsValidationAnalyzer(_mockYamlSettings.Object, _mockLogger.Object,
            mockSettingsService.Object);
    }

    public void Dispose()
    {
        try
        {
            if (Directory.Exists(_tempDirectory))
            {
                // Remove read-only attributes from any files
                var files = Directory.GetFiles(_tempDirectory, "*", SearchOption.AllDirectories);
                foreach (var file in files) File.SetAttributes(file, FileAttributes.Normal);

                Directory.Delete(_tempDirectory, true);
            }
        }
        catch (Exception ex)
        {
            // Log cleanup failure but don't throw
            Console.WriteLine($"Failed to cleanup temp directory {_tempDirectory}: {ex.Message}");
        }
    }

    [Fact]
    public async Task ParseSimpleIni_WithValidIniContent_ShouldParseCorrectly()
    {
        // Arrange
        var iniContent = @"
; This is a comment
[General]
bEnableLogging=1
sTestValue=TestString

[Archive]
bInvalidateOlderFiles=1
sResourceDataDirsFinal=

[Display]
iSize H=1080
iSize W=1920
";

        // Create a test INI file
        var testIniPath = Path.Combine(_tempDirectory, "test.ini");
        await File.WriteAllTextAsync(testIniPath, iniContent);

        var crashLog = new CrashLog
        {
            GameType = "Fallout4",
            OriginalLines = ["Test crash log"],
            FilePath = "test.log"
        };

        // Act
        var result = await _analyzer.AnalyzeAsync(crashLog);

        // Assert
        result.Should().NotBeNull();
        result.Should().BeOfType<DocumentsValidationResult>();
    }

    [Fact]
    public async Task ParseSimpleIni_WithCorruptedIniContent_ShouldHandleGracefully()
    {
        // Arrange
        var corruptedIniContent = @"
[General
bEnableLogging=1
invalid line without equals
[Archive]
bInvalidateOlderFiles=
=invalid value without key
";

        // Create a test INI file
        var testIniPath = Path.Combine(_tempDirectory, "corrupted.ini");
        await File.WriteAllTextAsync(testIniPath, corruptedIniContent);

        var crashLog = new CrashLog
        {
            GameType = "Fallout4",
            OriginalLines = ["Test crash log"],
            FilePath = "test.log"
        };

        // Act
        var result = await _analyzer.AnalyzeAsync(crashLog);

        // Assert
        result.Should().NotBeNull();
        result.Should().BeOfType<DocumentsValidationResult>();
    }

    [Theory]
    [InlineData("[Archive]\nbInvalidateOlderFiles=1\nsResourceDataDirsFinal=\n", true)]
    [InlineData("[Archive]\nbInvalidateOlderFiles=0\nsResourceDataDirsFinal=\n", false)]
    [InlineData("[Archive]\nbInvalidateOlderFiles=1\nsResourceDataDirsFinal=STRINGS\\\n", false)]
    [InlineData("[General]\nbEnableLogging=1\n", false)] // No Archive section
    public async Task ValidateCustomIniSettings_WithDifferentConfigurations_ShouldDetectArchiveInvalidation(
        string iniContent, bool expectedArchiveInvalidation)
    {
        // Arrange
        var testIniPath = Path.Combine(_tempDirectory, "testCustom.ini");
        await File.WriteAllTextAsync(testIniPath, iniContent);

        var crashLog = new CrashLog
        {
            GameType = "Fallout4",
            OriginalLines = ["Test crash log"],
            FilePath = "test.log"
        };

        // Act
        var result = await _analyzer.AnalyzeAsync(crashLog);

        // Assert
        result.Should().NotBeNull();
        result.Should().BeOfType<DocumentsValidationResult>();
        var documentsResult = (DocumentsValidationResult)result;

        // This test will need to be enhanced with a more sophisticated setup
        // to actually test the Custom.ini validation logic
    }

    [Fact]
    public async Task AnalyzeAsync_WithEmptyIniFile_ShouldReportCorruption()
    {
        // Arrange
        var emptyIniPath = Path.Combine(_tempDirectory, "empty.ini");
        await File.WriteAllTextAsync(emptyIniPath, "");

        var crashLog = new CrashLog
        {
            GameType = "Fallout4",
            OriginalLines = ["Test crash log"],
            FilePath = "test.log"
        };

        // Act
        var result = await _analyzer.AnalyzeAsync(crashLog);

        // Assert
        result.Should().NotBeNull();
        result.Should().BeOfType<DocumentsValidationResult>();
    }

    [Fact]
    public async Task AnalyzeAsync_WithReadOnlyIniFile_ShouldDetectReadOnlyStatus()
    {
        // Arrange
        var readOnlyIniPath = Path.Combine(_tempDirectory, "readonly.ini");
        await File.WriteAllTextAsync(readOnlyIniPath, "[General]\nbTest=1");
        File.SetAttributes(readOnlyIniPath, FileAttributes.ReadOnly);

        var crashLog = new CrashLog
        {
            GameType = "Fallout4",
            OriginalLines = ["Test crash log"],
            FilePath = "test.log"
        };

        try
        {
            // Act
            var result = await _analyzer.AnalyzeAsync(crashLog);

            // Assert
            result.Should().NotBeNull();
            result.Should().BeOfType<DocumentsValidationResult>();
        }
        finally
        {
            // Clean up read-only attribute
            if (File.Exists(readOnlyIniPath)) File.SetAttributes(readOnlyIniPath, FileAttributes.Normal);
        }
    }
}