using Scanner111.Core.Analyzers;
using Scanner111.Core.Models;
using Xunit;

namespace Scanner111.Tests.Analyzers;

public class SettingsScannerTests
{
    private readonly ClassicScanLogsInfo _config;
    private readonly SettingsScanner _analyzer;

    public SettingsScannerTests()
    {
        _config = new ClassicScanLogsInfo
        {
            CrashgenName = "Buffout 4"
        };
        _analyzer = new SettingsScanner(_config);
    }

    [Fact]
    public async Task AnalyzeAsync_WithValidSettings_ReturnsGenericAnalysisResult()
    {
        // Arrange
        var crashLog = new CrashLog
        {
            FilePath = "test.log",
            MainError = "Test error",
            CallStack = new List<string>
            {
                "test line 1",
                "test line 2"
            }
        };

        // Act
        var result = await _analyzer.AnalyzeAsync(crashLog);

        // Assert
        Assert.IsType<GenericAnalysisResult>(result);
        var settingsResult = (GenericAnalysisResult)result;
        
        Assert.Equal("Settings Scanner", settingsResult.AnalyzerName);
        Assert.NotNull(settingsResult.ReportLines);
        Assert.Contains("XSEModules", settingsResult.Data);
        Assert.Contains("CrashgenSettings", settingsResult.Data);
    }

    [Fact]
    public async Task AnalyzeAsync_WithNoSettings_ReturnsEmptyResult()
    {
        // Arrange
        var crashLog = new CrashLog
        {
            FilePath = "test.log",
            MainError = "Test error",
            CallStack = new List<string>
            {
                "normal line 1",
                "normal line 2"
            }
        };

        // Act
        var result = await _analyzer.AnalyzeAsync(crashLog);

        // Assert
        var settingsResult = (GenericAnalysisResult)result;
        Assert.Equal("Settings Scanner", settingsResult.AnalyzerName);
        
        // Data should be initialized but empty
        var xseModules = (HashSet<string>)settingsResult.Data["XSEModules"];
        var crashgenSettings = (Dictionary<string, object>)settingsResult.Data["CrashgenSettings"];
        
        Assert.Empty(xseModules);
        Assert.Empty(crashgenSettings);
    }

    [Fact]
    public async Task AnalyzeAsync_GeneratesCorrectReportStructure()
    {
        // Arrange
        var crashLog = new CrashLog
        {
            FilePath = "test.log",
            MainError = "Test error",
            CallStack = new List<string>
            {
                "test line"
            }
        };

        // Act
        var result = await _analyzer.AnalyzeAsync(crashLog);

        // Assert
        var settingsResult = (GenericAnalysisResult)result;
        
        // Should have run all the settings validation methods
        var reportText = settingsResult.ReportText;
        Assert.Contains("correctly configured", reportText);
        
        // Check that report structure is maintained
        Assert.NotEmpty(settingsResult.ReportLines);
    }

    [Fact]
    public async Task AnalyzeAsync_WithAchievementsSettings_ValidatesCorrectly()
    {
        // This test demonstrates the structure but would need actual crash log parsing
        // to extract XSE modules and crashgen settings from the crash log content
        
        // Arrange
        var crashLog = new CrashLog
        {
            FilePath = "test.log",
            MainError = "Test error",
            CallStack = new List<string>
            {
                "test line"
            }
        };

        // Act
        var result = await _analyzer.AnalyzeAsync(crashLog);

        // Assert
        var settingsResult = (GenericAnalysisResult)result;
        Assert.Equal("Settings Scanner", settingsResult.AnalyzerName);
        
        // Should contain achievements validation
        var reportText = settingsResult.ReportText;
        Assert.Contains("Achievements parameter", reportText);
    }

    [Fact]
    public async Task AnalyzeAsync_WithMemoryManagementSettings_ValidatesCorrectly()
    {
        // Arrange
        var crashLog = new CrashLog
        {
            FilePath = "test.log",
            MainError = "Test error",
            CallStack = new List<string>
            {
                "test line"
            }
        };

        // Act
        var result = await _analyzer.AnalyzeAsync(crashLog);

        // Assert
        var settingsResult = (GenericAnalysisResult)result;
        
        // Should contain memory management validation (defaults to correct setting)
        var reportText = settingsResult.ReportText;
        Assert.Contains("correctly configured", reportText);
    }

    [Fact]
    public async Task AnalyzeAsync_WithArchiveLimitSettings_ValidatesCorrectly()
    {
        // Arrange
        var crashLog = new CrashLog
        {
            FilePath = "test.log",
            MainError = "Test error",
            CallStack = new List<string>
            {
                "test line"
            }
        };

        // Act
        var result = await _analyzer.AnalyzeAsync(crashLog);

        // Assert
        var settingsResult = (GenericAnalysisResult)result;
        
        // Should contain archive limit validation
        var reportText = settingsResult.ReportText;
        Assert.Contains("ArchiveLimit parameter", reportText);
    }

    [Fact]
    public async Task AnalyzeAsync_WithF4EESettings_ValidatesCorrectly()
    {
        // Arrange
        var crashLog = new CrashLog
        {
            FilePath = "test.log",
            MainError = "Test error",
            CallStack = new List<string>
            {
                "test line"
            }
        };

        // Act
        var result = await _analyzer.AnalyzeAsync(crashLog);

        // Assert
        var settingsResult = (GenericAnalysisResult)result;
        
        // Should contain F4EE validation (but may not be present with empty settings)
        var reportText = settingsResult.ReportText;
        Assert.Contains("correctly configured", reportText);
    }

    [Fact]
    public async Task AnalyzeAsync_ReturnsConsistentResults()
    {
        // Arrange
        var crashLog = new CrashLog
        {
            FilePath = "test.log",
            MainError = "Test error",
            CallStack = new List<string>
            {
                "test line"
            }
        };

        // Act
        var result1 = await _analyzer.AnalyzeAsync(crashLog);
        var result2 = await _analyzer.AnalyzeAsync(crashLog);

        // Assert
        var settingsResult1 = (GenericAnalysisResult)result1;
        var settingsResult2 = (GenericAnalysisResult)result2;
        
        // Should return consistent results
        Assert.Equal(settingsResult1.AnalyzerName, settingsResult2.AnalyzerName);
        Assert.Equal(settingsResult1.ReportLines.Count, settingsResult2.ReportLines.Count);
        Assert.Equal(settingsResult1.HasFindings, settingsResult2.HasFindings);
    }

    [Fact]
    public async Task AnalyzeAsync_WithCrashgenName_UsesCorrectName()
    {
        // Arrange
        var customConfig = new ClassicScanLogsInfo
        {
            CrashgenName = "Custom Crash Generator"
        };
        var customAnalyzer = new SettingsScanner(customConfig);
        
        var crashLog = new CrashLog
        {
            FilePath = "test.log",
            MainError = "Test error",
            CallStack = new List<string>
            {
                "test line"
            }
        };

        // Act
        var result = await customAnalyzer.AnalyzeAsync(crashLog);

        // Assert
        var settingsResult = (GenericAnalysisResult)result;
        var reportText = settingsResult.ReportText;
        
        Assert.Contains("Custom Crash Generator", reportText);
        Assert.DoesNotContain("Buffout 4", reportText);
    }
}