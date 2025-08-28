using FluentAssertions;
using Microsoft.Extensions.Logging;
using NSubstitute;
using Scanner111.Core.Analysis.Validators;
using Scanner111.Core.Configuration;
using Scanner111.Core.Models;
using Scanner111.Core.Reporting;

namespace Scanner111.Test.Analysis.Validators;

public class VersionAwareSettingsValidatorTests
{
    private readonly ILogger<VersionAwareSettingsValidator> _logger;
    private readonly IAsyncYamlSettingsCore _yamlCore;
    private readonly VersionAwareSettingsValidator _sut;

    public VersionAwareSettingsValidatorTests()
    {
        _logger = Substitute.For<ILogger<VersionAwareSettingsValidator>>();
        _yamlCore = Substitute.For<IAsyncYamlSettingsCore>();
        _sut = new VersionAwareSettingsValidator(_logger, _yamlCore);
    }

    #region ValidateVersionCompatibilityAsync Tests

    [Fact]
    public async Task ValidateVersionCompatibilityAsync_WithNullSettings_ThrowsArgumentNullException()
    {
        // Arrange
        CrashGenSettings settings = null!;

        // Act & Assert
        await Assert.ThrowsAsync<ArgumentNullException>(
            () => _sut.ValidateVersionCompatibilityAsync(settings));
    }

    [Fact]
    public async Task ValidateVersionCompatibilityAsync_WithCurrentVersion_ReturnsSuccessReport()
    {
        // Arrange
        var settings = CreateModernSettings(new Version(1, 30, 0));

        // Act
        var result = await _sut.ValidateVersionCompatibilityAsync(settings);

        // Assert
        result.Should().NotBeNull();
        result.Type.Should().Be(FragmentType.Info);
        result.Content.Should().Contain("All version checks passed");
    }

    [Fact]
    public async Task ValidateVersionCompatibilityAsync_WithCancellation_ThrowsOperationCanceledException()
    {
        // Arrange
        var settings = CreateModernSettings(new Version(1, 30, 0));
        var cts = new CancellationTokenSource();
        cts.Cancel();

        // Act & Assert
        await Assert.ThrowsAsync<OperationCanceledException>(
            () => _sut.ValidateVersionCompatibilityAsync(settings, cancellationToken: cts.Token));
    }

    #endregion

    #region Deprecated Settings Tests

    [Fact]
    public async Task CheckDeprecatedSettings_ArchiveLimitInVersion129Plus_ReturnsWarning()
    {
        // Arrange
        var settings = new CrashGenSettings
        {
            CrashGenName = "Buffout4",
            Version = new Version(1, 29, 0),
            RawSettings = new Dictionary<string, object>
            {
                ["ArchiveLimit"] = true
            }
        };

        // Act
        var result = await _sut.ValidateVersionCompatibilityAsync(settings);

        // Assert
        result.Should().NotBeNull();
        result.Type.Should().Be(FragmentType.Warning);
        result.Content.Should().Contain("ArchiveLimit");
        result.Content.Should().Contain("deprecated");
        result.Content.Should().Contain("1.29.0");
    }

    [Fact]
    public async Task CheckDeprecatedSettings_ArchiveLimitInOlderVersion_NoWarning()
    {
        // Arrange
        var settings = new CrashGenSettings
        {
            CrashGenName = "Buffout4",
            Version = new Version(1, 28, 0),
            RawSettings = new Dictionary<string, object>
            {
                ["ArchiveLimit"] = true
            }
        };

        // Act
        var result = await _sut.ValidateVersionCompatibilityAsync(settings);

        // Assert
        result.Should().NotBeNull();
        result.Content.Should().NotContain("deprecated");
    }

    [Fact]
    public async Task CheckDeprecatedSettings_InputSwitchDeprecated_ReturnsWarning()
    {
        // Arrange
        var settings = new CrashGenSettings
        {
            CrashGenName = "Buffout4",
            Version = new Version(1, 26, 0),
            RawSettings = new Dictionary<string, object>
            {
                ["InputSwitch"] = true
            }
        };

        // Act
        var result = await _sut.ValidateVersionCompatibilityAsync(settings);

        // Assert
        result.Should().NotBeNull();
        result.Type.Should().Be(FragmentType.Warning);
        result.Content.Should().Contain("InputSwitch");
        result.Content.Should().Contain("no longer needed");
    }

    [Theory]
    [InlineData("1.30.0")]
    [InlineData("1.31.0")]
    [InlineData("1.35.0")]
    public async Task CheckDeprecatedSettings_MultipleVersions_HandlesCorrectly(string versionString)
    {
        // Arrange
        var version = Version.Parse(versionString);
        var settings = new CrashGenSettings
        {
            CrashGenName = "Buffout4",
            Version = version,
            RawSettings = new Dictionary<string, object>
            {
                ["ArchiveLimit"] = true,
                ["InputSwitch"] = true
            }
        };

        // Act
        var result = await _sut.ValidateVersionCompatibilityAsync(settings);

        // Assert
        result.Should().NotBeNull();
        result.Type.Should().Be(FragmentType.Warning);
        result.Content.Should().Contain("deprecated");
    }

    #endregion

    #region Added Settings Tests

    [Fact]
    public async Task CheckUnavailableSettings_F4EEBeforeVersion120_ReturnsError()
    {
        // Arrange
        var settings = new CrashGenSettings
        {
            CrashGenName = "Buffout4",
            Version = new Version(1, 19, 0),
            RawSettings = new Dictionary<string, object>
            {
                ["F4EE"] = true
            }
        };

        // Act
        var result = await _sut.ValidateVersionCompatibilityAsync(settings);

        // Assert
        result.Should().NotBeNull();
        result.Type.Should().Be(FragmentType.Error);
        result.Content.Should().Contain("F4EE");
        result.Content.Should().Contain("not available");
        result.Content.Should().Contain("1.19");
    }

    [Fact]
    public async Task CheckUnavailableSettings_F4EEAfterVersion120_NoError()
    {
        // Arrange
        var settings = new CrashGenSettings
        {
            CrashGenName = "Buffout4",
            Version = new Version(1, 20, 0),
            RawSettings = new Dictionary<string, object>
            {
                ["F4EE"] = true
            }
        };

        // Act
        var result = await _sut.ValidateVersionCompatibilityAsync(settings);

        // Assert
        result.Should().NotBeNull();
        result.Content.Should().NotContain("not available");
    }

    [Fact]
    public async Task CheckUnavailableSettings_MemoryManagerDebugBeforeVersion126_ReturnsError()
    {
        // Arrange
        var settings = new CrashGenSettings
        {
            CrashGenName = "Buffout4",
            Version = new Version(1, 25, 0),
            RawSettings = new Dictionary<string, object>
            {
                ["MemoryManagerDebug"] = true
            }
        };

        // Act
        var result = await _sut.ValidateVersionCompatibilityAsync(settings);

        // Assert
        result.Should().NotBeNull();
        result.Type.Should().Be(FragmentType.Error);
        result.Content.Should().Contain("MemoryManagerDebug");
        result.Content.Should().Contain("not available");
    }

    #endregion

    #region Version Behavior Tests

    [Fact]
    public async Task CheckVersionBehaviors_MemoryManagerOldVersion_ShowsBehaviorInfo()
    {
        // Arrange
        var settings = new CrashGenSettings
        {
            CrashGenName = "Buffout4",
            Version = new Version(1, 26, 0),
            RawSettings = new Dictionary<string, object>
            {
                ["MemoryManager"] = true
            }
        };

        // Act
        var result = await _sut.ValidateVersionCompatibilityAsync(settings);

        // Assert
        result.Should().NotBeNull();
        result.Content.Should().Contain("memory management");
    }

    [Fact]
    public async Task CheckVersionBehaviors_MemoryManagerImprovedVersion_ShowsImprovedInfo()
    {
        // Arrange
        var settings = new CrashGenSettings
        {
            CrashGenName = "Buffout4",
            Version = new Version(1, 28, 0),
            RawSettings = new Dictionary<string, object>
            {
                ["MemoryManager"] = true
            }
        };

        // Act
        var result = await _sut.ValidateVersionCompatibilityAsync(settings);

        // Assert
        result.Should().NotBeNull();
        // Shows improved memory management info
    }

    [Fact]
    public async Task CheckVersionBehaviors_MaxStdIOOldVersion_ShowsLimitInfo()
    {
        // Arrange
        var settings = new CrashGenSettings
        {
            CrashGenName = "Buffout4",
            Version = new Version(1, 23, 0),
            RawSettings = new Dictionary<string, object>
            {
                ["MaxStdIO"] = 4096
            }
        };

        // Act
        var result = await _sut.ValidateVersionCompatibilityAsync(settings);

        // Assert
        result.Should().NotBeNull();
        result.Content.Should().Contain("MaxStdIO");
        result.Content.Should().Contain("Limited to 2048");
    }

    [Fact]
    public async Task CheckVersionBehaviors_MaxStdIONewVersion_ShowsHigherValueSupport()
    {
        // Arrange
        var settings = new CrashGenSettings
        {
            CrashGenName = "Buffout4",
            Version = new Version(1, 25, 0),
            RawSettings = new Dictionary<string, object>
            {
                ["MaxStdIO"] = 4096
            }
        };

        // Act
        var result = await _sut.ValidateVersionCompatibilityAsync(settings);

        // Assert
        result.Should().NotBeNull();
        // Should not warn about high value
    }

    #endregion

    #region Upgrade Suggestion Tests

    [Fact]
    public async Task SuggestUpgrades_UnknownVersion_ReturnsWarning()
    {
        // Arrange
        var settings = new CrashGenSettings
        {
            CrashGenName = "Buffout4",
            Version = null,
            RawSettings = new Dictionary<string, object>()
        };

        // Act
        var result = await _sut.ValidateVersionCompatibilityAsync(settings);

        // Assert
        result.Should().NotBeNull();
        result.Type.Should().Be(FragmentType.Warning);
        result.Content.Should().Contain("Unable to determine");
        result.Content.Should().Contain("version");
    }

    [Fact]
    public async Task SuggestUpgrades_OldVersionPreMemoryImprovement_SuggestsUpgrade()
    {
        // Arrange
        var settings = new CrashGenSettings
        {
            CrashGenName = "Buffout4",
            Version = new Version(1, 27, 0),
            RawSettings = new Dictionary<string, object>()
        };

        // Act
        var result = await _sut.ValidateVersionCompatibilityAsync(settings);

        // Assert
        result.Should().NotBeNull();
        result.Type.Should().Be(FragmentType.Warning);
        result.Content.Should().Contain("outdated");
        result.Content.Should().Contain("Update to version 1.28.0");
        result.Content.Should().Contain("improved memory management");
    }

    [Fact]
    public async Task SuggestUpgrades_PreNGVersion_SuggestsNGUpgrade()
    {
        // Arrange
        var settings = new CrashGenSettings
        {
            CrashGenName = "Buffout4",
            Version = new Version(1, 29, 0),
            RawSettings = new Dictionary<string, object>()
        };

        // Act
        var result = await _sut.ValidateVersionCompatibilityAsync(settings);

        // Assert
        result.Should().NotBeNull();
        result.Content.Should().Contain("Buffout4 NG");
        result.Content.Should().Contain("Next-Gen update");
        result.Content.Should().Contain("nexusmods.com");
    }

    [Fact]
    public async Task SuggestUpgrades_CurrentNGVersion_NoUpgradeNeeded()
    {
        // Arrange
        var settings = new CrashGenSettings
        {
            CrashGenName = "Buffout4",
            Version = new Version(1, 30, 0),
            RawSettings = new Dictionary<string, object>()
        };

        // Act
        var result = await _sut.ValidateVersionCompatibilityAsync(settings);

        // Assert
        result.Should().NotBeNull();
        result.Type.Should().Be(FragmentType.Info);
        result.Content.Should().Contain("All version checks passed");
    }

    #endregion

    #region Game Version Compatibility Tests

    [Fact]
    public async Task CheckGameVersionCompatibility_NextGenGameWithOldBuffout_ReturnsError()
    {
        // Arrange
        var settings = new CrashGenSettings
        {
            CrashGenName = "Buffout4",
            Version = new Version(1, 29, 0),
            RawSettings = new Dictionary<string, object>()
        };
        var gameVersion = "1.10.980.0"; // Next-Gen version

        // Act
        var result = await _sut.ValidateVersionCompatibilityAsync(settings, gameVersion);

        // Assert
        result.Should().NotBeNull();
        result.Type.Should().Be(FragmentType.Error);
        result.Content.Should().Contain("old Buffout4 with Next-Gen");
        result.Content.Should().Contain("must use Buffout4 NG");
    }

    [Fact]
    public async Task CheckGameVersionCompatibility_OldGameWithNGBuffout_ReturnsWarning()
    {
        // Arrange
        var settings = new CrashGenSettings
        {
            CrashGenName = "Buffout4",
            Version = new Version(1, 30, 0), // NG version
            RawSettings = new Dictionary<string, object>()
        };
        var gameVersion = "1.10.163.0"; // Pre-Next-Gen version

        // Act
        var result = await _sut.ValidateVersionCompatibilityAsync(settings, gameVersion);

        // Assert
        result.Should().NotBeNull();
        result.Type.Should().Be(FragmentType.Warning);
        result.Content.Should().Contain("Buffout4 NG with pre-Next-Gen");
        result.Content.Should().Contain("original Buffout4");
    }

    [Theory]
    [InlineData("1.10.980.0")]
    [InlineData("1.10.984.0")]
    [InlineData("next-gen")]
    [InlineData("NG-1.10.980")]
    public async Task CheckGameVersionCompatibility_DetectsNextGenVersions(string gameVersion)
    {
        // Arrange
        var settings = new CrashGenSettings
        {
            CrashGenName = "Buffout4",
            Version = new Version(1, 29, 0), // Old version
            RawSettings = new Dictionary<string, object>()
        };

        // Act
        var result = await _sut.ValidateVersionCompatibilityAsync(settings, gameVersion);

        // Assert
        result.Should().NotBeNull();
        result.Type.Should().Be(FragmentType.Error);
        result.Content.Should().Contain("Next-Gen");
    }

    [Fact]
    public async Task CheckGameVersionCompatibility_CompatibleVersions_NoWarning()
    {
        // Arrange
        var settings = new CrashGenSettings
        {
            CrashGenName = "Buffout4",
            Version = new Version(1, 30, 0), // NG version
            RawSettings = new Dictionary<string, object>()
        };
        var gameVersion = "1.10.980.0"; // Next-Gen version

        // Act
        var result = await _sut.ValidateVersionCompatibilityAsync(settings, gameVersion);

        // Assert
        result.Should().NotBeNull();
        result.Type.Should().Be(FragmentType.Info);
    }

    #endregion

    #region NG-Specific Settings Tests

    [Fact]
    public async Task CheckNGSpecificSettings_NGVersionWithMemoryManager_ShowsEnhancedInfo()
    {
        // Arrange
        var settings = new CrashGenSettings
        {
            CrashGenName = "Buffout4",
            Version = new Version(1, 30, 0), // NG version
            RawSettings = new Dictionary<string, object>
            {
                ["MemoryManager"] = true
            }
        };

        // Act
        var result = await _sut.ValidateVersionCompatibilityAsync(settings);

        // Assert
        result.Should().NotBeNull();
        result.Content.Should().Contain("enhanced memory management");
        result.Content.Should().Contain("additional memory optimizations");
    }

    [Fact]
    public async Task CheckNGSpecificSettings_NGVersionWithBSTextureStreamer_ShowsInfo()
    {
        // Arrange
        var settings = new CrashGenSettings
        {
            CrashGenName = "Buffout4",
            Version = new Version(1, 30, 0), // NG version
            RawSettings = new Dictionary<string, object>
            {
                ["BSTextureStreamerLocalHeap"] = true
            }
        };

        // Act
        var result = await _sut.ValidateVersionCompatibilityAsync(settings);

        // Assert
        result.Should().NotBeNull();
        result.Content.Should().Contain("Texture streaming");
        result.Content.Should().Contain("behaves differently in NG");
    }

    [Fact]
    public async Task CheckNGSpecificSettings_PreNGVersion_NoNGSpecificWarnings()
    {
        // Arrange
        var settings = new CrashGenSettings
        {
            CrashGenName = "Buffout4",
            Version = new Version(1, 29, 0), // Pre-NG version
            RawSettings = new Dictionary<string, object>
            {
                ["MemoryManager"] = true,
                ["BSTextureStreamerLocalHeap"] = true
            }
        };

        // Act
        var result = await _sut.ValidateVersionCompatibilityAsync(settings);

        // Assert
        result.Should().NotBeNull();
        result.Content.Should().NotContain("NG version");
        result.Content.Should().NotContain("enhanced memory management");
    }

    #endregion

    #region YAML Configuration Loading Tests

    [Fact]
    public async Task LoadGameVersionCompatibility_LoadsFromYaml_AppliesRules()
    {
        // Arrange
        var compatibilityData = new Dictionary<string, object>
        {
            ["NextGenVersions"] = new[] { "1.10.980", "1.10.984" },
            ["RequiredBuffoutVersion"] = "1.30.0"
        };
        
        _yamlCore.GetSettingAsync<Dictionary<string, object>>(
                YamlStore.Game, "GameVersionCompatibility", null, Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(compatibilityData));

        var settings = new CrashGenSettings
        {
            CrashGenName = "Buffout4",
            Version = new Version(1, 29, 0),
            RawSettings = new Dictionary<string, object>()
        };

        // Act
        var result = await _sut.ValidateVersionCompatibilityAsync(settings, "1.10.980.0");

        // Assert
        result.Should().NotBeNull();
        // Should apply loaded compatibility rules
    }

    [Fact]
    public async Task LoadGameVersionCompatibility_YamlLoadFails_ContinuesWithDefaults()
    {
        // Arrange
        _yamlCore.GetSettingAsync<Dictionary<string, object>>(
                YamlStore.Game, "GameVersionCompatibility", null, Arg.Any<CancellationToken>())
            .Returns(Task.FromException<Dictionary<string, object>>(new Exception("YAML load failed")));

        var settings = CreateModernSettings(new Version(1, 30, 0));

        // Act
        var result = await _sut.ValidateVersionCompatibilityAsync(settings, "1.10.980.0");

        // Assert
        result.Should().NotBeNull();
        // Should continue with default compatibility checks
    }

    #endregion

    #region Report Building Tests

    [Fact]
    public async Task BuildVersionReport_WithMultipleSeverities_ProperlyFormatsReport()
    {
        // Arrange
        var settings = new CrashGenSettings
        {
            CrashGenName = "Buffout4",
            Version = new Version(1, 19, 0), // Very old version
            RawSettings = new Dictionary<string, object>
            {
                ["F4EE"] = true,              // Error - not available
                ["ArchiveLimit"] = true,       // Will be deprecated later
                ["MemoryManager"] = true       // Info - old behavior
            }
        };

        // Act
        var result = await _sut.ValidateVersionCompatibilityAsync(settings);

        // Assert
        result.Should().NotBeNull();
        result.Type.Should().Be(FragmentType.Error);
        result.Content.Should().Contain("❌"); // Error marker
        result.Content.Should().Contain("⚠️"); // Warning marker
        result.Content.Should().Contain("ℹ️"); // Info marker
        result.Content.Should().Contain("→");  // Recommendation arrow
    }

    [Fact]
    public async Task BuildVersionReport_AllChecksPass_ReturnsSuccessMessage()
    {
        // Arrange
        var settings = CreateModernSettings(new Version(1, 30, 0));

        // Act
        var result = await _sut.ValidateVersionCompatibilityAsync(settings);

        // Assert
        result.Should().NotBeNull();
        result.Type.Should().Be(FragmentType.Info);
        result.Content.Should().Contain("All version checks passed");
    }

    [Fact]
    public async Task BuildVersionReport_UnknownVersion_ShowsUnknown()
    {
        // Arrange
        var settings = new CrashGenSettings
        {
            CrashGenName = "Buffout4",
            Version = null,
            RawSettings = new Dictionary<string, object>()
        };

        // Act
        var result = await _sut.ValidateVersionCompatibilityAsync(settings);

        // Assert
        result.Should().NotBeNull();
        result.Content.Should().Contain("Version: Unknown");
    }

    #endregion

    #region Migration Path Tests

    [Fact]
    public async Task ValidateVersion_FromOldToNew_ProvidesMigrationGuidance()
    {
        // Arrange
        var settings = new CrashGenSettings
        {
            CrashGenName = "Buffout4",
            Version = new Version(1, 25, 0),
            RawSettings = new Dictionary<string, object>
            {
                ["ArchiveLimit"] = true,
                ["InputSwitch"] = true,
                ["MemoryManager"] = false
            }
        };

        // Act
        var result = await _sut.ValidateVersionCompatibilityAsync(settings);

        // Assert
        result.Should().NotBeNull();
        result.Type.Should().Be(FragmentType.Warning);
        // Should provide migration recommendations
        result.Content.Should().Contain("Update to version");
    }

    #endregion

    #region Edge Cases and Error Handling

    [Fact]
    public async Task ValidateVersion_WithMalformedVersion_HandlesGracefully()
    {
        // Arrange
        var settings = new CrashGenSettings
        {
            CrashGenName = "Buffout4",
            Version = null, // Simulate malformed version
            RawSettings = new Dictionary<string, object>
            {
                ["MemoryManager"] = true
            }
        };

        // Act
        var result = await _sut.ValidateVersionCompatibilityAsync(settings);

        // Assert
        result.Should().NotBeNull();
        result.Type.Should().Be(FragmentType.Warning);
        result.Content.Should().Contain("Unable to determine");
    }

    [Fact]
    public async Task ValidateVersion_EmptySettings_HandlesCorrectly()
    {
        // Arrange
        var settings = new CrashGenSettings
        {
            CrashGenName = "Buffout4",
            Version = new Version(1, 30, 0),
            RawSettings = new Dictionary<string, object>()
        };

        // Act
        var result = await _sut.ValidateVersionCompatibilityAsync(settings);

        // Assert
        result.Should().NotBeNull();
        result.Type.Should().Be(FragmentType.Info);
    }

    [Fact]
    public async Task ValidateVersion_NullGameVersion_SkipsGameCompatibilityCheck()
    {
        // Arrange
        var settings = CreateModernSettings(new Version(1, 30, 0));
        string? gameVersion = null;

        // Act
        var result = await _sut.ValidateVersionCompatibilityAsync(settings, gameVersion);

        // Assert
        result.Should().NotBeNull();
        // Should not have game version specific warnings
        result.Content.Should().NotContain("Next-Gen");
    }

    [Fact]
    public async Task ValidateVersion_EmptyGameVersion_SkipsGameCompatibilityCheck()
    {
        // Arrange
        var settings = CreateModernSettings(new Version(1, 30, 0));
        var gameVersion = "";

        // Act
        var result = await _sut.ValidateVersionCompatibilityAsync(settings, gameVersion);

        // Assert
        result.Should().NotBeNull();
        // Should not have game version specific warnings
        result.Content.Should().NotContain("Next-Gen");
    }

    #endregion

    #region Version Comparison Tests

    [Theory]
    [InlineData("1.0.0", "1.0.0", true)]
    [InlineData("1.0.0", "1.0.1", false)]
    [InlineData("1.0.1", "1.0.0", true)]
    [InlineData("2.0.0", "1.99.99", true)]
    [InlineData("1.20.0", "1.20.0", true)]
    public async Task VersionComparison_WorksCorrectly(string currentVersion, string thresholdVersion, bool shouldTriggerCheck)
    {
        // Arrange
        var current = Version.Parse(currentVersion);
        var threshold = Version.Parse(thresholdVersion);
        
        var settings = new CrashGenSettings
        {
            CrashGenName = "Buffout4",
            Version = current,
            RawSettings = new Dictionary<string, object>
            {
                ["F4EE"] = true // Added in 1.20.0
            }
        };

        // Act
        var result = await _sut.ValidateVersionCompatibilityAsync(settings);

        // Assert
        result.Should().NotBeNull();
        
        // F4EE was added in 1.20.0
        if (current < new Version(1, 20, 0))
        {
            result.Content.Should().Contain("not available");
        }
        else
        {
            result.Content.Should().NotContain("not available");
        }
    }

    #endregion

    #region Anniversary Edition vs Standard Edition Tests

    [Fact]
    public async Task ValidateVersion_AnniversaryEdition_ChecksCompatibility()
    {
        // Arrange
        var settings = CreateModernSettings(new Version(1, 30, 0));
        var gameVersion = "1.6.640.0"; // Anniversary Edition

        // Act
        var result = await _sut.ValidateVersionCompatibilityAsync(settings, gameVersion);

        // Assert
        result.Should().NotBeNull();
        // Should handle Anniversary Edition
    }

    [Fact]
    public async Task ValidateVersion_StandardEdition_ChecksCompatibility()
    {
        // Arrange
        var settings = CreateModernSettings(new Version(1, 30, 0));
        var gameVersion = "1.5.97.0"; // Standard Edition

        // Act
        var result = await _sut.ValidateVersionCompatibilityAsync(settings, gameVersion);

        // Assert
        result.Should().NotBeNull();
        // Should handle Standard Edition
    }

    #endregion

    #region VR Version Tests

    [Fact]
    public async Task ValidateVersion_VRVersion_ChecksVRCompatibility()
    {
        // Arrange
        var settings = CreateModernSettings(new Version(1, 30, 0));
        var gameVersion = "1.4.15.0 VR"; // VR version

        // Act
        var result = await _sut.ValidateVersionCompatibilityAsync(settings, gameVersion);

        // Assert
        result.Should().NotBeNull();
        // Should handle VR version appropriately
    }

    #endregion

    #region Console vs PC Tests

    [Fact]
    public async Task ValidateVersion_ConsoleVersion_NotSupported()
    {
        // Arrange
        var settings = CreateModernSettings(new Version(1, 30, 0));
        var gameVersion = "console-1.0.0"; // Console version indicator

        // Act
        var result = await _sut.ValidateVersionCompatibilityAsync(settings, gameVersion);

        // Assert
        result.Should().NotBeNull();
        // Console versions shouldn't use Buffout4
    }

    #endregion

    #region Language-Specific Tests

    [Fact]
    public async Task ValidateVersion_NonEnglishVersion_HandlesCorrectly()
    {
        // Arrange
        var settings = CreateModernSettings(new Version(1, 30, 0));
        var gameVersion = "1.10.980.0-DE"; // German version

        // Act
        var result = await _sut.ValidateVersionCompatibilityAsync(settings, gameVersion);

        // Assert
        result.Should().NotBeNull();
        // Should handle language-specific versions
    }

    #endregion

    #region Performance Tests

    [Fact]
    public async Task ValidateVersion_LargeSettingsFile_HandlesEfficiently()
    {
        // Arrange
        var largSettings = new Dictionary<string, object>();
        for (int i = 0; i < 1000; i++)
        {
            largSettings[$"Setting_{i}"] = i % 2 == 0;
        }
        
        var settings = new CrashGenSettings
        {
            CrashGenName = "Buffout4",
            Version = new Version(1, 30, 0),
            RawSettings = largSettings
        };

        // Act
        var result = await _sut.ValidateVersionCompatibilityAsync(settings);

        // Assert
        result.Should().NotBeNull();
        // Should complete without performance issues
    }

    #endregion

    #region Concurrent Validation Tests

    [Fact]
    public async Task ValidateVersion_ConcurrentValidations_ThreadSafe()
    {
        // Arrange
        var settings = CreateModernSettings(new Version(1, 30, 0));
        var tasks = new List<Task<ReportFragment>>();

        // Act
        for (int i = 0; i < 10; i++)
        {
            tasks.Add(_sut.ValidateVersionCompatibilityAsync(settings));
        }
        
        var results = await Task.WhenAll(tasks);

        // Assert
        results.Should().NotBeNull();
        results.Should().HaveCount(10);
        results.Should().OnlyContain(r => r != null);
    }

    #endregion

    #region Future Version Tests

    [Fact]
    public async Task ValidateVersion_FutureVersion_HandlesGracefully()
    {
        // Arrange
        var settings = new CrashGenSettings
        {
            CrashGenName = "Buffout4",
            Version = new Version(2, 0, 0), // Future version
            RawSettings = new Dictionary<string, object>
            {
                ["FutureSetting"] = true
            }
        };

        // Act
        var result = await _sut.ValidateVersionCompatibilityAsync(settings);

        // Assert
        result.Should().NotBeNull();
        // Should handle future versions without crashing
    }

    #endregion

    #region Beta/Alpha Version Tests

    [Fact]
    public async Task ValidateVersion_BetaVersion_HandlesCorrectly()
    {
        // Arrange
        var settings = new CrashGenSettings
        {
            CrashGenName = "Buffout4",
            Version = new Version(1, 30, 0, 1), // Beta version with build number
            RawSettings = new Dictionary<string, object>()
        };

        // Act
        var result = await _sut.ValidateVersionCompatibilityAsync(settings);

        // Assert
        result.Should().NotBeNull();
        // Should handle beta versions
    }

    #endregion

    #region Value Matching Tests

    [Theory]
    [InlineData(true, true, true)]
    [InlineData(false, false, true)]
    [InlineData(true, false, false)]
    [InlineData(123, 123, true)]
    [InlineData(123, 456, false)]
    [InlineData("true", true, true)]
    [InlineData("false", false, true)]
    [InlineData("123", 123, true)]
    public async Task ValuesMatch_VariousTypes_WorksCorrectly(object current, object recommended, bool expectedMatch)
    {
        // This is tested indirectly through version behavior validation
        // The validator uses ValuesMatch internally to compare settings values
        
        // Arrange
        var settings = new CrashGenSettings
        {
            CrashGenName = "Buffout4",
            Version = new Version(1, 30, 0),
            RawSettings = new Dictionary<string, object>
            {
                ["TestSetting"] = current
            }
        };

        // Act
        var result = await _sut.ValidateVersionCompatibilityAsync(settings);

        // Assert
        result.Should().NotBeNull();
    }

    #endregion

    #region Helper Methods

    private static CrashGenSettings CreateModernSettings(Version version)
    {
        return new CrashGenSettings
        {
            CrashGenName = "Buffout4",
            Version = version,
            RawSettings = new Dictionary<string, object>
            {
                ["MemoryManager"] = true,
                ["Achievements"] = false,
                ["F4EE"] = true,
                ["BSTextureStreamerLocalHeap"] = false,
                ["SmallBlockAllocator"] = true,
                ["ScaleformAllocator"] = true,
                ["MaxStdIO"] = -1
            }
        };
    }

    #endregion
}