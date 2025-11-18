using FluentAssertions;
using Scanner111.Common.Models.Analysis;
using Scanner111.Common.Services.Analysis;

namespace Scanner111.Common.Tests.Services.Analysis;

/// <summary>
/// Tests for PluginAnalyzer and related components.
/// </summary>
public class PluginAnalyzerTests
{
    private readonly PluginAnalyzer _analyzer;
    private readonly PluginListParser _parser;
    private readonly PluginLimitChecker _limitChecker;

    public PluginAnalyzerTests()
    {
        _analyzer = new PluginAnalyzer();
        _parser = new PluginListParser();
        _limitChecker = new PluginLimitChecker();
    }

    [Fact]
    public void ParsePluginList_WithValidFormat_ExtractsPluginsCorrectly()
    {
        // Arrange
        var pluginLines = new[]
        {
            "[E7]     StartMeUp.esp",
            "[E8]     PlayerComments.esp",
            "[FE:000] PPF.esm",
            "[FE:001] Resources Expanded - Recipes.esl"
        };
        var segment = new LogSegment { Lines = pluginLines };

        // Act
        var plugins = _parser.ParsePluginList(segment);

        // Assert
        plugins.Should().HaveCount(4);
        plugins[0].FormIdPrefix.Should().Be("E7");
        plugins[0].PluginName.Should().Be("StartMeUp.esp");
        plugins[0].IsLightPlugin.Should().BeFalse();

        plugins[2].FormIdPrefix.Should().Be("FE:000");
        plugins[2].PluginName.Should().Be("PPF.esm");
        plugins[2].IsLightPlugin.Should().BeTrue();
    }

    [Theory]
    [InlineData(new[] { "TestMod.esp", "AnotherMod.esm" }, new[] { "Test.*" }, 1)]
    [InlineData(new[] { "ModA.esp", "ModB.esp" }, new[] { "Mod[AB].*" }, 2)]
    [InlineData(new[] { "Example.esp", "Test.esp" }, new[] { "Nonexistent.*" }, 0)]
    public void MatchPluginPatterns_WithRegex_MatchesCorrectly(
        string[] plugins, string[] patterns, int expectedMatches)
    {
        // Arrange & Act
        var matches = _analyzer.MatchPluginPatterns(plugins, patterns);

        // Assert
        matches.Should().HaveCount(expectedMatches);
    }

    [Fact]
    public void MatchPluginPatterns_IsCaseInsensitive()
    {
        // Arrange
        var plugins = new[] { "TestMod.ESP", "AnotherMod.ESM" };
        var patterns = new[] { "testmod.*" };

        // Act
        var matches = _analyzer.MatchPluginPatterns(plugins, patterns);

        // Assert
        matches.Should().HaveCount(1);
        matches[0].Should().BeEquivalentTo("TestMod.ESP");
    }

    [Fact]
    public void CheckLimits_WithinLimits_NoWarnings()
    {
        // Arrange
        var plugins = Enumerable.Range(0, 100)
            .Select(i => new PluginInfo
            {
                FormIdPrefix = i.ToString("X2"),
                PluginName = $"Plugin{i}.esp"
            })
            .ToList();

        // Act
        var result = _limitChecker.CheckLimits(plugins);

        // Assert
        result.FullPluginCount.Should().Be(100);
        result.LightPluginCount.Should().Be(0);
        result.ApproachingLimit.Should().BeFalse();
        result.Warnings.Should().BeEmpty();
    }

    [Fact]
    public void CheckLimits_ApproachingFullLimit_GeneratesWarning()
    {
        // Arrange - Create 245 regular plugins (>= 240 warning threshold)
        var plugins = Enumerable.Range(0, 245)
            .Select(i => new PluginInfo
            {
                FormIdPrefix = i.ToString("X2"),
                PluginName = $"Plugin{i}.esp"
            })
            .ToList();

        // Act
        var result = _limitChecker.CheckLimits(plugins);

        // Assert
        result.FullPluginCount.Should().Be(245);
        result.ApproachingLimit.Should().BeTrue();
        result.Warnings.Should().NotBeEmpty();
        result.Warnings.Should().Contain(w => w.Contains("WARNING"));
    }

    [Fact]
    public void CheckLimits_AtFullLimit_GeneratesCriticalWarning()
    {
        // Arrange - Create exactly 254 plugins (at limit)
        var plugins = Enumerable.Range(0, 254)
            .Select(i => new PluginInfo
            {
                FormIdPrefix = i.ToString("X2"),
                PluginName = $"Plugin{i}.esp"
            })
            .ToList();

        // Act
        var result = _limitChecker.CheckLimits(plugins);

        // Assert
        result.FullPluginCount.Should().Be(254);
        result.ApproachingLimit.Should().BeTrue();
        result.Warnings.Should().Contain(w => w.Contains("CRITICAL"));
    }

    [Fact]
    public void CheckLimits_MixedPluginTypes_CountsCorrectly()
    {
        // Arrange - Mix of regular and light plugins
        var plugins = new List<PluginInfo>();

        // Add 100 regular plugins
        plugins.AddRange(Enumerable.Range(0, 100)
            .Select(i => new PluginInfo
            {
                FormIdPrefix = i.ToString("X2"),
                PluginName = $"Regular{i}.esp"
            }));

        // Add 50 light plugins
        plugins.AddRange(Enumerable.Range(0, 50)
            .Select(i => new PluginInfo
            {
                FormIdPrefix = $"FE:{i:000}",
                PluginName = $"Light{i}.esl"
            }));

        // Act
        var result = _limitChecker.CheckLimits(plugins);

        // Assert
        result.FullPluginCount.Should().Be(100);
        result.LightPluginCount.Should().Be(50);
    }

    [Fact]
    public async Task AnalyzeAsync_WithPluginSegment_ExtractsPlugins()
    {
        // Arrange
        var segments = new[]
        {
            new LogSegment
            {
                Name = "PLUGINS",
                Lines = new[]
                {
                    "[00] Fallout4.esm",
                    "[01] DLCRobot.esm",
                    "[E7] StartMeUp.esp"
                }
            }
        };

        // Act
        var result = await _analyzer.AnalyzeAsync(segments);

        // Assert
        result.Plugins.Should().HaveCount(3);
        result.RegularPluginCount.Should().Be(3);
        result.LightPluginCount.Should().Be(0);
    }

    [Fact]
    public async Task AnalyzeAsync_WithoutPluginSegment_ReturnsWarning()
    {
        // Arrange
        var segments = new[]
        {
            new LogSegment { Name = "SYSTEM SPECS" }
        };

        // Act
        var result = await _analyzer.AnalyzeAsync(segments);

        // Assert
        result.Plugins.Should().BeEmpty();
        result.Warnings.Should().Contain(w => w.Contains("No PLUGINS segment"));
    }

    [Fact]
    public void ParsePluginList_WithNullSegment_ReturnsEmpty()
    {
        // Act
        var plugins = _parser.ParsePluginList(null!);

        // Assert
        plugins.Should().BeEmpty();
    }

    [Fact]
    public void ParsePluginList_WithMalformedLines_SkipsInvalidLines()
    {
        // Arrange
        var pluginLines = new[]
        {
            "[E7] ValidPlugin.esp",
            "Invalid line without brackets",
            "[E8] AnotherValid.esp",
            "   ",
            ""
        };
        var segment = new LogSegment { Lines = pluginLines };

        // Act
        var plugins = _parser.ParsePluginList(segment);

        // Assert
        plugins.Should().HaveCount(2);
        plugins[0].PluginName.Should().Be("ValidPlugin.esp");
        plugins[1].PluginName.Should().Be("AnotherValid.esp");
    }

    [Fact]
    public void MatchPluginPatterns_WithMultiplePatterns_CombinesResults()
    {
        // Arrange
        var plugins = new[] { "ModA.esp", "ModB.esp", "TestC.esp" };
        var patterns = new[] { "Mod.*", "Test.*" };

        // Act
        var matches = _analyzer.MatchPluginPatterns(plugins, patterns);

        // Assert
        matches.Should().HaveCount(3);
    }

    [Fact]
    public void MatchPluginPatterns_WithDuplicateMatches_ReturnsUnique()
    {
        // Arrange
        var plugins = new[] { "TestMod.esp" };
        var patterns = new[] { "Test.*", ".*Mod.*" };

        // Act
        var matches = _analyzer.MatchPluginPatterns(plugins, patterns);

        // Assert
        matches.Should().HaveCount(1);
    }
}
