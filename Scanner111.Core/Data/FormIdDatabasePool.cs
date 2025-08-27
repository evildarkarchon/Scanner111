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
            command.CommandText = $@"
                SELECT entry 
                FROM {_options.GameTableName} 
                WHERE formid = @formid 
                AND plugin = @plugin COLLATE NOCASE";

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
        if (_connections.TryGetValue(dbPath, out var existingConnection))
            // Test if connection is still alive
            if (existingConnection.State == ConnectionState.Open)
                try
                {
                    using var testCommand = existingConnection.CreateCommand();
                    testCommand.CommandText = "SELECT 1";
                    await testCommand.ExecuteScalarAsync(cancellationToken).ConfigureAwait(false);
                    return existingConnection;
                }
                catch
                {
                    // Connection is dead, remove it
                    _connections.TryRemove(dbPath, out _);
                    try
                    {
                        await existingConnection.DisposeAsync().ConfigureAwait(false);
                    }
                    catch
                    {
                    }
                }

        try
        {
            var connection = new SqliteConnection($"Data Source={dbPath};Mode=ReadOnly");
            await connection.OpenAsync(cancellationToken).ConfigureAwait(false);

            _connections.TryAdd(dbPath, connection);
            _logger.LogDebug("Created new connection to database: {Path}", dbPath);
            return connection;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to create connection to database: {Path}", dbPath);
            return null;
        }
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