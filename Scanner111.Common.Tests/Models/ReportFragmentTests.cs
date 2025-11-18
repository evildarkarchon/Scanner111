using FluentAssertions;
using Scanner111.Common.Models.Reporting;

namespace Scanner111.Common.Tests.Models;

/// <summary>
/// Tests for report fragment composition (ReportFragment, LogAnalysisResult).
/// </summary>
public class ReportFragmentTests
{
    [Fact]
    public void ReportFragment_DefaultValues_AreCorrect()
    {
        // Arrange & Act
        var fragment = new ReportFragment();

        // Assert
        fragment.Lines.Should().BeEmpty();
        fragment.HasContent.Should().BeFalse();
    }

    [Fact]
    public void ReportFragment_FromLines_CreatesFragmentCorrectly()
    {
        // Arrange & Act
        var fragment = ReportFragment.FromLines("Line 1", "Line 2", "Line 3");

        // Assert
        fragment.Lines.Should().HaveCount(3);
        fragment.Lines[0].Should().Be("Line 1");
        fragment.Lines[1].Should().Be("Line 2");
        fragment.Lines[2].Should().Be("Line 3");
        fragment.HasContent.Should().BeTrue();
    }

    [Fact]
    public void ReportFragment_FromLines_WithNoLines_CreatesEmptyFragment()
    {
        // Arrange & Act
        var fragment = ReportFragment.FromLines();

        // Assert
        fragment.Lines.Should().BeEmpty();
        fragment.HasContent.Should().BeFalse();
    }

    [Fact]
    public void ReportFragment_WithHeader_AddsHeaderWhenContentExists()
    {
        // Arrange
        var fragment = ReportFragment.FromLines("Line 1", "Line 2");

        // Act
        var result = fragment.WithHeader("HEADER");

        // Assert
        result.Lines.Should().HaveCount(4);
        result.Lines[0].Should().Be("HEADER");
        result.Lines[1].Should().BeEmpty();
        result.Lines[2].Should().Be("Line 1");
        result.Lines[3].Should().Be("Line 2");
    }

    [Fact]
    public void ReportFragment_WithHeader_DoesNotAddHeaderWhenNoContent()
    {
        // Arrange
        var fragment = new ReportFragment();

        // Act
        var result = fragment.WithHeader("HEADER");

        // Assert
        result.Should().Be(fragment);
        result.Lines.Should().BeEmpty();
    }

    [Fact]
    public void ReportFragment_WithHeader_IsImmutable()
    {
        // Arrange
        var original = ReportFragment.FromLines("Content");

        // Act
        var withHeader = original.WithHeader("HEADER");

        // Assert
        original.Lines.Should().HaveCount(1);
        original.Lines[0].Should().Be("Content");
        withHeader.Lines.Should().HaveCount(3);
        withHeader.Lines[0].Should().Be("HEADER");
    }

    [Fact]
    public void ReportFragment_Addition_CombinesFragments()
    {
        // Arrange
        var frag1 = ReportFragment.FromLines("A", "B");
        var frag2 = ReportFragment.FromLines("C", "D");

        // Act
        var result = frag1 + frag2;

        // Assert
        result.Lines.Should().Equal("A", "B", "C", "D");
        result.HasContent.Should().BeTrue();
    }

    [Fact]
    public void ReportFragment_Addition_WithEmptyFragment_PreservesContent()
    {
        // Arrange
        var frag1 = ReportFragment.FromLines("A", "B");
        var frag2 = new ReportFragment();

        // Act
        var result = frag1 + frag2;

        // Assert
        result.Lines.Should().Equal("A", "B");
    }

    [Fact]
    public void ReportFragment_Addition_IsImmutable()
    {
        // Arrange
        var frag1 = ReportFragment.FromLines("A");
        var frag2 = ReportFragment.FromLines("B");

        // Act
        var result = frag1 + frag2;

        // Assert
        frag1.Lines.Should().HaveCount(1);
        frag2.Lines.Should().HaveCount(1);
        result.Lines.Should().HaveCount(2);
    }

    [Fact]
    public void ReportFragment_MultipleAdditions_CombinesCorrectly()
    {
        // Arrange
        var frag1 = ReportFragment.FromLines("1");
        var frag2 = ReportFragment.FromLines("2");
        var frag3 = ReportFragment.FromLines("3");

        // Act
        var result = frag1 + frag2 + frag3;

        // Assert
        result.Lines.Should().Equal("1", "2", "3");
    }

    [Fact]
    public void ReportFragment_ComplexComposition_WorksCorrectly()
    {
        // Arrange
        var header = ReportFragment.FromLines("# Report Header");
        var section1 = ReportFragment.FromLines("Section 1", "Data 1")
            .WithHeader("## Section 1");
        var section2 = ReportFragment.FromLines("Section 2", "Data 2")
            .WithHeader("## Section 2");

        // Act
        var report = header + section1 + section2;

        // Assert
        report.Lines.Should().HaveCount(9);
        report.Lines[0].Should().Be("# Report Header");
        report.Lines[1].Should().Be("## Section 1");
        report.Lines[2].Should().BeEmpty();
        report.Lines[3].Should().Be("Section 1");
        report.Lines[4].Should().Be("Data 1");
        report.Lines[5].Should().Be("## Section 2");
        report.Lines[6].Should().BeEmpty();
        report.Lines[7].Should().Be("Section 2");
        report.Lines[8].Should().Be("Data 2");
    }

    [Fact]
    public void LogAnalysisResult_DefaultValues_AreCorrect()
    {
        // Arrange & Act
        var result = new LogAnalysisResult();

        // Assert
        result.LogFileName.Should().BeEmpty();
        result.Segments.Should().BeEmpty();
        result.IsComplete.Should().BeFalse();
        result.Warnings.Should().BeEmpty();
    }

    [Fact]
    public void LogAnalysisResult_WithData_StoresCorrectly()
    {
        // Arrange
        var header = new Scanner111.Common.Models.Analysis.CrashHeader
        {
            GameVersion = "1.10.163.0",
            MainError = "Test error"
        };
        var segments = new List<Scanner111.Common.Models.Analysis.LogSegment>
        {
            new() { Name = "MODULES" },
            new() { Name = "PLUGINS" }
        };
        var report = ReportFragment.FromLines("Report content");
        var warnings = new List<string> { "Warning 1", "Warning 2" };

        // Act
        var result = new LogAnalysisResult
        {
            LogFileName = "crash-12624.log",
            Header = header,
            Segments = segments,
            Report = report,
            IsComplete = true,
            Warnings = warnings
        };

        // Assert
        result.LogFileName.Should().Be("crash-12624.log");
        result.Header.Should().Be(header);
        result.Segments.Should().HaveCount(2);
        result.Report.Should().Be(report);
        result.IsComplete.Should().BeTrue();
        result.Warnings.Should().HaveCount(2);
    }

    [Fact]
    public void LogAnalysisResult_IsImmutable()
    {
        // Arrange
        var result1 = new LogAnalysisResult
        {
            LogFileName = "original.log",
            IsComplete = false
        };

        // Act
        var result2 = result1 with
        {
            IsComplete = true
        };

        // Assert
        result1.IsComplete.Should().BeFalse();
        result2.IsComplete.Should().BeTrue();
        result1.LogFileName.Should().Be(result2.LogFileName);
    }
}
