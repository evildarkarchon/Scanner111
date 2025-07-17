using Scanner111.Core.Analyzers;
using Scanner111.Core.Models;
using Xunit;

namespace Scanner111.Tests.Analyzers;

public class PluginAnalyzerTests
{
    private readonly ClassicScanLogsInfo _config;
    private readonly PluginAnalyzer _analyzer;

    public PluginAnalyzerTests()
    {
        _config = new ClassicScanLogsInfo
        {
            CrashgenName = "Buffout 4",
            IgnorePluginsList = new List<string> { "ignored.esp" }
        };
        _analyzer = new PluginAnalyzer(_config);
    }

    [Fact]
    public async Task AnalyzeAsync_WithValidPlugins_ReturnsPluginAnalysisResult()
    {
        // Arrange
        var crashLog = new CrashLog
        {
            FilePath = "test.log",
            CallStack = new List<string>
            {
                "some line with testplugin.esp mentioned",
                "another line with anotherplugin.esp",
                "testplugin.esp appears again",
                "unrelated line"
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
        Assert.IsType<PluginAnalysisResult>(result);
        var pluginResult = (PluginAnalysisResult)result;
        
        Assert.Equal("Plugin Analyzer", pluginResult.AnalyzerName);
        Assert.True(pluginResult.HasFindings);
        Assert.Equal(2, pluginResult.Plugins.Count);
    }

    [Fact]
    public async Task AnalyzeAsync_WithNoPlugins_ReturnsEmptyResult()
    {
        // Arrange
        var crashLog = new CrashLog
        {
            FilePath = "test.log",
            CallStack = new List<string>
            {
                "some random line",
                "another line without plugins",
                "yet another line"
            },
            Plugins = new Dictionary<string, string>()
        };

        // Act
        var result = await _analyzer.AnalyzeAsync(crashLog);

        // Assert
        var pluginResult = (PluginAnalysisResult)result;
        Assert.False(pluginResult.HasFindings);
        Assert.Empty(pluginResult.Plugins);
        Assert.Contains("* COULDN'T FIND ANY PLUGIN SUSPECTS *", pluginResult.ReportText);
    }

    [Fact]
    public async Task AnalyzeAsync_WithPluginMatches_CountsCorrectly()
    {
        // Arrange
        var crashLog = new CrashLog
        {
            FilePath = "test.log",
            CallStack = new List<string>
            {
                "line with testplugin.esp mentioned",
                "another line with testplugin.esp",
                "testplugin.esp appears third time",
                "line with anotherplugin.esp mentioned once"
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
        var pluginResult = (PluginAnalysisResult)result;
        Assert.True(pluginResult.HasFindings);
        Assert.Contains("- testplugin.esp | 3", pluginResult.ReportText);
        Assert.Contains("- anotherplugin.esp | 1", pluginResult.ReportText);
        Assert.Contains("These Plugins were caught by Buffout 4", pluginResult.ReportText);
    }

    [Fact]
    public async Task AnalyzeAsync_WithIgnoredPlugins_FiltersThemOut()
    {
        // Arrange
        var crashLog = new CrashLog
        {
            FilePath = "test.log",
            CallStack = new List<string>
            {
                "line with testplugin.esp mentioned",
                "line with ignored.esp mentioned",
                "another line with ignored.esp"
            },
            Plugins = new Dictionary<string, string>
            {
                {"TestPlugin.esp", "00"},
                {"ignored.esp", "01"}
            }
        };

        // Act
        var result = await _analyzer.AnalyzeAsync(crashLog);

        // Assert
        var pluginResult = (PluginAnalysisResult)result;
        Assert.True(pluginResult.HasFindings);
        Assert.Equal(2, pluginResult.Plugins.Count); // Both plugins are in the list but ignored.esp is filtered from matching
        Assert.Contains("- testplugin.esp | 1", pluginResult.ReportText);
        // ignored.esp should not appear in the report due to filtering
        Assert.DoesNotContain("- ignored.esp", pluginResult.ReportText);
    }

    [Fact]
    public async Task AnalyzeAsync_WithModifiedByLines_FiltersThemOut()
    {
        // Arrange
        var crashLog = new CrashLog
        {
            FilePath = "test.log",
            CallStack = new List<string>
            {
                "line with testplugin.esp mentioned",
                "modified by: testplugin.esp - this should be filtered",
                "another line with testplugin.esp"
            },
            Plugins = new Dictionary<string, string>
            {
                {"TestPlugin.esp", "00"}
            }
        };

        // Act
        var result = await _analyzer.AnalyzeAsync(crashLog);

        // Assert
        var pluginResult = (PluginAnalysisResult)result;
        Assert.True(pluginResult.HasFindings);
        // Should only count 2 times (filtered out the "modified by:" line)
        Assert.Contains("- testplugin.esp | 2", pluginResult.ReportText);
    }

    [Fact]
    public async Task AnalyzeAsync_WithCaseInsensitiveMatching_WorksCorrectly()
    {
        // Arrange
        var crashLog = new CrashLog
        {
            FilePath = "test.log",
            CallStack = new List<string>
            {
                "line with TESTPLUGIN.ESP mentioned",
                "line with testplugin.esp mentioned",
                "line with TestPlugin.esp mentioned"
            },
            Plugins = new Dictionary<string, string>
            {
                {"TestPlugin.esp", "00"}
            }
        };

        // Act
        var result = await _analyzer.AnalyzeAsync(crashLog);

        // Assert
        var pluginResult = (PluginAnalysisResult)result;
        Assert.True(pluginResult.HasFindings);
        Assert.Contains("- testplugin.esp | 3", pluginResult.ReportText);
    }

    [Fact]
    public async Task AnalyzeAsync_WithMultiplePlugins_SortsByCountThenName()
    {
        // Arrange
        var crashLog = new CrashLog
        {
            FilePath = "test.log",
            CallStack = new List<string>
            {
                "line with beta.esp mentioned",
                "line with alpha.esp mentioned",
                "line with alpha.esp mentioned again",
                "line with charlie.esp mentioned",
                "line with charlie.esp mentioned again",
                "line with charlie.esp mentioned third time"
            },
            Plugins = new Dictionary<string, string>
            {
                {"Alpha.esp", "00"},
                {"Beta.esp", "01"},
                {"Charlie.esp", "02"}
            }
        };

        // Act
        var result = await _analyzer.AnalyzeAsync(crashLog);

        // Assert
        var pluginResult = (PluginAnalysisResult)result;
        Assert.True(pluginResult.HasFindings);
        
        var reportText = pluginResult.ReportText;
        
        // Should be sorted by count (descending) then by name (ascending)
        var charlieIndex = reportText.IndexOf("- charlie.esp | 3");
        var alphaIndex = reportText.IndexOf("- alpha.esp | 2");
        var betaIndex = reportText.IndexOf("- beta.esp | 1");
        
        Assert.True(charlieIndex < alphaIndex); // Charlie (3) before Alpha (2)
        Assert.True(alphaIndex < betaIndex);    // Alpha (2) before Beta (1)
    }

    [Fact]
    public async Task AnalyzeAsync_WithLoadorderTxtFile_UsesLoadorderPlugins()
    {
        // Note: This test would need to be enhanced to actually create/mock a loadorder.txt file
        // For now, we'll test the basic functionality without the file
        
        // Arrange
        var crashLog = new CrashLog
        {
            FilePath = "test.log",
            CallStack = new List<string>
            {
                "line with testplugin.esp mentioned"
            },
            Plugins = new Dictionary<string, string>
            {
                {"TestPlugin.esp", "00"}
            }
        };

        // Act
        var result = await _analyzer.AnalyzeAsync(crashLog);

        // Assert
        var pluginResult = (PluginAnalysisResult)result;
        Assert.Equal("Plugin Analyzer", pluginResult.AnalyzerName);
        // Without an actual loadorder.txt file, it should use crash log plugins
        Assert.Single(pluginResult.Plugins);
    }
}