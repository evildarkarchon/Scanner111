using System.Collections.Generic;
using System.Linq;
using Scanner111.Core.Analyzers;
using Scanner111.Core.FCX;
using Scanner111.Core.Models;
using Xunit;

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
        Assert.Single(report);
        Assert.Equal("Initial content", report[0]);
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
        Assert.Contains("FCX MODE - ADVANCED FILE INTEGRITY CHECKING\n", report);
        Assert.Contains("# FILE INTEGRITY CHECK RESULTS #\n", report);
        Assert.Contains("FCX finding 1", report);
        Assert.Contains("FCX finding 2", report);
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
        Assert.Contains("FCX MODE - ADVANCED FILE INTEGRITY CHECKING\n", report);
        Assert.Contains("# MOD CONFLICT ANALYSIS #\n", report);
        Assert.Contains("Conflict 1", report);
        Assert.Contains("Conflict 2", report);
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
        Assert.Contains("FCX MODE - ADVANCED FILE INTEGRITY CHECKING\n", report);
        Assert.Contains("# GAME VERSION INFORMATION #\n", report);
        Assert.Contains("Version info 1", report);
        Assert.Contains("Version info 2", report);
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
        Assert.Contains("FCX MODE - ADVANCED FILE INTEGRITY CHECKING\n", report);
        Assert.Contains("# GAME VERSION INFORMATION #\n", report);
        Assert.Contains("# FILE INTEGRITY CHECK RESULTS #\n", report);
        Assert.Contains("# MOD CONFLICT ANALYSIS #\n", report);
        Assert.Contains("Version finding", report);
        Assert.Contains("FCX finding", report);
        Assert.Contains("Conflict finding", report);
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
        
        Assert.True(versionIndex < fcxIndex);
        Assert.True(fcxIndex < conflictIndex);
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
        Assert.Throws<NullReferenceException>(() => report.AddFcxReportSections(scanResult));
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
        Assert.Empty(report);
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
        report.AddFcxReportSections(scanResult);
        Assert.Contains("FCX MODE - ADVANCED FILE INTEGRITY CHECKING\n", report);
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
        Assert.Contains("Version info", report);
        Assert.DoesNotContain("Should not appear", report);
        Assert.DoesNotContain("# FILE INTEGRITY CHECK RESULTS #\n", report);
    }

    [Fact]
    public void GenerateFcxSectionForSettings_WhenEnabled_ReturnsEnabledMessage()
    {
        // Act
        var result = FcxReportExtensions.GenerateFcxSectionForSettings(true);
        
        // Assert
        Assert.Contains("FCX MODE IS ENABLED", result);
        Assert.Contains("PERFORMING ADVANCED FILE INTEGRITY CHECKS", result);
        Assert.Contains("FCX Mode checks game file integrity and detects mod conflicts", result);
    }

    [Fact]
    public void GenerateFcxSectionForSettings_WhenDisabled_ReturnsDisabledMessage()
    {
        // Act
        var result = FcxReportExtensions.GenerateFcxSectionForSettings(false);
        
        // Assert
        Assert.Contains("FCX MODE IS DISABLED", result);
        Assert.Contains("YOU CAN ENABLE IT TO DETECT PROBLEMS", result);
        Assert.Contains("FCX Mode can be enabled in the Scanner 111 application settings", result);
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
        Assert.Contains("# FCX SUMMARY #\n", report);
        Assert.Contains("  • 5 modified game files detected\n", report);
        Assert.Contains("  • 3 missing game files detected\n", report);
        Assert.Contains("* For detailed FCX documentation and solutions, see: https://github.com/evildarkarchon/Scanner111/wiki/FCX-Mode *\n", report);
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
        Assert.Contains("# FCX SUMMARY #\n", report);
        Assert.Contains("  • 7 mod conflicts detected\n", report);
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
        Assert.Contains("# FCX SUMMARY #\n", report);
        Assert.Contains("  • Game version downgrade detected\n", report);
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
        Assert.DoesNotContain("# FCX SUMMARY #\n", report);
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
        Assert.Contains("# FCX SUMMARY #\n", report);
        Assert.Contains("  • 2 modified game files detected\n", report);
        Assert.Contains("  • 1 missing game files detected\n", report);
        Assert.Contains("  • 3 mod conflicts detected\n", report);
        Assert.Contains("  • Game version downgrade detected\n", report);
        Assert.Contains("The following issues were detected:\n", report);
    }
}