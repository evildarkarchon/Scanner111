using System.Collections.Concurrent;
using Scanner111.Common.Models.Analysis;
using Scanner111.Common.Models.Configuration;
using Scanner111.Common.Services.Analysis;

namespace Scanner111.Common.Services.Configuration;

/// <summary>
/// Thread-safe cache for configuration and database data.
/// Uses Lazy&lt;Task&lt;T&gt;&gt; pattern to prevent duplicate concurrent loads.
/// </summary>
public class ConfigurationCache : IConfigurationCache
{
    private readonly IYamlConfigLoader _loader;
    private readonly ConcurrentDictionary<string, Lazy<Task<GameConfiguration>>> _gameConfigs = new();
    private readonly ConcurrentDictionary<string, Lazy<Task<GameSettings>>> _gameSettings = new();
    private readonly ConcurrentDictionary<string, Lazy<Task<SuspectPatterns>>> _suspectPatterns = new();
    private readonly ConcurrentDictionary<string, Lazy<Task<ModConfiguration>>> _modConfigs = new();
    private readonly string _baseDataPath;

    /// <summary>
    /// Initializes a new instance of the <see cref="ConfigurationCache"/> class.
    /// </summary>
    /// <param name="loader">The YAML configuration loader.</param>
    /// <param name="baseDataPath">The base path for configuration files. Defaults to "CLASSIC Data".</param>
    public ConfigurationCache(IYamlConfigLoader loader, string baseDataPath = "CLASSIC Data")
    {
        _loader = loader ?? throw new ArgumentNullException(nameof(loader));
        _baseDataPath = baseDataPath;
    }

    /// <inheritdoc/>
    public async Task<GameConfiguration> GetGameConfigAsync(string gameName, CancellationToken ct = default)
    {
        var lazy = _gameConfigs.GetOrAdd(
            gameName,
            key => new Lazy<Task<GameConfiguration>>(() => LoadGameConfigAsync(key, ct)));

        return await lazy.Value.ConfigureAwait(false);
    }

    /// <inheritdoc/>
    public async Task<GameSettings> GetGameSettingsAsync(string gameName, CancellationToken ct = default)
    {
        var lazy = _gameSettings.GetOrAdd(
            gameName,
            key => new Lazy<Task<GameSettings>>(() => LoadGameSettingsAsync(key, ct)));

        return await lazy.Value.ConfigureAwait(false);
    }

    /// <inheritdoc/>
    public async Task<SuspectPatterns> GetSuspectPatternsAsync(string gameName, CancellationToken ct = default)
    {
        var lazy = _suspectPatterns.GetOrAdd(
            gameName,
            key => new Lazy<Task<SuspectPatterns>>(() => LoadSuspectPatternsAsync(key, ct)));

        return await lazy.Value.ConfigureAwait(false);
    }

    /// <inheritdoc/>
    public async Task<ModConfiguration> GetModConfigurationAsync(string gameName, CancellationToken ct = default)
    {
        var lazy = _modConfigs.GetOrAdd(
            gameName,
            key => new Lazy<Task<ModConfiguration>>(() => LoadModConfigurationAsync(key, ct)));

        return await lazy.Value.ConfigureAwait(false);
    }

    /// <inheritdoc/>
    public void Clear()
    {
        _gameConfigs.Clear();
        _gameSettings.Clear();
        _suspectPatterns.Clear();
        _modConfigs.Clear();
    }

    private async Task<GameConfiguration> LoadGameConfigAsync(string gameName, CancellationToken ct)
    {
        var fileName = $"CLASSIC {gameName}.yaml";
        var path = Path.Combine(_baseDataPath, "databases", fileName);
        return await _loader.LoadAsync<GameConfiguration>(path, ct).ConfigureAwait(false);
    }

    private async Task<GameSettings> LoadGameSettingsAsync(string gameName, CancellationToken ct)
    {
         var fileName = $"CLASSIC {gameName}.yaml";
         var path = Path.Combine(_baseDataPath, "databases", fileName);
         return await _loader.LoadAsync<GameSettings>(path, ct).ConfigureAwait(false);
    }

    private async Task<SuspectPatterns> LoadSuspectPatternsAsync(string gameName, CancellationToken ct)
    {
        var fileName = $"CLASSIC {gameName}.yaml";
        var path = Path.Combine(_baseDataPath, "databases", fileName);
        return await _loader.LoadAsync<SuspectPatterns>(path, ct).ConfigureAwait(false);
    }

    private async Task<ModConfiguration> LoadModConfigurationAsync(string gameName, CancellationToken ct)
    {
        var fileName = $"CLASSIC {gameName}.yaml";
        var path = Path.Combine(_baseDataPath, "databases", fileName);

        var dynamic = await _loader.LoadDynamicAsync(path, ct).ConfigureAwait(false);

        return new ModConfiguration
        {
            FrequentCrashMods = ExtractDictionary(dynamic, "Mods_FREQ"),
            ConflictingMods = ExtractDictionary(dynamic, "Mods_CONF"),
            SolutionMods = ExtractDictionary(dynamic, "Mods_SOLU"),
            OpcPatchedMods = ExtractDictionary(dynamic, "Mods_OPC2"),
            ImportantMods = ExtractDictionary(dynamic, "Mods_CORE")
        };
    }

    private static Dictionary<string, string> ExtractDictionary(
        Dictionary<string, object>? dynamic,
        string key)
    {
        if (dynamic == null || !dynamic.TryGetValue(key, out var value))
        {
            return new Dictionary<string, string>();
        }

        if (value is Dictionary<string, object> dict)
        {
            return dict.ToDictionary(
                kvp => kvp.Key,
                kvp => kvp.Value?.ToString() ?? string.Empty,
                StringComparer.OrdinalIgnoreCase);
        }

        if (value is Dictionary<object, object> objDict)
        {
            return objDict.ToDictionary(
                kvp => kvp.Key?.ToString() ?? string.Empty,
                kvp => kvp.Value?.ToString() ?? string.Empty,
                StringComparer.OrdinalIgnoreCase);
        }

        return new Dictionary<string, string>();
    }
}
