using System.Text.Json;
using FluentAssertions;
using Scanner111.Core.Reporting;

namespace Scanner111.Test.Reporting;

[Trait("Category", "Unit")]
[Trait("Performance", "Fast")]
[Trait("Component", "Reporting")]
public class ReportFragmentTests
{
    #region Immutability Tests

    [Fact]
    public void ReportFragment_ShouldBeImmutable()
    {
        // Arrange
        var fragment = ReportFragment.CreateSection("Test", "Content");

        // Act & Assert
        fragment.Title.Should().Be("Test");
        fragment.Content.Should().Be("Content");
        fragment.Id.Should().NotBeEmpty();
        fragment.CreatedAt.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(1));

        // Verify children are read-only
        fragment.Children.Should().BeAssignableTo<IReadOnlyList<ReportFragment>>();
    }

    #endregion

    #region Factory Method Tests

    [Fact]
    public void CreateHeader_WithValidTitle_ReturnsCorrectFragment()
    {
        // Act
        var fragment = ReportFragment.CreateHeader("Test Header", 10);

        // Assert
        fragment.Title.Should().Be("Test Header");
        fragment.Content.Should().BeEmpty();
        fragment.Order.Should().Be(10);
        fragment.Type.Should().Be(FragmentType.Header);
        fragment.Visibility.Should().Be(FragmentVisibility.Always);
    }

    [Fact]
    public void CreateSection_WithNullTitle_ThrowsArgumentNullException()
    {
        // Act & Assert
        var action = () => ReportFragment.CreateSection(null!, "Content");
        action.Should().Throw<ArgumentNullException>()
            .WithParameterName("title");
    }

    [Fact]
    public void CreateWarning_WithContent_SetsCorrectTypeAndOrder()
    {
        // Act
        var fragment = ReportFragment.CreateWarning("Warning Title", "Warning Content", 25);

        // Assert
        fragment.Title.Should().Be("Warning Title");
        fragment.Content.Should().Be("Warning Content");
        fragment.Order.Should().Be(25);
        fragment.Type.Should().Be(FragmentType.Warning);
        fragment.Visibility.Should().Be(FragmentVisibility.Always);
    }

    [Fact]
    public void CreateError_WithHighPriority_HasLowerOrderValue()
    {
        // Act
        var errorFragment = ReportFragment.CreateError("Error", "Content", 10);
        var infoFragment = ReportFragment.CreateInfo("Info", "Content", 200);

        // Assert
        errorFragment.Order.Should().BeLessThan(infoFragment.Order);
        errorFragment.Type.Should().Be(FragmentType.Error);
    }

    [Fact]
    public void CreateInfo_WithDefaults_UsesCorrectValues()
    {
        // Act
        var fragment = ReportFragment.CreateInfo("Info Title", "Info Content");

        // Assert
        fragment.Order.Should().Be(200);
        fragment.Type.Should().Be(FragmentType.Info);
        fragment.Visibility.Should().Be(FragmentVisibility.Always);
    }

    [Fact]
    public void CreateConditional_WithVisibility_AppliesCorrectRules()
    {
        // Act
        var fragment = ReportFragment.CreateConditional(
            "Conditional Title",
            "Conditional Content",
            FragmentVisibility.Verbose,
            150);

        // Assert
        fragment.Type.Should().Be(FragmentType.Conditional);
        fragment.Visibility.Should().Be(FragmentVisibility.Verbose);
        fragment.Order.Should().Be(150);
    }

    [Fact]
    public void CreateWithChildren_PreservesHierarchy()
    {
        // Arrange
        var child1 = ReportFragment.CreateInfo("Child 1", "Content 1");
        var child2 = ReportFragment.CreateWarning("Child 2", "Content 2");
        var children = new[] { child1, child2 };

        // Act
        var parent = ReportFragment.CreateWithChildren("Parent", children, 50);

        // Assert
        parent.Title.Should().Be("Parent");
        parent.Content.Should().BeEmpty();
        parent.Type.Should().Be(FragmentType.Container);
        parent.Children.Should().HaveCount(2);
        parent.Children[0].Should().BeSameAs(child1);
        parent.Children[1].Should().BeSameAs(child2);
    }

    [Fact]
    public void CreateWithChildren_WithNullChildren_CreatesEmptyContainer()
    {
        // Act
        var fragment = ReportFragment.CreateWithChildren("Container", null, 100);

        // Assert
        fragment.Children.Should().BeEmpty();
        fragment.Type.Should().Be(FragmentType.Container);
    }

    #endregion

    #region Markdown Generation Tests

    [Fact]
    public void ToMarkdown_WithSimpleSection_GeneratesCorrectMarkdown()
    {
        // Arrange
        var fragment = ReportFragment.CreateSection("Test Section", "This is content");

        // Act
        var markdown = fragment.ToMarkdown();

        // Assert
        markdown.Should().Contain("### Test Section");
        markdown.Should().Contain("This is content");
    }

    [Fact]
    public void ToMarkdown_WithNestedFragments_ProducesCorrectHierarchy()
    {
        // Arrange
        var child1 = ReportFragment.CreateInfo("Child Info", "Info content");
        var child2 = ReportFragment.CreateWarning("Child Warning", "Warning content");
        var parent = ReportFragment.CreateWithChildren("Parent Section", new[] { child1, child2 });

        // Act
        var markdown = parent.ToMarkdown(2);

        // Assert
        markdown.Should().Contain("### Parent Section");
        markdown.Should().Contain("#### ℹ️ Child Info");
        markdown.Should().Contain("#### ⚠️ Child Warning");
        markdown.Should().Contain("Info content");
        markdown.Should().Contain("Warning content");
    }

    [Theory]
    [InlineData(FragmentType.Warning, "⚠️")]
    [InlineData(FragmentType.Error, "❌")]
    [InlineData(FragmentType.Info, "ℹ️")]
    public void ToMarkdown_WithDifferentTypes_IncludesCorrectEmoji(FragmentType type, string expectedEmoji)
    {
        // Arrange
        var fragment = type switch
        {
            FragmentType.Warning => ReportFragment.CreateWarning("Title", "Content"),
            FragmentType.Error => ReportFragment.CreateError("Title", "Content"),
            FragmentType.Info => ReportFragment.CreateInfo("Title", "Content"),
            _ => ReportFragment.CreateSection("Title", "Content")
        };

        // Act
        var markdown = fragment.ToMarkdown();

        // Assert
        if (type != FragmentType.Section)
        {
            markdown.Should().Contain(expectedEmoji);
        }
    }

    [Fact]
    public void ToMarkdown_WithEmptyContent_OnlyIncludesTitle()
    {
        // Arrange
        var fragment = ReportFragment.CreateHeader("Header Only");

        // Act
        var markdown = fragment.ToMarkdown();

        // Assert
        markdown.Should().Contain("## Header Only");
        markdown.Should().NotContain("Content");
    }

    [Fact]
    public void ToMarkdown_WithDeepNesting_RespectsMaxHeaderLevel()
    {
        // Arrange
        var deepChild = ReportFragment.CreateSection("Deep Child", "Content");
        var level5 = ReportFragment.CreateWithChildren("Level 5", new[] { deepChild });
        var level4 = ReportFragment.CreateWithChildren("Level 4", new[] { level5 });
        var level3 = ReportFragment.CreateWithChildren("Level 3", new[] { level4 });
        var level2 = ReportFragment.CreateWithChildren("Level 2", new[] { level3 });
        var level1 = ReportFragment.CreateWithChildren("Level 1", new[] { level2 });

        // Act
        var markdown = level1.ToMarkdown(2);

        // Assert
        markdown.Should().Contain("### Level 1");
        markdown.Should().Contain("#### Level 2");
        markdown.Should().Contain("##### Level 3");
        markdown.Should().Contain("###### Level 4");
        markdown.Should().Contain("###### Level 5"); // Max level is 6
        markdown.Should().Contain("###### Deep Child"); // Still max level 6
    }

    #endregion

    #region Metadata Tests

    [Fact]
    public void Metadata_InitiallyNull()
    {
        // Arrange & Act
        var fragment = ReportFragment.CreateSection("Test", "Content");

        // Assert
        // Note: Current implementation doesn't support setting metadata after creation
        // This is a known limitation of the immutable design
        fragment.Metadata.Should().BeNull();
    }

    #endregion

    #region Thread Safety Tests

    [Fact]
    public async Task ReportFragment_ConcurrentAccess_IsThreadSafe()
    {
        // Arrange
        var fragment = ReportFragment.CreateSection("Shared Fragment", "Shared Content");
        var tasks = new List<Task>();
        var results = new List<string>();
        var lockObj = new object();

        // Act
        for (int i = 0; i < 100; i++)
        {
            tasks.Add(Task.Run(() =>
            {
                var markdown = fragment.ToMarkdown();
                var title = fragment.Title;
                var content = fragment.Content;
                var id = fragment.Id;

                lock (lockObj)
                {
                    results.Add(markdown);
                }

                // Verify immutability during concurrent access
                title.Should().Be("Shared Fragment");
                content.Should().Be("Shared Content");
                id.Should().NotBeEmpty();
            }));
        }

        await Task.WhenAll(tasks);

        // Assert
        results.Should().HaveCount(100);
        results.Should().OnlyContain(r => r.Contains("Shared Fragment"));
        results.Should().OnlyContain(r => r.Contains("Shared Content"));
    }

    [Fact]
    public async Task CreateWithChildren_ConcurrentModification_IsThreadSafe()
    {
        // Arrange
        var children = new List<ReportFragment>();
        var tasks = new List<Task>();

        // Act
        for (int i = 0; i < 50; i++)
        {
            var index = i;
            tasks.Add(Task.Run(() =>
            {
                var child = ReportFragment.CreateInfo($"Child {index}", $"Content {index}");
                lock (children)
                {
                    children.Add(child);
                }
            }));
        }

        await Task.WhenAll(tasks);

        var parent = ReportFragment.CreateWithChildren("Parent", children);

        // Assert
        parent.Children.Should().HaveCount(50);
        parent.Children.Select(c => c.Title).Should().OnlyHaveUniqueItems();
    }

    #endregion

    #region Edge Cases

    [Fact]
    public void CreateSection_WithEmptyTitle_DoesNotThrow()
    {
        // Act
        var fragment = ReportFragment.CreateSection(string.Empty, "Content");

        // Assert
        fragment.Title.Should().BeEmpty();
        fragment.Content.Should().Be("Content");
    }

    [Fact]
    public void ToMarkdown_WithSpecialCharacters_HandlesCorrectly()
    {
        // Arrange
        var fragment = ReportFragment.CreateSection(
            "Title with # and * special chars",
            "Content with **bold** and _italic_ and [links](http://test.com)");

        // Act
        var markdown = fragment.ToMarkdown();

        // Assert
        markdown.Should().Contain("Title with # and * special chars");
        markdown.Should().Contain("Content with **bold** and _italic_ and [links](http://test.com)");
    }

    [Fact]
    public void CreateWithChildren_WithMixedNullAndValidChildren_FiltersNulls()
    {
        // Arrange
        var child1 = ReportFragment.CreateInfo("Child 1", "Content");
        var children = new ReportFragment?[] { child1, null, null };

        // Act
        var parent = ReportFragment.CreateWithChildren("Parent", children!);

        // Assert
        parent.Children.Should().HaveCount(1);
        parent.Children[0].Title.Should().Be("Child 1");
    }

    #endregion
}