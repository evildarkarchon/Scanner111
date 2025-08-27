using Scanner111.Core.Models;
using Scanner111.Core.Reporting;

namespace Scanner111.Core.Services;

/// <summary>
///     Service for loading and processing plugin information from various sources.
///     Provides thread-safe operations for plugin detection and analysis.
/// </summary>
public interface IPluginLoader
{
    /// <summary>
    ///     Attempts to load plugins from the loadorder.txt file in the main directory.
    ///     This method mimics the behavior of the original Python implementation.
    /// </summary>
    /// <param name="loadOrderPath">Optional path to loadorder.txt file. If null, uses default path.</param>
    /// <param name="cancellationToken">Token to cancel the operation.</param>
    /// <returns>
    ///     A tuple containing:
    ///     - Dictionary of plugin names to their origin markers
    ///     - Boolean indicating if any plugins were successfully loaded
    ///     - ReportFragment with status information and any errors
    /// </returns>
    Task<(Dictionary<string, string> plugins, bool pluginsLoaded, ReportFragment fragment)>
        LoadFromLoadOrderFileAsync(string? loadOrderPath = null, CancellationToken cancellationToken = default);

    /// <summary>
    ///     Scans and processes plugin load order from crash log segment plugins.
    ///     Extracts plugin information, detects plugin limits, and handles version-specific behavior.
    /// </summary>
    /// <param name="segmentPlugins">List of plugin entries from crash log segments.</param>
    /// <param name="gameVersion">The detected version of the game.</param>
    /// <param name="currentVersion">The current version of the crash generator.</param>
    /// <param name="ignoredPlugins">Optional set of plugin names to ignore.</param>
    /// <returns>
    ///     A tuple containing:
    ///     - Dictionary mapping plugin names to their identifiers or classifications
    ///     - Boolean flag indicating if plugin limit marker was detected and triggered
    ///     - Boolean flag indicating if plugin limit checks were disabled
    /// </returns>
    (Dictionary<string, string> plugins, bool limitTriggered, bool limitCheckDisabled) ScanPluginsFromLog(
        IEnumerable<string> segmentPlugins,
        Version gameVersion,
        Version currentVersion,
        ISet<string>? ignoredPlugins = null);

    /// <summary>
    ///     Creates a collection of PluginInfo objects from various sources.
    ///     Combines loadorder.txt data with crash log plugin information.
    /// </summary>
    /// <param name="loadOrderPlugins">Plugins loaded from loadorder.txt.</param>
    /// <param name="crashLogPlugins">Plugins detected from crash logs.</param>
    /// <param name="ignoredPlugins">Set of plugin names to mark as ignored.</param>
    /// <returns>Collection of PluginInfo objects with proper metadata.</returns>
    IReadOnlyList<PluginInfo> CreatePluginInfoCollection(
        IDictionary<string, string>? loadOrderPlugins = null,
        IDictionary<string, string>? crashLogPlugins = null,
        ISet<string>? ignoredPlugins = null);

    /// <summary>
    ///     Filters ignored plugins from a plugin dictionary.
    ///     Performs case-insensitive comparison of plugin names.
    /// </summary>
    /// <param name="plugins">Dictionary of plugins to filter.</param>
    /// <param name="ignoredPlugins">Set of plugin names to ignore.</param>
    /// <returns>New dictionary with ignored plugins removed.</returns>
    Dictionary<string, string> FilterIgnoredPlugins(
        IDictionary<string, string> plugins,
        ISet<string> ignoredPlugins);

    /// <summary>
    ///     Validates that a loadorder.txt file has the expected format.
    /// </summary>
    /// <param name="filePath">Path to the loadorder.txt file.</param>
    /// <param name="cancellationToken">Token to cancel the operation.</param>
    /// <returns>True if the file appears to be a valid loadorder.txt file.</returns>
    Task<bool> ValidateLoadOrderFileAsync(string filePath, CancellationToken cancellationToken = default);

    /// <summary>
    ///     Gets statistics about the last plugin loading operation.
    ///     Useful for debugging and monitoring plugin detection performance.
    /// </summary>
    /// <returns>Plugin loading statistics.</returns>
    PluginLoadingStatistics GetStatistics();
}

/// <summary>
///     Statistics about plugin loading operations.
/// </summary>
public sealed record PluginLoadingStatistics
{
    /// <summary>
    ///     Gets the total number of plugins loaded from loadorder.txt.
    /// </summary>
    public int LoadOrderPluginCount { get; init; }

    /// <summary>
    ///     Gets the total number of plugins detected from crash logs.
    /// </summary>
    public int CrashLogPluginCount { get; init; }

    /// <summary>
    ///     Gets the number of plugins that were ignored.
    /// </summary>
    public int IgnoredPluginCount { get; init; }

    /// <summary>
    ///     Gets whether the plugin limit was triggered.
    /// </summary>
    public bool PluginLimitTriggered { get; init; }

    /// <summary>
    ///     Gets whether plugin limit checks were disabled.
    /// </summary>
    public bool LimitCheckDisabled { get; init; }

    /// <summary>
    ///     Gets the time taken for the last loading operation.
    /// </summary>
    public TimeSpan LastOperationDuration { get; init; }

    /// <summary>
    ///     Gets any errors encountered during the last operation.
    /// </summary>
    public IReadOnlyList<string> Errors { get; init; } = Array.Empty<string>();
}