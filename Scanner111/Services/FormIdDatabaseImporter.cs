using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Scanner111.Models;

namespace Scanner111.Services;

/// <summary>
///     Service for handling FormID database import operations
///     from CSV files to SQLite format
/// </summary>
public class FormIdDatabaseImporter
{
    private readonly AppSettings _appSettings;
    private readonly FormIdDatabaseService _formIdDatabaseService;

    public FormIdDatabaseImporter(
        AppSettings appSettings,
        FormIdDatabaseService formIdDatabaseService)
    {
        _appSettings = appSettings;
        _formIdDatabaseService = formIdDatabaseService;
    }

    /// <summary>
    ///     Checks if a directory contains CSV files that can be imported
    /// </summary>
    public bool HasImportableFiles(string directoryPath)
    {
        if (string.IsNullOrEmpty(directoryPath) || !Directory.Exists(directoryPath))
            return false;

        return Directory.GetFiles(directoryPath, "*.csv").Any();
    }

    /// <summary>
    ///     Creates a SQLite database from all CSV files in the specified directory
    /// </summary>
    /// <param name="csvDirectoryPath">Directory containing CSV files with FormID data</param>
    /// <param name="targetDbPath">Path where the SQLite database should be created</param>
    /// <param name="progress">Optional progress callback (0-100)</param>
    /// <returns>Import results as a tuple (success, recordsImported, errorMessage)</returns>
    public async Task<(bool Success, int RecordsImported, string ErrorMessage)> ImportFromDirectory(
        string csvDirectoryPath,
        string targetDbPath,
        IProgress<int>? progress = null)
    {
        if (string.IsNullOrEmpty(csvDirectoryPath) || !Directory.Exists(csvDirectoryPath))
            return (false, 0, "CSV directory not found");

        var csvFiles = Directory.GetFiles(csvDirectoryPath, "*.csv");
        if (csvFiles.Length == 0) return (false, 0, "No CSV files found in the directory");

        try
        {
            // Create the database with the game name from settings
            var gameName = _appSettings.GameName;

            var success = FormIdDatabaseService.CreateNewDatabase(targetDbPath, gameName);
            if (!success) return (false, 0, "Failed to create SQLite database");

            var totalRecordsImported = 0;
            var fileCount = csvFiles.Length;

            return await Task.Run(() =>
            {
                // Import each CSV file
                for (var i = 0; i < fileCount; i++)
                {
                    var csvFile = csvFiles[i];
                    var fileName = Path.GetFileName(csvFile);

                    try
                    {
                        var recordsImported = FormIdDatabaseService.ImportCsvToDatabase(
                            targetDbPath,
                            csvFile,
                            gameName);

                        totalRecordsImported += recordsImported;

                        // Report progress
                        progress?.Report((int)((i + 1) / (double)fileCount * 100));
                    }
                    catch (Exception ex)
                    {
                        return (false, totalRecordsImported,
                            $"Error importing {fileName}: {ex.Message}");
                    }
                }

                // If successful, update the application settings
                if (totalRecordsImported <= 0) return (true, totalRecordsImported, string.Empty);
                _appSettings.FormIdDatabasePath = targetDbPath;
                _formIdDatabaseService.UpdateDatabasePath(targetDbPath);

                return (true, totalRecordsImported, string.Empty);
            });
        }
        catch (Exception ex)
        {
            return (false, 0, $"Import operation failed: {ex.Message}");
        }
    }
}