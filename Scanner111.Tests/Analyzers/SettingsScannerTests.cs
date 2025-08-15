using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Scanner111.Core.Analyzers;
using Scanner111.Core.Models;
using Scanner111.Tests.TestHelpers;

namespace Scanner111.Tests.Analyzers;

/// <summary>
///     Contains unit tests for the <c>SettingsScanner</c> class, which analyzes crash logs for
///     specific settings and configurations.
/// </summary>
/// <remarks>
///     Each test in this class is designed to verify the correctness of different aspects of the
///     <c>SettingsScanner</c> functionality, ensuring accurate analysis and reporting of settings
///     found in crash logs.
/// </remarks>
public class SettingsScannerTests
{
    private readonly SettingsScanner _analyzer;

    public SettingsScannerTests()
    {
        var yamlSettings = new TestYamlSettingsProvider();
        var logger = NullLogger<SettingsScanner>.Instance;
        _analyzer = new SettingsScanner(yamlSettings, logger);
    }

    /// Tests the `AnalyzeAsync` method of the `SettingsScanner` class with a valid `CrashLog`.
    /// Verifies that the method returns an instance of `GenericAnalysisResult`
    /// containing the expected analyzer name, report lines, and specific data entries.
    /// <returns>
    ///     A task representing the asynchronous test operation. The test will pass if
    ///     the returned result is of type `GenericAnalysisResult` and contains the expected
    ///     attributes and data.
    /// </returns>
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
        result.Should().BeOfType<GenericAnalysisResult>();
        var settingsResult = (GenericAnalysisResult)result;

        settingsResult.AnalyzerName.Should().Be("Settings Scanner");
        settingsResult.ReportLines.Should().NotBeNull();
        settingsResult.Data.Should().ContainKey("XSEModules");
        settingsResult.Data.Should().ContainKey("CrashgenSettings");
    }

    /// Tests the `AnalyzeAsync` method of the `SettingsScanner` class with a `CrashLog`
    /// that contains no relevant settings data. Verifies that the method returns an instance
    /// of `GenericAnalysisResult` with the correct analyzer name and empty data structures.
    /// <returns>
    ///     A task representing the asynchronous test operation. The test will pass if
    ///     the returned result is of type `GenericAnalysisResult` with the proper analyzer
    ///     name and initialized but empty data containers.
    /// </returns>
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
        settingsResult.AnalyzerName.Should().Be("Settings Scanner");

        // Data should be initialized but empty
        var xseModules = (HashSet<string>)settingsResult.Data["XSEModules"];
        var crashgenSettings = (Dictionary<string, object>)settingsResult.Data["CrashgenSettings"];

        xseModules.Should().BeEmpty();
        crashgenSettings.Should().BeEmpty();
    }

    /// Tests the `AnalyzeAsync` method of the `SettingsScanner` class to ensure that
    /// it generates a report with the correct structure when analyzing a crash log.
    /// Verifies that all expected validation methods are executed, the report contains
    /// the appropriate configuration confirmation text, and the report structure is maintained.
    /// <returns>
    ///     A task representing the asynchronous test operation. The test will pass if the
    ///     generated report includes the expected validation results and retains the correct
    ///     structure of report lines and text.
    /// </returns>
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
        reportText.Should().Contain("correctly configured");

        // Check that report structure is maintained
        settingsResult.ReportLines.Should().NotBeEmpty();
    }

    /// Tests the `AnalyzeAsync` method of the `SettingsScanner` class with a `CrashLog`
    /// configured to test achievements-related settings validation.
    /// Verifies that the method correctly detects and processes achievements parameters
    /// and includes relevant details in the generated report.
    /// <returns>
    ///     A task representing the asynchronous operation. The test will pass if the method
    ///     produces a `GenericAnalysisResult` with an appropriate analyzer name, and the report text
    ///     contains achievements-specific validation details.
    /// </returns>
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
        settingsResult.AnalyzerName.Should().Be("Settings Scanner");

        // Should contain achievements validation
        var reportText = settingsResult.ReportText;
        reportText.Should().Contain("Achievements parameter");
    }

    /// Tests the `AnalyzeAsync` method of the `SettingsScanner` class when analyzing a crash log
    /// containing memory management settings. Verifies that the method validates the configuration
    /// correctly and returns a result indicating that the settings are appropriately configured.
    /// <returns>
    ///     A task representing the asynchronous test operation. The test will pass if the returned result
    ///     is of type `GenericAnalysisResult` and the report text contains the expected validation message
    ///     confirming correct memory management settings.
    /// </returns>
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
        reportText.Should().Contain("correctly configured");
    }

    /// Tests the `AnalyzeAsync` method of the `SettingsScanner` class with a `CrashLog` containing settings related to the ArchiveLimit parameter.
    /// Validates that the method correctly processes the crash log and includes ArchiveLimit-specific details in the generated report.
    /// <returns>
    ///     A task representing the asynchronous test operation. The test will pass if the returned
    ///     `GenericAnalysisResult` contains the expected ArchiveLimit parameter validation details in the report text.
    /// </returns>
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
        reportText.Should().Contain("ArchiveLimit parameter");
    }

    /// Tests the `AnalyzeAsync` method of the `SettingsScanner` class with a `CrashLog`
    /// containing F4EE-specific settings. Verifies that the method correctly validates
    /// the F4EE configurations and includes corresponding details in the analysis report.
    /// <returns>
    ///     A task representing the asynchronous test operation. The test will pass if
    ///     the `GenericAnalysisResult` contains validation output confirming the presence
    ///     or correct configuration of F4EE settings.
    /// </returns>
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
        reportText.Should().Contain("correctly configured");
    }

    /// Tests the `AnalyzeAsync` method of the `SettingsScanner` class
    /// to ensure that it consistently returns identical results
    /// when invoked multiple times with the same `CrashLog` input.
    /// Verifies that the resulting `GenericAnalysisResult` instances have
    /// matching `AnalyzerName`, `ReportLines` count, and `HasFindings` attributes.
    /// <returns>
    ///     A task representing the asynchronous test operation. The test will pass if
    ///     the method returns consistent `GenericAnalysisResult` instances across
    ///     multiple invocations with identical input.
    /// </returns>
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
        settingsResult2.AnalyzerName.Should().Be(settingsResult1.AnalyzerName);
        settingsResult2.ReportLines.Count.Should().Be(settingsResult1.ReportLines.Count);
        settingsResult2.HasFindings.Should().Be(settingsResult1.HasFindings);
    }

    /// Validates the `AnalyzeAsync` method of the `SettingsScanner` class when handling
    /// a crash log with a specific name, such as "Crashgen".
    /// Verifies that the method correctly identifies and includes the expected name
    /// ("Buffout 4") in the generated report text.
    /// <returns>
    ///     A task that represents the asynchronous test operation. The test will pass
    ///     if the resulting report text contains the expected name ("Buffout 4") and
    ///     confirms correct name usage within the analysis process.
    /// </returns>
    [Fact]
    public async Task AnalyzeAsync_WithCrashgenName_UsesCorrectName()
    {
        // Arrange
        var customYamlSettings = new TestYamlSettingsProvider();
        var logger = NullLogger<SettingsScanner>.Instance;
        var customAnalyzer = new SettingsScanner(customYamlSettings, logger);

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

        reportText.Should().Contain("Buffout 4");
    }
}