using System.Collections.Concurrent;
using System.Data;
using System.Diagnostics;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Scanner111.Core.Data;

/// <summary>
///     Thread-safe connection pool for FormID database operations with async support.
/// </summary>
public sealed class FormIdDatabasePool : IFormIdDatabase
{
    /// <summary>
    /// Whitelist of valid game table names to prevent SQL injection
    /// </summary>
    private static readonly HashSet<string> ValidTableNames = new(StringComparer.OrdinalIgnoreCase)
    {
        "Skyrim",
        "SkyrimSE", 
        "Fallout4",
        "FO4",
        "Fallout76",
        "Morrowind",
        "Oblivion"
    };

    private readonly IMemoryCache _cache;
    private readonly ConcurrentDictionary<string, SqliteConnection> _connections;
    private readonly SemaphoreSlim _connectionSemaphore;
    private readonly List<string> _databasePaths;
    private readonly ILogger<FormIdDatabasePool> _logger;
    private readonly FormIdDatabaseOptions _options;
    private readonly object _statsLock = new();
    private long _cacheHits;
    private long _cacheMisses;
    private long _databaseErrors;
    private bool _disposed;

    private bool _initialized;

    // Statistics tracking
    private long _totalQueries;
    private double _totalQueryTimeMs;

    public FormIdDatabasePool(
        ILogger<FormIdDatabasePool> logger,
        IMemoryCache cache,
        IOptions<FormIdDatabaseOptions> options)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _cache = cache ?? throw new ArgumentNullException(nameof(cache));
        _options = options?.Value ?? throw new ArgumentNullException(nameof(options));

        // Validate game table name to prevent SQL injection
        if (!ValidTableNames.Contains(_options.GameTableName))
        {
            throw new ArgumentException(
                $"Invalid game table name: '{_options.GameTableName}'. Valid names are: {string.Join(", ", ValidTableNames)}",
                nameof(options));
        }

        _connectionSemaphore = new SemaphoreSlim(_options.MaxConnections, _options.MaxConnections);
        _databasePaths = new List<string>();
        _connections = new ConcurrentDictionary<string, SqliteConnection>();
    }

    /// <inheritdoc />
    public bool IsAvailable => _initialized && !_disposed;

    /// <inheritdoc />
    public int CachedEntryCount => (int)_cacheHits; // IMemoryCache doesn't expose count, use cache hits as proxy

    /// <inheritdoc />
    public int MaxConnections => _options.MaxConnections;

    /// <inheritdoc />
    public async Task InitializeAsync(CancellationToken cancellationToken = default)
    {
        if (_initialized)
            return;

        _logger.LogDebug("Initializing FormID database pool with max {MaxConnections} connections",
            _options.MaxConnections);

        // Find available database files
        foreach (var dbPath in _options.DatabasePaths)
            if (File.Exists(dbPath))
            {
                _databasePaths.Add(dbPath);
                _logger.LogInformation("Found FormID database: {Path}", dbPath);
            }
            else
            {
                _logger.LogWarning("FormID database not found: {Path}", dbPath);
            }

        if (_databasePaths.Count == 0)
            _logger.LogWarning("No FormID databases found. FormID lookups will be unavailable.");
        else
            // Test connection to first database
            try
            {
                await using var testConnection = new SqliteConnection($"Data Source={_databasePaths[0]};Mode=ReadOnly");
                await testConnection.OpenAsync(cancellationToken).ConfigureAwait(false);
                _logger.LogDebug("Successfully tested connection to FormID database");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to test FormID database connection");
                _databasePaths.Clear();
            }

        _initialized = true;
    }

    /// <inheritdoc />
    public async Task<string?> GetEntryAsync(string formId, string plugin,
        CancellationToken cancellationToken = default)
    {
        if (!IsAvailable || _databasePaths.Count == 0)
            return null;

        if (string.IsNullOrWhiteSpace(formId) || string.IsNullOrWhiteSpace(plugin))
            return null;

        var stopwatch = Stopwatch.StartNew();
        Interlocked.Increment(ref _totalQueries);

        try
        {
            // Check cache first
            var cacheKey = FormIdCacheEntry.CreateKey(formId, plugin);
            if (_cache.TryGetValue<string>(cacheKey, out var cachedValue))
            {
                Interlocked.Increment(ref _cacheHits);
                _logger.LogTrace("Cache hit for FormID {FormId} in plugin {Plugin}", formId, plugin);
                return cachedValue;
            }

            Interlocked.Increment(ref _cacheMisses);

            // Query databases
            foreach (var dbPath in _databasePaths)
            {
                var result = await QueryDatabaseAsync(dbPath, formId, plugin, cancellationToken).ConfigureAwait(false);
                if (result != null)
                {
                    // Cache the result
                    var cacheOptions = new MemoryCacheEntryOptions
                    {
                        AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(_options.CacheExpirationMinutes),
                        Size = 1
                    };
                    _cache.Set(cacheKey, result, cacheOptions);

                    _logger.LogTrace("Found FormID {FormId} in plugin {Plugin}: {Result}", formId, plugin, result);
                    return result;
                }
            }

            // Not found in any database - cache the negative result to avoid repeated lookups
            _cache.Set(cacheKey, string.Empty, TimeSpan.FromMinutes(_options.CacheExpirationMinutes));
            return null;
        }
        finally
        {
            stopwatch.Stop();
            lock (_statsLock)
            {
                _totalQueryTimeMs += stopwatch.Elapsed.TotalMilliseconds;
            }
        }
    }

    /// <inheritdoc />
    public async Task<string?[]> GetEntriesAsync((string formId, string plugin)[] queries,
        CancellationToken cancellationToken = default)
    {
        if (!IsAvailable || queries == null || queries.Length == 0)
            return Array.Empty<string?>();

        // Process queries in parallel for better performance
        var tasks = queries.Select(q => GetEntryAsync(q.formId, q.plugin, cancellationToken));
        return await Task.WhenAll(tasks).ConfigureAwait(false);
    }

    /// <inheritdoc />
    public void ClearCache()
    {
        if (_cache is MemoryCache memCache) memCache.Compact(1.0); // Remove all entries
        _logger.LogInformation("FormID cache cleared");
    }

    /// <inheritdoc />
    public FormIdDatabaseStatistics GetStatistics()
    {
        lock (_statsLock)
        {
            return new FormIdDatabaseStatistics
            {
                TotalQueries = _totalQueries,
                CacheHits = _cacheHits,
                CacheMisses = _cacheMisses,
                DatabaseErrors = _databaseErrors,
                AverageQueryTimeMs = _totalQueries > 0 ? _totalQueryTimeMs / _totalQueries : 0,
                ActiveConnections = _connections.Count
            };
        }
    }

    public async ValueTask DisposeAsync()
    {
        if (_disposed)
            return;

        _disposed = true;

        // Close all connections
        var connections = _connections.Values.ToList();
        _connections.Clear();

        foreach (var connection in connections)
            try
            {
                await connection.DisposeAsync().ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error disposing database connection");
            }

        _connectionSemaphore.Dispose();
        _logger.LogDebug("FormID database pool disposed");
    }

    private async Task<string?> QueryDatabaseAsync(string dbPath, string formId, string plugin,
        CancellationToken cancellationToken)
    {
        await _connectionSemaphore.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            var connection = await GetOrCreateConnectionAsync(dbPath, cancellationToken).ConfigureAwait(false);
            if (connection == null)
                return null;

            using var command = connection.CreateCommand();
            
            // Build query using safe table name from validated whitelist
            // The table name has already been validated in the constructor against ValidTableNames
            var safeTableName = GetSafeTableName(_options.GameTableName);
            command.CommandText = BuildSecureQuery(safeTableName);

            command.Parameters.AddWithValue("@formid", formId);
            command.Parameters.AddWithValue("@plugin", plugin);

            var result = await command.ExecuteScalarAsync(cancellationToken).ConfigureAwait(false);
            return result?.ToString();
        }
        catch (Exception ex)
        {
            Interlocked.Increment(ref _databaseErrors);
            _logger.LogError(ex, "Database query error for FormID {FormId} in plugin {Plugin}", formId, plugin);

            // Remove failed connection
            if (_connections.TryRemove(dbPath, out var failedConnection))
                try
                {
                    await failedConnection.DisposeAsync().ConfigureAwait(false);
                }
                catch
                {
                }

            return null;
        }
        finally
        {
            _connectionSemaphore.Release();
        }
    }

    private async Task<SqliteConnection?> GetOrCreateConnectionAsync(string dbPath, CancellationToken cancellationToken)
    {
        SqliteConnection? connectionToDispose = null;
        
        try
        {
            if (_connections.TryGetValue(dbPath, out var existingConnection))
            {
                // Test if connection is still alive
                if (existingConnection.State == ConnectionState.Open)
                {
                    try
                    {
                        using var testCommand = existingConnection.CreateCommand();
                        testCommand.CommandText = "SELECT 1";
                        await testCommand.ExecuteScalarAsync(cancellationToken).ConfigureAwait(false);
                        return existingConnection;
                    }
                    catch (Exception testEx)
                    {
                        _logger.LogDebug(testEx, "Connection test failed for database: {Path}", dbPath);
                        // Connection is dead, mark for disposal
                        _connections.TryRemove(dbPath, out connectionToDispose);
                    }
                }
                else
                {
                    // Connection is not open, remove and dispose
                    _connections.TryRemove(dbPath, out connectionToDispose);
                }
            }

            // Create new connection
            var connection = new SqliteConnection($"Data Source={dbPath};Mode=ReadOnly");
            try
            {
                await connection.OpenAsync(cancellationToken).ConfigureAwait(false);
                _connections.TryAdd(dbPath, connection);
                _logger.LogDebug("Created new connection to database: {Path}", dbPath);
                return connection;
            }
            catch
            {
                // Dispose the new connection if it failed to open
                await connection.DisposeAsync().ConfigureAwait(false);
                throw;
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to create connection to database: {Path}", dbPath);
            return null;
        }
        finally
        {
            // Ensure failed connection is disposed
            if (connectionToDispose != null)
            {
                try
                {
                    await connectionToDispose.DisposeAsync().ConfigureAwait(false);
                    _logger.LogDebug("Disposed failed connection for database: {Path}", dbPath);
                }
                catch (Exception disposeEx)
                {
                    _logger.LogError(disposeEx, "Error disposing failed connection for database: {Path}", dbPath);
                }
            }
        }
    }

    /// <summary>
    /// Gets a safe table name that has been validated against the whitelist.
    /// </summary>
    private string GetSafeTableName(string tableName)
    {
        // Double-check validation (already done in constructor)
        if (!ValidTableNames.Contains(tableName))
        {
            throw new InvalidOperationException(
                $"Table name '{tableName}' is not in the validated whitelist. This should never happen.");
        }
        return tableName;
    }

    /// <summary>
    /// Builds a secure SQL query using pre-validated table name.
    /// </summary>
    private static string BuildSecureQuery(string safeTableName)
    {
        // Use a dictionary to map safe table names to queries
        // This completely avoids string interpolation in SQL
        var queryTemplates = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["Skyrim"] = "SELECT entry FROM Skyrim WHERE formid = @formid AND plugin = @plugin COLLATE NOCASE",
            ["SkyrimSE"] = "SELECT entry FROM SkyrimSE WHERE formid = @formid AND plugin = @plugin COLLATE NOCASE",
            ["Fallout4"] = "SELECT entry FROM Fallout4 WHERE formid = @formid AND plugin = @plugin COLLATE NOCASE",
            ["FO4"] = "SELECT entry FROM FO4 WHERE formid = @formid AND plugin = @plugin COLLATE NOCASE",
            ["Fallout76"] = "SELECT entry FROM Fallout76 WHERE formid = @formid AND plugin = @plugin COLLATE NOCASE",
            ["Morrowind"] = "SELECT entry FROM Morrowind WHERE formid = @formid AND plugin = @plugin COLLATE NOCASE",
            ["Oblivion"] = "SELECT entry FROM Oblivion WHERE formid = @formid AND plugin = @plugin COLLATE NOCASE"
        };

        if (!queryTemplates.TryGetValue(safeTableName, out var query))
        {
            throw new InvalidOperationException(
                $"No query template found for table '{safeTableName}'. This should never happen.");
        }

        return query;
    }
}

/// <summary>
///     Configuration options for FormID database access.
/// </summary>
public sealed class FormIdDatabaseOptions
{
    /// <summary>
    ///     Gets or sets the maximum number of concurrent database connections.
    /// </summary>
    public int MaxConnections { get; set; } = 5;

    /// <summary>
    ///     Gets or sets the cache expiration time in minutes.
    /// </summary>
    public int CacheExpirationMinutes { get; set; } = 30;

    /// <summary>
    ///     Gets or sets the database file paths to search for FormID data.
    /// </summary>
    public string[] DatabasePaths { get; set; } = Array.Empty<string>();

    /// <summary>
    ///     Gets or sets the game table name in the database.
    /// </summary>
    public string GameTableName { get; set; } = "Skyrim";

    /// <summary>
    ///     Gets or sets whether to enable FormID value lookups.
    /// </summary>
    public bool EnableFormIdLookups { get; set; } = true;
}