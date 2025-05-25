using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Microsoft.Extensions.Logging;
using YamlDotNet.Serialization;
using YamlDotNet.Serialization.NamingConventions;

namespace Scanner111.ClassicLib;

/// <summary>
/// A utility class for managing and caching YAML settings.
/// 
/// This class provides mechanisms for working with YAML configurations, including
/// retrieving paths, loading YAML files with caching, and accessing or modifying
/// settings in a structured YAML format. It employs a singleton pattern through DI to ensure
/// a single instance across the application. Static YAML files (those that don't
/// change during program execution) are handled differently from dynamic YAML
/// files, with separate caching mechanisms for improved performance.
/// </summary>
public class YamlSettingsCache
{
    // Static YAML stores that won't change during program execution
    private static readonly HashSet<YamlStoreType> StaticYamlStores =
    [
        YamlStoreType.Main,
        YamlStoreType.Game
    ];

    // Cache of loaded YAML contents by file path
    private readonly Dictionary<string, Dictionary<string, object>> _cache = new();

    // Keeps track of file modification times to know when to reload dynamic files
    private readonly Dictionary<string, DateTime> _fileModTimes = new();
    private readonly IFileSystem _fileSystem;
    private readonly IGameRegistry _gameRegistry;
    private readonly ILogger<YamlSettingsCache> _logger;

    // Cache of file paths by YAML store type
    private readonly Dictionary<YamlStoreType, string> _pathCache = new();

    // Cache for settings to avoid repeated file access and parsing
    private readonly Dictionary<SettingsCacheKey, object?> _settingsCache = new();

    /// <summary>
    /// Initializes a new instance of the <see cref="YamlSettingsCache"/> class.
    /// </summary>
    public YamlSettingsCache(
        ILogger<YamlSettingsCache> logger,
        IGameRegistry gameRegistry,
        IFileSystem fileSystem)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _gameRegistry = gameRegistry ?? throw new ArgumentNullException(nameof(gameRegistry));
        _fileSystem = fileSystem ?? throw new ArgumentNullException(nameof(fileSystem));
    }

    /// <summary>
    /// Determines and returns the file path for a given YAML configuration type.
    /// </summary>
    /// <param name="yamlStore">The YAML store type.</param>
    /// <returns>The full path to the YAML file.</returns>
    /// <exception cref="NotImplementedException">Thrown when the store type is not supported.</exception>
    /// <exception cref="FileNotFoundException">Thrown when no valid file path could be determined.</exception>
    public string GetPathForStore(YamlStoreType yamlStore)
    {
        if (_pathCache.TryGetValue(yamlStore, out var cachedPath)) return cachedPath;

        var currentDirectory = Directory.GetCurrentDirectory();
        var dataPath = Path.Combine(currentDirectory, "CLASSIC Data");

        var yamlPath = yamlStore switch
        {
            YamlStoreType.Main => Path.Combine(dataPath, "databases", "CLASSIC Main.yaml"),
            YamlStoreType.Settings => Path.Combine(currentDirectory, "CLASSIC Settings.yaml"),
            YamlStoreType.Ignore => Path.Combine(currentDirectory, "CLASSIC Ignore.yaml"),
            YamlStoreType.Game => Path.Combine(dataPath, "databases", $"CLASSIC {_gameRegistry.GetGame()}.yaml"),
            YamlStoreType.GameLocal => Path.Combine(dataPath, $"CLASSIC {_gameRegistry.GetGame()} Local.yaml"),
            YamlStoreType.Test => Path.Combine(currentDirectory, "tests", "test_settings.yaml"),
            _ => throw new NotImplementedException($"YAML store type {yamlStore} is not supported")
        };

        if (yamlPath != currentDirectory)
        {
            _pathCache[yamlStore] = yamlPath;
            return yamlPath;
        }

        throw new FileNotFoundException($"No YAML file found for {yamlStore}");
    }

    /// <summary>
    /// Loads the content of a YAML file into a cache and retrieves it.
    /// </summary>
    /// <param name="yamlPath">The path to the YAML file.</param>
    /// <returns>The content of the YAML file as a dictionary.</returns>
    public Dictionary<string, object> LoadYaml(string yamlPath)
    {
        if (!_fileSystem.FileExists(yamlPath)) return new Dictionary<string, object>();

        // Determine if this is a static file
        var isStatic = StaticYamlStores.Any(store =>
            yamlPath.Equals(_pathCache.GetValueOrDefault(store), StringComparison.OrdinalIgnoreCase));

        if (isStatic)
        {
            // For static files, just load once
            if (_cache.ContainsKey(yamlPath))
                return _cache.GetValueOrDefault(yamlPath, new Dictionary<string, object>());
            _logger.LogDebug("Loading static YAML file: {YamlPath}", yamlPath);
            CacheFile(yamlPath);
        }
        else
        {
            // For dynamic files, check modification time
            var lastModTime = _fileSystem.GetLastWriteTime(yamlPath);

            if (!_fileModTimes.TryGetValue(yamlPath, out var cachedTime) || cachedTime != lastModTime)
            {
                // Update the file modification time
                _fileModTimes[yamlPath] = lastModTime;

                _logger.LogDebug("Loading dynamic YAML file: {YamlPath}", yamlPath);
                // Reload the YAML file
                CacheFile(yamlPath);
            }
        }

        return _cache.GetValueOrDefault(yamlPath, new Dictionary<string, object>());

        void CacheFile(string path)
        {
            var yaml = _fileSystem.ReadAllText(path);
            var deserializer = new DeserializerBuilder()
                .WithNamingConvention(CamelCaseNamingConvention.Instance)
                .Build();

            _cache[path] = deserializer.Deserialize<Dictionary<string, object>>(yaml);
        }
    }

    /// <summary>
    /// Retrieves or updates a setting from a nested YAML data structure.
    /// </summary>
    /// <typeparam name="T">The expected type of the setting value.</typeparam>
    /// <param name="yamlStore">The YAML store from which the setting is retrieved or updated.</param>
    /// <param name="keyPath">The dot-delimited path specifying the location of the setting within the YAML structure.</param>
    /// <param name="newValue">The new value to update the setting with. If null, the method operates as a read.</param>
    /// <returns>The existing or updated setting value if successful, otherwise default(T).</returns>
    public T? GetSetting<T>(YamlStoreType yamlStore, string keyPath, T? newValue = default)
    {
        // If this is a read operation for a static store, check cache first
        var cacheKey = new SettingsCacheKey(yamlStore, keyPath, typeof(T));

        if (EqualityComparer<T>.Default.Equals(newValue, default) &&
            StaticYamlStores.Contains(yamlStore) &&
            _settingsCache.TryGetValue(cacheKey, out var cachedValue))
            return cachedValue != null ? (T)cachedValue : default;

        var yamlPath = GetPathForStore(yamlStore);

        // Load YAML with caching logic
        var data = LoadYaml(yamlPath);
        var keys = keyPath.Split('.');

        // Navigate to the parent container of the setting
        var currentContainer = data;
        for (var i = 0; i < keys.Length - 1; i++)
        {
            var key = keys[i];

            // If key doesn't exist in the container, create a new dictionary for it
            if (!currentContainer.ContainsKey(key)) currentContainer[key] = new Dictionary<string, object>();

            // Get the next level container
            var nextValue = currentContainer[key];

            // If the next value isn't a dictionary, we can't navigate further
            if (nextValue is not Dictionary<string, object> nextContainer)
            {
                _logger.LogError("Invalid path structure for {KeyPath} in {YamlStore}", keyPath, yamlStore);
                return default;
            }

            currentContainer = nextContainer;
        }

        // The last key in the path is the actual setting to get/set
        var settingKey = keys[^1];

        // If newValue is provided, update the value
        if (!EqualityComparer<T>.Default.Equals(newValue, default))
        {
            // If this is a static file and we're trying to modify it, warn about this
            if (StaticYamlStores.Contains(yamlStore))
                _logger.LogWarning("Attempting to modify static YAML store {YamlStore} at {KeyPath}",
                    yamlStore, keyPath);

            currentContainer[settingKey] =
                newValue!; // Add null-forgiving operator as we've checked newValue is not default

            // Write changes back to the YAML file
            var serializer = new SerializerBuilder()
                .WithNamingConvention(CamelCaseNamingConvention.Instance)
                .Build();

            var yaml = serializer.Serialize(data);
            _fileSystem.WriteAllText(yamlPath, yaml);

            // Update the cache
            _cache[yamlPath] = data;

            // Clear any cached results for this path
            _settingsCache.Remove(cacheKey);

            return newValue;
        }

        // Retrieve the value
        if (currentContainer.TryGetValue(settingKey, out var settingValue))
            try
            {
                // Try to convert the value to the requested type
                T? typedValue;
                if (typeof(T) == typeof(string) && settingValue is not string)
                    typedValue = (T)(object)settingValue.ToString()!;
                else if (settingValue is T value)
                    typedValue = value;
                else
                    // Try to convert to the target type
                    typedValue = (T)Convert.ChangeType(settingValue, typeof(T));

                // Cache the result for static stores
                if (StaticYamlStores.Contains(yamlStore)) _settingsCache[cacheKey] = typedValue;

                return typedValue;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to convert value for {KeyPath} to type {Type}", keyPath, typeof(T).Name);
                return default;
            }
        else if (!Constants.SettingsIgnoreNone.Contains(settingKey))
            Console.WriteLine($"❌ ERROR (yaml_settings) : Trying to grab a null value for : '{keyPath}'");

        return default;
    }

    // Private record for caching settings
    private record SettingsCacheKey(YamlStoreType YamlStore, string KeyPath, Type ValueType);
}