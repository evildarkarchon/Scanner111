namespace Scanner111.Common.Models.Reporting;

/// <summary>
/// Immutable report fragment for functional composition.
/// Report fragments can be combined to build complete crash log analysis reports.
/// </summary>
public record ReportFragment
{
    /// <summary>
    /// Gets the lines of text in this report fragment.
    /// </summary>
    public IReadOnlyList<string> Lines { get; init; } = Array.Empty<string>();

    /// <summary>
    /// Gets a value indicating whether this fragment has any content.
    /// </summary>
    public bool HasContent => Lines.Count > 0;

    /// <summary>
    /// Creates a new report fragment from the specified lines.
    /// </summary>
    /// <param name="lines">The lines to include in the fragment.</param>
    /// <returns>A new <see cref="ReportFragment"/> containing the specified lines.</returns>
    public static ReportFragment FromLines(params string[] lines)
        => new() { Lines = lines.ToList() };

    /// <summary>
    /// Creates a new report fragment with a header added at the beginning.
    /// If the fragment has no content, returns the fragment unchanged.
    /// </summary>
    /// <param name="header">The header text to add.</param>
    /// <returns>A new <see cref="ReportFragment"/> with the header added.</returns>
    public ReportFragment WithHeader(string header)
    {
        if (!HasContent)
            return this;

        return this with
        {
            Lines = new[] { header, string.Empty }
                .Concat(Lines)
                .ToList()
        };
    }

    /// <summary>
    /// Combines two report fragments into a single fragment.
    /// </summary>
    /// <param name="a">The first fragment.</param>
    /// <param name="b">The second fragment.</param>
    /// <returns>A new <see cref="ReportFragment"/> containing the lines from both fragments.</returns>
    public static ReportFragment operator +(ReportFragment a, ReportFragment b)
        => new() { Lines = a.Lines.Concat(b.Lines).ToList() };
}
