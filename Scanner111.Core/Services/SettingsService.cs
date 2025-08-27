using Microsoft.Extensions.Logging;
using Scanner111.Core.Analysis;
using Scanner111.Core.Configuration;
using Scanner111.Core.Models;

namespace Scanner111.Core.Services;

/// <summary>
///     Service for loading and managing crash generator settings.
///     Thread-safe for concurrent access.
/// </summary>
public interface ISettingsService
{
    /// <summary>
    ///     Loads crash generator settings from the context.
    /// </summary>
    Task<CrashGenSettings> LoadCrashGenSettingsAsync(
        AnalysisContext context,
        CancellationToken cancellationToken = default);

    /// <summary>
    ///     Loads mod detection settings from the context.
    /// </summary>
    Task<ModDetectionSettings> LoadModDetectionSettingsAsync(
        AnalysisContext context,
        CancellationToken cancellationToken = default);
}

/// <summary>
///     Implementation of settings service with caching support.
/// </summary>
public sealed class SettingsService : ISettingsService
{
    private readonly ILogger<SettingsService> _logger;
    private readonly IAsyncYamlSettingsCore _yamlCore;

    public SettingsService(
        ILogger<SettingsService> logger,
        IAsyncYamlSettingsCore yamlCore)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _yamlCore = yamlCore ?? throw new ArgumentNullException(nameof(yamlCore));
    }

    /// <inheritdoc />
    public async Task<CrashGenSettings> LoadCrashGenSettingsAsync(
        AnalysisContext context,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(context);

        try
        {
            // Check if settings are already in context's shared data
            if (context.TryGetSharedData<CrashGenSettings>("CrashGenSettings", out var cached))
            {
                _logger.LogDebug("Using cached CrashGen settings from context");
                return cached!;
            }

            // Load from YAML settings core
            var stores = new[] { YamlStore.Main, YamlStore.Settings };
            var allData = await _yamlCore.LoadMultipleStoresAsync(stores, cancellationToken)
                .ConfigureAwait(false);

            // Try to find the data we need from any available store
            Dictionary<string, object?> yamlData = null;
            foreach (var storeData in allData.Values)
                if (storeData != null && storeData.Count > 0)
                {
                    yamlData = storeData;
                    break;
                }

            yamlData ??= new Dictionary<string, object?>();

            // Extract crashgen configuration section
            var crashGenConfig = new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase);
            var crashGenName = "Buffout"; // Default name
            Version? version = null;

            if (yamlData != null)
            {
                // Look for Buffout/CrashGen configuration sections
                if (yamlData.TryGetValue("Buffout", out var buffoutSection) &&
                    buffoutSection is IDictionary<object, object> buffoutDict)
                {
                    crashGenName = "Buffout";
                    crashGenConfig = ConvertToStringDictionary(buffoutDict);
                }
                else if (yamlData.TryGetValue("CrashGen", out var crashGenSection) &&
                         crashGenSection is IDictionary<object, object> crashGenDict)
                {
                    crashGenName = "CrashGen";
                    crashGenConfig = ConvertToStringDictionary(crashGenDict);
                }

                // Try to extract version
                if (yamlData.TryGetValue("CrashGenVersion", out var versionObj) &&
                    versionObj != null)
                    if (Version.TryParse(versionObj.ToString(), out var parsedVersion))
                        version = parsedVersion;
            }

            // Look for ignored settings
            var ignoredSettings = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            if (yamlData?.TryGetValue("IgnoredSettings", out var ignored) == true &&
                ignored is IEnumerable<object> ignoredList)
                foreach (var item in ignoredList)
                    if (item != null)
                        ignoredSettings.Add(item.ToString()!);

            var settings = CrashGenSettings.FromDictionary(
                crashGenConfig,
                crashGenName,
                version,
                ignoredSettings);

            // Cache in context
            context.SetSharedData("CrashGenSettings", settings);

            _logger.LogInformation(
                "Loaded {Name} settings: Version={Version}, MemoryManager={MemMgr}, Achievements={Achievements}",
                settings.CrashGenName,
                settings.Version?.ToString() ?? "Unknown",
                settings.MemoryManager,
                settings.Achievements);

            return settings;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to load CrashGen settings");
            // Return default settings on error
            return new CrashGenSettings();
        }
    }

    /// <inheritdoc />
    public async Task<ModDetectionSettings> LoadModDetectionSettingsAsync(
        AnalysisContext context,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(context);

        try
        {
            // Check if settings are already in context's shared data
            if (context.TryGetSharedData<ModDetectionSettings>("ModDetectionSettings", out var cached))
            {
                _logger.LogDebug("Using cached mod detection settings from context");
                return cached!;
            }

            // Load from YAML settings core
            var stores = new[] { YamlStore.Main, YamlStore.Settings };
            var allData = await _yamlCore.LoadMultipleStoresAsync(stores, cancellationToken)
                .ConfigureAwait(false);

            // Try to find the data we need from any available store
            Dictionary<string, object?> yamlData = null;
            foreach (var storeData in allData.Values)
                if (storeData != null && storeData.Count > 0)
                {
                    yamlData = storeData;
                    break;
                }

            yamlData ??= new Dictionary<string, object?>();

            var xseModules = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var crashLogPlugins = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            bool? fcxMode = null;
            var metadata = new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase);

            if (yamlData != null)
            {
                // Extract XSE modules
                if (yamlData.TryGetValue("XSEPlugins", out var xseObj))
                {
                    if (xseObj is IEnumerable<object> xseList)
                    {
                        foreach (var item in xseList)
                            if (item != null)
                                xseModules.Add(item.ToString()!);
                    }
                    else if (xseObj is IDictionary<object, object> xseDict)
                    {
                        foreach (var key in xseDict.Keys)
                            if (key != null)
                                xseModules.Add(key.ToString()!);
                    }
                }

                // Extract crash log plugins
                if (yamlData.TryGetValue("Plugins", out var pluginsObj) &&
                    pluginsObj is IDictionary<object, object> pluginsDict)
                    foreach (var kvp in pluginsDict)
                        if (kvp.Key != null && kvp.Value != null)
                            crashLogPlugins[kvp.Key.ToString()!] = kvp.Value.ToString()!;

                // Extract FCX mode
                if (yamlData.TryGetValue("FCXMode", out var fcxObj))
                    fcxMode = fcxObj switch
                    {
                        bool b => b,
                        string s when bool.TryParse(s, out var parsed) => parsed,
                        int i => i != 0,
                        _ => null
                    };

                // Extract metadata
                if (yamlData.TryGetValue("ModMetadata", out var metaObj) &&
                    metaObj is IDictionary<object, object> metaDict)
                    metadata = ConvertToStringDictionary(metaDict);
            }

            var settings = ModDetectionSettings.FromDetectionData(
                xseModules,
                crashLogPlugins,
                fcxMode,
                metadata);

            // Cache in context
            context.SetSharedData("ModDetectionSettings", settings);

            _logger.LogInformation(
                "Loaded mod detection settings: XSE Modules={XseCount}, Plugins={PluginCount}, FCX={Fcx}",
                settings.XseModules.Count,
                settings.CrashLogPlugins.Count,
                settings.FcxMode);

            return settings;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to load mod detection settings");
            // Return default settings on error
            return new ModDetectionSettings();
        }
    }

    private static Dictionary<string, object> ConvertToStringDictionary(IDictionary<object, object> source)
    {
        var result = new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase);
        foreach (var kvp in source)
            if (kvp.Key != null)
                result[kvp.Key.ToString()!] = kvp.Value ?? string.Empty;

        return result;
    }
}