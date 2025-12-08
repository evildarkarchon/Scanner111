using System.Data;

namespace Scanner111.Common.Services.Database;

/// <summary>
/// Factory for creating database connections.
/// </summary>
public interface IDatabaseConnectionFactory
{
    /// <summary>
    /// Creates and opens a new database connection asynchronously.
    /// </summary>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>An open database connection.</returns>
    Task<IDbConnection> CreateConnectionAsync(CancellationToken ct = default);
}
