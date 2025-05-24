using System;
using System.Collections.Generic;
using System.Data.SQLite;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;

namespace Scanner111.Services.Interfaces;

/// <summary>
///     Service for database operations related to crash log scanning.
/// </summary>
public interface IDatabaseService
{
    /// <summary>
    ///     Gets an entry from the database based on FormID and plugin.
    /// </summary>
    /// <param name="formId">The FormID to look up.</param>
    /// <param name="plugin">The plugin name.</param>
    /// <returns>The database entry if found, null otherwise.</returns>
    Task<string?> GetEntryAsync(string formId, string plugin);

    /// <summary>
    ///     Checks if any database exists.
    /// </summary>
    /// <returns>True if at least one database exists.</returns>
    bool DatabaseExists();

    /// <summary>
    ///     Clears the query cache.
    /// </summary>
    void ClearCache();
}

/// <summary>
///     Implementation of database service for FormID lookups.
/// </summary>
public class DatabaseService : IDatabaseService
{
    private const string SqliteConnectionString = "Data Source={0};Version=3;";
    private const string FormIdQuery = "SELECT entry FROM formids WHERE formid = @formid AND plugin = @plugin LIMIT 1";
    private const string FormIdParameter = "@formid";
    private const string PluginParameter = "@plugin";

    private readonly string[] _dbPaths;
    private readonly ILogger<DatabaseService> _logger;
    private readonly Dictionary<FormIdKey, string> _queryCache = new();

    public DatabaseService(ILogger<DatabaseService> logger)
    {
        _logger = logger;

        // Initialize database paths
        _dbPaths =
        [
            Path.Combine("CLASSIC Data", "databases", "Fallout4 FormIDs Main.db"),
            Path.Combine("CLASSIC Data", "databases", "Fallout4 FormIDs Local.db")
        ];
    }

    /// <summary>
    /// Gets an entry from the cache or database.
    /// </summary>
    public async Task<string?> GetEntryAsync(string formId, string plugin)
    {
        ArgumentNullException.ThrowIfNull(formId);
        ArgumentNullException.ThrowIfNull(plugin);

        var key = new FormIdKey(formId, plugin);

        // Check cache first
        if (_queryCache.TryGetValue(key, out var cachedEntry))
            return cachedEntry;

        // Search in databases
        foreach (var dbPath in _dbPaths)
        {
            if (!File.Exists(dbPath))
                continue;

            try
            {
                var entry = await QueryDatabaseAsync(dbPath, formId, plugin);
                if (string.IsNullOrEmpty(entry)) continue;
                _queryCache[key] = entry;
                return entry;
            }
            catch (SQLiteException ex)
            {
                _logger.LogWarning(ex, "SQLite error querying database {DbPath}: {Message}", dbPath, ex.Message);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Unexpected error querying database {DbPath}", dbPath);
            }
        }

        return null;
    }

    /// <summary>
    /// Checks if any database exists.
    /// </summary>
    public bool DatabaseExists()
    {
        return _dbPaths.Any(File.Exists);
    }

    /// <summary>
    /// Clears the query cache.
    /// </summary>
    public void ClearCache()
    {
        _queryCache.Clear();
    }

    /// <summary>
    /// Queries a specific database file.
    /// </summary>
    private async Task<string?> QueryDatabaseAsync(string dbPath, string formId, string plugin)
    {
        var connectionString = string.Format(SqliteConnectionString, dbPath);
        await using var connection = new SQLiteConnection(connectionString);
        await connection.OpenAsync();

        await using var command = new SQLiteCommand(FormIdQuery, connection);
        command.Parameters.AddWithValue(FormIdParameter, formId);
        command.Parameters.AddWithValue(PluginParameter, plugin);

        var result = await command.ExecuteScalarAsync();
        return result?.ToString();
    }

    /// <summary>
    /// Record representing a form ID and plugin combination for cache lookup
    /// </summary>
    private readonly record struct FormIdKey(string FormId, string Plugin);
}