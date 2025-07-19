using System.Collections.Concurrent;
using System.Data.SQLite;

namespace Scanner111.Core.Infrastructure;

/// <summary>
/// Service for managing FormID database lookups and caching
/// </summary>
public interface IFormIdDatabaseService
{
    /// <summary>
    /// Check if FormID databases exist
    /// </summary>
    bool DatabaseExists { get; }
    
    /// <summary>
    /// Look up a FormID value in the database
    /// </summary>
    /// <param name="formId">The FormID to look up (without plugin prefix)</param>
    /// <param name="plugin">The plugin name</param>
    /// <returns>The description/value if found, null otherwise</returns>
    string? GetEntry(string formId, string plugin);
}

/// <summary>
/// Implementation of FormID database service with caching
/// </summary>
public class FormIdDatabaseService : IFormIdDatabaseService
{
    private readonly string _gameName;
    private readonly string[] _databasePaths;
    private readonly ConcurrentDictionary<(string formId, string plugin), string> _queryCache = new();
    private readonly bool _databasesExist;
    
    /// <summary>
    /// Check if FormID databases exist
    /// </summary>
    public bool DatabaseExists => _databasesExist;

    /// <summary>
    /// Initialize the FormID database service
    /// </summary>
    /// <param name="gameName">Game name (e.g., "Fallout4")</param>
    public FormIdDatabaseService(string gameName = "Fallout4")
    {
        _gameName = gameName;
        
        // Set up potential database paths
        var dataPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Data", "databases");
        _databasePaths = new[]
        {
            Path.Combine(dataPath, $"{_gameName} FormIDs Main.db"),
            Path.Combine(dataPath, $"{_gameName} FormIDs Local.db")
        };
        
        // Check if any databases exist
        _databasesExist = _databasePaths.Any(File.Exists);
    }

    /// <summary>
    /// Look up a FormID value in the database
    /// </summary>
    /// <param name="formId">The FormID to look up (without plugin prefix)</param>
    /// <param name="plugin">The plugin name</param>
    /// <returns>The description/value if found, null otherwise</returns>
    public string? GetEntry(string formId, string plugin)
    {
        if (!_databasesExist)
            return null;
            
        // Check cache first
        var cacheKey = (formId.ToUpper(), plugin);
        if (_queryCache.TryGetValue(cacheKey, out var cachedValue))
            return cachedValue;
            
        // Search databases
        foreach (var dbPath in _databasePaths.Where(File.Exists))
        {
            try
            {
                using var connection = new SQLiteConnection($"Data Source={dbPath};Version=3;Read Only=True;");
                connection.Open();
                
                using var command = new SQLiteCommand($@"
                    SELECT entry FROM {_gameName} 
                    WHERE formid = @formid AND plugin = @plugin COLLATE NOCASE", connection);
                command.Parameters.AddWithValue("@formid", formId);
                command.Parameters.AddWithValue("@plugin", plugin);
                
                var result = command.ExecuteScalar() as string;
                if (result != null)
                {
                    // Cache the result
                    _queryCache.TryAdd(cacheKey, result);
                    return result;
                }
            }
            catch (Exception ex)
            {
                // Log error but continue to next database
                Console.WriteLine($"Error accessing FormID database {dbPath}: {ex.Message}");
            }
        }
        
        return null;
    }
}