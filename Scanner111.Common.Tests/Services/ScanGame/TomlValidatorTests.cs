using FluentAssertions;
using Scanner111.Common.Models.ScanGame;
using Scanner111.Common.Services.ScanGame;

namespace Scanner111.Common.Tests.Services.ScanGame;

/// <summary>
/// Tests for the TomlValidator class.
/// </summary>
public class TomlValidatorTests : IDisposable
{
    private readonly TomlValidator _validator;
    private readonly string _tempDirectory;
    private readonly string _pluginsDirectory;

    public TomlValidatorTests()
    {
        _validator = new TomlValidator();
        _tempDirectory = Path.Combine(Path.GetTempPath(), $"TomlValidatorTests_{Guid.NewGuid():N}");
        _pluginsDirectory = Path.Combine(_tempDirectory, "F4SE", "Plugins");
        Directory.CreateDirectory(_pluginsDirectory);
    }

    public void Dispose()
    {
        try
        {
            if (Directory.Exists(_tempDirectory))
            {
                Directory.Delete(_tempDirectory, recursive: true);
            }
        }
        catch
        {
            // Ignore cleanup errors in tests
        }
        GC.SuppressFinalize(this);
    }

    #region Helper Methods

    private void CreateTomlFile(string relativePath, string content)
    {
        var fullPath = Path.Combine(_pluginsDirectory, relativePath);
        var directory = Path.GetDirectoryName(fullPath);
        if (!string.IsNullOrEmpty(directory))
        {
            Directory.CreateDirectory(directory);
        }
        File.WriteAllText(fullPath, content);
    }

    private void CreatePluginDll(string fileName)
    {
        var fullPath = Path.Combine(_pluginsDirectory, fileName);
        File.WriteAllBytes(fullPath, Array.Empty<byte>());
    }

    #endregion

    #region Basic Validation Tests

    [Fact]
    public async Task ValidateAsync_WithNonExistentDirectory_ReturnsNotFoundResult()
    {
        // Arrange
        var nonExistent = Path.Combine(_tempDirectory, "nonexistent");

        // Act
        var result = await _validator.ValidateAsync(nonExistent, "Buffout4", "Fallout4");

        // Assert
        result.ConfigFileFound.Should().BeFalse();
        result.InstalledPlugins.Should().BeEmpty();
    }

    [Fact]
    public async Task ValidateAsync_WithNoConfigFile_ReturnsNotFoundResult()
    {
        // Act
        var result = await _validator.ValidateAsync(_pluginsDirectory, "Buffout4", "Fallout4");

        // Assert
        result.ConfigFileFound.Should().BeFalse();
        result.FormattedReport.Should().Contain("TOML SETTINGS FILE NOT FOUND");
    }

    [Fact]
    public async Task ValidateAsync_WithOgConfigFile_FindsConfig()
    {
        // Arrange
        CreateTomlFile("Buffout4/config.toml", """
            [Patches]
            Achievements = true
            """);

        // Act
        var result = await _validator.ValidateAsync(_pluginsDirectory, "Buffout4", "Fallout4");

        // Assert
        result.ConfigFileFound.Should().BeTrue();
        result.ConfigFilePath.Should().Contain("Buffout4");
        result.ConfigFilePath.Should().Contain("config.toml");
    }

    [Fact]
    public async Task ValidateAsync_WithVrConfigFile_FindsConfig()
    {
        // Arrange
        CreateTomlFile("Buffout4.toml", """
            [Patches]
            Achievements = true
            """);

        // Act
        var result = await _validator.ValidateAsync(_pluginsDirectory, "Buffout4", "Fallout4");

        // Assert
        result.ConfigFileFound.Should().BeTrue();
        result.ConfigFilePath.Should().EndWith("Buffout4.toml");
    }

    [Fact]
    public async Task ValidateAsync_WithBothConfigFiles_DetectsDuplicates()
    {
        // Arrange
        CreateTomlFile("Buffout4/config.toml", """
            [Patches]
            Achievements = true
            """);
        CreateTomlFile("Buffout4.toml", """
            [Patches]
            Achievements = true
            """);

        // Act
        var result = await _validator.ValidateAsync(_pluginsDirectory, "Buffout4", "Fallout4");

        // Assert
        result.HasDuplicateConfigs.Should().BeTrue();
        result.FormattedReport.Should().Contain("BOTH VERSIONS");
    }

    #endregion

    #region Plugin Detection Tests

    [Fact]
    public async Task ValidateAsync_DetectsInstalledPlugins()
    {
        // Arrange
        CreateTomlFile("Buffout4/config.toml", "[Patches]\nAchievements = true");
        CreatePluginDll("x-cell-fo4.dll");
        CreatePluginDll("achievements.dll");

        // Act
        var result = await _validator.ValidateAsync(_pluginsDirectory, "Buffout4", "Fallout4");

        // Assert
        result.InstalledPlugins.Should().Contain("x-cell-fo4.dll");
        result.InstalledPlugins.Should().Contain("achievements.dll");
    }

    #endregion

    #region Settings Validation Tests

    [Fact]
    public async Task ValidateAsync_WithAchievementsMod_DetectsConflict()
    {
        // Arrange
        CreateTomlFile("Buffout4/config.toml", """
            [Patches]
            Achievements = true
            """);
        CreatePluginDll("achievements.dll");

        // Act
        var result = await _validator.ValidateAsync(_pluginsDirectory, "Buffout4", "Fallout4");

        // Assert
        result.ConfigIssues.Should().ContainSingle(i => i.Setting == "Achievements");
        result.ConfigIssues.First().CurrentValue.Should().Be("True");
        result.ConfigIssues.First().RecommendedValue.Should().Be("False");
    }

    [Fact]
    public async Task ValidateAsync_WithXCellMod_DetectsMemoryManagerConflict()
    {
        // Arrange
        CreateTomlFile("Buffout4/config.toml", """
            [Patches]
            MemoryManager = true
            HavokMemorySystem = true
            """);
        CreatePluginDll("x-cell-fo4.dll");

        // Act
        var result = await _validator.ValidateAsync(_pluginsDirectory, "Buffout4", "Fallout4");

        // Assert
        result.ConfigIssues.Should().Contain(i => i.Setting == "MemoryManager");
        result.ConfigIssues.Should().Contain(i => i.Setting == "HavokMemorySystem");
    }

    [Fact]
    public async Task ValidateAsync_WithXCellNg2_DetectsConflict()
    {
        // Arrange
        CreateTomlFile("Buffout4/config.toml", """
            [Patches]
            BSTextureStreamerLocalHeap = true
            """);
        CreatePluginDll("x-cell-ng2.dll");

        // Act
        var result = await _validator.ValidateAsync(_pluginsDirectory, "Buffout4", "Fallout4");

        // Assert
        result.ConfigIssues.Should().ContainSingle(i => i.Setting == "BSTextureStreamerLocalHeap");
    }

    [Fact]
    public async Task ValidateAsync_WithLooksMenu_DetectsF4EEMissing()
    {
        // Arrange
        CreateTomlFile("Buffout4/config.toml", """
            [Compatibility]
            F4EE = false
            """);
        CreatePluginDll("f4ee.dll");

        // Act
        var result = await _validator.ValidateAsync(_pluginsDirectory, "Buffout4", "Fallout4");

        // Assert
        result.ConfigIssues.Should().ContainSingle(i => i.Setting == "F4EE");
        result.ConfigIssues.First().RecommendedValue.Should().Be("True");
    }

    [Fact]
    public async Task ValidateAsync_WithCorrectSettings_NoIssues()
    {
        // Arrange
        CreateTomlFile("Buffout4/config.toml", """
            [Patches]
            Achievements = false
            MemoryManager = false

            [Compatibility]
            F4EE = true
            """);
        CreatePluginDll("achievements.dll");
        CreatePluginDll("x-cell-fo4.dll");
        CreatePluginDll("f4ee.dll");

        // Act
        var result = await _validator.ValidateAsync(_pluginsDirectory, "Buffout4", "Fallout4");

        // Assert
        result.ConfigIssues.Should().BeEmpty();
        result.FormattedReport.Should().Contain("correctly configured");
    }

    [Fact]
    public async Task ValidateAsync_WithBakaScrapHeap_DetectsRedundantMod()
    {
        // Arrange
        CreateTomlFile("Buffout4/config.toml", """
            [Patches]
            MemoryManager = true
            """);
        CreatePluginDll("x-cell-fo4.dll");
        CreatePluginDll("bakascrapheap.dll");

        // Act
        var result = await _validator.ValidateAsync(_pluginsDirectory, "Buffout4", "Fallout4");

        // Assert
        result.ConfigIssues.Should().Contain(i =>
            i.Description.Contains("Baka ScrapHeap") &&
            i.Severity == ConfigIssueSeverity.Error);
    }

    #endregion

    #region Non-Fallout4 Game Tests

    [Fact]
    public async Task ValidateAsync_WithNonFallout4Game_SkipsSettingsCheck()
    {
        // Arrange
        CreateTomlFile("Buffout4/config.toml", """
            [Patches]
            Achievements = true
            """);
        CreatePluginDll("achievements.dll");

        // Act
        var result = await _validator.ValidateAsync(_pluginsDirectory, "Buffout4", "SkyrimSE");

        // Assert
        // Should find config but not check settings for non-Fallout4 games
        result.ConfigFileFound.Should().BeTrue();
        result.ConfigIssues.Should().BeEmpty();
    }

    #endregion

    #region Progress Reporting Tests

    [Fact]
    public async Task ValidateWithProgressAsync_ReportsProgress()
    {
        // Arrange
        CreateTomlFile("Buffout4/config.toml", """
            [Patches]
            Achievements = true
            """);

        var progressReports = new List<TomlValidationProgress>();
        var progress = new Progress<TomlValidationProgress>(p => progressReports.Add(p));

        // Act
        await _validator.ValidateWithProgressAsync(_pluginsDirectory, "Buffout4", "Fallout4", progress);

        // Allow time for progress reports to be captured
        await Task.Delay(100);

        // Assert
        progressReports.Should().NotBeEmpty();
    }

    #endregion

    #region Value Reading Tests

    [Fact]
    public async Task GetValueAsync_WithBoolValue_ReturnsBool()
    {
        // Arrange
        var tomlPath = Path.Combine(_pluginsDirectory, "test.toml");
        File.WriteAllText(tomlPath, """
            [Patches]
            Enabled = true
            """);

        // Act
        var result = await _validator.GetValueAsync<bool>(tomlPath, "Patches", "Enabled");

        // Assert
        result.Should().BeTrue();
    }

    [Fact]
    public async Task GetValueAsync_WithIntValue_ReturnsInt()
    {
        // Arrange
        var tomlPath = Path.Combine(_pluginsDirectory, "test.toml");
        File.WriteAllText(tomlPath, """
            [Patches]
            MaxStdIO = 2048
            """);

        // Act
        var result = await _validator.GetValueAsync<long>(tomlPath, "Patches", "MaxStdIO");

        // Assert
        result.Should().Be(2048);
    }

    [Fact]
    public async Task GetStringValueAsync_ReturnsStringValue()
    {
        // Arrange
        var tomlPath = Path.Combine(_pluginsDirectory, "test.toml");
        File.WriteAllText(tomlPath, """
            [Info]
            Name = "Test Config"
            """);

        // Act
        var result = await _validator.GetStringValueAsync(tomlPath, "Info", "Name");

        // Assert
        result.Should().Be("Test Config");
    }

    [Fact]
    public async Task GetValueAsync_WithNonExistentKey_ReturnsNull()
    {
        // Arrange
        var tomlPath = Path.Combine(_pluginsDirectory, "test.toml");
        File.WriteAllText(tomlPath, """
            [Patches]
            Enabled = true
            """);

        // Act
        var result = await _validator.GetValueAsync<bool>(tomlPath, "Patches", "NonExistent");

        // Assert
        result.Should().BeNull();
    }

    [Fact]
    public async Task GetValueAsync_WithNonExistentSection_ReturnsNull()
    {
        // Arrange
        var tomlPath = Path.Combine(_pluginsDirectory, "test.toml");
        File.WriteAllText(tomlPath, """
            [Patches]
            Enabled = true
            """);

        // Act
        var result = await _validator.GetValueAsync<bool>(tomlPath, "NonExistent", "Enabled");

        // Assert
        result.Should().BeNull();
    }

    #endregion

    #region TOML Validation Tests

    [Fact]
    public async Task IsValidTomlFileAsync_WithValidToml_ReturnsTrue()
    {
        // Arrange
        var tomlPath = Path.Combine(_pluginsDirectory, "valid.toml");
        File.WriteAllText(tomlPath, """
            [Section]
            key = "value"
            number = 42
            bool = true
            """);

        // Act
        var result = await _validator.IsValidTomlFileAsync(tomlPath);

        // Assert
        result.Should().BeTrue();
    }

    [Fact]
    public async Task IsValidTomlFileAsync_WithInvalidToml_ReturnsFalse()
    {
        // Arrange
        var tomlPath = Path.Combine(_pluginsDirectory, "invalid.toml");
        File.WriteAllText(tomlPath, """
            [Section
            key = "unclosed string
            """);

        // Act
        var result = await _validator.IsValidTomlFileAsync(tomlPath);

        // Assert
        result.Should().BeFalse();
    }

    [Fact]
    public async Task IsValidTomlFileAsync_WithNonExistentFile_ReturnsFalse()
    {
        // Act
        var result = await _validator.IsValidTomlFileAsync(
            Path.Combine(_pluginsDirectory, "nonexistent.toml"));

        // Assert
        result.Should().BeFalse();
    }

    #endregion

    #region Edge Cases

    [Fact]
    public async Task ValidateAsync_WithMalformedToml_ReturnsParseError()
    {
        // Arrange
        CreateTomlFile("Buffout4/config.toml", """
            [Patches
            invalid toml content
            """);

        // Act
        var result = await _validator.ValidateAsync(_pluginsDirectory, "Buffout4", "Fallout4");

        // Assert
        result.ConfigFileFound.Should().BeTrue();
        result.FormattedReport.Should().Contain("ERROR");
        result.FormattedReport.Should().Contain("Failed to parse");
    }

    [Fact]
    public async Task ValidateAsync_SupportsCancellation()
    {
        // Arrange
        CreateTomlFile("Buffout4/config.toml", "[Patches]\nAchievements = true");
        var cts = new CancellationTokenSource();
        cts.Cancel();

        // Act & Assert
        // TaskCanceledException inherits from OperationCanceledException
        await Assert.ThrowsAnyAsync<OperationCanceledException>(
            () => _validator.ValidateAsync(_pluginsDirectory, "Buffout4", "Fallout4", cts.Token));
    }

    [Fact]
    public async Task ValidateAsync_WithEmptyPluginsDir_DetectsNoPlugins()
    {
        // Arrange
        CreateTomlFile("Buffout4/config.toml", """
            [Patches]
            Achievements = true
            """);

        // Act
        var result = await _validator.ValidateAsync(_pluginsDirectory, "Buffout4", "Fallout4");

        // Assert
        // achievements.dll is not installed, so no issue should be raised for Achievements setting
        result.ConfigIssues.Should().BeEmpty();
    }

    #endregion
}
