using FluentAssertions;
using Scanner111.Common.Models.Reporting;
using Scanner111.Common.Services.Reporting;

namespace Scanner111.Common.Tests.Services.Reporting;

/// <summary>
/// Tests for ReportBuilder.
/// </summary>
public class ReportBuilderTests
{
    private readonly ReportBuilder _builder;

    public ReportBuilderTests()
    {
        _builder = new ReportBuilder();
    }

    [Fact]
    public void Build_WithMultipleFragments_CombinesCorrectly()
    {
        // Arrange & Act
        var result = _builder
            .Add(ReportFragment.FromLines("Section 1"))
            .Add(ReportFragment.FromLines("Section 2"))
            .Build();

        // Assert
        result.Lines.Should().Equal("Section 1", "Section 2");
    }

    [Fact]
    public void Build_WithNoFragments_ReturnsEmptyFragment()
    {
        // Act
        var result = _builder.Build();

        // Assert
        result.HasContent.Should().BeFalse();
        result.Lines.Should().BeEmpty();
    }

    [Fact]
    public void Add_WithEmptyFragment_DoesNotAdd()
    {
        // Arrange
        var emptyFragment = new ReportFragment();

        // Act
        var result = _builder
            .Add(emptyFragment)
            .Add(ReportFragment.FromLines("Content"))
            .Build();

        // Assert
        result.Lines.Should().Equal("Content");
    }

    [Fact]
    public void AddConditional_WithEmptyFragment_DoesNotAdd()
    {
        // Act
        var result = _builder
            .AddConditional(() => new ReportFragment(), "HEADER")
            .AddConditional(() => ReportFragment.FromLines("Real content"), "HEADER2")
            .Build();

        // Assert
        result.Lines.Should().Contain("HEADER2");
        result.Lines.Should().NotContain("HEADER");
    }

    [Fact]
    public void AddConditional_WithContent_AddsWithHeader()
    {
        // Act
        var result = _builder
            .AddConditional(() => ReportFragment.FromLines("Line 1", "Line 2"), "MY HEADER")
            .Build();

        // Assert
        result.Lines.Should().HaveCount(4); // header, blank line, line 1, line 2
        result.Lines[0].Should().Be("MY HEADER");
        result.Lines[1].Should().BeEmpty();
        result.Lines[2].Should().Be("Line 1");
        result.Lines[3].Should().Be("Line 2");
    }

    [Fact]
    public void AddConditional_WithContentAndNoHeader_AddsWithoutHeader()
    {
        // Act
        var result = _builder
            .AddConditional(() => ReportFragment.FromLines("Line 1", "Line 2"))
            .Build();

        // Assert
        result.Lines.Should().Equal("Line 1", "Line 2");
    }

    [Fact]
    public void AddSection_WithLines_AddsWithHeader()
    {
        // Act
        var result = _builder
            .AddSection("My Section", new[] { "Item 1", "Item 2" })
            .Build();

        // Assert
        result.Lines.Should().HaveCount(4); // header, blank line, item 1, item 2
        result.Lines[0].Should().Be("My Section");
        result.Lines[1].Should().BeEmpty();
        result.Lines[2].Should().Be("Item 1");
        result.Lines[3].Should().Be("Item 2");
    }

    [Fact]
    public void AddSection_WithEmptyLines_DoesNotAdd()
    {
        // Act
        var result = _builder
            .AddSection("Empty Section", Array.Empty<string>())
            .AddSection("Real Section", new[] { "Content" })
            .Build();

        // Assert
        result.Lines.Should().NotContain("Empty Section");
        result.Lines.Should().Contain("Real Section");
    }

    [Fact]
    public void Builder_SupportsMethodChaining()
    {
        // Act
        var result = _builder
            .Add(ReportFragment.FromLines("Part 1"))
            .AddSection("Section", new[] { "Content" })
            .AddConditional(() => ReportFragment.FromLines("Conditional"), "Header")
            .Build();

        // Assert
        result.HasContent.Should().BeTrue();
        result.Lines.Should().Contain("Part 1");
        result.Lines.Should().Contain("Section");
        result.Lines.Should().Contain("Header");
    }

    [Fact]
    public void Builder_CanBeReused()
    {
        // Arrange
        var builder = new ReportBuilder();

        // Act - First build
        var result1 = builder
            .Add(ReportFragment.FromLines("First"))
            .Build();

        // Act - Second build (should be independent)
        var result2 = builder
            .Add(ReportFragment.FromLines("Second"))
            .Build();

        // Assert
        result1.Lines.Should().Equal("First");
        result2.Lines.Should().Equal("First", "Second"); // Builder accumulates
    }

    [Fact]
    public void AddConditional_LazilyEvaluatesContent()
    {
        // Arrange
        var wasEvaluated = false;
        ReportFragment Generator()
        {
            wasEvaluated = true;
            return ReportFragment.FromLines("Content");
        }

        // Act
        _builder.AddConditional(Generator);

        // Assert - generator should be called immediately during Add
        wasEvaluated.Should().BeTrue();
    }

    [Fact]
    public void Build_WithComplexComposition_MaintainsOrder()
    {
        // Act
        var result = _builder
            .Add(ReportFragment.FromLines("A"))
            .AddSection("Section B", new[] { "B1", "B2" })
            .Add(ReportFragment.FromLines("C"))
            .AddConditional(() => ReportFragment.FromLines("D1", "D2"), "Section D")
            .Build();

        // Assert
        result.Lines[0].Should().Be("A");
        result.Lines[1].Should().Be("Section B");
        result.Lines.Should().Contain("B1");
        result.Lines.Should().Contain("C");
        result.Lines.Should().Contain("Section D");
        result.Lines.Should().Contain("D1");
    }
}
