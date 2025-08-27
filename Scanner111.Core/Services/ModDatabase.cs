using System.Collections.Concurrent;
using Microsoft.Extensions.Logging;
using Scanner111.Core.Configuration;

namespace Scanner111.Core.Services;

/// <summary>
///     Service for loading mod database information from YAML configuration files.
///     Uses caching to improve performance for repeated access.
/// </summary>
public sealed class ModDatabase : IModDatabase
{
    private readonly IAsyncYamlSettingsCore _yamlCore;
    private readonly ILogger<ModDatabase> _logger;
    
    private readonly ConcurrentDictionary<string, IReadOnlyDictionary<string, string>> _warningCache = new();
    private readonly ConcurrentDictionary<string, IReadOnlyDictionary<string, string>> _importantCache = new();
    private readonly Lazy<Task<IReadOnlyDictionary<string, string>>> _conflictsCache;

    public ModDatabase(IAsyncYamlSettingsCore yamlCore, ILogger<ModDatabase> logger)
    {
        _yamlCore = yamlCore ?? throw new ArgumentNullException(nameof(yamlCore));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        
        _conflictsCache = new Lazy<Task<IReadOnlyDictionary<string, string>>>(
            () => LoadConflictsInternalAsync(CancellationToken.None));
    }

    /// <inheritdoc />
    public async Task<IReadOnlyDictionary<string, string>> LoadModWarningsAsync(
        string category,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(category);

        var cacheKey = $"warnings_{category.ToUpperInvariant()}";
        
        if (_warningCache.TryGetValue(cacheKey, out var cached))
        {
            return cached;
        }

        try
        {
            var yamlKey = $"Mods_{category.ToUpperInvariant()}";
            var modData = await _yamlCore.GetSettingAsync<Dictionary<string, string>>(
                    YamlStore.Game, yamlKey, null, cancellationToken)
                .ConfigureAwait(false);

            if (modData == null)
            {
                _logger.LogWarning("No mod warning data found for category {Category}", category);
                var emptyDict = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
                _warningCache.TryAdd(cacheKey, emptyDict);
                return emptyDict;
            }

            // Convert to case-insensitive dictionary
            var result = new Dictionary<string, string>(modData, StringComparer.OrdinalIgnoreCase);
            _warningCache.TryAdd(cacheKey, result);

            _logger.LogDebug("Loaded {Count} mod warnings for category {Category}", result.Count, category);
            return result;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to load mod warnings for category {Category}", category);
            var emptyDict = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            _warningCache.TryAdd(cacheKey, emptyDict);
            return emptyDict;
        }
    }

    /// <inheritdoc />
    public async Task<IReadOnlyDictionary<string, string>> LoadModConflictsAsync(
        CancellationToken cancellationToken = default)
    {
        try
        {
            return await _conflictsCache.Value.ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to load mod conflicts from cache");
            return new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        }
    }

    /// <inheritdoc />
    public async Task<IReadOnlyDictionary<string, string>> LoadImportantModsAsync(
        string category,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(category);

        var cacheKey = $"important_{category.ToUpperInvariant()}";
        
        if (_importantCache.TryGetValue(cacheKey, out var cached))
        {
            return cached;
        }

        try
        {
            var yamlKey = $"Mods_{category.ToUpperInvariant()}";
            var modData = await _yamlCore.GetSettingAsync<Dictionary<string, string>>(
                    YamlStore.Game, yamlKey, null, cancellationToken)
                .ConfigureAwait(false);

            if (modData == null)
            {
                _logger.LogWarning("No important mod data found for category {Category}", category);
                var emptyDict = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
                _importantCache.TryAdd(cacheKey, emptyDict);
                return emptyDict;
            }

            // Convert to case-insensitive dictionary
            var result = new Dictionary<string, string>(modData, StringComparer.OrdinalIgnoreCase);
            _importantCache.TryAdd(cacheKey, result);

            _logger.LogDebug("Loaded {Count} important mods for category {Category}", result.Count, category);
            return result;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to load important mods for category {Category}", category);
            var emptyDict = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            _importantCache.TryAdd(cacheKey, emptyDict);
            return emptyDict;
        }
    }

    /// <inheritdoc />
    public Task<IReadOnlyList<string>> GetModWarningCategoriesAsync(
        CancellationToken cancellationToken = default)
    {
        // Standard mod warning categories based on YAML structure
        var categories = new List<string> { "FREQ", "PERF", "STAB" };
        
        // Note: Dynamic discovery of categories would require additional YAML traversal methods
        // For now, we rely on the standard categories defined above

        var result = categories.Distinct(StringComparer.OrdinalIgnoreCase).ToArray();
        return Task.FromResult<IReadOnlyList<string>>(result);
    }

    /// <inheritdoc />
    public Task<IReadOnlyList<string>> GetImportantModCategoriesAsync(
        CancellationToken cancellationToken = default)
    {
        // Standard important mod categories
        var categories = new List<string> { "CORE", "CORE_FOLON" };
        
        // Note: Dynamic discovery of categories would require additional YAML traversal methods
        // For now, we rely on the standard categories defined above

        var result = categories.Distinct(StringComparer.OrdinalIgnoreCase).ToArray();
        return Task.FromResult<IReadOnlyList<string>>(result);
    }

    /// <inheritdoc />
    public async Task<bool> IsAvailableAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            // Check if we can load at least one mod warning category
            var warnings = await LoadModWarningsAsync("FREQ", cancellationToken).ConfigureAwait(false);
            return warnings.Count > 0;
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "Mod database is not available");
            return false;
        }
    }

    /// <summary>
    ///     Internal method to load mod conflicts with proper error handling.
    /// </summary>
    private async Task<IReadOnlyDictionary<string, string>> LoadConflictsInternalAsync(
        CancellationToken cancellationToken)
    {
        try
        {
            var conflictData = await _yamlCore.GetSettingAsync<Dictionary<string, string>>(
                    YamlStore.Game, "Mods_CONF", null, cancellationToken)
                .ConfigureAwait(false);

            if (conflictData == null)
            {
                _logger.LogWarning("No mod conflict data found in YAML configuration");
                return new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            }

            // Convert to case-insensitive dictionary
            var result = new Dictionary<string, string>(conflictData, StringComparer.OrdinalIgnoreCase);
            
            _logger.LogDebug("Loaded {Count} mod conflicts from configuration", result.Count);
            return result;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to load mod conflicts from configuration");
            return new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        }
    }
}