using FluentAssertions;
using Microsoft.Extensions.Logging;
using NSubstitute;
using Scanner111.Core.Analysis.Validators;
using Scanner111.Core.Models;
using Scanner111.Core.Reporting;

namespace Scanner111.Test.Analysis.Validators;

public class Buffout4SettingsValidatorTests
{
    private readonly ILogger<Buffout4SettingsValidator> _logger;
    private readonly Buffout4SettingsValidator _sut;

    public Buffout4SettingsValidatorTests()
    {
        _logger = Substitute.For<ILogger<Buffout4SettingsValidator>>();
        _sut = new Buffout4SettingsValidator(_logger);
    }

    #region ValidateComprehensive Tests

    [Fact]
    public void ValidateComprehensive_WithNullSettings_ThrowsArgumentNullException()
    {
        // Arrange
        CrashGenSettings settings = null!;

        // Act & Assert
        Assert.Throws<ArgumentNullException>(() => _sut.ValidateComprehensive(settings));
    }

    [Fact]
    public void ValidateComprehensive_WithValidBasicSettings_ReturnsSuccessReport()
    {
        // Arrange
        var settings = CreateValidSettings();

        // Act
        var result = _sut.ValidateComprehensive(settings);

        // Assert
        result.Should().NotBeNull();
        result.Type.Should().Be(FragmentType.Info);
        result.Content.Should().Contain("All settings are properly configured");
    }

    [Fact]
    public void ValidateComprehensive_WithMissingCriticalSettings_ReturnsWarnings()
    {
        // Arrange
        var settings = new CrashGenSettings
        {
            CrashGenName = "Buffout4",
            RawSettings = new Dictionary<string, object>
            {
                ["Achievements"] = true
                // Missing MemoryManager, ArchiveLimit, etc.
            }
        };

        // Act
        var result = _sut.ValidateComprehensive(settings);

        // Assert
        result.Should().NotBeNull();
        result.Type.Should().Be(FragmentType.Warning);
        result.Content.Should().Contain("Critical setting");
        result.Content.Should().Contain("MemoryManager");
    }

    [Fact]
    public void ValidateComprehensive_WithBakaScrapHeapConflict_ReturnsError()
    {
        // Arrange
        var settings = CreateValidSettings();
        var modSettings = new ModDetectionSettings
        {
            HasBakaScrapHeap = true
        };

        // Act
        var result = _sut.ValidateComprehensive(settings, modSettings);

        // Assert
        result.Should().NotBeNull();
        result.Type.Should().Be(FragmentType.Error);
        result.Content.Should().Contain("Baka ScrapHeap");
        result.Content.Should().Contain("conflicts");
    }

    [Fact]
    public void ValidateComprehensive_WithDebugSettingsInProduction_ReturnsError()
    {
        // Arrange
        var settings = new CrashGenSettings
        {
            CrashGenName = "Buffout4",
            RawSettings = new Dictionary<string, object>
            {
                ["MemoryManagerDebug"] = true,
                ["WaitForDebugger"] = true,
                ["MemoryManager"] = true
            }
        };

        // Act
        var result = _sut.ValidateComprehensive(settings);

        // Assert
        result.Should().NotBeNull();
        result.Content.Should().Contain("Debug settings");
        result.Content.Should().Contain("production");
    }

    #endregion

    #region Critical Settings Tests

    [Fact]
    public void ValidateCriticalSettings_AllPresent_NoIssues()
    {
        // Arrange
        var settings = new CrashGenSettings
        {
            CrashGenName = "Buffout4",
            RawSettings = new Dictionary<string, object>
            {
                ["MemoryManager"] = true,
                ["Achievements"] = false,
                ["F4EE"] = true,
                ["ArchiveLimit"] = false,
                ["BSTextureStreamerLocalHeap"] = false,
                ["SmallBlockAllocator"] = true,
                ["ScaleformAllocator"] = true,
                ["HavokMemorySystem"] = true
            }
        };

        // Act
        var result = _sut.ValidateComprehensive(settings);

        // Assert
        result.Should().NotBeNull();
        result.Type.Should().Be(FragmentType.Info);
        result.Content.Should().Contain("All settings are properly configured");
    }

    [Theory]
    [InlineData("MemoryManager")]
    [InlineData("Achievements")]
    [InlineData("F4EE")]
    [InlineData("ArchiveLimit")]
    [InlineData("BSTextureStreamerLocalHeap")]
    [InlineData("SmallBlockAllocator")]
    [InlineData("ScaleformAllocator")]
    [InlineData("HavokMemorySystem")]
    public void ValidateCriticalSettings_MissingSetting_ReturnsWarning(string missingSetting)
    {
        // Arrange
        var allSettings = new Dictionary<string, object>
        {
            ["MemoryManager"] = true,
            ["Achievements"] = false,
            ["F4EE"] = true,
            ["ArchiveLimit"] = false,
            ["BSTextureStreamerLocalHeap"] = false,
            ["SmallBlockAllocator"] = true,
            ["ScaleformAllocator"] = true,
            ["HavokMemorySystem"] = true
        };
        allSettings.Remove(missingSetting);

        var settings = new CrashGenSettings
        {
            CrashGenName = "Buffout4",
            RawSettings = allSettings
        };

        // Act
        var result = _sut.ValidateComprehensive(settings);

        // Assert
        result.Should().NotBeNull();
        result.Content.Should().Contain(missingSetting);
        result.Content.Should().Contain("missing");
    }

    #endregion

    #region Setting Dependencies Tests

    [Fact]
    public void ValidateSettingDependencies_MemoryManagerWithoutDependencies_ReturnsWarning()
    {
        // Arrange
        var settings = new CrashGenSettings
        {
            CrashGenName = "Buffout4",
            RawSettings = new Dictionary<string, object>
            {
                ["MemoryManager"] = true
                // Missing SmallBlockAllocator and ScaleformAllocator
            }
        };

        // Act
        var result = _sut.ValidateComprehensive(settings);

        // Assert
        result.Should().NotBeNull();
        result.Type.Should().Be(FragmentType.Warning);
        result.Content.Should().Contain("SmallBlockAllocator");
        result.Content.Should().Contain("ScaleformAllocator");
    }

    [Fact]
    public void ValidateSettingDependencies_MemoryManagerWithDependencies_NoIssues()
    {
        // Arrange
        var settings = new CrashGenSettings
        {
            CrashGenName = "Buffout4",
            RawSettings = new Dictionary<string, object>
            {
                ["MemoryManager"] = true,
                ["SmallBlockAllocator"] = true,
                ["ScaleformAllocator"] = true
            }
        };

        // Act
        var result = _sut.ValidateComprehensive(settings);

        // Assert
        result.Should().NotBeNull();
        result.Content.Should().NotContain("SmallBlockAllocator");
        result.Content.Should().NotContain("ScaleformAllocator");
    }

    [Fact]
    public void ValidateSettingDependencies_MemoryManagerWithWrongDependencyValues_ReturnsWarning()
    {
        // Arrange
        var settings = new CrashGenSettings
        {
            CrashGenName = "Buffout4",
            RawSettings = new Dictionary<string, object>
            {
                ["MemoryManager"] = true,
                ["SmallBlockAllocator"] = false, // Should be true
                ["ScaleformAllocator"] = false   // Should be true
            }
        };

        // Act
        var result = _sut.ValidateComprehensive(settings);

        // Assert
        result.Should().NotBeNull();
        result.Type.Should().Be(FragmentType.Warning);
        result.Content.Should().Contain("requires SmallBlockAllocator to be True");
        result.Content.Should().Contain("requires ScaleformAllocator to be True");
    }

    [Fact]
    public void ValidateSettingDependencies_F4EEEnabled_ChecksForCompatibilitySection()
    {
        // Arrange
        var settings = new CrashGenSettings
        {
            CrashGenName = "Buffout4",
            RawSettings = new Dictionary<string, object>
            {
                ["F4EE"] = true
            }
        };

        // Act
        var result = _sut.ValidateComprehensive(settings);

        // Assert
        result.Should().NotBeNull();
        result.Content.Should().Contain("F4EE");
        result.Content.Should().Contain("Compatibility");
    }

    #endregion

    #region Performance Impact Tests

    [Fact]
    public void AnalyzePerformanceImpact_MemoryManagerDebugEnabled_ReturnsWarning()
    {
        // Arrange
        var settings = new CrashGenSettings
        {
            CrashGenName = "Buffout4",
            RawSettings = new Dictionary<string, object>
            {
                ["MemoryManagerDebug"] = true
            }
        };

        // Act
        var result = _sut.ValidateComprehensive(settings);

        // Assert
        result.Should().NotBeNull();
        result.Content.Should().Contain("MemoryManagerDebug");
        result.Content.Should().Contain("performance");
        result.Content.Should().Contain("Debug mode significantly impacts performance");
    }

    [Fact]
    public void AnalyzePerformanceImpact_BSTextureStreamerLocalHeapEnabled_ReturnsInfo()
    {
        // Arrange
        var settings = new CrashGenSettings
        {
            CrashGenName = "Buffout4",
            RawSettings = new Dictionary<string, object>
            {
                ["BSTextureStreamerLocalHeap"] = true
            }
        };

        // Act
        var result = _sut.ValidateComprehensive(settings);

        // Assert
        result.Should().NotBeNull();
        result.Content.Should().Contain("BSTextureStreamerLocalHeap");
        result.Content.Should().Contain("texture streaming");
    }

    [Theory]
    [InlineData("ActorIsHostileToActor", true)]
    [InlineData("MaxStdIO", 4096)]
    public void AnalyzePerformanceImpact_PerformanceSettings_ReturnsAppropriateWarning(string setting, object value)
    {
        // Arrange
        var settings = new CrashGenSettings
        {
            CrashGenName = "Buffout4",
            RawSettings = new Dictionary<string, object>
            {
                [setting] = value
            }
        };

        // Act
        var result = _sut.ValidateComprehensive(settings);

        // Assert
        result.Should().NotBeNull();
        result.Content.Should().Contain(setting);
    }

    #endregion

    #region Setting Combinations Tests

    [Fact]
    public void ValidateSettingCombinations_MemoryManagerAndBSTextureStreamerBothEnabled_ReturnsWarning()
    {
        // Arrange
        var settings = new CrashGenSettings
        {
            CrashGenName = "Buffout4",
            RawSettings = new Dictionary<string, object>
            {
                ["MemoryManager"] = true,
                ["BSTextureStreamerLocalHeap"] = true
            }
        };

        // Act
        var result = _sut.ValidateComprehensive(settings);

        // Assert
        result.Should().NotBeNull();
        result.Type.Should().Be(FragmentType.Warning);
        result.Content.Should().Contain("BSTextureStreamerLocalHeap should typically be FALSE");
    }

    [Fact]
    public void ValidateSettingCombinations_ArchiveLimitWithLowMaxStdIO_ReturnsInfo()
    {
        // Arrange
        var settings = new CrashGenSettings
        {
            CrashGenName = "Buffout4",
            RawSettings = new Dictionary<string, object>
            {
                ["ArchiveLimit"] = true,
                ["MaxStdIO"] = 1024
            }
        };

        // Act
        var result = _sut.ValidateComprehensive(settings);

        // Assert
        result.Should().NotBeNull();
        result.Content.Should().Contain("ArchiveLimit");
        result.Content.Should().Contain("MaxStdIO");
        result.Content.Should().Contain("Consider increasing MaxStdIO");
    }

    [Fact]
    public void ValidateSettingCombinations_ArchiveLimitWithHighMaxStdIO_NoWarning()
    {
        // Arrange
        var settings = new CrashGenSettings
        {
            CrashGenName = "Buffout4",
            RawSettings = new Dictionary<string, object>
            {
                ["ArchiveLimit"] = true,
                ["MaxStdIO"] = 4096
            }
        };

        // Act
        var result = _sut.ValidateComprehensive(settings);

        // Assert
        result.Should().NotBeNull();
        result.Content.Should().NotContain("Consider increasing MaxStdIO");
    }

    #endregion

    #region Value Range Validation Tests

    [Theory]
    [InlineData(512, "quite low")]
    [InlineData(256, "quite low")]
    [InlineData(1, "quite low")]
    public void ValidateValueRanges_MaxStdIOTooLow_ReturnsInfo(int value, string expectedMessage)
    {
        // Arrange
        var settings = new CrashGenSettings
        {
            CrashGenName = "Buffout4",
            RawSettings = new Dictionary<string, object>
            {
                ["MaxStdIO"] = value
            }
        };

        // Act
        var result = _sut.ValidateComprehensive(settings);

        // Assert
        result.Should().NotBeNull();
        result.Content.Should().Contain("MaxStdIO");
        result.Content.Should().Contain(expectedMessage);
    }

    [Theory]
    [InlineData(10000, "very high")]
    [InlineData(16384, "very high")]
    [InlineData(32768, "very high")]
    public void ValidateValueRanges_MaxStdIOTooHigh_ReturnsWarning(int value, string expectedMessage)
    {
        // Arrange
        var settings = new CrashGenSettings
        {
            CrashGenName = "Buffout4",
            RawSettings = new Dictionary<string, object>
            {
                ["MaxStdIO"] = value
            }
        };

        // Act
        var result = _sut.ValidateComprehensive(settings);

        // Assert
        result.Should().NotBeNull();
        result.Content.Should().Contain("MaxStdIO");
        result.Content.Should().Contain(expectedMessage);
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(2048)]
    [InlineData(4096)]
    [InlineData(8192)]
    public void ValidateValueRanges_MaxStdIOValid_NoWarning(int value)
    {
        // Arrange
        var settings = new CrashGenSettings
        {
            CrashGenName = "Buffout4",
            RawSettings = new Dictionary<string, object>
            {
                ["MaxStdIO"] = value
            }
        };

        // Act
        var result = _sut.ValidateComprehensive(settings);

        // Assert
        result.Should().NotBeNull();
        // Should not contain MaxStdIO warnings
        if (result.Content.Contains("MaxStdIO"))
        {
            result.Content.Should().NotContain("very high");
            result.Content.Should().NotContain("quite low");
        }
    }

    #endregion

    #region Mod-Specific Settings Tests

    [Fact]
    public void ValidateModSpecificSettings_BakaScrapHeapWithMemoryManager_ReturnsError()
    {
        // Arrange
        var settings = new CrashGenSettings
        {
            CrashGenName = "Buffout4",
            RawSettings = new Dictionary<string, object>
            {
                ["MemoryManager"] = true
            }
        };
        var modSettings = new ModDetectionSettings
        {
            HasBakaScrapHeap = true
        };

        // Act
        var result = _sut.ValidateComprehensive(settings, modSettings);

        // Assert
        result.Should().NotBeNull();
        result.Type.Should().Be(FragmentType.Error);
        result.Content.Should().Contain("MemoryManager conflicts with Baka ScrapHeap");
    }

    [Fact]
    public void ValidateModSpecificSettings_AchievementsModWithAchievementsEnabled_ReturnsWarning()
    {
        // Arrange
        var settings = new CrashGenSettings
        {
            CrashGenName = "Buffout4",
            RawSettings = new Dictionary<string, object>
            {
                ["Achievements"] = true
            }
        };
        var modSettings = ModDetectionSettings.CreateWithXseModules(new[] { "achievements.dll" });

        // Act
        var result = _sut.ValidateComprehensive(settings, modSettings);

        // Assert
        result.Should().NotBeNull();
        result.Type.Should().Be(FragmentType.Warning);
        result.Content.Should().Contain("Achievements parameter conflicts");
    }

    [Fact]
    public void ValidateModSpecificSettings_UnlimitedSurvivalModeWithAchievements_ReturnsWarning()
    {
        // Arrange
        var settings = new CrashGenSettings
        {
            CrashGenName = "Buffout4",
            RawSettings = new Dictionary<string, object>
            {
                ["Achievements"] = true
            }
        };
        var modSettings = ModDetectionSettings.CreateWithXseModules(new[] { "unlimitedsurvivalmode.dll" });

        // Act
        var result = _sut.ValidateComprehensive(settings, modSettings);

        // Assert
        result.Should().NotBeNull();
        result.Type.Should().Be(FragmentType.Warning);
        result.Content.Should().Contain("Achievements parameter conflicts");
    }

    [Fact]
    public void ValidateModSpecificSettings_LooksMenuWithoutF4EE_ReturnsError()
    {
        // Arrange
        var settings = new CrashGenSettings
        {
            CrashGenName = "Buffout4",
            RawSettings = new Dictionary<string, object>
            {
                ["F4EE"] = false
            }
        };
        var modSettings = ModDetectionSettings.CreateWithXseModules(new[] { "f4ee.dll" });

        // Act
        var result = _sut.ValidateComprehensive(settings, modSettings);

        // Assert
        result.Should().NotBeNull();
        result.Type.Should().Be(FragmentType.Error);
        result.Content.Should().Contain("Looks Menu");
        result.Content.Should().Contain("F4EE is not enabled");
    }

    [Fact]
    public void ValidateModSpecificSettings_WorkshopFrameworkWithoutFixes_ReturnsInfo()
    {
        // Arrange
        var settings = new CrashGenSettings
        {
            CrashGenName = "Buffout4",
            RawSettings = new Dictionary<string, object>
            {
                ["WorkshopMenu"] = false
            }
        };
        var modSettings = new ModDetectionSettings
        {
            CrashLogPlugins = new Dictionary<string, string>
            {
                ["WorkshopFramework"] = "1.0.0"
            }
        };

        // Act
        var result = _sut.ValidateComprehensive(settings, modSettings);

        // Assert
        result.Should().NotBeNull();
        result.Content.Should().Contain("Workshop Framework");
        result.Content.Should().Contain("WorkshopMenu");
    }

    #endregion

    #region TOML Parsing Tests

    [Fact]
    public void ValidateSettings_WithMalformedValues_HandlesGracefully()
    {
        // Arrange
        var settings = new CrashGenSettings
        {
            CrashGenName = "Buffout4",
            RawSettings = new Dictionary<string, object>
            {
                ["MemoryManager"] = "yes",  // String instead of bool
                ["MaxStdIO"] = "invalid",    // Invalid int
                ["F4EE"] = 1                 // Int instead of bool
            }
        };

        // Act
        var result = _sut.ValidateComprehensive(settings);

        // Assert
        result.Should().NotBeNull();
        // Should handle type conversions gracefully
    }

    [Fact]
    public void ValidateSettings_WithEmptySettings_ReturnsMultipleWarnings()
    {
        // Arrange
        var settings = new CrashGenSettings
        {
            CrashGenName = "Buffout4",
            RawSettings = new Dictionary<string, object>()
        };

        // Act
        var result = _sut.ValidateComprehensive(settings);

        // Assert
        result.Should().NotBeNull();
        result.Type.Should().Be(FragmentType.Warning);
        result.Content.Should().Contain("Critical setting");
        result.Content.Should().Contain("missing");
    }

    #endregion

    #region Papyrus Settings Tests

    [Fact]
    public void ValidateSettings_WithPapyrusSettings_ValidatesCorrectly()
    {
        // Arrange
        var settings = new CrashGenSettings
        {
            CrashGenName = "Buffout4",
            RawSettings = new Dictionary<string, object>
            {
                ["PapyrusStackOverflow"] = true,
                ["PapyrusInfiniteLoop"] = true
            }
        };

        // Act
        var result = _sut.ValidateComprehensive(settings);

        // Assert
        result.Should().NotBeNull();
        // No specific warnings for these settings
    }

    #endregion

    #region F4EE/LooksMenu Settings Tests

    [Fact]
    public void ValidateSettings_F4EEEnabledWithoutMod_ReturnsInfo()
    {
        // Arrange
        var settings = new CrashGenSettings
        {
            CrashGenName = "Buffout4",
            RawSettings = new Dictionary<string, object>
            {
                ["F4EE"] = true
            }
        };
        var modSettings = new ModDetectionSettings(); // No f4ee.dll

        // Act
        var result = _sut.ValidateComprehensive(settings, modSettings);

        // Assert
        result.Should().NotBeNull();
        // F4EE enabled but mod not detected - not necessarily an error
    }

    #endregion

    #region ENB Settings Tests

    [Theory]
    [InlineData("ENBSeries")]
    [InlineData("ComplexParallax")]
    [InlineData("ParallaxOcclusion")]
    public void ValidateSettings_WithENBRelatedSettings_ValidatesCorrectly(string setting)
    {
        // Arrange
        var settings = new CrashGenSettings
        {
            CrashGenName = "Buffout4",
            RawSettings = new Dictionary<string, object>
            {
                [setting] = true
            }
        };

        // Act
        var result = _sut.ValidateComprehensive(settings);

        // Assert
        result.Should().NotBeNull();
        // Should handle ENB-related settings
    }

    #endregion

    #region Report Building Tests

    [Fact]
    public void BuildValidationReport_WithMultipleIssues_ProperlyFormatsReport()
    {
        // Arrange
        var settings = new CrashGenSettings
        {
            CrashGenName = "Buffout4",
            RawSettings = new Dictionary<string, object>
            {
                ["MemoryManagerDebug"] = true,
                ["MaxStdIO"] = 100,
                ["MemoryManager"] = true,
                ["BSTextureStreamerLocalHeap"] = true
            }
        };

        // Act
        var result = _sut.ValidateComprehensive(settings);

        // Assert
        result.Should().NotBeNull();
        result.Content.Should().Contain("❌"); // Error marker
        result.Content.Should().Contain("⚠️"); // Warning marker
        result.Content.Should().Contain("ℹ️"); // Info marker
        result.Content.Should().Contain("→"); // Recommendation arrow
    }

    [Fact]
    public void BuildValidationReport_AllSettingsValid_ReturnsSuccessMessage()
    {
        // Arrange
        var settings = CreateValidSettings();

        // Act
        var result = _sut.ValidateComprehensive(settings);

        // Assert
        result.Should().NotBeNull();
        result.Type.Should().Be(FragmentType.Info);
        result.Content.Should().Contain("All settings are properly configured");
        result.Content.Should().Contain("no issues detected");
    }

    #endregion

    #region Edge Cases and Boundary Tests

    [Fact]
    public void ValidateSettings_WithNullModSettings_HandlesGracefully()
    {
        // Arrange
        var settings = CreateValidSettings();
        ModDetectionSettings modSettings = null;

        // Act
        var result = _sut.ValidateComprehensive(settings, modSettings);

        // Assert
        result.Should().NotBeNull();
        // Should still validate basic settings without mod-specific checks
    }

    [Fact]
    public void ValidateSettings_WithCaseInsensitiveKeys_WorksCorrectly()
    {
        // Arrange
        var settings = new CrashGenSettings
        {
            CrashGenName = "Buffout4",
            RawSettings = new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase)
            {
                ["memorymanager"] = true,
                ["ACHIEVEMENTS"] = false,
                ["F4ee"] = true
            }
        };

        // Act
        var result = _sut.ValidateComprehensive(settings);

        // Assert
        result.Should().NotBeNull();
        // Should handle case-insensitive keys properly
    }

    [Fact]
    public void ValidateSettings_WithSpecialCharactersInValues_HandlesGracefully()
    {
        // Arrange
        var settings = new CrashGenSettings
        {
            CrashGenName = "Buffout4",
            RawSettings = new Dictionary<string, object>
            {
                ["CustomSetting"] = "Value with special chars: !@#$%^&*()",
                ["MemoryManager"] = true
            }
        };

        // Act
        var result = _sut.ValidateComprehensive(settings);

        // Assert
        result.Should().NotBeNull();
        // Should not crash on special characters
    }

    #endregion

    #region Helper Methods

    private static CrashGenSettings CreateValidSettings()
    {
        return new CrashGenSettings
        {
            CrashGenName = "Buffout4",
            Version = new Version(1, 30, 0),
            RawSettings = new Dictionary<string, object>
            {
                ["MemoryManager"] = true,
                ["Achievements"] = false,
                ["F4EE"] = true,
                ["ArchiveLimit"] = false,
                ["BSTextureStreamerLocalHeap"] = false,
                ["SmallBlockAllocator"] = true,
                ["ScaleformAllocator"] = true,
                ["HavokMemorySystem"] = true,
                ["MaxStdIO"] = -1
            }
        };
    }

    #endregion
}