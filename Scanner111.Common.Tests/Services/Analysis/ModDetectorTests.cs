using FluentAssertions;
using Scanner111.Common.Models.Analysis;
using Scanner111.Common.Models.Configuration;
using Scanner111.Common.Services.Analysis;

namespace Scanner111.Common.Tests.Services.Analysis;

/// <summary>
/// Tests for ModDetector.
/// </summary>
public class ModDetectorTests
{
    private readonly ModDetector _detector;

    public ModDetectorTests()
    {
        _detector = new ModDetector();
    }

    #region Single Mod Detection Tests

    [Fact]
    public async Task DetectAsync_WithProblematicMod_DetectsMod()
    {
        // Arrange
        var plugins = new[]
        {
            new PluginInfo { FormIdPrefix = "E7", PluginName = "DamageThresholdFramework.esp" },
            new PluginInfo { FormIdPrefix = "E8", PluginName = "SafeMod.esp" }
        };

        _detector.Configuration = new ModConfiguration
        {
            FrequentCrashMods = new Dictionary<string, string>
            {
                ["DamageThresholdFramework"] = "Damage Threshold Framework\nThis mod can cause frequent crashes."
            }
        };

        // Act
        var result = await _detector.DetectAsync(plugins, new HashSet<string>());

        // Assert
        result.ProblematicMods.Should().HaveCount(1);
        result.ProblematicMods[0].ModName.Should().Be("Damage Threshold Framework");
        result.ProblematicMods[0].MatchedPlugin.ToLowerInvariant().Should().Contain("damagethresholdframework");
        result.ProblematicMods[0].PluginFormId.Should().Be("E7");
        result.ProblematicMods[0].Category.Should().Be(ModCategory.FrequentCrashes);
    }

    [Fact]
    public async Task DetectAsync_WithNoProblematicMods_ReturnsEmpty()
    {
        // Arrange
        var plugins = new[]
        {
            new PluginInfo { FormIdPrefix = "E7", PluginName = "SafeMod.esp" },
            new PluginInfo { FormIdPrefix = "E8", PluginName = "AnotherSafe.esp" }
        };

        _detector.Configuration = new ModConfiguration
        {
            FrequentCrashMods = new Dictionary<string, string>
            {
                ["DangerousMod"] = "This is a dangerous mod."
            }
        };

        // Act
        var result = await _detector.DetectAsync(plugins, new HashSet<string>());

        // Assert
        result.ProblematicMods.Should().BeEmpty();
        result.HasIssues.Should().BeFalse();
    }

    [Fact]
    public async Task DetectAsync_IsCaseInsensitive()
    {
        // Arrange
        var plugins = new[]
        {
            new PluginInfo { FormIdPrefix = "E7", PluginName = "DAMAGETHRESHOLDFRAMEWORK.ESP" }
        };

        _detector.Configuration = new ModConfiguration
        {
            FrequentCrashMods = new Dictionary<string, string>
            {
                ["damagethresholdframework"] = "Damage Threshold Framework\nThis mod can cause frequent crashes."
            }
        };

        // Act
        var result = await _detector.DetectAsync(plugins, new HashSet<string>());

        // Assert
        result.ProblematicMods.Should().HaveCount(1);
    }

    [Fact]
    public async Task DetectAsync_WithMultipleCategories_DetectsAll()
    {
        // Arrange
        var plugins = new[]
        {
            new PluginInfo { FormIdPrefix = "E7", PluginName = "FrequentCrashMod.esp" },
            new PluginInfo { FormIdPrefix = "E8", PluginName = "ModWithSolution.esp" },
            new PluginInfo { FormIdPrefix = "E9", PluginName = "OpcPatchedMod.esp" }
        };

        _detector.Configuration = new ModConfiguration
        {
            FrequentCrashMods = new Dictionary<string, string>
            {
                ["FrequentCrashMod"] = "Frequent Crash Mod\nCauses crashes."
            },
            SolutionMods = new Dictionary<string, string>
            {
                ["ModWithSolution"] = "Mod With Solution\nHas a patch available."
            },
            OpcPatchedMods = new Dictionary<string, string>
            {
                ["OpcPatchedMod"] = "OPC Patched Mod\nPatched via OPC."
            }
        };

        // Act
        var result = await _detector.DetectAsync(plugins, new HashSet<string>());

        // Assert
        result.ProblematicMods.Should().HaveCount(3);
        result.ProblematicMods.Should().Contain(m => m.Category == ModCategory.FrequentCrashes);
        result.ProblematicMods.Should().Contain(m => m.Category == ModCategory.HasSolution);
        result.ProblematicMods.Should().Contain(m => m.Category == ModCategory.OpcPatched);
    }

    #endregion

    #region Conflict Detection Tests

    [Fact]
    public async Task DetectAsync_WithModConflict_DetectsConflict()
    {
        // Arrange
        var plugins = new[]
        {
            new PluginInfo { FormIdPrefix = "E7", PluginName = "BetterPowerArmor.esp" },
            new PluginInfo { FormIdPrefix = "E8", PluginName = "KnockoutFramework.esp" }
        };

        _detector.Configuration = new ModConfiguration
        {
            ConflictingMods = new Dictionary<string, string>
            {
                ["betterpowerarmor | knockoutframework"] = "Better Power Armor Redux conflicts with Knockout Framework."
            }
        };

        // Act
        var result = await _detector.DetectAsync(plugins, new HashSet<string>());

        // Assert
        result.Conflicts.Should().HaveCount(1);
        result.Conflicts[0].Mod1.Should().Be("betterpowerarmor");
        result.Conflicts[0].Mod2.Should().Be("knockoutframework");
        result.HasIssues.Should().BeTrue();
    }

    [Fact]
    public async Task DetectAsync_WithOnlyOneModOfPair_NoConflict()
    {
        // Arrange
        var plugins = new[]
        {
            new PluginInfo { FormIdPrefix = "E7", PluginName = "BetterPowerArmor.esp" },
            new PluginInfo { FormIdPrefix = "E8", PluginName = "OtherMod.esp" }
        };

        _detector.Configuration = new ModConfiguration
        {
            ConflictingMods = new Dictionary<string, string>
            {
                ["betterpowerarmor | knockoutframework"] = "Conflict message"
            }
        };

        // Act
        var result = await _detector.DetectAsync(plugins, new HashSet<string>());

        // Assert
        result.Conflicts.Should().BeEmpty();
    }

    #endregion

    #region Important Mods Tests

    [Fact]
    public async Task DetectAsync_WithImportantModInstalled_DetectsAsInstalled()
    {
        // Arrange
        var plugins = new[]
        {
            new PluginInfo { FormIdPrefix = "E7", PluginName = "CanarySaveFileMonitor.esp" }
        };

        _detector.Configuration = new ModConfiguration
        {
            ImportantMods = new Dictionary<string, string>
            {
                ["CanarySaveFileMonitor | Canary Save File Monitor"] = "Highly recommended mod."
            }
        };

        // Act
        var result = await _detector.DetectAsync(plugins, new HashSet<string>());

        // Assert
        result.ImportantMods.Should().HaveCount(1);
        result.ImportantMods[0].DisplayName.Should().Be("Canary Save File Monitor");
        result.ImportantMods[0].IsInstalled.Should().BeTrue();
    }

    [Fact]
    public async Task DetectAsync_WithImportantModMissing_DetectsAsMissing()
    {
        // Arrange
        var plugins = new[]
        {
            new PluginInfo { FormIdPrefix = "E7", PluginName = "OtherMod.esp" }
        };

        _detector.Configuration = new ModConfiguration
        {
            ImportantMods = new Dictionary<string, string>
            {
                ["CanarySaveFileMonitor | Canary Save File Monitor"] = "Highly recommended mod."
            }
        };

        // Act
        var result = await _detector.DetectAsync(plugins, new HashSet<string>());

        // Assert
        result.ImportantMods.Should().HaveCount(1);
        result.ImportantMods[0].DisplayName.Should().Be("Canary Save File Monitor");
        result.ImportantMods[0].IsInstalled.Should().BeFalse();
        result.ImportantMods[0].Warning.Should().NotBeNull();
    }

    [Fact]
    public async Task DetectAsync_WithXseModule_DetectsImportantMod()
    {
        // Arrange
        var plugins = Array.Empty<PluginInfo>();
        var xseModules = new HashSet<string> { "BakaScrapHeap.dll" };

        _detector.Configuration = new ModConfiguration
        {
            ImportantMods = new Dictionary<string, string>
            {
                ["BakaScrapHeap | Baka ScrapHeap"] = "Recommended for stability."
            }
        };

        // Act
        var result = await _detector.DetectAsync(plugins, xseModules);

        // Assert
        result.ImportantMods.Should().HaveCount(1);
        result.ImportantMods[0].IsInstalled.Should().BeTrue();
    }

    #endregion

    #region Report Generation Tests

    [Fact]
    public void CreateReportFragment_WithProblematicMods_GeneratesSection()
    {
        // Arrange
        var result = new ModDetectionResult
        {
            ProblematicMods = new[]
            {
                new DetectedMod
                {
                    ModName = "Test Mod",
                    PluginFormId = "E7",
                    Warning = "This mod causes issues.",
                    Category = ModCategory.FrequentCrashes
                }
            }
        };

        // Act
        var fragment = _detector.CreateReportFragment(result);

        // Assert
        fragment.HasContent.Should().BeTrue();
        string.Join("\n", fragment.Lines).Should().Contain("Detected Mod Issues");
        string.Join("\n", fragment.Lines).Should().Contain("[!] FOUND");
        string.Join("\n", fragment.Lines).Should().Contain("E7");
    }

    [Fact]
    public void CreateReportFragment_WithConflicts_GeneratesConflictSection()
    {
        // Arrange
        var result = new ModDetectionResult
        {
            Conflicts = new[]
            {
                new ModConflict
                {
                    Mod1 = "ModA",
                    Mod2 = "ModB",
                    Warning = "These mods conflict with each other."
                }
            }
        };

        // Act
        var fragment = _detector.CreateReportFragment(result);

        // Assert
        fragment.HasContent.Should().BeTrue();
        string.Join("\n", fragment.Lines).Should().Contain("Mod Conflicts");
        string.Join("\n", fragment.Lines).Should().Contain("ModA");
        string.Join("\n", fragment.Lines).Should().Contain("ModB");
    }

    [Fact]
    public void CreateReportFragment_WithImportantMods_GeneratesStatusSection()
    {
        // Arrange
        var result = new ModDetectionResult
        {
            ImportantMods = new[]
            {
                new ImportantModStatus
                {
                    DisplayName = "Recommended Mod",
                    IsInstalled = true
                },
                new ImportantModStatus
                {
                    DisplayName = "Missing Mod",
                    IsInstalled = false,
                    Warning = "Please install this mod."
                }
            }
        };

        // Act
        var fragment = _detector.CreateReportFragment(result);

        // Assert
        fragment.HasContent.Should().BeTrue();
        string.Join("\n", fragment.Lines).Should().Contain("Important Mods Status");
        string.Join("\n", fragment.Lines).Should().Contain("✔️");
        string.Join("\n", fragment.Lines).Should().Contain("❌");
    }

    [Fact]
    public void CreateReportFragment_WithNoIssues_ReturnsEmptyFragment()
    {
        // Arrange
        var result = new ModDetectionResult();

        // Act
        var fragment = _detector.CreateReportFragment(result);

        // Assert
        fragment.HasContent.Should().BeFalse();
    }

    #endregion

    #region Edge Cases

    [Fact]
    public async Task DetectAsync_WithEmptyConfiguration_ReturnsNoIssues()
    {
        // Arrange
        var plugins = new[]
        {
            new PluginInfo { FormIdPrefix = "E7", PluginName = "SomeMod.esp" }
        };

        _detector.Configuration = ModConfiguration.Empty;

        // Act
        var result = await _detector.DetectAsync(plugins, new HashSet<string>());

        // Assert
        result.ProblematicMods.Should().BeEmpty();
        result.Conflicts.Should().BeEmpty();
        result.HasIssues.Should().BeFalse();
    }

    [Fact]
    public async Task DetectAsync_WithEmptyPluginList_ReturnsNoIssues()
    {
        // Arrange
        var plugins = Array.Empty<PluginInfo>();

        _detector.Configuration = new ModConfiguration
        {
            FrequentCrashMods = new Dictionary<string, string>
            {
                ["TestMod"] = "Test warning"
            }
        };

        // Act
        var result = await _detector.DetectAsync(plugins, new HashSet<string>());

        // Assert
        result.ProblematicMods.Should().BeEmpty();
    }

    [Fact]
    public async Task DetectAsync_WithCancellation_ThrowsOperationCancelledException()
    {
        // Arrange
        var plugins = new[]
        {
            new PluginInfo { FormIdPrefix = "E7", PluginName = "Test.esp" }
        };

        var cts = new CancellationTokenSource();
        cts.Cancel();

        // Act & Assert
        await Assert.ThrowsAsync<OperationCanceledException>(() =>
            _detector.DetectAsync(plugins, new HashSet<string>(), cancellationToken: cts.Token));
    }

    [Fact]
    public async Task DetectAsync_PatternMatchesPartialPluginName()
    {
        // Arrange - Pattern "DamageThreshold" should match "DamageThresholdFramework.esp"
        var plugins = new[]
        {
            new PluginInfo { FormIdPrefix = "E7", PluginName = "DamageThresholdFramework.esp" }
        };

        _detector.Configuration = new ModConfiguration
        {
            FrequentCrashMods = new Dictionary<string, string>
            {
                ["DamageThreshold"] = "Damage Threshold\nPartial match test."
            }
        };

        // Act
        var result = await _detector.DetectAsync(plugins, new HashSet<string>());

        // Assert
        result.ProblematicMods.Should().HaveCount(1);
    }

    #endregion
}
