using System.Collections.Concurrent;
using System.Text.RegularExpressions;
using Scanner111.Common.Models.Analysis;

namespace Scanner111.Common.Services.Analysis;

/// <summary>
/// Analyzes plugins from crash logs, including pattern matching and limit checking.
/// </summary>
public class PluginAnalyzer : IPluginAnalyzer
{
    private readonly PluginListParser _parser;
    private readonly PluginLimitChecker _limitChecker;
    private readonly ConcurrentDictionary<string, Regex> _compiledPatterns;

    /// <summary>
    /// Initializes a new instance of the <see cref="PluginAnalyzer"/> class.
    /// </summary>
    public PluginAnalyzer()
    {
        _parser = new PluginListParser();
        _limitChecker = new PluginLimitChecker();
        _compiledPatterns = new ConcurrentDictionary<string, Regex>();
    }

    /// <inheritdoc/>
    public async Task<PluginAnalysisResult> AnalyzeAsync(
        IReadOnlyList<LogSegment> segments,
        CancellationToken cancellationToken = default)
    {
        // Allow async operation to be cancelled
        await Task.Yield();
        cancellationToken.ThrowIfCancellationRequested();

        // Find the PLUGINS segment
        var pluginSegment = segments.FirstOrDefault(s =>
            s.Name.Equals("PLUGINS", StringComparison.OrdinalIgnoreCase));

        if (pluginSegment == null)
        {
            return new PluginAnalysisResult
            {
                Warnings = new[] { "No PLUGINS segment found in crash log" }
            };
        }

        // Extract plugins
        var plugins = ExtractPlugins(pluginSegment);

        // Check limits
        var limitResult = _limitChecker.CheckLimits(plugins);

        return new PluginAnalysisResult
        {
            Plugins = plugins,
            RegularPluginCount = limitResult.FullPluginCount,
            LightPluginCount = limitResult.LightPluginCount,
            ApproachingLimit = limitResult.ApproachingLimit,
            Warnings = limitResult.Warnings
        };
    }

    /// <inheritdoc/>
    public IReadOnlyList<PluginInfo> ExtractPlugins(LogSegment pluginSegment)
    {
        return _parser.ParsePluginList(pluginSegment);
    }

    /// <inheritdoc/>
    public IReadOnlyList<string> MatchPluginPatterns(
        IReadOnlyList<string> pluginNames,
        IReadOnlyList<string> patterns)
    {
        var matches = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var pattern in patterns)
        {
            var regex = GetOrCompileRegex(pattern);

            foreach (var pluginName in pluginNames)
            {
                if (regex.IsMatch(pluginName))
                {
                    matches.Add(pluginName);
                }
            }
        }

        return matches.ToList();
    }

    private Regex GetOrCompileRegex(string pattern)
    {
        return _compiledPatterns.GetOrAdd(pattern, p =>
            new Regex(p, RegexOptions.Compiled | RegexOptions.IgnoreCase));
    }
}
