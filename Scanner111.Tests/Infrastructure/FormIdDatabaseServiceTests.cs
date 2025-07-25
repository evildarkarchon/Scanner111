using System.Collections.Concurrent;
using System.Data.SQLite;
using System.Reflection;
using Scanner111.Core.Infrastructure;
using Scanner111.Tests.TestHelpers;

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

    /// Releases all resources used by the current instance of the class.
    /// This method is called to clean up resources that are no longer needed.
    /// Specifically, it deletes the temporary directory created during the test
    /// execution to store test databases.
    /// Exceptions during the cleanup process are handled silently, and
    /// no exceptions will propagate from this method.
    /// Finalization is suppressed for the object to prevent the garbage collector
    /// from finalizing it again after this method has executed.
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

    /// Verifies that the `DatabaseExists` property of the service correctly
    /// <returns>Asserts `false` when no databases are present in the specified paths.
    /// This ensures that the method accurately identifies the absence of database files.</returns>
    [Fact]
    public void DatabaseExists_WhenNoDatabasesExist_ShouldReturnFalse()
    {
        // Arrange - use a non-existent directory
        var tempService = new TestFormIdDatabaseService("Fallout4", new string[0]);

        // Act & Assert
        Assert.False(tempService.DatabaseExists);
    }

    /// Verifies that the `DatabaseExists` property of the `FormIdDatabaseService` class returns true
    /// when there are existing databases. This test ensures that the service correctly identifies
    /// the presence of databases in the configured paths.
    /// This method creates a test database, initializes the service with it, and asserts that the
    /// `DatabaseExists` property correctly reflects the presence of the database.
    [Fact]
    public void DatabaseExists_WhenDatabasesExist_ShouldReturnTrue()
    {
        // Arrange
        CreateTestDatabase(_mainDbPath);
        var service = new TestFormIdDatabaseService("Fallout4", new[] { _mainDbPath });

        // Act & Assert
        Assert.True(service.DatabaseExists);
    }

    /// Validates the behavior of retrieving an entry from the database when the database does not exist.
    /// This test ensures the method under test returns null in cases where no databases are present.
    /// The service is instantiated with no database paths, mimicking a scenario with missing database files.
    /// The null result verifies that the method correctly handles missing resources without throwing exceptions.
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

    /// Tests the behavior of the GetEntry method when the requested entry exists in the database.
    /// Ensures that the method correctly retrieves and returns the expected entry data for
    /// the specified form ID and plugin.
    /// The test sets up a temporary test database, inserts mock data, and verifies that the
    /// GetEntry method returns the correct entry description.<br/>
    /// Asserts that:<br/>
    /// - The retrieved entry matches the expected description associated with the provided form ID and plugin.:<br/>
    /// - The method behaves as intended under normal conditions with valid inputs.
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

    /// Verifies that the GetEntry method returns null when the requested entry
    /// does not exist in the database.
    /// This test ensures correct behavior when the specified form ID and plugin
    /// are not found in the database.
    /// A temporary test database is created and associated with the service for the test scope.
    /// Confirms that null is returned when querying for non-existent entries.
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

    /// Verifies that the GetEntry method behaves in a case-insensitive manner when matching plugin names.
    /// The method ensures that entries can be successfully retrieved regardless of the casing used for the plugin parameter.
    /// It checks consistency by performing lookups using various casing combinations (e.g., lower case, upper case, mixed case)
    /// and asserts that the correct entry description is returned for each case.
    /// This test relies on a test database populated with known entries, and validates that case-insensitivity
    /// is correctly enforced in string comparisons for plugin names.
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

    /// Verifies that the results of the `GetEntry` method are cached properly to improve performance.
    /// This ensures that repeated calls to `GetEntry` with the same parameters do not query the database multiple times.
    /// It checks that the caching mechanism handles case-insensitive plugin names correctly.
    /// Validates the caching functionality by confirming that a cache entry exists for the provided parameters.
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

    /// Tests the ability of the GetEntry method to search multiple databases when locating entries.
    /// This test ensures that the method queries all available source databases and retrieves
    /// the correct entry if it exists in any of them.
    /// The test sets up two test databases, inserts distinct entries into each, and verifies
    /// that the GetEntry method accurately retrieves entries from both databases.
    /// The correctness of the method is validated by asserting the returned entry values.
    /// This test emphasizes the correct functioning of multi-database search capabilities.
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

    /// Verifies that the `GetEntry` method returns the first matching entry found
    /// when duplicate entries exist across multiple databases.
    /// The order of the databases determines the precedence for resolving duplicates.
    /// The test ensures that the method retrieves the entry from the first database
    /// in the configured list that contains the matching record, even when subsequent
    /// databases contain entries with the same form ID and plugin.
    /// The goal is to validate that database query prioritization is implemented correctly.
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

    /// Tests that the GetEntry method properly handles scenarios where a corrupt
    /// database file is included in the search list. This ensures that the method
    /// skips over corrupt databases and continues searching through subsequent
    /// valid databases. The test verifies that entries can still be retrieved
    /// from other, non-corrupt databases in the presence of one or more corrupt files.
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

    /// Verifies that the GetEntry method correctly retrieves database entries
    /// for various valid FormId formats. This test ensures compatibility with
    /// multiple FormId input variations, such as alphanumeric, hexadecimal, and empty formats.
    /// <param name="formId">
    /// The FormId string to be tested. This could represent different formats,
    /// including empty strings, spaces, alphanumeric values, or hexadecimal values.
    /// </param>
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

    /// Ensures that the GetEntry method is thread-safe when accessed concurrently.
    /// This test verifies that multiple threads can simultaneously query the database
    /// without causing data corruption or unexpected behavior. It also ensures that
    /// the results returned by concurrent executions of the GetEntry method remain
    /// correct and consistent across all threads.
    /// Multiple test entries are inserted into the database, and the method is invoked
    /// simultaneously by numerous threads with different formId values.
    /// The test checks that all expected results are returned correctly and that
    /// no incorrect or missing data is encountered.
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

    /// Creates a test database at the specified path.
    /// This method initializes a SQLite database with a predefined table structure
    /// to facilitate testing of database-related functionality.
    /// <param name="dbPath">The file path where the SQLite database will be created.</param>
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

    /// Inserts test data into the specified SQLite database.
    /// This method is used to populate the database with data
    /// for testing purposes, such as entries with form IDs, plugin names,
    /// and associated descriptions.
    /// <param name="dbPath">The file path to the SQLite database where data will be inserted.</param>
    /// <param name="formId">The form ID for the entry to be inserted into the database.</param>
    /// <param name="plugin">The plugin name associated with the form ID entry.</param>
    /// <param name="entry">The description or value for the specified form ID and plugin combination.</param>
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
/// Represents a test implementation of the `IFormIdDatabaseService` interface,
/// designed to facilitate unit testing scenarios by allowing the specification
/// of custom database paths.
internal class TestFormIdDatabaseService : IFormIdDatabaseService
{
    private readonly FormIdDatabaseService _inner;
    private readonly ConcurrentDictionary<(string formId, string plugin), string>? _queryCache;

    public TestFormIdDatabaseService(string gameName, string[] databasePaths)
    {
        // Use reflection to create the service with custom paths for testing
        var field = typeof(FormIdDatabaseService).GetField("_databasePaths",
            BindingFlags.NonPublic | BindingFlags.Instance);
        var cacheField = typeof(FormIdDatabaseService).GetField("_queryCache",
            BindingFlags.NonPublic | BindingFlags.Instance);

        var logger = new TestLogger<FormIdDatabaseService>();
        _inner = new FormIdDatabaseService(gameName, logger);

        if (field != null)
            field.SetValue(_inner, databasePaths);
        if (cacheField != null)
            _queryCache = (ConcurrentDictionary<(string formId, string plugin), string>)cacheField.GetValue(_inner)!;
    }

    public bool DatabaseExists => _inner.DatabaseExists;

    /// Retrieves an entry from the database based on the specified form ID and plugin name.
    /// The method searches for an entry in the database that matches the provided form ID and plugin name.
    /// If the plugin name is provided in a different case than how it's stored in the database, the search is case-insensitive.
    /// If no entry is found or the database does not exist, the method returns null.
    /// <param name="formId">The form ID that represents the unique identifier of the entry to retrieve.</param>
    /// <param name="plugin">The name of the plugin associated with the entry. This parameter is case-insensitive.</param>
    /// <return>
    /// A string representing the description of the entry if found; otherwise, null if no matching entry exists.
    /// </return>
    public string? GetEntry(string formId, string plugin)
    {
        return _inner.GetEntry(formId, plugin);
    }

    /// Determines whether the specified combination of form ID and plugin is present in the cache.
    /// This method checks the query cache to verify if the result for the given form ID and
    /// plugin combination has been cached, enabling faster subsequent data retrieval.
    /// <param name="formId">The form ID to check for in the cache.</param>
    /// <param name="plugin">The plugin associated with the form ID to validate its presence in the cache.</param>
    /// <returns>
    /// true if the specified combination of form ID and plugin exists in the cache; otherwise, false.
    /// </returns>
    public bool IsCached(string formId, string plugin)
    {
        return _queryCache?.ContainsKey((formId, plugin)) ?? false;
    }
}