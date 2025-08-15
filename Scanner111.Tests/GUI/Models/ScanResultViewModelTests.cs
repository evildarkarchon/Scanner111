using FluentAssertions;
using Scanner111.Core.Analyzers;
using Scanner111.Core.Models;
using Scanner111.GUI.Models;

namespace Scanner111.Tests.GUI.Models;

[Collection("GUI Tests")]
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
        viewModel.ScanResult.Should().Be(scanResult, "because the scan result should be stored");
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
        description.Should().Be("crash-2024-01-01-123456.log", "because description should be the file name");
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
        details.Should().Be("Error 1; Error 2", "because details should show all error messages");
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
        details.Should().Be("Processing time: 1.50s", "because details should show processing time when no errors");
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
        {
            scanResult.Status = ScanStatus.Failed;
            scanResult.AddError("Failed scan");
        }

        if (hasErrors)
            scanResult.AddError("Test error");
        var viewModel = new ScanResultViewModel(scanResult);

        // Act
        var severity = viewModel.Severity;

        // Assert
        severity.Should().Be(expectedSeverity, "because severity should reflect the error state");
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
        {
            scanResult.AddError("Test error");
        }

        var viewModel = new ScanResultViewModel(scanResult);

        // Act
        var color = viewModel.SeverityColor;

        // Assert
        color.Should().Be(expectedColor, "because severity color should match the severity level");
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
        category.Should().Be("Completed", "because category should show the scan status");
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
        firstLine.Should().Be("No issues found", "because empty report should show default message");
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
        firstLine.Should().Be("This is the first line with dash", "because leading dash should be removed");
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
        firstLine.Should().Be("This is the first line with asterisk", "because leading asterisk should be removed");
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
        firstLine.Should().HaveLength(100, "because long lines should be truncated to 100 characters");
        firstLine.Should().EndWith("...", "because truncated lines should have ellipsis");
        firstLine.Should().Be(new string('A', 97) + "...", "because truncation should preserve first 97 characters");
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
        firstLine.Should().Be("Trimmed content", "because whitespace and markers should be trimmed");
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
        viewModel1.ScanResult.Should().BeSameAs(scanResult, "because view model should reference the same scan result");
        viewModel2.ScanResult.Should().BeSameAs(scanResult, "because view model should reference the same scan result");
        viewModel1.ScanResult.Should().BeSameAs(viewModel2.ScanResult,
            "because both view models should reference the same instance");
    }
}