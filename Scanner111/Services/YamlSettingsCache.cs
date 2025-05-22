using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using Microsoft.Extensions.Logging;
using YamlDotNet.Serialization;
using YamlDotNet.Serialization.NamingConventions;

namespace Scanner111.Services;

/// <summary>
///     Implementation of the YAML settings cache service.
/// </summary>
public class YamlSettingsCache : IYamlSettingsCache
{
    // Singleton instance
    private static YamlSettingsCache? _instance;
    private static readonly Lock LockObject = new();
    private readonly Dictionary<string, Dictionary<string, object>> _cache = new();

    private readonly IDeserializer _deserializer;
    private readonly Dictionary<string, DateTime> _fileModTimes = new();
    private readonly IGameContextService _gameContextService;
    private readonly ILogger<YamlSettingsCache> _logger;
    private readonly Dictionary<YamlStore, string> _pathCache = new();
    private readonly ISerializer _serializer;
    private readonly Dictionary<(YamlStore, string, Type), object> _settingsCache = new();

    /// <summary>
    ///     Constructor made private to enforce singleton pattern.
    /// </summary>
    private YamlSettingsCache(ILogger<YamlSettingsCache> logger, IGameContextService gameContextService)
    {
        _logger = logger;
        _gameContextService = gameContextService;

        // Setup YamlDotNet serializer/deserializer
        _deserializer = new DeserializerBuilder()
            .WithNamingConvention(CamelCaseNamingConvention.Instance)
            .Build();

        _serializer = new SerializerBuilder()
            .WithNamingConvention(CamelCaseNamingConvention.Instance)
            .Build();
    }

    /// <summary>
    ///     Gets the singleton instance of YamlSettingsCache.
    /// </summary>
    public static YamlSettingsCache Instance
    {
        get
        {
            if (_instance == null)
                lock (LockObject)
                {
                    if (_instance == null)
                        throw new InvalidOperationException(
                            "YamlSettingsCache not initialized. Call Initialize() first.");
                }

            return _instance;
        }
    }

    /// <inheritdoc />
    public string GetPathForStore(YamlStore yamlStore)
    {
        if (_pathCache.TryGetValue(yamlStore, out var path)) return path;

        string yamlPath;
        var dataPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "CLASSIC Data/");

        switch (yamlStore)
        {
            case YamlStore.Main:
                yamlPath = Path.Combine(dataPath, "databases/CLASSIC Main.yaml");
                break;
            case YamlStore.Settings:
                yamlPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "CLASSIC Settings.yaml");
                break;
            case YamlStore.Ignore:
                yamlPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "CLASSIC Ignore.yaml");
                break;
            case YamlStore.Game:
                yamlPath = Path.Combine(dataPath, $"databases/CLASSIC {_gameContextService.GetCurrentGame()}.yaml");
                break;
            case YamlStore.GameLocal:
                yamlPath = Path.Combine(dataPath, $"CLASSIC {_gameContextService.GetCurrentGame()} Local.yaml");
                break;
            case YamlStore.Test:
                yamlPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "tests/test_settings.yaml");
                break;
            default:
                throw new NotImplementedException($"YAML store {yamlStore} not implemented");
        }

        if (!string.IsNullOrEmpty(yamlPath))
            _pathCache[yamlStore] = yamlPath;
        else
            throw new FileNotFoundException($"No YAML file found for {yamlStore}");

        return yamlPath;
    }

    /// <inheritdoc />
    public Dictionary<string, object> LoadYaml(string yamlPath)
    {
        if (!File.Exists(yamlPath)) return new Dictionary<string, object>();

        // Determine if this is a static file
        var isStatic = YamlConstants.StaticYamlStores.Any(store =>
            yamlPath == GetPathForStore(store));

        // For static files, just load once
        if (isStatic && _cache.TryGetValue(yamlPath, out var cachedData)) return cachedData;

        // For dynamic files, check modification time
        if (!isStatic)
        {
            var lastModTime = File.GetLastWriteTime(yamlPath);
            if (_fileModTimes.TryGetValue(yamlPath, out var cachedModTime) &&
                cachedModTime == lastModTime &&
                _cache.TryGetValue(yamlPath, out cachedData))
                return cachedData;

            // Update the file modification time
            _fileModTimes[yamlPath] = lastModTime;
        }

        try
        {
            // Load and cache the YAML file
            _logger.LogDebug($"Loading {(isStatic ? "static" : "dynamic")} YAML file: {yamlPath}");

            var yamlContent = File.ReadAllText(yamlPath);
            var data = _deserializer.Deserialize<Dictionary<string, object>>(yamlContent);

            _cache[yamlPath] = data;
            return data;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, $"Error loading YAML file: {yamlPath}");
            return new Dictionary<string, object>();
        }
    }

    /// <inheritdoc />
    public T? GetSetting<T>(YamlStore yamlStore, string keyPath, T? newValue = default)
    {
        // If this is a read operation for a static store, check cache first
        var cacheKey = (yamlStore, keyPath, typeof(T));
        if (EqualityComparer<T>.Default.Equals(newValue, default) &&
            YamlConstants.StaticYamlStores.Contains(yamlStore) &&
            _settingsCache.TryGetValue(cacheKey, out var cachedValue))
            return (T?)cachedValue;

        var yamlPath = GetPathForStore(yamlStore);
        var data = LoadYaml(yamlPath);

        var keys = keyPath.Split('.');

        // Navigate to the container that should hold our setting
        var current = data;
        for (var i = 0; i < keys.Length - 1; i++)
        {
            var key = keys[i];

            if (!current.TryGetValue(key, out var next))
            {
                // If we're writing, create the missing structures
                if (!EqualityComparer<T>.Default.Equals(newValue, default))
                {
                    next = new Dictionary<string, object>();
                    current[key] = next;
                }
                else
                {
                    return default;
                }
            }

            if (next is not Dictionary<string, object> nextDict)
            {
                if (!EqualityComparer<T>.Default.Equals(newValue, default))
                {
                    // Override existing value with new dictionary
                    nextDict = new Dictionary<string, object>();
                    current[key] = nextDict;
                }
                else
                {
                    _logger.LogError($"Invalid path structure for {keyPath} in {yamlStore}");
                    return default;
                }
            }

            current = nextDict;
        }

        var lastKey = keys[^1];

        // If new_value is provided, update the value
        if (!EqualityComparer<T>.Default.Equals(newValue, default))
        {
            // If this is a static file and we're trying to modify it, warn about this
            if (YamlConstants.StaticYamlStores.Contains(yamlStore))
                _logger.LogWarning($"Attempting to modify static YAML store {yamlStore} at {keyPath}");

            current[lastKey] = newValue!;

            // Write changes back to the YAML file
            try
            {
                // Ensure directory exists
                var directory = Path.GetDirectoryName(yamlPath);
                if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
                    Directory.CreateDirectory(directory);

                var yaml = _serializer.Serialize(data);
                File.WriteAllText(yamlPath, yaml);

                // Update the cache
                _cache[yamlPath] = data;

                // Clear any cached results for this path
                _settingsCache.Remove(cacheKey);

                return newValue;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error writing to YAML file: {yamlPath}");
                return default;
            }
        }

        // Get the value
        if (current.TryGetValue(lastKey, out var value))
            // Try to convert to requested type
            try
            {
                T? typedValue;

                if (value is T directValue)
                    typedValue = directValue;
                else if (typeof(T) == typeof(string))
                    typedValue = (T)(object)value.ToString()!;
                else
                    // Try conversion using Convert
                    typedValue = (T)Convert.ChangeType(value, typeof(T));

                // Cache the result for static stores
                if (YamlConstants.StaticYamlStores.Contains(yamlStore)) _settingsCache[cacheKey] = typedValue;

                return typedValue;
            }
            catch
            {
                _logger.LogError($"Failed to convert value at {keyPath} to {typeof(T).Name}");
                return default;
            }

        if (!YamlConstants.SettingsIgnoreNone.Contains(lastKey))
            _logger.LogError($"Trying to grab a None value for: '{keyPath}'");

        return default;
    }

    /// <summary>
    ///     Initializes the singleton instance with required dependencies.
    /// </summary>
    /// <param name="logger">The logger.</param>
    /// <param name="gameContextService">The game context service.</param>
    /// <returns>The singleton instance.</returns>
    public static YamlSettingsCache Initialize(ILogger<YamlSettingsCache> logger,
        IGameContextService gameContextService)
    {
        lock (LockObject)
        {
            _instance ??= new YamlSettingsCache(logger, gameContextService);
            return _instance;
        }
    }
}