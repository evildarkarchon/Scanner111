using System.Text.RegularExpressions;

namespace Scanner111.Common.Services.Parsing;

/// <summary>
/// Reformats Buffout 4 crash logs by removing extra spaces in load order sections.
/// Buffout 4 sometimes adds excessive whitespace between columns in the plugin list.
/// </summary>
public static partial class LogReformatter
{
    /// <summary>
    /// Regex to match load order lines with excessive spaces.
    /// Format: [Index] [LoadOrder] [FormID] [PluginName]
    /// Example: "  253   253    FD Unmanaged.esp"
    /// </summary>
    [GeneratedRegex(@"^\s*(\d+)\s+(\d+)\s+([A-Fa-f0-9:]+)\s+(.+?)$", RegexOptions.Multiline)]
    private static partial Regex LoadOrderSpacesRegex();

    /// <summary>
    /// Reformats Buffout 4 load order by normalizing spacing between columns.
    /// </summary>
    /// <param name="logContent">The full crash log content.</param>
    /// <returns>The reformatted log content with normalized spacing.</returns>
    public static string ReformatBuffout4LoadOrder(string logContent)
    {
        if (string.IsNullOrWhiteSpace(logContent))
        {
            return logContent;
        }

        // Replace excessive spaces with single spaces between columns
        return LoadOrderSpacesRegex().Replace(logContent, match =>
        {
            var index = match.Groups[1].Value;
            var loadOrder = match.Groups[2].Value;
            var formId = match.Groups[3].Value;
            var pluginName = match.Groups[4].Value;

            // Normalize to single space between columns
            return $"  {index} {loadOrder} {formId} {pluginName}";
        });
    }
}
