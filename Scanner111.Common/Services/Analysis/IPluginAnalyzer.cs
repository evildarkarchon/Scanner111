using Scanner111.Common.Models.Analysis;

namespace Scanner111.Common.Services.Analysis;

/// <summary>
/// Provides plugin analysis functionality for crash logs.
/// </summary>
public interface IPluginAnalyzer
{
    /// <summary>
    /// Analyzes plugins from log segments asynchronously.
    /// </summary>
    /// <param name="segments">The parsed log segments.</param>
    /// <param name="cancellationToken">A token to cancel the operation.</param>
    /// <returns>A <see cref="PluginAnalysisResult"/> containing the analysis results.</returns>
    Task<PluginAnalysisResult> AnalyzeAsync(
        IReadOnlyList<LogSegment> segments,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Extracts plugin information from a PLUGINS segment.
    /// </summary>
    /// <param name="pluginSegment">The PLUGINS segment from the crash log.</param>
    /// <returns>A list of <see cref="PluginInfo"/> objects.</returns>
    IReadOnlyList<PluginInfo> ExtractPlugins(LogSegment pluginSegment);

    /// <summary>
    /// Matches plugin names against regex patterns.
    /// </summary>
    /// <param name="pluginNames">The list of plugin names to match.</param>
    /// <param name="patterns">The regex patterns to match against.</param>
    /// <returns>A list of plugin names that matched any pattern.</returns>
    IReadOnlyList<string> MatchPluginPatterns(
        IReadOnlyList<string> pluginNames,
        IReadOnlyList<string> patterns);
}

/// <summary>
/// Represents the result of plugin analysis.
/// </summary>
public record PluginAnalysisResult
{
    /// <summary>
    /// Gets the list of plugins found in the crash log.
    /// </summary>
    public IReadOnlyList<PluginInfo> Plugins { get; init; } = Array.Empty<PluginInfo>();

    /// <summary>
    /// Gets the number of regular (non-light) plugins.
    /// </summary>
    public int RegularPluginCount { get; init; }

    /// <summary>
    /// Gets the number of light plugins.
    /// </summary>
    public int LightPluginCount { get; init; }

    /// <summary>
    /// Gets a value indicating whether the plugin limit is being approached.
    /// </summary>
    public bool ApproachingLimit { get; init; }

    /// <summary>
    /// Gets warning messages related to plugin limits.
    /// </summary>
    public IReadOnlyList<string> Warnings { get; init; } = Array.Empty<string>();
}
