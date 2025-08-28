using FluentAssertions;
using Microsoft.Extensions.Logging;
using NSubstitute;
using Scanner111.Core.Configuration;
using Scanner111.Core.Services;

namespace Scanner111.Test.Services;

/// <summary>
///     Unit tests for ModDatabase service.
///     Tests YAML configuration loading and caching functionality.
/// </summary>
[Trait("Category", "Unit")]
[Trait("Performance", "Fast")]
[Trait("Component", "Service")]
public class ModDatabaseTests
{
    private readonly IAsyncYamlSettingsCore _mockYamlCore;
    private readonly ILogger<ModDatabase> _mockLogger;
    private readonly ModDatabase _modDatabase;

    public ModDatabaseTests()
    {
        _mockYamlCore = Substitute.For<IAsyncYamlSettingsCore>();
        _mockLogger = Substitute.For<ILogger<ModDatabase>>();
        _modDatabase = new ModDatabase(_mockYamlCore, _mockLogger);
    }

    [Fact]
    public void Constructor_WithNullYamlCore_ThrowsArgumentNullException()
    {
        // Act & Assert
        var act = () => new ModDatabase(null!, _mockLogger);
        act.Should().Throw<ArgumentNullException>().WithParameterName("yamlCore");
    }

    [Fact]
    public void Constructor_WithNullLogger_ThrowsArgumentNullException()
    {
        // Act & Assert
        var act = () => new ModDatabase(_mockYamlCore, null!);
        act.Should().Throw<ArgumentNullException>().WithParameterName("logger");
    }

    [Fact]
    public async Task LoadModWarningsAsync_WithNullOrEmptyCategory_ThrowsArgumentException()
    {
        // Act & Assert
        var actNull = async () => await _modDatabase.LoadModWarningsAsync(null!);
        await actNull.Should().ThrowAsync<ArgumentException>();

        var actEmpty = async () => await _modDatabase.LoadModWarningsAsync("");
        await actEmpty.Should().ThrowAsync<ArgumentException>();

        var actWhitespace = async () => await _modDatabase.LoadModWarningsAsync("   ");
        await actWhitespace.Should().ThrowAsync<ArgumentException>();
    }

    [Fact]
    public async Task LoadModWarningsAsync_WithValidCategory_LoadsFromYaml()
    {
        // Arrange
        var testData = new Dictionary<string, string>
        {
            { "ScrapEverything", "Warning about Scrap Everything" },
            { "SpringCleaning", "Warning about Spring Cleaning" }
        };

        _mockYamlCore.GetSettingAsync<Dictionary<string, string>>(
                YamlStore.Game, "Mods_FREQ", null, Arg.Any<CancellationToken>())
            .Returns(testData);

        // Act
        var result = await _modDatabase.LoadModWarningsAsync("FREQ");

        // Assert
        result.Should().HaveCount(2);
        result.Should().ContainKeys("ScrapEverything", "SpringCleaning");
        result["ScrapEverything"].Should().Be("Warning about Scrap Everything");
        result["SpringCleaning"].Should().Be("Warning about Spring Cleaning");

        await _mockYamlCore.Received(1).GetSettingAsync<Dictionary<string, string>>(
            YamlStore.Game, "Mods_FREQ", null, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task LoadModWarningsAsync_WithCaching_DoesNotReloadOnSecondCall()
    {
        // Arrange
        var testData = new Dictionary<string, string>
        {
            { "TestMod", "Test Warning" }
        };

        _mockYamlCore.GetSettingAsync<Dictionary<string, string>>(
                YamlStore.Game, "Mods_FREQ", null, Arg.Any<CancellationToken>())
            .Returns(testData);

        // Act
        var result1 = await _modDatabase.LoadModWarningsAsync("FREQ");
        var result2 = await _modDatabase.LoadModWarningsAsync("FREQ");

        // Assert
        result1.Should().BeEquivalentTo(result2);
        
        // Should only be called once due to caching
        await _mockYamlCore.Received(1).GetSettingAsync<Dictionary<string, string>>(
            YamlStore.Game, "Mods_FREQ", null, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task LoadModWarningsAsync_WithNullDataFromYaml_ReturnsEmptyDictionary()
    {
        // Arrange
        _mockYamlCore.GetSettingAsync<Dictionary<string, string>>(
                YamlStore.Game, "Mods_FREQ", null, Arg.Any<CancellationToken>())
            .Returns((Dictionary<string, string>?)null);

        // Act
        var result = await _modDatabase.LoadModWarningsAsync("FREQ");

        // Assert
        result.Should().BeEmpty();
    }

    [Fact]
    public async Task LoadModWarningsAsync_WithException_ReturnsEmptyDictionaryAndLogsError()
    {
        // Arrange
        _mockYamlCore.GetSettingAsync<Dictionary<string, string>>(
                YamlStore.Game, "Mods_FREQ", null, Arg.Any<CancellationToken>())
            .Returns(Task.FromException<Dictionary<string, string>?>(new InvalidOperationException("Test exception")));

        // Act
        var result = await _modDatabase.LoadModWarningsAsync("FREQ");

        // Assert
        result.Should().BeEmpty();
        
        // Verify error was logged (we can't easily match the exact message format with structured logging)
        _mockLogger.ReceivedWithAnyArgs(1).LogError(default(Exception), default(string), default(object[]));
    }

    [Fact]
    public async Task LoadModConflictsAsync_WithValidData_LoadsFromYaml()
    {
        // Arrange
        var conflictData = new Dictionary<string, string>
        {
            { "mod1 | mod2", "Conflict between mod1 and mod2" },
            { "mod3 | mod4", "Conflict between mod3 and mod4" }
        };

        _mockYamlCore.GetSettingAsync<Dictionary<string, string>>(
                YamlStore.Game, "Mods_CONF", null, Arg.Any<CancellationToken>())
            .Returns(conflictData);

        // Act
        var result = await _modDatabase.LoadModConflictsAsync();

        // Assert
        result.Should().HaveCount(2);
        result.Should().ContainKeys("mod1 | mod2", "mod3 | mod4");
        result["mod1 | mod2"].Should().Be("Conflict between mod1 and mod2");
    }

    [Fact]
    public async Task LoadModConflictsAsync_WithCaching_UsesLazyLoading()
    {
        // Arrange
        var conflictData = new Dictionary<string, string>
        {
            { "mod1 | mod2", "Test conflict" }
        };

        _mockYamlCore.GetSettingAsync<Dictionary<string, string>>(
                YamlStore.Game, "Mods_CONF", null, Arg.Any<CancellationToken>())
            .Returns(conflictData);

        // Act
        var result1 = await _modDatabase.LoadModConflictsAsync();
        var result2 = await _modDatabase.LoadModConflictsAsync();

        // Assert
        result1.Should().BeEquivalentTo(result2);
        
        // Should only be called once due to lazy caching
        await _mockYamlCore.Received(1).GetSettingAsync<Dictionary<string, string>>(
            YamlStore.Game, "Mods_CONF", null, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task LoadImportantModsAsync_WithValidCategory_LoadsFromYaml()
    {
        // Arrange
        var importantData = new Dictionary<string, string>
        {
            { "mod1 | Important Mod 1", "This mod is very important" },
            { "mod2 | Important Mod 2", "This mod is also important" }
        };

        _mockYamlCore.GetSettingAsync<Dictionary<string, string>>(
                YamlStore.Game, "Mods_CORE", null, Arg.Any<CancellationToken>())
            .Returns(importantData);

        // Act
        var result = await _modDatabase.LoadImportantModsAsync("CORE");

        // Assert
        result.Should().HaveCount(2);
        result.Should().ContainKeys("mod1 | Important Mod 1", "mod2 | Important Mod 2");
        result["mod1 | Important Mod 1"].Should().Be("This mod is very important");
    }

    [Fact]
    public async Task GetModWarningCategoriesAsync_ReturnsDefaultCategories()
    {
        // Arrange
        // Note: GetAllKeysAsync is not available in current interface, so we test default behavior

        // Act
        var result = await _modDatabase.GetModWarningCategoriesAsync();

        // Assert
        result.Should().Contain(new[] { "FREQ", "PERF", "STAB" });
    }

    [Fact]
    public async Task GetModWarningCategoriesAsync_ReturnsStaticCategories()
    {
        // Arrange
        // Note: Dynamic discovery requires additional YAML methods not currently available

        // Act
        var result = await _modDatabase.GetModWarningCategoriesAsync();

        // Assert
        result.Should().Contain(new[] { "FREQ", "PERF", "STAB" });
        result.Should().NotContain("CORE"); // Core is an important mod category, not warning
    }

    [Fact]
    public async Task GetImportantModCategoriesAsync_ReturnsDefaultCategories()
    {
        // Arrange
        // Note: GetAllKeysAsync is not available in current interface, so we test default behavior

        // Act
        var result = await _modDatabase.GetImportantModCategoriesAsync();

        // Assert
        result.Should().Contain(new[] { "CORE", "CORE_FOLON" });
    }

    [Fact]
    public async Task GetImportantModCategoriesAsync_ReturnsStaticCategories()
    {
        // Arrange
        // Note: Dynamic discovery requires additional YAML methods not currently available

        // Act
        var result = await _modDatabase.GetImportantModCategoriesAsync();

        // Assert
        result.Should().Contain(new[] { "CORE", "CORE_FOLON" });
        result.Should().NotContain("FREQ");
    }

    [Fact]
    public async Task IsAvailableAsync_WithValidData_ReturnsTrue()
    {
        // Arrange
        var testData = new Dictionary<string, string>
        {
            { "TestMod", "Test Warning" }
        };

        _mockYamlCore.GetSettingAsync<Dictionary<string, string>>(
                YamlStore.Game, "Mods_FREQ", null, Arg.Any<CancellationToken>())
            .Returns(testData);

        // Act
        var result = await _modDatabase.IsAvailableAsync();

        // Assert
        result.Should().BeTrue();
    }

    [Fact]
    public async Task IsAvailableAsync_WithEmptyData_ReturnsFalse()
    {
        // Arrange
        _mockYamlCore.GetSettingAsync<Dictionary<string, string>>(
                YamlStore.Game, "Mods_FREQ", null, Arg.Any<CancellationToken>())
            .Returns(new Dictionary<string, string>());

        // Act
        var result = await _modDatabase.IsAvailableAsync();

        // Assert
        result.Should().BeFalse();
    }

    [Fact]
    public async Task IsAvailableAsync_WithException_ReturnsFalse()
    {
        // Arrange
        _mockYamlCore.GetSettingAsync<Dictionary<string, string>>(
                YamlStore.Game, "Mods_FREQ", null, Arg.Any<CancellationToken>())
            .Returns(Task.FromException<Dictionary<string, string>?>(new InvalidOperationException("Test exception")));

        // Act
        var result = await _modDatabase.IsAvailableAsync();

        // Assert
        result.Should().BeFalse();
    }

    [Theory]
    [InlineData("freq", "FREQ")] // Case insensitive
    [InlineData("PERFORMANCE", "PERFORMANCE")]
    [InlineData("stability", "STABILITY")]
    public async Task LoadModWarningsAsync_CaseInsensitiveCategory_WorksCorrectly(
        string inputCategory, string expectedYamlKey)
    {
        // Arrange
        var testData = new Dictionary<string, string>
        {
            { "TestMod", "Test Warning" }
        };

        _mockYamlCore.GetSettingAsync<Dictionary<string, string>>(
                YamlStore.Game, $"Mods_{expectedYamlKey}", null, Arg.Any<CancellationToken>())
            .Returns(testData);

        // Act
        var result = await _modDatabase.LoadModWarningsAsync(inputCategory);

        // Assert
        result.Should().HaveCount(1);
        await _mockYamlCore.Received(1).GetSettingAsync<Dictionary<string, string>>(
            YamlStore.Game, $"Mods_{expectedYamlKey}", null, Arg.Any<CancellationToken>());
    }
}