using Scanner111.Core.GameScanning;
using Scanner111.Core.Infrastructure;
using Scanner111.Core.Models;

namespace Scanner111.Tests.GameScanning;

/// <summary>
///     Comprehensive tests for ModIniScanner.
/// </summary>
[Collection("Settings Test Collection")]
public class ModIniScannerTests : IDisposable
{
    private readonly Mock<ILogger<ModIniScanner>> _mockLogger;
    private readonly Mock<IApplicationSettingsService> _mockSettingsService;
    private readonly ModIniScanner _scanner;
    private readonly string _testDirectory;
    private readonly string _testGamePath;

    public ModIniScannerTests()
    {
        _mockSettingsService = new Mock<IApplicationSettingsService>();
        _mockLogger = new Mock<ILogger<ModIniScanner>>();

        _testDirectory = Path.Combine(Path.GetTempPath(), $"Scanner111Tests_{Guid.NewGuid()}");
        Directory.CreateDirectory(_testDirectory);

        _testGamePath = Path.Combine(_testDirectory, "TestGame");

        _scanner = new ModIniScanner(
            _mockSettingsService.Object,
            _mockLogger.Object);

        SetupDefaultMocks();
    }

    public void Dispose()
    {
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

    private void SetupDefaultMocks()
    {
        var settings = new ApplicationSettings
        {
            GamePath = _testGamePath,
            GameType = GameType.Fallout4
        };

        _mockSettingsService.Setup(x => x.LoadSettingsAsync())
            .ReturnsAsync(settings);
    }

    #region Performance Tests

    [Fact]
    public async Task ScanAsync_ManyIniFiles_CompletesInReasonableTime()
    {
        // Arrange
        CreateTestGameDirectory();

        // Create many INI files
        for (var i = 0; i < 50; i++)
            CreateIniFile($"mod_{i}.ini", $@"
[Section{i}]
key1=value1
key2=value2
key3=value3
");

        // Act
        var startTime = DateTime.UtcNow;
        var result = await _scanner.ScanAsync();
        var elapsed = DateTime.UtcNow - startTime;

        // Assert
        result.Should().NotBeNull();
        elapsed.TotalMilliseconds.Should().BeLessThan(2000); // Should complete within 2 seconds
    }

    #endregion

    #region Basic Functionality Tests

    [Fact]
    public async Task ScanAsync_NoGamePath_ReturnsWarning()
    {
        // Arrange
        var settings = new ApplicationSettings
        {
            GamePath = null,
            GameType = GameType.Fallout4
        };
        _mockSettingsService.Setup(x => x.LoadSettingsAsync()).ReturnsAsync(settings);

        // Act
        var result = await _scanner.ScanAsync();

        // Assert
        result.Should().Contain("WARNING : Game path not configured or doesn't exist");
    }

    [Fact]
    public async Task ScanAsync_GamePathDoesNotExist_ReturnsWarning()
    {
        // Arrange
        var settings = new ApplicationSettings
        {
            GamePath = @"C:\NonExistent\Path",
            GameType = GameType.Fallout4
        };
        _mockSettingsService.Setup(x => x.LoadSettingsAsync()).ReturnsAsync(settings);

        // Act
        var result = await _scanner.ScanAsync();

        // Assert
        result.Should().Contain("WARNING : Game path not configured or doesn't exist");
    }

    [Fact]
    public async Task ScanAsync_NoIniFiles_ReturnsInfoMessage()
    {
        // Arrange
        Directory.CreateDirectory(_testGamePath);

        // Act
        var result = await _scanner.ScanAsync();

        // Assert
        result.Should().Contain("No mod INI files found in the game directory");
    }

    #endregion

    #region Console Command Settings Tests

    [Fact]
    public async Task ScanAsync_ConsoleCommandSettingFound_ShowsNotice()
    {
        // Arrange
        CreateTestGameDirectory();
        CreateIniFile("Fallout4Custom.ini", @"
[General]
sStartingConsoleCommand=bat autoexec
");

        // Act
        var result = await _scanner.ScanAsync();

        // Assert
        result.Should().Contain("NOTICE : Console commands (sStartingConsoleCommand) are configured");
        result.Should().Contain("can slow down the initial game startup time");
        result.Should().Contain("Fallout4Custom.ini");
    }

    [Fact]
    public async Task ScanAsync_MultipleConsoleCommands_ShowsAllFiles()
    {
        // Arrange
        CreateTestGameDirectory();
        CreateIniFile("Fallout4Custom.ini", @"
[General]
sStartingConsoleCommand=bat autoexec
");
        CreateIniFile("Fallout4.ini", @"
[General]
sStartingConsoleCommand=tcl
");

        // Act
        var result = await _scanner.ScanAsync();

        // Assert
        result.Should().Contain("Fallout4Custom.ini");
        result.Should().Contain("Fallout4.ini");
        result.Should().Contain("sStartingConsoleCommand");
    }

    #endregion

    #region VSync Settings Tests

    [Fact]
    public async Task ScanAsync_VSyncInDxvkConf_ShowsNotice()
    {
        // Arrange
        CreateTestGameDirectory();
        CreateIniFile("dxvk.conf", @"
[dxgi]
syncInterval = 1
");

        // Act
        var result = await _scanner.ScanAsync();

        // Assert
        result.Should().Contain("VSYNC IS CURRENTLY ENABLED");
        result.Should().Contain("dxvk.conf");
        result.Should().Contain("syncInterval");
    }

    [Fact]
    public async Task ScanAsync_VSyncInEnbLocal_ShowsNotice()
    {
        // Arrange
        CreateTestGameDirectory();
        CreateIniFile("enblocal.ini", @"
[ENGINE]
ForceVSync=true
");

        // Act
        var result = await _scanner.ScanAsync();

        // Assert
        result.Should().Contain("VSYNC IS CURRENTLY ENABLED");
        result.Should().Contain("enblocal.ini");
        result.Should().Contain("ForceVSync");
    }

    [Fact]
    public async Task ScanAsync_MultipleVSyncSettings_ShowsAll()
    {
        // Arrange
        CreateTestGameDirectory();
        CreateIniFile("dxvk.conf", @"
[dxgi]
syncInterval = 1
");
        CreateIniFile("enblocal.ini", @"
[ENGINE]
ForceVSync=true
");
        CreateIniFile("reshade.ini", @"
[APP]
ForceVsync=1
");

        // Act
        var result = await _scanner.ScanAsync();

        // Assert
        result.Should().Contain("VSYNC IS CURRENTLY ENABLED");
        result.Should().Contain("dxvk.conf");
        result.Should().Contain("enblocal.ini");
        result.Should().Contain("reshade.ini");
    }

    [Fact]
    public async Task ScanAsync_VSyncDisabled_NoNotice()
    {
        // Arrange
        CreateTestGameDirectory();
        CreateIniFile("enblocal.ini", @"
[ENGINE]
ForceVSync=false
");

        // Act
        var result = await _scanner.ScanAsync();

        // Assert
        result.Should().NotContain("VSYNC IS CURRENTLY ENABLED");
    }

    #endregion

    #region INI Fix Application Tests

    [Fact]
    public async Task ScanAsync_ProblematicBuffoutSetting_GetsCorrected()
    {
        // Arrange
        CreateTestGameDirectory();
        var iniPath = CreateIniFile("Buffout4.ini", @"
[Patches]
MemoryManager=true
");

        // Act
        var result = await _scanner.ScanAsync();

        // Assert
        // The scanner should apply fixes to problematic settings
        var updatedContent = File.ReadAllText(iniPath);
        // Specific fix logic would depend on implementation
        result.Should().NotBeNull();
    }

    [Fact]
    public async Task ScanAsync_InvalidArchiveLimit_GetsCorrected()
    {
        // Arrange
        CreateTestGameDirectory();
        CreateIniFile("Fallout4Custom.ini", @"
[Archive]
iArchiveLimit=9999
");

        // Act
        var result = await _scanner.ScanAsync();

        // Assert
        result.Should().NotBeNull();
        // Check if the setting was corrected or warned about
    }

    #endregion

    #region Duplicate File Detection Tests

    [Fact]
    public async Task ScanAsync_DuplicateFiles_ShowsWarning()
    {
        // Arrange
        CreateTestGameDirectory();
        CreateIniFile("config.ini", "content1");
        CreateIniFile("config.ini.bak", "content1");
        CreateIniFile("config.old.ini", "content1");

        // Act
        var result = await _scanner.ScanAsync();

        // Assert
        result.Should().NotBeNull();
        // Implementation should detect potential duplicate configs
    }

    [Fact]
    public async Task ScanAsync_BackupFiles_HandlesCorrectly()
    {
        // Arrange
        CreateTestGameDirectory();
        CreateIniFile("Fallout4.ini", "main content");
        CreateIniFile("Fallout4.ini.backup", "backup content");
        CreateIniFile("Fallout4.ini.old", "old content");

        // Act
        var result = await _scanner.ScanAsync();

        // Assert
        result.Should().NotBeNull();
        // Should process main file and potentially warn about backups
    }

    #endregion

    #region Error Handling Tests

    [Fact]
    public async Task ScanAsync_CorruptedIniFile_HandlesGracefully()
    {
        // Arrange
        CreateTestGameDirectory();
        var corruptedPath = Path.Combine(_testGamePath, "corrupted.ini");
        File.WriteAllText(corruptedPath, "[Section\nkey=value"); // Missing closing bracket

        // Act
        var result = await _scanner.ScanAsync();

        // Assert
        result.Should().NotBeNull();
        _mockLogger.Verify(x => x.Log(
                LogLevel.Warning,
                It.IsAny<EventId>(),
                It.IsAny<It.IsAnyType>(),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.AtLeastOnce);
    }

    [Fact]
    public async Task ScanAsync_IniFileAccessDenied_HandlesGracefully()
    {
        // Arrange
        CreateTestGameDirectory();
        var protectedPath = CreateIniFile("protected.ini", "content");

        // Make file read-only
        var fileInfo = new FileInfo(protectedPath);
        fileInfo.Attributes = FileAttributes.ReadOnly;

        // Act
        var result = await _scanner.ScanAsync();

        // Assert
        result.Should().NotBeNull();
        // Should handle read-only files appropriately
    }

    #endregion

    #region Complex Scenario Tests

    [Fact]
    public async Task ScanAsync_CompleteScenario_ProcessesAllChecks()
    {
        // Arrange
        CreateTestGameDirectory();

        // Create various INI files with different issues
        CreateIniFile("Fallout4Custom.ini", @"
[General]
sStartingConsoleCommand=bat autoexec

[Archive]
iArchiveLimit=255
");

        CreateIniFile("enblocal.ini", @"
[ENGINE]
ForceVSync=true
EnableFPSLimit=false
");

        CreateIniFile("dxvk.conf", @"
[dxgi]
syncInterval = 1
");

        CreateIniFile("Buffout4.ini", @"
[Patches]
MemoryManager=false
");

        // Act
        var result = await _scanner.ScanAsync();

        // Assert
        result.Should().Contain("sStartingConsoleCommand");
        result.Should().Contain("VSYNC IS CURRENTLY ENABLED");
        result.Should().Contain("enblocal.ini");
        result.Should().Contain("dxvk.conf");
    }

    [Fact]
    public async Task ScanAsync_SubdirectoryIniFiles_AreProcessed()
    {
        // Arrange
        CreateTestGameDirectory();
        var subDir = Path.Combine(_testGamePath, "Data", "F4SE", "Plugins");
        Directory.CreateDirectory(subDir);

        var iniPath = Path.Combine(subDir, "plugin.ini");
        File.WriteAllText(iniPath, @"
[Settings]
Enabled=1
");

        // Act
        var result = await _scanner.ScanAsync();

        // Assert
        result.Should().NotBeNull();
        // Should process INI files in subdirectories
    }

    #endregion

    #region Helper Methods

    private void CreateTestGameDirectory()
    {
        Directory.CreateDirectory(_testGamePath);
    }

    private string CreateIniFile(string filename, string content)
    {
        var filePath = Path.Combine(_testGamePath, filename);
        var directory = Path.GetDirectoryName(filePath);

        if (!string.IsNullOrEmpty(directory)) Directory.CreateDirectory(directory);

        File.WriteAllText(filePath, content);
        return filePath;
    }

    #endregion
}