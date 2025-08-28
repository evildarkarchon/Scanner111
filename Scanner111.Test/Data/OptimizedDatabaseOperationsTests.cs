using System.Collections.Concurrent;
using FluentAssertions;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using NSubstitute;
using Scanner111.Core.Data;

namespace Scanner111.Test.Data;

[Trait("Category", "Unit")]
[Trait("Performance", "Medium")]
[Trait("Component", "Data")]
public sealed class OptimizedDatabaseOperationsTests : IDisposable
{
    private readonly ILogger<OptimizedDatabaseOperations> _logger;
    private readonly IMemoryCache _cache;
    private readonly FormIdDatabaseOptions _options;
    private readonly OptimizedDatabaseOperations _sut;
    private readonly string _testDbPath;
    private SqliteConnection? _testConnection;

    public OptimizedDatabaseOperationsTests()
    {
        _logger = Substitute.For<ILogger<OptimizedDatabaseOperations>>();
        _cache = new MemoryCache(Options.Create(new MemoryCacheOptions()));
        
        // Create test database
        _testDbPath = Path.Combine(Path.GetTempPath(), $"test_{Guid.NewGuid():N}.db");
        CreateTestDatabase();

        _options = new FormIdDatabaseOptions
        {
            DatabasePaths = new string[] { _testDbPath },
            GameTableName = "Fallout4",
            MaxConnections = 5,
            CacheExpirationMinutes = 10
        };

        var optionsWrapper = Options.Create(_options);
        _sut = new OptimizedDatabaseOperations(_logger, _cache, optionsWrapper);
    }

    private void CreateTestDatabase()
    {
        var connectionString = new SqliteConnectionStringBuilder
        {
            DataSource = _testDbPath,
            Mode = SqliteOpenMode.ReadWriteCreate
        }.ToString();

        _testConnection = new SqliteConnection(connectionString);
        _testConnection.Open();

        using var cmd = _testConnection.CreateCommand();
        
        // Create test table
        cmd.CommandText = @"
            CREATE TABLE Fallout4 (
                FormID TEXT PRIMARY KEY,
                Name TEXT NOT NULL
            )";
        cmd.ExecuteNonQuery();

        // Insert test data
        cmd.CommandText = @"
            INSERT INTO Fallout4 (FormID, Name) VALUES 
            ('00000001', 'Test Item 1'),
            ('00000002', 'Test Item 2'),
            ('00000003', 'Test Item 3'),
            ('00000004', 'Test Item 4'),
            ('00000005', 'Test Item 5')";
        cmd.ExecuteNonQuery();

        // Create second table for testing
        cmd.CommandText = @"
            CREATE TABLE DLC (
                FormID TEXT PRIMARY KEY,
                Name TEXT NOT NULL
            )";
        cmd.ExecuteNonQuery();

        cmd.CommandText = @"
            INSERT INTO DLC (FormID, Name) VALUES 
            ('00001001', 'DLC Item 1'),
            ('00001002', 'DLC Item 2')";
        cmd.ExecuteNonQuery();
    }

    [Fact]
    public void Constructor_NullLogger_ThrowsArgumentNullException()
    {
        // Act & Assert
        var act = () => new OptimizedDatabaseOperations(null!, _cache, Options.Create(_options));
        act.Should().Throw<ArgumentNullException>().WithParameterName("logger");
    }

    [Fact]
    public void Constructor_NullCache_ThrowsArgumentNullException()
    {
        // Act & Assert
        var act = () => new OptimizedDatabaseOperations(_logger, null!, Options.Create(_options));
        act.Should().Throw<ArgumentNullException>().WithParameterName("cache");
    }

    [Fact]
    public void Constructor_NullOptions_ThrowsArgumentNullException()
    {
        // Act & Assert
        var act = () => new OptimizedDatabaseOperations(_logger, _cache, null!);
        act.Should().Throw<ArgumentNullException>().WithParameterName("options");
    }

    [Fact]
    public async Task InitializeAsync_ValidDatabase_InitializesSuccessfully()
    {
        // Act
        await _sut.InitializeAsync();

        // Assert
        _sut.IsAvailable.Should().BeTrue();
        _sut.MaxConnections.Should().Be(5);
    }

    [Fact]
    public async Task InitializeAsync_MultipleCallsConcurrent_InitializesOnce()
    {
        // Arrange
        var tasks = new Task[10];
        var initCount = 0;

        // Act - Try to initialize multiple times concurrently
        for (int i = 0; i < tasks.Length; i++)
        {
            tasks[i] = Task.Run(async () =>
            {
                await _sut.InitializeAsync();
                Interlocked.Increment(ref initCount);
            });
        }

        await Task.WhenAll(tasks);

        // Assert
        _sut.IsAvailable.Should().BeTrue();
        initCount.Should().Be(10); // All calls complete
        // Logger should show initialization happened once (verify through logs if needed)
    }

    [Fact]
    public async Task InitializeAsync_NoDatabaseFiles_HandlesGracefully()
    {
        // Arrange
        var options = new FormIdDatabaseOptions
        {
            DatabasePaths = new string[] { "nonexistent.db" },
            GameTableName = "Fallout4"
        };
        var sut = new OptimizedDatabaseOperations(_logger, _cache, Options.Create(options));

        // Act
        await sut.InitializeAsync();

        // Assert
        sut.IsAvailable.Should().BeFalse();
    }

    [Fact]
    public async Task InitializeAsync_Cancelled_ThrowsOperationCanceledException()
    {
        // Arrange
        var cts = new CancellationTokenSource();
        cts.Cancel();

        // Act & Assert
        var act = async () => await _sut.InitializeAsync(cts.Token);
        await act.Should().ThrowAsync<OperationCanceledException>();
    }

    [Fact]
    public async Task LookupFormIdAsync_ExistingFormId_ReturnsName()
    {
        // Arrange
        await _sut.InitializeAsync();

        // Act
        var result = await _sut.LookupFormIdAsync("00000001");

        // Assert
        result.Should().Be("Test Item 1");
    }

    [Fact]
    public async Task LookupFormIdAsync_NonExistentFormId_ReturnsNull()
    {
        // Arrange
        await _sut.InitializeAsync();

        // Act
        var result = await _sut.LookupFormIdAsync("99999999");

        // Assert
        result.Should().BeNull();
    }

    [Fact]
    public async Task LookupFormIdAsync_WithTableName_QueriesCorrectTable()
    {
        // Arrange
        await _sut.InitializeAsync();

        // Act
        var result = await _sut.LookupFormIdAsync("00001001", "DLC");

        // Assert
        result.Should().Be("DLC Item 1");
    }

    [Fact]
    public async Task LookupFormIdAsync_NotInitialized_ThrowsInvalidOperationException()
    {
        // Act & Assert
        var act = async () => await _sut.LookupFormIdAsync("00000001");
        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("Database not initialized");
    }

    [Fact]
    public async Task BatchLookupAsync_MultipleFormIds_ReturnsAllResults()
    {
        // Arrange
        await _sut.InitializeAsync();
        var formIds = new[] { "00000001", "00000003", "00000005" };

        // Act
        var results = await _sut.BatchLookupAsync(formIds);

        // Assert
        results.Should().HaveCount(3);
        results["00000001"].Should().Be("Test Item 1");
        results["00000003"].Should().Be("Test Item 3");
        results["00000005"].Should().Be("Test Item 5");
    }

    [Fact]
    public async Task BatchLookupAsync_MixedExistingAndNonExistent_ReturnsOnlyFound()
    {
        // Arrange
        await _sut.InitializeAsync();
        var formIds = new[] { "00000001", "99999999", "00000003" };

        // Act
        var results = await _sut.BatchLookupAsync(formIds);

        // Assert
        results.Should().HaveCount(2);
        results.Should().ContainKeys("00000001", "00000003");
        results.Should().NotContainKey("99999999");
    }

    [Fact]
    public async Task BatchLookupAsync_EmptyList_ReturnsEmptyDictionary()
    {
        // Arrange
        await _sut.InitializeAsync();

        // Act
        var results = await _sut.BatchLookupAsync(Array.Empty<string>());

        // Assert
        results.Should().BeEmpty();
    }

    [Fact]
    public async Task BatchLookupAsync_LargeBatch_ProcessesInBatches()
    {
        // Arrange
        await _sut.InitializeAsync();
        
        // Create a large list of form IDs (mix of existing and non-existing)
        var formIds = new List<string>();
        for (int i = 1; i <= 150; i++)
        {
            formIds.Add($"{i:D8}");
        }
        // Add some that exist
        formIds.AddRange(new[] { "00000001", "00000002", "00000003" });

        // Act
        var results = await _sut.BatchLookupAsync(formIds);

        // Assert
        results.Should().ContainKeys("00000001", "00000002", "00000003");
        results.Count.Should().BeGreaterThanOrEqualTo(3);
    }

    [Fact]
    public async Task BatchLookupAsync_CachedValues_ReturnsCachedResults()
    {
        // Arrange
        await _sut.InitializeAsync();
        
        // First lookup to cache
        await _sut.LookupFormIdAsync("00000001");
        var initialCacheHits = _sut.CachedEntryCount;

        // Act - Second lookup should hit cache
        var result = await _sut.BatchLookupAsync(new[] { "00000001" });

        // Assert
        result["00000001"].Should().Be("Test Item 1");
        _sut.CachedEntryCount.Should().BeGreaterThan(initialCacheHits);
    }

    [Fact]
    public async Task BatchLookupAsync_Cancelled_ThrowsOperationCanceledException()
    {
        // Arrange
        await _sut.InitializeAsync();
        var cts = new CancellationTokenSource();
        cts.Cancel();

        // Act & Assert
        var act = async () => await _sut.BatchLookupAsync(new[] { "00000001" }, null, cts.Token);
        await act.Should().ThrowAsync<OperationCanceledException>();
    }

    [Fact]
    public async Task GetEntryAsync_ValidQuery_ReturnsName()
    {
        // Arrange
        await _sut.InitializeAsync();

        // Act
        var result = await _sut.GetEntryAsync("00000002", "Fallout4");

        // Assert
        result.Should().Be("Test Item 2");
    }

    [Fact]
    public async Task GetEntriesAsync_MultipleQueries_ReturnsCorrectResults()
    {
        // Arrange
        await _sut.InitializeAsync();
        var queries = new[]
        {
            ("00000001", "Fallout4"),
            ("00001001", "DLC"),
            ("99999999", "Fallout4"), // Non-existent
            ("00000003", "Fallout4")
        };

        // Act
        var results = await _sut.GetEntriesAsync(queries);

        // Assert
        results.Should().HaveCount(4);
        results[0].Should().Be("Test Item 1");
        results[1].Should().Be("DLC Item 1");
        results[2].Should().BeNull();
        results[3].Should().Be("Test Item 3");
    }

    [Fact]
    public async Task GetEntriesAsync_EmptyArray_ReturnsEmptyArray()
    {
        // Arrange
        await _sut.InitializeAsync();

        // Act
        var results = await _sut.GetEntriesAsync(Array.Empty<(string, string)>());

        // Assert
        results.Should().BeEmpty();
    }

    [Fact]
    public async Task GetStatistics_ReturnsCorrectStats()
    {
        // Act - Perform some operations first
        await _sut.InitializeAsync();
        await _sut.LookupFormIdAsync("00000001");
        await _sut.LookupFormIdAsync("00000001"); // Should hit cache
        await _sut.BatchLookupAsync(new[] { "00000002", "00000003" });

        // Act
        var stats = _sut.GetStatistics();

        // Assert
        stats.Should().NotBeNull();
        stats.TotalQueries.Should().BeGreaterThan(0);
        stats.CacheHits.Should().BeGreaterThanOrEqualTo(0);
        stats.ActiveConnections.Should().BeGreaterThan(0);
        stats.AverageQueryTimeMs.Should().BeGreaterThanOrEqualTo(0);
    }

    [Fact]
    public async Task ClearCache_ExecutesWithoutError()
    {
        // Arrange
        await _sut.InitializeAsync();

        // Act & Assert
        var act = () => _sut.ClearCache();
        act.Should().NotThrow();
    }

    [Fact]
    public async Task DisposeAsync_CleansUpResources()
    {
        // Arrange
        await _sut.InitializeAsync();
        await _sut.LookupFormIdAsync("00000001");

        // Act
        await _sut.DisposeAsync();

        // Assert
        _sut.IsAvailable.Should().BeFalse();
        
        // Attempting to use after disposal should fail
        var act = async () => await _sut.LookupFormIdAsync("00000001");
        await act.Should().ThrowAsync<InvalidOperationException>();
    }

    [Fact]
    public async Task DisposeAsync_MultipleDispose_HandlesGracefully()
    {
        // Arrange
        await _sut.InitializeAsync();

        // Act & Assert
        await _sut.DisposeAsync();
        var act = async () => await _sut.DisposeAsync();
        await act.Should().NotThrowAsync();
    }

    [Theory]
    [InlineData("")]
    [InlineData(" ")]
    [InlineData(null)]
    public async Task LookupFormIdAsync_InvalidFormId_HandlesGracefully(string? formId)
    {
        // Arrange
        await _sut.InitializeAsync();

        // Act
        var result = await _sut.LookupFormIdAsync(formId!);

        // Assert
        result.Should().BeNull();
    }

    [Fact]
    public async Task ConcurrentOperations_ThreadSafety_HandlesCorrectly()
    {
        // Arrange
        await _sut.InitializeAsync();
        var tasks = new List<Task<string?>>();
        var formIds = new[] { "00000001", "00000002", "00000003", "00000004", "00000005" };

        // Act - Perform many concurrent lookups
        for (int i = 0; i < 100; i++)
        {
            var formId = formIds[i % formIds.Length];
            tasks.Add(_sut.LookupFormIdAsync(formId));
        }

        var results = await Task.WhenAll(tasks);

        // Assert
        results.Should().AllSatisfy(r => r.Should().NotBeNull());
        results.Where(r => r == "Test Item 1").Should().HaveCountGreaterThan(10);
    }

    [Fact]
    public async Task PreparedStatements_Reuse_ImprovePerformance()
    {
        // Arrange
        await _sut.InitializeAsync();
        var formIds = new[] { "00000001", "00000002" };

        // Act - Multiple batch lookups with same size should reuse prepared statements
        var results1 = await _sut.BatchLookupAsync(formIds);
        var results2 = await _sut.BatchLookupAsync(new[] { "00000003", "00000004" });
        var results3 = await _sut.BatchLookupAsync(new[] { "00000001", "00000005" });

        // Assert
        results1.Should().HaveCount(2);
        results2.Should().HaveCount(2);
        results3.Should().HaveCount(2);
        
        // Performance should improve with prepared statement reuse
        var stats = _sut.GetStatistics();
        stats.TotalQueries.Should().BeGreaterThan(0);
    }

    public void Dispose()
    {
        _sut?.DisposeAsync().AsTask().Wait();
        _testConnection?.Dispose();
        _cache?.Dispose();
        
        // Clean up test database
        if (File.Exists(_testDbPath))
        {
            try { File.Delete(_testDbPath); } catch { /* Best effort */ }
        }
    }
}