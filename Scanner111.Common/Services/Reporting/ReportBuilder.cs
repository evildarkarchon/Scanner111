using Scanner111.Common.Models.Reporting;

namespace Scanner111.Common.Services.Reporting;

/// <summary>
/// Fluent builder for composing crash log analysis reports from immutable fragments.
/// </summary>
public class ReportBuilder : IReportBuilder
{
    private readonly List<ReportFragment> _fragments = new();

    /// <inheritdoc/>
    public IReportBuilder Add(ReportFragment fragment)
    {
        if (fragment.HasContent)
        {
            _fragments.Add(fragment);
        }
        return this;
    }

    /// <inheritdoc/>
    public IReportBuilder AddConditional(Func<ReportFragment> contentGenerator, string? header = null)
    {
        var fragment = contentGenerator();
        if (fragment.HasContent)
        {
            var withHeader = header != null
                ? fragment.WithHeader(header)
                : fragment;
            _fragments.Add(withHeader);
        }
        return this;
    }

    /// <inheritdoc/>
    public IReportBuilder AddSection(string sectionName, IEnumerable<string> lines)
    {
        var linesList = lines.ToList();
        if (linesList.Count > 0)
        {
            var fragment = ReportFragment.FromLines(linesList.ToArray());
            var withHeader = fragment.WithHeader(sectionName);
            _fragments.Add(withHeader);
        }
        return this;
    }

    /// <inheritdoc/>
    public ReportFragment Build()
    {
        if (_fragments.Count == 0)
        {
            return new ReportFragment();
        }

        return _fragments.Aggregate((a, b) => a + b);
    }
}
