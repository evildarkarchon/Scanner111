using System.Collections.Concurrent;
using System.Data.SQLite;
using System.Reflection;
using Scanner111.Core.Infrastructure;

namespace Scanner111.Tests.Infrastructure;

public class FormIdDatabaseServiceTests : IDisposable
{
    private readonly string _localDbPath;
    private readonly string _mainDbPath;
    private readonly string _testDataPath;

    public FormIdDatabaseServiceTests()
    {
        // Create a temporary directory for test databases
        _testDataPath = Path.Combine(Path.GetTempPath(), "Scanner111Tests", Guid.NewGuid().ToString());
        Directory.CreateDirectory(Path.Combine(_testDataPath, "databases"));

        _mainDbPath = Path.Combine(_testDataPath, "databases", "Fallout4 FormIDs Main.db");
        _localDbPath = Path.Combine(_testDataPath, "databases", "Fallout4 FormIDs Local.db");
    }

    public void Dispose()
    {
        try
        {
            if (Directory.Exists(_testDataPath)) Directory.Delete(_testDataPath, true);
        }
        catch
        {
            // Ignore cleanup errors
        }
        finally
        {
            GC.SuppressFinalize(this);
        }
    }

    [Fact]
    public void DatabaseExists_WhenNoDatabasesExist_ShouldReturnFalse()
    {
        // Arrange - use a non-existent directory
        var tempService = new TestFormIdDatabaseService("Fallout4", new string[0]);

        // Act & Assert
        Assert.False(tempService.DatabaseExists);
    }

    [Fact]
    public void DatabaseExists_WhenDatabasesExist_ShouldReturnTrue()
    {
        // Arrange
        CreateTestDatabase(_mainDbPath);
        var service = new TestFormIdDatabaseService("Fallout4", new[] { _mainDbPath });

        // Act & Assert
        Assert.True(service.DatabaseExists);
    }

    [Fact]
    public void GetEntry_WhenDatabaseDoesNotExist_ShouldReturnNull()
    {
        // Arrange
        var service = new TestFormIdDatabaseService("Fallout4", new string[0]);

        // Act
        var result = service.GetEntry("12345", "TestPlugin.esp");

        // Assert
        Assert.Null(result);
    }

    [Fact]
    public void GetEntry_WhenEntryExists_ShouldReturnEntry()
    {
        // Arrange
        CreateTestDatabase(_mainDbPath);
        InsertTestData(_mainDbPath, "12345", "TestPlugin.esp", "Test Entry Description");

        var service = new TestFormIdDatabaseService("Fallout4", new[] { _mainDbPath });

        // Act
        var result = service.GetEntry("12345", "TestPlugin.esp");

        // Assert
        Assert.Equal("Test Entry Description", result);
    }

    [Fact]
    public void GetEntry_WhenEntryDoesNotExist_ShouldReturnNull()
    {
        // Arrange
        CreateTestDatabase(_mainDbPath);
        var service = new TestFormIdDatabaseService("Fallout4", new[] { _mainDbPath });

        // Act
        var result = service.GetEntry("99999", "NonExistentPlugin.esp");

        // Assert
        Assert.Null(result);
    }

    [Fact]
    public void GetEntry_ShouldBeCaseInsensitiveForPlugin()
    {
        // Arrange
        CreateTestDatabase(_mainDbPath);
        InsertTestData(_mainDbPath, "12345", "TestPlugin.esp", "Test Entry Description");

        var service = new TestFormIdDatabaseService("Fallout4", new[] { _mainDbPath });

        // Act
        var result1 = service.GetEntry("12345", "TestPlugin.esp");
        var result2 = service.GetEntry("12345", "TESTPLUGIN.ESP");
        var result3 = service.GetEntry("12345", "testplugin.esp");

        // Assert
        Assert.Equal("Test Entry Description", result1);
        Assert.Equal("Test Entry Description", result2);
        Assert.Equal("Test Entry Description", result3);
    }

    [Fact]
    public void GetEntry_ShouldCacheResults()
    {
        // Arrange
        CreateTestDatabase(_mainDbPath);
        InsertTestData(_mainDbPath, "12345", "TestPlugin.esp", "Test Entry Description");

        var service = new TestFormIdDatabaseService("Fallout4", new[] { _mainDbPath });

        // Act - Call multiple times
        var result1 = service.GetEntry("12345", "TestPlugin.esp");
        var result2 = service.GetEntry("12345", "TestPlugin.esp");
        var result3 = service.GetEntry("12345", "TESTPLUGIN.ESP"); // Different case

        // Assert
        Assert.Equal("Test Entry Description", result1);
        Assert.Equal("Test Entry Description", result2);
        Assert.Equal("Test Entry Description", result3);

        // Verify caching is working by checking the cache
        Assert.True(service.IsCached("12345".ToUpper(), "TestPlugin.esp"));
    }

    [Fact]
    public void GetEntry_WithMultipleDatabases_ShouldSearchBoth()
    {
        // Arrange
        CreateTestDatabase(_mainDbPath);
        CreateTestDatabase(_localDbPath);

        InsertTestData(_mainDbPath, "11111", "MainPlugin.esp", "Entry from Main DB");
        InsertTestData(_localDbPath, "22222", "LocalPlugin.esp", "Entry from Local DB");

        var service = new TestFormIdDatabaseService("Fallout4", new[] { _mainDbPath, _localDbPath });

        // Act
        var result1 = service.GetEntry("11111", "MainPlugin.esp");
        var result2 = service.GetEntry("22222", "LocalPlugin.esp");

        // Assert
        Assert.Equal("Entry from Main DB", result1);
        Assert.Equal("Entry from Local DB", result2);
    }

    [Fact]
    public void GetEntry_WithDuplicateEntries_ShouldReturnFirstFound()
    {
        // Arrange
        CreateTestDatabase(_mainDbPath);
        CreateTestDatabase(_localDbPath);

        // Insert same FormID/Plugin in both databases with different entries
        InsertTestData(_mainDbPath, "12345", "TestPlugin.esp", "Entry from Main DB");
        InsertTestData(_localDbPath, "12345", "TestPlugin.esp", "Entry from Local DB");

        var service = new TestFormIdDatabaseService("Fallout4", new[] { _mainDbPath, _localDbPath });

        // Act
        var result = service.GetEntry("12345", "TestPlugin.esp");

        // Assert
        Assert.Equal("Entry from Main DB", result); // Should return from first database
    }

    [Fact]
    public void GetEntry_WithCorruptDatabase_ShouldContinueToNextDatabase()
    {
        // Arrange
        CreateTestDatabase(_localDbPath);
        InsertTestData(_localDbPath, "12345", "TestPlugin.esp", "Entry from Local DB");

        // Create a corrupt database file (just empty file)
        File.WriteAllText(_mainDbPath, "This is not a valid SQLite database");

        var service = new TestFormIdDatabaseService("Fallout4", new[] { _mainDbPath, _localDbPath });

        // Act
        var result = service.GetEntry("12345", "TestPlugin.esp");

        // Assert
        Assert.Equal("Entry from Local DB", result); // Should find in second database despite first being corrupt
    }

    [Theory]
    [InlineData("")]
    [InlineData(" ")]
    [InlineData("ABCDEF")]
    [InlineData("123456")]
    [InlineData("FF0000")]
    public void GetEntry_WithVariousFormIdFormats_ShouldWork(string formId)
    {
        // Arrange
        CreateTestDatabase(_mainDbPath);
        InsertTestData(_mainDbPath, formId, "TestPlugin.esp", $"Entry for {formId}");

        var service = new TestFormIdDatabaseService("Fallout4", new[] { _mainDbPath });

        // Act
        var result = service.GetEntry(formId, "TestPlugin.esp");

        // Assert
        Assert.Equal($"Entry for {formId}", result);
    }

    [Fact]
    public async Task GetEntry_ConcurrentAccess_ShouldBeThreadSafe()
    {
        // Arrange
        CreateTestDatabase(_mainDbPath);

        // Insert multiple test entries
        for (var i = 0; i < 100; i++) InsertTestData(_mainDbPath, i.ToString("X6"), "TestPlugin.esp", $"Entry {i}");

        var service = new TestFormIdDatabaseService("Fallout4", new[] { _mainDbPath });
        var tasks = new List<Task<string?>>();

        // Act - Access from multiple threads simultaneously
        for (var i = 0; i < 50; i++)
        {
            var formId = i.ToString("X6");
            tasks.Add(Task.Run(() => service.GetEntry(formId, "TestPlugin.esp")));
        }

        var results = await Task.WhenAll(tasks);

        // Assert
        Assert.Equal(50, results.Length);
        for (var i = 0; i < 50; i++) Assert.Equal($"Entry {i}", results[i]);
    }

    private void CreateTestDatabase(string dbPath)
    {
        var connectionString = $"Data Source={dbPath};Version=3;";

        using var connection = new SQLiteConnection(connectionString);
        connection.Open();

        using var command = new SQLiteCommand(@"
            CREATE TABLE IF NOT EXISTS Fallout4 (
                formid TEXT,
                plugin TEXT,
                entry TEXT
            )", connection);
        command.ExecuteNonQuery();
    }

    private void InsertTestData(string dbPath, string formId, string plugin, string entry)
    {
        var connectionString = $"Data Source={dbPath};Version=3;";

        using var connection = new SQLiteConnection(connectionString);
        connection.Open();

        using var command = new SQLiteCommand(@"
            INSERT INTO Fallout4 (formid, plugin, entry) 
            VALUES (@formid, @plugin, @entry)", connection);
        command.Parameters.AddWithValue("@formid", formId);
        command.Parameters.AddWithValue("@plugin", plugin);
        command.Parameters.AddWithValue("@entry", entry);
        command.ExecuteNonQuery();
    }
}

// Test implementation that allows us to control database paths
internal class TestFormIdDatabaseService : IFormIdDatabaseService
{
    private readonly FormIdDatabaseService _inner;
    private readonly ConcurrentDictionary<(string formId, string plugin), string>? _queryCache;

    public TestFormIdDatabaseService(string gameName, string[] databasePaths)
    {
        // Use reflection to create the service with custom paths for testing
        var field = typeof(FormIdDatabaseService).GetField("_databasePaths",
            BindingFlags.NonPublic | BindingFlags.Instance);
        var existsField = typeof(FormIdDatabaseService).GetField("_databasesExist",
            BindingFlags.NonPublic | BindingFlags.Instance);
        var cacheField = typeof(FormIdDatabaseService).GetField("_queryCache",
            BindingFlags.NonPublic | BindingFlags.Instance);

        _inner = new FormIdDatabaseService(gameName);

        if (field != null)
            field.SetValue(_inner, databasePaths);
        if (existsField != null)
            existsField.SetValue(_inner, databasePaths.Any(File.Exists));
        if (cacheField != null)
            _queryCache = (ConcurrentDictionary<(string formId, string plugin), string>)cacheField.GetValue(_inner)!;
    }

    public bool DatabaseExists => _inner.DatabaseExists;

    public string? GetEntry(string formId, string plugin)
    {
        return _inner.GetEntry(formId, plugin);
    }

    public bool IsCached(string formId, string plugin)
    {
        return _queryCache?.ContainsKey((formId, plugin)) ?? false;
    }
}