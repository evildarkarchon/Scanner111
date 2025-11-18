namespace Scanner111.Common.Services.Reporting;

/// <summary>
/// Provides utility methods for formatting text as Markdown.
/// </summary>
public static class MarkdownFormatter
{
    /// <summary>
    /// Formats text as bold in Markdown.
    /// </summary>
    /// <param name="text">The text to format.</param>
    /// <returns>The text wrapped in bold markers (**text**).</returns>
    public static string Bold(string text) => $"**{text}**";

    /// <summary>
    /// Formats text as italic in Markdown.
    /// </summary>
    /// <param name="text">The text to format.</param>
    /// <returns>The text wrapped in italic markers (*text*).</returns>
    public static string Italic(string text) => $"*{text}*";

    /// <summary>
    /// Formats text as inline code in Markdown.
    /// </summary>
    /// <param name="text">The text to format.</param>
    /// <returns>The text wrapped in backticks (`text`).</returns>
    public static string Code(string text) => $"`{text}`";

    /// <summary>
    /// Formats text as a code block in Markdown.
    /// </summary>
    /// <param name="text">The code text to format.</param>
    /// <param name="language">Optional language identifier for syntax highlighting.</param>
    /// <returns>The text formatted as a fenced code block.</returns>
    public static string CodeBlock(string text, string language = "")
        => $"```{language}\n{text}\n```";

    /// <summary>
    /// Formats a collection of items as a bulleted list in Markdown.
    /// </summary>
    /// <param name="items">The items to format.</param>
    /// <returns>A string with each item prefixed with "- ".</returns>
    public static string BulletList(IEnumerable<string> items)
        => string.Join("\n", items.Select(i => $"- {i}"));

    /// <summary>
    /// Formats a collection of items as a numbered list in Markdown.
    /// </summary>
    /// <param name="items">The items to format.</param>
    /// <returns>A string with each item numbered (1. item, 2. item, ...).</returns>
    public static string NumberedList(IEnumerable<string> items)
        => string.Join("\n", items.Select((i, idx) => $"{idx + 1}. {i}"));

    /// <summary>
    /// Creates a Markdown heading at the specified level.
    /// </summary>
    /// <param name="text">The heading text.</param>
    /// <param name="level">The heading level (1-6, default is 1 for # heading).</param>
    /// <returns>The text formatted as a Markdown heading.</returns>
    public static string Heading(string text, int level = 1)
    {
        level = Math.Clamp(level, 1, 6);
        var hashes = new string('#', level);
        return $"{hashes} {text}";
    }

    /// <summary>
    /// Creates a Markdown link.
    /// </summary>
    /// <param name="text">The link text to display.</param>
    /// <param name="url">The URL to link to.</param>
    /// <returns>The formatted Markdown link [text](url).</returns>
    public static string Link(string text, string url) => $"[{text}]({url})";

    /// <summary>
    /// Creates a horizontal rule in Markdown.
    /// </summary>
    /// <returns>A Markdown horizontal rule (---).</returns>
    public static string HorizontalRule() => "---";

    /// <summary>
    /// Creates a Markdown blockquote.
    /// </summary>
    /// <param name="text">The text to format as a quote.</param>
    /// <returns>The text prefixed with "> ".</returns>
    public static string Blockquote(string text) => $"> {text}";

    /// <summary>
    /// Creates a Markdown table from headers and rows.
    /// </summary>
    /// <param name="headers">The column headers.</param>
    /// <param name="rows">The table rows (each row is a list of cell values).</param>
    /// <returns>A formatted Markdown table.</returns>
    public static string Table(IReadOnlyList<string> headers, IReadOnlyList<IReadOnlyList<string>> rows)
    {
        var lines = new List<string>();

        // Header row
        lines.Add("| " + string.Join(" | ", headers) + " |");

        // Separator row
        lines.Add("| " + string.Join(" | ", headers.Select(_ => "---")) + " |");

        // Data rows
        foreach (var row in rows)
        {
            lines.Add("| " + string.Join(" | ", row) + " |");
        }

        return string.Join("\n", lines);
    }
}
