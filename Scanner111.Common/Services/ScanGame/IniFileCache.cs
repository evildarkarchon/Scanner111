using System.Collections.Concurrent;
using System.Security.Cryptography;

namespace Scanner111.Common.Services.ScanGame;

/// <summary>
/// Thread-safe cache for parsed INI file contents.
/// </summary>
/// <remarks>
/// <para>
/// Caches parsed INI data to avoid re-reading files during validation.
/// Uses ConcurrentDictionary for thread safety during parallel scanning.
/// </para>
/// <para>
/// Also tracks duplicate files detected during scanning, identified by
/// content hash and other similarity metrics.
/// </para>
/// </remarks>
public sealed class IniFileCache
{
    private readonly ConcurrentDictionary<string, CachedIniFile> _cache;
    private readonly ConcurrentDictionary<string, ConcurrentBag<string>> _duplicateFiles;
    private readonly ConcurrentDictionary<string, string> _hashCache;
    private readonly IniParser _parser;

    /// <summary>
    /// Initializes a new instance of the <see cref="IniFileCache"/> class.
    /// </summary>
    public IniFileCache()
    {
        _cache = new ConcurrentDictionary<string, CachedIniFile>(StringComparer.OrdinalIgnoreCase);
        _duplicateFiles = new ConcurrentDictionary<string, ConcurrentBag<string>>(StringComparer.OrdinalIgnoreCase);
        _hashCache = new ConcurrentDictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        _parser = new IniParser();
    }

    /// <summary>
    /// Gets the duplicate files detected during scanning.
    /// </summary>
    public IReadOnlyDictionary<string, IReadOnlyList<string>> DuplicateFiles
    {
        get
        {
            var result = new Dictionary<string, IReadOnlyList<string>>(StringComparer.OrdinalIgnoreCase);
            foreach (var (key, bag) in _duplicateFiles)
            {
                var list = bag.ToList();
                if (list.Count > 0)
                {
                    result[key] = list;
                }
            }
            return result;
        }
    }

    /// <summary>
    /// Gets the file paths currently in the cache.
    /// </summary>
    public IEnumerable<(string FileNameLower, string FilePath)> CachedFiles =>
        _cache.Select(kvp => (kvp.Key, kvp.Value.FilePath));

    /// <summary>
    /// Loads and caches an INI file.
    /// </summary>
    /// <param name="filePath">The path to the INI file.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The cached file data, or null if loading failed.</returns>
    public async Task<CachedIniFile?> LoadAsync(string filePath, CancellationToken cancellationToken = default)
    {
        var fileNameLower = Path.GetFileName(filePath).ToLowerInvariant();

        // Check if already cached
        if (_cache.TryGetValue(fileNameLower, out var existing))
        {
            // Check for duplicate
            await TrackDuplicateAsync(fileNameLower, filePath, existing, cancellationToken).ConfigureAwait(false);
            return existing;
        }

        try
        {
            var sections = await _parser.ParseFileAsync(filePath, cancellationToken).ConfigureAwait(false);
            var fileHash = await ComputeFileHashAsync(filePath, cancellationToken).ConfigureAwait(false);

            var cached = new CachedIniFile(filePath, fileHash, sections);

            // Try to add to cache - if another thread added it first, check for duplicate
            if (_cache.TryAdd(fileNameLower, cached))
            {
                return cached;
            }

            // Another thread added it, check for duplicate
            if (_cache.TryGetValue(fileNameLower, out existing))
            {
                await TrackDuplicateAsync(fileNameLower, filePath, existing, cancellationToken).ConfigureAwait(false);
                return existing;
            }

            return cached;
        }
        catch (Exception)
        {
            // Failed to parse - return null
            return null;
        }
    }

    /// <summary>
    /// Gets a value from a cached file.
    /// </summary>
    /// <typeparam name="T">The value type (must be a struct like int, float, bool).</typeparam>
    /// <param name="fileNameLower">The lowercase filename.</param>
    /// <param name="section">The section name.</param>
    /// <param name="setting">The setting name.</param>
    /// <returns>The parsed value, or null if not found or parse failed.</returns>
    public T? GetValue<T>(string fileNameLower, string section, string setting) where T : struct
    {
        var stringValue = GetStringValue(fileNameLower, section, setting);
        if (stringValue == null)
        {
            return null;
        }

        try
        {
            if (typeof(T) == typeof(bool))
            {
                // Handle common boolean representations
                return (T)(object)ParseBool(stringValue);
            }

            if (typeof(T) == typeof(int))
            {
                return (T)(object)int.Parse(stringValue);
            }

            if (typeof(T) == typeof(float))
            {
                return (T)(object)float.Parse(stringValue);
            }

            if (typeof(T) == typeof(double))
            {
                return (T)(object)double.Parse(stringValue);
            }

            return null;
        }
        catch
        {
            return null;
        }
    }

    /// <summary>
    /// Gets a string value from a cached file.
    /// </summary>
    /// <param name="fileNameLower">The lowercase filename.</param>
    /// <param name="section">The section name.</param>
    /// <param name="setting">The setting name.</param>
    /// <returns>The string value, or null if not found.</returns>
    public string? GetStringValue(string fileNameLower, string section, string setting)
    {
        if (!_cache.TryGetValue(fileNameLower, out var cached))
        {
            return null;
        }

        return IniParser.GetValue(cached.Sections, section, setting);
    }

    /// <summary>
    /// Gets the full file path for a cached file.
    /// </summary>
    /// <param name="fileNameLower">The lowercase filename.</param>
    /// <returns>The full file path, or null if not cached.</returns>
    public string? GetFilePath(string fileNameLower)
    {
        return _cache.TryGetValue(fileNameLower, out var cached) ? cached.FilePath : null;
    }

    /// <summary>
    /// Checks if a file is in the cache.
    /// </summary>
    /// <param name="fileNameLower">The lowercase filename.</param>
    /// <returns>True if the file is cached; otherwise, false.</returns>
    public bool Contains(string fileNameLower) => _cache.ContainsKey(fileNameLower);

    /// <summary>
    /// Checks if a setting exists in a cached file.
    /// </summary>
    /// <param name="fileNameLower">The lowercase filename.</param>
    /// <param name="section">The section name.</param>
    /// <param name="setting">The setting name.</param>
    /// <returns>True if the setting exists; otherwise, false.</returns>
    public bool HasSetting(string fileNameLower, string section, string setting)
    {
        if (!_cache.TryGetValue(fileNameLower, out var cached))
        {
            return false;
        }

        return IniParser.HasSetting(cached.Sections, section, setting);
    }

    /// <summary>
    /// Clears all cached data.
    /// </summary>
    public void Clear()
    {
        _cache.Clear();
        _duplicateFiles.Clear();
        _hashCache.Clear();
    }

    private static bool ParseBool(string value)
    {
        var lower = value.ToLowerInvariant();
        return lower switch
        {
            "1" or "true" or "yes" or "on" => true,
            "0" or "false" or "no" or "off" => false,
            _ => bool.Parse(value)
        };
    }

    private async Task TrackDuplicateAsync(
        string fileNameLower,
        string newFilePath,
        CachedIniFile existing,
        CancellationToken cancellationToken)
    {
        // Don't track if it's the same file
        if (string.Equals(newFilePath, existing.FilePath, StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        // Compute hash for new file
        var newHash = await ComputeFileHashAsync(newFilePath, cancellationToken).ConfigureAwait(false);

        // Check if hashes match (exact duplicate)
        if (newHash == existing.FileHash)
        {
            var bag = _duplicateFiles.GetOrAdd(fileNameLower, _ => new ConcurrentBag<string>());
            bag.Add(newFilePath);
        }
    }

    private async Task<string> ComputeFileHashAsync(string filePath, CancellationToken cancellationToken)
    {
        // Check cache first
        if (_hashCache.TryGetValue(filePath, out var cachedHash))
        {
            return cachedHash;
        }

        await using var stream = new FileStream(
            filePath,
            FileMode.Open,
            FileAccess.Read,
            FileShare.Read,
            bufferSize: 4096,
            useAsync: true);

        var hashBytes = await SHA256.HashDataAsync(stream, cancellationToken).ConfigureAwait(false);
        var hash = Convert.ToHexStringLower(hashBytes);

        _hashCache.TryAdd(filePath, hash);
        return hash;
    }

    /// <summary>
    /// Represents a cached INI file with its parsed contents.
    /// </summary>
    /// <param name="FilePath">The full path to the file.</param>
    /// <param name="FileHash">The SHA256 hash of the file content.</param>
    /// <param name="Sections">The parsed sections and their key-value pairs.</param>
    public record CachedIniFile(
        string FilePath,
        string FileHash,
        IReadOnlyDictionary<string, IReadOnlyDictionary<string, string>> Sections);
}
