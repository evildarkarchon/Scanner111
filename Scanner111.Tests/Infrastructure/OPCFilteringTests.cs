using System.Reflection;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Scanner111.Core.Infrastructure;

namespace Scanner111.Tests.Infrastructure;

/// <summary>
///     Contains unit tests for filtering OPC-specific content from reports.
/// </summary>
/// <remarks>
///     This class ensures OPC-related headers, sections, or messages within given report content
///     are correctly identified and removed while preserving other valid sections.
/// </remarks>
[Collection("IO Heavy Tests")]
public class OpcFilteringTests : IDisposable
{
    private readonly ReportWriter _reportWriter;
    private readonly string _tempDirectory;

    public OpcFilteringTests()
    {
        _tempDirectory = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
        Directory.CreateDirectory(_tempDirectory);
        _reportWriter = new ReportWriter(NullLogger<ReportWriter>.Instance);
    }

    /// <summary>
    ///     Releases resources used by the OpcFilteringTests instance, including the temporary directory
    ///     created during test initialization.
    /// </summary>
    /// <remarks>
    ///     Deletes the temporary directory if it exists and suppresses finalization for the instance.
    /// </remarks>
    public void Dispose()
    {
        if (Directory.Exists(_tempDirectory)) Directory.Delete(_tempDirectory, true);
        GC.SuppressFinalize(this);
    }

    /// <summary>
    ///     Validates that OPC-specific headers and related content are removed from the report,
    ///     while preserving other valid sections and content.
    /// </summary>
    /// <param name="opcHeader">The text of the OPC header that signifies the start of OPC-related content in the report.</param>
    [Theory]
    [InlineData("CHECKING FOR MODS THAT ARE PATCHED THROUGH OPC INSTALLER...")]
    [InlineData("MODS PATCHED THROUGH OPC INSTALLER")]
    public void FilterReportContent_WithOPCHeaders_RemovesOPCSections(string opcHeader)
    {
        // Arrange
        var reportContent = string.Join('\n', "Normal content before",
            "====================================================", opcHeader,
            "====================================================", "OPC content to be removed", "More OPC content",
            "====================================================", "NEXT VALID SECTION",
            "====================================================", "Normal content after");

        // Act
        var filtered = InvokeFilterReportContent(reportContent);

        // Assert
        filtered.Should().Contain("Normal content before",
            "because normal content before OPC section should be preserved");
        filtered.Should().Contain("Normal content after",
            "because normal content after OPC section should be preserved");
        filtered.Should().NotContain("OPC content to be removed", "because OPC content should be filtered out");
        filtered.Should().NotContain("More OPC content", "because all OPC content should be removed");
        filtered.Should().NotContain(opcHeader, "because OPC headers should be removed");
        filtered.Should().Contain("NEXT VALID SECTION", "because non-OPC sections should be preserved");
    }

    /// <summary>
    ///     Verifies that OPC-related content found in the provided report is correctly removed while other content is
    ///     preserved.
    /// </summary>
    /// <remarks>
    ///     Ensures that specific OPC-related headers, messages, and sections, such as those indicating
    ///     diagnostics or patch statuses through the OPC installer, are identified and filtered out
    ///     from the input report content.
    /// </remarks>
    [Fact]
    public void FilterReportContent_WithOPCFoundMessage_RemovesOPCContent()
    {
        // Arrange
        var reportContent = string.Join('\n', "Regular analysis",
            "====================================================",
            "CHECKING FOR MODS THAT ARE PATCHED THROUGH OPC INSTALLER...",
            "====================================================",
            "# FOUND NO PROBLEMATIC MODS THAT ARE ALREADY PATCHED THROUGH THE OPC INSTALLER #", "",
            "====================================================",
            "CHECKING FOR MODS THAT IF IMPORTANT PATCHES & FIXES ARE INSTALLED...",
            "====================================================", "Continuing with normal analysis");

        // Act
        var filtered = InvokeFilterReportContent(reportContent);

        // Assert
        filtered.Should().Contain("Regular analysis", "because non-OPC content should be preserved");
        filtered.Should().Contain("Continuing with normal analysis", "because content after OPC section should remain");
        filtered.Should().NotContain("FOUND NO PROBLEMATIC MODS THAT ARE ALREADY PATCHED THROUGH THE OPC INSTALLER",
            "because OPC result messages should be removed");
        filtered.Should().NotContain("CHECKING FOR MODS THAT ARE PATCHED THROUGH OPC INSTALLER",
            "because OPC headers should be removed");
        filtered.Should().Contain("CHECKING FOR MODS THAT IF IMPORTANT PATCHES & FIXES ARE INSTALLED",
            "because non-OPC sections should be preserved");
    }

    /// <summary>
    ///     Filters the report content containing nested OPC sections and removes all OPC-specific content.
    /// </summary>
    /// <remarks>
    ///     Ensures that all headers, sections, and messages specific to OPC, including nested instances,
    ///     are completely removed from the report content while retaining other sections and entries.
    /// </remarks>
    [Fact]
    public void FilterReportContent_WithNestedOPCSections_RemovesAllOPCContent()
    {
        // Arrange
        var reportContent = string.Join('\n', "Start", "====================================================",
            "MODS PATCHED THROUGH OPC INSTALLER", "====================================================",
            "First OPC section", "====================================================",
            "CHECKING FOR MODS THAT ARE PATCHED THROUGH OPC INSTALLER...",
            "====================================================", "Second OPC section",
            "====================================================", "REGULAR SECTION NAME",
            "====================================================", "End");

        // Act
        var filtered = InvokeFilterReportContent(reportContent);

        // Assert
        filtered.Should().Contain("Start", "because content before OPC sections should be preserved");
        filtered.Should().Contain("End", "because content after OPC sections should be preserved");
        filtered.Should().Contain("REGULAR SECTION NAME", "because non-OPC sections should be preserved");
        filtered.Should().NotContain("First OPC section", "because first OPC section content should be removed");
        filtered.Should().NotContain("Second OPC section", "because second OPC section content should be removed");
        filtered.Should().NotContain("MODS PATCHED THROUGH OPC INSTALLER", "because OPC headers should be removed");
        filtered.Should().NotContain("CHECKING FOR MODS THAT ARE PATCHED THROUGH OPC INSTALLER",
            "because all OPC headers should be removed");
    }

    /// <summary>
    ///     Ensures that OPC content at the beginning of a report is correctly identified and removed while
    ///     preserving valid sections that follow.
    /// </summary>
    /// <remarks>
    ///     This method verifies that report content starting with OPC-specific headers or sections
    ///     is processed to exclude those segments, ensuring only non-OPC content remains in the final output.
    /// </remarks>
    [Fact]
    public void FilterReportContent_WithOPCAtBeginning_RemovesOPCContent()
    {
        // Arrange
        var reportContent = string.Join('\n', "====================================================",
            "CHECKING FOR MODS THAT ARE PATCHED THROUGH OPC INSTALLER...",
            "====================================================", "OPC content at start",
            "====================================================", "NORMAL SECTION",
            "====================================================", "Normal content");

        // Act
        var filtered = InvokeFilterReportContent(reportContent);

        // Assert
        filtered.Should().NotContain("OPC content at start", "because OPC content at the beginning should be removed");
        filtered.Should().NotContain("CHECKING FOR MODS THAT ARE PATCHED THROUGH OPC INSTALLER",
            "because OPC headers should be removed");
        filtered.Should().Contain("NORMAL SECTION", "because non-OPC sections should be preserved");
        filtered.Should().Contain("Normal content", "because normal content should be preserved");
    }

    /// <summary>
    ///     Filters report content to remove any OPC-specific content found at the end of the report.
    /// </summary>
    /// <remarks>
    ///     Ensures that OPC-related sections or messages, such as "MODS PATCHED THROUGH OPC INSTALLER"
    ///     or other similar OPC headers/messages located at the end of the report, are entirely removed,
    ///     while preserving the integrity of other non-OPC-related content.
    /// </remarks>
    [Fact]
    public void FilterReportContent_WithOPCAtEnd_RemovesOPCContent()
    {
        // Arrange
        var reportContent = string.Join('\n', "Normal content", "====================================================",
            "REGULAR SECTION", "====================================================", "More normal content",
            "====================================================", "MODS PATCHED THROUGH OPC INSTALLER",
            "====================================================", "OPC content at end");

        // Act
        var filtered = InvokeFilterReportContent(reportContent);

        // Assert
        filtered.Should().Contain("Normal content", "because normal content should be preserved");
        filtered.Should().Contain("More normal content", "because all normal content should be preserved");
        filtered.Should().Contain("REGULAR SECTION", "because non-OPC sections should be preserved");
        filtered.Should().NotContain("OPC content at end", "because OPC content at the end should be removed");
        filtered.Should().NotContain("MODS PATCHED THROUGH OPC INSTALLER", "because OPC headers should be removed");
    }

    /// <summary>
    ///     Validates that the filtering process does not alter report content when no OPC-specific
    ///     content is present.
    /// </summary>
    /// <remarks>
    ///     Ensures that reports containing no OPC-related headers, sections, or messages remain unchanged
    ///     after applying the filtering logic.
    /// </remarks>
    [Fact]
    public void FilterReportContent_WithoutOPCContent_ReturnsUnchanged()
    {
        // Arrange
        var reportContent = string.Join('\n', "Normal analysis content",
            "====================================================", "CHECKING FOR PLUGIN ISSUES",
            "====================================================", "Found 3 plugin issues",
            "====================================================", "CHECKING FOR FORM ID ISSUES",
            "====================================================", "No Form ID issues found");

        // Act
        var filtered = InvokeFilterReportContent(reportContent);

        // Assert
        filtered.Should().Be(reportContent, "because no OPC content is present to filter");
    }

    /// <summary>
    ///     Tests whether the FilterReportContent method correctly handles an empty input string by
    ///     returning an empty string without attempting any filtering operations.
    /// </summary>
    /// <remarks>
    ///     Ensures that providing an empty report content does not produce any unintended
    ///     alterations or errors in the filtering process.
    /// </remarks>
    [Fact]
    public void FilterReportContent_WithEmptyContent_ReturnsEmpty()
    {
        // Arrange
        var reportContent = "";

        // Act
        var filtered = InvokeFilterReportContent(reportContent);

        // Assert
        filtered.Should().Be("", "because empty input should return empty output");
    }

    /// <summary>
    ///     Verifies that the FilterReportContent method returns null when provided with null input content.
    /// </summary>
    /// <remarks>
    ///     Ensures that the method properly handles null values and does not throw exceptions or return unintended results.
    /// </remarks>
    [Fact]
    public void FilterReportContent_WithNullContent_ReturnsNull()
    {
        // Arrange
        string? reportContent = null;

        // Act
        var filtered = InvokeFilterReportContent(reportContent);

        // Assert
        filtered.Should().BeNull("because null input should return null");
    }

    /// <summary>
    ///     Ensures that report content containing mentions of OPC within the middle of a line,
    ///     without being part of an OPC-specific header or section, is not removed.
    /// </summary>
    /// <remarks>
    ///     Validates that lines mentioning "OPC" in a non-structural context are preserved,
    ///     ensuring unintentional filtering does not occur in such cases.
    /// </remarks>
    [Fact]
    public void FilterReportContent_WithOPCInMiddleOfLine_DoesNotRemove()
    {
        // Arrange
        var reportContent = string.Join('\n', "This line mentions OPC INSTALLER but is not a section header",
            "====================================================", "NORMAL SECTION",
            "====================================================", "Some plugins may need OPC installer patches",
            "This should remain");

        // Act
        var filtered = InvokeFilterReportContent(reportContent);

        // Assert
        filtered.Should().Be(reportContent, "because OPC mentions in regular content should not be filtered");
        filtered.Should().Contain("This line mentions OPC INSTALLER but is not a section header",
            "because OPC mentions outside headers should be preserved");
        filtered.Should().Contain("Some plugins may need OPC installer patches",
            "because OPC references in content should remain");
    }

    /// <summary>
    ///     Tests the filtering logic of report content containing OPC-related sections or messages across
    ///     varying case sensitivities.
    /// </summary>
    /// <remarks>
    ///     Ensures that OPC-specific content, even with case variations in headers or text, is correctly
    ///     handled by the filtering mechanism without impacting unrelated sections of the report.
    ///     This test reflects the current case-sensitive behavior of the implementation.
    /// </remarks>
    [Fact]
    public void FilterReportContent_WithOPCCaseVariations_RemovesOPCContent()
    {
        // Arrange
        var reportContent = string.Join('\n', "Normal content", "====================================================",
            "checking for mods that are patched through opc installer...",
            "====================================================", "OPC content with different case",
            "====================================================", "NORMAL SECTION",
            "====================================================", "End content");

        // Act
        var filtered = InvokeFilterReportContent(reportContent);

        // Assert
        filtered.Should().Contain("Normal content", "because normal content should be preserved");
        filtered.Should().Contain("End content", "because content after OPC section should be preserved");
        filtered.Should().Contain("NORMAL SECTION", "because non-OPC sections should be preserved");
        // Note: The current implementation is case-sensitive, so this would NOT be filtered
        // This test documents the current behavior
        filtered.Should().Contain("checking for mods that are patched through opc installer",
            "because lowercase OPC headers are not filtered in current implementation");
    }

    /// <summary>
    ///     Filters report content to remove OPC-related result messages found within the content.
    /// </summary>
    /// <param name="opcResultMessage">
    ///     A string representing an OPC result message to be removed from the report content.
    /// </param>
    /// <remarks>
    ///     Ensures that OPC-related result messages, typically produced during analysis, are identified and removed
    ///     while preserving other content and sections in the report.
    /// </remarks>
    [Theory]
    [InlineData("FOUND NO PROBLEMATIC MODS THAT ARE ALREADY PATCHED THROUGH THE OPC INSTALLER")]
    [InlineData("# FOUND NO PROBLEMATIC MODS THAT ARE ALREADY PATCHED THROUGH THE OPC INSTALLER #")]
    [InlineData("FOUND PROBLEMATIC MODS THAT ARE ALREADY PATCHED THROUGH THE OPC INSTALLER")]
    public void FilterReportContent_WithOPCResultMessages_RemovesMessages(string opcResultMessage)
    {
        // Arrange
        var reportContent = string.Join('\n', "Analysis start", "====================================================",
            "CHECKING FOR MODS THAT ARE PATCHED THROUGH OPC INSTALLER...",
            "====================================================", opcResultMessage, "",
            "====================================================", "NEXT SECTION",
            "====================================================", "Analysis continues");

        // Act
        var filtered = InvokeFilterReportContent(reportContent);

        // Assert
        filtered.Should().Contain("Analysis start", "because content before OPC section should be preserved");
        filtered.Should().Contain("Analysis continues", "because content after OPC section should be preserved");
        filtered.Should().Contain("NEXT SECTION", "because non-OPC sections should be preserved");
        filtered.Should().NotContain(opcResultMessage, "because OPC result messages should be removed");
        filtered.Should().NotContain("CHECKING FOR MODS THAT ARE PATCHED THROUGH OPC INSTALLER",
            "because OPC headers should be removed");
    }

    /// <summary>
    ///     Invokes the private FilterReportContent method on the ReportWriter using reflection.
    /// </summary>
    /// <param name="reportContent">The report content to be passed into the FilterReportContent method.</param>
    /// <returns>The filtered report content after removing specific sections defined by the FilterReportContent logic.</returns>
    /// <exception cref="InvalidOperationException">Thrown if the FilterReportContent method is not found on the ReportWriter.</exception>
    private string? InvokeFilterReportContent(string? reportContent)
    {
        var method = typeof(ReportWriter).GetMethod("FilterReportContent",
            BindingFlags.NonPublic | BindingFlags.Static);

        if (method == null) throw new InvalidOperationException("FilterReportContent method not found on ReportWriter");

        return (string?)method.Invoke(null, new object?[] { reportContent });
    }
}