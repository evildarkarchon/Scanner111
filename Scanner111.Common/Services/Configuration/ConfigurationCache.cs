using System.Collections.Concurrent;
using Scanner111.Common.Models.Analysis;
using Scanner111.Common.Models.Configuration;
using Scanner111.Common.Services.Analysis;

namespace Scanner111.Common.Services.Configuration;

/// <summary>
/// Thread-safe cache for configuration and database data.
/// </summary>
public class ConfigurationCache : IConfigurationCache
{
    private readonly IYamlConfigLoader _loader;
    private readonly ConcurrentDictionary<string, GameConfiguration> _gameConfigs = new();
    private readonly ConcurrentDictionary<string, GameSettings> _gameSettings = new();
    private readonly ConcurrentDictionary<string, SuspectPatterns> _suspectPatterns = new();
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
        if (_gameConfigs.TryGetValue(gameName, out var cachedConfig))
        {
            return cachedConfig;
        }

        var config = await LoadGameConfigAsync(gameName, ct);
        _gameConfigs.TryAdd(gameName, config);
        return config;
    }

    /// <inheritdoc/>
    public async Task<GameSettings> GetGameSettingsAsync(string gameName, CancellationToken ct = default)
    {
        if (_gameSettings.TryGetValue(gameName, out var cachedSettings))
        {
            return cachedSettings;
        }

        var settings = await LoadGameSettingsAsync(gameName, ct);
        _gameSettings.TryAdd(gameName, settings);
        return settings;
    }

    /// <inheritdoc/>
    public async Task<SuspectPatterns> GetSuspectPatternsAsync(string gameName, CancellationToken ct = default)
    {
        if (_suspectPatterns.TryGetValue(gameName, out var cachedPatterns))
        {
            return cachedPatterns;
        }

        var patterns = await LoadSuspectPatternsAsync(gameName, ct);
        _suspectPatterns.TryAdd(gameName, patterns);
        return patterns;
    }

    /// <inheritdoc/>
    public void Clear()
    {
        _gameConfigs.Clear();
        _gameSettings.Clear();
        _suspectPatterns.Clear();
    }

    private async Task<GameConfiguration> LoadGameConfigAsync(string gameName, CancellationToken ct)
    {
        // Mapped to: CLASSIC Data/databases/CLASSIC {GameName}.yaml
        var fileName = $"CLASSIC {gameName}.yaml";
        var path = Path.Combine(_baseDataPath, "databases", fileName);
        
        // For simplicity, assuming structure matches exactly or close enough for now.
        // In reality, we might need a specific DTO or custom mapping if the YAML structure is complex.
        return await _loader.LoadAsync<GameConfiguration>(path, ct);
    }

    private async Task<GameSettings> LoadGameSettingsAsync(string gameName, CancellationToken ct)
    {
         // Mapped to: CLASSIC Data/databases/CLASSIC {GameName}.yaml
         // Note: In the original python code, multiple things might be in one file or split.
         // Assuming for now we load from the same file or a specific settings file.
         // Based on Phase 3, GameSettings had: GameName, RecommendedSettings, LatestCrashLoggerVersion.
         
         // If these are in the same file as GameConfiguration, we might need to load it once and split it,
         // or just load the specific parts.
         // Let's assume a separate file or section for now, or load the same file if that's how it's structured.
         
         // Checking PORTING_ROADMAP, it says: 
         // Path.Combine("CLASSIC Data", "databases", $"CLASSIC {key}.yaml");
         
         var fileName = $"CLASSIC {gameName}.yaml";
         var path = Path.Combine(_baseDataPath, "databases", fileName);
         
         // We might need to load as dynamic if the structure doesn't match 1:1
         // But let's try loading as GameSettings directly if the YAML supports it.
         return await _loader.LoadAsync<GameSettings>(path, ct);
    }

    private async Task<SuspectPatterns> LoadSuspectPatternsAsync(string gameName, CancellationToken ct)
    {
        // Mapped to: CLASSIC Data/databases/CLASSIC {GameName}.yaml
        // The patterns (error/stack) are likely in the same main database file.
        
        var fileName = $"CLASSIC {gameName}.yaml";
        var path = Path.Combine(_baseDataPath, "databases", fileName);
        
        return await _loader.LoadAsync<SuspectPatterns>(path, ct);
    }
}
