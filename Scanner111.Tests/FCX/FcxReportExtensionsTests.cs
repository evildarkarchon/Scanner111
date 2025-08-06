using System.Collections.Generic;
using System.Linq;
using Scanner111.Core.Analyzers;
using Scanner111.Core.FCX;
using Scanner111.Core.Models;
using Xunit;
using FluentAssertions;

namespace Scanner111.Tests.FCX;

public class FcxReportExtensionsTests
{
    [Fact]
    public void AddFcxReportSections_DoesNotAddSection_WhenNoFcxFindings()
    {
        // Arrange
        var report = new List<string> { "Initial content" };
        var scanResult = new ScanResult
        {
            LogPath = "test.log",
            AnalysisResults = new List<AnalysisResult>
            {
                new GenericAnalysisResult
                {
                    AnalyzerName = "Some Analyzer",
                    HasFindings = false
                }
            }
        };
        
        // Act
        report.AddFcxReportSections(scanResult);
        
        // Assert
        report.Should().ContainSingle("report should not be modified when no FCX findings exist");
        report[0].Should().Be("Initial content", "original content should remain unchanged");
    }

    [Fact]
    public void AddFcxReportSections_AddsSection_WhenFcxAnalyzerHasFindings()
    {
        // Arrange
        var report = new List<string>();
        var scanResult = new ScanResult
        {
            LogPath = "test.log",
            AnalysisResults = new List<AnalysisResult>
            {
                new GenericAnalysisResult
                {
                    AnalyzerName = "FCX Analyzer",
                    HasFindings = true,
                    ReportLines = new List<string> { "FCX finding 1", "FCX finding 2" }
                }
            }
        };
        
        // Act
        report.AddFcxReportSections(scanResult);
        
        // Assert
        report.Should().Contain("FCX MODE - ADVANCED FILE INTEGRITY CHECKING\n", "FCX header should be added");
        report.Should().Contain("# FILE INTEGRITY CHECK RESULTS #\n", "FCX section header should be present");
        report.Should().Contain("FCX finding 1", "first finding should be included");
        report.Should().Contain("FCX finding 2", "second finding should be included");
    }

    [Fact]
    public void AddFcxReportSections_AddsSection_WhenModConflictAnalyzerHasFindings()
    {
        // Arrange
        var report = new List<string>();
        var scanResult = new ScanResult
        {
            LogPath = "test.log",
            AnalysisResults = new List<AnalysisResult>
            {
                new GenericAnalysisResult
                {
                    AnalyzerName = "Mod Conflict Analyzer",
                    HasFindings = true,
                    ReportLines = new List<string> { "Conflict 1", "Conflict 2" }
                }
            }
        };
        
        // Act
        report.AddFcxReportSections(scanResult);
        
        // Assert
        report.Should().Contain("FCX MODE - ADVANCED FILE INTEGRITY CHECKING\n", "FCX header should be added");
        report.Should().Contain("# MOD CONFLICT ANALYSIS #\n", "mod conflict section header should be present");
        report.Should().Contain("Conflict 1", "first conflict should be included");
        report.Should().Contain("Conflict 2", "second conflict should be included");
    }

    [Fact]
    public void AddFcxReportSections_AddsSection_WhenVersionAnalyzerHasFindings()
    {
        // Arrange
        var report = new List<string>();
        var scanResult = new ScanResult
        {
            LogPath = "test.log",
            AnalysisResults = new List<AnalysisResult>
            {
                new GenericAnalysisResult
                {
                    AnalyzerName = "Version Analyzer",
                    HasFindings = true,
                    ReportLines = new List<string> { "Version info 1", "Version info 2" }
                }
            }
        };
        
        // Act
        report.AddFcxReportSections(scanResult);
        
        // Assert
        report.Should().Contain("FCX MODE - ADVANCED FILE INTEGRITY CHECKING\n", "FCX header should be added");
        report.Should().Contain("# GAME VERSION INFORMATION #\n", "version section header should be present");
        report.Should().Contain("Version info 1", "first version info should be included");
        report.Should().Contain("Version info 2", "second version info should be included");
    }

    [Fact]
    public void AddFcxReportSections_AddsAllSections_WhenMultipleAnalyzersHaveFindings()
    {
        // Arrange
        var report = new List<string>();
        var scanResult = new ScanResult
        {
            LogPath = "test.log",
            AnalysisResults = new List<AnalysisResult>
            {
                new GenericAnalysisResult
                {
                    AnalyzerName = "FCX Analyzer",
                    HasFindings = true,
                    ReportLines = new List<string> { "FCX finding" }
                },
                new GenericAnalysisResult
                {
                    AnalyzerName = "Mod Conflict Analyzer",
                    HasFindings = true,
                    ReportLines = new List<string> { "Conflict finding" }
                },
                new GenericAnalysisResult
                {
                    AnalyzerName = "Version Analyzer",
                    HasFindings = true,
                    ReportLines = new List<string> { "Version finding" }
                }
            }
        };
        
        // Act
        report.AddFcxReportSections(scanResult);
        
        // Assert
        report.Should().Contain("FCX MODE - ADVANCED FILE INTEGRITY CHECKING\n", "FCX header should be added");
        report.Should().Contain("# GAME VERSION INFORMATION #\n", "version section should be present");
        report.Should().Contain("# FILE INTEGRITY CHECK RESULTS #\n", "FCX section should be present");
        report.Should().Contain("# MOD CONFLICT ANALYSIS #\n", "conflict section should be present");
        report.Should().Contain("Version finding", "version finding should be included");
        report.Should().Contain("FCX finding", "FCX finding should be included");
        report.Should().Contain("Conflict finding", "conflict finding should be included");
    }

    [Fact]
    public void AddFcxReportSections_MaintainsOrder_VersionThenFcxThenModConflict()
    {
        // Arrange
        var report = new List<string>();
        var scanResult = new ScanResult
        {
            LogPath = "test.log",
            AnalysisResults = new List<AnalysisResult>
            {
                new GenericAnalysisResult
                {
                    AnalyzerName = "Mod Conflict Analyzer",
                    HasFindings = true,
                    ReportLines = new List<string> { "Conflict" }
                },
                new GenericAnalysisResult
                {
                    AnalyzerName = "FCX Analyzer",
                    HasFindings = true,
                    ReportLines = new List<string> { "FCX" }
                },
                new GenericAnalysisResult
                {
                    AnalyzerName = "Version Analyzer",
                    HasFindings = true,
                    ReportLines = new List<string> { "Version" }
                }
            }
        };
        
        // Act
        report.AddFcxReportSections(scanResult);
        
        // Assert
        var versionIndex = report.FindIndex(s => s.Contains("# GAME VERSION INFORMATION #\n"));
        var fcxIndex = report.FindIndex(s => s.Contains("# FILE INTEGRITY CHECK RESULTS #\n"));
        var conflictIndex = report.FindIndex(s => s.Contains("# MOD CONFLICT ANALYSIS #\n"));
        
        versionIndex.Should().BeLessThan(fcxIndex, "version section should come before FCX section");
        fcxIndex.Should().BeLessThan(conflictIndex, "FCX section should come before conflict section");
    }

    [Fact]
    public void AddFcxReportSections_HandlesNullAnalysisResults()
    {
        // Arrange
        var report = new List<string>();
        var scanResult = new ScanResult
        {
            LogPath = "test.log",
            AnalysisResults = null
        };
        
        // Act & Assert - Should throw NullReferenceException since the code doesn't check for null
        var action = () => report.AddFcxReportSections(scanResult);
        action.Should().Throw<NullReferenceException>("code doesn't check for null analysis results");
    }

    [Fact]
    public void AddFcxReportSections_HandlesEmptyAnalysisResults()
    {
        // Arrange
        var report = new List<string>();
        var scanResult = new ScanResult
        {
            LogPath = "test.log",
            AnalysisResults = new List<AnalysisResult>()
        };
        
        // Act
        report.AddFcxReportSections(scanResult);
        
        // Assert
        report.Should().BeEmpty("no sections should be added when there are no analysis results");
    }

    [Fact]
    public void AddFcxReportSections_HandlesNullReportLines()
    {
        // Arrange
        var report = new List<string>();
        var scanResult = new ScanResult
        {
            LogPath = "test.log",
            AnalysisResults = new List<AnalysisResult>
            {
                new GenericAnalysisResult
                {
                    AnalyzerName = "FCX Analyzer",
                    HasFindings = true,
                    ReportLines = null
                }
            }
        };
        
        // Act & Assert - Should not throw
        var action = () => report.AddFcxReportSections(scanResult);
        action.Should().NotThrow("null report lines should be handled gracefully");
        report.Should().Contain("FCX MODE - ADVANCED FILE INTEGRITY CHECKING\n", "FCX header should still be added");
    }

    [Fact]
    public void AddFcxReportSections_OnlyAddsRelevantSections()
    {
        // Arrange
        var report = new List<string>();
        var scanResult = new ScanResult
        {
            LogPath = "test.log",
            AnalysisResults = new List<AnalysisResult>
            {
                new GenericAnalysisResult
                {
                    AnalyzerName = "FCX Analyzer",
                    HasFindings = false,
                    ReportLines = new List<string> { "Should not appear" }
                },
                new GenericAnalysisResult
                {
                    AnalyzerName = "Version Analyzer",
                    HasFindings = true,
                    ReportLines = new List<string> { "Version info" }
                }
            }
        };
        
        // Act
        report.AddFcxReportSections(scanResult);
        
        // Assert
        report.Should().Contain("Version info", "version info from analyzer with findings should be included");
        report.Should().NotContain("Should not appear", "content from analyzer without findings should not be included");
        report.Should().NotContain("# FILE INTEGRITY CHECK RESULTS #\n", "FCX section should not be added when FCX analyzer has no findings");
    }

    [Fact]
    public void GenerateFcxSectionForSettings_WhenEnabled_ReturnsEnabledMessage()
    {
        // Act
        var result = FcxReportExtensions.GenerateFcxSectionForSettings(true);
        
        // Assert
        result.Should().Contain("FCX MODE IS ENABLED", "enabled status should be shown");
        result.Should().Contain("PERFORMING ADVANCED FILE INTEGRITY CHECKS", "active status should be indicated");
        result.Should().Contain("FCX Mode checks game file integrity and detects mod conflicts", "description should be included");
    }

    [Fact]
    public void GenerateFcxSectionForSettings_WhenDisabled_ReturnsDisabledMessage()
    {
        // Act
        var result = FcxReportExtensions.GenerateFcxSectionForSettings(false);
        
        // Assert
        result.Should().Contain("FCX MODE IS DISABLED", "disabled status should be shown");
        result.Should().Contain("YOU CAN ENABLE IT TO DETECT PROBLEMS", "suggestion to enable should be included");
        result.Should().Contain("FCX Mode can be enabled in the Scanner 111 application settings", "instructions should be provided");
    }

    [Fact]
    public void AddFcxReportSections_AddsSummary_WhenFcxAnalyzerHasData()
    {
        // Arrange
        var report = new List<string>();
        var fcxResult = new GenericAnalysisResult
        {
            AnalyzerName = "FCX Analyzer",
            HasFindings = true,
            ReportLines = new List<string> { "FCX details" },
            Data = new Dictionary<string, object>
            {
                { "ModifiedFilesCount", 5 },
                { "MissingFilesCount", 3 }
            }
        };
        
        var scanResult = new ScanResult
        {
            LogPath = "test.log",
            AnalysisResults = new List<AnalysisResult> { fcxResult }
        };
        
        // Act
        report.AddFcxReportSections(scanResult);
        
        // Assert
        report.Should().Contain("# FCX SUMMARY #\n", "FCX summary section should be added");
        report.Should().Contain("  • 5 modified game files detected\n", "modified files count should be shown");
        report.Should().Contain("  • 3 missing game files detected\n", "missing files count should be shown");
        report.Should().Contain("* For detailed FCX documentation and solutions, see: https://github.com/evildarkarchon/Scanner111/wiki/FCX-Mode *\n", "wiki link should be included");
    }

    [Fact]
    public void AddFcxReportSections_AddsSummary_WhenModConflictAnalyzerHasData()
    {
        // Arrange
        var report = new List<string>();
        var conflictResult = new GenericAnalysisResult
        {
            AnalyzerName = "Mod Conflict Analyzer",
            HasFindings = true,
            ReportLines = new List<string> { "Conflict details" },
            Data = new Dictionary<string, object>
            {
                { "TotalIssues", 7 }
            }
        };
        
        var scanResult = new ScanResult
        {
            LogPath = "test.log",
            AnalysisResults = new List<AnalysisResult> { conflictResult }
        };
        
        // Act
        report.AddFcxReportSections(scanResult);
        
        // Assert
        report.Should().Contain("# FCX SUMMARY #\n", "FCX summary section should be added");
        report.Should().Contain("  • 7 mod conflicts detected\n", "mod conflicts count should be shown");
    }

    [Fact]
    public void AddFcxReportSections_AddsSummary_WhenVersionAnalyzerDetectsDowngrade()
    {
        // Arrange
        var report = new List<string>();
        var versionResult = new GenericAnalysisResult
        {
            AnalyzerName = "Version Analyzer",
            HasFindings = true,
            ReportLines = new List<string> { "Version details" },
            Data = new Dictionary<string, object>
            {
                { "IsDowngrade", true }
            }
        };
        
        var scanResult = new ScanResult
        {
            LogPath = "test.log",
            AnalysisResults = new List<AnalysisResult> { versionResult }
        };
        
        // Act
        report.AddFcxReportSections(scanResult);
        
        // Assert
        report.Should().Contain("# FCX SUMMARY #\n", "FCX summary section should be added");
        report.Should().Contain("  • Game version downgrade detected\n", "downgrade warning should be shown");
    }

    [Fact]
    public void AddFcxReportSections_NoSummary_WhenNoIssuesDetected()
    {
        // Arrange
        var report = new List<string>();
        var versionResult = new GenericAnalysisResult
        {
            AnalyzerName = "Version Analyzer",
            HasFindings = true,
            ReportLines = new List<string> { "Version details" },
            Data = new Dictionary<string, object>
            {
                { "IsDowngrade", false }
            }
        };
        
        var scanResult = new ScanResult
        {
            LogPath = "test.log",
            AnalysisResults = new List<AnalysisResult> { versionResult }
        };
        
        // Act
        report.AddFcxReportSections(scanResult);
        
        // Assert
        report.Should().NotContain("# FCX SUMMARY #\n", "summary should not be added when no issues are detected");
    }

    [Fact]
    public void AddFcxReportSections_HandlesCombinedIssuesInSummary()
    {
        // Arrange
        var report = new List<string>();
        var fcxResult = new GenericAnalysisResult
        {
            AnalyzerName = "FCX Analyzer",
            HasFindings = true,
            ReportLines = new List<string> { "FCX" },
            Data = new Dictionary<string, object>
            {
                { "ModifiedFilesCount", 2 },
                { "MissingFilesCount", 1 }
            }
        };
        
        var conflictResult = new GenericAnalysisResult
        {
            AnalyzerName = "Mod Conflict Analyzer",
            HasFindings = true,
            ReportLines = new List<string> { "Conflict" },
            Data = new Dictionary<string, object>
            {
                { "TotalIssues", 3 }
            }
        };
        
        var versionResult = new GenericAnalysisResult
        {
            AnalyzerName = "Version Analyzer",
            HasFindings = true,
            ReportLines = new List<string> { "Version" },
            Data = new Dictionary<string, object>
            {
                { "IsDowngrade", true }
            }
        };
        
        var scanResult = new ScanResult
        {
            LogPath = "test.log",
            AnalysisResults = new List<AnalysisResult> { fcxResult, conflictResult, versionResult }
        };
        
        // Act
        report.AddFcxReportSections(scanResult);
        
        // Assert
        report.Should().Contain("# FCX SUMMARY #\n", "FCX summary section should be added");
        report.Should().Contain("  • 2 modified game files detected\n", "modified files count should be shown");
        report.Should().Contain("  • 1 missing game files detected\n", "missing files count should be shown");
        report.Should().Contain("  • 3 mod conflicts detected\n", "mod conflicts count should be shown");
        report.Should().Contain("  • Game version downgrade detected\n", "downgrade warning should be shown");
        report.Should().Contain("The following issues were detected:\n", "summary header should be included");
    }
}