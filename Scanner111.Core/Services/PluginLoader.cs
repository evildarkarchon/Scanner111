using System.Collections.Concurrent;
using System.Diagnostics;
using System.Text.RegularExpressions;
using Microsoft.Extensions.Logging;
using Scanner111.Core.Models;
using Scanner111.Core.Reporting;

namespace Scanner111.Core.Services;

/// <summary>
///     Thread-safe service for loading and processing plugin information from various sources.
///     Implements the functionality from the original Python PluginAnalyzer class.
/// </summary>
public sealed partial class PluginLoader : IPluginLoader
{
    private const string LoadOrderOrigin = "LO";
    private const string PluginStatusDll = "DLL";
    private const string PluginStatusUnknown = "???";
    private const string PluginLimitMarker = "[FF]";

    private readonly ILogger<PluginLoader> _logger;
    private readonly SemaphoreSlim _fileAccessLock = new(1, 1);

    // Thread-safe statistics tracking
    private readonly object _statsLock = new();
    private PluginLoadingStatistics _lastStatistics = new();

    public PluginLoader(ILogger<PluginLoader> logger)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <inheritdoc />
    public async Task<(Dictionary<string, string> plugins, bool pluginsLoaded, ReportFragment fragment)>
        LoadFromLoadOrderFileAsync(string? loadOrderPath = null, CancellationToken cancellationToken = default)
    {
        var stopwatch = Stopwatch.StartNew();
        var errors = new List<string>();
        var plugins = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        try
        {
            loadOrderPath ??= Path.Combine(Environment.CurrentDirectory, "loadorder.txt");
            _logger.LogDebug("Attempting to load plugins from loadorder.txt at: {Path}", loadOrderPath);

            if (!File.Exists(loadOrderPath))
            {
                _logger.LogDebug("Loadorder.txt file not found at: {Path}", loadOrderPath);
                var notFoundFragment = ReportFragment.CreateInfo(
                    "Load Order Status",
                    "No loadorder.txt file found in the main directory. Plugin detection will use crash log data.",
                    100);

                return (plugins, false, notFoundFragment);
            }

            // Use semaphore for thread-safe file access
            await _fileAccessLock.WaitAsync(cancellationToken).ConfigureAwait(false);
            try
            {
                var lines = new List<string>
                {
                    "* ✔️ LOADORDER.TXT FILE FOUND IN THE MAIN SCANNER111 FOLDER! *\n",
                    "Scanner111 will now ignore plugins in all crash logs and only detect plugins in this file.\n",
                    "[ To disable this functionality, simply remove loadorder.txt from your Scanner111 folder. ]\n\n"
                };

                string[] fileLines;
                try
                {
                    fileLines = await File.ReadAllLinesAsync(loadOrderPath, cancellationToken)
                        .ConfigureAwait(false);
                    _logger.LogInformation("Successfully read {LineCount} lines from loadorder.txt", fileLines.Length);
                }
                catch (Exception ex)
                {
                    var errorMsg = $"Error reading loadorder.txt: {ex.Message}";
                    _logger.LogError(ex, "Failed to read loadorder.txt file");
                    errors.Add(errorMsg);
                    lines.Add(errorMsg);

                    var errorFragment = ReportFragment.CreateWarning(
                        "Load Order File Error",
                        string.Join("", lines),
                        50);
                    return (plugins, false, errorFragment);
                }

                // Skip the header line (first line) of the loadorder.txt file
                if (fileLines.Length > 1)
                {
                    for (var i = 1; i < fileLines.Length; i++)
                    {
                        var pluginEntry = fileLines[i].Trim();
                        if (!string.IsNullOrWhiteSpace(pluginEntry) && !plugins.ContainsKey(pluginEntry))
                        {
                            plugins[pluginEntry] = LoadOrderOrigin;
                        }
                    }
                }

                var pluginsLoaded = plugins.Count > 0;
                _logger.LogInformation("Loaded {Count} plugins from loadorder.txt", plugins.Count);

                var fragment = ReportFragment.CreateInfo(
                    "Load Order Status",
                    string.Join("", lines),
                    10); // High priority

                return (plugins, pluginsLoaded, fragment);
            }
            finally
            {
                _fileAccessLock.Release();
            }
        }
        catch (OperationCanceledException)
        {
            _logger.LogDebug("Load order file reading was cancelled");
            throw;
        }
        catch (Exception ex)
        {
            var errorMsg = $"Unexpected error loading loadorder.txt: {ex.Message}";
            _logger.LogError(ex, "Unexpected error in LoadFromLoadOrderFileAsync");
            errors.Add(errorMsg);

            var errorFragment = ReportFragment.CreateError(
                "Load Order File Error",
                errorMsg,
                10);
            return (plugins, false, errorFragment);
        }
        finally
        {
            stopwatch.Stop();
            UpdateStatistics(plugins.Count, 0, 0, false, false, stopwatch.Elapsed, errors);
        }
    }

    /// <inheritdoc />
    public (Dictionary<string, string> plugins, bool limitTriggered, bool limitCheckDisabled) ScanPluginsFromLog(
        IEnumerable<string> segmentPlugins,
        Version gameVersion,
        Version currentVersion,
        ISet<string>? ignoredPlugins = null)
    {
        ArgumentNullException.ThrowIfNull(segmentPlugins);
        ArgumentNullException.ThrowIfNull(gameVersion);
        ArgumentNullException.ThrowIfNull(currentVersion);

        var stopwatch = Stopwatch.StartNew();
        var plugins = segmentPlugins.ToList();
        
        // Initialize return values outside try block for finally access
        var pluginMap = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        var pluginLimitTriggered = false;
        var limitCheckDisabled = false;

        try
        {
            // Early return for empty input
            if (plugins.Count == 0)
            {
                _logger.LogDebug("No segment plugins provided for scanning");
                return (new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase), false, false);
            }

            _logger.LogDebug("Scanning {Count} plugin entries from crash log", plugins.Count);

            // Version-specific behavior constants (these would come from YAML configuration in real implementation)
            var gameVersionBase = new Version(1, 0, 0); // Placeholder for game_version
            var gameVersionVR = new Version(1, 1, 0); // Placeholder for game_version_vr  
            var gameVersionNew = new Version(2, 0, 0); // Placeholder for game_version_new

            // Determine game version characteristics
            var isOriginalGame = gameVersion == gameVersionBase || gameVersion == gameVersionVR;
            var isNewGameCrashGenPre137 = gameVersion >= gameVersionNew && currentVersion < new Version(1, 37, 0);

            var regex = PluginSearchPattern();

            // Process each plugin entry
            foreach (var entry in plugins)
            {
                // Check for plugin limit markers
                if (entry.Contains(PluginLimitMarker, StringComparison.Ordinal))
                {
                    if (isOriginalGame)
                    {
                        pluginLimitTriggered = true;
                        _logger.LogWarning("Plugin limit marker detected for original game version");
                    }
                    else if (isNewGameCrashGenPre137)
                    {
                        limitCheckDisabled = true;
                        _logger.LogInformation("Plugin limit check disabled for new game version with pre-1.37 crash generator");
                    }
                }

                // Extract plugin information using regex
                var match = regex.Match(entry);
                if (!match.Success) continue;

                var pluginId = match.Groups[1].Value;
                var pluginName = match.Groups[3].Value.Trim();

                // Skip if plugin name is empty or already processed
                if (string.IsNullOrWhiteSpace(pluginName) || pluginMap.ContainsKey(pluginName))
                    continue;

                // Skip ignored plugins
                if (ignoredPlugins?.Contains(pluginName) == true)
                {
                    _logger.LogTrace("Skipping ignored plugin: {Plugin}", pluginName);
                    continue;
                }

                // Classify the plugin
                var classification = ClassifyPlugin(pluginId, pluginName);
                pluginMap[pluginName] = classification;

                _logger.LogTrace("Added plugin: {Name} -> {Classification}", pluginName, classification);
            }

            _logger.LogInformation(
                "Scanned plugins: Found={Count}, LimitTriggered={LimitTriggered}, LimitDisabled={LimitDisabled}",
                pluginMap.Count, pluginLimitTriggered, limitCheckDisabled);

            return (pluginMap, pluginLimitTriggered, limitCheckDisabled);
        }
        finally
        {
            stopwatch.Stop();
            UpdateStatistics(0, pluginMap?.Count ?? 0, ignoredPlugins?.Count ?? 0,
                pluginLimitTriggered, limitCheckDisabled, stopwatch.Elapsed, []);
        }
    }

    /// <inheritdoc />
    public IReadOnlyList<PluginInfo> CreatePluginInfoCollection(
        IDictionary<string, string>? loadOrderPlugins = null,
        IDictionary<string, string>? crashLogPlugins = null,
        ISet<string>? ignoredPlugins = null)
    {
        var plugins = new List<PluginInfo>();
        var processedNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        // Process load order plugins first (higher priority)
        if (loadOrderPlugins != null)
        {
            var index = 0;
            foreach (var (name, origin) in loadOrderPlugins)
            {
                if (processedNames.Add(name))
                {
                    var isIgnored = ignoredPlugins?.Contains(name) == true;
                    plugins.Add(PluginInfo.FromLoadOrder(name, index++, isIgnored));
                }
            }
        }

        // Process crash log plugins
        if (crashLogPlugins != null)
        {
            foreach (var (name, pluginId) in crashLogPlugins)
            {
                if (processedNames.Add(name))
                {
                    var isIgnored = ignoredPlugins?.Contains(name) == true;
                    plugins.Add(PluginInfo.FromCrashLog(name, pluginId, isIgnored));
                }
            }
        }

        _logger.LogDebug("Created {Count} plugin info objects", plugins.Count);
        return plugins.AsReadOnly();
    }

    /// <inheritdoc />
    public Dictionary<string, string> FilterIgnoredPlugins(
        IDictionary<string, string> plugins,
        ISet<string> ignoredPlugins)
    {
        ArgumentNullException.ThrowIfNull(plugins);
        ArgumentNullException.ThrowIfNull(ignoredPlugins);

        if (ignoredPlugins.Count == 0)
        {
            return new Dictionary<string, string>(plugins, StringComparer.OrdinalIgnoreCase);
        }

        var filtered = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        foreach (var (name, value) in plugins)
        {
            if (!ignoredPlugins.Contains(name))
            {
                filtered[name] = value;
            }
            else
            {
                _logger.LogTrace("Filtered out ignored plugin: {Plugin}", name);
            }
        }

        _logger.LogDebug("Filtered plugins: {Original} -> {Filtered} (removed {Removed})",
            plugins.Count, filtered.Count, plugins.Count - filtered.Count);

        return filtered;
    }

    /// <inheritdoc />
    public async Task<bool> ValidateLoadOrderFileAsync(string filePath, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(filePath);

        try
        {
            if (!File.Exists(filePath))
                return false;

            var lines = await File.ReadAllLinesAsync(filePath, cancellationToken).ConfigureAwait(false);
            
            // Basic validation: should have more than just a header line
            if (lines.Length <= 1)
                return false;

            // Check if lines contain typical plugin extensions
            var pluginExtensions = new[] { ".esm", ".esp", ".esl" };
            var validPluginCount = 0;

            // Skip header line
            for (var i = 1; i < Math.Min(lines.Length, 10); i++) // Check first 10 entries
            {
                var line = lines[i].Trim();
                if (pluginExtensions.Any(ext => line.EndsWith(ext, StringComparison.OrdinalIgnoreCase)))
                {
                    validPluginCount++;
                }
            }

            var isValid = validPluginCount > 0;
            _logger.LogDebug("LoadOrder validation: {IsValid} (found {Count} valid entries)", isValid, validPluginCount);
            
            return isValid;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error validating loadorder file: {Path}", filePath);
            return false;
        }
    }

    /// <inheritdoc />
    public PluginLoadingStatistics GetStatistics()
    {
        lock (_statsLock)
        {
            return _lastStatistics;
        }
    }

    /// <summary>
    ///     Classifies a plugin based on its ID and name.
    /// </summary>
    private static string ClassifyPlugin(string pluginId, string pluginName)
    {
        // Check if it's a DLL plugin first (DLL files always get "DLL" classification)
        if (pluginName.EndsWith(".dll", StringComparison.OrdinalIgnoreCase))
        {
            return PluginStatusDll;
        }

        // If we have a valid plugin ID, clean it up and return it
        if (!string.IsNullOrWhiteSpace(pluginId))
        {
            return pluginId.Replace(":", "", StringComparison.Ordinal);
        }

        // Unknown classification
        return PluginStatusUnknown;
    }

    /// <summary>
    ///     Updates the internal statistics for monitoring.
    /// </summary>
    private void UpdateStatistics(int loadOrderCount, int crashLogCount, int ignoredCount,
        bool limitTriggered, bool limitDisabled, TimeSpan duration, IList<string> errors)
    {
        lock (_statsLock)
        {
            _lastStatistics = new PluginLoadingStatistics
            {
                LoadOrderPluginCount = loadOrderCount,
                CrashLogPluginCount = crashLogCount,
                IgnoredPluginCount = ignoredCount,
                PluginLimitTriggered = limitTriggered,
                LimitCheckDisabled = limitDisabled,
                LastOperationDuration = duration,
                Errors = errors.ToList().AsReadOnly()
            };
        }
    }

    /// <summary>
    ///     Compiled regex for plugin pattern matching to improve performance.
    ///     Matches plugin entries in the format: [FE:XXX] or [XX] followed by plugin name.
    /// </summary>
    [GeneratedRegex(@"\s*\[(FE:([0-9A-F]{3})|[0-9A-F]{2})\]\s*(.+?(?:\.(?:es[pml]|dll))+)", 
                    RegexOptions.IgnoreCase | RegexOptions.Compiled)]
    private static partial Regex PluginSearchPattern();
}