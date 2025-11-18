using FluentAssertions;
using Scanner111.Common.Models.Analysis;
using Scanner111.Common.Services.Reporting;

namespace Scanner111.Common.Tests.Services.Reporting;

/// <summary>
/// Tests for ReportSections.
/// </summary>
public class ReportSectionsTests
{
    [Fact]
    public void CreateHeader_WithFullCrashHeader_IncludesAllFields()
    {
        // Arrange
        var header = new CrashHeader
        {
            GameVersion = "1.10.163.0",
            CrashGeneratorVersion = "Buffout 4 v1.26.2",
            MainError = "EXCEPTION_ACCESS_VIOLATION",
            CrashTimestamp = new DateTime(2023, 12, 7, 2, 24, 27)
        };

        // Act
        var result = ReportSections.CreateHeader(header, "Fallout 4");

        // Assert
        result.HasContent.Should().BeTrue();
        result.Lines.Should().Contain(line => line.Contains("Fallout 4"));
        result.Lines.Should().Contain(line => line.Contains("1.10.163.0"));
        result.Lines.Should().Contain(line => line.Contains("Buffout 4 v1.26.2"));
        result.Lines.Should().Contain(line => line.Contains("EXCEPTION_ACCESS_VIOLATION"));
        result.Lines.Should().Contain(line => line.Contains("2023-12-07"));
    }

    [Fact]
    public void CreateHeader_WithoutTimestamp_ExcludesTimestamp()
    {
        // Arrange
        var header = new CrashHeader
        {
            GameVersion = "1.10.163.0",
            CrashGeneratorVersion = "Buffout 4 v1.26.2",
            MainError = "EXCEPTION_ACCESS_VIOLATION",
            CrashTimestamp = null
        };

        // Act
        var result = ReportSections.CreateHeader(header, "Fallout 4");

        // Assert
        result.HasContent.Should().BeTrue();
        result.Lines.Should().NotContain(line => line.Contains("Crash Time"));
    }

    [Fact]
    public void CreatePluginSummary_WithNoPlugins_ReturnsEmptyFragment()
    {
        // Arrange
        var plugins = Array.Empty<PluginInfo>();

        // Act
        var result = ReportSections.CreatePluginSummary(plugins);

        // Assert
        result.HasContent.Should().BeFalse();
    }

    [Fact]
    public void CreatePluginSummary_WithPlugins_IncludesCounts()
    {
        // Arrange
        var plugins = new[]
        {
            new PluginInfo { FormIdPrefix = "E7", PluginName = "Mod1.esp" },
            new PluginInfo { FormIdPrefix = "E8", PluginName = "Mod2.esp" },
            new PluginInfo { FormIdPrefix = "FE:000", PluginName = "LightMod.esl" },
            new PluginInfo { FormIdPrefix = "FE:001", PluginName = "LightMod2.esl" }
        };

        // Act
        var result = ReportSections.CreatePluginSummary(plugins);

        // Assert
        result.HasContent.Should().BeTrue();
        result.Lines.Should().Contain(line => line.Contains("Total Plugins") && line.Contains("4"));
        result.Lines.Should().Contain(line => line.Contains("Full Plugins") && line.Contains("2"));
        result.Lines.Should().Contain(line => line.Contains("Light Plugins") && line.Contains("2"));
    }

    [Fact]
    public void CreatePluginSummary_WithHighFullPluginCount_IncludesWarning()
    {
        // Arrange
        var plugins = Enumerable.Range(0, 245)
            .Select(i => new PluginInfo { FormIdPrefix = i.ToString("X2"), PluginName = $"Mod{i}.esp" })
            .ToList();

        // Act
        var result = ReportSections.CreatePluginSummary(plugins);

        // Assert
        result.Lines.Should().Contain(line => line.Contains("Warning") && line.Contains("full plugin limit"));
    }

    [Fact]
    public void CreatePluginSummary_WithHighLightPluginCount_IncludesWarning()
    {
        // Arrange
        var plugins = Enumerable.Range(0, 3950)
            .Select(i => new PluginInfo { FormIdPrefix = $"FE:{i:000}", PluginName = $"LightMod{i}.esl" })
            .ToList();

        // Act
        var result = ReportSections.CreatePluginSummary(plugins);

        // Assert
        result.Lines.Should().Contain(line => line.Contains("Warning") && line.Contains("light plugin limit"));
    }

    [Fact]
    public void CreatePluginSummary_WithSafeCounts_NoWarnings()
    {
        // Arrange
        var plugins = new[]
        {
            new PluginInfo { FormIdPrefix = "E7", PluginName = "Mod1.esp" },
            new PluginInfo { FormIdPrefix = "FE:000", PluginName = "LightMod.esl" }
        };

        // Act
        var result = ReportSections.CreatePluginSummary(plugins);

        // Assert
        result.Lines.Should().NotContain(line => line.Contains("Warning"));
    }

    [Fact]
    public void CreateWarningsSection_WithNoWarnings_ReturnsEmptyFragment()
    {
        // Arrange
        var warnings = Array.Empty<string>();

        // Act
        var result = ReportSections.CreateWarningsSection(warnings);

        // Assert
        result.HasContent.Should().BeFalse();
    }

    [Fact]
    public void CreateWarningsSection_WithWarnings_IncludesAllWarnings()
    {
        // Arrange
        var warnings = new[]
        {
            "Memory manager should be disabled",
            "Outdated crash logger version"
        };

        // Act
        var result = ReportSections.CreateWarningsSection(warnings);

        // Assert
        result.HasContent.Should().BeTrue();
        result.Lines.Should().Contain(line => line.Contains("Memory manager"));
        result.Lines.Should().Contain(line => line.Contains("Outdated crash logger"));
        result.Lines.Should().Contain("## Warnings");
    }

    [Fact]
    public void CreateRecommendationsSection_WithNoRecommendations_ReturnsEmptyFragment()
    {
        // Arrange
        var recommendations = Array.Empty<string>();

        // Act
        var result = ReportSections.CreateRecommendationsSection(recommendations);

        // Assert
        result.HasContent.Should().BeFalse();
    }

    [Fact]
    public void CreateRecommendationsSection_WithRecommendations_NumbersThem()
    {
        // Arrange
        var recommendations = new[]
        {
            "Update crash logger to latest version",
            "Disable memory manager in Buffout 4 settings",
            "Check for mod conflicts"
        };

        // Act
        var result = ReportSections.CreateRecommendationsSection(recommendations);

        // Assert
        result.HasContent.Should().BeTrue();
        result.Lines.Should().Contain(line => line.StartsWith("1. ") && line.Contains("Update crash logger"));
        result.Lines.Should().Contain(line => line.StartsWith("2. ") && line.Contains("Disable memory manager"));
        result.Lines.Should().Contain(line => line.StartsWith("3. ") && line.Contains("Check for mod conflicts"));
        result.Lines.Should().Contain("## Recommended Actions");
    }

    [Fact]
    public void CreateFooter_IncludesStandardFooter()
    {
        // Act
        var result = ReportSections.CreateFooter();

        // Assert
        result.HasContent.Should().BeTrue();
        result.Lines.Should().Contain(line => line.Contains("Scanner111"));
        result.Lines.Should().Contain("---");
        result.Lines.Should().Contain(line => line.Contains("crash log analysis") || line.Contains("Crash Log Reading"));
    }

    [Fact]
    public void CreateFooter_IncludesLinks()
    {
        // Act
        var result = ReportSections.CreateFooter();

        // Assert
        result.Lines.Should().Contain(line => line.Contains("[") && line.Contains("]") && line.Contains("("));
    }

    [Fact]
    public void ContentSections_UseMarkdownHeaders()
    {
        // Arrange
        var header = new CrashHeader { GameVersion = "1.0.0", CrashGeneratorVersion = "Test", MainError = "ERROR" };
        var plugins = new[] { new PluginInfo { FormIdPrefix = "00", PluginName = "Test.esp" } };
        var warnings = new[] { "Warning" };
        var recommendations = new[] { "Recommendation" };

        // Act
        var sections = new[]
        {
            ReportSections.CreateHeader(header, "Game"),
            ReportSections.CreatePluginSummary(plugins),
            ReportSections.CreateWarningsSection(warnings),
            ReportSections.CreateRecommendationsSection(recommendations)
            // Note: Footer excluded as it uses horizontal rules instead of headers
        };

        // Assert
        foreach (var section in sections)
        {
            if (section.HasContent)
            {
                var hasHeader = section.Lines.Any(line => line.StartsWith("#"));
                hasHeader.Should().BeTrue($"content sections should have markdown headers");
            }
        }
    }
}
