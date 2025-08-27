using FluentAssertions;
using Scanner111.Core.Analysis;
using Scanner111.Core.Models;

namespace Scanner111.Test.Models;

/// <summary>
///     Unit tests for mod detection models.
///     Tests ModWarning, ModConflict, and ImportantMod record types.
/// </summary>
public class ModDetectionModelsTests
{
    #region ModWarning Tests

    [Fact]
    public void ModWarning_Create_WithValidParameters_CreatesCorrectInstance()
    {
        // Act
        var warning = ModWarning.Create(
            "TestMod",
            "This is a test warning",
            AnalysisSeverity.Error,
            "plugin123",
            "FREQ");

        // Assert
        warning.ModName.Should().Be("TestMod");
        warning.Warning.Should().Be("This is a test warning");
        warning.Severity.Should().Be(AnalysisSeverity.Error);
        warning.PluginId.Should().Be("plugin123");
        warning.Category.Should().Be("FREQ");
    }

    [Fact]
    public void ModWarning_Create_WithNullModName_ThrowsArgumentException()
    {
        // Act & Assert
        var act = () => ModWarning.Create(null!, "Warning");
        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void ModWarning_Create_WithEmptyWarning_ThrowsArgumentException()
    {
        // Act & Assert
        var act = () => ModWarning.Create("ModName", "");
        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void ModWarning_Create_WithDefaultSeverity_UsesWarning()
    {
        // Act
        var warning = ModWarning.Create("TestMod", "Test warning");

        // Assert
        warning.Severity.Should().Be(AnalysisSeverity.Warning);
    }

    [Fact]
    public void ModWarning_Create_WithOptionalParameters_SetsCorrectValues()
    {
        // Act
        var warning = ModWarning.Create("TestMod", "Test warning");

        // Assert
        warning.PluginId.Should().BeNull();
        warning.Category.Should().BeNull();
    }

    #endregion

    #region ModConflict Tests

    [Fact]
    public void ModConflict_Create_WithValidParameters_CreatesCorrectInstance()
    {
        // Arrange
        var pluginIds = new[] { "plugin1", "plugin2" };

        // Act
        var conflict = ModConflict.Create(
            "Mod1",
            "Mod2",
            "These mods conflict",
            AnalysisSeverity.Error,
            pluginIds);

        // Assert
        conflict.FirstMod.Should().Be("Mod1");
        conflict.SecondMod.Should().Be("Mod2");
        conflict.ConflictWarning.Should().Be("These mods conflict");
        conflict.Severity.Should().Be(AnalysisSeverity.Error);
        conflict.FoundPluginIds.Should().BeEquivalentTo(pluginIds);
    }

    [Fact]
    public void ModConflict_Create_WithNullFirstMod_ThrowsArgumentException()
    {
        // Act & Assert
        var act = () => ModConflict.Create(null!, "Mod2", "Conflict");
        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void ModConflict_Create_WithEmptySecondMod_ThrowsArgumentException()
    {
        // Act & Assert
        var act = () => ModConflict.Create("Mod1", "", "Conflict");
        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void ModConflict_Create_WithDefaultSeverity_UsesWarning()
    {
        // Act
        var conflict = ModConflict.Create("Mod1", "Mod2", "Conflict");

        // Assert
        conflict.Severity.Should().Be(AnalysisSeverity.Warning);
    }

    [Fact]
    public void ModConflict_Create_WithNullPluginIds_UsesEmptyArray()
    {
        // Act
        var conflict = ModConflict.Create("Mod1", "Mod2", "Conflict");

        // Assert
        conflict.FoundPluginIds.Should().BeEmpty();
    }

    [Fact]
    public void ModConflict_GetConflictPair_ReturnsCorrectFormat()
    {
        // Arrange
        var conflict = ModConflict.Create("ModA", "ModB", "Test conflict");

        // Act
        var pair = conflict.GetConflictPair();

        // Assert
        pair.Should().Be("ModA | ModB");
    }

    #endregion

    #region ImportantMod Tests

    [Fact]
    public void ImportantMod_Create_WithValidParameters_CreatesCorrectInstance()
    {
        // Act
        var mod = ImportantMod.Create(
            "TestMod",
            "Test Mod Display Name",
            "This mod is recommended",
            true,
            "CORE");

        // Assert
        mod.ModId.Should().Be("TestMod");
        mod.DisplayName.Should().Be("Test Mod Display Name");
        mod.Recommendation.Should().Be("This mod is recommended");
        mod.IsInstalled.Should().BeTrue();
        mod.Category.Should().Be("CORE");
        mod.HasGpuRequirement.Should().BeFalse();
        mod.RequiredGpuType.Should().BeNull();
        mod.DetectedGpuType.Should().BeNull();
        mod.HasGpuCompatibilityIssue.Should().BeFalse();
    }

    [Fact]
    public void ImportantMod_Create_WithNullModId_ThrowsArgumentException()
    {
        // Act & Assert
        var act = () => ImportantMod.Create(null!, "Display", "Recommendation");
        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void ImportantMod_Create_WithEmptyDisplayName_ThrowsArgumentException()
    {
        // Act & Assert
        var act = () => ImportantMod.Create("ModId", "", "Recommendation");
        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void ImportantMod_CreateWithGpuCheck_WithMatchingGpu_NoCompatibilityIssue()
    {
        // Act
        var mod = ImportantMod.CreateWithGpuCheck(
            "NvidiaMod",
            "Nvidia Specific Mod",
            "For Nvidia GPUs only",
            true,
            "nvidia",
            "nvidia");

        // Assert
        mod.IsInstalled.Should().BeTrue();
        mod.HasGpuRequirement.Should().BeTrue();
        mod.RequiredGpuType.Should().Be("nvidia");
        mod.DetectedGpuType.Should().Be("nvidia");
        mod.HasGpuCompatibilityIssue.Should().BeFalse();
    }

    [Fact]
    public void ImportantMod_CreateWithGpuCheck_WithMismatchedGpu_HasCompatibilityIssue()
    {
        // Act
        var mod = ImportantMod.CreateWithGpuCheck(
            "NvidiaMod",
            "Nvidia Specific Mod",
            "For Nvidia GPUs only",
            true,
            "nvidia",
            "amd");

        // Assert
        mod.IsInstalled.Should().BeTrue();
        mod.HasGpuRequirement.Should().BeTrue();
        mod.RequiredGpuType.Should().Be("nvidia");
        mod.DetectedGpuType.Should().Be("amd");
        mod.HasGpuCompatibilityIssue.Should().BeTrue();
    }

    [Fact]
    public void ImportantMod_CreateWithGpuCheck_WithNullRequiredGpu_NoGpuRequirement()
    {
        // Act
        var mod = ImportantMod.CreateWithGpuCheck(
            "GenericMod",
            "Generic Mod",
            "Works with all GPUs",
            true,
            null,
            "nvidia");

        // Assert
        mod.HasGpuRequirement.Should().BeFalse();
        mod.RequiredGpuType.Should().BeNull();
        mod.HasGpuCompatibilityIssue.Should().BeFalse();
    }

    [Theory]
    [InlineData(true, false, "nvidia", "nvidia", ModStatus.Installed)]
    [InlineData(false, false, null, null, ModStatus.NotInstalled)]
    [InlineData(true, true, "nvidia", "amd", ModStatus.InstalledWithGpuIssue)]
    [InlineData(false, true, "nvidia", "amd", ModStatus.NotNeededForGpu)]
    public void ImportantMod_GetStatus_ReturnsCorrectStatus(
        bool isInstalled,
        bool hasGpuRequirement,
        string? requiredGpuType,
        string? detectedGpuType,
        ModStatus expectedStatus)
    {
        // Arrange
        var mod = new ImportantMod
        {
            ModId = "TestMod",
            DisplayName = "Test Mod",
            Recommendation = "Test",
            IsInstalled = isInstalled,
            HasGpuRequirement = hasGpuRequirement,
            RequiredGpuType = requiredGpuType,
            DetectedGpuType = detectedGpuType,
            HasGpuCompatibilityIssue = hasGpuRequirement && 
                                      !string.IsNullOrWhiteSpace(detectedGpuType) && 
                                      !string.Equals(requiredGpuType, detectedGpuType, StringComparison.OrdinalIgnoreCase)
        };

        // Act
        var status = mod.GetStatus();

        // Assert
        status.Should().Be(expectedStatus);
    }

    [Fact]
    public void ImportantMod_GetStatus_WithComplexGpuMismatchScenario_ReturnsCorrectStatus()
    {
        // Arrange - mod not installed but wrong GPU type
        var mod = ImportantMod.CreateWithGpuCheck(
            "NvidiaMod",
            "Nvidia Only Mod",
            "nvidia specific mod",
            false, // Not installed
            "nvidia",
            "amd"); // User has AMD GPU

        // Act
        var status = mod.GetStatus();

        // Assert - Should be NotNeededForGpu because user has wrong GPU type
        status.Should().Be(ModStatus.NotNeededForGpu);
    }

    #endregion

    #region ModStatus Enum Tests

    [Fact]
    public void ModStatus_HasExpectedValues()
    {
        // Assert
        Enum.GetValues<ModStatus>().Should().Contain(new[]
        {
            ModStatus.Installed,
            ModStatus.NotInstalled,
            ModStatus.InstalledWithGpuIssue,
            ModStatus.NotNeededForGpu
        });
    }

    #endregion

    #region Record Equality Tests

    [Fact]
    public void ModWarning_EqualityComparison_WorksCorrectly()
    {
        // Arrange
        var warning1 = ModWarning.Create("TestMod", "Warning", AnalysisSeverity.Warning, "plugin1", "FREQ");
        var warning2 = ModWarning.Create("TestMod", "Warning", AnalysisSeverity.Warning, "plugin1", "FREQ");
        var warning3 = ModWarning.Create("DifferentMod", "Warning", AnalysisSeverity.Warning, "plugin1", "FREQ");

        // Assert
        warning1.Should().Be(warning2); // Same values
        warning1.Should().NotBe(warning3); // Different mod name
    }

    [Fact]
    public void ModConflict_EqualityComparison_WorksCorrectly()
    {
        // Arrange
        var conflict1 = ModConflict.Create("Mod1", "Mod2", "Conflict");
        var conflict2 = ModConflict.Create("Mod1", "Mod2", "Conflict");
        var conflict3 = ModConflict.Create("Mod1", "Mod3", "Conflict");

        // Assert
        conflict1.Should().Be(conflict2); // Same values
        conflict1.Should().NotBe(conflict3); // Different second mod
    }

    [Fact]
    public void ImportantMod_EqualityComparison_WorksCorrectly()
    {
        // Arrange
        var mod1 = ImportantMod.Create("ModId", "Display", "Recommendation", true, "CORE");
        var mod2 = ImportantMod.Create("ModId", "Display", "Recommendation", true, "CORE");
        var mod3 = ImportantMod.Create("ModId", "Display", "Recommendation", false, "CORE");

        // Assert
        mod1.Should().Be(mod2); // Same values
        mod1.Should().NotBe(mod3); // Different installation status
    }

    #endregion
}