using FluentAssertions;
using Scanner111.Core.Reporting;

namespace Scanner111.Test.Reporting;

[Trait("Category", "Unit")]
[Trait("Performance", "Fast")]
[Trait("Component", "Reporting")]
public class ReportFragmentCompositionTests
{
    #region Operator Overloading Tests

    [Fact]
    public void AddOperator_CombinesFragments_PreservesOrder()
    {
        // Arrange
        var fragment1 = ReportFragment.CreateSection("First", "First Content", 1);
        var fragment2 = ReportFragment.CreateSection("Second", "Second Content", 2);

        // Act
        var combined = fragment1 + fragment2;

        // Assert
        combined.Should().NotBeNull();
        combined.Type.Should().Be(FragmentType.Container);
        combined.Children.Should().HaveCount(2);
        combined.Children[0].Title.Should().Be("First");
        combined.Children[1].Title.Should().Be("Second");
    }

    [Fact]
    public void AddOperator_WithEmptyFragment_ReturnsNonEmptyFragment()
    {
        // Arrange
        var fragment = ReportFragment.CreateSection("Content", "Some content");
        var empty = ReportFragment.Empty();

        // Act
        var result1 = fragment + empty;
        var result2 = empty + fragment;

        // Assert
        result1.Should().BeSameAs(fragment);
        result2.Should().BeSameAs(fragment);
    }

    [Fact]
    public void AddOperator_BothEmpty_ReturnsEmpty()
    {
        // Arrange
        var empty1 = ReportFragment.Empty();
        var empty2 = ReportFragment.Empty();

        // Act
        var result = empty1 + empty2;

        // Assert
        result.Should().NotBeNull();
        result.HasContent().Should().BeFalse();
    }

    [Fact]
    public void AddOperator_MultipleFragments_ChainsCorrectly()
    {
        // Arrange
        var fragment1 = ReportFragment.CreateInfo("Info", "Info content");
        var fragment2 = ReportFragment.CreateWarning("Warning", "Warning content");
        var fragment3 = ReportFragment.CreateError("Error", "Error content");

        // Act
        var combined = fragment1 + fragment2 + fragment3;

        // Assert
        combined.Children.Should().HaveCount(3);
        combined.Children[0].Type.Should().Be(FragmentType.Info);
        combined.Children[1].Type.Should().Be(FragmentType.Warning);
        combined.Children[2].Type.Should().Be(FragmentType.Error);
    }

    [Fact]
    public void AddOperator_WithContainers_FlattensHierarchy()
    {
        // Arrange
        var child1 = ReportFragment.CreateInfo("Child1", "Content1");
        var child2 = ReportFragment.CreateInfo("Child2", "Content2");
        var container1 = ReportFragment.CreateWithChildren("Container1", new[] { child1 });
        var container2 = ReportFragment.CreateWithChildren("Container2", new[] { child2 });

        // Act
        var combined = container1 + container2;

        // Assert
        combined.Type.Should().Be(FragmentType.Container);
        combined.Children.Should().HaveCount(2);
        combined.Children[0].Should().BeSameAs(container1);
        combined.Children[1].Should().BeSameAs(container2);
    }

    #endregion

    #region Composition Method Tests

    [Fact]
    public void Compose_MultipleFragments_MaintainsHierarchy()
    {
        // Arrange
        var fragments = new[]
        {
            ReportFragment.CreateHeader("Header"),
            ReportFragment.CreateSection("Section 1", "Content 1"),
            ReportFragment.CreateSection("Section 2", "Content 2"),
            ReportFragment.CreateInfo("Info", "Info content")
        };

        // Act
        var composed = ReportFragment.Compose(fragments);

        // Assert
        composed.Should().NotBeNull();
        composed.Children.Should().HaveCount(4);
        composed.Children[0].Type.Should().Be(FragmentType.Header);
        composed.Children[3].Type.Should().Be(FragmentType.Info);
    }

    [Fact]
    public void Compose_WithNullFragments_FiltersNulls()
    {
        // Arrange
        var fragments = new ReportFragment?[]
        {
            ReportFragment.CreateHeader("Header"),
            null,
            ReportFragment.CreateSection("Section", "Content"),
            null
        };

        // Act
        var composed = ReportFragment.Compose(fragments);

        // Assert
        composed.Children.Should().HaveCount(2);
        composed.Children.Should().NotContainNulls();
    }

    [Fact]
    public void Compose_EmptyArray_ReturnsEmptyFragment()
    {
        // Act
        var composed = ReportFragment.Compose(Array.Empty<ReportFragment>());

        // Assert
        composed.HasContent().Should().BeFalse();
    }

    #endregion

    #region Conditional Section Tests

    [Fact]
    public void ConditionalSection_WithContent_AddsHeader()
    {
        // Arrange
        Func<ReportFragment> contentGenerator = () =>
            ReportFragment.CreateSection("Section", "Has content");
        Func<string> headerGenerator = () => "Conditional Header";

        // Act
        var result = ReportFragment.ConditionalSection(contentGenerator, headerGenerator);

        // Assert
        result.Should().NotBeNull();
        result.Title.Should().Be("Conditional Header");
        result.Children.Should().HaveCount(1);
        result.Children[0].Title.Should().Be("Section");
    }

    [Fact]
    public void ConditionalSection_WithoutContent_OmitsHeader()
    {
        // Arrange
        Func<ReportFragment> contentGenerator = () => ReportFragment.Empty();
        Func<string> headerGenerator = () => "Should Not Appear";

        // Act
        var result = ReportFragment.ConditionalSection(contentGenerator, headerGenerator);

        // Assert
        result.HasContent().Should().BeFalse();
        result.Title.Should().NotBe("Should Not Appear");
    }

    [Fact]
    public void ConditionalSection_WithMultipleFragments_AppliesHeaderOnlyIfAnyHasContent()
    {
        // Arrange
        var fragments = new[]
        {
            ReportFragment.Empty(),
            ReportFragment.CreateInfo("Has Content", "Some content"),
            ReportFragment.Empty()
        };

        // Act
        var result = ReportFragment.ConditionalSection(
            () => ReportFragment.Compose(fragments),
            () => "Found Content Header");

        // Assert
        result.HasContent().Should().BeTrue();
        result.Title.Should().Be("Found Content Header");
    }

    #endregion

    #region WithHeader Method Tests

    [Fact]
    public void WithHeader_OnlyAddsIfHasContent()
    {
        // Arrange
        var fragmentWithContent = ReportFragment.CreateSection("Section", "Content");
        var emptyFragment = ReportFragment.Empty();

        // Act
        var withHeaderContent = fragmentWithContent.WithHeader("Added Header");
        var withHeaderEmpty = emptyFragment.WithHeader("Should Not Add");

        // Assert
        withHeaderContent.Title.Should().Be("Added Header");
        withHeaderContent.Children.Should().HaveCount(1);
        withHeaderContent.Children[0].Title.Should().Be("Section");

        withHeaderEmpty.HasContent().Should().BeFalse();
    }

    [Fact]
    public void WithHeader_PreservesOriginalFragment()
    {
        // Arrange
        var original = ReportFragment.CreateSection("Original", "Content");

        // Act
        var withHeader = original.WithHeader("New Header");

        // Assert
        // Original should be unchanged (immutability)
        original.Title.Should().Be("Original");
        original.Children.Should().BeEmpty();

        // New fragment should have header
        withHeader.Title.Should().Be("New Header");
        withHeader.Children.Should().HaveCount(1);
    }

    [Fact]
    public void WithHeader_CanChainMultipleTimes()
    {
        // Arrange
        var fragment = ReportFragment.CreateSection("Content", "Some content");

        // Act
        var result = fragment
            .WithHeader("Level 2")
            .WithHeader("Level 1");

        // Assert
        result.Title.Should().Be("Level 1");
        result.Children.Should().HaveCount(1);
        result.Children[0].Title.Should().Be("Level 2");
        result.Children[0].Children.Should().HaveCount(1);
        result.Children[0].Children[0].Title.Should().Be("Content");
    }

    #endregion

    #region HasContent Extension Tests

    [Fact]
    public void HasContent_WithContent_ReturnsTrue()
    {
        // Arrange
        var fragments = new[]
        {
            ReportFragment.CreateSection("Test", "Content"),
            ReportFragment.CreateWarning("Warning", "Warning message"),
            ReportFragment.CreateHeader("Header") // Header with no content
        };

        // Act & Assert
        fragments[0].HasContent().Should().BeTrue();
        fragments[1].HasContent().Should().BeTrue();
        fragments[2].HasContent().Should().BeFalse(); // No content, just title
    }

    [Fact]
    public void HasContent_WithOnlyChildren_ReturnsTrue()
    {
        // Arrange
        var child = ReportFragment.CreateInfo("Child", "Child content");
        var parent = ReportFragment.CreateWithChildren("Parent", new[] { child });

        // Act & Assert
        parent.HasContent().Should().BeTrue(); // Has children with content
    }

    [Fact]
    public void HasContent_EmptyFragment_ReturnsFalse()
    {
        // Arrange
        var empty = ReportFragment.Empty();

        // Act & Assert
        empty.HasContent().Should().BeFalse();
    }

    #endregion

    #region Complex Composition Scenarios

    [Fact]
    public void ComplexComposition_BuildsExpectedStructure()
    {
        // Arrange
        var header = ReportFragment.CreateHeader("Analysis Report");
        var errorSection = ReportFragment.CreateError("Critical Error", "System failure detected");
        var warningSection = ReportFragment.CreateWarning("Performance Warning", "High memory usage");
        
        var detailsChildren = new[]
        {
            ReportFragment.CreateInfo("Memory", "8GB used"),
            ReportFragment.CreateInfo("CPU", "95% usage")
        };
        var detailsSection = ReportFragment.CreateWithChildren("System Details", detailsChildren);

        // Act
        var report = header + errorSection + warningSection + detailsSection;

        // Assert
        report.Children.Should().HaveCount(4);
        report.Children[0].Type.Should().Be(FragmentType.Header);
        report.Children[1].Type.Should().Be(FragmentType.Error);
        report.Children[2].Type.Should().Be(FragmentType.Warning);
        report.Children[3].Type.Should().Be(FragmentType.Container);
        report.Children[3].Children.Should().HaveCount(2);
    }

    [Fact]
    public void ConditionalComposition_OnlyIncludesRelevantSections()
    {
        // Arrange
        bool hasErrors = true;
        bool hasWarnings = false;
        
        var errorFragment = hasErrors 
            ? ReportFragment.CreateError("Error", "Error found") 
            : ReportFragment.Empty();
        
        var warningFragment = hasWarnings 
            ? ReportFragment.CreateWarning("Warning", "Warning found") 
            : ReportFragment.Empty();

        var infoFragment = ReportFragment.CreateInfo("Info", "Always included");

        // Act
        var report = ReportFragment.Compose(new[] { errorFragment, warningFragment, infoFragment });

        // Assert
        report.Children.Where(c => c.HasContent()).Should().HaveCount(2);
        report.Children.Should().Contain(c => c.Type == FragmentType.Error);
        report.Children.Should().Contain(c => c.Type == FragmentType.Info);
    }

    #endregion

    #region Thread Safety for Composition

    [Fact]
    public async Task Composition_ConcurrentOperations_IsThreadSafe()
    {
        // Arrange
        var baseFragments = Enumerable.Range(0, 10)
            .Select(i => ReportFragment.CreateInfo($"Fragment {i}", $"Content {i}"))
            .ToArray();

        var tasks = new List<Task<ReportFragment>>();

        // Act
        for (int i = 0; i < 50; i++)
        {
            var taskIndex = i;
            tasks.Add(Task.Run(() =>
            {
                var fragment1 = baseFragments[taskIndex % 10];
                var fragment2 = baseFragments[(taskIndex + 1) % 10];
                return fragment1 + fragment2;
            }));
        }

        var results = await Task.WhenAll(tasks);

        // Assert
        results.Should().HaveCount(50);
        results.Should().OnlyContain(r => r != null);
        results.Should().OnlyContain(r => r.Children.Count == 2);
    }

    #endregion
}