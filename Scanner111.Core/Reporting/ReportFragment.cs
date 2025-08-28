using System.Text;

namespace Scanner111.Core.Reporting;

/// <summary>
///     Represents a fragment of a report that can be composed with other fragments.
///     Immutable to ensure thread-safety during parallel analyzer execution.
/// </summary>
public sealed class ReportFragment
{
    private readonly List<ReportFragment> _children;

    private ReportFragment(
        string title,
        string content,
        int order,
        FragmentType type,
        FragmentVisibility visibility)
    {
        Title = title;
        Content = content;
        Order = order;
        Type = type;
        Visibility = visibility;
        _children = new List<ReportFragment>();
        Id = Guid.NewGuid();
        CreatedAt = DateTime.UtcNow;
    }

    /// <summary>
    ///     Gets the unique identifier for this fragment.
    /// </summary>
    public Guid Id { get; }

    /// <summary>
    ///     Gets the title of this fragment.
    /// </summary>
    public string Title { get; }

    /// <summary>
    ///     Gets the content of this fragment.
    /// </summary>
    public string Content { get; }

    /// <summary>
    ///     Gets the display order for this fragment. Lower values appear first.
    /// </summary>
    public int Order { get; }

    /// <summary>
    ///     Gets the type of this fragment.
    /// </summary>
    public FragmentType Type { get; }

    /// <summary>
    ///     Gets the visibility rules for this fragment.
    /// </summary>
    public FragmentVisibility Visibility { get; }

    /// <summary>
    ///     Gets child fragments nested under this fragment.
    /// </summary>
    public IReadOnlyList<ReportFragment> Children => _children.AsReadOnly();

    /// <summary>
    ///     Gets when this fragment was created.
    /// </summary>
    public DateTime CreatedAt { get; }

    /// <summary>
    ///     Gets optional metadata for conditional rendering.
    /// </summary>
    public IReadOnlyDictionary<string, string>? Metadata { get; init; }

    /// <summary>
    ///     Creates an empty fragment with no content.
    /// </summary>
    public static ReportFragment Empty()
    {
        return new ReportFragment(
            string.Empty,
            string.Empty,
            int.MaxValue,
            FragmentType.Section,
            FragmentVisibility.Always);
    }

    /// <summary>
    ///     Creates a new header fragment.
    /// </summary>
    public static ReportFragment CreateHeader(string title, int order = 0)
    {
        return new ReportFragment(
            title ?? throw new ArgumentNullException(nameof(title)),
            string.Empty,
            order,
            FragmentType.Header,
            FragmentVisibility.Always);
    }

    /// <summary>
    ///     Creates a new section fragment with content.
    /// </summary>
    public static ReportFragment CreateSection(string title, string content, int order = 100)
    {
        return new ReportFragment(
            title ?? throw new ArgumentNullException(nameof(title)),
            content ?? string.Empty,
            order,
            FragmentType.Section,
            FragmentVisibility.Always);
    }

    /// <summary>
    ///     Creates a new warning fragment.
    /// </summary>
    public static ReportFragment CreateWarning(string title, string content, int order = 50)
    {
        return new ReportFragment(
            title ?? throw new ArgumentNullException(nameof(title)),
            content ?? throw new ArgumentNullException(nameof(content)),
            order,
            FragmentType.Warning,
            FragmentVisibility.Always);
    }

    /// <summary>
    ///     Creates a new error fragment.
    /// </summary>
    public static ReportFragment CreateError(string title, string content, int order = 10)
    {
        return new ReportFragment(
            title ?? throw new ArgumentNullException(nameof(title)),
            content ?? throw new ArgumentNullException(nameof(content)),
            order,
            FragmentType.Error,
            FragmentVisibility.Always);
    }

    /// <summary>
    ///     Creates a new informational fragment.
    /// </summary>
    public static ReportFragment CreateInfo(string title, string content, int order = 200)
    {
        return new ReportFragment(
            title ?? throw new ArgumentNullException(nameof(title)),
            content ?? string.Empty,
            order,
            FragmentType.Info,
            FragmentVisibility.Always);
    }

    /// <summary>
    ///     Creates a conditional fragment that may be hidden based on rules.
    /// </summary>
    public static ReportFragment CreateConditional(
        string title,
        string content,
        FragmentVisibility visibility,
        int order = 150)
    {
        return new ReportFragment(
            title ?? throw new ArgumentNullException(nameof(title)),
            content ?? string.Empty,
            order,
            FragmentType.Conditional,
            visibility);
    }

    /// <summary>
    ///     Creates a new fragment with child fragments.
    /// </summary>
    public static ReportFragment CreateWithChildren(
        string title,
        IEnumerable<ReportFragment> children,
        int order = 100)
    {
        var fragment = new ReportFragment(
            title ?? throw new ArgumentNullException(nameof(title)),
            string.Empty,
            order,
            FragmentType.Container,
            FragmentVisibility.Always);

        if (children != null)
            fragment._children.AddRange(children);

        return fragment;
    }

    /// <summary>
    ///     Composes multiple fragments into a single container fragment.
    /// </summary>
    public static ReportFragment Compose(params ReportFragment?[] fragments)
    {
        return Compose(fragments.AsEnumerable());
    }

    /// <summary>
    ///     Composes multiple fragments into a single container fragment.
    /// </summary>
    public static ReportFragment Compose(IEnumerable<ReportFragment?> fragments)
    {
        var nonNullFragments = fragments?.Where(f => f != null).Cast<ReportFragment>().ToList() 
            ?? new List<ReportFragment>();

        if (!nonNullFragments.Any())
            return Empty();

        if (nonNullFragments.Count == 1)
            return nonNullFragments[0];

        return CreateWithChildren("Composed Report", nonNullFragments, 0);
    }

    /// <summary>
    ///     Creates a conditional section with a header only if content is present.
    /// </summary>
    public static ReportFragment ConditionalSection(
        Func<ReportFragment> contentGenerator,
        Func<string> headerGenerator)
    {
        var content = contentGenerator();
        if (content.HasContent())
        {
            return content.WithHeader(headerGenerator());
        }
        return content;
    }

    /// <summary>
    ///     Checks if this fragment has meaningful content.
    /// </summary>
    public bool HasContent()
    {
        return !string.IsNullOrWhiteSpace(Content) || _children.Any(c => c.HasContent());
    }

    /// <summary>
    ///     Adds a header to this fragment only if it has content.
    /// </summary>
    public ReportFragment WithHeader(string headerTitle)
    {
        if (!HasContent())
            return this;

        var headerFragment = CreateWithChildren(headerTitle, new[] { this }, Order);
        return headerFragment;
    }

    /// <summary>
    ///     Combines two fragments using the + operator.
    /// </summary>
    public static ReportFragment operator +(ReportFragment left, ReportFragment right)
    {
        if (left == null) throw new ArgumentNullException(nameof(left));
        if (right == null) throw new ArgumentNullException(nameof(right));

        // If either is empty, return the other
        if (!left.HasContent()) return right;
        if (!right.HasContent()) return left;

        // Combine into a container
        return CreateWithChildren("Combined Fragments", new[] { left, right }, 
            Math.Min(left.Order, right.Order));
    }

    /// <summary>
    ///     Renders this fragment to Markdown format.
    /// </summary>
    public string ToMarkdown(int headerLevel = 2)
    {
        var sb = new StringBuilder();

        // Add title if present
        if (!string.IsNullOrWhiteSpace(Title))
        {
            var headerPrefix = new string('#', Math.Min(headerLevel, 6));

            switch (Type)
            {
                case FragmentType.Header:
                    sb.AppendLine($"{headerPrefix} {Title}");
                    sb.AppendLine();
                    break;

                case FragmentType.Section:
                case FragmentType.Container:
                    sb.AppendLine($"{headerPrefix}# {Title}");
                    sb.AppendLine();
                    break;

                case FragmentType.Warning:
                    sb.AppendLine($"{headerPrefix}# ⚠️ {Title}");
                    sb.AppendLine();
                    break;

                case FragmentType.Error:
                    sb.AppendLine($"{headerPrefix}# ❌ {Title}");
                    sb.AppendLine();
                    break;

                case FragmentType.Info:
                    sb.AppendLine($"{headerPrefix}# ℹ️ {Title}");
                    sb.AppendLine();
                    break;
            }
        }

        // Add content
        if (!string.IsNullOrWhiteSpace(Content))
        {
            sb.AppendLine(Content);
            sb.AppendLine();
        }

        // Add children
        foreach (var child in _children) sb.Append(child.ToMarkdown(headerLevel + 1));

        return sb.ToString();
    }
}

/// <summary>
///     Specifies the type of report fragment.
/// </summary>
public enum FragmentType
{
    /// <summary>
    ///     Main header fragment.
    /// </summary>
    Header,

    /// <summary>
    ///     Section with content.
    /// </summary>
    Section,

    /// <summary>
    ///     Warning message.
    /// </summary>
    Warning,

    /// <summary>
    ///     Error message.
    /// </summary>
    Error,

    /// <summary>
    ///     Informational content.
    /// </summary>
    Info,

    /// <summary>
    ///     Container for child fragments.
    /// </summary>
    Container,

    /// <summary>
    ///     Conditional content that may be hidden.
    /// </summary>
    Conditional
}

/// <summary>
///     Defines visibility rules for a fragment.
/// </summary>
public enum FragmentVisibility
{
    /// <summary>
    ///     Always visible.
    /// </summary>
    Always,

    /// <summary>
    ///     Only visible when errors exist.
    /// </summary>
    OnError,

    /// <summary>
    ///     Only visible when warnings exist.
    /// </summary>
    OnWarning,

    /// <summary>
    ///     Only visible in verbose mode.
    /// </summary>
    Verbose,

    /// <summary>
    ///     Hidden by default.
    /// </summary>
    Hidden
}