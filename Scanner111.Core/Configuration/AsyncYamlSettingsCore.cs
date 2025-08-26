using System.Collections.Concurrent;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Scanner111.Core.IO;
using YamlDotNet.Serialization;
using YamlDotNet.Serialization.NamingConventions;

namespace Scanner111.Core.Configuration;

/// <summary>
///     Async-first YAML settings management core with thread-safe caching and concurrency control.
/// </summary>
public class AsyncYamlSettingsCore : IAsyncYamlSettingsCore
{
    // Thread-safe caching structures
    private readonly ConcurrentDictionary<string, YamlCacheEntry> _cache = new();

    // Current game for path resolution (would typically come from a game service)
    private readonly string _currentGame;
    private readonly IDeserializer _deserializer;
    private readonly IFileIoCore _fileIo;
    private readonly ConcurrentDictionary<string, SemaphoreSlim> _fileLocks = new();

    // Global lock for managing file-specific locks
    private readonly SemaphoreSlim _globalLock = new(1, 1);
    private readonly ILogger<AsyncYamlSettingsCore> _logger;
    private readonly YamlSettingsOptions _options;
    private readonly ConcurrentDictionary<YamlStore, string> _pathCache = new();
    private readonly ISerializer _serializer;
    private readonly ConcurrentDictionary<(YamlStore, string, Type), object?> _settingsCache = new();

    // Performance metrics
    private long _cacheHits;
    private long _cacheMisses;

    // Disposal tracking
    private bool _disposed;
    private long _fileReads;
    private long _fileWrites;

    public AsyncYamlSettingsCore(
        IFileIoCore fileIo,
        ILogger<AsyncYamlSettingsCore> logger,
        IOptions<YamlSettingsOptions> options)
    {
        _fileIo = fileIo ?? throw new ArgumentNullException(nameof(fileIo));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _options = options?.Value ?? throw new ArgumentNullException(nameof(options));

        _currentGame = _options.DefaultGame;

        // Configure YamlDotNet
        _serializer = new SerializerBuilder()
            .WithNamingConvention(UnderscoredNamingConvention.Instance)
            .Build();

        _deserializer = new DeserializerBuilder()
            .WithNamingConvention(UnderscoredNamingConvention.Instance)
            .WithAttemptingUnquotedStringTypeDeserialization()
            .Build();
    }

    /// <inheritdoc />
    public async Task<string> GetPathForStoreAsync(YamlStore yamlStore, CancellationToken cancellationToken = default)
    {
        // Check cache first
        if (_pathCache.TryGetValue(yamlStore, out var cachedPath)) return cachedPath;

        // Run path resolution in a task to maintain async context
        // This is important for proper async stack traces and context flow
        return await Task.Run(() =>
        {
            var yamlPath = yamlStore switch
            {
                YamlStore.Main => Path.Combine("CLASSIC Data", "databases", "CLASSIC Main.yaml"),
                YamlStore.Settings => "CLASSIC Settings.yaml",
                YamlStore.Ignore => "CLASSIC Ignore.yaml",
                YamlStore.Game => Path.Combine("CLASSIC Data", "databases", $"CLASSIC {_currentGame}.yaml"),
                YamlStore.GameLocal => Path.Combine("CLASSIC Data", $"CLASSIC {_currentGame} Local.yaml"),
                YamlStore.Test => Path.Combine("tests", "test_settings.yaml"),
                _ => throw new NotSupportedException($"YAML store {yamlStore} is not supported")
            };

            // Cache the path
            _pathCache.TryAdd(yamlStore, yamlPath);

            return yamlPath;
        }, cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc />
    public async Task<Dictionary<string, object?>> LoadYamlAsync(string yamlPath,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(yamlPath);

        // Check if file exists
        if (!await _fileIo.FileExistsAsync(yamlPath, cancellationToken).ConfigureAwait(false))
        {
            _logger.LogWarning("YAML file not found: {Path}", yamlPath);
            return new Dictionary<string, object?>();
        }

        // Determine if this is a static file
        var isStatic = await IsStaticFileAsync(yamlPath, cancellationToken).ConfigureAwait(false);

        // Get file-specific lock
        var fileLock = await GetFileLockAsync(yamlPath).ConfigureAwait(false);

        await fileLock.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            // Handle static files (load once)
            if (isStatic)
            {
                if (_cache.TryGetValue(yamlPath, out var staticEntry))
                {
                    Interlocked.Increment(ref _cacheHits);
                    return staticEntry.Data;
                }

                // Load static file
                var data = await LoadYamlFileAsync(yamlPath, cancellationToken).ConfigureAwait(false);
                var entry = new YamlCacheEntry(data, DateTime.UtcNow, DateTime.UtcNow, yamlPath);
                _cache.TryAdd(yamlPath, entry);

                _logger.LogDebug("Loaded static YAML file: {Path}", yamlPath);
                return data;
            }

            // Handle dynamic files with TTL
            if (_cache.TryGetValue(yamlPath, out var cachedEntry))
            {
                // Check if cache needs refresh
                if (!cachedEntry.NeedsRefresh(_options.CacheTtl))
                {
                    Interlocked.Increment(ref _cacheHits);
                    return cachedEntry.Data;
                }

                // Check if file has been modified
                var lastWriteTime = await _fileIo.GetLastWriteTimeAsync(yamlPath, cancellationToken)
                    .ConfigureAwait(false);

                if (lastWriteTime.HasValue && lastWriteTime.Value <= cachedEntry.LastModified)
                {
                    // File hasn't changed, just update check time
                    _cache.TryUpdate(yamlPath, cachedEntry.WithUpdatedCheckTime(), cachedEntry);
                    Interlocked.Increment(ref _cacheHits);
                    return cachedEntry.Data;
                }
            }

            // Load or reload the file
            Interlocked.Increment(ref _cacheMisses);
            var loadedData = await LoadYamlFileAsync(yamlPath, cancellationToken).ConfigureAwait(false);
            var newEntry = new YamlCacheEntry(
                loadedData,
                await _fileIo.GetLastWriteTimeAsync(yamlPath, cancellationToken).ConfigureAwait(false) ??
                DateTime.UtcNow,
                DateTime.UtcNow,
                yamlPath);

            _cache.AddOrUpdate(yamlPath, newEntry, (_, __) => newEntry);

            _logger.LogDebug("Loaded dynamic YAML file: {Path}", yamlPath);
            return loadedData;
        }
        finally
        {
            fileLock.Release();
        }
    }

    /// <inheritdoc />
    public async Task<T?> GetSettingAsync<T>(YamlStore yamlStore, string keyPath, T? newValue = default,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(keyPath);

        // Check if this is a read operation for a static store
        var cacheKey = (yamlStore, keyPath, typeof(T));
        if (newValue == null && _options.StaticStores.Contains(yamlStore) &&
            _settingsCache.TryGetValue(cacheKey, out var cachedValue))
            return (T?)cachedValue;

        var yamlPath = await GetPathForStoreAsync(yamlStore, cancellationToken).ConfigureAwait(false);
        var data = await LoadYamlAsync(yamlPath, cancellationToken).ConfigureAwait(false);

        var keys = keyPath.Split('.');

        // Navigate to the setting location
        object? current = data;
        Dictionary<string, object?>? container = null;

        for (var i = 0; i < keys.Length - 1; i++)
        {
            if (current is not Dictionary<string, object?> dict)
            {
                _logger.LogError("Invalid path structure for {KeyPath} in {Store}", keyPath, yamlStore);
                return default;
            }

            if (!dict.TryGetValue(keys[i], out current))
            {
                // Create nested structure if updating
                if (newValue != null)
                {
                    current = new Dictionary<string, object?>();
                    dict[keys[i]] = current;
                }
                else
                {
                    return default;
                }
            }

            container = current as Dictionary<string, object?>;
        }

        if (container == null)
        {
            _logger.LogError("Could not navigate to container for {KeyPath} in {Store}", keyPath, yamlStore);
            return default;
        }

        // Handle update operations
        if (newValue != null)
        {
            // Check if trying to modify a static store
            if (_options.StaticStores.Contains(yamlStore))
            {
                var error = $"Attempted to modify static YAML store {yamlStore} at {keyPath}";
                _logger.LogError(error);
                throw new InvalidOperationException(error);
            }

            container[keys[^1]] = newValue;

            // Write changes back to file
            await SaveYamlFileAsync(yamlPath, data, cancellationToken).ConfigureAwait(false);

            // Update cache
            var fileLock = await GetFileLockAsync(yamlPath).ConfigureAwait(false);
            await fileLock.WaitAsync(cancellationToken).ConfigureAwait(false);
            try
            {
                if (_cache.TryGetValue(yamlPath, out var entry))
                {
                    var newEntry = entry with { Data = data, LastModified = DateTime.UtcNow };
                    _cache.TryUpdate(yamlPath, newEntry, entry);
                }
            }
            finally
            {
                fileLock.Release();
            }

            // Clear cached setting
            _settingsCache.TryRemove(cacheKey, out _);

            return newValue;
        }

        // Get the value
        if (!container.TryGetValue(keys[^1], out var value))
        {
            if (!YamlConstants.SettingsIgnoreNone.Contains(keys[^1]))
                _logger.LogWarning("Setting {KeyPath} not found in {Store}", keyPath, yamlStore);
            return default;
        }

        // Convert value to requested type
        var result = ConvertValue<T>(value);

        // Cache the result for static stores
        if (_options.StaticStores.Contains(yamlStore)) _settingsCache.TryAdd(cacheKey, result);

        return result;
    }

    /// <inheritdoc />
    public async Task<Dictionary<YamlStore, Dictionary<string, object?>>> LoadMultipleStoresAsync(
        IEnumerable<YamlStore> stores, CancellationToken cancellationToken = default)
    {
        var tasks = stores.Select(async store =>
        {
            var path = await GetPathForStoreAsync(store, cancellationToken).ConfigureAwait(false);
            var data = await LoadYamlAsync(path, cancellationToken).ConfigureAwait(false);
            return (store, data);
        });

        var results = await Task.WhenAll(tasks).ConfigureAwait(false);
        return results.ToDictionary(r => r.store, r => r.data);
    }

    /// <inheritdoc />
    public async Task<List<object?>> BatchGetSettingsAsync(
        IEnumerable<(YamlStore store, string keyPath)> requests,
        CancellationToken cancellationToken = default)
    {
        var tasks = requests.Select(async request =>
            await GetSettingAsync<object>(request.store, request.keyPath, null, cancellationToken)
                .ConfigureAwait(false));

        return (await Task.WhenAll(tasks).ConfigureAwait(false)).ToList();
    }

    /// <inheritdoc />
    public async Task PrefetchAllSettingsAsync(CancellationToken cancellationToken = default)
    {
        var commonStores = new[] { YamlStore.Main, YamlStore.Settings, YamlStore.Game };
        await LoadMultipleStoresAsync(commonStores, cancellationToken).ConfigureAwait(false);
        _logger.LogInformation("Prefetched {Count} common YAML stores into cache", commonStores.Length);
    }

    /// <inheritdoc />
    public void ClearCache()
    {
        _cache.Clear();
        _settingsCache.Clear();
        _pathCache.Clear();
        _logger.LogInformation("Cleared all YAML caches");
    }

    /// <inheritdoc />
    public IReadOnlyDictionary<string, long> GetMetrics()
    {
        if (!_options.EnableMetrics) return new Dictionary<string, long>();

        return new Dictionary<string, long>
        {
            ["CacheHits"] = _cacheHits,
            ["CacheMisses"] = _cacheMisses,
            ["FileReads"] = _fileReads,
            ["FileWrites"] = _fileWrites,
            ["CachedFiles"] = _cache.Count,
            ["CachedSettings"] = _settingsCache.Count
        };
    }

    /// <inheritdoc />
    public async ValueTask DisposeAsync()
    {
        if (_disposed) return;

        _disposed = true;

        // Dispose all file locks
        foreach (var lockPair in _fileLocks) lockPair.Value?.Dispose();

        _globalLock?.Dispose();

        // Clear caches
        ClearCache();

        await Task.CompletedTask;
    }

    #region Private Methods

    private async Task<SemaphoreSlim> GetFileLockAsync(string path)
    {
        if (_fileLocks.TryGetValue(path, out var existingLock)) return existingLock;

        await _globalLock.WaitAsync().ConfigureAwait(false);
        try
        {
            if (_fileLocks.TryGetValue(path, out existingLock)) return existingLock;

            var newLock = new SemaphoreSlim(1, 1);
            _fileLocks.TryAdd(path, newLock);

            // Clean up old locks if we exceed the limit
            if (_fileLocks.Count > _options.MaxFileLocks) CleanupOldLocks();

            return newLock;
        }
        finally
        {
            _globalLock.Release();
        }
    }

    private void CleanupOldLocks()
    {
        // Remove locks for files not in cache (simple cleanup strategy)
        var keysToRemove = _fileLocks.Keys
            .Where(k => !_cache.ContainsKey(k))
            .Take(_fileLocks.Count - _options.MaxFileLocks + 10)
            .ToList();

        foreach (var key in keysToRemove)
            if (_fileLocks.TryRemove(key, out var lockToDispose))
                lockToDispose.Dispose();
    }

    private async Task<bool> IsStaticFileAsync(string path, CancellationToken cancellationToken)
    {
        foreach (var store in _options.StaticStores)
        {
            var storePath = await GetPathForStoreAsync(store, cancellationToken).ConfigureAwait(false);
            if (string.Equals(path, storePath, StringComparison.OrdinalIgnoreCase)) return true;
        }

        return false;
    }

    private async Task<Dictionary<string, object?>> LoadYamlFileAsync(string path, CancellationToken cancellationToken)
    {
        try
        {
            Interlocked.Increment(ref _fileReads);

            var content = await _fileIo.ReadFileAsync(path, cancellationToken).ConfigureAwait(false);
            var yamlObject = _deserializer.Deserialize<object>(content);

            var result = ConvertYamlObject(yamlObject) as Dictionary<string, object?> ??
                         new Dictionary<string, object?>();

            // Validate settings file structure if applicable
            if (_options.ValidateSettingsStructure &&
                path.EndsWith("Settings.yaml", StringComparison.OrdinalIgnoreCase))
                if (!ValidateSettingsStructure(result))
                {
                    _logger.LogWarning("Invalid settings file structure detected in {Path}", path);

                    if (_options.AutoRegenerateCorruptedSettings)
                    {
                        await RegenerateSettingsFileAsync(path, cancellationToken).ConfigureAwait(false);
                        // Reload after regeneration
                        content = await _fileIo.ReadFileAsync(path, cancellationToken).ConfigureAwait(false);
                        yamlObject = _deserializer.Deserialize<object>(content);
                        result = ConvertYamlObject(yamlObject) as Dictionary<string, object?> ??
                                 new Dictionary<string, object?>();
                    }
                }

            return result;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to load YAML file {Path}", path);

            // Auto-regenerate corrupted settings files
            if (_options.AutoRegenerateCorruptedSettings &&
                path.EndsWith("Settings.yaml", StringComparison.OrdinalIgnoreCase))
            {
                _logger.LogWarning("Attempting to regenerate corrupted settings file {Path}", path);
                await RegenerateSettingsFileAsync(path, cancellationToken).ConfigureAwait(false);

                // Try loading again
                var content = await _fileIo.ReadFileAsync(path, cancellationToken).ConfigureAwait(false);
                var yamlObject = _deserializer.Deserialize<object>(content);
                return ConvertYamlObject(yamlObject) as Dictionary<string, object?> ??
                       new Dictionary<string, object?>();
            }

            return new Dictionary<string, object?>();
        }
    }

    private async Task SaveYamlFileAsync(string path, Dictionary<string, object?> data,
        CancellationToken cancellationToken)
    {
        try
        {
            Interlocked.Increment(ref _fileWrites);

            var yaml = _serializer.Serialize(data);
            await _fileIo.WriteFileAsync(path, yaml, cancellationToken: cancellationToken).ConfigureAwait(false);

            _logger.LogDebug("Saved YAML file {Path}", path);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to save YAML file {Path}", path);
            throw;
        }
    }

    private static bool ValidateSettingsStructure(Dictionary<string, object?> data)
    {
        // Check if classic_settings key exists and is a dictionary (using underscore naming convention)
        return data.ContainsKey("classic_settings") &&
               data["classic_settings"] is Dictionary<string, object?>;
    }

    private async Task RegenerateSettingsFileAsync(string path, CancellationToken cancellationToken)
    {
        try
        {
            // Backup corrupted file if it exists
            if (await _fileIo.FileExistsAsync(path, cancellationToken).ConfigureAwait(false))
            {
                var backupPath = $"{path}.corrupted.{DateTime.Now:yyyyMMddHHmmss}.bak";
                await _fileIo.CopyFileAsync(path, backupPath, false, cancellationToken).ConfigureAwait(false);
                _logger.LogInformation("Backed up corrupted settings to {BackupPath}", backupPath);
            }

            // Create minimal valid settings structure
            var defaultSettings = new Dictionary<string, object?>
            {
                ["classic_settings"] = new Dictionary<string, object?>
                {
                    ["managed_game"] = _currentGame
                }
            };

            // Try to load default settings from Main.yaml
            try
            {
                var mainPath = await GetPathForStoreAsync(YamlStore.Main, cancellationToken).ConfigureAwait(false);
                if (await _fileIo.FileExistsAsync(mainPath, cancellationToken).ConfigureAwait(false))
                {
                    var mainData = await LoadYamlFileAsync(mainPath, cancellationToken).ConfigureAwait(false);

                    if (mainData.TryGetValue("classic_info", out var classicInfo) &&
                        classicInfo is Dictionary<string, object?> infoDict &&
                        infoDict.TryGetValue("default_settings", out var defaultSettingsContent) &&
                        defaultSettingsContent is string settingsYaml)
                    {
                        // Parse the default settings YAML
                        var settingsObject = _deserializer.Deserialize<object>(settingsYaml);
                        defaultSettings = ConvertYamlObject(settingsObject) as Dictionary<string, object?> ??
                                          defaultSettings;
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Could not load default settings from Main.yaml");
            }

            // Save the regenerated settings
            await SaveYamlFileAsync(path, defaultSettings, cancellationToken).ConfigureAwait(false);
            _logger.LogInformation("Successfully regenerated settings file at {Path}", path);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to regenerate settings file at {Path}", path);
            throw;
        }
    }

    private static object? ConvertYamlObject(object? yamlObject)
    {
        return yamlObject switch
        {
            Dictionary<object, object> dict => dict.ToDictionary(
                kvp => kvp.Key.ToString() ?? string.Empty,
                kvp => ConvertYamlObject(kvp.Value)),
            List<object> list => list.Select(ConvertYamlObject).ToList(),
            _ => yamlObject
        };
    }

    private static T? ConvertValue<T>(object? value)
    {
        if (value == null) return default;

        if (value is T typedValue) return typedValue;

        // Handle common conversions
        var targetType = typeof(T);

        if (targetType == typeof(string)) return (T)(object)value.ToString()!;

        // Handle numeric and boolean conversions
        // YAML might return different numeric types (Int16, Int32, Int64, etc.)
        if (targetType == typeof(int) || targetType == typeof(long) ||
            targetType == typeof(double) || targetType == typeof(float) ||
            targetType == typeof(decimal) || targetType == typeof(bool) ||
            targetType == typeof(short) || targetType == typeof(byte))
            try
            {
                return (T)Convert.ChangeType(value, targetType);
            }
            catch
            {
                return default;
            }

        // Handle nullable types
        if (Nullable.GetUnderlyingType(targetType) != null)
        {
            var underlyingType = Nullable.GetUnderlyingType(targetType)!;
            try
            {
                var convertedValue = Convert.ChangeType(value, underlyingType);
                return (T)convertedValue;
            }
            catch
            {
                return default;
            }
        }

        // Handle collections
        if (targetType.IsGenericType)
        {
            if (targetType.GetGenericTypeDefinition() == typeof(List<>))
            {
                if (value is List<object?> list)
                {
                    var elementType = targetType.GetGenericArguments()[0];
                    var typedList = Activator.CreateInstance(targetType);
                    var addMethod = targetType.GetMethod("Add")!;

                    foreach (var item in list)
                    {
                        var convertedItem = Convert.ChangeType(item, elementType);
                        addMethod.Invoke(typedList, new[] { convertedItem });
                    }

                    return (T)typedList!;
                }
            }
            else if (targetType.GetGenericTypeDefinition() == typeof(Dictionary<,>))
            {
                if (value is Dictionary<string, object?> dict) return (T)(object)dict;
            }
        }

        // Try direct cast as last resort
        try
        {
            return (T)value;
        }
        catch
        {
            return default;
        }
    }

    #endregion
}