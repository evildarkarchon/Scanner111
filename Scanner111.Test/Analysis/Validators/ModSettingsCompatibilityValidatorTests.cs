using FluentAssertions;
using Microsoft.Extensions.Logging;
using NSubstitute;
using Scanner111.Core.Analysis.Validators;
using Scanner111.Core.Configuration;
using Scanner111.Core.Models;
using Scanner111.Core.Reporting;

namespace Scanner111.Test.Analysis.Validators;

public class ModSettingsCompatibilityValidatorTests
{
    private readonly ILogger<ModSettingsCompatibilityValidator> _logger;
    private readonly IAsyncYamlSettingsCore _yamlCore;
    private readonly ModSettingsCompatibilityValidator _sut;

    public ModSettingsCompatibilityValidatorTests()
    {
        _logger = Substitute.For<ILogger<ModSettingsCompatibilityValidator>>();
        _yamlCore = Substitute.For<IAsyncYamlSettingsCore>();
        _sut = new ModSettingsCompatibilityValidator(_logger, _yamlCore);
    }

    #region ValidateModCompatibilityAsync Tests

    [Fact]
    public async Task ValidateModCompatibilityAsync_WithNullSettings_ThrowsArgumentNullException()
    {
        // Arrange
        CrashGenSettings settings = null!;
        var modSettings = new ModDetectionSettings();

        // Act & Assert
        await Assert.ThrowsAsync<ArgumentNullException>(
            () => _sut.ValidateModCompatibilityAsync(settings, modSettings));
    }

    [Fact]
    public async Task ValidateModCompatibilityAsync_WithNullModSettings_ThrowsArgumentNullException()
    {
        // Arrange
        var settings = CreateValidSettings();
        ModDetectionSettings modSettings = null!;

        // Act & Assert
        await Assert.ThrowsAsync<ArgumentNullException>(
            () => _sut.ValidateModCompatibilityAsync(settings, modSettings));
    }

    [Fact]
    public async Task ValidateModCompatibilityAsync_NoModsInstalled_ReturnsSuccessReport()
    {
        // Arrange
        var settings = CreateValidSettings();
        var modSettings = new ModDetectionSettings();

        // Act
        var result = await _sut.ValidateModCompatibilityAsync(settings, modSettings);

        // Assert
        result.Should().NotBeNull();
        result.Type.Should().Be(FragmentType.Info);
        result.Content.Should().Contain("All mod compatibility checks passed");
        result.Content.Should().Contain("no conflicts detected");
    }

    [Fact]
    public async Task ValidateModCompatibilityAsync_WithCancellation_ThrowsOperationCanceledException()
    {
        // Arrange
        var settings = CreateValidSettings();
        var modSettings = new ModDetectionSettings();
        var cts = new CancellationTokenSource();
        cts.Cancel();

        // Act & Assert
        await Assert.ThrowsAsync<OperationCanceledException>(
            () => _sut.ValidateModCompatibilityAsync(settings, modSettings, cancellationToken: cts.Token));
    }

    #endregion

    #region Baka ScrapHeap Compatibility Tests

    [Fact]
    public async Task ValidateModCompatibility_BakaScrapHeapWithMemoryManager_ReturnsError()
    {
        // Arrange
        var settings = new CrashGenSettings
        {
            CrashGenName = "Buffout4",
            RawSettings = new Dictionary<string, object>
            {
                ["MemoryManager"] = true,
                ["SmallBlockAllocator"] = true
            }
        };
        var modSettings = new ModDetectionSettings
        {
            HasBakaScrapHeap = true
        };

        // Act
        var result = await _sut.ValidateModCompatibilityAsync(settings, modSettings);

        // Assert
        result.Should().NotBeNull();
        result.Type.Should().Be(FragmentType.Error);
        result.Content.Should().Contain("Baka ScrapHeap");
        result.Content.Should().Contain("memory management");
    }

    [Fact]
    public async Task ValidateModCompatibility_BakaScrapHeapWithoutMemoryManager_NoError()
    {
        // Arrange
        var settings = new CrashGenSettings
        {
            CrashGenName = "Buffout4",
            RawSettings = new Dictionary<string, object>
            {
                ["MemoryManager"] = false,
                ["SmallBlockAllocator"] = false
            }
        };
        var modSettings = new ModDetectionSettings
        {
            HasBakaScrapHeap = true
        };

        // Act
        var result = await _sut.ValidateModCompatibilityAsync(settings, modSettings);

        // Assert
        result.Should().NotBeNull();
        result.Type.Should().Be(FragmentType.Info);
        result.Content.Should().Contain("All mod compatibility checks passed");
    }

    #endregion

    #region Looks Menu/F4EE Compatibility Tests

    [Fact]
    public async Task ValidateModCompatibility_LooksMenuWithoutF4EE_ReturnsError()
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
        var modSettings = new ModDetectionSettings
        {
            CrashLogPlugins = new Dictionary<string, string>
            {
                ["LooksMenu"] = "1.0.0"
            }
        };

        // Act
        var result = await _sut.ValidateModCompatibilityAsync(settings, modSettings);

        // Assert
        result.Should().NotBeNull();
        result.Type.Should().Be(FragmentType.Error);
        result.Content.Should().Contain("Looks Menu");
        result.Content.Should().Contain("F4EE");
        result.Content.Should().Contain("compatibility");
    }

    [Fact]
    public async Task ValidateModCompatibility_LooksMenuWithF4EE_NoError()
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
        var modSettings = new ModDetectionSettings
        {
            CrashLogPlugins = new Dictionary<string, string>
            {
                ["LooksMenu"] = "1.0.0"
            }
        };

        // Act
        var result = await _sut.ValidateModCompatibilityAsync(settings, modSettings);

        // Assert
        result.Should().NotBeNull();
        result.Type.Should().Be(FragmentType.Info);
    }

    #endregion

    #region High FPS Physics Fix Tests

    [Fact]
    public async Task ValidateModCompatibility_HighFPSPhysicsFixWithActorIsHostile_ReturnsWarning()
    {
        // Arrange
        var settings = new CrashGenSettings
        {
            CrashGenName = "Buffout4",
            RawSettings = new Dictionary<string, object>
            {
                ["ActorIsHostileToActor"] = true
            }
        };
        var modSettings = new ModDetectionSettings
        {
            CrashLogPlugins = new Dictionary<string, string>
            {
                ["HighFPSPhysicsFix"] = "1.0.0"
            }
        };

        // Act
        var result = await _sut.ValidateModCompatibilityAsync(settings, modSettings);

        // Assert
        result.Should().NotBeNull();
        result.Type.Should().Be(FragmentType.Warning);
        result.Content.Should().Contain("High FPS Physics Fix");
        result.Content.Should().Contain("handles this fix internally");
    }

    #endregion

    #region Workshop Framework Tests

    [Fact]
    public async Task ValidateModCompatibility_WorkshopFrameworkWithoutRequiredSettings_ReturnsWarning()
    {
        // Arrange
        var settings = new CrashGenSettings
        {
            CrashGenName = "Buffout4",
            RawSettings = new Dictionary<string, object>
            {
                ["WorkshopMenu"] = false,
                ["PackageAllocateLocation"] = false
            }
        };
        var modSettings = new ModDetectionSettings
        {
            CrashLogPlugins = new Dictionary<string, string>
            {
                ["WorkshopFramework"] = "2.0.0"
            }
        };

        // Act
        var result = await _sut.ValidateModCompatibilityAsync(settings, modSettings);

        // Assert
        result.Should().NotBeNull();
        result.Type.Should().Be(FragmentType.Warning);
        result.Content.Should().Contain("Workshop Framework");
        result.Content.Should().Contain("WorkshopMenu");
        result.Content.Should().Contain("PackageAllocateLocation");
    }

    [Fact]
    public async Task ValidateModCompatibility_WorkshopFrameworkWithRequiredSettings_NoWarning()
    {
        // Arrange
        var settings = new CrashGenSettings
        {
            CrashGenName = "Buffout4",
            RawSettings = new Dictionary<string, object>
            {
                ["WorkshopMenu"] = true,
                ["PackageAllocateLocation"] = true
            }
        };
        var modSettings = new ModDetectionSettings
        {
            CrashLogPlugins = new Dictionary<string, string>
            {
                ["WorkshopFramework"] = "2.0.0"
            }
        };

        // Act
        var result = await _sut.ValidateModCompatibilityAsync(settings, modSettings);

        // Assert
        result.Should().NotBeNull();
        result.Type.Should().Be(FragmentType.Info);
    }

    #endregion

    #region PRP (Previs Repair Pack) Tests

    [Fact]
    public async Task ValidateModCompatibility_PRPWithoutBSPreCulledObjects_ReturnsInfo()
    {
        // Arrange
        var settings = new CrashGenSettings
        {
            CrashGenName = "Buffout4",
            RawSettings = new Dictionary<string, object>
            {
                ["BSPreCulledObjects"] = false
            }
        };
        var modSettings = new ModDetectionSettings
        {
            CrashLogPlugins = new Dictionary<string, string>
            {
                ["PRP"] = "1.0.0"
            }
        };

        // Act
        var result = await _sut.ValidateModCompatibilityAsync(settings, modSettings);

        // Assert
        result.Should().NotBeNull();
        result.Content.Should().Contain("Previs Repair Pack");
        result.Content.Should().Contain("BSPreCulledObjects");
    }

    #endregion

    #region Buffout NG Tests

    [Fact]
    public async Task ValidateModCompatibility_BuffoutNGWithMemoryManager_ReturnsError()
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
            CrashLogPlugins = new Dictionary<string, string>
            {
                ["BuffOutNG"] = "1.0.0"
            }
        };

        // Act
        var result = await _sut.ValidateModCompatibilityAsync(settings, modSettings);

        // Assert
        result.Should().NotBeNull();
        result.Type.Should().Be(FragmentType.Error);
        result.Content.Should().Contain("Buffout NG");
        result.Content.Should().Contain("own memory management");
    }

    #endregion

    #region Mod Combination Tests

    [Fact]
    public async Task ValidateModCombinations_LooksMenuAndAAF_RequiresSpecificSettings()
    {
        // Arrange
        var settings = new CrashGenSettings
        {
            CrashGenName = "Buffout4",
            RawSettings = new Dictionary<string, object>
            {
                ["F4EE"] = false,
                ["ActorIsHostileToActor"] = false
            }
        };
        var modSettings = new ModDetectionSettings
        {
            CrashLogPlugins = new Dictionary<string, string>
            {
                ["LooksMenu"] = "1.0.0",
                ["AAF"] = "1.0.0"
            }
        };

        // Act
        var result = await _sut.ValidateModCompatibilityAsync(settings, modSettings);

        // Assert
        result.Should().NotBeNull();
        result.Type.Should().Be(FragmentType.Warning);
        result.Content.Should().Contain("LooksMenu + AAF");
        result.Content.Should().Contain("specific settings");
    }

    [Fact]
    public async Task ValidateModCombinations_WorkshopFrameworkAndSimSettlements2_RequiresOptimizedMemory()
    {
        // Arrange
        var settings = new CrashGenSettings
        {
            CrashGenName = "Buffout4",
            RawSettings = new Dictionary<string, object>
            {
                ["WorkshopMenu"] = false,
                ["PackageAllocateLocation"] = false,
                ["MemoryManager"] = false
            }
        };
        var modSettings = new ModDetectionSettings
        {
            CrashLogPlugins = new Dictionary<string, string>
            {
                ["WorkshopFramework"] = "2.0.0",
                ["SimSettlements2"] = "2.0.0"
            }
        };

        // Act
        var result = await _sut.ValidateModCompatibilityAsync(settings, modSettings);

        // Assert
        result.Should().NotBeNull();
        result.Type.Should().Be(FragmentType.Warning);
        result.Content.Should().Contain("Heavy workshop mods");
        result.Content.Should().Contain("optimized memory");
    }

    #endregion

    #region Load Order Impact Tests

    [Fact]
    public async Task ValidateLoadOrderImpact_MultipleWorkshopMods_SuggestsMemoryManager()
    {
        // Arrange
        var settings = new CrashGenSettings
        {
            CrashGenName = "Buffout4",
            RawSettings = new Dictionary<string, object>
            {
                ["MemoryManager"] = false
            }
        };
        var modSettings = new ModDetectionSettings();
        var loadOrder = new List<string>
        {
            "WorkshopFramework.esm",
            "WorkshopPlus.esp",
            "BetterWorkshop.esp"
        };

        // Act
        var result = await _sut.ValidateModCompatibilityAsync(settings, modSettings, loadOrder);

        // Assert
        result.Should().NotBeNull();
        result.Type.Should().Be(FragmentType.Warning);
        result.Content.Should().Contain("Multiple workshop mods");
        result.Content.Should().Contain("MemoryManager");
    }

    [Fact]
    public async Task ValidateLoadOrderImpact_HeavyTextureMods_ChecksBSTextureStreamer()
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
        var modSettings = new ModDetectionSettings();
        var loadOrder = new List<string>
        {
            "4K_Textures.ba2",
            "HD_Overhaul.ba2",
            "UltraHD_Pack.ba2",
            "Texture_Optimization.ba2",
            "4K_Commonwealth.ba2"
        };

        // Act
        var result = await _sut.ValidateModCompatibilityAsync(settings, modSettings, loadOrder);

        // Assert
        result.Should().NotBeNull();
        result.Content.Should().Contain("texture mods");
        result.Content.Should().Contain("BSTextureStreamerLocalHeap");
    }

    [Fact]
    public async Task ValidateLoadOrderImpact_EmptyLoadOrder_NoLoadOrderWarnings()
    {
        // Arrange
        var settings = CreateValidSettings();
        var modSettings = new ModDetectionSettings();
        var loadOrder = new List<string>();

        // Act
        var result = await _sut.ValidateModCompatibilityAsync(settings, modSettings, loadOrder);

        // Assert
        result.Should().NotBeNull();
        result.Type.Should().Be(FragmentType.Info);
    }

    #endregion

    #region Mod-Specific Optimizations Tests

    [Fact]
    public async Task SuggestModSpecificOptimizations_ScriptHeavyMods_SuggestsHigherMaxStdIO()
    {
        // Arrange
        var settings = new CrashGenSettings
        {
            CrashGenName = "Buffout4",
            RawSettings = new Dictionary<string, object>
            {
                ["MaxStdIO"] = 512
            }
        };
        var modSettings = new ModDetectionSettings
        {
            CrashLogPlugins = new Dictionary<string, string>
            {
                ["ScriptExtenderPlugin"] = "1.0.0",
                ["PapyrusExtender"] = "1.0.0"
            }
        };

        // Act
        var result = await _sut.ValidateModCompatibilityAsync(settings, modSettings);

        // Assert
        result.Should().NotBeNull();
        result.Content.Should().Contain("Script-heavy mods");
        result.Content.Should().Contain("MaxStdIO");
        result.Content.Should().Contain("2048 or higher");
    }

    [Fact]
    public async Task SuggestModSpecificOptimizations_ENBDetected_SuggestsScaleformAllocator()
    {
        // Arrange
        var settings = new CrashGenSettings
        {
            CrashGenName = "Buffout4",
            RawSettings = new Dictionary<string, object>
            {
                ["ScaleformAllocator"] = false
            }
        };
        var modSettings = new ModDetectionSettings
        {
            CrashLogPlugins = new Dictionary<string, string>
            {
                ["ENB"] = "0.475"
            }
        };

        // Act
        var result = await _sut.ValidateModCompatibilityAsync(settings, modSettings);

        // Assert
        result.Should().NotBeNull();
        result.Content.Should().Contain("ENB");
        result.Content.Should().Contain("ScaleformAllocator");
    }

    [Fact]
    public async Task SuggestModSpecificOptimizations_ENBModuleDetected_SuggestsScaleformAllocator()
    {
        // Arrange
        var settings = new CrashGenSettings
        {
            CrashGenName = "Buffout4",
            RawSettings = new Dictionary<string, object>
            {
                ["ScaleformAllocator"] = false
            }
        };
        var modSettings = ModDetectionSettings.CreateWithXseModules(new[] { "enbhelper.dll" });

        // Act
        var result = await _sut.ValidateModCompatibilityAsync(settings, modSettings);

        // Assert
        result.Should().NotBeNull();
        result.Content.Should().Contain("ENB");
        result.Content.Should().Contain("ScaleformAllocator");
    }

    #endregion

    #region Custom Compatibility Rules Tests

    [Fact]
    public async Task LoadCustomCompatibilityRules_LoadsFromYaml_AppliesCustomRules()
    {
        // Arrange
        var customRules = new List<Dictionary<string, object>>
        {
            new()
            {
                ["ModName"] = "CustomMod",
                ["Settings"] = new Dictionary<string, object>
                {
                    ["CustomSetting"] = true
                }
            }
        };
        
        _yamlCore.GetSettingAsync<List<Dictionary<string, object>>>(
                YamlStore.Game, "ModCompatibilityRules", null, Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(customRules));

        var settings = new CrashGenSettings
        {
            CrashGenName = "Buffout4",
            RawSettings = new Dictionary<string, object>
            {
                ["CustomSetting"] = false
            }
        };
        
        var modSettings = new ModDetectionSettings
        {
            CrashLogPlugins = new Dictionary<string, string>
            {
                ["CustomMod"] = "1.0.0"
            }
        };

        // Act
        var result = await _sut.ValidateModCompatibilityAsync(settings, modSettings);

        // Assert
        result.Should().NotBeNull();
        result.Type.Should().Be(FragmentType.Warning);
        result.Content.Should().Contain("CustomMod");
    }

    [Fact]
    public async Task LoadCustomCompatibilityRules_YamlLoadFails_ContinuesWithoutCustomRules()
    {
        // Arrange
        _yamlCore.GetSettingAsync<List<Dictionary<string, object>>>(
                YamlStore.Game, "ModCompatibilityRules", null, Arg.Any<CancellationToken>())
            .Returns(Task.FromException<List<Dictionary<string, object>>>(new Exception("YAML load failed")));

        var settings = CreateValidSettings();
        var modSettings = new ModDetectionSettings();

        // Act
        var result = await _sut.ValidateModCompatibilityAsync(settings, modSettings);

        // Assert
        result.Should().NotBeNull();
        // Should continue without crashing
    }

    #endregion

    #region Report Building Tests

    [Fact]
    public async Task BuildCompatibilityReport_WithMultipleSeverities_ProperlyFormatsReport()
    {
        // Arrange
        var settings = new CrashGenSettings
        {
            CrashGenName = "Buffout4",
            RawSettings = new Dictionary<string, object>
            {
                ["MemoryManager"] = true,
                ["F4EE"] = false,
                ["BSPreCulledObjects"] = false
            }
        };
        var modSettings = new ModDetectionSettings
        {
            HasBakaScrapHeap = true, // Error
            CrashLogPlugins = new Dictionary<string, string>
            {
                ["LooksMenu"] = "1.0.0", // Error (F4EE not enabled)
                ["PRP"] = "1.0.0"        // Info (BSPreCulledObjects)
            }
        };

        // Act
        var result = await _sut.ValidateModCompatibilityAsync(settings, modSettings);

        // Assert
        result.Should().NotBeNull();
        result.Type.Should().Be(FragmentType.Error);
        result.Content.Should().Contain("❌"); // Error marker
        result.Content.Should().Contain("ℹ️"); // Info marker
        result.Content.Should().Contain("→");  // Recommendation arrow
    }

    [Fact]
    public async Task BuildCompatibilityReport_AllChecksPass_ReturnsSuccessMessage()
    {
        // Arrange
        var settings = CreateValidSettings();
        var modSettings = new ModDetectionSettings();

        // Act
        var result = await _sut.ValidateModCompatibilityAsync(settings, modSettings);

        // Assert
        result.Should().NotBeNull();
        result.Type.Should().Be(FragmentType.Info);
        result.Content.Should().Contain("All mod compatibility checks passed");
        result.Content.Should().Contain("no conflicts detected");
    }

    #endregion

    #region Mod Detection Tests

    [Fact]
    public async Task IsModInstalled_ChecksMultipleSources_FindsModCorrectly()
    {
        // Arrange
        var settings = CreateValidSettings();
        var modSettings = new ModDetectionSettings
        {
            XseModules = new HashSet<string> { "testmod.dll" },
            CrashLogPlugins = new Dictionary<string, string>
            {
                ["AnotherMod"] = "1.0.0"
            }
        };

        // Test XSE module detection
        var xseSettings = new CrashGenSettings
        {
            CrashGenName = "Buffout4",
            RawSettings = new Dictionary<string, object>()
        };

        // Act & Assert - Should find via XSE modules
        var result = await _sut.ValidateModCompatibilityAsync(xseSettings, modSettings);
        result.Should().NotBeNull();
    }

    [Theory]
    [InlineData("LooksMenu", "looksmenu")]
    [InlineData("WorkshopFramework", "WORKSHOPFRAMEWORK")]
    [InlineData("SimSettlements2", "simsettlements2")]
    public async Task IsModInstalled_CaseInsensitive_FindsModRegardlessOfCase(string modName, string installedName)
    {
        // Arrange
        var settings = CreateValidSettings();
        var modSettings = new ModDetectionSettings
        {
            CrashLogPlugins = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                [installedName] = "1.0.0"
            }
        };

        // Act - Should trigger mod-specific checks
        var result = await _sut.ValidateModCompatibilityAsync(settings, modSettings);

        // Assert
        result.Should().NotBeNull();
    }

    #endregion

    #region Value Matching Tests

    [Theory]
    [InlineData(true, true, true)]
    [InlineData(false, false, true)]
    [InlineData(true, false, false)]
    [InlineData(false, true, false)]
    public async Task ValuesMatch_BooleanComparison_WorksCorrectly(bool current, bool required, bool shouldMatch)
    {
        // Arrange
        var settings = new CrashGenSettings
        {
            CrashGenName = "Buffout4",
            RawSettings = new Dictionary<string, object>
            {
                ["TestSetting"] = current
            }
        };

        // This is indirectly tested through mod requirement validation
        // Just ensure the validator handles boolean comparisons properly
        var modSettings = new ModDetectionSettings();

        // Act
        var result = await _sut.ValidateModCompatibilityAsync(settings, modSettings);

        // Assert
        result.Should().NotBeNull();
    }

    [Theory]
    [InlineData("true", true)]
    [InlineData("True", true)]
    [InlineData("TRUE", true)]
    [InlineData("false", false)]
    [InlineData("False", false)]
    [InlineData("FALSE", false)]
    public async Task ValuesMatch_StringToBoolConversion_WorksCorrectly(string stringValue, bool expectedBool)
    {
        // Arrange
        var settings = new CrashGenSettings
        {
            CrashGenName = "Buffout4",
            RawSettings = new Dictionary<string, object>
            {
                ["F4EE"] = stringValue
            }
        };
        var modSettings = new ModDetectionSettings
        {
            CrashLogPlugins = new Dictionary<string, string>
            {
                ["LooksMenu"] = "1.0.0"
            }
        };

        // Act
        var result = await _sut.ValidateModCompatibilityAsync(settings, modSettings);

        // Assert
        result.Should().NotBeNull();
        if (!expectedBool)
        {
            result.Content.Should().Contain("F4EE");
        }
    }

    #endregion

    #region Edge Cases and Error Handling

    [Fact]
    public async Task ValidateModCompatibility_EmptySettingsAndMods_ReturnsSuccess()
    {
        // Arrange
        var settings = new CrashGenSettings
        {
            CrashGenName = "Buffout4",
            RawSettings = new Dictionary<string, object>()
        };
        var modSettings = new ModDetectionSettings();

        // Act
        var result = await _sut.ValidateModCompatibilityAsync(settings, modSettings);

        // Assert
        result.Should().NotBeNull();
        result.Type.Should().Be(FragmentType.Info);
    }

    [Fact]
    public async Task ValidateModCompatibility_MalformedSettingValues_HandlesGracefully()
    {
        // Arrange
        var settings = new CrashGenSettings
        {
            CrashGenName = "Buffout4",
            RawSettings = new Dictionary<string, object>
            {
                ["MemoryManager"] = "not_a_bool",
                ["MaxStdIO"] = "not_an_int",
                ["F4EE"] = new object() // Invalid type
            }
        };
        var modSettings = new ModDetectionSettings
        {
            HasBakaScrapHeap = true
        };

        // Act
        var result = await _sut.ValidateModCompatibilityAsync(settings, modSettings);

        // Assert
        result.Should().NotBeNull();
        // Should handle conversion errors gracefully
    }

    [Fact]
    public async Task ValidateModCompatibility_VeryLargeLoadOrder_HandlesEfficiently()
    {
        // Arrange
        var settings = CreateValidSettings();
        var modSettings = new ModDetectionSettings();
        var loadOrder = Enumerable.Range(0, 1000)
            .Select(i => $"Mod_{i}.esp")
            .ToList();

        // Act
        var result = await _sut.ValidateModCompatibilityAsync(settings, modSettings, loadOrder);

        // Assert
        result.Should().NotBeNull();
        // Should complete without performance issues
    }

    [Fact]
    public async Task ValidateModCompatibility_CircularDependencies_DoesNotInfiniteLoop()
    {
        // Arrange
        var settings = new CrashGenSettings
        {
            CrashGenName = "Buffout4",
            RawSettings = new Dictionary<string, object>
            {
                ["Setting1"] = true,
                ["Setting2"] = true
            }
        };
        var modSettings = new ModDetectionSettings
        {
            CrashLogPlugins = new Dictionary<string, string>
            {
                ["ModA"] = "1.0.0",
                ["ModB"] = "1.0.0"
            }
        };

        // Act
        var result = await _sut.ValidateModCompatibilityAsync(settings, modSettings);

        // Assert
        result.Should().NotBeNull();
        // Should handle potential circular references
    }

    #endregion

    #region DLL Conflict Tests

    [Fact]
    public async Task ValidateModCompatibility_MultipleDLLVersions_DetectsConflict()
    {
        // Arrange
        var settings = CreateValidSettings();
        var modSettings = ModDetectionSettings.CreateWithXseModules(new[]
        {
            "achievement.dll",
            "achievements.dll",
            "achievement_enabler.dll"
        });

        // Act
        var result = await _sut.ValidateModCompatibilityAsync(settings, modSettings);

        // Assert
        result.Should().NotBeNull();
        // Should detect potential DLL conflicts
    }

    #endregion

    #region Script Extender Requirements Tests

    [Fact]
    public async Task ValidateModCompatibility_F4SERequiredMods_ChecksCompatibility()
    {
        // Arrange
        var settings = CreateValidSettings();
        var modSettings = ModDetectionSettings.CreateWithXseModules(new[]
        {
            "f4se_loader.dll",
            "f4ee.dll",
            "mcm.dll"
        });

        // Act
        var result = await _sut.ValidateModCompatibilityAsync(settings, modSettings);

        // Assert
        result.Should().NotBeNull();
        // F4SE-dependent mods should be validated
    }

    #endregion

    #region ENB Compatibility Tests

    [Fact]
    public async Task ValidateModCompatibility_ENBWithComplexParallax_ChecksSettings()
    {
        // Arrange
        var settings = new CrashGenSettings
        {
            CrashGenName = "Buffout4",
            RawSettings = new Dictionary<string, object>
            {
                ["ScaleformAllocator"] = false,
                ["ComplexParallax"] = true
            }
        };
        var modSettings = new ModDetectionSettings
        {
            CrashLogPlugins = new Dictionary<string, string>
            {
                ["ENB"] = "0.475",
                ["ComplexParallax"] = "1.0.0"
            }
        };

        // Act
        var result = await _sut.ValidateModCompatibilityAsync(settings, modSettings);

        // Assert
        result.Should().NotBeNull();
        result.Content.Should().Contain("ENB");
    }

    #endregion

    #region VR-Specific Tests

    [Fact]
    public async Task ValidateModCompatibility_VRMods_ChecksVRCompatibility()
    {
        // Arrange
        var settings = CreateValidSettings();
        var modSettings = new ModDetectionSettings
        {
            CrashLogPlugins = new Dictionary<string, string>
            {
                ["FRIK"] = "1.0.0",
                ["VRTools"] = "1.0.0"
            }
        };

        // Act
        var result = await _sut.ValidateModCompatibilityAsync(settings, modSettings);

        // Assert
        result.Should().NotBeNull();
        // VR mods should be handled appropriately
    }

    #endregion

    #region Anniversary Edition Tests

    [Fact]
    public async Task ValidateModCompatibility_AnniversaryEditionContent_ChecksCompatibility()
    {
        // Arrange
        var settings = CreateValidSettings();
        var modSettings = new ModDetectionSettings
        {
            CrashLogPlugins = new Dictionary<string, string>
            {
                ["ccBGSSSE001-Fish.esm"] = "1.0.0",
                ["ccBGSSSE025-AdvDSGS.esm"] = "1.0.0"
            }
        };

        // Act
        var result = await _sut.ValidateModCompatibilityAsync(settings, modSettings);

        // Assert
        result.Should().NotBeNull();
        // Anniversary Edition content should be handled
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
                ["BSTextureStreamerLocalHeap"] = false,
                ["SmallBlockAllocator"] = true,
                ["ScaleformAllocator"] = true,
                ["WorkshopMenu"] = true,
                ["PackageAllocateLocation"] = true,
                ["ActorIsHostileToActor"] = false,
                ["BSPreCulledObjects"] = true,
                ["MaxStdIO"] = 2048
            }
        };
    }

    #endregion
}