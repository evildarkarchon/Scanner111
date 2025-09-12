using System.Collections.Immutable;

namespace Scanner111.Core.Reporting;

/// <summary>
/// Provides a fluent builder for creating report fragments.
/// </summary>
public sealed class FragmentBuilder
{
    private string _title = string.Empty;
    private string _content = string.Empty;
    private int _order = 100;
    private FragmentType _type = FragmentType.Section;
    private FragmentVisibility _visibility = FragmentVisibility.Always;
    private readonly List<ReportFragment> _children = new();
    private readonly Dictionary<string, string> _metadata = new();
    private readonly List<string> _lines = new();

    /// <summary>
    /// Creates a new fragment builder.
    /// </summary>
    public static FragmentBuilder Create() => new();

    /// <summary>
    /// Sets the title of the fragment.
    /// </summary>
    public FragmentBuilder WithTitle(string title)
    {
        _title = title ?? string.Empty;
        return this;
    }

    /// <summary>
    /// Sets the content of the fragment.
    /// </summary>
    public FragmentBuilder WithContent(string content)
    {
        _content = content ?? string.Empty;
        return this;
    }

    /// <summary>
    /// Adds a line to the fragment content.
    /// </summary>
    public FragmentBuilder AddLine(string line)
    {
        _lines.Add(line ?? string.Empty);
        return this;
    }

    /// <summary>
    /// Adds multiple lines to the fragment content.
    /// </summary>
    public FragmentBuilder AddLines(params string[] lines)
    {
        if (lines != null)
            _lines.AddRange(lines);
        return this;
    }

    /// <summary>
    /// Adds multiple lines to the fragment content.
    /// </summary>
    public FragmentBuilder AddLines(IEnumerable<string> lines)
    {
        if (lines != null)
            _lines.AddRange(lines);
        return this;
    }

    /// <summary>
    /// Sets the display order of the fragment.
    /// </summary>
    public FragmentBuilder WithOrder(int order)
    {
        _order = order;
        return this;
    }

    /// <summary>
    /// Sets the type of the fragment.
    /// </summary>
    public FragmentBuilder WithType(FragmentType type)
    {
        _type = type;
        return this;
    }

    /// <summary>
    /// Sets the visibility of the fragment.
    /// </summary>
    public FragmentBuilder WithVisibility(FragmentVisibility visibility)
    {
        _visibility = visibility;
        return this;
    }

    /// <summary>
    /// Adds a child fragment.
    /// </summary>
    public FragmentBuilder AddChild(ReportFragment child)
    {
        if (child != null)
            _children.Add(child);
        return this;
    }

    /// <summary>
    /// Adds multiple child fragments.
    /// </summary>
    public FragmentBuilder AddChildren(params ReportFragment[] children)
    {
        if (children != null)
            _children.AddRange(children.Where(c => c != null));
        return this;
    }

    /// <summary>
    /// Adds multiple child fragments.
    /// </summary>
    public FragmentBuilder AddChildren(IEnumerable<ReportFragment> children)
    {
        if (children != null)
            _children.AddRange(children.Where(c => c != null));
        return this;
    }

    /// <summary>
    /// Adds metadata to the fragment.
    /// </summary>
    public FragmentBuilder AddMetadata(string key, string value)
    {
        if (!string.IsNullOrEmpty(key))
            _metadata[key] = value ?? string.Empty;
        return this;
    }

    /// <summary>
    /// Adds multiple metadata entries to the fragment.
    /// </summary>
    public FragmentBuilder AddMetadata(IEnumerable<KeyValuePair<string, string>> metadata)
    {
        if (metadata != null)
        {
            foreach (var kvp in metadata)
            {
                if (!string.IsNullOrEmpty(kvp.Key))
                    _metadata[kvp.Key] = kvp.Value ?? string.Empty;
            }
        }
        return this;
    }

    /// <summary>
    /// Conditionally modifies the builder based on a condition.
    /// </summary>
    public FragmentBuilder When(bool condition, Func<FragmentBuilder, FragmentBuilder> configure)
    {
        return condition && configure != null ? configure(this) : this;
    }

    /// <summary>
    /// Conditionally modifies the builder based on a predicate.
    /// </summary>
    public FragmentBuilder When<T>(T value, Func<T, bool> predicate, Func<FragmentBuilder, T, FragmentBuilder> configure)
    {
        return predicate != null && configure != null && predicate(value) 
            ? configure(this, value) 
            : this;
    }

    /// <summary>
    /// Marks the fragment as a header.
    /// </summary>
    public FragmentBuilder AsHeader(int order = 0)
    {
        _type = FragmentType.Header;
        _order = order;
        return this;
    }

    /// <summary>
    /// Marks the fragment as a section.
    /// </summary>
    public FragmentBuilder AsSection(int order = 100)
    {
        _type = FragmentType.Section;
        _order = order;
        return this;
    }

    /// <summary>
    /// Marks the fragment as a warning.
    /// </summary>
    public FragmentBuilder AsWarning(int order = 50)
    {
        _type = FragmentType.Warning;
        _order = order;
        return this;
    }

    /// <summary>
    /// Marks the fragment as an error.
    /// </summary>
    public FragmentBuilder AsError(int order = 10)
    {
        _type = FragmentType.Error;
        _order = order;
        return this;
    }

    /// <summary>
    /// Marks the fragment as informational.
    /// </summary>
    public FragmentBuilder AsInfo(int order = 200)
    {
        _type = FragmentType.Info;
        _order = order;
        return this;
    }

    /// <summary>
    /// Marks the fragment as conditional.
    /// </summary>
    public FragmentBuilder AsConditional(FragmentVisibility visibility, int order = 150)
    {
        _type = FragmentType.Conditional;
        _visibility = visibility;
        _order = order;
        return this;
    }

    /// <summary>
    /// Marks the fragment as a container.
    /// </summary>
    public FragmentBuilder AsContainer(int order = 100)
    {
        _type = FragmentType.Container;
        _order = order;
        return this;
    }

    /// <summary>
    /// Sets the fragment to be always visible.
    /// </summary>
    public FragmentBuilder AlwaysVisible()
    {
        _visibility = FragmentVisibility.Always;
        return this;
    }

    /// <summary>
    /// Sets the fragment to be visible only in verbose mode.
    /// </summary>
    public FragmentBuilder VerboseOnly()
    {
        _visibility = FragmentVisibility.Verbose;
        return this;
    }

    /// <summary>
    /// Sets the fragment to be visible only on error.
    /// </summary>
    public FragmentBuilder OnErrorOnly()
    {
        _visibility = FragmentVisibility.OnError;
        return this;
    }

    /// <summary>
    /// Sets the fragment to be visible only on warning.
    /// </summary>
    public FragmentBuilder OnWarningOnly()
    {
        _visibility = FragmentVisibility.OnWarning;
        return this;
    }

    /// <summary>
    /// Builds the report fragment.
    /// </summary>
    public ReportFragment Build()
    {
        // Combine content and lines
        var finalContent = _content;
        if (_lines.Any())
        {
            if (!string.IsNullOrEmpty(finalContent))
                finalContent += Environment.NewLine;
            finalContent += string.Join(Environment.NewLine, _lines);
        }

        // Create the fragment based on type
        ReportFragment fragment = _type switch
        {
            FragmentType.Header => ReportFragment.CreateHeader(_title, _order),
            FragmentType.Warning => ReportFragment.CreateWarning(_title, finalContent, _order),
            FragmentType.Error => ReportFragment.CreateError(_title, finalContent, _order),
            FragmentType.Info => ReportFragment.CreateInfo(_title, finalContent, _order),
            FragmentType.Conditional => ReportFragment.CreateConditional(_title, finalContent, _visibility, _order),
            FragmentType.Container when _children.Any() => ReportFragment.CreateWithChildren(_title, _children, _order),
            _ => ReportFragment.CreateSection(_title, finalContent, _order)
        };

        // Add children if any (for non-container types)
        if (_children.Any() && _type != FragmentType.Container)
        {
            fragment = fragment.WithChildren(_children.ToArray());
        }

        // Add metadata
        foreach (var kvp in _metadata)
        {
            fragment = fragment.WithMetadata(kvp.Key, kvp.Value);
        }

        return fragment;
    }

    /// <summary>
    /// Builds the fragment only if it has content.
    /// </summary>
    public ReportFragment? BuildIfHasContent()
    {
        var fragment = Build();
        return fragment.HasContent() ? fragment : null;
    }

    /// <summary>
    /// Builds the fragment or returns empty if no content.
    /// </summary>
    public ReportFragment BuildOrEmpty()
    {
        var fragment = Build();
        return fragment.HasContent() ? fragment : ReportFragment.Empty();
    }

    /// <summary>
    /// Resets the builder to initial state.
    /// </summary>
    public FragmentBuilder Reset()
    {
        _title = string.Empty;
        _content = string.Empty;
        _order = 100;
        _type = FragmentType.Section;
        _visibility = FragmentVisibility.Always;
        _children.Clear();
        _metadata.Clear();
        _lines.Clear();
        return this;
    }

    /// <summary>
    /// Creates a copy of the current builder state.
    /// </summary>
    public FragmentBuilder Clone()
    {
        var clone = new FragmentBuilder
        {
            _title = _title,
            _content = _content,
            _order = _order,
            _type = _type,
            _visibility = _visibility
        };
        clone._children.AddRange(_children);
        foreach (var kvp in _metadata)
            clone._metadata[kvp.Key] = kvp.Value;
        clone._lines.AddRange(_lines);
        return clone;
    }
}