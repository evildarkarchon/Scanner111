using System.Text;
using Microsoft.Extensions.Logging;
using Scanner111.Core.Infrastructure;
using Scanner111.Core.Models;
using Scanner111.Core.Models.Yaml;

namespace Scanner111.Core.Analyzers;

/// <summary>
///     Handles plugin analysis and matching operations, direct port of Python PluginAnalyzer
/// </summary>
public class PluginAnalyzer : IAnalyzer
{
    private readonly ILogger<PluginAnalyzer> _logger;
    private readonly HashSet<string> _lowerPluginsIgnore;
    private readonly IYamlSettingsProvider _yamlSettings;

    /// <summary>
    ///     Initialize the plugin analyzer
    /// </summary>
    /// <param name="yamlSettings">YAML settings provider for configuration</param>
    /// <param name="logger">Logger for debug output</param>
    public PluginAnalyzer(IYamlSettingsProvider yamlSettings, ILogger<PluginAnalyzer> logger)
    {
        _yamlSettings = yamlSettings;
        _logger = logger;

        // Initialize plugin ignore list from YAML settings
        var fallout4Yaml = _yamlSettings.LoadYaml<ClassicFallout4YamlV2>("CLASSIC Fallout4");
        var ignorePluginsList = fallout4Yaml?.CrashlogPluginsExclude ?? new List<string>();
        _lowerPluginsIgnore = new HashSet<string>(
            ignorePluginsList.Select(p => p.ToLower()),
            StringComparer.OrdinalIgnoreCase);
    }

    /// <summary>
    ///     Name of the analyzer
    /// </summary>
    public string Name => "Plugin Analyzer";

    /// <summary>
    ///     Priority of the analyzer (lower values run first)
    /// </summary>
    public int Priority => 20;

    /// <summary>
    ///     Whether this analyzer can be run in parallel with others
    /// </summary>
    public bool CanRunInParallel => true;

    /// <summary>
    ///     Analyzes a crash log for plugin-related information asynchronously.
    /// </summary>
    /// <param name="crashLog">The crash log containing data to analyze.</param>
    /// <param name="cancellationToken">A token to monitor for cancellation requests.</param>
    /// <returns>An analysis result with information about the detected plugins.</returns>
    public async Task<AnalysisResult> AnalyzeAsync(CrashLog crashLog, CancellationToken cancellationToken = default)
    {
        await Task.CompletedTask.ConfigureAwait(false); // Make it async-ready

        var reportLines = new List<string>();
        var plugins = new List<Plugin>();

        // Check if loadorder.txt exists
        var (loadorderPlugins, pluginsLoaded) = await LoadorderScanLoadorderTxt(reportLines).ConfigureAwait(false);

        if (pluginsLoaded)
            // Use loadorder.txt plugins
            foreach (var (pluginName, origin) in loadorderPlugins)
                plugins.Add(new Plugin
                {
                    FileName = pluginName,
                    LoadOrder = origin
                });
        else
            // Use crash log plugins if no loadorder.txt (unfiltered)
            foreach (var (pluginName, loadOrder) in crashLog.Plugins)
                plugins.Add(new Plugin
                {
                    FileName = pluginName,
                    LoadOrder = loadOrder
                });

        // Filter ignored plugins for matching
        var filteredPlugins = FilterIgnoredPlugins(crashLog.Plugins);

        // Debug: log plugin count
        _logger.LogDebug("Crash log plugins: {OriginalCount}, filtered: {FilteredCount}",
            crashLog.Plugins.Count, filteredPlugins.Count);

        // Perform plugin matching
        var crashlogPluginsLower = filteredPlugins.Keys.Select(k => k.ToLower()).ToHashSet();
        var segmentCallstackLower = crashLog.CallStack.Select(line => line.ToLower()).ToList();

        PluginMatch(segmentCallstackLower, crashlogPluginsLower, reportLines);

        return new PluginAnalysisResult
        {
            AnalyzerName = Name,
            Plugins = plugins,
            ReportLines = reportLines,
            HasFindings = plugins.Count > 0
        };
    }

    /// <summary>
    ///     Loads and processes the "loadorder.txt" file from the main "CLASSIC" folder, if available.
    ///     Direct port of Python loadorder_scan_loadorder_txt method.
    /// </summary>
    /// <param name="autoscanReport">A list to log messages or errors related to the scanning process</param>
    /// <returns>
    ///     A dictionary of plugin names mapped to their origin markers and a boolean indicating whether any plugins were
    ///     loaded
    /// </returns>
    private static async Task<(Dictionary<string, string>, bool)> LoadorderScanLoadorderTxt(List<string> autoscanReport)
    {
        var loadorderMessages = new[]
        {
            "* ✔️ LOADORDER.TXT FILE FOUND IN THE MAIN CLASSIC FOLDER! *\n",
            "CLASSIC will now ignore plugins in all crash logs and only detect plugins in this file.\n",
            "[ To disable this functionality, simply remove loadorder.txt from your CLASSIC folder. ]\n\n"
        };
        const string loadorderOrigin = "LO"; // Origin marker for plugins from loadorder.txt
        const string loadorderPath = "loadorder.txt";

        var loadorderPlugins = new Dictionary<string, string>();

        if (!File.Exists(loadorderPath)) return (loadorderPlugins, false);

        autoscanReport.AddRange(loadorderMessages);

        try
        {
            var loadorderData = await File.ReadAllLinesAsync(loadorderPath, Encoding.UTF8).ConfigureAwait(false);

            // Skip the header line (first line) of the loadorder.txt file
            if (loadorderData.Length > 1)
                foreach (var pluginEntry in loadorderData.Skip(1))
                {
                    var trimmedEntry = pluginEntry.Trim();
                    if (!string.IsNullOrEmpty(trimmedEntry))
                        loadorderPlugins.TryAdd(trimmedEntry, loadorderOrigin);
                }
        }
        catch (Exception e)
        {
            // Log file access error but continue execution
            var errorMsg = $"Error reading loadorder.txt: {e.Message}";
            autoscanReport.Add(errorMsg);
        }

        // Check if any plugins were loaded
        var pluginsLoaded = loadorderPlugins.Count > 0;

        return (loadorderPlugins, pluginsLoaded);
    }

    /// <summary>
    ///     Analyzes crash logs for relevant plugin references and updates the autoscan report with any matches found.
    ///     Direct port of Python plugin_match method.
    /// </summary>
    /// <param name="segmentCallstackLower">A list of lowercased strings representing the crash stack of a segment</param>
    /// <param name="crashlogPluginsLower">A set of lowercased plugin names derived from the crash log for matching purposes</param>
    /// <param name="autoscanReport">A mutable list to which the results of the analysis will be appended</param>
    private void PluginMatch(List<string> segmentCallstackLower, HashSet<string> crashlogPluginsLower,
        List<string> autoscanReport)
    {
        // Check if plugins list is empty - this indicates missing plugin information
        if (crashlogPluginsLower.Count == 0)
        {
            // Still check for XSE plugins in the call stack
            CheckXsePluginsInCallStack(segmentCallstackLower, autoscanReport);

            // If no XSE plugins found either, report no suspects
            if (autoscanReport.Count == 0) autoscanReport.Add("* COULDN'T FIND ANY PLUGIN SUSPECTS *\n\n");
            return;
        }

        // Pre-filter call stack lines that won't match
        var relevantLines = segmentCallstackLower.Where(line => !line.Contains("modified by:")).ToList();

        // Use Dictionary for counting instead of Counter
        var pluginsMatches = new Dictionary<string, int>();

        // Optimize the matching algorithm
        foreach (var line in relevantLines)
        foreach (var plugin in crashlogPluginsLower)
        {
            // Skip plugins that are in the ignore list
            if (_lowerPluginsIgnore.Contains(plugin)) continue;

            if (line.Contains(plugin)) pluginsMatches[plugin] = pluginsMatches.GetValueOrDefault(plugin, 0) + 1;
        }

        if (pluginsMatches.Count > 0)
        {
            autoscanReport.Add("The following PLUGINS were found in the CRASH STACK:\n");

            // Sort by count (descending) then by name for consistent output
            foreach (var (plugin, count) in pluginsMatches.OrderByDescending(x => x.Value).ThenBy(x => x.Key))
                autoscanReport.Add($"- {plugin} | {count}\n");

            autoscanReport.AddRange([
                "\n[Last number counts how many times each Plugin Suspect shows up in the crash log.]\n",
                $"These Plugins were caught by {GetCrashgenLogName()} and some of them might be responsible for this crash.\n",
                "You can try disabling these plugins and check if the game still crashes, though this method can be unreliable.\n\n"
            ]);
        }
        else
        {
            autoscanReport.Add("* COULDN'T FIND ANY PLUGIN SUSPECTS *\n\n");
        }
    }

    /// <summary>
    ///     Filters out ignored plugins from a dictionary of crash log plugins.
    ///     Direct port of Python filter_ignored_plugins method.
    /// </summary>
    /// <param name="crashlogPlugins">The dictionary containing plugin names as keys and their associated values</param>
    /// <returns>A dictionary of crash log plugins with the ignored plugins removed</returns>
    private Dictionary<string, string> FilterIgnoredPlugins(Dictionary<string, string> crashlogPlugins)
    {
        var ignorePluginsList =
            GetPluginsExcludeList();

        if (ignorePluginsList == null || ignorePluginsList.Count == 0) return crashlogPlugins;

        // Create lowercase version for comparison
        var crashlogPluginsLower = crashlogPlugins.Keys.ToDictionary(k => k.ToLower(), k => k);
        var ignorePluginsLower = ignorePluginsList.Select(p => p.ToLower()).ToHashSet();

        // Remove ignored plugins
        var filteredPlugins = new Dictionary<string, string>(crashlogPlugins);

        foreach (var signal in ignorePluginsLower)
            if (crashlogPluginsLower.TryGetValue(signal, out var originalKey))
                filteredPlugins.Remove(originalKey);

        return filteredPlugins;
    }

    private string GetCrashgenLogName()
    {
        var fallout4Yaml = _yamlSettings.LoadYaml<ClassicFallout4YamlV2>("CLASSIC Fallout4");
        return fallout4Yaml?.GameInfo?.CrashgenLogName ?? "Crash Logger";
    }

    private List<string> GetPluginsExcludeList()
    {
        var fallout4Yaml = _yamlSettings.LoadYaml<ClassicFallout4YamlV2>("CLASSIC Fallout4");
        return fallout4Yaml?.CrashlogPluginsExclude ?? new List<string>();
    }

    /// <summary>
    ///     Checks for XSE (F4SE) plugins in the call stack when regular plugin list is missing
    /// </summary>
    /// <param name="segmentCallstackLower">Lowercased call stack lines</param>
    /// <param name="autoscanReport">Report to add findings to</param>
    private void CheckXsePluginsInCallStack(List<string> segmentCallstackLower, List<string> autoscanReport)
    {
        // Common XSE plugin patterns to look for
        var xsePluginPatterns = new[]
        {
            ".dll+", "f4se_", "xse_", "_f4se", "_xse",
            "achievements.dll", "buffout4.dll", "x-cell-og.dll",
            "looksmenu.dll", "mcm.dll", "powerarmor.dll"
        };

        var foundXsePlugins = new HashSet<string>();

        foreach (var line in segmentCallstackLower)
        foreach (var pattern in xsePluginPatterns)
            if (line.Contains(pattern))
            {
                // Extract the DLL name from the line
                var parts = line.Split(new[] { ' ', '\t' }, StringSplitOptions.RemoveEmptyEntries);
                foreach (var part in parts)
                    if (part.Contains(".dll") && !foundXsePlugins.Contains(part))
                        foundXsePlugins.Add(part);
            }

        if (foundXsePlugins.Count > 0)
        {
            autoscanReport.Add("The following XSE PLUGINS were found in the CRASH STACK:\n");
            foreach (var plugin in foundXsePlugins.OrderBy(x => x)) autoscanReport.Add($"- {plugin}\n");
            autoscanReport.Add("\n[XSE plugins found in call stack when regular plugin list unavailable]\n\n");
        }
    }
}