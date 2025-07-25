using System.Collections.Concurrent;
using System.Data.SQLite;
using Microsoft.Extensions.Logging;

namespace Scanner111.Core.Infrastructure;

/// <summary>
/// Interface for the FormID database service, providing methods for querying
/// database entries and checking database existence status.
/// </summary>
public interface IFormIdDatabaseService
{
    /// <summary>
    ///     Check if FormID databases exist
    /// </summary>
    bool DatabaseExists { get; }

    /// <summary>
    ///     Look up a FormID value in the database
    /// </summary>
    /// <param name="formId">The FormID to look up (without plugin prefix)</param>
    /// <param name="plugin">The plugin name</param>
    /// <returns>The description/value if found, null otherwise</returns>
    string? GetEntry(string formId, string plugin);
}

/// <summary>
/// Provides an implementation of the FormID database service. This class supports
/// querying FormID values from a database with built-in caching for improved performance.
/// </summary>
public class FormIdDatabaseService : IFormIdDatabaseService
{
    private readonly string[] _databasePaths;
    private readonly string _gameName;
    private readonly ILogger<FormIdDatabaseService>? _logger;
    private readonly ConcurrentDictionary<(string formId, string plugin), string> _queryCache = new();

    /// <summary>
    /// Provides an implementation of the FormID database service. This class supports
    /// querying FormID values from a database with built-in caching for improved performance.
    /// </summary>
    public FormIdDatabaseService(string gameName = "Fallout4", ILogger<FormIdDatabaseService>? logger = null)
    {
        _logger = logger;
        _gameName = gameName;

        // Set up potential database paths
        var dataPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Data", "databases");
        _databasePaths =
        [
          Path.Combine(dataPath, $"{_gameName} FormIDs Main.db"),
            Path.Combine(dataPath, $"{_gameName} FormIDs Local.db")
        ];
    }

    /// <summary>
    ///     Check if FormID databases exist
    /// </summary>
    public bool DatabaseExists => _databasePaths?.Any(File.Exists) ?? false;

    /// <summary>
    /// Look up a FormID value in the database
    /// </summary>
    /// <param name="formId">The FormID to look up (without plugin prefix)</param>
    /// <param name="plugin">The plugin name</param>
    /// <returns>The description/value if found, null otherwise</returns>
    public string? GetEntry(string formId, string plugin)
    {
        if (!DatabaseExists)
            return null;

        // Check cache first
        var cacheKey = (formId.ToUpper(), plugin);
        if (_queryCache.TryGetValue(cacheKey, out var cachedValue))
            return cachedValue;

        // Search databases
        foreach (var dbPath in _databasePaths.Where(File.Exists))
            try
            {
                using var connection = new SQLiteConnection($"Data Source={dbPath};Version=3;Read Only=True;");
                connection.Open();

                using var command = new SQLiteCommand($@"
                    SELECT entry FROM {_gameName} 
                    WHERE formid = @formid AND plugin = @plugin COLLATE NOCASE", connection);
                command.Parameters.AddWithValue("@formid", formId);
                command.Parameters.AddWithValue("@plugin", plugin);

                if (command.ExecuteScalar() is not string result) continue;
                // Cache the result
                _queryCache.TryAdd(cacheKey, result);
                return result;
            }
            catch (Exception ex)
            {
                // Log error but continue to next database
                _logger?.LogError(ex, "Error accessing FormID database {DatabasePath}", dbPath);
            }

        return null;
    }
}