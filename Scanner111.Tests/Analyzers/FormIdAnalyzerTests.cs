using FluentAssertions;
using Scanner111.Core.Analyzers;
using Scanner111.Core.Models;
using Scanner111.Tests.TestHelpers;

namespace Scanner111.Tests.Analyzers;

/// <summary>
///     Unit tests for the <see cref="FormIdAnalyzer" /> class,
///     verifying its ability to correctly process and analyze crash logs
///     for identifying and reporting Form IDs.
/// </summary>
public class FormIdAnalyzerTests
{
    private readonly FormIdAnalyzer _analyzer;

    public FormIdAnalyzerTests()
    {
        var yamlSettings = new TestYamlSettingsProvider();
        var formIdDatabase = new TestFormIdDatabaseService();
        var appSettings = new TestApplicationSettingsService();
        _analyzer = new FormIdAnalyzer(yamlSettings, formIdDatabase, appSettings);
    }

    /// <summary>
    ///     Verifies that the <see cref="FormIdAnalyzer.AnalyzeAsync" /> method correctly analyzes
    ///     a crash log containing valid Form IDs and returns an instance of <see cref="FormIdAnalysisResult" />.
    /// </summary>
    /// <returns>
    ///     A task representing the asynchronous operation. The result is a <see cref="FormIdAnalysisResult" />
    ///     containing the analyzed Form IDs from the crash log.
    /// </returns>
    [Fact]
    public async Task AnalyzeAsync_WithValidFormIds_ReturnsFormIdAnalysisResult()
    {
        // Arrange
        var crashLog = new CrashLog
        {
            FilePath = "test.log",
            CallStack =
            [
                "  Form ID: 0x0001A332",
                "  Form ID: 0x00014E45",
                "  Form ID: 0xFF000000", // Should be skipped (starts with FF)
                "  Some other line"
            ],
            Plugins = new Dictionary<string, string>
            {
                { "TestPlugin.esp", "00" },
                { "AnotherPlugin.esp", "01" }
            }
        };

        // Act
        var result = await _analyzer.AnalyzeAsync(crashLog);

        // Assert
        result.Should().BeOfType<FormIdAnalysisResult>();
        var formIdResult = (FormIdAnalysisResult)result;

        formIdResult.AnalyzerName.Should().Be("FormID Analyzer");
        formIdResult.HasFindings.Should().BeTrue("form IDs were found in the crash log");
        formIdResult.FormIds.Should()
            .HaveCount(2, "two valid form IDs were found (FF000000 should be skipped)")
            .And.Contain("Form ID: 0001A332")
            .And.Contain("Form ID: 00014E45")
            .And.NotContain("Form ID: FF000000", "form IDs starting with FF should be filtered out");
    }

    /// <summary>
    ///     Verifies that the <see cref="FormIdAnalyzer.AnalyzeAsync" /> method processes a crash log
    ///     without any Form IDs and returns an instance of <see cref="FormIdAnalysisResult" /> with no findings.
    /// </summary>
    /// <returns>
    ///     A task representing the asynchronous operation. The result is a <see cref="FormIdAnalysisResult" />
    ///     indicating no Form IDs were identified in the provided crash log.
    /// </returns>
    [Fact]
    public async Task AnalyzeAsync_WithNoFormIds_ReturnsEmptyResult()
    {
        // Arrange
        var crashLog = new CrashLog
        {
            FilePath = "test.log",
            CallStack =
            [
                "  Some random line",
                "  Another line without FormID",
                "  Yet another line"
            ],
            Plugins = new Dictionary<string, string>()
        };

        // Act
        var result = await _analyzer.AnalyzeAsync(crashLog);

        // Assert
        var formIdResult = (FormIdAnalysisResult)result;
        formIdResult.HasFindings.Should().BeFalse("no form IDs were present in the crash log");
        formIdResult.FormIds.Should().BeEmpty();
        formIdResult.ReportText.Should().Contain("* COULDN'T FIND ANY FORM ID SUSPECTS *");
    }

    /// <summary>
    ///     Verifies that the <see cref="FormIdAnalyzer.AnalyzeAsync" /> method skips Form IDs
    ///     that start with "FF" when analyzing a crash log and returns a <see cref="FormIdAnalysisResult" />
    ///     containing only the valid Form IDs.
    /// </summary>
    /// <returns>
    ///     A task representing the asynchronous operation. The result is a <see cref="FormIdAnalysisResult" />
    ///     containing the filtered results with Form IDs that do not start with "FF".
    /// </returns>
    [Fact]
    public async Task AnalyzeAsync_WithFormIdsStartingWithFF_SkipsThose()
    {
        // Arrange
        var crashLog = new CrashLog
        {
            FilePath = "test.log",
            CallStack =
            [
                "  Form ID: 0xFF000001",
                "  Form ID: 0xFF000002",
                "  Form ID: 0x00012345"
            ],
            Plugins = new Dictionary<string, string>
            {
                { "TestPlugin.esp", "00" }
            }
        };

        // Act
        var result = await _analyzer.AnalyzeAsync(crashLog);

        // Assert
        var formIdResult = (FormIdAnalysisResult)result;
        formIdResult.HasFindings.Should().BeTrue("at least one valid form ID was found");
        formIdResult.FormIds.Should()
            .ContainSingle("only one form ID doesn't start with FF")
            .Which.Should().Be("Form ID: 00012345");
        formIdResult.FormIds.Should()
            .NotContain("Form ID: FF000001", "form IDs starting with FF should be filtered")
            .And.NotContain("Form ID: FF000002", "form IDs starting with FF should be filtered");
    }

    /// <summary>
    ///     Tests that the <see cref="FormIdAnalyzer.AnalyzeAsync" /> method generates a correct report
    ///     when analyzing a crash log with matching plugins.
    /// </summary>
    /// <returns>
    ///     A task representing the asynchronous operation. The result is a <see cref="FormIdAnalysisResult" />
    ///     which includes a detailed report of matched Form IDs with their corresponding plugins.
    /// </returns>
    [Fact]
    public async Task AnalyzeAsync_WithMatchingPlugins_GeneratesCorrectReport()
    {
        // Arrange
        var crashLog = new CrashLog
        {
            FilePath = "test.log",
            CallStack =
            [
                "  Form ID: 0x00012345",
                "  Form ID: 0x01006789"
            ],
            Plugins = new Dictionary<string, string>
            {
                { "TestPlugin.esp", "00" },
                { "AnotherPlugin.esp", "01" }
            }
        };

        // Act
        var result = await _analyzer.AnalyzeAsync(crashLog);

        // Assert
        var formIdResult = (FormIdAnalysisResult)result;
        formIdResult.HasFindings.Should().BeTrue("form IDs were found");
        formIdResult.ReportText.Should()
            .Contain("- Form ID: 00012345 | [TestPlugin.esp] | 1", "first form ID should match TestPlugin")
            .And.Contain("- Form ID: 01006789 | [AnotherPlugin.esp] | 1", "second form ID should match AnotherPlugin")
            .And.Contain("These Form IDs were caught by Buffout 4");
    }

    /// <summary>
    ///     Verifies that the <see cref="FormIdAnalyzer.AnalyzeAsync" /> method correctly counts occurrences
    ///     of duplicate Form IDs in a crash log and returns an accurate <see cref="FormIdAnalysisResult" />
    ///     report with the count of each Form ID.
    /// </summary>
    /// <returns>
    ///     A task representing the asynchronous operation. The result is a <see cref="FormIdAnalysisResult" />
    ///     containing the frequency count of each unique Form ID detected in the crash log.
    /// </returns>
    [Fact]
    public async Task AnalyzeAsync_WithDuplicateFormIds_CountsCorrectly()
    {
        // Arrange
        var crashLog = new CrashLog
        {
            FilePath = "test.log",
            CallStack =
            [
                "  Form ID: 0x00012345",
                "  Form ID: 0x00012345",
                "  Form ID: 0x00012345",
                "  Form ID: 0x00067890"
            ],
            Plugins = new Dictionary<string, string>
            {
                { "TestPlugin.esp", "00" }
            }
        };

        // Act
        var result = await _analyzer.AnalyzeAsync(crashLog);

        // Assert
        var formIdResult = (FormIdAnalysisResult)result;
        formIdResult.HasFindings.Should().BeTrue("form IDs were found");
        formIdResult.ReportText.Should()
            .Contain("- Form ID: 00012345 | [TestPlugin.esp] | 3", "form ID 00012345 appeared 3 times")
            .And.Contain("- Form ID: 00067890 | [TestPlugin.esp] | 1", "form ID 00067890 appeared once");
    }

    /// <summary>
    ///     Validates that the <see cref="FormIdAnalyzer.AnalyzeAsync" /> method accurately ignores
    ///     malformed Form IDs in the provided crash log while correctly processing valid ones.
    /// </summary>
    /// <returns>
    ///     A task representing the asynchronous operation. The result is a <see cref="FormIdAnalysisResult" />
    ///     containing only the valid Form IDs from the crash log.
    /// </returns>
    [Fact]
    public async Task AnalyzeAsync_WithMalformedFormIds_IgnoresThem()
    {
        // Arrange
        var crashLog = new CrashLog
        {
            FilePath = "test.log",
            CallStack =
            [
                "  Form ID: 0x12345", // Too short
                "  Form ID: 0xGGGGGGGG", // Invalid hex
                "  Form ID: 0x00012345", // Valid
                "  Form ID: not a form id"
            ],
            Plugins = new Dictionary<string, string>
            {
                { "TestPlugin.esp", "00" }
            }
        };

        // Act
        var result = await _analyzer.AnalyzeAsync(crashLog);

        // Assert
        var formIdResult = (FormIdAnalysisResult)result;
        formIdResult.HasFindings.Should().BeTrue("at least one valid form ID was found");
        formIdResult.FormIds.Should()
            .ContainSingle("only one valid form ID should be recognized")
            .Which.Should().Be("Form ID: 00012345");
    }

    /// <summary>
    ///     Verifies that the <see cref="FormIdAnalyzer.AnalyzeAsync" /> method correctly processes a crash log
    ///     containing case-insensitive Form IDs, ensuring proper normalization and handling of Form ID values.
    /// </summary>
    /// <returns>
    ///     A task representing the asynchronous operation. The result is a <see cref="FormIdAnalysisResult" />
    ///     where Form IDs are normalized to a consistent format and duplicates differing only by case are treated as
    ///     equivalent.
    /// </returns>
    [Fact]
    public async Task AnalyzeAsync_WithCaseInsensitiveFormIds_HandlesCorrectly()
    {
        // Arrange
        var crashLog = new CrashLog
        {
            FilePath = "test.log",
            CallStack =
            [
                "  Form ID: 0x0001abcd",
                "  Form ID: 0x0001ABCD",
                "  form id: 0x00012345"
            ],
            Plugins = new Dictionary<string, string>
            {
                { "TestPlugin.esp", "00" }
            }
        };

        // Act
        var result = await _analyzer.AnalyzeAsync(crashLog);

        // Assert
        var formIdResult = (FormIdAnalysisResult)result;
        formIdResult.HasFindings.Should().BeTrue("form IDs were found");

        // Should normalize to uppercase and treat as same FormID
        formIdResult.FormIds.Should()
            .Contain("Form ID: 0001ABCD", "form IDs should be normalized to uppercase")
            .And.Contain("Form ID: 00012345", "lowercase form IDs should be converted to uppercase");
    }
}