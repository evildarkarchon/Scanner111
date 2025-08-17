using Scanner111.Core.GameScanning;
using Scanner111.Core.Infrastructure;
using Scanner111.Core.Models;

namespace Scanner111.Tests.GameScanning;

/// <summary>
///     Comprehensive tests for CrashGenChecker (Buffout4 configuration validator).
/// </summary>
[Collection("Settings Test Collection")]
public class CrashGenCheckerTests : IDisposable
{
    private readonly CrashGenChecker _checker;
    private readonly Mock<ILogger<CrashGenChecker>> _mockLogger;
    private readonly Mock<IApplicationSettingsService> _mockSettingsService;
    private readonly Mock<IYamlSettingsProvider> _mockYamlProvider;
    private readonly string _testConfigPath;
    private readonly string _testDirectory;
    private readonly string _testPluginsPath;

    public CrashGenCheckerTests()
    {
        _mockSettingsService = new Mock<IApplicationSettingsService>();
        _mockYamlProvider = new Mock<IYamlSettingsProvider>();
        _mockLogger = new Mock<ILogger<CrashGenChecker>>();

        _testDirectory = Path.Combine(Path.GetTempPath(), $"Scanner111Tests_{Guid.NewGuid()}");
        Directory.CreateDirectory(_testDirectory);

        _testPluginsPath = Path.Combine(_testDirectory, "TestPlugins");
        _testConfigPath = Path.Combine(_testPluginsPath, "Buffout4", "config.toml");

        _checker = new CrashGenChecker(
            _mockSettingsService.Object,
            _mockYamlProvider.Object,
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
            PluginsFolder = _testPluginsPath,
            GameType = GameType.Fallout4
        };

        _mockSettingsService.Setup(x => x.LoadSettingsAsync())
            .ReturnsAsync(settings);
    }

    #region Basic Functionality Tests

    [Fact]
    public async Task CheckAsync_NoConfigFile_ReturnsNoticeMessage()
    {
        // Arrange
        // Don't create any files, so config won't be found

        // Act
        var result = await _checker.CheckAsync();

        // Assert
        result.Should().Contain("NOTICE : Unable to find the Buffout4 config file");
        result.Should().Contain("settings check will be skipped");
        result.Should().Contain("If you are using Mod Organizer 2");
    }

    [Fact]
    public async Task CheckAsync_ValidConfigAllSettingsCorrect_ReturnsSuccessMessages()
    {
        // Arrange
        CreateTestPluginsDirectory();
        CreateValidBuffout4Config();

        // Act
        var result = await _checker.CheckAsync();

        // Assert
        result.Should().Contain("✔️");
        result.Should().Contain("correctly configured");
        result.Should().NotContain("❌");
        result.Should().NotContain("⚠️");
    }

    [Fact]
    public async Task CheckAsync_ConfigInAlternativeLocation_ShowsNoticeAndProcesses()
    {
        // Arrange
        CreateTestPluginsDirectory();
        var altConfigPath = Path.Combine(_testPluginsPath, "Buffout4.toml");
        File.WriteAllText(altConfigPath, GetValidTomlContent());

        // Act
        var result = await _checker.CheckAsync();

        // Assert
        result.Should().Contain("NOTICE : Buffout4 config found in non-standard location");
        result.Should().Contain($"Config file found at: {altConfigPath}");
        result.Should().Contain("Consider moving it to:");
    }

    #endregion

    #region Obsolete Plugin Detection Tests

    [Fact]
    public async Task CheckAsync_ObsoleteXCellDetected_ShowsWarning()
    {
        // Arrange
        CreateTestPluginsDirectory();
        CreatePluginFile("x-cell-fo4.dll");
        CreateValidBuffout4Config();

        // Act
        var result = await _checker.CheckAsync();

        // Assert
        result.Should().Contain("WARNING : OBSOLETE X-CELL VERSION DETECTED");
        result.Should().Contain("x-cell-fo4.dll");
        result.Should().Contain("For regular Fallout 4: Use x-cell-og.dll");
        result.Should().Contain("For Next-Gen update: Use x-cell-ng2.dll");
    }

    [Fact]
    public async Task CheckAsync_ObsoleteAchievementsModDetected_ShowsWarning()
    {
        // Arrange
        CreateTestPluginsDirectory();
        CreatePluginFile("achievementsmodsenablerloader.dll");
        CreateValidBuffout4Config();

        // Act
        var result = await _checker.CheckAsync();

        // Assert
        result.Should().Contain("WARNING : OBSOLETE PLUGIN DETECTED");
        result.Should().Contain("Achievements Mod Enabler");
        result.Should().Contain("achievementsmodsenablerloader.dll");
    }

    [Fact]
    public async Task CheckAsync_MultipleObsoletePlugins_ShowsAllWarnings()
    {
        // Arrange
        CreateTestPluginsDirectory();
        CreatePluginFile("x-cell-fo4.dll");
        CreatePluginFile("f4se_loader_bridge.dll");
        CreateValidBuffout4Config();

        // Act
        var result = await _checker.CheckAsync();

        // Assert
        result.Should().Contain("OBSOLETE X-CELL VERSION DETECTED");
        result.Should().Contain("F4SE Loader Bridge");
    }

    #endregion

    #region Plugin Conflict Detection Tests

    [Fact]
    public async Task CheckAsync_XCellInstalled_DisablesConflictingSettings()
    {
        // Arrange
        CreateTestPluginsDirectory();
        CreatePluginFile("x-cell-og.dll");
        CreateBuffout4ConfigWithSettings(new Dictionary<string, Dictionary<string, object>>
        {
            ["Patches"] = new()
            {
                ["MemoryManager"] = true,
                ["HavokMemorySystem"] = true,
                ["BSTextureStreamerLocalHeap"] = true,
                ["ScaleformAllocator"] = true,
                ["SmallBlockAllocator"] = true
            }
        });

        // Act
        var result = await _checker.CheckAsync();

        // Assert
        result.Should().Contain("The X-Cell Mod is installed");
        result.Should().Contain("CAUTION");
        result.Should().Contain("Auto Scanner will change this parameter");
        result.Should().Contain("to prevent conflicts with X-Cell");

        // Verify the config was updated
        var updatedConfig = File.ReadAllText(_testConfigPath);
        updatedConfig.Should().Contain("MemoryManager = false");
        updatedConfig.Should().Contain("HavokMemorySystem = false");
    }

    [Fact]
    public async Task CheckAsync_AchievementsModInstalled_DisablesAchievementsParameter()
    {
        // Arrange
        CreateTestPluginsDirectory();
        CreatePluginFile("achievements.dll");
        CreateBuffout4ConfigWithSettings(new Dictionary<string, Dictionary<string, object>>
        {
            ["Patches"] = new()
            {
                ["Achievements"] = true
            }
        });

        // Act
        var result = await _checker.CheckAsync();

        // Assert
        result.Should().Contain("The Achievements Mod and/or Unlimited Survival Mode is installed");
        result.Should().Contain("Auto Scanner will change this parameter to false");
        result.Should().Contain("to prevent conflicts with Buffout4");
    }

    [Fact]
    public async Task CheckAsync_BakaScrapHeapWithMemoryManager_ShowsRedundancyWarning()
    {
        // Arrange
        CreateTestPluginsDirectory();
        CreatePluginFile("bakascrapheap.dll");
        CreatePluginFile("x-cell-og.dll");
        CreateBuffout4ConfigWithSettings(new Dictionary<string, Dictionary<string, object>>
        {
            ["Patches"] = new()
            {
                ["MemoryManager"] = false
            }
        });

        // Act
        var result = await _checker.CheckAsync();

        // Assert
        result.Should().Contain("The Baka ScrapHeap Mod is installed, but is redundant with Buffout4");
        result.Should().Contain("Uninstall the Baka ScrapHeap Mod");
    }

    [Fact]
    public async Task CheckAsync_LooksMenuInstalled_EnablesF4EEParameter()
    {
        // Arrange
        CreateTestPluginsDirectory();
        CreatePluginFile("f4ee.dll");
        CreateBuffout4ConfigWithSettings(new Dictionary<string, Dictionary<string, object>>
        {
            ["Compatibility"] = new()
            {
                ["F4EE"] = false
            }
        });

        // Act
        var result = await _checker.CheckAsync();

        // Assert
        result.Should().Contain("Looks Menu is installed, but F4EE parameter is set to FALSE");
        result.Should().Contain("Auto Scanner will change this parameter to true");
        result.Should().Contain("to prevent bugs and crashes from Looks Menu");
    }

    #endregion

    #region VR-Specific Tests

    [Fact]
    public async Task CheckAsync_VRGame_HandlesCorrectly()
    {
        // Arrange
        var vrSettings = new ApplicationSettings
        {
            PluginsFolder = _testPluginsPath,
            GameType = GameType.Fallout4VR
        };
        _mockSettingsService.Setup(x => x.LoadSettingsAsync()).ReturnsAsync(vrSettings);

        CreateTestPluginsDirectory();
        CreateBuffout4ConfigWithSettings(new Dictionary<string, Dictionary<string, object>>
        {
            ["Patches"] = new()
            {
                ["ArchiveLimit"] = false // Should be allowed for VR
            }
        });

        // Act
        var result = await _checker.CheckAsync();

        // Assert
        result.Should().NotContain("Archive Limit is enabled");
    }

    [Fact]
    public async Task CheckAsync_NonVRGame_ChecksArchiveLimit()
    {
        // Arrange
        CreateTestPluginsDirectory();
        CreateBuffout4ConfigWithSettings(new Dictionary<string, Dictionary<string, object>>
        {
            ["Patches"] = new()
            {
                ["ArchiveLimit"] = true // Should be disabled for non-VR
            }
        });

        // Act
        var result = await _checker.CheckAsync();

        // Assert
        result.Should().Contain("Archive Limit is enabled");
        result.Should().Contain("Auto Scanner will change this parameter to false");
        result.Should().Contain("to prevent crashes");
    }

    #endregion

    #region Error Handling Tests

    [Fact]
    public async Task CheckAsync_InvalidTomlContent_HandlesGracefully()
    {
        // Arrange
        CreateTestPluginsDirectory();
        Directory.CreateDirectory(Path.GetDirectoryName(_testConfigPath)!);
        File.WriteAllText(_testConfigPath, "This is not valid TOML content {{}");

        // Act
        var result = await _checker.CheckAsync();

        // Assert
        result.Should().Contain("ERROR : Failed to process Buffout4 settings");
        _mockLogger.Verify(x => x.Log(
                LogLevel.Error,
                It.IsAny<EventId>(),
                It.IsAny<It.IsAnyType>(),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.AtLeastOnce);
    }

    [Fact]
    public async Task CheckAsync_NoPluginsFolder_HandlesGracefully()
    {
        // Arrange
        var settings = new ApplicationSettings
        {
            PluginsFolder = null,
            GameType = GameType.Fallout4
        };
        _mockSettingsService.Setup(x => x.LoadSettingsAsync()).ReturnsAsync(settings);

        // Act
        var result = await _checker.CheckAsync();

        // Assert
        result.Should().Contain("Unable to find the Buffout4 config file");
    }

    [Fact]
    public async Task CheckAsync_NonExistentPluginsFolder_HandlesGracefully()
    {
        // Arrange
        var settings = new ApplicationSettings
        {
            PluginsFolder = @"C:\NonExistent\Path\That\Should\Not\Exist",
            GameType = GameType.Fallout4
        };
        _mockSettingsService.Setup(x => x.LoadSettingsAsync()).ReturnsAsync(settings);

        // Act
        var result = await _checker.CheckAsync();

        // Assert
        result.Should().Contain("Unable to find the Buffout4 config file");
    }

    #endregion

    #region HasPlugin Method Tests

    [Fact]
    public void HasPlugin_SinglePluginExists_ReturnsTrue()
    {
        // Arrange
        CreateTestPluginsDirectory();
        CreatePluginFile("x-cell-og.dll");

        // Force initialization
        _ = _checker.CheckAsync().Result;

        // Act
        var result = _checker.HasPlugin(new List<string> { "x-cell-og.dll" });

        // Assert
        result.Should().BeTrue();
    }

    [Fact]
    public void HasPlugin_MultiplePluginsOneExists_ReturnsTrue()
    {
        // Arrange
        CreateTestPluginsDirectory();
        CreatePluginFile("achievements.dll");

        // Force initialization
        _ = _checker.CheckAsync().Result;

        // Act
        var result = _checker.HasPlugin(new List<string>
        {
            "nonexistent.dll",
            "achievements.dll",
            "another.dll"
        });

        // Assert
        result.Should().BeTrue();
    }

    [Fact]
    public void HasPlugin_NoPluginsExist_ReturnsFalse()
    {
        // Arrange
        CreateTestPluginsDirectory();

        // Force initialization
        _ = _checker.CheckAsync().Result;

        // Act
        var result = _checker.HasPlugin(new List<string> { "nonexistent.dll" });

        // Assert
        result.Should().BeFalse();
    }

    [Fact]
    public void HasPlugin_CaseInsensitive_ReturnsTrue()
    {
        // Arrange
        CreateTestPluginsDirectory();
        CreatePluginFile("X-Cell-OG.dll");

        // Force initialization
        _ = _checker.CheckAsync().Result;

        // Act
        var result = _checker.HasPlugin(new List<string> { "x-cell-og.dll" });

        // Assert
        result.Should().BeTrue();
    }

    #endregion

    #region Helper Methods

    private void CreateTestPluginsDirectory()
    {
        Directory.CreateDirectory(_testPluginsPath);
    }

    private void CreatePluginFile(string filename)
    {
        var pluginPath = Path.Combine(_testPluginsPath, filename);
        File.WriteAllText(pluginPath, "dummy dll content");
    }

    private void CreateValidBuffout4Config()
    {
        Directory.CreateDirectory(Path.GetDirectoryName(_testConfigPath)!);
        File.WriteAllText(_testConfigPath, GetValidTomlContent());
    }

    private void CreateBuffout4ConfigWithSettings(Dictionary<string, Dictionary<string, object>> settings)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(_testConfigPath)!);

        var tomlContent = "";
        foreach (var section in settings)
        {
            tomlContent += $"[{section.Key}]\n";
            foreach (var setting in section.Value)
            {
                var value = setting.Value is bool b ? b.ToString().ToLower() : setting.Value.ToString();
                tomlContent += $"{setting.Key} = {value}\n";
            }

            tomlContent += "\n";
        }

        File.WriteAllText(_testConfigPath, tomlContent);
    }

    private string GetValidTomlContent()
    {
        return @"
[Patches]
Achievements = false
MemoryManager = false
HavokMemorySystem = false
BSTextureStreamerLocalHeap = false
ScaleformAllocator = false
SmallBlockAllocator = false
ArchiveLimit = false
MaxStdIO = 2048

[Compatibility]
F4EE = false
";
    }

    #endregion
}