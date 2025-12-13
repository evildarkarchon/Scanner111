using System.Data;
using Microsoft.Data.Sqlite;

namespace Scanner111.Common.Services.Database;

/// <summary>
/// SQLite implementation of <see cref="IDatabaseConnectionFactory"/>.
/// </summary>
public class SqliteDatabaseConnectionFactory : IDatabaseConnectionFactory
{
    private readonly string _connectionString;

    /// <summary>
    /// Initializes a new instance of the <see cref="SqliteDatabaseConnectionFactory"/> class.
    /// </summary>
    /// <param name="dbPath">The path to the SQLite database file.</param>
    public SqliteDatabaseConnectionFactory(string dbPath)
    {
        _connectionString = $"Data Source={dbPath};Mode=ReadOnly";
    }

    /// <inheritdoc/>
    public async Task<IDbConnection> CreateConnectionAsync(CancellationToken ct = default)
    {
        var connection = new SqliteConnection(_connectionString);
        await connection.OpenAsync(ct).ConfigureAwait(false);
        return connection;
    }
}
