using FluentAssertions;
using Scanner111.Common.Services.Reporting;

namespace Scanner111.Common.Tests.Services.Reporting;

/// <summary>
/// Tests for MarkdownFormatter.
/// </summary>
public class MarkdownFormatterTests
{
    [Fact]
    public void Bold_FormatsTextCorrectly()
    {
        // Act
        var result = MarkdownFormatter.Bold("Important");

        // Assert
        result.Should().Be("**Important**");
    }

    [Fact]
    public void Italic_FormatsTextCorrectly()
    {
        // Act
        var result = MarkdownFormatter.Italic("Emphasized");

        // Assert
        result.Should().Be("*Emphasized*");
    }

    [Fact]
    public void Code_FormatsTextCorrectly()
    {
        // Act
        var result = MarkdownFormatter.Code("variable");

        // Assert
        result.Should().Be("`variable`");
    }

    [Fact]
    public void CodeBlock_WithoutLanguage_FormatsCorrectly()
    {
        // Act
        var result = MarkdownFormatter.CodeBlock("int x = 42;");

        // Assert
        result.Should().Be("```\nint x = 42;\n```");
    }

    [Fact]
    public void CodeBlock_WithLanguage_FormatsCorrectly()
    {
        // Act
        var result = MarkdownFormatter.CodeBlock("int x = 42;", "csharp");

        // Assert
        result.Should().Be("```csharp\nint x = 42;\n```");
    }

    [Fact]
    public void BulletList_WithMultipleItems_FormatsCorrectly()
    {
        // Arrange
        var items = new[] { "Item 1", "Item 2", "Item 3" };

        // Act
        var result = MarkdownFormatter.BulletList(items);

        // Assert
        result.Should().Be("- Item 1\n- Item 2\n- Item 3");
    }

    [Fact]
    public void BulletList_WithEmptyList_ReturnsEmptyString()
    {
        // Arrange
        var items = Array.Empty<string>();

        // Act
        var result = MarkdownFormatter.BulletList(items);

        // Assert
        result.Should().BeEmpty();
    }

    [Fact]
    public void NumberedList_WithMultipleItems_FormatsCorrectly()
    {
        // Arrange
        var items = new[] { "First", "Second", "Third" };

        // Act
        var result = MarkdownFormatter.NumberedList(items);

        // Assert
        result.Should().Be("1. First\n2. Second\n3. Third");
    }

    [Fact]
    public void NumberedList_WithEmptyList_ReturnsEmptyString()
    {
        // Arrange
        var items = Array.Empty<string>();

        // Act
        var result = MarkdownFormatter.NumberedList(items);

        // Assert
        result.Should().BeEmpty();
    }

    [Theory]
    [InlineData("Title", 1, "# Title")]
    [InlineData("Subtitle", 2, "## Subtitle")]
    [InlineData("Section", 3, "### Section")]
    [InlineData("Subsection", 4, "#### Subsection")]
    [InlineData("Minor", 5, "##### Minor")]
    [InlineData("Tiny", 6, "###### Tiny")]
    public void Heading_WithVariousLevels_FormatsCorrectly(string text, int level, string expected)
    {
        // Act
        var result = MarkdownFormatter.Heading(text, level);

        // Assert
        result.Should().Be(expected);
    }

    [Fact]
    public void Heading_WithDefaultLevel_UsesLevel1()
    {
        // Act
        var result = MarkdownFormatter.Heading("Title");

        // Assert
        result.Should().Be("# Title");
    }

    [Theory]
    [InlineData(0, "# Title")]  // Below min should clamp to 1
    [InlineData(7, "###### Title")]  // Above max should clamp to 6
    [InlineData(-1, "# Title")]  // Negative should clamp to 1
    [InlineData(10, "###### Title")]  // Far above max should clamp to 6
    public void Heading_WithOutOfRangeLevels_ClampsToValidRange(int level, string expected)
    {
        // Act
        var result = MarkdownFormatter.Heading("Title", level);

        // Assert
        result.Should().Be(expected);
    }

    [Fact]
    public void Link_FormatsCorrectly()
    {
        // Act
        var result = MarkdownFormatter.Link("Click here", "https://example.com");

        // Assert
        result.Should().Be("[Click here](https://example.com)");
    }

    [Fact]
    public void HorizontalRule_ReturnsCorrectFormat()
    {
        // Act
        var result = MarkdownFormatter.HorizontalRule();

        // Assert
        result.Should().Be("---");
    }

    [Fact]
    public void Blockquote_FormatsCorrectly()
    {
        // Act
        var result = MarkdownFormatter.Blockquote("This is a quote");

        // Assert
        result.Should().Be("> This is a quote");
    }

    [Fact]
    public void Table_WithHeadersAndRows_FormatsCorrectly()
    {
        // Arrange
        var headers = new[] { "Name", "Age", "City" };
        var rows = new[]
        {
            new[] { "Alice", "30", "NYC" }.ToList().AsReadOnly(),
            new[] { "Bob", "25", "LA" }.ToList().AsReadOnly()
        };

        // Act
        var result = MarkdownFormatter.Table(headers, rows);

        // Assert
        result.Should().Contain("| Name | Age | City |");
        result.Should().Contain("| --- | --- | --- |");
        result.Should().Contain("| Alice | 30 | NYC |");
        result.Should().Contain("| Bob | 25 | LA |");
    }

    [Fact]
    public void Table_WithEmptyRows_ContainsOnlyHeadersAndSeparator()
    {
        // Arrange
        var headers = new[] { "Column1", "Column2" };
        var rows = Array.Empty<IReadOnlyList<string>>();

        // Act
        var result = MarkdownFormatter.Table(headers, rows);

        // Assert
        result.Should().Contain("| Column1 | Column2 |");
        result.Should().Contain("| --- | --- |");
        result.Split('\n').Should().HaveCount(2);
    }

    [Fact]
    public void CombinedFormatting_WorksTogether()
    {
        // Act
        var bold = MarkdownFormatter.Bold("Bold text");
        var italic = MarkdownFormatter.Italic("Italic text");
        var combined = $"{bold} and {italic}";

        // Assert
        combined.Should().Be("**Bold text** and *Italic text*");
    }
}
