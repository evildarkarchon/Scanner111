using FluentAssertions;
using Microsoft.Extensions.Logging;
using NSubstitute;
using Scanner111.Core.Analysis;
using Scanner111.Core.Analysis.Analyzers;
using Scanner111.Core.Configuration;
using Scanner111.Core.Models;
using Scanner111.Core.Services;

namespace Scanner111.Test.Analysis.Analyzers;

/// <summary>
///     Unit tests for ModDetectionAnalyzer.
///     Tests mod detection functionality including problematic mods, conflicts, and important mods.
/// </summary>
public class ModDetectionAnalyzerTests
{
    private readonly ILogger<ModDetectionAnalyzer> _logger;
    private readonly IModDatabase _mockModDatabase;
    private readonly IAsyncYamlSettingsCore _mockYamlCore;
    private readonly ModDetectionAnalyzer _analyzer;

    public ModDetectionAnalyzerTests()
    {
        _logger = Substitute.For<ILogger<ModDetectionAnalyzer>>();
        _mockModDatabase = Substitute.For<IModDatabase>();
        _mockYamlCore = Substitute.For<IAsyncYamlSettingsCore>();
        _analyzer = new ModDetectionAnalyzer(_logger, _mockModDatabase);
    }

    [Fact]
    public void Constructor_WithNullLogger_ThrowsArgumentNullException()
    {
        // Act & Assert
        var act = () => new ModDetectionAnalyzer(null!, _mockModDatabase);
        act.Should().Throw<ArgumentNullException>().WithParameterName("logger");
    }

    [Fact]
    public void Constructor_WithNullModDatabase_ThrowsArgumentNullException()
    {
        // Act & Assert
        var act = () => new ModDetectionAnalyzer(_logger, null!);
        act.Should().Throw<ArgumentNullException>().WithParameterName("modDatabase");
    }

    [Fact]
    public void Properties_ReturnsExpectedValues()
    {
        // Assert
        _analyzer.Name.Should().Be("ModDetectionAnalyzer");
        _analyzer.DisplayName.Should().Be("Mod Detection Analysis");
        _analyzer.Priority.Should().Be(35);
        _analyzer.Timeout.Should().Be(TimeSpan.FromMinutes(1));
    }

    [Fact]
    public async Task DetectProblematicModsAsync_WithEmptyPlugins_ReturnsEmpty()
    {
        // Arrange
        var emptyPlugins = new Dictionary<string, string>();

        // Act
        var result = await _analyzer.DetectProblematicModsAsync(emptyPlugins);

        // Assert
        result.Should().BeEmpty();
    }

    [Fact]
    public async Task DetectProblematicModsAsync_WithNullPlugins_ThrowsArgumentNullException()
    {
        // Act & Assert
        var act = async () => await _analyzer.DetectProblematicModsAsync(null!);
        await act.Should().ThrowAsync<ArgumentNullException>();
    }

    [Fact]
    public async Task DetectProblematicModsAsync_WithMatchingMods_ReturnsWarnings()
    {
        // Arrange
        var crashLogPlugins = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            { "ScrapEverything.esp", "[00] ScrapEverything.esp" },
            { "SpringCleaning.esp", "[01] SpringCleaning.esp" },
            { "SomethingElse.esp", "[02] SomethingElse.esp" }
        };

        var categories = new[] { "FREQ" };
        var freqMods = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            { "Scrap Everything", "Scrap Everything warning message" },
            { "Spring Cleaning", "Spring Cleaning warning message" }
        };

        _mockModDatabase.GetModWarningCategoriesAsync(Arg.Any<CancellationToken>())
            .Returns(categories);
        _mockModDatabase.LoadModWarningsAsync("FREQ", Arg.Any<CancellationToken>())
            .Returns(freqMods);

        // Act
        var result = await _analyzer.DetectProblematicModsAsync(crashLogPlugins);

        // Assert
        result.Should().HaveCount(2);
        
        var scrapEverythingWarning = result.FirstOrDefault(w => w.ModName.Contains("scrap", StringComparison.OrdinalIgnoreCase));
        scrapEverythingWarning.Should().NotBeNull();
        scrapEverythingWarning!.Warning.Should().Be("Scrap Everything warning message");
        scrapEverythingWarning.Category.Should().Be("FREQ");
        scrapEverythingWarning.Severity.Should().Be(AnalysisSeverity.Warning);

        var springCleaningWarning = result.FirstOrDefault(w => w.ModName.Contains("spring", StringComparison.OrdinalIgnoreCase));
        springCleaningWarning.Should().NotBeNull();
        springCleaningWarning!.Warning.Should().Be("Spring Cleaning warning message");
    }

    [Fact]
    public async Task DetectModConflictsAsync_WithConflictingMods_ReturnsConflicts()
    {
        // Arrange
        var crashLogPlugins = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            { "BetterPowerArmor.esp", "[00] BetterPowerArmor.esp" },
            { "KnockoutFramework.esp", "[01] KnockoutFramework.esp" },
            { "SomethingElse.esp", "[02] SomethingElse.esp" }
        };

        var conflicts = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            { "betterpowerarmor | Knockout Framework", "Better Power Armor conflicts with Knockout Framework" },
            { "mod1 | mod2", "Some other conflict that shouldn't match" }
        };

        _mockModDatabase.LoadModConflictsAsync(Arg.Any<CancellationToken>())
            .Returns(conflicts);

        // Act
        var result = await _analyzer.DetectModConflictsAsync(crashLogPlugins);

        // Assert
        result.Should().HaveCount(1);
        var conflict = result.First();
        conflict.FirstMod.Should().Be("betterpowerarmor");
        conflict.SecondMod.Should().Be("knockout framework");
        conflict.ConflictWarning.Should().Be("Better Power Armor conflicts with Knockout Framework");
        conflict.Severity.Should().Be(AnalysisSeverity.Warning);
        conflict.FoundPluginIds.Should().HaveCount(2);
    }

    [Fact]
    public async Task DetectModConflictsAsync_WithNoConflicts_ReturnsEmpty()
    {
        // Arrange
        var crashLogPlugins = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            { "SafeMod.esp", "[00] SafeMod.esp" }
        };

        var conflicts = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            { "mod1 | mod2", "Some conflict that won't match" }
        };

        _mockModDatabase.LoadModConflictsAsync(Arg.Any<CancellationToken>())
            .Returns(conflicts);

        // Act
        var result = await _analyzer.DetectModConflictsAsync(crashLogPlugins);

        // Assert
        result.Should().BeEmpty();
    }

    [Fact]
    public async Task DetectImportantModsAsync_WithInstalledMods_ReturnsCorrectStatus()
    {
        // Arrange
        var crashLogPlugins = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            { "HighFPSPhysicsFix.dll", "[00] HighFPSPhysicsFix.dll" },
            { "WeaponDebrisCrashFix.dll", "[01] WeaponDebrisCrashFix.dll" }
        };

        var categories = new[] { "CORE" };
        var coreMods = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            { "HighFPSPhysicsFix | High FPS Physics Fix", "This is a mandatory patch that prevents game engine problems." },
            { "WeaponDebrisCrashFix.dll | Nvidia Weapon Debris Fix", "This is a mandatory patch required for almost all Nvidia GPUs." },
            { "PRP.esp | Previs Repair Pack", "This mod can vastly improve performance." }
        };

        _mockModDatabase.GetImportantModCategoriesAsync(Arg.Any<CancellationToken>())
            .Returns(categories);
        _mockModDatabase.LoadImportantModsAsync("CORE", Arg.Any<CancellationToken>())
            .Returns(coreMods);

        // Act
        var result = await _analyzer.DetectImportantModsAsync(crashLogPlugins, "nvidia");

        // Assert
        result.Should().HaveCount(3);

        var highFpsFix = result.FirstOrDefault(m => m.DisplayName == "High FPS Physics Fix");
        highFpsFix.Should().NotBeNull();
        highFpsFix!.IsInstalled.Should().BeTrue();
        highFpsFix.GetStatus().Should().Be(ModStatus.Installed);

        var weaponDebrisFix = result.FirstOrDefault(m => m.DisplayName == "Nvidia Weapon Debris Fix");
        weaponDebrisFix.Should().NotBeNull();
        weaponDebrisFix!.IsInstalled.Should().BeTrue();
        weaponDebrisFix.HasGpuRequirement.Should().BeTrue();
        weaponDebrisFix.RequiredGpuType.Should().Be("nvidia");
        weaponDebrisFix.DetectedGpuType.Should().Be("nvidia");
        weaponDebrisFix.GetStatus().Should().Be(ModStatus.Installed);

        var prp = result.FirstOrDefault(m => m.DisplayName == "Previs Repair Pack");
        prp.Should().NotBeNull();
        prp!.IsInstalled.Should().BeFalse();
        prp.GetStatus().Should().Be(ModStatus.NotInstalled);
    }

    [Fact]
    public async Task DetectImportantModsAsync_WithGpuMismatch_ReturnsGpuIssue()
    {
        // Arrange
        var crashLogPlugins = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            { "WeaponDebrisCrashFix.dll", "[00] WeaponDebrisCrashFix.dll" }
        };

        var categories = new[] { "CORE" };
        var coreMods = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            { "WeaponDebrisCrashFix.dll | Nvidia Weapon Debris Fix", "This is required for Nvidia GPUs." }
        };

        _mockModDatabase.GetImportantModCategoriesAsync(Arg.Any<CancellationToken>())
            .Returns(categories);
        _mockModDatabase.LoadImportantModsAsync("CORE", Arg.Any<CancellationToken>())
            .Returns(coreMods);

        // Act - mod is installed but user has AMD GPU
        var result = await _analyzer.DetectImportantModsAsync(crashLogPlugins, "amd");

        // Assert
        result.Should().HaveCount(1);
        var mod = result.First();
        mod.IsInstalled.Should().BeTrue();
        mod.HasGpuCompatibilityIssue.Should().BeTrue();
        mod.RequiredGpuType.Should().Be("nvidia");
        mod.DetectedGpuType.Should().Be("amd");
        mod.GetStatus().Should().Be(ModStatus.InstalledWithGpuIssue);
    }

    [Fact]
    public async Task PerformComprehensiveAnalysisAsync_WithVariousIssues_CombinesResults()
    {
        // Arrange
        var crashLogPlugins = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            { "ScrapEverything.esp", "[00] ScrapEverything.esp" },
            { "BetterPowerArmor.esp", "[01] BetterPowerArmor.esp" },
            { "KnockoutFramework.esp", "[02] KnockoutFramework.esp" },
            { "HighFPSPhysicsFix.dll", "[03] HighFPSPhysicsFix.dll" }
        };

        // Setup problematic mods
        _mockModDatabase.GetModWarningCategoriesAsync(Arg.Any<CancellationToken>())
            .Returns(new[] { "FREQ" });
        _mockModDatabase.LoadModWarningsAsync("FREQ", Arg.Any<CancellationToken>())
            .Returns(new Dictionary<string, string>
            {
                { "Scrap Everything", "Problematic mod warning" }
            });

        // Setup conflicts
        _mockModDatabase.LoadModConflictsAsync(Arg.Any<CancellationToken>())
            .Returns(new Dictionary<string, string>
            {
                { "betterpowerarmor | KnockoutFramework", "These mods conflict" }
            });

        // Setup important mods
        _mockModDatabase.GetImportantModCategoriesAsync(Arg.Any<CancellationToken>())
            .Returns(new[] { "CORE" });
        _mockModDatabase.LoadImportantModsAsync("CORE", Arg.Any<CancellationToken>())
            .Returns(new Dictionary<string, string>
            {
                { "HighFPSPhysicsFix | High FPS Physics Fix", "Mandatory fix" },
                { "PRP.esp | Previs Repair Pack", "Performance improvement" }
            });

        // Act
        var result = await _analyzer.PerformComprehensiveAnalysisAsync(crashLogPlugins, "nvidia");

        // Assert
        result.DetectedWarnings.Should().HaveCount(1);
        result.DetectedConflicts.Should().HaveCount(1);
        result.ImportantMods.Should().HaveCount(2);
        result.CrashLogPlugins.Should().BeEquivalentTo(crashLogPlugins);
        result.DetectedGpuType.Should().Be("nvidia");
    }

    [Fact]
    public async Task CanAnalyzeAsync_WithNoDatabaseAvailable_ReturnsFalse()
    {
        // Arrange
        var context = new AnalysisContext("test.log", _mockYamlCore);
        context.SetSharedData("CrashLogPlugins", new Dictionary<string, string> { { "test.esp", "test" } });
        
        _mockModDatabase.IsAvailableAsync(Arg.Any<CancellationToken>())
            .Returns(false);

        // Act
        var result = await _analyzer.CanAnalyzeAsync(context);

        // Assert
        result.Should().BeFalse();
    }

    [Fact]
    public async Task CanAnalyzeAsync_WithNoPluginsInContext_ReturnsFalse()
    {
        // Arrange
        var context = new AnalysisContext("test.log", _mockYamlCore);
        
        _mockModDatabase.IsAvailableAsync(Arg.Any<CancellationToken>())
            .Returns(true);

        // Act
        var result = await _analyzer.CanAnalyzeAsync(context);

        // Assert
        result.Should().BeFalse();
    }

    [Fact]
    public async Task CanAnalyzeAsync_WithDatabaseAndPlugins_ReturnsTrue()
    {
        // Arrange
        var context = new AnalysisContext("test.log", _mockYamlCore);
        context.SetSharedData("CrashLogPlugins", new Dictionary<string, string> { { "test.esp", "test" } });
        
        _mockModDatabase.IsAvailableAsync(Arg.Any<CancellationToken>())
            .Returns(true);

        // Act
        var result = await _analyzer.CanAnalyzeAsync(context);

        // Assert
        result.Should().BeTrue();
    }

    [Fact]
    public async Task PerformAnalysisAsync_WithNoPlugins_ReturnsInfoResult()
    {
        // Arrange
        var context = new AnalysisContext("test.log", _mockYamlCore);
        // No plugins in context

        // Act
        var result = await _analyzer.AnalyzeAsync(context, CancellationToken.None);

        // Assert
        result.Success.Should().BeTrue();
        result.Severity.Should().Be(AnalysisSeverity.Info);
        result.Fragment.Should().NotBeNull();
        result.Metadata.Should().ContainKeys("ProblematicModsCount", "ConflictsCount", "ImportantModsCount");
    }

    [Theory]
    [InlineData("scrap everything", "ScrapEverything.esp", true)]
    [InlineData("spring cleaning", "SpringCleaning.esp", true)]
    [InlineData("nonexistent mod", "NonexistentMod.esp", false)]
    [InlineData("SCRAP EVERYTHING", "scrapeverything.esp", true)] // Case insensitive
    public async Task DetectProblematicModsAsync_ModMatching_WorksCorrectly(
        string modName, string pluginName, bool shouldMatch)
    {
        // Arrange
        var crashLogPlugins = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            { pluginName, $"[00] {pluginName}" }
        };

        _mockModDatabase.GetModWarningCategoriesAsync(Arg.Any<CancellationToken>())
            .Returns(new[] { "FREQ" });
        _mockModDatabase.LoadModWarningsAsync("FREQ", Arg.Any<CancellationToken>())
            .Returns(new Dictionary<string, string>
            {
                { modName, $"Warning for {modName}" }
            });

        // Act
        var result = await _analyzer.DetectProblematicModsAsync(crashLogPlugins);

        // Assert
        if (shouldMatch)
        {
            result.Should().HaveCount(1);
            result.First().Warning.Should().Be($"Warning for {modName}");
        }
        else
        {
            result.Should().BeEmpty();
        }
    }
}