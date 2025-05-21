using System;
using System.IO;
using System.Threading.Tasks;
using Scanner111.Models;
using Scanner111.Services;
using Xunit;

namespace Scanner111.Tests.Services
{
    public class FormIdDatabaseImporterTests : IDisposable
    {
        private readonly string _tempDir;
        private readonly string _dbPath;
        private readonly string _csvDir;
        private readonly AppSettings _appSettings;

        public FormIdDatabaseImporterTests()
        {
            // Setup for tests
            _tempDir = Path.Combine(Path.GetTempPath(), "ImporterTests_" + Guid.NewGuid());
            Directory.CreateDirectory(_tempDir);
            _dbPath = Path.Combine(_tempDir, "ImportTest.db");
            _csvDir = Path.Combine(_tempDir, "CsvFiles");
            Directory.CreateDirectory(_csvDir);

            // Create app settings with test values
            _appSettings = new AppSettings
            {
                GameName = "TestGame",
                FormIdDatabasePath = null, // Initially no database
                LocalDir = _tempDir
            };

            // Create test CSV files
            CreateTestCsvFiles();
        }

        public void Dispose()
        {
            // Cleanup after tests
            try
            {
                if (Directory.Exists(_tempDir))
                {
                    Directory.Delete(_tempDir, true);
                }
            }
            catch
            {
                // Ignore cleanup errors
            }
        }

        private void CreateTestCsvFiles()
        {
            // Create a few CSV files for testing
            File.WriteAllText(
                Path.Combine(_csvDir, "file1.csv"),
                "FormID,Plugin,EditorID,Name\n" +
                "ABCD1234,test1.esp,Test1,Test Item 1\n" +
                "EFGH5678,test1.esp,Test2,Test Item 2");

            File.WriteAllText(
                Path.Combine(_csvDir, "file2.csv"),
                "FormID,Plugin,EditorID,Name\n" +
                "1234ABCD,test2.esp,Test3,Test Item 3\n" +
                "5678EFGH,test2.esp,Test4,Test Item 4");
        }

        [Fact]
        public void HasImportableFiles_ReturnsTrueForDirectoryWithCsvFiles()
        {
            // Arrange
            var formIdDatabaseService = new FormIdDatabaseService(_appSettings);
            var importer = new FormIdDatabaseImporter(_appSettings, formIdDatabaseService);

            // Act
            var result = importer.HasImportableFiles(_csvDir);

            // Assert
            Assert.True(result);

            // Test for a directory with no CSV files
            var emptyDir = Path.Combine(_tempDir, "EmptyDir");
            Directory.CreateDirectory(emptyDir);
            Assert.False(importer.HasImportableFiles(emptyDir));

            // Test for non-existent directory
            Assert.False(importer.HasImportableFiles(Path.Combine(_tempDir, "NonExistentDir")));
        }

        [Fact]
        public async Task ImportFromDirectory_CreatesAndPopulatesDatabaseCorrectly()
        {
            // Arrange
            var formIdDatabaseService = new FormIdDatabaseService(_appSettings);
            var importer = new FormIdDatabaseImporter(_appSettings, formIdDatabaseService);

            // Create the databases subfolder
            var databasesDir = Path.Combine(_appSettings.LocalDir, "CLASSIC Data", "databases");
            Directory.CreateDirectory(databasesDir);
            var targetDbPath = Path.Combine(databasesDir, $"{_appSettings.GameName}_FormIDs.db");

            // Track progress
            int progressCalled = 0;
            var progress = new Progress<int>(value =>
            {
                progressCalled++;
                Assert.True(value >= 0 && value <= 100);
            });

            // Act
            var result = await importer.ImportFromDirectory(_csvDir, targetDbPath, progress);

            // Assert
            Assert.True(result.Success);
            Assert.True(result.RecordsImported > 0);
            Assert.Empty(result.ErrorMessage);
            Assert.True(progressCalled > 0);

            // Verify database exists
            Assert.True(File.Exists(targetDbPath));

            // Verify database contains imported records
            using var connection = new System.Data.SQLite.SQLiteConnection($"Data Source={targetDbPath};Version=3;");
            connection.Open();
            using var cmd = connection.CreateCommand();

            cmd.CommandText = $"SELECT COUNT(*) FROM {_appSettings.GameName}";
            var count = Convert.ToInt32(cmd.ExecuteScalar());
            Assert.Equal(4, count); // We created 4 test records total

            // Verify FormIdDatabaseService was updated
            Assert.Equal(targetDbPath, _appSettings.FormIdDatabasePath);
            Assert.Contains(targetDbPath, _appSettings.FormIdDbPaths);
        }
    }
}
