using System;
using System.Collections.Generic;
using Scanner111.Core.Analyzers;
using Scanner111.Core.Models;
using Scanner111.GUI.Models;
using Xunit;

namespace Scanner111.Tests.GUI.Models;

public class ScanResultViewModelTests
{
    [Fact]
    public void Constructor_SetsProperties()
    {
        // Arrange
        var scanResult = new ScanResult { LogPath = "test.log" };

        // Act
        var viewModel = new ScanResultViewModel(scanResult);

        // Assert
        Assert.Equal(scanResult, viewModel.ScanResult);
    }

    [Fact]
    public void Description_ReturnsFileName()
    {
        // Arrange
        var scanResult = new ScanResult { LogPath = @"C:\Users\Test\Documents\crash-2024-01-01-123456.log" };
        var viewModel = new ScanResultViewModel(scanResult);

        // Act
        var description = viewModel.Description;

        // Assert
        Assert.Equal("crash-2024-01-01-123456.log", description);
    }

    [Fact]
    public void Details_WhenHasErrors_ReturnsErrorMessages()
    {
        // Arrange
        var scanResult = new ScanResult { LogPath = "test.log" };
        scanResult.AddError("Error 1");
        scanResult.AddError("Error 2");
        var viewModel = new ScanResultViewModel(scanResult);

        // Act
        var details = viewModel.Details;

        // Assert
        Assert.Equal("Error 1; Error 2", details);
    }

    [Fact]
    public void Details_WhenNoErrors_ReturnsProcessingTime()
    {
        // Arrange
        var scanResult = new ScanResult { LogPath = "test.log" };
        scanResult.ProcessingTime = TimeSpan.FromSeconds(1.5);
        var viewModel = new ScanResultViewModel(scanResult);

        // Act
        var details = viewModel.Details;

        // Assert
        Assert.Equal("Processing time: 1.50s", details);
    }

    [Theory]
    [InlineData(true, false, "ERROR")]
    [InlineData(false, true, "WARNING")]
    [InlineData(false, false, "INFO")]
    public void Severity_ReturnsCorrectValue(bool failed, bool hasErrors, string expectedSeverity)
    {
        // Arrange
        var scanResult = new ScanResult { LogPath = "test.log" };
        if (failed)
            scanResult.AddError("Failed scan");
        if (hasErrors)
            scanResult.AddError("Test error");
        var viewModel = new ScanResultViewModel(scanResult);

        // Act
        var severity = viewModel.Severity;

        // Assert
        Assert.Equal(expectedSeverity, severity);
    }

    [Theory]
    [InlineData("ERROR", "#FFE53E3E")]
    [InlineData("WARNING", "#FFFF9500")]
    [InlineData("INFO", "#FF0e639c")]
    public void SeverityColor_ReturnsCorrectColor(string severity, string expectedColor)
    {
        // Arrange
        var scanResult = new ScanResult { LogPath = "test.log" };
        if (severity == "ERROR")
        {
            scanResult.AddError("Critical error");
            scanResult.Status = ScanStatus.Failed;
        }
        else if (severity == "WARNING")
            scanResult.AddError("Test error");
        var viewModel = new ScanResultViewModel(scanResult);

        // Act
        var color = viewModel.SeverityColor;

        // Assert
        Assert.Equal(expectedColor, color);
    }

    [Fact]
    public void Category_ReturnsStatusString()
    {
        // Arrange
        var scanResult = new ScanResult 
        {
            LogPath = "test.log",
            Status = ScanStatus.Completed
        };
        var viewModel = new ScanResultViewModel(scanResult);

        // Act
        var category = viewModel.Category;

        // Assert
        Assert.Equal("Completed", category);
    }

    [Fact]
    public void GetFirstReportLine_WhenReportEmpty_ReturnsDefaultMessage()
    {
        // Arrange
        var scanResult = new ScanResult { LogPath = "test.log" };
        var viewModel = new ScanResultViewModel(scanResult);

        // Act
        var firstLine = viewModel.GetFirstReportLine();

        // Assert
        Assert.Equal("No issues found", firstLine);
    }

    [Fact]
    public void GetFirstReportLine_WhenReportHasContent_ReturnsCleanedFirstLine()
    {
        // Arrange
        var scanResult = new ScanResult { LogPath = "test.log" };
        scanResult.Report.Add("- This is the first line with dash");
        scanResult.Report.Add("Second line");
        var viewModel = new ScanResultViewModel(scanResult);

        // Act
        var firstLine = viewModel.GetFirstReportLine();

        // Assert
        Assert.Equal("This is the first line with dash", firstLine);
    }

    [Fact]
    public void GetFirstReportLine_WhenFirstLineHasAsterisk_RemovesIt()
    {
        // Arrange
        var scanResult = new ScanResult { LogPath = "test.log" };
        scanResult.Report.Add("* This is the first line with asterisk");
        var viewModel = new ScanResultViewModel(scanResult);

        // Act
        var firstLine = viewModel.GetFirstReportLine();

        // Assert
        Assert.Equal("This is the first line with asterisk", firstLine);
    }

    [Fact]
    public void GetFirstReportLine_WhenLineTooLong_TruncatesWithEllipsis()
    {
        // Arrange
        var scanResult = new ScanResult { LogPath = "test.log" };
        var longText = new string('A', 110);
        scanResult.Report.Add(longText);
        var viewModel = new ScanResultViewModel(scanResult);

        // Act
        var firstLine = viewModel.GetFirstReportLine();

        // Assert
        Assert.Equal(100, firstLine.Length);
        Assert.EndsWith("...", firstLine);
        Assert.Equal(new string('A', 97) + "...", firstLine);
    }

    [Fact]
    public void GetFirstReportLine_HandlesWhitespace()
    {
        // Arrange
        var scanResult = new ScanResult { LogPath = "test.log" };
        scanResult.Report.Add("  - Trimmed content  ");
        var viewModel = new ScanResultViewModel(scanResult);

        // Act
        var firstLine = viewModel.GetFirstReportLine();

        // Assert
        Assert.Equal("Trimmed content", firstLine);
    }

    [Fact]
    public void MultipleViewModels_ShareSameScanResult()
    {
        // Arrange
        var scanResult = new ScanResult { LogPath = "test.log" };
        scanResult.AddAnalysisResult(new GenericAnalysisResult
        {
            AnalyzerName = "Test Analyzer"
        });

        // Act
        var viewModel1 = new ScanResultViewModel(scanResult);
        var viewModel2 = new ScanResultViewModel(scanResult);

        // Assert
        Assert.Same(scanResult, viewModel1.ScanResult);
        Assert.Same(scanResult, viewModel2.ScanResult);
        Assert.Same(viewModel1.ScanResult, viewModel2.ScanResult);
    }
}