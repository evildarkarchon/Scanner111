using FluentAssertions;
using FluentAssertions.Execution;
using Scanner111.Core.Reporting;

namespace Scanner111.Test.Infrastructure.Assertions;

/// <summary>
///     Custom assertions for ReportFragment testing.
/// </summary>
public static class ReportFragmentAssertions
{
    /// <summary>
    ///     Asserts that a fragment has the expected basic properties.
    /// </summary>
    public static void ShouldHaveBasicProperties(
        this ReportFragment fragment,
        string expectedTitle,
        FragmentType expectedType,
        int? expectedPriority = null)
    {
        using (new AssertionScope())
        {
            fragment.Should().NotBeNull();
            fragment.Title.Should().Be(expectedTitle);
            fragment.Type.Should().Be(expectedType);
            
            if (expectedPriority.HasValue)
            {
                fragment.Priority.Should().Be(expectedPriority.Value);
            }
        }
    }

    /// <summary>
    ///     Asserts that a fragment contains expected content patterns.
    /// </summary>
    public static void ShouldContainContent(
        this ReportFragment fragment,
        params string[] expectedPatterns)
    {
        fragment.Should().NotBeNull();
        fragment.Content.Should().NotBeNullOrEmpty();
        
        foreach (var pattern in expectedPatterns)
        {
            fragment.Content.Should().Contain(pattern,
                $"Fragment content should contain '{pattern}'");
        }
    }

    /// <summary>
    ///     Asserts that a fragment does not contain certain content.
    /// </summary>
    public static void ShouldNotContainContent(
        this ReportFragment fragment,
        params string[] unexpectedPatterns)
    {
        fragment.Should().NotBeNull();
        
        if (!string.IsNullOrEmpty(fragment.Content))
        {
            foreach (var pattern in unexpectedPatterns)
            {
                fragment.Content.Should().NotContain(pattern,
                    $"Fragment content should not contain '{pattern}'");
            }
        }
    }

    /// <summary>
    ///     Asserts that a fragment has specific child fragments.
    /// </summary>
    public static void ShouldHaveChildren(
        this ReportFragment fragment,
        int expectedCount,
        Action<IList<ReportFragment>>? childrenAssertion = null)
    {
        using (new AssertionScope())
        {
            fragment.Should().NotBeNull();
            fragment.Children.Should().NotBeNull();
            fragment.Children.Should().HaveCount(expectedCount);
            
            childrenAssertion?.Invoke(fragment.Children);
        }
    }

    /// <summary>
    ///     Asserts that a fragment has a child with a specific title.
    /// </summary>
    public static ReportFragment ShouldHaveChildWithTitle(
        this ReportFragment fragment,
        string expectedTitle)
    {
        fragment.Should().NotBeNull();
        fragment.Children.Should().NotBeNullOrEmpty();
        
        var child = fragment.Children.FirstOrDefault(c => c.Title == expectedTitle);
        child.Should().NotBeNull($"Expected to find child fragment with title '{expectedTitle}'");
        
        return child!;
    }

    /// <summary>
    ///     Asserts that a fragment is a warning with specific content.
    /// </summary>
    public static void ShouldBeWarning(
        this ReportFragment fragment,
        string? expectedWarningPattern = null)
    {
        using (new AssertionScope())
        {
            fragment.Should().NotBeNull();
            fragment.Type.Should().Be(FragmentType.Warning);
            
            if (expectedWarningPattern != null)
            {
                fragment.Content.Should().Contain(expectedWarningPattern);
            }
            
            // Common warning indicators
            fragment.Content?.ToUpper().Should().MatchAny(
                content => content.Contains("CAUTION"),
                content => content.Contains("WARNING"),
                content => content.Contains("ATTENTION"));
        }
    }

    /// <summary>
    ///     Asserts that a fragment is an error with specific content.
    /// </summary>
    public static void ShouldBeError(
        this ReportFragment fragment,
        string? expectedErrorPattern = null)
    {
        using (new AssertionScope())
        {
            fragment.Should().NotBeNull();
            fragment.Type.Should().Be(FragmentType.Error);
            
            if (expectedErrorPattern != null)
            {
                fragment.Content.Should().Contain(expectedErrorPattern);
            }
            
            // Common error indicators
            fragment.Content?.ToUpper().Should().MatchAny(
                content => content.Contains("ERROR"),
                content => content.Contains("FAILED"),
                content => content.Contains("CRITICAL"));
        }
    }

    /// <summary>
    ///     Asserts that a fragment indicates success.
    /// </summary>
    public static void ShouldIndicateSuccess(this ReportFragment fragment)
    {
        using (new AssertionScope())
        {
            fragment.Should().NotBeNull();
            fragment.Type.Should().BeOneOf(FragmentType.Info, FragmentType.Success);
            
            // Common success indicators
            if (!string.IsNullOrEmpty(fragment.Content))
            {
                fragment.Content.Should().MatchAny(
                    content => content.Contains("✔"),
                    content => content.Contains("✓"),
                    content => content.Contains("correctly"),
                    content => content.Contains("successfully"),
                    content => content.Contains("OK", StringComparison.OrdinalIgnoreCase));
            }
        }
    }

    /// <summary>
    ///     Asserts fragment metadata contains expected values.
    /// </summary>
    public static void ShouldHaveMetadata(
        this ReportFragment fragment,
        string key,
        object expectedValue)
    {
        fragment.Should().NotBeNull();
        fragment.Metadata.Should().NotBeNull();
        fragment.Metadata.Should().ContainKey(key);
        fragment.Metadata![key].Should().Be(expectedValue);
    }

    private static bool MatchAny(this string content, params Func<string, bool>[] predicates)
    {
        return predicates.Any(p => p(content));
    }
}