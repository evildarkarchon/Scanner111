using System.Data;
using FluentAssertions;
using Microsoft.Data.Sqlite;
using Moq;
using Scanner111.Common.Models.Analysis;
using Scanner111.Common.Services.Analysis;
using Scanner111.Common.Services.Database;

namespace Scanner111.Common.Tests.Services.Database;

public class FormIdAnalyzerTests : IDisposable
{
    private readonly SqliteConnection _connection;
    private readonly Mock<IDatabaseConnectionFactory> _factoryMock;
    private readonly FormIdAnalyzer _analyzer;

    public FormIdAnalyzerTests()
    {
        // Use in-memory database for testing
        _connection = new SqliteConnection("DataSource=:memory:");
        _connection.Open();

        // Setup schema and seed data without Dapper
        using (var command = _connection.CreateCommand())
        {
            command.CommandText = "CREATE TABLE FormIDDatabase (FormID TEXT PRIMARY KEY, RecordName TEXT, Plugin TEXT)";
            command.ExecuteNonQuery();
        }

        using (var command = _connection.CreateCommand())
        {
            command.CommandText = "INSERT INTO FormIDDatabase (FormID, RecordName, Plugin) VALUES ('00012345', 'Iron Sword', 'Skyrim.esm')";
            command.ExecuteNonQuery();
        }

        using (var command = _connection.CreateCommand())
        {
            command.CommandText = "INSERT INTO FormIDDatabase (FormID, RecordName, Plugin) VALUES ('000ABCDE', 'Gold Coin', 'Skyrim.esm')";
            command.ExecuteNonQuery();
        }

        _factoryMock = new Mock<IDatabaseConnectionFactory>();
        _factoryMock.Setup(x => x.CreateConnectionAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(() => new SqliteConnectionWrapper(_connection)); 
    }
    
    // Wrapper to prevent disposing the in-memory connection during the test method execution
    // because FormIdAnalyzer will dispose it.
    private class SqliteConnectionWrapper : IDbConnection, IAsyncDisposable
    {
        private readonly SqliteConnection _inner;

        public SqliteConnectionWrapper(SqliteConnection inner)
        {
            _inner = inner;
        }

        public void Dispose() { /* Do nothing to keep in-memory DB alive */ }
        public ValueTask DisposeAsync() { return ValueTask.CompletedTask; }

        public string ConnectionString { get => _inner.ConnectionString; set => _inner.ConnectionString = value; }
        public int ConnectionTimeout => _inner.ConnectionTimeout;
        public string Database => _inner.Database;
        public ConnectionState State => _inner.State;

        public IDbTransaction BeginTransaction() => _inner.BeginTransaction();
        public IDbTransaction BeginTransaction(IsolationLevel il) => _inner.BeginTransaction(il);
        public void ChangeDatabase(string databaseName) => _inner.ChangeDatabase(databaseName);
        public void Close() { /* Do nothing */ }
        public IDbCommand CreateCommand() => _inner.CreateCommand();
        public void Open() { if (_inner.State == ConnectionState.Closed) _inner.Open(); }
    }

    public void Dispose()
    {
        _connection.Close();
        _connection.Dispose();
    }

    [Fact]
    public async Task LookupFormIdsAsync_WithValidFormIds_ReturnsRecordNames()
    {
        var analyzer = new FormIdAnalyzer(_factoryMock.Object);
        var formIds = new[] { "00012345", "000ABCDE", "00000000" }; // 2 valid, 1 missing

        var results = await analyzer.LookupFormIdsAsync(formIds);

        results.Should().ContainKey("00012345");
        results["00012345"].Should().Be("Iron Sword");
        
        results.Should().ContainKey("000ABCDE");
        results["000ABCDE"].Should().Be("Gold Coin");
        
        results.Should().NotContainKey("00000000");
    }

    [Fact]
    public async Task AnalyzeAsync_ExtractsAndLooksUpFormIds()
    {
        var analyzer = new FormIdAnalyzer(_factoryMock.Object);
        var segments = new List<LogSegment>
        {
            new LogSegment 
            { 
                Name = "Call Stack", 
                Lines = new[] { "  [0] 0x00012345 Skyrim.esm+12345" } 
            }
        };

        var result = await analyzer.AnalyzeAsync(segments);

        result.DetectedRecords.Should().ContainKey("00012345");
        result.DetectedRecords["00012345"].Should().Be("Iron Sword");
    }
}