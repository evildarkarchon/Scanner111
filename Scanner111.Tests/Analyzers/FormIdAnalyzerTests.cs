using Scanner111.Core.Analyzers;
using Scanner111.Core.Models;
using Xunit;

namespace Scanner111.Tests.Analyzers;

public class FormIdAnalyzerTests
{
    private readonly ClassicScanLogsInfo _config;
    private readonly FormIdAnalyzer _analyzer;

    public FormIdAnalyzerTests()
    {
        _config = new ClassicScanLogsInfo
        {
            CrashgenName = "Buffout 4"
        };
        _analyzer = new FormIdAnalyzer(_config, false, false);
    }

    [Fact]
    public async Task AnalyzeAsync_WithValidFormIds_ReturnsFormIdAnalysisResult()
    {
        // Arrange
        var crashLog = new CrashLog
        {
            FilePath = "test.log",
            CallStack = new List<string>
            {
                "  Form ID: 0x0001A332",
                "  Form ID: 0x00014E45",
                "  Form ID: 0xFF000000", // Should be skipped (starts with FF)
                "  Some other line"
            },
            Plugins = new Dictionary<string, string>
            {
                {"TestPlugin.esp", "00"},
                {"AnotherPlugin.esp", "01"}
            }
        };

        // Act
        var result = await _analyzer.AnalyzeAsync(crashLog);

        // Assert
        Assert.IsType<FormIdAnalysisResult>(result);
        var formIdResult = (FormIdAnalysisResult)result;
        
        Assert.Equal("FormID Analyzer", formIdResult.AnalyzerName);
        Assert.True(formIdResult.HasFindings);
        Assert.Equal(2, formIdResult.FormIds.Count);
        Assert.Contains("Form ID: 0001A332", formIdResult.FormIds);
        Assert.Contains("Form ID: 00014E45", formIdResult.FormIds);
        Assert.DoesNotContain("Form ID: FF000000", formIdResult.FormIds);
    }

    [Fact]
    public async Task AnalyzeAsync_WithNoFormIds_ReturnsEmptyResult()
    {
        // Arrange
        var crashLog = new CrashLog
        {
            FilePath = "test.log",
            CallStack = new List<string>
            {
                "  Some random line",
                "  Another line without FormID",
                "  Yet another line"
            },
            Plugins = new Dictionary<string, string>()
        };

        // Act
        var result = await _analyzer.AnalyzeAsync(crashLog);

        // Assert
        var formIdResult = (FormIdAnalysisResult)result;
        Assert.False(formIdResult.HasFindings);
        Assert.Empty(formIdResult.FormIds);
        Assert.Contains("* COULDN'T FIND ANY FORM ID SUSPECTS *", formIdResult.ReportText);
    }

    [Fact]
    public async Task AnalyzeAsync_WithFormIdsStartingWithFF_SkipsThose()
    {
        // Arrange
        var crashLog = new CrashLog
        {
            FilePath = "test.log",
            CallStack = new List<string>
            {
                "  Form ID: 0xFF000001",
                "  Form ID: 0xFF000002",
                "  Form ID: 0x00012345" // This should be included
            },
            Plugins = new Dictionary<string, string>
            {
                {"TestPlugin.esp", "00"}
            }
        };

        // Act
        var result = await _analyzer.AnalyzeAsync(crashLog);

        // Assert
        var formIdResult = (FormIdAnalysisResult)result;
        Assert.True(formIdResult.HasFindings);
        Assert.Single(formIdResult.FormIds);
        Assert.Contains("Form ID: 00012345", formIdResult.FormIds);
        Assert.DoesNotContain("Form ID: FF000001", formIdResult.FormIds);
        Assert.DoesNotContain("Form ID: FF000002", formIdResult.FormIds);
    }

    [Fact]
    public async Task AnalyzeAsync_WithMatchingPlugins_GeneratesCorrectReport()
    {
        // Arrange
        var crashLog = new CrashLog
        {
            FilePath = "test.log",
            CallStack = new List<string>
            {
                "  Form ID: 0x00012345",
                "  Form ID: 0x01006789"
            },
            Plugins = new Dictionary<string, string>
            {
                {"TestPlugin.esp", "00"},
                {"AnotherPlugin.esp", "01"}
            }
        };

        // Act
        var result = await _analyzer.AnalyzeAsync(crashLog);

        // Assert
        var formIdResult = (FormIdAnalysisResult)result;
        Assert.True(formIdResult.HasFindings);
        Assert.Contains("- Form ID: 00012345 | [TestPlugin.esp] | 1", formIdResult.ReportText);
        Assert.Contains("- Form ID: 01006789 | [AnotherPlugin.esp] | 1", formIdResult.ReportText);
        Assert.Contains("These Form IDs were caught by Buffout 4", formIdResult.ReportText);
    }

    [Fact]
    public async Task AnalyzeAsync_WithDuplicateFormIds_CountsCorrectly()
    {
        // Arrange
        var crashLog = new CrashLog
        {
            FilePath = "test.log",
            CallStack = new List<string>
            {
                "  Form ID: 0x00012345",
                "  Form ID: 0x00012345",
                "  Form ID: 0x00012345",
                "  Form ID: 0x00067890"
            },
            Plugins = new Dictionary<string, string>
            {
                {"TestPlugin.esp", "00"}
            }
        };

        // Act
        var result = await _analyzer.AnalyzeAsync(crashLog);

        // Assert
        var formIdResult = (FormIdAnalysisResult)result;
        Assert.True(formIdResult.HasFindings);
        Assert.Contains("- Form ID: 00012345 | [TestPlugin.esp] | 3", formIdResult.ReportText);
        Assert.Contains("- Form ID: 00067890 | [TestPlugin.esp] | 1", formIdResult.ReportText);
    }

    [Fact]
    public async Task AnalyzeAsync_WithMalformedFormIds_IgnoresThem()
    {
        // Arrange
        var crashLog = new CrashLog
        {
            FilePath = "test.log",
            CallStack = new List<string>
            {
                "  Form ID: 0x12345", // Too short
                "  Form ID: 0xGGGGGGGG", // Invalid hex
                "  Form ID: 0x00012345", // Valid
                "  Form ID: not a form id"
            },
            Plugins = new Dictionary<string, string>
            {
                {"TestPlugin.esp", "00"}
            }
        };

        // Act
        var result = await _analyzer.AnalyzeAsync(crashLog);

        // Assert
        var formIdResult = (FormIdAnalysisResult)result;
        Assert.True(formIdResult.HasFindings);
        Assert.Single(formIdResult.FormIds);
        Assert.Contains("Form ID: 00012345", formIdResult.FormIds);
    }

    [Fact]
    public async Task AnalyzeAsync_WithCaseInsensitiveFormIds_HandlesCorrectly()
    {
        // Arrange
        var crashLog = new CrashLog
        {
            FilePath = "test.log",
            CallStack = new List<string>
            {
                "  Form ID: 0x0001abcd",
                "  Form ID: 0x0001ABCD",
                "  form id: 0x00012345"
            },
            Plugins = new Dictionary<string, string>
            {
                {"TestPlugin.esp", "00"}
            }
        };

        // Act
        var result = await _analyzer.AnalyzeAsync(crashLog);

        // Assert
        var formIdResult = (FormIdAnalysisResult)result;
        Assert.True(formIdResult.HasFindings);
        
        // Should normalize to uppercase and treat as same FormID
        Assert.Contains("Form ID: 0001ABCD", formIdResult.FormIds);
        Assert.Contains("Form ID: 00012345", formIdResult.FormIds);
    }
}