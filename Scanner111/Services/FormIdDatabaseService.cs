using System;
using System.Collections.Generic;
using System.Data;
using System.Data.SQLite;
using System.IO;
using System.Text;
using Scanner111.Models;

namespace Scanner111.Services;

/// <summary>
///     Service responsible for querying FormID information from SQLite database files.
///     Ported from Python's get_entry functionality in ClassicLib/ScanLog/Util.py.
/// </summary>
public class FormIdDatabaseService : IDisposable
{
    private readonly AppSettings _appSettings;
    private readonly Dictionary<(string FormId, string Plugin), string> _queryCache = new();
    private SQLiteConnection? _dbConnection;

    public FormIdDatabaseService(AppSettings appSettings)
    {
        _appSettings = appSettings;
        InitializeDatabase();
    }

    /// <summary>
    ///     Clean up resources
    /// </summary>
    public void Dispose()
    {
        Dispose(true);
        GC.SuppressFinalize(this);
    }

    /// <summary>
    ///     Initialize the SQLite database connection
    /// </summary>
    private void InitializeDatabase()
    {
        // Check if FormIdDatabasePath is configured
        if (string.IsNullOrEmpty(_appSettings.FormIdDatabasePath) || !File.Exists(_appSettings.FormIdDatabasePath))
        {
            // Set flag that database doesn't exist
            _appSettings.FormIdDbExists = false;
            return;
        }

        try
        {
            // Initialize SQLite connection
            _dbConnection =
                new SQLiteConnection($"Data Source={_appSettings.FormIdDatabasePath};Version=3;Read Only=True;");
            _dbConnection.Open();

            // Update the flag for database existence
            _appSettings.FormIdDbExists = true;

            // Store database path in list for compatibility with other code
            if (!_appSettings.FormIdDbPaths.Contains(_appSettings.FormIdDatabasePath))
                _appSettings.FormIdDbPaths.Add(_appSettings.FormIdDatabasePath);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error initializing FormID database: {ex.Message}");
            _appSettings.FormIdDbExists = false;
            _dbConnection = null;
        }
    }

    /// <summary>
    ///     Updates database path from application settings and initializes the connection
    /// </summary>
    /// <param name="newDatabasePath">New path to the FormID database</param>
    public void UpdateDatabasePath(string? newDatabasePath)
    {
        if (string.IsNullOrEmpty(newDatabasePath) || !File.Exists(newDatabasePath))
        {
            _appSettings.FormIdDbExists = false;
            return;
        }

        // Only change if the path is different
        if (_appSettings.FormIdDatabasePath == newDatabasePath && _dbConnection != null)
            return;

        // Clean up existing connection if any
        Dispose();

        // Update path and reinitialize
        _appSettings.FormIdDatabasePath = newDatabasePath;
        InitializeDatabase();
    }

    /// <summary>
    ///     Checks if FormID database file exists and can be accessed
    /// </summary>
    /// <returns>True if the database exists and is accessible</returns>
    public bool DatabaseExists()
    {
        return _appSettings.FormIdDbExists && _dbConnection != null;
    }

    /// <summary>
    ///     Get information about a FormID from the database
    /// </summary>
    /// <param name="formId">FormID to look up</param>
    /// <param name="plugin">Plugin name associated with the FormID</param>
    /// <returns>FormID database entry information or null if not found</returns>
    public string? GetEntry(string formId, string plugin)
    {
        // Check cache first
        var key = (formId, plugin);
        if (_queryCache.TryGetValue(key, out var cachedResult))
            return string.IsNullOrEmpty(cachedResult) ? null : cachedResult;

        // If database is not available, return null
        if (!DatabaseExists())
        {
            _queryCache[key] = string.Empty;
            return null;
        }

        // Normalize inputs
        formId = formId.Trim().ToUpper();
        plugin = plugin.Trim().ToLower();

        try
        {
            // Execute the query using the same table name format as the Python code
            using var cmd = _dbConnection!.CreateCommand();
            cmd.CommandText =
                $"SELECT entry FROM {_appSettings.GameName} WHERE formid=@formid AND plugin=@plugin COLLATE nocase";
            cmd.Parameters.AddWithValue("@formid", formId);
            cmd.Parameters.AddWithValue("@plugin", plugin);

            var result = cmd.ExecuteScalar()?.ToString();

            // Cache the result
            _queryCache[key] = result ?? string.Empty;

            return result;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error querying FormID database: {ex.Message}");
            _queryCache[key] = string.Empty;
            return null;
        }
    }

    /// <summary>
    ///     Dispose pattern implementation
    /// </summary>
    protected virtual void Dispose(bool disposing)
    {
        if (!disposing) return;
        // Free managed resources
        if (_dbConnection == null) return;
        try
        {
            if (_dbConnection.State == ConnectionState.Open) _dbConnection.Close();
            _dbConnection.Dispose();
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error disposing FormID database connection: {ex.Message}");
        }
        finally
        {
            _dbConnection = null;
        }

        // Free unmanaged resources
        // (none in this case)
    }

    /// <summary>
    ///     Finalizer
    /// </summary>
    ~FormIdDatabaseService()
    {
        Dispose(false);
    }

    /// <summary>
    ///     Creates a new FormID database with the correct schema.
    ///     This is a utility method for creating new databases from exported CSV files.
    /// </summary>
    /// <param name="databasePath">Path where the new database should be created</param>
    /// <param name="gameName">Name of the game (table name, e.g., "Fallout4")</param>
    /// <returns>True if database was successfully created</returns>
    public static bool CreateNewDatabase(string databasePath, string gameName)
    {
        try
        {
            // Delete the file if it exists
            if (File.Exists(databasePath)) File.Delete(databasePath);

            // Create database and set up schema
            SQLiteConnection.CreateFile(databasePath);

            using var connection = new SQLiteConnection($"Data Source={databasePath};Version=3;");
            connection.Open();

            // Create table with schema matching the Python implementation
            using var command = connection.CreateCommand();
            command.CommandText = $@"
                    CREATE TABLE {gameName} (
                        formid TEXT NOT NULL,
                        plugin TEXT NOT NULL,
                        entry TEXT,
                        PRIMARY KEY (formid, plugin)
                    )";
            command.ExecuteNonQuery();

            // Create indexes for better performance
            command.CommandText = $"CREATE INDEX idx_{gameName}_formid ON {gameName} (formid)";
            command.ExecuteNonQuery();

            command.CommandText = $"CREATE INDEX idx_{gameName}_plugin ON {gameName} (plugin)";
            command.ExecuteNonQuery();

            return true;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error creating FormID database: {ex.Message}");
            return false;
        }
    }

    /// <summary>
    ///     Imports FormID data from a CSV file into the SQLite database.
    /// </summary>
    /// <param name="databasePath">Path to the SQLite database</param>
    /// <param name="csvPath">Path to the CSV file to import</param>
    /// <param name="gameName">Name of the game table</param>
    /// <param name="formIdColumn">Name of the FormID column in the CSV</param>
    /// <param name="pluginColumn">Name of the plugin column in the CSV</param>
    /// <param name="hasHeaderRow">Whether the CSV has a header row</param>
    /// <returns>Number of records imported</returns>
    public static int ImportCsvToDatabase(string databasePath, string csvPath, string gameName,
        string formIdColumn = "FormID", string pluginColumn = "Plugin", bool hasHeaderRow = true)
    {
        if (!File.Exists(databasePath) || !File.Exists(csvPath))
            return 0;

        try
        {
            using var connection = new SQLiteConnection($"Data Source={databasePath};Version=3;");
            connection.Open();

            // Begin transaction for faster bulk insert
            using var transaction = connection.BeginTransaction();

            // Prepare insert statement
            using var command = connection.CreateCommand();
            command.CommandText =
                $"INSERT OR REPLACE INTO {gameName} (formid, plugin, entry) VALUES (@formid, @plugin, @entry)";
            var paramFormId = command.Parameters.Add("@formid", DbType.String);
            var paramPlugin = command.Parameters.Add("@plugin", DbType.String);
            var paramEntry = command.Parameters.Add("@entry", DbType.String);

            // Read and process CSV
            var recordCount = 0;
            using var reader = new StreamReader(csvPath);

            // Skip header row if needed
            if (hasHeaderRow)
            {
                var headerLine = reader.ReadLine();
                if (headerLine == null)
                    return 0;

                // Parse header to find column indices
                var headers = headerLine.Split(',');
                var formIdColIndex = Array.FindIndex(headers,
                    h => h.Equals(formIdColumn, StringComparison.OrdinalIgnoreCase));
                var pluginColIndex = Array.FindIndex(headers,
                    h => h.Equals(pluginColumn, StringComparison.OrdinalIgnoreCase));
                var edidColIndex =
                    Array.FindIndex(headers, h => h.Equals("EditorID", StringComparison.OrdinalIgnoreCase));
                var nameColIndex = Array.FindIndex(headers, h => h.Equals("Name", StringComparison.OrdinalIgnoreCase));

                if (formIdColIndex < 0 || pluginColIndex < 0)
                {
                    Console.WriteLine($"CSV file does not have required columns: {formIdColumn}, {pluginColumn}");
                    return 0;
                }

                while (reader.ReadLine() is { } line)
                {
                    var columns = line.Split(',');
                    if (columns.Length <= Math.Max(formIdColIndex, pluginColIndex))
                        continue;

                    var formId = columns[formIdColIndex].Trim().ToUpper();
                    var plugin = columns[pluginColIndex].Trim().ToLower();

                    // Build entry string similar to the Python implementation
                    var entryBuilder = new StringBuilder();
                    entryBuilder.Append($"FormID: {formId} - ");

                    if (edidColIndex >= 0 && edidColIndex < columns.Length)
                        entryBuilder.Append($"EDID: {columns[edidColIndex]} - ");

                    if (nameColIndex >= 0 && nameColIndex < columns.Length)
                        entryBuilder.Append($"Name: {columns[nameColIndex]}");

                    var entry = entryBuilder.ToString().TrimEnd(' ', '-');

                    // Execute insert
                    paramFormId.Value = formId;
                    paramPlugin.Value = plugin;
                    paramEntry.Value = entry;
                    command.ExecuteNonQuery();

                    recordCount++;
                }
            }

            // Commit transaction
            transaction.Commit();

            return recordCount;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error importing CSV to database: {ex.Message}");
            return 0;
        }
    }
}