using System.Text;
using Microsoft.Extensions.Logging;
using Scanner111.Core.Configuration;
using Scanner111.Core.Models;
using Scanner111.Core.Reporting;
using Scanner111.Core.Services;

namespace Scanner111.Core.Analysis.Analyzers;

/// <summary>
///     Analyzes crash logs to identify plugin suspects and match them against call stack entries.
///     Thread-safe analyzer that processes plugin load orders and performs pattern matching.
/// </summary>
public sealed class PluginAnalyzer : AnalyzerBase
{
    private readonly IPluginLoader _pluginLoader;
    private readonly IAsyncYamlSettingsCore _yamlCore;
    private readonly string _crashGenName;

    public PluginAnalyzer(
        ILogger<PluginAnalyzer> logger,
        IPluginLoader pluginLoader,
        IAsyncYamlSettingsCore yamlCore,
        string? crashGenName = null)
        : base(logger)
    {
        _pluginLoader = pluginLoader ?? throw new ArgumentNullException(nameof(pluginLoader));
        _yamlCore = yamlCore ?? throw new ArgumentNullException(nameof(yamlCore));
        _crashGenName = crashGenName ?? "Scanner111";
    }

    /// <inheritdoc />
    public override string Name => "PluginAnalyzer";

    /// <inheritdoc />
    public override string DisplayName => "Plugin Analysis";

    /// <inheritdoc />
    public override int Priority => 40; // Run after basic crash data extraction but before FormID analysis

    /// <inheritdoc />
    public override TimeSpan Timeout => TimeSpan.FromMinutes(2); // Allow time for file operations

    /// <inheritdoc />
    protected override async Task<AnalysisResult> PerformAnalysisAsync(
        AnalysisContext context,
        CancellationToken cancellationToken)
    {
        LogDebug("Starting plugin analysis for {Path}", context.InputPath);

        try
        {
            // First, try to load plugins from loadorder.txt
            var (loadOrderPlugins, hasLoadOrderFile, loadOrderFragment) =
                await _pluginLoader.LoadFromLoadOrderFileAsync(cancellationToken: cancellationToken)
                    .ConfigureAwait(false);

            var fragments = new List<ReportFragment> { loadOrderFragment };
            Dictionary<string, string> finalPlugins;

            if (hasLoadOrderFile)
            {
                // Use loadorder.txt plugins exclusively
                finalPlugins = loadOrderPlugins;
                LogInformation("Using {Count} plugins from loadorder.txt", finalPlugins.Count);

                // Store plugins in shared context for other analyzers
                context.SetSharedData("CrashLogPlugins", finalPlugins);
                context.SetSharedData("PluginsSource", "LoadOrder");
            }
            else
            {
                // Extract plugins from crash log segments
                finalPlugins = await ExtractPluginsFromCrashLogAsync(context, cancellationToken)
                    .ConfigureAwait(false);

                if (finalPlugins.Count == 0)
                {
                    LogDebug("No plugins found in crash log segments");
                    return CreateNoPluginsResult();
                }

                LogInformation("Extracted {Count} plugins from crash log", finalPlugins.Count);

                // Store plugins in shared context for other analyzers
                context.SetSharedData("CrashLogPlugins", finalPlugins);
                context.SetSharedData("PluginsSource", "CrashLog");
            }

            // Perform plugin matching against call stack
            var pluginMatchFragment = await PerformPluginMatchingAsync(context, finalPlugins, cancellationToken)
                .ConfigureAwait(false);

            if (pluginMatchFragment != null)
            {
                fragments.Add(pluginMatchFragment);
            }

            // Combine all fragments
            var combinedFragment = CombineFragments(fragments);

            // Determine overall severity
            var severity = DetermineSeverity(fragments);

            var result = new AnalysisResult(Name)
            {
                Success = true,
                Fragment = combinedFragment,
                Severity = severity
            };

            // Add metadata
            result.AddMetadata("PluginCount", finalPlugins.Count.ToString());
            result.AddMetadata("PluginsSource", hasLoadOrderFile ? "LoadOrder" : "CrashLog");

            // Add plugin loader statistics
            var stats = _pluginLoader.GetStatistics();
            result.AddMetadata("LoadOrderPluginCount", stats.LoadOrderPluginCount.ToString());
            result.AddMetadata("CrashLogPluginCount", stats.CrashLogPluginCount.ToString());
            result.AddMetadata("IgnoredPluginCount", stats.IgnoredPluginCount.ToString());

            if (stats.PluginLimitTriggered)
            {
                result.AddMetadata("PluginLimitTriggered", "true");
                context.SetSharedData("PluginLimitTriggered", true);
            }

            LogInformation("Plugin analysis completed with severity: {Severity}", severity);

            return result;
        }
        catch (Exception ex)
        {
            LogError(ex, "Failed to analyze plugins");
            return AnalysisResult.CreateFailure(Name, $"Plugin analysis failed: {ex.Message}");
        }
    }

    /// <summary>
    ///     Extracts plugins from crash log segments using the plugin loader.
    /// </summary>
    private async Task<Dictionary<string, string>> ExtractPluginsFromCrashLogAsync(
        AnalysisContext context,
        CancellationToken cancellationToken)
    {
        // Try to get plugin segment data from context
        if (!context.TryGetSharedData<List<string>>("PluginSegment", out var pluginSegment) ||
            pluginSegment == null || pluginSegment.Count == 0)
        {
            LogDebug("No plugin segment found in context");
            return new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        }

        // Load game version information (placeholder values - would come from YAML in real implementation)
        var gameVersion = new Version(1, 0, 0); // Would be loaded from configuration
        var currentVersion = new Version(1, 37, 0); // Would be loaded from configuration

        // Load ignored plugins from YAML configuration
        var ignoredPlugins = await LoadIgnoredPluginsAsync(cancellationToken).ConfigureAwait(false);

        // Scan plugins from log segments
        var (plugins, limitTriggered, limitCheckDisabled) = _pluginLoader.ScanPluginsFromLog(
            pluginSegment,
            gameVersion,
            currentVersion,
            ignoredPlugins);

        // Store additional context data
        if (limitTriggered)
        {
            context.SetSharedData("PluginLimitTriggered", true);
        }
        if (limitCheckDisabled)
        {
            context.SetSharedData("PluginLimitCheckDisabled", true);
        }

        return plugins;
    }

    /// <summary>
    ///     Performs plugin matching against call stack entries.
    /// </summary>
    private async Task<ReportFragment?> PerformPluginMatchingAsync(
        AnalysisContext context,
        Dictionary<string, string> plugins,
        CancellationToken cancellationToken)
    {
        // Get call stack segment from shared data
        if (!context.TryGetSharedData<List<string>>("CallStackSegment", out var callStackSegment) ||
            callStackSegment == null || callStackSegment.Count == 0)
        {
            LogDebug("No call stack segment found for plugin matching");
            return null;
        }

        // Load ignored plugins for filtering
        var ignoredPlugins = await LoadIgnoredPluginsAsync(cancellationToken).ConfigureAwait(false);

        // Convert call stack to lowercase for case-insensitive matching
        var callStackLower = callStackSegment
            .Select(line => line.ToLowerInvariant())
            .ToList();

        // Create lowercase plugin set for matching
        var pluginsLower = new HashSet<string>(
            plugins.Keys.Select(p => p.ToLowerInvariant()),
            StringComparer.OrdinalIgnoreCase);

        // Filter out ignored plugins
        var filteredPluginsLower = pluginsLower
            .Where(plugin => !ignoredPlugins.Contains(plugin))
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        LogDebug("Matching {Count} plugins against {CallStackLines} call stack lines",
            filteredPluginsLower.Count, callStackLower.Count);

        // Find plugin matches in call stack
        var pluginMatches = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);

        // Pre-filter call stack lines that won't match (optimize performance)
        var relevantLines = callStackLower
            .Where(line => !line.Contains("modified by:", StringComparison.Ordinal))
            .ToList();

        foreach (var line in relevantLines)
        {
            foreach (var plugin in filteredPluginsLower)
            {
                if (line.Contains(plugin, StringComparison.Ordinal))
                {
                    pluginMatches[plugin] = pluginMatches.GetValueOrDefault(plugin, 0) + 1;
                }
            }
        }

        // Generate report fragment
        return CreatePluginMatchReport(pluginMatches);
    }

    /// <summary>
    ///     Creates a report fragment for plugin matching results.
    /// </summary>
    private ReportFragment CreatePluginMatchReport(Dictionary<string, int> pluginMatches)
    {
        var content = new StringBuilder();

        if (pluginMatches.Count > 0)
        {
            content.AppendLine("The following PLUGINS were found in the CRASH STACK:\n");

            // Sort by count (descending) then by name for consistent output
            var sortedMatches = pluginMatches
                .OrderByDescending(kvp => kvp.Value)
                .ThenBy(kvp => kvp.Key, StringComparer.OrdinalIgnoreCase);

            foreach (var (plugin, count) in sortedMatches)
            {
                content.AppendLine($"- {plugin} | {count}");
            }

            content.AppendLine();
            content.AppendLine("[Last number counts how many times each Plugin Suspect shows up in the crash log.]");
            content.AppendLine(
                $"These Plugins were caught by {_crashGenName} and some of them might be responsible for this crash.");
            content.AppendLine(
                "You can try disabling these plugins and check if the game still crashes, though this method can be unreliable.");
            content.AppendLine();

            LogInformation("Found {Count} plugin suspects in call stack", pluginMatches.Count);

            return ReportFragment.CreateWarning(
                "Plugin Suspects",
                content.ToString(),
                30); // Higher priority for warnings
        }
        else
        {
            content.AppendLine("* COULDN'T FIND ANY PLUGIN SUSPECTS *");
            content.AppendLine();

            LogDebug("No plugin suspects found in call stack");

            return ReportFragment.CreateInfo(
                "Plugin Suspects",
                content.ToString(),
                100);
        }
    }

    /// <summary>
    ///     Loads ignored plugins from YAML configuration.
    /// </summary>
    private async Task<HashSet<string>> LoadIgnoredPluginsAsync(CancellationToken cancellationToken)
    {
        try
        {
            // Load game-specific ignored plugins
            var gameIgnorePlugins = await _yamlCore.GetSettingAsync<List<string>>(
                    YamlStore.Game, "game_ignore_plugins", null, cancellationToken)
                .ConfigureAwait(false) ?? new List<string>();

            // Load user-specified ignore list
            var ignoreList = await _yamlCore.GetSettingAsync<List<string>>(
                    YamlStore.Settings, "ignore_list", null, cancellationToken)
                .ConfigureAwait(false) ?? new List<string>();

            // Combine both lists with case-insensitive comparison
            var combinedIgnoreList = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            
            foreach (var plugin in gameIgnorePlugins.Concat(ignoreList))
            {
                if (!string.IsNullOrWhiteSpace(plugin))
                {
                    combinedIgnoreList.Add(plugin.ToLowerInvariant());
                }
            }

            LogDebug("Loaded {Count} ignored plugins from configuration", combinedIgnoreList.Count);
            return combinedIgnoreList;
        }
        catch (Exception ex)
        {
            LogWarning("Failed to load ignored plugins from configuration, using empty set: {Message}", ex.Message);
            return new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        }
    }

    /// <summary>
    ///     Creates a result when no plugins are found.
    /// </summary>
    private AnalysisResult CreateNoPluginsResult()
    {
        var fragment = ReportFragment.CreateInfo(
            "Plugin Analysis",
            "No plugins found for analysis. This may indicate an issue with crash log parsing or an empty plugin list.",
            100);

        var result = AnalysisResult.CreateSuccess(Name, fragment);
        result.AddMetadata("PluginCount", "0");
        result.AddMetadata("PluginsSource", "None");

        return result;
    }

    /// <summary>
    ///     Combines multiple report fragments into a single fragment.
    /// </summary>
    private static ReportFragment CombineFragments(List<ReportFragment> fragments)
    {
        var validFragments = fragments.Where(f => f != null).ToList();
        
        if (validFragments.Count == 0)
        {
            return ReportFragment.CreateInfo(
                "Plugin Analysis",
                "Plugin analysis completed with no significant findings.",
                100);
        }

        if (validFragments.Count == 1)
        {
            return validFragments[0];
        }

        return ReportFragment.CreateWithChildren(
            "Plugin Analysis",
            validFragments,
            10); // High priority for combined results
    }

    /// <summary>
    ///     Determines the overall severity based on fragment types.
    /// </summary>
    private static AnalysisSeverity DetermineSeverity(List<ReportFragment> fragments)
    {
        var hasErrors = fragments.Any(f => f?.Type == FragmentType.Error);
        var hasWarnings = fragments.Any(f => f?.Type == FragmentType.Warning);

        if (hasErrors)
            return AnalysisSeverity.Error;
        if (hasWarnings)
            return AnalysisSeverity.Warning;

        return AnalysisSeverity.Info;
    }

    /// <inheritdoc />
    public override async Task<bool> CanAnalyzeAsync(AnalysisContext context)
    {
        var canAnalyze = await base.CanAnalyzeAsync(context).ConfigureAwait(false);
        
        if (!canAnalyze)
            return false;

        // Additional validation: we can analyze if we have either plugin segments or loadorder.txt
        var hasPluginSegment = context.TryGetSharedData<List<string>>("PluginSegment", out var pluginSegment) &&
                              pluginSegment != null;
        
        var hasLoadOrderFile = await _pluginLoader.ValidateLoadOrderFileAsync("loadorder.txt")
            .ConfigureAwait(false);

        var result = hasPluginSegment || hasLoadOrderFile;
        
        LogDebug("CanAnalyze: {Result} (PluginSegment: {HasSegment}, LoadOrder: {HasLoadOrder})",
            result, hasPluginSegment, hasLoadOrderFile);
        
        return result;
    }
}