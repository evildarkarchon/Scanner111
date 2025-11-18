using System.Text.RegularExpressions;
using Scanner111.Common.Models.Analysis;

namespace Scanner111.Common.Services.Analysis;

/// <summary>
/// Parses plugin lists from crash log segments.
/// The plugin list IS the load order - plugins appear in the order they were loaded.
/// </summary>
public partial class PluginListParser
{
    /// <summary>
    /// Regex to match plugin lines in the format: [FormIdPrefix] PluginName.ext
    /// Examples:
    /// - [E7] StartMeUp.esp
    /// - [FE:000] PPF.esm
    /// - [00] Fallout4.esm
    /// </summary>
    [GeneratedRegex(@"^\s*\[([A-Fa-f0-9:]+)\]\s+(.+?)\s*$", RegexOptions.Multiline)]
    private static partial Regex PluginLineRegex();

    /// <summary>
    /// Parses the plugin list from a PLUGINS segment.
    /// </summary>
    /// <param name="pluginSegment">The PLUGINS segment from the crash log.</param>
    /// <returns>A list of <see cref="PluginInfo"/> objects in load order.</returns>
    public IReadOnlyList<PluginInfo> ParsePluginList(LogSegment pluginSegment)
    {
        if (pluginSegment == null)
        {
            return Array.Empty<PluginInfo>();
        }

        var plugins = new List<PluginInfo>();

        foreach (var line in pluginSegment.Lines)
        {
            var match = PluginLineRegex().Match(line);
            if (match.Success)
            {
                var formIdPrefix = match.Groups[1].Value.Trim();
                var pluginName = match.Groups[2].Value.Trim();

                plugins.Add(new PluginInfo
                {
                    FormIdPrefix = formIdPrefix,
                    PluginName = pluginName
                });
            }
        }

        return plugins;
    }
}
