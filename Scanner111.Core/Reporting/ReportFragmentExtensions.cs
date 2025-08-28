namespace Scanner111.Core.Reporting;

/// <summary>
///     Extension methods for ReportFragment providing utility functions.
/// </summary>
public static class ReportFragmentExtensions
{
    /// <summary>
    ///     Combines multiple fragments into a single parent fragment.
    ///     Returns null if no fragments have content.
    /// </summary>
    public static ReportFragment? CombineFragments(
        string title,
        IEnumerable<ReportFragment?> fragments,
        int order = 100)
    {
        ArgumentNullException.ThrowIfNull(title);
        ArgumentNullException.ThrowIfNull(fragments);

        var nonEmptyFragments = fragments
            .Where(f => f != null && f.HasContent())
            .ToList();

        if (!nonEmptyFragments.Any())
            return null;

        if (nonEmptyFragments.Count == 1)
        {
            // Single fragment - just wrap it with new title
            var single = nonEmptyFragments[0];
            return ReportFragment.CreateSection(title, single?.Content ?? string.Empty, order);
        }

        // Cast to non-nullable list since we've already filtered out nulls
        var nonNullFragments = nonEmptyFragments.Cast<ReportFragment>().ToList();
        return ReportFragment.CreateWithChildren(title, nonNullFragments, order);
    }

    /// <summary>
    ///     Adds a parent fragment with a title only if this fragment has content.
    /// </summary>
    public static ReportFragment? WithParentIfHasContent(
        this ReportFragment? fragment,
        string parentTitle,
        int? parentOrder = null)
    {
        if (fragment == null || !fragment.HasContent())
            return null;

        return ReportFragment.CreateWithChildren(
            parentTitle,
            new[] { fragment },
            parentOrder ?? fragment.Order);
    }

    /// <summary>
    ///     Converts fragment content to lines for compatibility.
    /// </summary>
    public static IReadOnlyList<string> GetContentLines(this ReportFragment fragment)
    {
        ArgumentNullException.ThrowIfNull(fragment);

        if (string.IsNullOrWhiteSpace(fragment.Content))
            return Array.Empty<string>();

        return fragment.Content.Split(
            new[] { "\r\n", "\r", "\n" },
            StringSplitOptions.None);
    }

    /// <summary>
    ///     Creates a quick success fragment.
    /// </summary>
    public static ReportFragment CreateQuickSuccess(string title, string message, int order = 200)
    {
        return ReportFragmentBuilder
            .Create()
            .WithTitle(title)
            .WithType(FragmentType.Info)
            .WithOrder(order)
            .AppendSuccess(message)
            .Build();
    }

    /// <summary>
    ///     Creates a quick warning fragment.
    /// </summary>
    public static ReportFragment CreateQuickWarning(string title, string warning, string? fix = null, int order = 50)
    {
        var builder = ReportFragmentBuilder
            .Create()
            .WithTitle(title)
            .WithType(FragmentType.Warning)
            .WithOrder(order)
            .AppendWarning(warning);

        if (!string.IsNullOrWhiteSpace(fix))
            builder.AppendFix(fix);

        return builder.Build();
    }

    /// <summary>
    ///     Creates a quick error fragment.
    /// </summary>
    public static ReportFragment CreateQuickError(string title, string error, string? solution = null, int order = 10)
    {
        var builder = ReportFragmentBuilder
            .Create()
            .WithTitle(title)
            .WithType(FragmentType.Error)
            .WithOrder(order)
            .AppendError(error);

        if (!string.IsNullOrWhiteSpace(solution))
            builder.AppendSolution(solution);

        return builder.Build();
    }

    /// <summary>
    ///     Creates a quick notice fragment.
    /// </summary>
    public static ReportFragment CreateQuickNotice(string title, string notice, int order = 150)
    {
        return ReportFragmentBuilder
            .Create()
            .WithTitle(title)
            .WithType(FragmentType.Info)
            .WithOrder(order)
            .AppendNotice(notice)
            .Build();
    }
}