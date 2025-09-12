using System.Collections.Immutable;

namespace Scanner111.Core.Reporting;

/// <summary>
/// Provides functional composition methods for report fragments.
/// </summary>
public static class FragmentComposer
{
    /// <summary>
    /// Composes multiple fragments into a single fragment.
    /// </summary>
    /// <param name="fragments">The fragments to compose.</param>
    /// <returns>A single composed fragment containing all input fragments.</returns>
    public static ReportFragment Compose(params ReportFragment[] fragments)
    {
        if (fragments == null || fragments.Length == 0)
            return ReportFragment.Empty();

        // Filter out null and empty fragments
        var validFragments = fragments
            .Where(f => f != null && f.HasContent())
            .ToArray();

        if (validFragments.Length == 0)
            return ReportFragment.Empty();

        if (validFragments.Length == 1)
            return validFragments[0];

        // Create a container with all fragments as children
        return ReportFragment.CreateWithChildren(
            "Composed Report",
            validFragments,
            validFragments.Min(f => f.Order));
    }

    /// <summary>
    /// Creates a conditional section with a header that's only added if the content generator produces content.
    /// </summary>
    /// <param name="headerGenerator">Function to generate the header.</param>
    /// <param name="contentGenerator">Function to generate the content.</param>
    /// <returns>A fragment with header if content exists, otherwise empty.</returns>
    public static ReportFragment ConditionalSection(
        Func<string> headerGenerator,
        Func<ReportFragment> contentGenerator)
    {
        if (headerGenerator == null) throw new ArgumentNullException(nameof(headerGenerator));
        if (contentGenerator == null) throw new ArgumentNullException(nameof(contentGenerator));

        var content = contentGenerator();
        if (content == null || !content.HasContent())
            return ReportFragment.Empty();

        var header = headerGenerator();
        return content.WithHeader(header);
    }

    /// <summary>
    /// Creates a conditional section with multiple content generators.
    /// </summary>
    /// <param name="headerGenerator">Function to generate the header.</param>
    /// <param name="contentGenerators">Multiple content generator functions.</param>
    /// <returns>A fragment with header if any content exists, otherwise empty.</returns>
    public static ReportFragment ConditionalSection(
        Func<string> headerGenerator,
        params Func<ReportFragment>[] contentGenerators)
    {
        if (headerGenerator == null) throw new ArgumentNullException(nameof(headerGenerator));
        if (contentGenerators == null || contentGenerators.Length == 0)
            return ReportFragment.Empty();

        var fragments = contentGenerators
            .Select(g => g?.Invoke())
            .Where(f => f != null && f.HasContent())
            .Cast<ReportFragment>()
            .ToArray();

        if (fragments.Length == 0)
            return ReportFragment.Empty();

        var combinedContent = Compose(fragments);
        return combinedContent.WithHeader(headerGenerator());
    }

    /// <summary>
    /// Conditionally includes a fragment based on a condition.
    /// </summary>
    /// <param name="condition">The condition to evaluate.</param>
    /// <param name="fragmentGenerator">Function to generate the fragment if condition is true.</param>
    /// <returns>The generated fragment if condition is true, otherwise empty.</returns>
    public static ReportFragment When(bool condition, Func<ReportFragment> fragmentGenerator)
    {
        if (!condition || fragmentGenerator == null)
            return ReportFragment.Empty();

        return fragmentGenerator() ?? ReportFragment.Empty();
    }

    /// <summary>
    /// Conditionally includes a fragment based on a predicate.
    /// </summary>
    /// <typeparam name="T">The type of value to evaluate.</typeparam>
    /// <param name="value">The value to evaluate.</param>
    /// <param name="predicate">The predicate to test.</param>
    /// <param name="fragmentGenerator">Function to generate the fragment if predicate returns true.</param>
    /// <returns>The generated fragment if predicate is true, otherwise empty.</returns>
    public static ReportFragment When<T>(T value, Func<T, bool> predicate, Func<T, ReportFragment> fragmentGenerator)
    {
        if (predicate == null || fragmentGenerator == null)
            return ReportFragment.Empty();

        if (!predicate(value))
            return ReportFragment.Empty();

        return fragmentGenerator(value) ?? ReportFragment.Empty();
    }

    /// <summary>
    /// Maps a collection of items to fragments and composes them.
    /// </summary>
    /// <typeparam name="T">The type of items in the collection.</typeparam>
    /// <param name="items">The items to map.</param>
    /// <param name="mapper">Function to map each item to a fragment.</param>
    /// <returns>A composed fragment containing all mapped fragments.</returns>
    public static ReportFragment Map<T>(IEnumerable<T> items, Func<T, ReportFragment> mapper)
    {
        if (items == null || mapper == null)
            return ReportFragment.Empty();

        var fragments = items
            .Select(mapper)
            .Where(f => f != null && f.HasContent())
            .ToArray();

        return Compose(fragments);
    }

    /// <summary>
    /// Maps a collection of items to fragments with an index and composes them.
    /// </summary>
    /// <typeparam name="T">The type of items in the collection.</typeparam>
    /// <param name="items">The items to map.</param>
    /// <param name="mapper">Function to map each item and its index to a fragment.</param>
    /// <returns>A composed fragment containing all mapped fragments.</returns>
    public static ReportFragment MapIndexed<T>(IEnumerable<T> items, Func<T, int, ReportFragment> mapper)
    {
        if (items == null || mapper == null)
            return ReportFragment.Empty();

        var fragments = items
            .Select((item, index) => mapper(item, index))
            .Where(f => f != null && f.HasContent())
            .ToArray();

        return Compose(fragments);
    }

    /// <summary>
    /// Aggregates a collection into a single fragment using a folder function.
    /// </summary>
    /// <typeparam name="T">The type of items in the collection.</typeparam>
    /// <param name="items">The items to aggregate.</param>
    /// <param name="seed">The initial fragment.</param>
    /// <param name="folder">Function to combine the accumulator with each item.</param>
    /// <returns>The aggregated fragment.</returns>
    public static ReportFragment Aggregate<T>(
        IEnumerable<T> items,
        ReportFragment seed,
        Func<ReportFragment, T, ReportFragment> folder)
    {
        if (items == null || folder == null)
            return seed ?? ReportFragment.Empty();

        return items.Aggregate(seed ?? ReportFragment.Empty(), folder);
    }

    /// <summary>
    /// Partitions fragments based on a condition and creates sections for each partition.
    /// </summary>
    /// <param name="fragments">The fragments to partition.</param>
    /// <param name="predicate">The predicate to partition by.</param>
    /// <param name="trueHeader">Header for fragments that match the predicate.</param>
    /// <param name="falseHeader">Header for fragments that don't match the predicate.</param>
    /// <returns>A composed fragment with two sections.</returns>
    public static ReportFragment Partition(
        IEnumerable<ReportFragment> fragments,
        Func<ReportFragment, bool> predicate,
        string trueHeader,
        string falseHeader)
    {
        if (fragments == null || predicate == null)
            return ReportFragment.Empty();

        var fragmentList = fragments.ToList();
        var trueFragments = fragmentList.Where(predicate).ToArray();
        var falseFragments = fragmentList.Where(f => !predicate(f)).ToArray();

        var sections = new List<ReportFragment>();

        if (trueFragments.Any(f => f.HasContent()))
        {
            var trueSection = Compose(trueFragments);
            if (!string.IsNullOrEmpty(trueHeader))
                trueSection = trueSection.WithHeader(trueHeader);
            sections.Add(trueSection);
        }

        if (falseFragments.Any(f => f.HasContent()))
        {
            var falseSection = Compose(falseFragments);
            if (!string.IsNullOrEmpty(falseHeader))
                falseSection = falseSection.WithHeader(falseHeader);
            sections.Add(falseSection);
        }

        return Compose(sections.ToArray());
    }

    /// <summary>
    /// Groups fragments by a key selector and creates sections for each group.
    /// </summary>
    /// <typeparam name="TKey">The type of the grouping key.</typeparam>
    /// <param name="fragments">The fragments to group.</param>
    /// <param name="keySelector">Function to select the grouping key.</param>
    /// <param name="headerGenerator">Function to generate a header for each group.</param>
    /// <returns>A composed fragment with sections for each group.</returns>
    public static ReportFragment GroupBy<TKey>(
        IEnumerable<ReportFragment> fragments,
        Func<ReportFragment, TKey> keySelector,
        Func<TKey, string> headerGenerator)
    {
        if (fragments == null || keySelector == null || headerGenerator == null)
            return ReportFragment.Empty();

        var groups = fragments
            .Where(f => f != null && f.HasContent())
            .GroupBy(keySelector)
            .Select(g =>
            {
                var groupFragments = Compose(g.ToArray());
                var header = headerGenerator(g.Key);
                return groupFragments.WithHeader(header);
            })
            .ToArray();

        return Compose(groups);
    }

    /// <summary>
    /// Creates a try-catch pattern for fragment generation with error handling.
    /// </summary>
    /// <param name="generator">The fragment generator that might throw.</param>
    /// <param name="errorHandler">Function to create an error fragment from the exception.</param>
    /// <returns>The generated fragment or error fragment.</returns>
    public static ReportFragment TryCatch(
        Func<ReportFragment> generator,
        Func<Exception, ReportFragment> errorHandler)
    {
        if (generator == null)
            return ReportFragment.Empty();

        try
        {
            return generator() ?? ReportFragment.Empty();
        }
        catch (Exception ex)
        {
            if (errorHandler == null)
                return ReportFragment.CreateError("Error", ex.Message, 0);
            
            return errorHandler(ex) ?? ReportFragment.CreateError("Error", ex.Message, 0);
        }
    }

    /// <summary>
    /// Chains multiple fragment generators, stopping at the first that produces content.
    /// </summary>
    /// <param name="generators">The fragment generators to try in order.</param>
    /// <returns>The first fragment with content, or empty if none produce content.</returns>
    public static ReportFragment FirstOrDefault(params Func<ReportFragment>[] generators)
    {
        if (generators == null || generators.Length == 0)
            return ReportFragment.Empty();

        foreach (var generator in generators)
        {
            if (generator == null) continue;
            
            var fragment = generator();
            if (fragment != null && fragment.HasContent())
                return fragment;
        }

        return ReportFragment.Empty();
    }
}