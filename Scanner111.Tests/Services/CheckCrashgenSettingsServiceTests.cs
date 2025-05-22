using Moq;
using Scanner111.Models;
using Scanner111.Services;

namespace Scanner111.Tests.Services;

public class CheckCrashgenSettingsServiceTests
{
    private readonly Mock<IYamlSettingsCacheService> _mockYamlSettingsCache;
    private readonly CheckCrashgenSettingsService _service;
    private readonly string _tempPath;

    public CheckCrashgenSettingsServiceTests()
    {
        _mockYamlSettingsCache = new Mock<IYamlSettingsCacheService>();
        _service = new CheckCrashgenSettingsService(_mockYamlSettingsCache.Object);
        _tempPath = Path.GetTempPath();
    }

    [Fact]
    public async Task CheckCrashgenSettingsAsync_WithMissingGameDir_ReturnsError()
    {
        // Arrange
        string nullString = null;
        _mockYamlSettingsCache
            .Setup(x => x.GetSetting<string>(Yaml.Game, "game_dir", It.IsAny<string>()))
            .Returns(nullString);

        // Act
        var result = await _service.CheckCrashgenSettingsAsync();

        // Assert
        Assert.Contains("❌ ERROR : Game directory not configured in settings", result);
    }

    [Fact]
    public async Task CheckCrashgenSettingsAsync_WithNoConfigFiles_ReturnsWarning()
    {
        // Arrange
        var nonExistentPath = Path.Combine(_tempPath, Guid.NewGuid().ToString());
        _mockYamlSettingsCache
            .Setup(x => x.GetSetting<string>(Yaml.Game, "game_dir", It.IsAny<string>()))
            .Returns(nonExistentPath);

        // Act
        var result = await _service.CheckCrashgenSettingsAsync();

        // Assert
        Assert.Contains("⚠️ WARNING: No Buffout/Crashgen configuration files were found", result);
        Assert.Contains("Consider installing Buffout4", result);
    }

    [Fact]
    public async Task CheckCrashgenSettingsAsync_WithBuffout4Config_AnalyzesSettings()
    {
        // Arrange
        var gameDir = Path.Combine(_tempPath, Guid.NewGuid().ToString());
        var configDir = Path.Combine(gameDir, "Data", "F4SE", "Plugins");
        var configPath = Path.Combine(configDir, "Buffout4.toml");

        try
        {
            // Create directory and config file
            Directory.CreateDirectory(configDir);
            await File.WriteAllTextAsync(configPath, @"
[Fixes]
MemoryManager = true
ActorIsHostileToActor = false

[Logging]
ModNames = true
Plugins = true
Crashes = true
StockGames = false
");

            _mockYamlSettingsCache
                .Setup(x => x.GetSetting<string>(Yaml.Game, "game_dir", It.IsAny<string>()))
                .Returns(gameDir);

            // Act
            var result = await _service.CheckCrashgenSettingsAsync();

            // Assert
            Assert.Contains("✔️ Analyzing settings file: Buffout4.toml", result);
            Assert.Contains("✔️ Fixes.MemoryManager = true (Correct)", result);
            Assert.Contains("⚠️ Fixes.ActorIsHostileToActor = false", result);
            Assert.Contains("✔️ Logging.ModNames = true (Correct)", result);
            Assert.Contains("⚠️ Logging.StockGames = false", result);
        }
        finally
        {
            // Cleanup
            if (Directory.Exists(gameDir)) Directory.Delete(gameDir, true);
        }
    }

    [Fact]
    public async Task CheckCrashgenSettingsAsync_WithCrashLoggerConfig_AnalyzesSettings()
    {
        // Arrange
        var gameDir = Path.Combine(_tempPath, Guid.NewGuid().ToString());
        var configDir = Path.Combine(gameDir, "Data", "SKSE", "Plugins");
        var configPath = Path.Combine(configDir, "CrashLogger.toml");

        try
        {
            // Create directory and config file
            Directory.CreateDirectory(configDir);
            await File.WriteAllTextAsync(configPath, @"
[Settings]
EnableCrashLogger = true
IncludePluginsList = false
DumpModList = true
");

            _mockYamlSettingsCache
                .Setup(x => x.GetSetting<string>(Yaml.Game, "game_dir", It.IsAny<string>()))
                .Returns(gameDir);

            // Act
            var result = await _service.CheckCrashgenSettingsAsync();

            // Assert
            Assert.Contains("✔️ Analyzing settings file: CrashLogger.toml", result);
            Assert.Contains("✔️ Settings.EnableCrashLogger = true (Correct)", result);
            Assert.Contains("⚠️ Settings.IncludePluginsList = false", result);
            Assert.Contains("✔️ Settings.DumpModList = true (Correct)", result);
        }
        finally
        {
            // Cleanup
            if (Directory.Exists(gameDir)) Directory.Delete(gameDir, true);
        }
    }

    [Fact]
    public async Task CheckCrashgenSettingsAsync_WithEngineFixes_AnalyzesSettings()
    {
        // Arrange
        var gameDir = Path.Combine(_tempPath, Guid.NewGuid().ToString());
        var configDir = Path.Combine(gameDir, "Data", "SKSE", "Plugins");
        var configPath = Path.Combine(configDir, "EngineFixes.toml");

        try
        {
            // Create directory and config file
            Directory.CreateDirectory(configDir);
            await File.WriteAllTextAsync(configPath, @"
[Patches]
EnableMemoryManager = true
SaveGameMaxSize = false

[Fixes]
MemoryAccessErrors = true
RegularQuickSaves = false
");

            _mockYamlSettingsCache
                .Setup(x => x.GetSetting<string>(Yaml.Game, "game_dir", It.IsAny<string>()))
                .Returns(gameDir);

            // Act
            var result = await _service.CheckCrashgenSettingsAsync();

            // Assert
            Assert.Contains("✔️ Analyzing settings file: EngineFixes.toml", result);
            Assert.Contains("✔️ Patches.EnableMemoryManager = true (Correct)", result);
            Assert.Contains("⚠️ Patches.SaveGameMaxSize = false", result);
            Assert.Contains("✔️ Fixes.MemoryAccessErrors = true (Correct)", result);
            Assert.Contains("⚠️ Fixes.RegularQuickSaves = false", result);
        }
        finally
        {
            // Cleanup
            if (Directory.Exists(gameDir)) Directory.Delete(gameDir, true);
        }
    }

    [Fact]
    public async Task CheckCrashgenSettingsAsync_WithInvalidTomlFile_HandlesException()
    {
        // Arrange
        var gameDir = Path.Combine(_tempPath, Guid.NewGuid().ToString());
        var configDir = Path.Combine(gameDir, "Data", "F4SE", "Plugins");
        var configPath = Path.Combine(configDir, "Buffout4.toml");

        try
        {
            // Create directory and config file with invalid TOML
            Directory.CreateDirectory(configDir);
            await File.WriteAllTextAsync(configPath, @"
[Fixes
MemoryManager = true
This is invalid TOML
");

            _mockYamlSettingsCache
                .Setup(x => x.GetSetting<string>(Yaml.Game, "game_dir", It.IsAny<string>()))
                .Returns(gameDir);

            // Act
            var result = await _service.CheckCrashgenSettingsAsync();

            // Assert
            Assert.Contains("✔️ Analyzing settings file: Buffout4.toml", result);
            Assert.DoesNotContain("❌ ERROR: Failed to parse Buffout4.toml", result);
        }
        finally
        {
            // Cleanup
            if (Directory.Exists(gameDir)) Directory.Delete(gameDir, true);
        }
    }

    [Fact]
    public async Task CheckCrashgenSettingsAsync_WithMultipleConfigFiles_AnalyzesAll()
    {
        // Arrange
        var gameDir = Path.Combine(_tempPath, Guid.NewGuid().ToString());
        var f4SeDir = Path.Combine(gameDir, "Data", "F4SE", "Plugins");
        var skseDir = Path.Combine(gameDir, "Data", "SKSE", "Plugins");
        var buffoutPath = Path.Combine(f4SeDir, "Buffout4.toml");
        var crashLoggerPath = Path.Combine(skseDir, "CrashLogger.toml");

        try
        {
            // Create directories and config files
            Directory.CreateDirectory(f4SeDir);
            Directory.CreateDirectory(skseDir);

            await File.WriteAllTextAsync(buffoutPath, @"
[Fixes]
MemoryManager = true

[Logging]
ModNames = true
");

            await File.WriteAllTextAsync(crashLoggerPath, @"
[Settings]
EnableCrashLogger = true
");

            _mockYamlSettingsCache
                .Setup(x => x.GetSetting<string>(Yaml.Game, "game_dir", It.IsAny<string>()))
                .Returns(gameDir);

            // Act
            var result = await _service.CheckCrashgenSettingsAsync();

            // Assert
            Assert.Contains("✔️ Analyzing settings file: Buffout4.toml", result);
            Assert.Contains("✔️ Analyzing settings file: CrashLogger.toml", result);
            Assert.Contains("✔️ Fixes.MemoryManager = true (Correct)", result);
            Assert.Contains("✔️ Logging.ModNames = true (Correct)", result);
            Assert.Contains("✔️ Settings.EnableCrashLogger = true (Correct)", result);
        }
        finally
        {
            // Cleanup
            if (Directory.Exists(gameDir)) Directory.Delete(gameDir, true);
        }
    }
}