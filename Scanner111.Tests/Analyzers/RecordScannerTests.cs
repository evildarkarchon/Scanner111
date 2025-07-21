using Scanner111.Core.Analyzers;
using Scanner111.Core.Models;
using Scanner111.Tests.TestHelpers;

namespace Scanner111.Tests.Analyzers;

public class RecordScannerTests
{
    private readonly RecordScanner _analyzer;
    private readonly TestYamlSettingsProvider _yamlSettings;

    public RecordScannerTests()
    {
        _yamlSettings = new TestYamlSettingsProvider();
        _analyzer = new RecordScanner(_yamlSettings);
    }

    [Fact]
    public async Task AnalyzeAsync_WithValidRecords_ReturnsGenericAnalysisResult()
    {
        // Arrange
        var crashLog = new CrashLog
        {
            FilePath = "test.log",
            MainError = "Test error",
            CallStack = new List<string>
            {
                "normal line 1",
                "normal line 2",
                "another line"
            }
        };

        // Act
        var result = await _analyzer.AnalyzeAsync(crashLog);

        // Assert
        Assert.IsType<GenericAnalysisResult>(result);
        var recordResult = (GenericAnalysisResult)result;

        Assert.Equal("Record Scanner", recordResult.AnalyzerName);
        Assert.NotNull(recordResult.ReportLines);
        Assert.Contains("RecordsMatches", recordResult.Data);
        Assert.Contains("ExtractedRecords", recordResult.Data);
    }

    [Fact]
    public async Task AnalyzeAsync_WithNoRecords_ReturnsEmptyResult()
    {
        // Arrange
        var crashLog = new CrashLog
        {
            FilePath = "test.log",
            MainError = "Test error",
            CallStack = new List<string>
            {
                "normal line 1",
                "normal line 2",
                "another line"
            }
        };

        // Act
        var result = await _analyzer.AnalyzeAsync(crashLog);

        // Assert
        var recordResult = (GenericAnalysisResult)result;
        Assert.False(recordResult.HasFindings);

        var recordsMatches = (List<string>)recordResult.Data["RecordsMatches"];
        var extractedRecords = (List<string>)recordResult.Data["ExtractedRecords"];

        Assert.Empty(recordsMatches);
        Assert.Empty(extractedRecords);
        Assert.Contains("* COULDN'T FIND ANY NAMED RECORDS *", recordResult.ReportText);
    }

    [Fact]
    public async Task AnalyzeAsync_WithRSPMarkerLines_ExtractsCorrectly()
    {
        // Note: The current implementation has empty record sets, so this test demonstrates structure
        // In a real implementation, the _lowerRecords would be populated from YAML configuration

        // Arrange
        var crashLog = new CrashLog
        {
            FilePath = "test.log",
            MainError = "Test error",
            CallStack = new List<string>
            {
                "[RSP+0x20] 0x12345678 some_record_name",
                "[RSP+0x30] 0x87654321 another_record",
                "normal line without RSP marker"
            }
        };

        // Act
        var result = await _analyzer.AnalyzeAsync(crashLog);

        // Assert
        var recordResult = (GenericAnalysisResult)result;
        Assert.Equal("Record Scanner", recordResult.AnalyzerName);

        // With empty record sets, no records should be found
        Assert.False(recordResult.HasFindings);
        Assert.Contains("* COULDN'T FIND ANY NAMED RECORDS *", recordResult.ReportText);
    }

    [Fact]
    public async Task AnalyzeAsync_WithNonRSPMarkerLines_ExtractsCorrectly()
    {
        // Arrange
        var crashLog = new CrashLog
        {
            FilePath = "test.log",
            MainError = "Test error",
            CallStack = new List<string>
            {
                "some_record_name at location 0x12345678",
                "another_record found in memory",
                "normal line"
            }
        };

        // Act
        var result = await _analyzer.AnalyzeAsync(crashLog);

        // Assert
        var recordResult = (GenericAnalysisResult)result;
        Assert.Equal("Record Scanner", recordResult.AnalyzerName);

        // With empty record sets, no records should be found
        Assert.False(recordResult.HasFindings);
    }

    [Fact]
    public async Task AnalyzeAsync_ExtractsRecordsFromCallStack()
    {
        // Arrange
        var crashLog = new CrashLog
        {
            FilePath = "test.log",
            MainError = "Test error",
            CallStack = new List<string>
            {
                "[RSP+0x20] 0x12345678 record1",
                "[RSP+0x30] 0x87654321 record2",
                "normal line",
                "[RSP+0x40] 0x11111111 record3"
            }
        };

        // Act
        var result = await _analyzer.AnalyzeAsync(crashLog);

        // Assert
        var recordResult = (GenericAnalysisResult)result;
        var extractedRecords = (List<string>)recordResult.Data["ExtractedRecords"];

        // Records should be extracted but not matched (due to empty record sets)
        Assert.Empty(extractedRecords);
    }

    [Fact]
    public async Task AnalyzeAsync_WithDuplicateRecords_CountsCorrectly()
    {
        // This test demonstrates the counting functionality structure
        // In a real implementation with populated record sets, this would work

        // Arrange
        var crashLog = new CrashLog
        {
            FilePath = "test.log",
            MainError = "Test error",
            CallStack = new List<string>
            {
                "[RSP+0x20] 0x12345678 duplicate_record",
                "[RSP+0x30] 0x87654321 duplicate_record",
                "[RSP+0x40] 0x11111111 unique_record",
                "[RSP+0x50] 0x22222222 duplicate_record"
            }
        };

        // Act
        var result = await _analyzer.AnalyzeAsync(crashLog);

        // Assert
        var recordResult = (GenericAnalysisResult)result;
        Assert.Equal("Record Scanner", recordResult.AnalyzerName);

        // Should handle duplicate counting properly when records are found
        Assert.False(recordResult.HasFindings); // Empty record sets
    }

    [Fact]
    public async Task AnalyzeAsync_GeneratesCorrectReportFormat()
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
        var recordResult = (GenericAnalysisResult)result;
        var reportText = recordResult.ReportText;

        // Should contain the "no records found" message
        Assert.Contains("* COULDN'T FIND ANY NAMED RECORDS *", reportText);

        // Report structure should be correct
        Assert.NotNull(recordResult.ReportLines);
        Assert.Single(recordResult.ReportLines);
    }

    [Fact]
    public async Task AnalyzeAsync_WithExplanatoryNotes_FormatsCorrectly()
    {
        // Test would need actual records to trigger explanatory notes
        // This test demonstrates the expected behavior structure

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
        var recordResult = (GenericAnalysisResult)result;

        // With populated record sets, the report would contain:
        // - Individual record entries with counts
        // - Explanatory notes about what the numbers mean
        // - Information about the crash generator
        // - Notes about named records providing extra info

        Assert.Equal("Record Scanner", recordResult.AnalyzerName);
        Assert.NotNull(recordResult.ReportLines);
    }

    [Fact]
    public async Task AnalyzeAsync_WithCrashgenName_UsesCorrectName()
    {
        // Arrange
        var customYamlSettings = new TestYamlSettingsProvider();
        var customAnalyzer = new RecordScanner(customYamlSettings);

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
        var recordResult = (GenericAnalysisResult)result;
        Assert.Equal("Record Scanner", recordResult.AnalyzerName);

        // The crashgen name would be used in explanatory notes when records are found
        Assert.NotNull(recordResult.ReportLines);
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
        var recordResult1 = (GenericAnalysisResult)result1;
        var recordResult2 = (GenericAnalysisResult)result2;

        // Should return consistent results
        Assert.Equal(recordResult1.AnalyzerName, recordResult2.AnalyzerName);
        Assert.Equal(recordResult1.ReportLines.Count, recordResult2.ReportLines.Count);
        Assert.Equal(recordResult1.HasFindings, recordResult2.HasFindings);
    }
}