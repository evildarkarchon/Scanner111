using System.Collections.Concurrent;
using System.Data.Common;
using System.Diagnostics;
using System.Threading.Channels;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Scanner111.Core.Data;

/// <summary>
///     Optimized database operations leveraging native SQLite features.
///     Uses prepared statements, memory-mapped I/O, and efficient batching.
/// </summary>
public sealed class OptimizedDatabaseOperations : IFormIdDatabase, IAsyncDisposable
{
    private readonly ILogger<OptimizedDatabaseOperations> _logger;
    private readonly IMemoryCache _cache;
    private readonly FormIdDatabaseOptions _options;
    private readonly ConcurrentDictionary<string, SqliteConnection> _connections;
    private readonly ConcurrentDictionary<string, PreparedStatement> _preparedStatements;
    private readonly Channel<QueryRequest> _queryChannel;
    private readonly Task _queryProcessor;
    private readonly CancellationTokenSource _shutdownCts;
    private readonly SemaphoreSlim _initLock;
    private bool _initialized;
    private bool _disposed;

    // Performance tracking
    private long _totalQueries;
    private long _cacheHits;
    private long _totalQueryTimeMs;

    public OptimizedDatabaseOperations(
        ILogger<OptimizedDatabaseOperations> logger,
        IMemoryCache cache,
        IOptions<FormIdDatabaseOptions> options)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _cache = cache ?? throw new ArgumentNullException(nameof(cache));
        _options = options?.Value ?? throw new ArgumentNullException(nameof(options));
        
        _connections = new ConcurrentDictionary<string, SqliteConnection>();
        _preparedStatements = new ConcurrentDictionary<string, PreparedStatement>();
        _shutdownCts = new CancellationTokenSource();
        _initLock = new SemaphoreSlim(1, 1);

        // Create query processing channel for batching
        _queryChannel = Channel.CreateUnbounded<QueryRequest>(new UnboundedChannelOptions
        {
            SingleReader = true,
            SingleWriter = false
        });

        // Start background query processor
        _queryProcessor = Task.Run(ProcessQueriesAsync);
    }

    /// <inheritdoc />
    public bool IsAvailable => _initialized && !_disposed;

    /// <inheritdoc />
    public int CachedEntryCount => (int)_cacheHits;

    /// <inheritdoc />
    public int MaxConnections => _options.MaxConnections;

    /// <inheritdoc />
    public async Task InitializeAsync(CancellationToken cancellationToken = default)
    {
        await _initLock.WaitAsync(cancellationToken);
        try
        {
            if (_initialized)
                return;

            _logger.LogInformation("Initializing optimized database operations");

            foreach (var dbPath in _options.DatabasePaths.Where(File.Exists))
            {
                var connection = await CreateOptimizedConnectionAsync(dbPath, cancellationToken);
                _connections[dbPath] = connection;
                _logger.LogInformation("Initialized database: {Path}", dbPath);
            }

            if (_connections.Count == 0)
            {
                _logger.LogWarning("No FormID databases found");
                return;
            }

            _initialized = true;
        }
        finally
        {
            _initLock.Release();
        }
    }

    private async Task<SqliteConnection> CreateOptimizedConnectionAsync(
        string dbPath,
        CancellationToken cancellationToken)
    {
        var connectionString = new SqliteConnectionStringBuilder
        {
            DataSource = dbPath,
            Mode = SqliteOpenMode.ReadOnly,
            Cache = SqliteCacheMode.Shared,
            Pooling = true
        }.ToString();

        var connection = new SqliteConnection(connectionString);
        await connection.OpenAsync(cancellationToken);

        // Apply performance optimizations
        using (var cmd = connection.CreateCommand())
        {
            // Enable memory-mapped I/O for better performance
            cmd.CommandText = "PRAGMA mmap_size = 268435456"; // 256MB
            await cmd.ExecuteNonQueryAsync(cancellationToken);

            // Increase page cache
            cmd.CommandText = "PRAGMA cache_size = -64000"; // 64MB
            await cmd.ExecuteNonQueryAsync(cancellationToken);

            // Use faster but less safe journal mode for read-only
            cmd.CommandText = "PRAGMA journal_mode = OFF";
            await cmd.ExecuteNonQueryAsync(cancellationToken);

            // Optimize query planner
            cmd.CommandText = "PRAGMA optimize";
            await cmd.ExecuteNonQueryAsync(cancellationToken);
        }

        return connection;
    }

    /// <inheritdoc />
    public async Task<IReadOnlyDictionary<string, string>> BatchLookupAsync(
        IEnumerable<string> formIds,
        string? tableName = null,
        CancellationToken cancellationToken = default)
    {
        if (!_initialized)
            throw new InvalidOperationException("Database not initialized");

        tableName ??= _options.GameTableName;
        var formIdList = formIds.ToList();
        var results = new Dictionary<string, string>();

        // Check cache first
        var uncachedIds = new List<string>();
        foreach (var formId in formIdList)
        {
            var cacheKey = $"{tableName}:{formId}";
            if (_cache.TryGetValue<string>(cacheKey, out var cachedName))
            {
                results[formId] = cachedName!;
                Interlocked.Increment(ref _cacheHits);
            }
            else
            {
                uncachedIds.Add(formId);
            }
        }

        if (uncachedIds.Count == 0)
            return results;

        // Batch database queries
        var stopwatch = Stopwatch.StartNew();
        
        // Process in optimal batch sizes
        const int batchSize = 100;
        for (int i = 0; i < uncachedIds.Count; i += batchSize)
        {
            var batch = uncachedIds.Skip(i).Take(batchSize).ToList();
            var batchResults = await QueryBatchAsync(batch, tableName, cancellationToken);
            
            foreach (var (formId, name) in batchResults)
            {
                results[formId] = name;
                
                // Cache the result
                var cacheKey = $"{tableName}:{formId}";
                _cache.Set(cacheKey, name, TimeSpan.FromMinutes(10));
            }
        }

        stopwatch.Stop();
        Interlocked.Add(ref _totalQueryTimeMs, stopwatch.ElapsedMilliseconds);
        Interlocked.Add(ref _totalQueries, uncachedIds.Count);

        return results;
    }

    private async Task<Dictionary<string, string>> QueryBatchAsync(
        List<string> formIds,
        string tableName,
        CancellationToken cancellationToken)
    {
        var results = new Dictionary<string, string>();
        
        foreach (var connection in _connections.Values)
        {
            if (cancellationToken.IsCancellationRequested)
                break;

            try
            {
                // Get or create prepared statement
                var stmtKey = $"{connection.DataSource}:{tableName}:batch";
                var preparedStmt = await GetOrCreatePreparedStatementAsync(
                    connection,
                    stmtKey,
                    tableName,
                    formIds.Count,
                    cancellationToken);

                // Execute batch query
                using var cmd = preparedStmt.Command;
                
                // Bind parameters
                for (int i = 0; i < formIds.Count; i++)
                {
                    cmd.Parameters[$"@id{i}"].Value = formIds[i];
                }

                using var reader = await cmd.ExecuteReaderAsync(cancellationToken);
                while (await reader.ReadAsync(cancellationToken))
                {
                    var formId = reader.GetString(0);
                    var name = reader.GetString(1);
                    results[formId] = name;
                }

                if (results.Count == formIds.Count)
                    break; // Found all, no need to check other databases
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Batch query failed for {TableName}", tableName);
            }
        }

        return results;
    }

    private async Task<PreparedStatement> GetOrCreatePreparedStatementAsync(
        SqliteConnection connection,
        string key,
        string tableName,
        int paramCount,
        CancellationToken cancellationToken)
    {
        // Try to reuse existing prepared statement if parameter count matches
        if (_preparedStatements.TryGetValue(key, out var existing) && existing.ParameterCount == paramCount)
        {
            return existing;
        }

        // Create new prepared statement
        var parameters = string.Join(",", Enumerable.Range(0, paramCount).Select(i => $"@id{i}"));
        var sql = $"SELECT FormID, Name FROM {tableName} WHERE FormID IN ({parameters})";
        
        var cmd = connection.CreateCommand();
        cmd.CommandText = sql;
        
        // Add parameters
        for (int i = 0; i < paramCount; i++)
        {
            cmd.Parameters.AddWithValue($"@id{i}", string.Empty);
        }
        
        // Prepare the statement for better performance
        await cmd.PrepareAsync(cancellationToken);

        var preparedStmt = new PreparedStatement
        {
            Command = cmd,
            ParameterCount = paramCount
        };

        _preparedStatements[key] = preparedStmt;
        return preparedStmt;
    }

    private async Task ProcessQueriesAsync()
    {
        var batch = new List<QueryRequest>();
        var batchTimer = new PeriodicTimer(TimeSpan.FromMilliseconds(10));

        try
        {
            while (!_shutdownCts.Token.IsCancellationRequested)
            {
                // Collect queries for batching
                var hasMore = true;
                while (hasMore && batch.Count < 100)
                {
                    hasMore = _queryChannel.Reader.TryRead(out var request);
                    if (hasMore && request != null)
                    {
                        batch.Add(request);
                    }
                }

                // Process batch if we have items or timeout occurred
                if (batch.Count > 0)
                {
                    await ProcessQueryBatchAsync(batch);
                    batch.Clear();
                }
                else
                {
                    // Wait for more items or timeout
                    await batchTimer.WaitForNextTickAsync(_shutdownCts.Token);
                }
            }
        }
        catch (OperationCanceledException)
        {
            // Expected during shutdown
        }
        finally
        {
            batchTimer.Dispose();
        }
    }

    private async Task ProcessQueryBatchAsync(List<QueryRequest> batch)
    {
        // Group by table name for efficient querying
        var groups = batch.GroupBy(r => r.TableName);

        foreach (var group in groups)
        {
            var formIds = group.Select(r => r.FormId).ToList();
            var results = await QueryBatchAsync(formIds, group.Key, CancellationToken.None);

            foreach (var request in group)
            {
                if (results.TryGetValue(request.FormId, out var name))
                {
                    request.TaskSource.SetResult(name);
                }
                else
                {
                    request.TaskSource.SetResult(null);
                }
            }
        }
    }

    /// <inheritdoc />
    public async Task<string?> LookupFormIdAsync(
        string formId,
        string? tableName = null,
        CancellationToken cancellationToken = default)
    {
        var results = await BatchLookupAsync(new[] { formId }, tableName, cancellationToken);
        return results.TryGetValue(formId, out var name) ? name : null;
    }

    /// <inheritdoc />
    public async Task<string?> GetEntryAsync(
        string formId,
        string plugin,
        CancellationToken cancellationToken = default)
    {
        // Use the batch lookup with single item
        var results = await BatchLookupAsync(new[] { formId }, plugin, cancellationToken);
        return results.TryGetValue(formId, out var name) ? name : null;
    }

    /// <inheritdoc />
    public async Task<string?[]> GetEntriesAsync(
        (string formId, string plugin)[] queries,
        CancellationToken cancellationToken = default)
    {
        var results = new string?[queries.Length];
        
        // Group by plugin for efficient querying
        var groupedQueries = queries
            .Select((q, i) => new { Query = q, Index = i })
            .GroupBy(x => x.Query.plugin);

        foreach (var group in groupedQueries)
        {
            var formIds = group.Select(g => g.Query.formId).ToArray();
            var lookupResults = await BatchLookupAsync(formIds, group.Key, cancellationToken);
            
            foreach (var item in group)
            {
                results[item.Index] = lookupResults.TryGetValue(item.Query.formId, out var name) 
                    ? name 
                    : null;
            }
        }

        return results;
    }

    /// <inheritdoc />
    public void ClearCache()
    {
        // IMemoryCache doesn't provide a clear method, so we track keys
        // For now, we'll rely on cache expiration
        _logger.LogInformation("Cache clear requested (relies on expiration)");
    }

    /// <inheritdoc />
    public FormIdDatabaseStatistics GetStatistics()
    {
        var avgQueryTime = _totalQueries > 0 
            ? _totalQueryTimeMs / (double)_totalQueries 
            : 0;

        return new FormIdDatabaseStatistics
        {
            TotalQueries = _totalQueries,
            CacheHits = _cacheHits,
            CacheMisses = _totalQueries - _cacheHits,
            AverageQueryTimeMs = avgQueryTime,
            DatabaseErrors = 0, // Track this if needed
            ActiveConnections = _connections.Count
        };
    }

    public async ValueTask DisposeAsync()
    {
        if (_disposed)
            return;

        _shutdownCts?.Cancel();

        // Wait for query processor to finish
        if (_queryProcessor != null)
        {
            try
            {
                await _queryProcessor.WaitAsync(TimeSpan.FromSeconds(5));
            }
            catch
            {
                // Ignore timeout
            }
        }

        // Dispose prepared statements
        foreach (var stmt in _preparedStatements.Values)
        {
            stmt.Command?.Dispose();
        }
        _preparedStatements.Clear();

        // Close connections
        foreach (var connection in _connections.Values)
        {
            await connection.CloseAsync();
            await connection.DisposeAsync();
        }
        _connections.Clear();

        _shutdownCts?.Dispose();
        _initLock?.Dispose();
        
        _disposed = true;
    }

    private sealed class PreparedStatement
    {
        public required DbCommand Command { get; init; }
        public required int ParameterCount { get; init; }
    }

    private sealed class QueryRequest
    {
        public required string FormId { get; init; }
        public required string TableName { get; init; }
        public required TaskCompletionSource<string?> TaskSource { get; init; }
    }
}