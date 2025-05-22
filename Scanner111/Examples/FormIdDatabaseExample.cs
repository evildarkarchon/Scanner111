using System;
using System.Data.SQLite;
using System.IO;
using Scanner111.Models;
using Scanner111.Services;

namespace Scanner111.Examples;

/// <summary>
///     Example class demonstrating how to use the FormIdDatabaseService with SQLite
///     This is not an actual test, but a usage example that can be executed in a test environment
/// </summary>
public static class FormIdDatabaseExample
{
    /// <summary>
    ///     Demonstrates creating and using a SQLite FormID database
    /// </summary>
    public static void RunExample(string tempDirectory)
    {
        // Create a temporary database file
        var dbPath = Path.Combine(tempDirectory, "TestFormIdDb.db");
        var gameName = "Fallout4";

        Console.WriteLine($"Creating test database at: {dbPath}");

        // Create the database schema
        var success = FormIdDatabaseService.CreateNewDatabase(dbPath, gameName);
        if (!success)
        {
            Console.WriteLine("Failed to create database");
            return;
        }

        // Add some test data using direct SQLite access
        Console.WriteLine("Adding test data...");
        using (var connection = new SQLiteConnection($"Data Source={dbPath};Version=3;"))
        {
            connection.Open();
            using var cmd = connection.CreateCommand();

            // Insert some test FormIDs
            cmd.CommandText = $"INSERT INTO {gameName} (formid, plugin, entry) VALUES (@formid, @plugin, @entry)";

            // Test FormID 1
            cmd.Parameters.Clear();
            cmd.Parameters.AddWithValue("@formid", "01234567");
            cmd.Parameters.AddWithValue("@plugin", "testplugin.esp");
            cmd.Parameters.AddWithValue("@entry", "FormID: 01234567 - EDID: TestItem1 - Name: Test Item 1");
            cmd.ExecuteNonQuery();

            // Test FormID 2
            cmd.Parameters.Clear();
            cmd.Parameters.AddWithValue("@formid", "89ABCDEF");
            cmd.Parameters.AddWithValue("@plugin", "testplugin.esp");
            cmd.Parameters.AddWithValue("@entry", "FormID: 89ABCDEF - EDID: TestItem2 - Name: Test Item 2");
            cmd.ExecuteNonQuery();

            // Test FormID 3 (different plugin)
            cmd.Parameters.Clear();
            cmd.Parameters.AddWithValue("@formid", "FEDCBA98");
            cmd.Parameters.AddWithValue("@plugin", "otherplugin.esp");
            cmd.Parameters.AddWithValue("@entry", "FormID: FEDCBA98 - EDID: TestItem3 - Name: Test Item 3");
            cmd.ExecuteNonQuery();
        }

        // Now create the service and use it
        var appSettings = new AppSettings
        {
            GameName = gameName,
            FormIdDatabasePath = dbPath,
            ShowFormIdValues = true
        };

        var service = new FormIdDatabaseService(appSettings);

        // Test lookups
        Console.WriteLine("Testing FormID lookups:");

        TestLookup(service, "01234567", "testplugin.esp");
        TestLookup(service, "89ABCDEF", "testplugin.esp");
        TestLookup(service, "FEDCBA98", "otherplugin.esp");

        // Test non-existent FormID
        TestLookup(service, "00000000", "testplugin.esp");

        // Test with different case
        TestLookup(service, "01234567", "TESTPLUGIN.ESP");

        // Verify that cache is working
        Console.WriteLine("Testing cache:");
        TestLookup(service, "01234567", "testplugin.esp"); // Should be from cache

        // Clean up
        service.Dispose();
        Console.WriteLine("Example completed. Database file created at: " + dbPath);
    }

    private static void TestLookup(FormIdDatabaseService service, string formId, string plugin)
    {
        var result = service.GetEntry(formId, plugin);
        Console.WriteLine($"Lookup: {formId} in {plugin} = {result ?? "null"}");
    }
}