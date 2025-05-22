using System.Data.SQLite;
using Scanner111.Models;
using Scanner111.Services;

namespace Scanner111.Tests.Services;

public class FormIdDatabaseServiceTests : IDisposable
{
    private readonly string _dbPath;
    private readonly string _gameName;
    private readonly string _tempDir;

    public FormIdDatabaseServiceTests()
    {
        // Setup for tests
        _tempDir = Path.Combine(Path.GetTempPath(), "FormIdTests_" + Guid.NewGuid());
        Directory.CreateDirectory(_tempDir);
        _dbPath = Path.Combine(_tempDir, "TestFormIdDb.db");
        _gameName = "TestGame";
    }

    public void Dispose()
    {
        // Cleanup after tests
        try
        {
            if (Directory.Exists(_tempDir)) Directory.Delete(_tempDir, true);
        }
        catch
        {
            // Ignore cleanup errors
        }
    }

    [Fact]
    public void CreateNewDatabase_CreatesValidDatabase()
    {
        // Act
        var result = FormIdDatabaseService.CreateNewDatabase(_dbPath, _gameName);

        // Assert
        Assert.True(result);
        Assert.True(File.Exists(_dbPath));

        // Verify the database has the correct schema
        using var connection = new SQLiteConnection($"Data Source={_dbPath};Version=3;");
        connection.Open();
        using var cmd = connection.CreateCommand();

        // Check if table exists
        cmd.CommandText = $"SELECT name FROM sqlite_master WHERE type='table' AND name='{_gameName}'";
        var tableName = cmd.ExecuteScalar() as string;
        Assert.Equal(_gameName, tableName);

        // Check columns
        cmd.CommandText = $"PRAGMA table_info({_gameName})";
        using var reader = cmd.ExecuteReader();
        var hasFormId = false;
        var hasPlugin = false;
        var hasEntry = false;

        while (reader.Read())
        {
            var colName = reader["name"].ToString();
            switch (colName)
            {
                case "formid":
                    hasFormId = true;
                    break;
                case "plugin":
                    hasPlugin = true;
                    break;
                case "entry":
                    hasEntry = true;
                    break;
            }
        }

        Assert.True(hasFormId);
        Assert.True(hasPlugin);
        Assert.True(hasEntry);
    }

    [Fact]
    public void GetEntry_ReturnsCorrectData()
    {
        // Arrange
        FormIdDatabaseService.CreateNewDatabase(_dbPath, _gameName);
        var appSettings = new AppSettings
        {
            FormIdDatabasePath = _dbPath,
            GameName = _gameName
        };

        // Add test data directly to the database
        using (var connection = new SQLiteConnection($"Data Source={_dbPath};Version=3;"))
        {
            connection.Open();
            using var cmd = connection.CreateCommand();
            cmd.CommandText = $"INSERT INTO {_gameName} (formid, plugin, entry) VALUES (@formid, @plugin, @entry)";

            cmd.Parameters.AddWithValue("@formid", "ABCD1234");
            cmd.Parameters.AddWithValue("@plugin", "test.esp");
            cmd.Parameters.AddWithValue("@entry", "Test Entry");
            cmd.ExecuteNonQuery();
        }

        // Create the service
        var service = new FormIdDatabaseService(appSettings);

        // Act
        var result = service.GetEntry("ABCD1234", "test.esp");

        // Assert
        Assert.Equal("Test Entry", result);

        // Also check case insensitivity
        var resultCaseInsensitive = service.GetEntry("abcd1234", "TEST.ESP");
        Assert.Equal("Test Entry", resultCaseInsensitive);

        // Check caching by retrieving the same item again
        var cachedResult = service.GetEntry("ABCD1234", "test.esp");
        Assert.Equal("Test Entry", cachedResult);
    }

    [Fact]
    public void ImportCsvToDatabase_ImportsDataCorrectly()
    {
        // Arrange
        FormIdDatabaseService.CreateNewDatabase(_dbPath, _gameName);
        var csvPath = Path.Combine(_tempDir, "test.csv");

        // Create a test CSV file
        File.WriteAllText(csvPath,
            "FormID,Plugin,EditorID,Name\n" +
            "ABCD1234,test.esp,TestItem,Test Item Name\n" +
            "5678EFGH,another.esp,AnotherTest,Another Test Name");

        // Act
        var recordCount = FormIdDatabaseService.ImportCsvToDatabase(
            _dbPath, csvPath, _gameName);

        // Assert
        Assert.Equal(2, recordCount);

        // Verify data was imported correctly
        using var connection = new SQLiteConnection($"Data Source={_dbPath};Version=3;");
        connection.Open();
        using var cmd = connection.CreateCommand();

        cmd.CommandText = $"SELECT COUNT(*) FROM {_gameName}";
        var count = Convert.ToInt32(cmd.ExecuteScalar());
        Assert.Equal(2, count);

        cmd.CommandText = $"SELECT entry FROM {_gameName} WHERE formid='ABCD1234' AND plugin='test.esp'";
        var entry = cmd.ExecuteScalar() as string;
        Assert.Contains("TestItem", entry);
        Assert.Contains("Test Item Name", entry);
    }
}