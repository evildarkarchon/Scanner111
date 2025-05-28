using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using YamlDotNet.Serialization;
using YamlDotNet.Serialization.NamingConventions;
using Microsoft.Extensions.Logging;

namespace Scanner111.Services.Configuration;

/// <summary>
/// YAML-based configuration service with caching and thread-safety
/// </summary>
public class YamlConfigurationService : IConfigurationService, IDisposable
{
    private readonly ILogger<YamlConfigurationService> _logger;
    private readonly ISerializer _yamlSerializer;
    private readonly IDeserializer _yamlDeserializer;
    private readonly ReaderWriterLockSlim _cacheLock;

    // Cache for YAML content and file modification times
    private readonly ConcurrentDictionary<YamlStore, object?> _yamlCache;
    private readonly ConcurrentDictionary<YamlStore, DateTime> _fileModificationTimes;
    private readonly ConcurrentDictionary<YamlStore, string> _pathCache;

    // Static stores that don't change during program execution
    private static readonly HashSet<YamlStore> StaticStores = new()
    {
        YamlStore.Main,
        YamlStore.Game
    };

    // Settings that should ignore null values
    private static readonly HashSet<string> SettingsIgnoreNone = new()
    {
        "SCAN Custom Path",
        "MODS Folder Path",
        "Root_Folder_Game",
        "Root_Folder_Docs"
    };

    private bool _disposed;

    /// <summary>
    /// Provides functionality for managing and interacting with YAML-based configuration files.
    /// Includes caching mechanisms and thread-safe operations for reading and writing configuration data.
    /// </summary>
    public YamlConfigurationService(ILogger<YamlConfigurationService> logger)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));

        _yamlSerializer = new SerializerBuilder()
            .WithNamingConvention(UnderscoredNamingConvention.Instance)
            .Build();

        _yamlDeserializer = new DeserializerBuilder()
            .WithNamingConvention(UnderscoredNamingConvention.Instance)
            .IgnoreUnmatchedProperties()
            .Build();

        _cacheLock = new ReaderWriterLockSlim();
        _yamlCache = new ConcurrentDictionary<YamlStore, object?>();
        _fileModificationTimes = new ConcurrentDictionary<YamlStore, DateTime>();
        _pathCache = new ConcurrentDictionary<YamlStore, string>();
    }

    /// <summary>
    /// Retrieves the value associated with the specified key path in the given YAML store.
    /// This method is synchronous and may block if underlying async operations are time-consuming.
    /// </summary>
    /// <typeparam name="T">The type of the value to retrieve.</typeparam>
    /// <param name="store">The YAML store to query.</param>
    /// <param name="keyPath">The path of the key in the configuration hierarchy.</param>
    /// <returns>The value associated with the key path, or null if the key does not exist or cannot be found.</returns>
    public T? GetValue<T>(YamlStore store, string keyPath)
    {
        return GetValueAsync<T>(store, keyPath).GetAwaiter().GetResult();
    }

    /// <summary>
    /// Asynchronously retrieves a value from the specified YAML configuration store based on the provided key path.
    /// </summary>
    /// <typeparam name="T">The type of the value to be retrieved.</typeparam>
    /// <param name="store">The target YAML configuration store.</param>
    /// <param name="keyPath">The hierarchical key path for locating the desired value in the YAML structure.</param>
    /// <returns>A task representing the asynchronous operation. The task result contains the value of type <typeparamref name="T"/>, or default if the value was not found or could not be converted.</returns>
    public async Task<T?> GetValueAsync<T>(YamlStore store, string keyPath)
    {
        try
        {
            var yamlData = await LoadYamlAsync(store);
            if (yamlData == null) return default;

            var value = GetNestedValue(yamlData, keyPath);
            return ConvertValue<T>(value);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting value from {Store} at path {KeyPath}", store, keyPath);
            return default;
        }
    }

    /// <summary>
    /// Asynchronously sets a value in a specified YAML store at the given key path.
    /// Updates the relevant YAML file and the internal cache after modification.
    /// </summary>
    /// <param name="store">The YAML store where the value should be updated.</param>
    /// <param name="keyPath">The key path within the YAML store where the value will be set.</param>
    /// <param name="value">The value to be written at the specified key path.</param>
    /// <typeparam name="T">The type of the value being set.</typeparam>
    /// <returns>A task that represents the asynchronous operation. The task result contains a boolean indicating whether the value was successfully set.</returns>
    public async Task<bool> SetValueAsync<T>(YamlStore store, string keyPath, T value)
    {
        try
        {
            var filePath = GetPathForStore(store);
            var yamlData = await LoadYamlAsync(store) ?? new Dictionary<object, object>();

            SetNestedValue(yamlData, keyPath, value);

            var yamlContent = _yamlSerializer.Serialize(yamlData);
            await File.WriteAllTextAsync(filePath, yamlContent);

            // Update cache
            _yamlCache.TryUpdate(store, yamlData, _yamlCache.GetValueOrDefault(store));
            _fileModificationTimes.TryUpdate(store, File.GetLastWriteTime(filePath),
                _fileModificationTimes.GetValueOrDefault(store));

            _logger.LogDebug("Set value in {Store} at path {KeyPath}", store, keyPath);
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error setting value in {Store} at path {KeyPath}", store, keyPath);
            return false;
        }
    }

    /// <summary>
    /// Retrieves a setting from the YAML configuration. If the specified setting does not exist, a default value is returned.
    /// </summary>
    /// <typeparam name="T">The type of the setting value.</typeparam>
    /// <param name="keyPath">The key path to the setting within the YAML configuration.</param>
    /// <param name="defaultValue">The default value to return if the setting is not found. Defaults to the default value of the type if not specified.</param>
    /// <returns>The value of the setting if it exists; otherwise, the specified default value.</returns>
    public T GetSetting<T>(string keyPath, T defaultValue = default!)
    {
        var fullPath = keyPath.StartsWith("CLASSIC_Settings.") ? keyPath : $"CLASSIC_Settings.{keyPath}";
        var result = GetValue<T>(YamlStore.Settings, fullPath);
        return result ?? defaultValue;
    }

    /// <summary>
    /// Updates or creates a configuration setting under the "CLASSIC_Settings" namespace asynchronously.
    /// Uses the specified key path and value to persist the setting in the "Settings" YAML store.
    /// </summary>
    /// <typeparam name="T">The type of the value to be set.</typeparam>
    /// <param name="keyPath">The key path identifying the setting to be updated or created.</param>
    /// <param name="value">The value to associate with the specified key path.</param>
    /// <returns>
    /// A task that represents the asynchronous operation. The task result is a boolean indicating
    /// whether the operation succeeded.
    /// </returns>
    public async Task<bool> SetSettingAsync<T>(string keyPath, T value)
    {
        var fullPath = keyPath.StartsWith("CLASSIC_Settings.") ? keyPath : $"CLASSIC_Settings.{keyPath}";
        return await SetValueAsync(YamlStore.Settings, fullPath, value);
    }

    /// <summary>
    /// Clears the cached data for the specified YAML store or all caches if no store is specified.
    /// </summary>
    /// <param name="store">
    /// The optional YAML store to clear the cache for. If null, clears all cached data across all stores.
    /// </param>
    public void ClearCache(YamlStore? store = null)
    {
        _cacheLock.EnterWriteLock();
        try
        {
            if (store.HasValue)
            {
                _yamlCache.TryRemove(store.Value, out _);
                _fileModificationTimes.TryRemove(store.Value, out _);
                _logger.LogDebug("Cleared cache for store {Store}", store.Value);
            }
            else
            {
                _yamlCache.Clear();
                _fileModificationTimes.Clear();
                _logger.LogDebug("Cleared all cache");
            }
        }
        finally
        {
            _cacheLock.ExitWriteLock();
        }
    }

    /// <summary>
    /// Asynchronously preloads YAML files for a predefined set of static configuration stores,
    /// ensuring these files are loaded into cache. Logs debug and warning
    /// messages during the process to indicate success or failures for each store.
    /// </summary>
    /// <returns>A task that represents the asynchronous operation.</returns>
    public async Task PreloadStaticFilesAsync()
    {
        var tasks = StaticStores.Select(async store =>
        {
            try
            {
                await LoadYamlAsync(store);
                _logger.LogDebug("Preloaded static file for store {Store}", store);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to preload static file for store {Store}", store);
            }
        });

        await Task.WhenAll(tasks);
    }

    /// <summary>
    /// Determines if the specified YAML store exists on the file system.
    /// </summary>
    /// <param name="store">The YAML store to check for existence.</param>
    /// <returns>True if the store exists; otherwise, false.</returns>
    public bool StoreExists(YamlStore store)
    {
        var path = GetPathForStore(store);
        return File.Exists(path);
    }

    /// <summary>
    /// Ensures that default configuration files and directories exist in the application environment.
    /// This includes creating required directories and initializing default configuration files
    /// for settings and ignore lists if they are not already present.
    /// </summary>
    /// <returns>
    /// A task that represents the asynchronous operation of verifying and creating default files.
    /// </returns>
    public async Task EnsureDefaultFilesAsync()
    {
        try
        {
            // Ensure directories exist
            Directory.CreateDirectory("CLASSIC Data");
            Directory.CreateDirectory("CLASSIC Data/databases");

            // Create default settings file if it doesn't exist
            if (!StoreExists(YamlStore.Settings)) await CreateDefaultSettingsFileAsync();

            // Create default ignore file if it doesn't exist
            if (!StoreExists(YamlStore.Ignore)) await CreateDefaultIgnoreFileAsync();

            _logger.LogInformation("Ensured default configuration files exist");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error ensuring default configuration files");
        }
    }

    /// <summary>
    /// Asynchronously loads and parses a YAML file for the specified store.
    /// Uses caching to optimize file access and prevent redundant reads.
    /// Updates the cache only if the file has been modified or if it is not yet cached.
    /// </summary>
    /// <param name="store">The target YAML store from which the configuration will be loaded.</param>
    /// <returns>
    /// A task that represents the asynchronous operation.
    /// The task result contains a dictionary representing the parsed YAML data,
    /// or null if the file does not exist or an error occurs during loading.
    /// </returns>
    private async Task<Dictionary<object, object>?> LoadYamlAsync(YamlStore store)
    {
        var filePath = GetPathForStore(store);

        if (!File.Exists(filePath))
        {
            _logger.LogDebug("YAML file not found: {FilePath}", filePath);
            return null;
        }

        var isStatic = StaticStores.Contains(store);
        var lastWriteTime = File.GetLastWriteTime(filePath);

        // Check cache first
        if (_yamlCache.TryGetValue(store, out var cachedData))
        {
            if (isStatic) return cachedData as Dictionary<object, object>;

            // For dynamic files, check if file has been modified
            if (_fileModificationTimes.TryGetValue(store, out var cachedTime) &&
                cachedTime >= lastWriteTime)
                return cachedData as Dictionary<object, object>;
        }

        // Load from file
        _cacheLock.EnterWriteLock();
        try
        {
            var yamlContent = await File.ReadAllTextAsync(filePath);
            var yamlData = _yamlDeserializer.Deserialize<Dictionary<object, object>>(yamlContent)
                           ?? new Dictionary<object, object>();

            _yamlCache.AddOrUpdate(store, yamlData, (_, _) => yamlData);
            _fileModificationTimes.AddOrUpdate(store, lastWriteTime, (_, _) => lastWriteTime);

            _logger.LogDebug("Loaded YAML file: {FilePath} (Static: {IsStatic})", filePath, isStatic);
            return yamlData;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error loading YAML file: {FilePath}", filePath);
            return null;
        }
        finally
        {
            _cacheLock.ExitWriteLock();
        }
    }

    /// <summary>
    /// Retrieves the file path associated with the specified YAML store.
    /// Caches the paths for efficient subsequent retrievals.
    /// </summary>
    /// <param name="store">The <see cref="YamlStore"/> enumeration value specifying the desired YAML store.</param>
    /// <returns>A string representing the file path of the specified YAML store.</returns>
    private string GetPathForStore(YamlStore store)
    {
        return _pathCache.GetOrAdd(store, s =>
        {
            var currentDir = Environment.CurrentDirectory;
            var dataPath = Path.Combine(currentDir, "CLASSIC Data");

            return s switch
            {
                YamlStore.Main => Path.Combine(dataPath, "databases", "CLASSIC Main.yaml"),
                YamlStore.Settings => Path.Combine(currentDir, "CLASSIC Settings.yaml"),
                YamlStore.Ignore => Path.Combine(currentDir, "CLASSIC Ignore.yaml"),
                YamlStore.Game => Path.Combine(dataPath, "databases", "CLASSIC Fallout4.yaml"),
                YamlStore.GameLocal => Path.Combine(dataPath, "CLASSIC Fallout4 Local.yaml"),
                YamlStore.Test => Path.Combine(currentDir, "tests", "test_settings.yaml"),
                _ => throw new ArgumentOutOfRangeException(nameof(store), s, "Unknown YAML store")
            };
        });
    }

    /// <summary>
    /// Retrieves a nested value from a hierarchical dictionary structure based on a dot-delimited key path.
    /// </summary>
    /// <param name="data">The dictionary containing the hierarchical data to search.</param>
    /// <param name="keyPath">The dot-delimited string specifying the path to the nested value.</param>
    /// <returns>The value found at the specified key path, or null if the key does not exist.</returns>
    private static object? GetNestedValue(Dictionary<object, object> data, string keyPath)
    {
        var keys = keyPath.Split('.');
        object? current = data;

        foreach (var key in keys)
            if (current is not Dictionary<object, object> dict || !dict.TryGetValue(key, out current))
                return null;

        return current;
    }

    /// <summary>
    /// Inserts or updates a value at a specified nested path within a YAML data structure represented as a dictionary.
    /// If intermediary nodes in the path do not exist, they are created as dictionaries.
    /// </summary>
    /// <typeparam name="T">The type of the value to be inserted or updated.</typeparam>
    /// <param name="data">The dictionary representing the YAML data structure.</param>
    /// <param name="keyPath">The dot-separated path specifying where the value should be added or updated.</param>
    /// <param name="value">The value to set at the specified path.</param>
    /// <exception cref="InvalidOperationException">Thrown when an intermediate key in the path is not a dictionary.</exception>
    private static void SetNestedValue<T>(Dictionary<object, object> data, string keyPath, T value)
    {
        var keys = keyPath.Split('.');
        var current = data;

        for (var i = 0; i < keys.Length - 1; i++)
        {
            var key = keys[i];
            if (!current.TryGetValue(key, out var next))
            {
                next = new Dictionary<object, object>();
                current[key] = next;
            }

            if (next is not Dictionary<object, object> nextDict)
                throw new InvalidOperationException($"Cannot set nested value: key '{key}' is not a dictionary");

            current = nextDict;
        }

        current[keys[^1]] = value!;
    }

    /// <summary>
    /// Converts the specified object to the desired type, handling common data types and nullable scenarios.
    /// </summary>
    /// <typeparam name="T">The target type to which the value should be converted.</typeparam>
    /// <param name="value">The object to be converted. Can be null.</param>
    /// <returns>
    /// The converted value of the specified type, or the default value for the type if the conversion cannot be performed.
    /// </returns>
    private static T? ConvertValue<T>(object? value)
    {
        if (value == null) return default;

        try
        {
            var targetType = typeof(T);
            var nullableType = Nullable.GetUnderlyingType(targetType);

            if (nullableType != null) targetType = nullableType;

            if (targetType == typeof(string)) return (T)(object)value.ToString()!;

            if (targetType == typeof(bool))
            {
                if (value is bool boolValue) return (T)(object)boolValue;
                if (bool.TryParse(value.ToString(), out var parsed)) return (T)(object)parsed;
                return default;
            }

            if (targetType == typeof(int))
            {
                if (value is int intValue) return (T)(object)intValue;
                if (int.TryParse(value.ToString(), out var parsed)) return (T)(object)parsed;
                return default;
            }

            if (targetType == typeof(double))
            {
                if (value is double doubleValue) return (T)(object)doubleValue;
                if (double.TryParse(value.ToString(), out var parsed)) return (T)(object)parsed;
                return default;
            }

            if (targetType == typeof(List<string>))
            {
                if (value is IEnumerable<object> enumerable)
                {
                    var stringList = enumerable.Select(x => x?.ToString() ?? "").ToList();
                    return (T)(object)stringList;
                }

                return default;
            }

            // Handle other collection types and complex objects as needed
            return (T)Convert.ChangeType(value, targetType);
        }
        catch
        {
            return default;
        }
    }

    /// <summary>
    /// Asynchronously creates a default settings YAML file with predefined configuration values if it does not already exist.
    /// Populates the file with default settings and logs the output on completion.
    /// </summary>
    /// <returns>
    /// A task that represents the asynchronous operation of creating a default settings file.
    /// </returns>
    private async Task CreateDefaultSettingsFileAsync()
    {
        var defaultSettings = new Dictionary<object, object>
        {
            ["CLASSIC_Settings"] = new Dictionary<object, object>
            {
                ["Managed Game"] = "Fallout 4",
                ["Update Check"] = false,
                ["VR Mode"] = false,
                ["FCX Mode"] = false,
                ["Simplify Logs"] = false,
                ["Show Statistics"] = false,
                ["Show FormID Values"] = true,
                ["Move Unsolved Logs"] = true,
                ["INI Folder Path"] = "",
                ["MODS Folder Path"] = "",
                ["SCAN Custom Path"] = "",
                ["Audio Notifications"] = true,
                ["Update Source"] = "Both"
            }
        };

        var path = GetPathForStore(YamlStore.Settings);
        var yaml = _yamlSerializer.Serialize(defaultSettings);
        await File.WriteAllTextAsync(path, yaml);

        _logger.LogInformation("Created default settings file: {Path}", path);
    }

    /// <summary>
    /// Asynchronously creates a default ignore file with predefined settings for handling plugin and file exclusions for specific games.
    /// If the ignore file already exists, no action is taken.
    /// </summary>
    /// <returns>
    /// A task that represents the asynchronous operation of creating the default ignore file.
    /// Completes successfully when the default ignore file is created or when no action is necessary because the file already exists.
    /// </returns>
    private async Task CreateDefaultIgnoreFileAsync()
    {
        var defaultIgnore = new Dictionary<object, object>
        {
            ["CLASSIC_Ignore_Fallout4"] = new List<object>
            {
                "Example Plugin.esp",
                "Another Example.esl",
                "Example_DLL.dll"
            },
            ["CLASSIC_Ignore_SkyrimSE"] = new List<object>
            {
                "Example Plugin.esp",
                "Another Example.esl",
                "Example_DLL.dll"
            }
        };

        var path = GetPathForStore(YamlStore.Ignore);
        var yaml = _yamlSerializer.Serialize(defaultIgnore);
        await File.WriteAllTextAsync(path, yaml);

        _logger.LogInformation("Created default ignore file: {Path}", path);
    }

    public void Dispose()
    {
        if (!_disposed)
        {
            _cacheLock?.Dispose();
            _disposed = true;
        }
    }
}