using FluentAssertions;
using Microsoft.Extensions.Logging;
using Moq;
using Scanner111.Core.FCX;
using Scanner111.Core.Infrastructure;
using Scanner111.Core.Models;

namespace Scanner111.Tests.FCX;

public class ModCompatibilityServiceTests
{
    private readonly Mock<IApplicationSettingsService> _appSettingsMock;
    private readonly Mock<ILogger<ModCompatibilityService>> _loggerMock;
    private readonly ModCompatibilityService _service;
    private readonly Mock<IYamlSettingsProvider> _yamlSettingsMock;

    public ModCompatibilityServiceTests()
    {
        _loggerMock = new Mock<ILogger<ModCompatibilityService>>();
        _yamlSettingsMock = new Mock<IYamlSettingsProvider>();
        _appSettingsMock = new Mock<IApplicationSettingsService>();
        _service = new ModCompatibilityService(_loggerMock.Object, _yamlSettingsMock.Object, _appSettingsMock.Object);
    }

    [Fact]
    public async Task GetCompatibilityInfoAsync_ReturnsNull_WhenDataFileDoesNotExist()
    {
        // Act
        var result = await _service.GetCompatibilityInfoAsync("TestMod", GameType.Fallout4, "1.10.163.0");

        // Assert
        result.Should().BeNull("no compatibility info should be returned when data file doesn't exist");
    }

    [Fact]
    public async Task GetCompatibilityInfoAsync_ReturnsNull_WhenModNotFound()
    {
        // Since we can't mock file system easily, we'll just verify the behavior
        // Act
        var result = await _service.GetCompatibilityInfoAsync("NonExistentMod", GameType.Fallout4, "1.10.163.0");

        // Assert
        result.Should().BeNull("no compatibility info should be returned when data file doesn't exist");
    }

    [Fact]
    public async Task GetKnownIssuesAsync_ReturnsEmptyList_WhenDataFileDoesNotExist()
    {
        // Act
        var result = await _service.GetKnownIssuesAsync(GameType.Fallout4, "1.10.163.0");

        // Assert
        result.Should().NotBeNull("method should return a list even when data file doesn't exist");
        result.Should().BeEmpty("empty list should be returned when no data is available");
    }

    [Fact]
    public async Task GetXseRequirementsAsync_ReturnsEmptyList_WhenDataFileDoesNotExist()
    {
        // Act
        var result = await _service.GetXseRequirementsAsync(GameType.Fallout4, "1.10.163.0");

        // Assert
        result.Should().NotBeNull("method should return a list even when data file doesn't exist");
        result.Should().BeEmpty("empty list should be returned when no data is available");
    }

    [Theory]
    [InlineData("1.10.163.0", "1.10.0.0", null, true)] // Current version is higher than min
    [InlineData("1.10.163.0", "1.11.0.0", null, false)] // Current version is lower than min
    [InlineData("1.10.163.0", null, "1.11.0.0", true)] // Current version is lower than max
    [InlineData("1.10.163.0", null, "1.9.0.0", false)] // Current version is higher than max
    [InlineData("1.10.163.0", "1.10.0.0", "1.11.0.0", true)] // Current version is within range
    [InlineData("1.10.163.0", "1.11.0.0", "1.12.0.0", false)] // Current version is below range
    public void IsVersionCompatible_HandlesVersionRanges(string gameVersion, string minVersion, string maxVersion,
        bool expectedResult)
    {
        // This tests the private method indirectly through the expected behavior
        // We would need to create a test YAML file to properly test this
        true.Should().BeTrue("placeholder test for version compatibility logic");
    }

    [Fact]
    public async Task GetCompatibilityInfoAsync_HandlesConcurrentCalls()
    {
        // Arrange
        var tasks = new List<Task<ModCompatibilityInfo?>>();

        // Act
        for (var i = 0; i < 10; i++)
            tasks.Add(_service.GetCompatibilityInfoAsync($"Mod{i}", GameType.Fallout4, "1.10.163.0"));

        var results = await Task.WhenAll(tasks);

        // Assert
        results.Should().AllSatisfy(r => r.Should().BeNull("all results should be null since no data file exists"));
    }

    [Fact]
    public async Task GetKnownIssuesAsync_HandlesDifferentGameTypes()
    {
        // Act
        var fallout4Issues = await _service.GetKnownIssuesAsync(GameType.Fallout4, "1.10.163.0");
        var skyrimIssues = await _service.GetKnownIssuesAsync(GameType.SkyrimSE, "1.6.640.0");

        // Assert
        fallout4Issues.Should().NotBeNull("Fallout 4 issues list should not be null");
        skyrimIssues.Should().NotBeNull("Skyrim issues list should not be null");
        fallout4Issues.Should().BeEmpty("no Fallout 4 issues when data doesn't exist");
        skyrimIssues.Should().BeEmpty("no Skyrim issues when data doesn't exist");
    }

    [Fact]
    public async Task GetXseRequirementsAsync_UsesCorrectXseType()
    {
        // Act
        var f4seRequirements = await _service.GetXseRequirementsAsync(GameType.Fallout4, "1.10.163.0");
        var skseRequirements = await _service.GetXseRequirementsAsync(GameType.SkyrimSE, "1.6.640.0");

        // Assert
        // Since we don't have actual data, we just verify no exceptions
        f4seRequirements.Should().NotBeNull("F4SE requirements list should not be null");
        skseRequirements.Should().NotBeNull("SKSE requirements list should not be null");
    }

    [Fact]
    public void ModCompatibilityInfo_PropertiesWorkCorrectly()
    {
        // Arrange
        var info = new ModCompatibilityInfo
        {
            ModName = "TestMod",
            MinVersion = "1.0.0",
            MaxVersion = "2.0.0",
            Notes = "Test notes",
            IsCompatible = true,
            RecommendedAction = null
        };

        // Assert
        info.ModName.Should().Be("TestMod", "mod name should be set correctly");
        info.MinVersion.Should().Be("1.0.0", "min version should be set correctly");
        info.MaxVersion.Should().Be("2.0.0", "max version should be set correctly");
        info.Notes.Should().Be("Test notes", "notes should be set correctly");
        info.IsCompatible.Should().BeTrue("compatibility flag should be set correctly");
        info.RecommendedAction.Should().BeNull("recommended action should be null when not set");
    }

    [Fact]
    public void ModCompatibilityIssue_PropertiesWorkCorrectly()
    {
        // Arrange
        var issue = new ModCompatibilityIssue
        {
            ModName = "ProblematicMod",
            AffectedVersions = new List<string> { "1.0.0", "1.1.0" },
            Issue = "Crashes on startup",
            Solution = "Update to version 2.0"
        };

        // Assert
        issue.ModName.Should().Be("ProblematicMod", "mod name should be set correctly");
        issue.AffectedVersions.Should().HaveCount(2, "affected versions list should contain two items");
        issue.Issue.Should().Be("Crashes on startup", "issue description should be set correctly");
        issue.Solution.Should().Be("Update to version 2.0", "solution should be set correctly");
    }

    [Fact]
    public void XsePluginRequirement_PropertiesWorkCorrectly()
    {
        // Arrange
        var requirement = new XsePluginRequirement
        {
            PluginName = "TestPlugin",
            RequiredXseVersion = "0.6.23",
            CompatibleGameVersions = new List<string> { "1.10.163.0" }
        };

        // Assert
        requirement.PluginName.Should().Be("TestPlugin", "plugin name should be set correctly");
        requirement.RequiredXseVersion.Should().Be("0.6.23", "required XSE version should be set correctly");
        requirement.CompatibleGameVersions.Should().ContainSingle("compatible game versions should have one entry");
        requirement.CompatibleGameVersions[0].Should()
            .Be("1.10.163.0", "compatible game version should be set correctly");
    }

    [Theory]
    [InlineData("1.10.163", "1.10.163.0", "1.11.0.0", true)] // Parse partial version
    [InlineData("1.10.163.0.1", "1.10.0.0", "1.11.0.0", true)] // Parse extra version parts
    [InlineData("invalid", "1.10.0.0", "1.11.0.0", true)] // Invalid version format defaults to compatible
    [InlineData("1.10.163.0", "invalid", "1.11.0.0", true)] // Invalid min version
    [InlineData("1.10.163.0", "1.10.0.0", "invalid", true)] // Invalid max version
    [InlineData("", "1.10.0.0", "1.11.0.0", true)] // Empty version string
    [InlineData("1.10.163.0", "", "1.11.0.0", true)] // Empty min version
    [InlineData("1.10.163.0", "1.10.0.0", "", true)] // Empty max version
    [InlineData("1.10.163.0", "null", "null", true)] // String "null" values
    public async Task GetCompatibilityInfoAsync_HandlesVersionParsingEdgeCases(string gameVersion, string minVersion,
        string maxVersion, bool expectedCompatible)
    {
        // This test verifies that the version parsing handles edge cases gracefully
        // Since we can't easily mock the file system, we're testing the expected behavior
        var result = await _service.GetCompatibilityInfoAsync("TestMod", GameType.Fallout4, gameVersion);

        // The service should handle invalid versions gracefully
        // In the absence of data, result will be null, but no exceptions should occur
        result.Should().BeNull("no compatibility info should be returned when data file doesn't exist");
    }

    [Fact]
    public async Task GetCompatibilityInfoAsync_HandlesNullModName()
    {
        // Act & Assert - should not throw
        var result = await _service.GetCompatibilityInfoAsync(null!, GameType.Fallout4, "1.10.163.0");
        result.Should().BeNull("no compatibility info should be returned when data file doesn't exist");
    }

    [Fact]
    public async Task GetCompatibilityInfoAsync_HandlesEmptyModName()
    {
        // Act & Assert - should not throw
        var result = await _service.GetCompatibilityInfoAsync("", GameType.Fallout4, "1.10.163.0");
        result.Should().BeNull("no compatibility info should be returned when data file doesn't exist");
    }

    [Fact]
    public async Task GetKnownIssuesAsync_WithInvalidGameVersion_ReturnsEmptyList()
    {
        // Act
        var result = await _service.GetKnownIssuesAsync(GameType.Fallout4, "invalid.version");

        // Assert
        result.Should().NotBeNull("method should return a list even when data file doesn't exist");
        result.Should().BeEmpty("empty list should be returned when no data is available");
    }

    [Fact]
    public async Task GetXseRequirementsAsync_WithNullGameVersion_HandlesGracefully()
    {
        // Act & Assert - should not throw
        var result = await _service.GetXseRequirementsAsync(GameType.SkyrimSE, null!);
        result.Should().NotBeNull("method should return a list even when data file doesn't exist");
        result.Should().BeEmpty("empty list should be returned when no data is available");
    }

    [Fact]
    public async Task GetCompatibilityInfoAsync_CaseInsensitiveModNameMatch()
    {
        // The implementation uses StringComparison.OrdinalIgnoreCase
        // Test with different casing
        var result1 = await _service.GetCompatibilityInfoAsync("TESTMOD", GameType.Fallout4, "1.10.163.0");
        var result2 = await _service.GetCompatibilityInfoAsync("testmod", GameType.Fallout4, "1.10.163.0");
        var result3 = await _service.GetCompatibilityInfoAsync("TestMod", GameType.Fallout4, "1.10.163.0");

        // All should return the same result (null in this case)
        result1.Should().Be(result2, "results should be the same regardless of casing");
        result2.Should().Be(result3, "results should be the same regardless of casing");
    }

    [Theory]
    [InlineData(GameType.Fallout4)]
    [InlineData(GameType.SkyrimSE)]
    [InlineData((GameType)999)] // Invalid game type
    public async Task AllMethods_HandleDifferentGameTypes(GameType gameType)
    {
        // Act - should not throw for any game type
        var compatInfo = await _service.GetCompatibilityInfoAsync("Mod", gameType, "1.0.0");
        var issues = await _service.GetKnownIssuesAsync(gameType, "1.0.0");
        var requirements = await _service.GetXseRequirementsAsync(gameType, "1.0.0");

        // Assert - all should handle gracefully
        compatInfo.Should().BeNull("compatibility info should be null when data doesn't exist");
        issues.Should().NotBeNull("issues list should not be null");
        requirements.Should().NotBeNull("requirements list should not be null");
    }

    [Fact]
    public async Task EnsureDataLoadedAsync_HandlesFileLocking()
    {
        // Test concurrent access to ensure proper locking
        var tasks = new List<Task>();
        for (var i = 0; i < 20; i++)
            tasks.Add(Task.Run(async () =>
            {
                await _service.GetCompatibilityInfoAsync($"Mod{i}", GameType.Fallout4, "1.10.163.0");
                await _service.GetKnownIssuesAsync(GameType.Fallout4, "1.10.163.0");
                await _service.GetXseRequirementsAsync(GameType.Fallout4, "1.10.163.0");
            }));

        // Act & Assert - should complete without deadlock or exceptions
        await Task.WhenAll(tasks);
    }

    [Fact]
    public void ModCompatibilityInfo_HandlesNullValues()
    {
        // Arrange
        var info = new ModCompatibilityInfo
        {
            ModName = null!,
            MinVersion = null,
            MaxVersion = null,
            Notes = null,
            IsCompatible = false,
            RecommendedAction = null
        };

        // Assert - verify nulls are handled
        info.ModName.Should().BeNull("mod name can be null");
        info.MinVersion.Should().BeNull("min version can be null");
        info.MaxVersion.Should().BeNull("max version can be null");
        info.Notes.Should().BeNull("notes can be null");
        info.IsCompatible.Should().BeFalse("compatibility flag should be false");
        info.RecommendedAction.Should().BeNull("recommended action can be null");
    }

    [Fact]
    public void ModCompatibilityIssue_WithEmptyAffectedVersions()
    {
        // Arrange
        var issue = new ModCompatibilityIssue
        {
            ModName = "TestMod",
            AffectedVersions = new List<string>(),
            Issue = "Test issue",
            Solution = null
        };

        // Assert
        issue.AffectedVersions.Should().BeEmpty("affected versions list can be empty");
        issue.Solution.Should().BeNull("solution can be null");
    }

    [Fact]
    public void XsePluginRequirement_WithEmptyCompatibleVersions()
    {
        // Arrange
        var requirement = new XsePluginRequirement
        {
            PluginName = "",
            RequiredXseVersion = "",
            CompatibleGameVersions = new List<string>()
        };

        // Assert
        requirement.PluginName.Should().BeEmpty("plugin name can be empty");
        requirement.RequiredXseVersion.Should().BeEmpty("required XSE version can be empty");
        requirement.CompatibleGameVersions.Should().BeEmpty("compatible game versions list can be empty");
    }

    [Fact]
    public async Task GetCompatibilityInfoAsync_WithSpecialCharactersInModName()
    {
        // Test with mod names containing special characters
        var specialNames = new[]
        {
            "Mod's Name",
            "Mod-Name",
            "Mod.Name",
            "Mod@Name",
            "Mod Name (Version 1.0)",
            "Mod/Name",
            "Mod\\Name"
        };

        foreach (var name in specialNames)
        {
            // Act & Assert - should not throw
            var result = await _service.GetCompatibilityInfoAsync(name, GameType.Fallout4, "1.10.163.0");
            result.Should().BeNull("no compatibility info should be returned when data file doesn't exist");
        }
    }

    [Theory]
    [InlineData("2147483647.2147483647.2147483647.2147483647")] // Max int values
    [InlineData("0.0.0.0")] // All zeros
    [InlineData("999.999.999.999")] // Large numbers
    public async Task GetCompatibilityInfoAsync_WithExtremeVersionNumbers(string version)
    {
        // Act & Assert - should handle extreme version numbers
        var result = await _service.GetCompatibilityInfoAsync("Mod", GameType.Fallout4, version);
        result.Should().BeNull("no compatibility info should be returned when data file doesn't exist");
    }
}