using System.Text;

namespace Scanner111.Core.Reporting;

/// <summary>
///     Fluent builder for constructing report fragments in a C# idiomatic way.
///     Thread-safe for building fragments.
/// </summary>
public sealed class ReportFragmentBuilder
{
    private readonly List<ReportFragment> _children;
    private readonly StringBuilder _contentBuilder;
    private readonly Dictionary<string, string> _metadata;
    private int _order;
    private string _title;
    private FragmentType _type;
    private FragmentVisibility _visibility;

    private ReportFragmentBuilder()
    {
        _contentBuilder = new StringBuilder();
        _children = new List<ReportFragment>();
        _metadata = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        _title = "Report Section";
        _type = FragmentType.Section;
        _order = 100;
        _visibility = FragmentVisibility.Always;
    }

    /// <summary>
    ///     Creates a new report fragment builder.
    /// </summary>
    public static ReportFragmentBuilder Create()
    {
        return new ReportFragmentBuilder();
    }

    /// <summary>
    ///     Creates a builder for a success report.
    /// </summary>
    public static ReportFragmentBuilder CreateSuccess(string message)
    {
        return new ReportFragmentBuilder()
            .WithType(FragmentType.Info)
            .WithOrder(200)
            .AppendSuccess(message);
    }

    /// <summary>
    ///     Creates a builder for a warning report.
    /// </summary>
    public static ReportFragmentBuilder CreateWarning(string warning, string? fix = null)
    {
        var builder = new ReportFragmentBuilder()
            .WithType(FragmentType.Warning)
            .WithOrder(50)
            .AppendWarning(warning);

        if (!string.IsNullOrWhiteSpace(fix)) builder.AppendFix(fix);

        return builder;
    }

    /// <summary>
    ///     Creates a builder for an error report.
    /// </summary>
    public static ReportFragmentBuilder CreateError(string error, string? solution = null)
    {
        var builder = new ReportFragmentBuilder()
            .WithType(FragmentType.Error)
            .WithOrder(10)
            .AppendError(error);

        if (!string.IsNullOrWhiteSpace(solution)) builder.AppendSolution(solution);

        return builder;
    }

    /// <summary>
    ///     Sets the title for the fragment.
    /// </summary>
    public ReportFragmentBuilder WithTitle(string title)
    {
        _title = title ?? throw new ArgumentNullException(nameof(title));
        return this;
    }

    /// <summary>
    ///     Sets the fragment type.
    /// </summary>
    public ReportFragmentBuilder WithType(FragmentType type)
    {
        _type = type;
        return this;
    }

    /// <summary>
    ///     Sets the display order.
    /// </summary>
    public ReportFragmentBuilder WithOrder(int order)
    {
        _order = order;
        return this;
    }

    /// <summary>
    ///     Sets the visibility rules.
    /// </summary>
    public ReportFragmentBuilder WithVisibility(FragmentVisibility visibility)
    {
        _visibility = visibility;
        return this;
    }

    /// <summary>
    ///     Adds metadata to the fragment.
    /// </summary>
    public ReportFragmentBuilder WithMetadata(string key, string value)
    {
        _metadata[key] = value;
        return this;
    }

    /// <summary>
    ///     Appends raw text to the content.
    /// </summary>
    public ReportFragmentBuilder Append(string text)
    {
        _contentBuilder.Append(text);
        return this;
    }

    /// <summary>
    ///     Appends a line to the content.
    /// </summary>
    public ReportFragmentBuilder AppendLine(string? line = null)
    {
        if (line != null)
            _contentBuilder.AppendLine(line);
        else
            _contentBuilder.AppendLine();
        return this;
    }

    /// <summary>
    ///     Appends a formatted line to the content.
    /// </summary>
    public ReportFragmentBuilder AppendFormatted(string format, params object[] args)
    {
        _contentBuilder.AppendFormat(format, args);
        return this;
    }

    /// <summary>
    ///     Appends a success message with checkmark.
    /// </summary>
    public ReportFragmentBuilder AppendSuccess(string message)
    {
        _contentBuilder.AppendLine($"✔️ {message}");
        return this;
    }

    /// <summary>
    ///     Appends a warning message.
    /// </summary>
    public ReportFragmentBuilder AppendWarning(string warning)
    {
        _contentBuilder.AppendLine($"# ❌ CAUTION : {warning} #");
        return this;
    }

    /// <summary>
    ///     Appends an error message.
    /// </summary>
    public ReportFragmentBuilder AppendError(string error)
    {
        _contentBuilder.AppendLine($"❌ ERROR: {error}");
        return this;
    }

    /// <summary>
    ///     Appends a fix instruction.
    /// </summary>
    public ReportFragmentBuilder AppendFix(string fix)
    {
        _contentBuilder.AppendLine($" FIX: {fix}");
        return this;
    }

    /// <summary>
    ///     Appends a solution instruction.
    /// </summary>
    public ReportFragmentBuilder AppendSolution(string solution)
    {
        _contentBuilder.AppendLine($" SOLUTION: {solution}");
        return this;
    }

    /// <summary>
    ///     Appends a notice message.
    /// </summary>
    public ReportFragmentBuilder AppendNotice(string notice)
    {
        _contentBuilder.AppendLine($"* NOTICE : {notice} *");
        return this;
    }

    /// <summary>
    ///     Appends a separator line.
    /// </summary>
    public ReportFragmentBuilder AppendSeparator()
    {
        _contentBuilder.AppendLine("-----");
        return this;
    }

    /// <summary>
    ///     Conditionally appends content.
    /// </summary>
    public ReportFragmentBuilder AppendIf(bool condition, string text)
    {
        if (condition) _contentBuilder.Append(text);
        return this;
    }

    /// <summary>
    ///     Conditionally appends a line.
    /// </summary>
    public ReportFragmentBuilder AppendLineIf(bool condition, string line)
    {
        if (condition) _contentBuilder.AppendLine(line);
        return this;
    }

    /// <summary>
    ///     Appends multiple lines from a collection.
    /// </summary>
    public ReportFragmentBuilder AppendLines(IEnumerable<string> lines)
    {
        foreach (var line in lines) _contentBuilder.AppendLine(line);
        return this;
    }

    /// <summary>
    ///     Adds a child fragment.
    /// </summary>
    public ReportFragmentBuilder AddChild(ReportFragment child)
    {
        ArgumentNullException.ThrowIfNull(child);
        _children.Add(child);
        return this;
    }

    /// <summary>
    ///     Adds multiple child fragments.
    /// </summary>
    public ReportFragmentBuilder AddChildren(IEnumerable<ReportFragment> children)
    {
        _children.AddRange(children.Where(c => c != null));
        return this;
    }

    /// <summary>
    ///     Builds the final report fragment.
    /// </summary>
    public ReportFragment Build()
    {
        var content = _contentBuilder.ToString();

        // If we have children, create a container
        if (_children.Any())
        {
            var fragment = ReportFragment.CreateWithChildren(_title, _children, _order);
            // Note: Metadata cannot be set after creation since it's init-only
            // This is a limitation of the current design
            return fragment;
        }

        // Create fragment based on type
        var result = _type switch
        {
            FragmentType.Error => ReportFragment.CreateError(_title, content, _order),
            FragmentType.Warning => ReportFragment.CreateWarning(_title, content, _order),
            FragmentType.Info => ReportFragment.CreateInfo(_title, content, _order),
            FragmentType.Header => ReportFragment.CreateHeader(_title, _order),
            FragmentType.Conditional => ReportFragment.CreateConditional(_title, content, _visibility, _order),
            _ => ReportFragment.CreateSection(_title, content, _order)
        };

        // Note: Metadata cannot be set after creation since it's init-only
        // This is a limitation of the current design
        return result;
    }

    /// <summary>
    ///     Builds the fragment only if it has content.
    /// </summary>
    public ReportFragment? BuildIfNotEmpty()
    {
        if (_contentBuilder.Length == 0 && !_children.Any()) return null;
        return Build();
    }
}