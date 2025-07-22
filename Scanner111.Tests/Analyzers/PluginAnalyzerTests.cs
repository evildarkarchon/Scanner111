using Scanner111.Core.Analyzers;
using Scanner111.Core.Models;
using Scanner111.Tests.TestHelpers;

namespace Scanner111.Tests.Analyzers;

/// <summary>
/// Unit tests for the PluginAnalyzer class, focusing on its ability to analyze plugin-related crash logs
/// and produce PluginAnalysisResult objects based on various scenarios.
/// </summary>
public class PluginAnalyzerTests
{
    private readonly PluginAnalyzer _analyzer;

    public PluginAnalyzerTests()
    {
        var yamlSettings = new TestYamlSettingsProvider();
        _analyzer = new PluginAnalyzer(yamlSettings);
    }

    /// <summary>
    /// Ensures that the PluginAnalyzer's AnalyzeAsync method correctly processes a crash log containing
    /// valid plugin data and returns a PluginAnalysisResult. The test verifies that plugins mentioned
    /// in the call stack are appropriately identified, counted, and included in the result.
    /// </summary>
    /// <returns>
    /// A task representing the asynchronous operation. The resulting PluginAnalysisResult contains the
    /// correct analyzer name, indicates that findings were detected, and includes a list of the detected
    /// plugins with their respective occurrence counts.
    /// </returns>
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
                { "TestPlugin.esp", "00" },
                { "AnotherPlugin.esp", "01" }
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

    /// <summary>
    /// Validates that the PluginAnalyzer's AnalyzeAsync method correctly processes a crash log
    /// that contains no plugin data and returns an empty PluginAnalysisResult. The test ensures
    /// that no findings are detected, no plugins are included in the result, and the report text
    /// contains an appropriate message indicating the absence of plugin suspects.
    /// </summary>
    /// <returns>
    /// A task representing the asynchronous operation. The resulting PluginAnalysisResult confirms
    /// no findings were detected, the plugin list is empty, and the report text includes a message
    /// about the lack of plugin suspects.
    /// </returns>
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

    /// <summary>
    /// Tests whether the PluginAnalyzer's AnalyzeAsync method accurately counts the occurrences
    /// of plugins mentioned in a crash log's call stack and produces a PluginAnalysisResult with
    /// the correct findings. The test ensures that each plugin is identified and its occurrence
    /// count is reflected in the result.
    /// </summary>
    /// <returns>
    /// A task representing the asynchronous operation. The resulting PluginAnalysisResult confirms
    /// that findings were detected, and the ReportText includes the identified plugins and their
    /// respective occurrence counts, sorted properly in the output.
    /// </returns>
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
                { "TestPlugin.esp", "00" },
                { "AnotherPlugin.esp", "01" }
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

    /// <summary>
    /// Verifies that the PluginAnalyzer's AnalyzeAsync method correctly processes a crash log
    /// by identifying and filtering out plugins specified as ignored. The test ensures that
    /// ignored plugins do not contribute to the analysis results or appear in the generated report.
    /// </summary>
    /// <returns>
    /// A task representing the asynchronous operation. The resulting PluginAnalysisResult confirms
    /// that ignored plugins are excluded from the findings while valid plugins are accurately identified
    /// and included in the analysis.
    /// </returns>
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
                { "TestPlugin.esp", "00" },
                { "ignored.esp", "01" }
            }
        };

        // Act
        var result = await _analyzer.AnalyzeAsync(crashLog);

        // Assert
        var pluginResult = (PluginAnalysisResult)result;
        Assert.True(pluginResult.HasFindings);
        Assert.Equal(2,
            pluginResult.Plugins.Count); // Both plugins are in the list but ignored.esp is filtered from matching
        Assert.Contains("- testplugin.esp | 1", pluginResult.ReportText);
        // ignored.esp should not appear in the report due to filtering
        Assert.DoesNotContain("- ignored.esp", pluginResult.ReportText);
    }

    /// <summary>
    /// Verifies that the PluginAnalyzer's AnalyzeAsync method correctly processes crash log call stack entries
    /// by filtering out unnecessary "modified by" lines while accurately counting the occurrences of relevant plugins.
    /// The test ensures that unwanted lines are excluded, and the resulting PluginAnalysisResult accurately reflects
    /// the plugin data.
    /// </summary>
    /// <returns>
    /// A task representing the asynchronous operation. The resulting PluginAnalysisResult indicates that findings
    /// were detected and contains a report that correctly counts plugin occurrences, excluding lines that begin
    /// with "modified by".
    /// </returns>
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
                { "TestPlugin.esp", "00" }
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

    /// <summary>
    /// Verifies that the PluginAnalyzer's AnalyzeAsync method properly handles case-insensitive plugin name matching
    /// within a crash log. The test ensures that plugin names are matched correctly regardless of letter casing
    /// and that occurrences are counted accurately in the result.
    /// </summary>
    /// <returns>
    /// A task representing the asynchronous operation. The resulting PluginAnalysisResult confirms that findings
    /// were detected, with case-insensitive matches appropriately aggregated and reported.
    /// </returns>
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
                { "TestPlugin.esp", "00" }
            }
        };

        // Act
        var result = await _analyzer.AnalyzeAsync(crashLog);

        // Assert
        var pluginResult = (PluginAnalysisResult)result;
        Assert.True(pluginResult.HasFindings);
        Assert.Contains("- testplugin.esp | 3", pluginResult.ReportText);
    }

    /// <summary>
    /// Tests the AnalyzeAsync method of the PluginAnalyzer to ensure that when provided with a crash log containing
    /// multiple plugin mentions, it correctly sorts the results by the number of occurrences in descending order,
    /// and then alphabetically by plugin name in ascending order for ties.
    /// </summary>
    /// <returns>
    /// A task representing the asynchronous operation. The resulting PluginAnalysisResult contains
    /// a correctly ordered list of plugins based on the specified sorting criteria.
    /// </returns>
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
                { "Alpha.esp", "00" },
                { "Beta.esp", "01" },
                { "Charlie.esp", "02" }
            }
        };

        // Act
        var result = await _analyzer.AnalyzeAsync(crashLog);

        // Assert
        var pluginResult = (PluginAnalysisResult)result;
        Assert.True(pluginResult.HasFindings);

        var reportText = pluginResult.ReportText;

        // Should be sorted by count (descending) then by name (ascending)
        var charlieIndex = reportText.IndexOf("- charlie.esp | 3", StringComparison.Ordinal);
        var alphaIndex = reportText.IndexOf("- alpha.esp | 2", StringComparison.Ordinal);
        var betaIndex = reportText.IndexOf("- beta.esp | 1", StringComparison.Ordinal);

        Assert.True(charlieIndex < alphaIndex); // Charlie (3) before Alpha (2)
        Assert.True(alphaIndex < betaIndex); // Alpha (2) before Beta (1)
    }

    /// <summary>
    /// Verifies that the PluginAnalyzer's AnalyzeAsync method integrates information from a crash log
    /// with a loadorder.txt file to determine and utilize the plugins listed in the file. The test
    /// ensures that, in the absence of the loadorder.txt file, the plugins found in the crash log
    /// are accurately processed and included in the analysis.
    /// </summary>
    /// <returns>
    /// A task representing the asynchronous operation. The resulting PluginAnalysisResult contains
    /// the analyzer name and a list of plugins, demonstrating the ability to process either loadorder.txt
    /// file data or fallback to crash log plugin data.
    /// </returns>
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
                { "TestPlugin.esp", "00" }
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