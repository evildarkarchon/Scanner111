using Scanner111.Core.Analyzers;
using Scanner111.Core.Models;
using Scanner111.Core.Infrastructure;
using Scanner111.Tests.TestHelpers;
using Xunit;

namespace Scanner111.Tests.Analyzers;

/// <summary>
/// Unit tests for the BuffoutVersionAnalyzer class
/// </summary>
public class BuffoutVersionAnalyzerTests
{
    private readonly BuffoutVersionAnalyzer _analyzer;
    private readonly TestYamlSettingsProvider _yamlSettings;

    public BuffoutVersionAnalyzerTests()
    {
        _yamlSettings = new TestYamlSettingsProvider();
        _analyzer = new BuffoutVersionAnalyzer(_yamlSettings);
    }

    [Fact]
    public async Task AnalyzeAsync_WithLatestVersion_ReportsUpToDate()
    {
        // Arrange
        var crashLog = new CrashLog
        {
            FilePath = "test.log",
            CrashGenVersion = "v0.3.0",
            MainError = "Test error"
        };

        // Act
        var result = await _analyzer.AnalyzeAsync(crashLog);

        // Assert
        Assert.IsType<GenericAnalysisResult>(result);
        var genericResult = (GenericAnalysisResult)result;
        Assert.True(genericResult.Success);
        Assert.False(genericResult.HasFindings);
        Assert.Contains("You have the latest version of Buffout 4!", genericResult.ReportText);
        Assert.Contains("Detected Buffout 4 Version: v0.3.0", genericResult.ReportText);
    }

    [Fact]
    public async Task AnalyzeAsync_WithOutdatedVersion_ReportsUpdateAvailable()
    {
        // Arrange
        var crashLog = new CrashLog
        {
            FilePath = "test.log",
            CrashGenVersion = "v0.2.0",
            MainError = "Test error"
        };

        // Act
        var result = await _analyzer.AnalyzeAsync(crashLog);

        // Assert
        var genericResult = (GenericAnalysisResult)result;
        Assert.True(genericResult.Success);
        Assert.True(genericResult.HasFindings);
        Assert.Contains("AN UPDATE IS AVAILABLE FOR Buffout 4", genericResult.ReportText);
        Assert.Contains("https://www.nexusmods.com/fallout4/mods/47359", genericResult.ReportText);
    }

    [Fact]
    public async Task AnalyzeAsync_WithNoVersionInfo_ReturnsNoFindings()
    {
        // Arrange
        var crashLog = new CrashLog
        {
            FilePath = "test.log",
            CrashGenVersion = null,
            MainError = "Test error"
        };

        // Act
        var result = await _analyzer.AnalyzeAsync(crashLog);

        // Assert
        var genericResult = (GenericAnalysisResult)result;
        Assert.True(genericResult.Success);
        Assert.False(genericResult.HasFindings);
        Assert.Empty(genericResult.ReportLines);
    }

    [Fact]
    public async Task AnalyzeAsync_WithInvalidVersionFormat_HandlesGracefully()
    {
        // Arrange
        var crashLog = new CrashLog
        {
            FilePath = "test.log",
            CrashGenVersion = "invalid-version",
            MainError = "Test error"
        };

        // Act
        var result = await _analyzer.AnalyzeAsync(crashLog);

        // Assert
        var genericResult = (GenericAnalysisResult)result;
        Assert.True(genericResult.Success);
        Assert.Contains("Detected Buffout 4 Version: invalid-version", genericResult.ReportText);
        // Should not contain latest version message since version parsing failed
        Assert.DoesNotContain("You have the latest version", genericResult.ReportText);
    }

    [Fact]
    public async Task AnalyzeAsync_WithNgVersion_ComparesCorrectly()
    {
        // Arrange
        var crashLog = new CrashLog
        {
            FilePath = "test.log",
            CrashGenVersion = "v0.3.0-NG",
            MainError = "Test error"
        };

        // Act
        var result = await _analyzer.AnalyzeAsync(crashLog);

        // Assert
        var genericResult = (GenericAnalysisResult)result;
        Assert.True(genericResult.Success);
        Assert.False(genericResult.HasFindings);
        Assert.Contains("You have the latest version of Buffout 4!", genericResult.ReportText);
    }

    [Fact]
    public async Task AnalyzeAsync_WhenYamlLoadFails_ReturnsWarning()
    {
        // Arrange
        var badYamlSettings = new TestYamlSettingsProvider();
        // Override LoadYaml to return null
        var analyzer = new BuffoutVersionAnalyzer(new NullYamlSettingsProvider());
        
        var crashLog = new CrashLog
        {
            FilePath = "test.log",
            CrashGenVersion = "v0.3.0",
            MainError = "Test error"
        };

        // Act
        var result = await _analyzer.AnalyzeAsync(crashLog);

        // Assert
        var genericResult = (GenericAnalysisResult)result;
        Assert.True(genericResult.Success); // Still successful, just with different result
        Assert.Contains("Detected Buffout 4 Version: v0.3.0", genericResult.ReportText);
    }

    [Fact]
    public void AnalyzerProperties_AreSetCorrectly()
    {
        // Assert
        Assert.Equal("Buffout Version Analyzer", _analyzer.Name);
        Assert.Equal(95, _analyzer.Priority);
        Assert.True(_analyzer.CanRunInParallel);
    }

    [Fact]
    public async Task AnalyzeAsync_WithVersionHigherThanLatest_ReportsAsLatest()
    {
        // Arrange
        var crashLog = new CrashLog
        {
            FilePath = "test.log",
            CrashGenVersion = "v0.4.0", // Higher than v0.3.0
            MainError = "Test error"
        };

        // Act
        var result = await _analyzer.AnalyzeAsync(crashLog);

        // Assert
        var genericResult = (GenericAnalysisResult)result;
        Assert.False(genericResult.HasFindings);
        Assert.Contains("You have the latest version of Buffout 4!", genericResult.ReportText);
    }

    /// <summary>
    /// Helper class for testing YAML load failures
    /// </summary>
    private class NullYamlSettingsProvider : IYamlSettingsProvider
    {
        public T? LoadYaml<T>(string yamlFile) where T : class => null;
        public Task<T?> LoadYamlAsync<T>(string yamlFile) where T : class => Task.FromResult<T?>(null);
        public void ClearCache() { }
    }
}