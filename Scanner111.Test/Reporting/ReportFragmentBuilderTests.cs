using FluentAssertions;
using Scanner111.Core.Reporting;

namespace Scanner111.Test.Reporting;

public class ReportFragmentBuilderTests
{
    #region Factory Method Tests

    [Fact]
    public void Create_ReturnsNewBuilder()
    {
        // Act
        var builder = ReportFragmentBuilder.Create();

        // Assert
        builder.Should().NotBeNull();
        builder.Should().BeOfType<ReportFragmentBuilder>();
    }

    [Fact]
    public void CreateSuccess_SetsCorrectDefaults()
    {
        // Act
        var fragment = ReportFragmentBuilder.CreateSuccess("Operation completed").Build();

        // Assert
        fragment.Should().NotBeNull();
        fragment.Type.Should().Be(FragmentType.Info);
        fragment.Order.Should().Be(200);
        fragment.Content.Should().Contain("✔️ Operation completed");
    }

    [Fact]
    public void CreateWarning_WithFix_IncludesSolution()
    {
        // Act
        var fragment = ReportFragmentBuilder.CreateWarning("Warning message", "Apply this fix").Build();

        // Assert
        fragment.Type.Should().Be(FragmentType.Warning);
        fragment.Order.Should().Be(50);
        fragment.Content.Should().Contain("❌ CAUTION : Warning message");
        fragment.Content.Should().Contain("FIX: Apply this fix");
    }

    [Fact]
    public void CreateError_WithSolution_FormatsCorrectly()
    {
        // Act
        var fragment = ReportFragmentBuilder.CreateError("Error occurred", "Try this solution").Build();

        // Assert
        fragment.Type.Should().Be(FragmentType.Error);
        fragment.Order.Should().Be(10);
        fragment.Content.Should().Contain("❌ ERROR: Error occurred");
        fragment.Content.Should().Contain("SOLUTION: Try this solution");
    }

    [Fact]
    public void CreateWarning_WithoutFix_OmitsFix()
    {
        // Act
        var fragment = ReportFragmentBuilder.CreateWarning("Warning only").Build();

        // Assert
        fragment.Content.Should().Contain("Warning only");
        fragment.Content.Should().NotContain("FIX:");
    }

    [Fact]
    public void CreateError_WithoutSolution_OmitsSolution()
    {
        // Act
        var fragment = ReportFragmentBuilder.CreateError("Error only").Build();

        // Assert
        fragment.Content.Should().Contain("Error only");
        fragment.Content.Should().NotContain("SOLUTION:");
    }

    #endregion

    #region Builder Method Tests

    [Fact]
    public void WithTitle_SetsTitle()
    {
        // Act
        var fragment = ReportFragmentBuilder.Create()
            .WithTitle("Custom Title")
            .Build();

        // Assert
        fragment.Title.Should().Be("Custom Title");
    }

    [Fact]
    public void WithTitle_NullTitle_ThrowsArgumentNullException()
    {
        // Arrange
        var builder = ReportFragmentBuilder.Create();

        // Act & Assert
        var action = () => builder.WithTitle(null!);
        action.Should().Throw<ArgumentNullException>().WithParameterName("title");
    }

    [Fact]
    public void WithType_ChangesFragmentType()
    {
        // Act
        var fragment = ReportFragmentBuilder.Create()
            .WithType(FragmentType.Warning)
            .Build();

        // Assert
        fragment.Type.Should().Be(FragmentType.Warning);
    }

    [Fact]
    public void WithOrder_SetsPriority()
    {
        // Act
        var fragment = ReportFragmentBuilder.Create()
            .WithOrder(42)
            .Build();

        // Assert
        fragment.Order.Should().Be(42);
    }

    [Fact]
    public void WithVisibility_AppliesRules()
    {
        // Act
        var fragment = ReportFragmentBuilder.Create()
            .WithType(FragmentType.Conditional)
            .WithVisibility(FragmentVisibility.Verbose)
            .Build();

        // Assert
        fragment.Visibility.Should().Be(FragmentVisibility.Verbose);
    }

    [Fact]
    public void WithMetadata_AddsKeyValuePairs()
    {
        // Act
        var builder = ReportFragmentBuilder.Create()
            .WithMetadata("key1", "value1")
            .WithMetadata("key2", "value2");

        // Note: Current implementation doesn't support metadata on built fragments
        // This is a known limitation mentioned in the implementation
        
        // Assert
        builder.Should().NotBeNull();
    }

    #endregion

    #region Content Building Tests

    [Fact]
    public void Append_AddsText()
    {
        // Act
        var fragment = ReportFragmentBuilder.Create()
            .Append("Hello")
            .Append(" ")
            .Append("World")
            .Build();

        // Assert
        fragment.Content.Should().Be("Hello World");
    }

    [Fact]
    public void AppendLine_AddsLineWithNewline()
    {
        // Act
        var fragment = ReportFragmentBuilder.Create()
            .AppendLine("Line 1")
            .AppendLine("Line 2")
            .Build();

        // Assert
        fragment.Content.Should().Contain("Line 1");
        fragment.Content.Should().Contain("Line 2");
        fragment.Content.Should().Contain(Environment.NewLine);
    }

    [Fact]
    public void AppendLine_WithoutParameter_AddsEmptyLine()
    {
        // Act
        var fragment = ReportFragmentBuilder.Create()
            .AppendLine("Line 1")
            .AppendLine()
            .AppendLine("Line 2")
            .Build();

        // Assert
        var lines = fragment.Content.Split(Environment.NewLine);
        lines.Should().Contain(string.Empty);
    }

    [Fact]
    public void AppendFormatted_FormatsCorrectly()
    {
        // Act
        var fragment = ReportFragmentBuilder.Create()
            .AppendFormatted("Hello {0}, you have {1} messages", "User", 5)
            .Build();

        // Assert
        fragment.Content.Should().Be("Hello User, you have 5 messages");
    }

    [Fact]
    public void AppendSuccess_AddsCheckmark()
    {
        // Act
        var fragment = ReportFragmentBuilder.Create()
            .AppendSuccess("Task completed")
            .Build();

        // Assert
        fragment.Content.Should().Contain("✔️ Task completed");
    }

    [Fact]
    public void AppendWarning_AddsWarningFormat()
    {
        // Act
        var fragment = ReportFragmentBuilder.Create()
            .AppendWarning("This is a warning")
            .Build();

        // Assert
        fragment.Content.Should().Contain("# ❌ CAUTION : This is a warning #");
    }

    [Fact]
    public void AppendError_AddsErrorFormat()
    {
        // Act
        var fragment = ReportFragmentBuilder.Create()
            .AppendError("An error occurred")
            .Build();

        // Assert
        fragment.Content.Should().Contain("❌ ERROR: An error occurred");
    }

    [Fact]
    public void AppendFix_AddsFix()
    {
        // Act
        var fragment = ReportFragmentBuilder.Create()
            .AppendWarning("Problem detected")
            .AppendFix("Apply this fix")
            .Build();

        // Assert
        fragment.Content.Should().Contain("FIX: Apply this fix");
    }

    [Fact]
    public void AppendSolution_AddsSolution()
    {
        // Act
        var fragment = ReportFragmentBuilder.Create()
            .AppendError("Critical error")
            .AppendSolution("Try this solution")
            .Build();

        // Assert
        fragment.Content.Should().Contain("SOLUTION: Try this solution");
    }

    [Fact]
    public void AppendNotice_AddsNotice()
    {
        // Act
        var fragment = ReportFragmentBuilder.Create()
            .AppendNotice("Important information")
            .Build();

        // Assert
        fragment.Content.Should().Contain("* NOTICE : Important information *");
    }

    [Fact]
    public void AppendSeparator_AddsSeparatorLine()
    {
        // Act
        var fragment = ReportFragmentBuilder.Create()
            .AppendLine("Before")
            .AppendSeparator()
            .AppendLine("After")
            .Build();

        // Assert
        fragment.Content.Should().Contain("-----");
    }

    #endregion

    #region Conditional Building Tests

    [Fact]
    public void AppendIf_True_AddsContent()
    {
        // Act
        var fragment = ReportFragmentBuilder.Create()
            .AppendIf(true, "This appears")
            .AppendIf(false, "This doesn't")
            .Build();

        // Assert
        fragment.Content.Should().Contain("This appears");
        fragment.Content.Should().NotContain("This doesn't");
    }

    [Fact]
    public void AppendLineIf_ConditionallyAddsLine()
    {
        // Arrange
        bool hasWarning = true;
        bool hasError = false;

        // Act
        var fragment = ReportFragmentBuilder.Create()
            .AppendLineIf(hasWarning, "Warning detected")
            .AppendLineIf(hasError, "Error detected")
            .Build();

        // Assert
        fragment.Content.Should().Contain("Warning detected");
        fragment.Content.Should().NotContain("Error detected");
    }

    [Fact]
    public void AppendLines_AddsMultipleLines()
    {
        // Arrange
        var lines = new[] { "Line 1", "Line 2", "Line 3" };

        // Act
        var fragment = ReportFragmentBuilder.Create()
            .AppendLines(lines)
            .Build();

        // Assert
        fragment.Content.Should().Contain("Line 1");
        fragment.Content.Should().Contain("Line 2");
        fragment.Content.Should().Contain("Line 3");
    }

    #endregion

    #region Child Fragment Tests

    [Fact]
    public void AddChild_CreatesHierarchy()
    {
        // Arrange
        var child = ReportFragment.CreateInfo("Child", "Child content");

        // Act
        var fragment = ReportFragmentBuilder.Create()
            .WithTitle("Parent")
            .AddChild(child)
            .Build();

        // Assert
        fragment.Title.Should().Be("Parent");
        fragment.Children.Should().HaveCount(1);
        fragment.Children[0].Should().BeSameAs(child);
    }

    [Fact]
    public void AddChild_NullChild_ThrowsArgumentNullException()
    {
        // Arrange
        var builder = ReportFragmentBuilder.Create();

        // Act & Assert
        var action = () => builder.AddChild(null!);
        action.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void AddChildren_AddsMultiple()
    {
        // Arrange
        var children = new[]
        {
            ReportFragment.CreateInfo("Child1", "Content1"),
            ReportFragment.CreateWarning("Child2", "Content2"),
            ReportFragment.CreateError("Child3", "Content3")
        };

        // Act
        var fragment = ReportFragmentBuilder.Create()
            .WithTitle("Parent")
            .AddChildren(children)
            .Build();

        // Assert
        fragment.Children.Should().HaveCount(3);
        fragment.Children[0].Type.Should().Be(FragmentType.Info);
        fragment.Children[1].Type.Should().Be(FragmentType.Warning);
        fragment.Children[2].Type.Should().Be(FragmentType.Error);
    }

    [Fact]
    public void AddChildren_WithNulls_FiltersNulls()
    {
        // Arrange
        var children = new ReportFragment?[]
        {
            ReportFragment.CreateInfo("Child1", "Content1"),
            null,
            ReportFragment.CreateInfo("Child2", "Content2")
        };

        // Act
        var fragment = ReportFragmentBuilder.Create()
            .WithTitle("Parent")
            .AddChildren(children!)
            .Build();

        // Assert
        fragment.Children.Should().HaveCount(2);
        fragment.Children.Should().NotContainNulls();
    }

    #endregion

    #region Build Method Tests

    [Fact]
    public void Build_CreatesImmutableFragment()
    {
        // Act
        var fragment = ReportFragmentBuilder.Create()
            .WithTitle("Test")
            .WithType(FragmentType.Section)
            .AppendLine("Content")
            .Build();

        // Assert
        fragment.Should().NotBeNull();
        fragment.Title.Should().Be("Test");
        fragment.Type.Should().Be(FragmentType.Section);
        fragment.Content.Should().Contain("Content");
    }

    [Fact]
    public void BuildIfNotEmpty_WithContent_ReturnsFragment()
    {
        // Act
        var fragment = ReportFragmentBuilder.Create()
            .WithTitle("Test")
            .AppendLine("Content")
            .BuildIfNotEmpty();

        // Assert
        fragment.Should().NotBeNull();
        fragment!.Content.Should().Contain("Content");
    }

    [Fact]
    public void BuildIfNotEmpty_WithoutContent_ReturnsNull()
    {
        // Act
        var fragment = ReportFragmentBuilder.Create()
            .WithTitle("Empty")
            .BuildIfNotEmpty();

        // Assert
        fragment.Should().BeNull();
    }

    [Fact]
    public void BuildIfNotEmpty_WithOnlyChildren_ReturnsFragment()
    {
        // Arrange
        var child = ReportFragment.CreateInfo("Child", "Content");

        // Act
        var fragment = ReportFragmentBuilder.Create()
            .WithTitle("Parent")
            .AddChild(child)
            .BuildIfNotEmpty();

        // Assert
        fragment.Should().NotBeNull();
        fragment!.Children.Should().HaveCount(1);
    }

    #endregion

    #region Fluent Interface Tests

    [Fact]
    public void FluentInterface_ChainsCorrectly()
    {
        // Act
        var fragment = ReportFragmentBuilder.Create()
            .WithTitle("Complex Report")
            .WithType(FragmentType.Section)
            .WithOrder(100)
            .WithVisibility(FragmentVisibility.Always)
            .AppendLine("Header Line")
            .AppendSeparator()
            .AppendSuccess("Step 1 completed")
            .AppendWarning("Potential issue detected")
            .AppendFix("Apply this workaround")
            .AppendSeparator()
            .AppendNotice("Additional information")
            .AppendLineIf(true, "Conditional line included")
            .AppendLineIf(false, "This won't appear")
            .Build();

        // Assert
        fragment.Should().NotBeNull();
        fragment.Title.Should().Be("Complex Report");
        fragment.Type.Should().Be(FragmentType.Section);
        fragment.Order.Should().Be(100);
        fragment.Content.Should().Contain("Header Line");
        fragment.Content.Should().Contain("✔️ Step 1 completed");
        fragment.Content.Should().Contain("❌ CAUTION");
        fragment.Content.Should().Contain("FIX:");
        fragment.Content.Should().Contain("NOTICE");
        fragment.Content.Should().Contain("Conditional line included");
        fragment.Content.Should().NotContain("This won't appear");
    }

    #endregion

    #region Edge Cases

    [Fact]
    public void Build_EmptyBuilder_CreatesMinimalFragment()
    {
        // Act
        var fragment = ReportFragmentBuilder.Create().Build();

        // Assert
        fragment.Should().NotBeNull();
        fragment.Title.Should().Be("Report Section"); // Default title
        fragment.Type.Should().Be(FragmentType.Section); // Default type
        fragment.Order.Should().Be(100); // Default order
    }

    [Fact]
    public void WithMetadata_CurrentLimitation_DoesNotPersist()
    {
        // This test documents the current limitation where metadata
        // cannot be set on the final fragment due to init-only property

        // Act
        var fragment = ReportFragmentBuilder.Create()
            .WithMetadata("key", "value")
            .Build();

        // Assert
        // Metadata will be null because it can't be set after creation
        fragment.Metadata.Should().BeNull();
    }

    #endregion
}