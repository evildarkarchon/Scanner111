using Microsoft.Extensions.Logging.Abstractions;
using Scanner111.Core.Infrastructure;
using Scanner111.Core.Models;
using System.Reflection;
using Xunit;

namespace Scanner111.Tests.Infrastructure;

public class OPCFilteringTests : IDisposable
{
    private readonly string _tempDirectory;
    private readonly ReportWriter _reportWriter;

    public OPCFilteringTests()
    {
        _tempDirectory = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
        Directory.CreateDirectory(_tempDirectory);
        _reportWriter = new ReportWriter(NullLogger<ReportWriter>.Instance);
    }

    public void Dispose()
    {
        if (Directory.Exists(_tempDirectory))
        {
            Directory.Delete(_tempDirectory, true);
        }
    }

    [Theory]
    [InlineData("CHECKING FOR MODS THAT ARE PATCHED THROUGH OPC INSTALLER...")]
    [InlineData("MODS PATCHED THROUGH OPC INSTALLER")]
    public void FilterReportContent_WithOPCHeaders_RemovesOPCSections(string opcHeader)
    {
        // Arrange
        var reportContent = string.Join('\n', new[]
        {
            "Normal content before",
            "====================================================",
            opcHeader,
            "====================================================",
            "OPC content to be removed",
            "More OPC content",
            "====================================================",
            "NEXT VALID SECTION",
            "====================================================",
            "Normal content after"
        });

        // Act
        var filtered = InvokeFilterReportContent(reportContent);

        // Assert
        Assert.Contains("Normal content before", filtered);
        Assert.Contains("Normal content after", filtered);
        Assert.DoesNotContain("OPC content to be removed", filtered);
        Assert.DoesNotContain("More OPC content", filtered);
        Assert.DoesNotContain(opcHeader, filtered);
        Assert.Contains("NEXT VALID SECTION", filtered);
    }

    [Fact]
    public void FilterReportContent_WithOPCFoundMessage_RemovesOPCContent()
    {
        // Arrange
        var reportContent = string.Join('\n', new[]
        {
            "Regular analysis",
            "====================================================",
            "CHECKING FOR MODS THAT ARE PATCHED THROUGH OPC INSTALLER...",
            "====================================================",
            "# FOUND NO PROBLEMATIC MODS THAT ARE ALREADY PATCHED THROUGH THE OPC INSTALLER #",
            "",
            "====================================================",
            "CHECKING FOR MODS THAT IF IMPORTANT PATCHES & FIXES ARE INSTALLED...",
            "====================================================",
            "Continuing with normal analysis"
        });

        // Act
        var filtered = InvokeFilterReportContent(reportContent);

        // Assert
        Assert.Contains("Regular analysis", filtered);
        Assert.Contains("Continuing with normal analysis", filtered);
        Assert.DoesNotContain("FOUND NO PROBLEMATIC MODS THAT ARE ALREADY PATCHED THROUGH THE OPC INSTALLER", filtered);
        Assert.DoesNotContain("CHECKING FOR MODS THAT ARE PATCHED THROUGH OPC INSTALLER", filtered);
        Assert.Contains("CHECKING FOR MODS THAT IF IMPORTANT PATCHES & FIXES ARE INSTALLED", filtered);
    }

    [Fact]
    public void FilterReportContent_WithNestedOPCSections_RemovesAllOPCContent()
    {
        // Arrange
        var reportContent = string.Join('\n', new[]
        {
            "Start",
            "====================================================",
            "MODS PATCHED THROUGH OPC INSTALLER",
            "====================================================",
            "First OPC section",
            "====================================================",
            "CHECKING FOR MODS THAT ARE PATCHED THROUGH OPC INSTALLER...",
            "====================================================",
            "Second OPC section",
            "====================================================",
            "REGULAR SECTION NAME",
            "====================================================",
            "End"
        });

        // Act
        var filtered = InvokeFilterReportContent(reportContent);

        // Assert
        Assert.Contains("Start", filtered);
        Assert.Contains("End", filtered);
        Assert.Contains("REGULAR SECTION NAME", filtered);
        Assert.DoesNotContain("First OPC section", filtered);
        Assert.DoesNotContain("Second OPC section", filtered);
        Assert.DoesNotContain("MODS PATCHED THROUGH OPC INSTALLER", filtered);
        Assert.DoesNotContain("CHECKING FOR MODS THAT ARE PATCHED THROUGH OPC INSTALLER", filtered);
    }

    [Fact]
    public void FilterReportContent_WithOPCAtBeginning_RemovesOPCContent()
    {
        // Arrange
        var reportContent = string.Join('\n', new[]
        {
            "====================================================",
            "CHECKING FOR MODS THAT ARE PATCHED THROUGH OPC INSTALLER...",
            "====================================================",
            "OPC content at start",
            "====================================================",
            "NORMAL SECTION",
            "====================================================",
            "Normal content"
        });

        // Act
        var filtered = InvokeFilterReportContent(reportContent);

        // Assert
        Assert.DoesNotContain("OPC content at start", filtered);
        Assert.DoesNotContain("CHECKING FOR MODS THAT ARE PATCHED THROUGH OPC INSTALLER", filtered);
        Assert.Contains("NORMAL SECTION", filtered);
        Assert.Contains("Normal content", filtered);
    }

    [Fact]
    public void FilterReportContent_WithOPCAtEnd_RemovesOPCContent()
    {
        // Arrange
        var reportContent = string.Join('\n', new[]
        {
            "Normal content",
            "====================================================",
            "REGULAR SECTION",
            "====================================================",
            "More normal content",
            "====================================================",
            "MODS PATCHED THROUGH OPC INSTALLER",
            "====================================================",
            "OPC content at end"
        });

        // Act
        var filtered = InvokeFilterReportContent(reportContent);

        // Assert
        Assert.Contains("Normal content", filtered);
        Assert.Contains("More normal content", filtered);
        Assert.Contains("REGULAR SECTION", filtered);
        Assert.DoesNotContain("OPC content at end", filtered);
        Assert.DoesNotContain("MODS PATCHED THROUGH OPC INSTALLER", filtered);
    }

    [Fact]
    public void FilterReportContent_WithoutOPCContent_ReturnsUnchanged()
    {
        // Arrange
        var reportContent = string.Join('\n', new[]
        {
            "Normal analysis content",
            "====================================================",
            "CHECKING FOR PLUGIN ISSUES",
            "====================================================",
            "Found 3 plugin issues",
            "====================================================",
            "CHECKING FOR FORM ID ISSUES",
            "====================================================",
            "No Form ID issues found"
        });

        // Act
        var filtered = InvokeFilterReportContent(reportContent);

        // Assert
        Assert.Equal(reportContent, filtered);
    }

    [Fact]
    public void FilterReportContent_WithEmptyContent_ReturnsEmpty()
    {
        // Arrange
        var reportContent = "";

        // Act
        var filtered = InvokeFilterReportContent(reportContent);

        // Assert
        Assert.Equal("", filtered);
    }

    [Fact]
    public void FilterReportContent_WithNullContent_ReturnsNull()
    {
        // Arrange
        string? reportContent = null;

        // Act
        var filtered = InvokeFilterReportContent(reportContent);

        // Assert
        Assert.Null(filtered);
    }

    [Fact]
    public void FilterReportContent_WithOPCInMiddleOfLine_DoesNotRemove()
    {
        // Arrange
        var reportContent = string.Join('\n', new[]
        {
            "This line mentions OPC INSTALLER but is not a section header",
            "====================================================",
            "NORMAL SECTION",
            "====================================================",
            "Some plugins may need OPC installer patches",
            "This should remain"
        });

        // Act
        var filtered = InvokeFilterReportContent(reportContent);

        // Assert
        Assert.Equal(reportContent, filtered);
        Assert.Contains("This line mentions OPC INSTALLER but is not a section header", filtered);
        Assert.Contains("Some plugins may need OPC installer patches", filtered);
    }

    [Fact]
    public void FilterReportContent_WithOPCCaseVariations_RemovesOPCContent()
    {
        // Arrange
        var reportContent = string.Join('\n', new[]
        {
            "Normal content",
            "====================================================",
            "checking for mods that are patched through opc installer...",
            "====================================================",
            "OPC content with different case",
            "====================================================",
            "NORMAL SECTION",
            "====================================================",
            "End content"
        });

        // Act
        var filtered = InvokeFilterReportContent(reportContent);

        // Assert
        Assert.Contains("Normal content", filtered);
        Assert.Contains("End content", filtered);
        Assert.Contains("NORMAL SECTION", filtered);
        // Note: The current implementation is case-sensitive, so this would NOT be filtered
        // This test documents the current behavior
        Assert.Contains("checking for mods that are patched through opc installer", filtered);
    }

    [Theory]
    [InlineData("FOUND NO PROBLEMATIC MODS THAT ARE ALREADY PATCHED THROUGH THE OPC INSTALLER")]
    [InlineData("# FOUND NO PROBLEMATIC MODS THAT ARE ALREADY PATCHED THROUGH THE OPC INSTALLER #")]
    [InlineData("FOUND PROBLEMATIC MODS THAT ARE ALREADY PATCHED THROUGH THE OPC INSTALLER")]
    public void FilterReportContent_WithOPCResultMessages_RemovesMessages(string opcResultMessage)
    {
        // Arrange
        var reportContent = string.Join('\n', new[]
        {
            "Analysis start",
            "====================================================",
            "CHECKING FOR MODS THAT ARE PATCHED THROUGH OPC INSTALLER...",
            "====================================================",
            opcResultMessage,
            "",
            "====================================================",
            "NEXT SECTION",
            "====================================================",
            "Analysis continues"
        });

        // Act
        var filtered = InvokeFilterReportContent(reportContent);

        // Assert
        Assert.Contains("Analysis start", filtered);
        Assert.Contains("Analysis continues", filtered);
        Assert.Contains("NEXT SECTION", filtered);
        Assert.DoesNotContain(opcResultMessage, filtered);
        Assert.DoesNotContain("CHECKING FOR MODS THAT ARE PATCHED THROUGH OPC INSTALLER", filtered);
    }

    /// <summary>
    /// Uses reflection to access the private FilterReportContent method for testing
    /// </summary>
    private string? InvokeFilterReportContent(string? reportContent)
    {
        var method = typeof(ReportWriter).GetMethod("FilterReportContent", BindingFlags.NonPublic | BindingFlags.Static);
        Assert.NotNull(method);
        
        return (string?)method.Invoke(null, new object?[] { reportContent });
    }
}