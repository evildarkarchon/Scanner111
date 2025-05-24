using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Scanner111.Services.Interfaces;

namespace Scanner111.Services;

/// <summary>
/// Service responsible for handling crash log files.
/// </summary>
public class CrashLogFileService : ICrashLogFileService
{
    private const string CrashLogPattern = "crash-*.log";
    private const string CrashAutoscanPattern = "crash-*-AUTOSCAN.md";
    private const string CrashLogsDirectoryName = "Crash Logs";
    private const string PastebinDirectoryName = "Pastebin";
    private const string PluginsSectionHeader = "PLUGINS:";

    private readonly ILogger<CrashLogFileService> _logger;
    private readonly IYamlSettingsCache _yamlSettingsCache;

    /// <summary>
    ///     Creates a new instance of the CrashLogFileService class.
    /// </summary>
    /// <param name="yamlSettingsCache">The YAML settings cache service.</param>
    /// <param name="logger">Optional logger for logging messages.</param>
    public CrashLogFileService(IYamlSettingsCache yamlSettingsCache, ILogger<CrashLogFileService> logger)
    {
        _yamlSettingsCache = yamlSettingsCache;
        _logger = logger;
    }

    /// <summary>
    /// Asynchronously retrieves a list of crash log files from specified directories.
    /// </summary>
    /// <returns>
    /// A task that represents the asynchronous operation. The task result contains
    /// a list of file paths for all discovered crash log files.
    /// </returns>
    public async Task<List<string>> GetCrashLogFilesAsync()
    {
        var baseFolder = Directory.GetCurrentDirectory();
        var crashLogsDir = Path.Combine(baseFolder, CrashLogsDirectoryName);
        var pastebinDir = Path.Combine(crashLogsDir, PastebinDirectoryName);

        EnsureDirectoriesExist(crashLogsDir, pastebinDir);
        await CollectCrashLogFilesFromBaseDirectoryAsync(baseFolder, crashLogsDir);
        await CollectCrashLogFilesFromCustomDirectoriesAsync(crashLogsDir);

        var crashFiles = Directory.GetFiles(crashLogsDir, CrashLogPattern, SearchOption.AllDirectories).ToList();
        _logger.LogInformation("Found {CrashFileCount} crash log files", crashFiles.Count);

        return crashFiles;
    }

    /// <summary>
    /// Reformats the provided crash log files by removing specified patterns.
    /// </summary>
    /// <param name="crashLogFiles">A collection of file paths representing the crash logs to be reformatted.</param>
    /// <param name="removePatterns">A collection of patterns to be removed from the crash log files during reformatting.</param>
    /// <returns>An asynchronous task representing the reformatting operation.</returns>
    public async Task ReformatCrashLogsAsync(IEnumerable<string> crashLogFiles, IEnumerable<string> removePatterns)
    {
        var removePatternsList = removePatterns.ToList();
        foreach (var file in crashLogFiles) await ReformatSingleLogFileAsync(file, removePatternsList);
    }

    /// <summary>
    /// Asynchronously moves files from the source directory to the target directory based on the specified pattern.
    /// </summary>
    /// <param name="sourceDir">The directory from which files are to be moved.</param>
    /// <param name="targetDir">The directory to which files are to be moved.</param>
    /// <param name="pattern">The search pattern to match files for moving.</param>
    /// <returns>A task that represents the asynchronous operation.</returns>
    public async Task MoveFilesAsync(string sourceDir, string targetDir, string pattern)
    {
        await ProcessFilesAsync(sourceDir, targetDir, pattern, MoveFileWithErrorHandling);
    }

    /// <summary>
    /// Copies files matching the specified pattern from the source directory to the target directory.
    /// </summary>
    /// <param name="sourceDir">The source directory containing the files to be copied.</param>
    /// <param name="targetDir">The target directory where the files will be copied.</param>
    /// <param name="pattern">The search pattern used to identify the files to copy.</param>
    /// <returns>
    /// A task that represents the asynchronous operation of copying files.
    /// </returns>
    public async Task CopyFilesAsync(string sourceDir, string targetDir, string pattern)
    {
        await ProcessFilesAsync(sourceDir, targetDir, pattern, CopyFileWithErrorHandling);
    }

    /// <summary>
    /// Processes files in the specified source directory based on a given pattern
    /// and applies a specified file operation for each file to transfer it to the target directory.
    /// </summary>
    /// <param name="sourceDir">The source directory from which files are processed.</param>
    /// <param name="targetDir">The target directory where files are moved or copied.</param>
    /// <param name="pattern">The search pattern used to filter files in the source directory.</param>
    /// <param name="fileOperation">
    /// The action to perform on each file, such as moving or copying.
    /// The operation is defined as a callback method taking the source and destination file paths as parameters.
    /// </param>
    /// <returns>A task that represents the asynchronous operation.</returns>
    private async Task ProcessFilesAsync(string sourceDir, string targetDir, string pattern,
        Action<string, string> fileOperation)
    {
        await Task.Run(() =>
        {
            if (!Directory.Exists(sourceDir)) return;

            var files = Directory.GetFiles(sourceDir, pattern);
            foreach (var file in files)
            {
                var fileName = Path.GetFileName(file);
                var destinationFile = Path.Combine(targetDir, fileName);

                if (!File.Exists(destinationFile)) fileOperation(file, destinationFile);
            }
        });
    }

    /// <summary>
    /// Attempts to move a file from the source path to the destination path with error handling.
    /// </summary>
    /// <param name="sourcePath">The full path of the source file to be moved.</param>
    /// <param name="destinationPath">The full path of the destination where the file should be moved.</param>
    private void MoveFileWithErrorHandling(string sourcePath, string destinationPath)
    {
        try
        {
            File.Move(sourcePath, destinationPath);
        }
        catch (Exception ex)
        {
            LogFileOperationError("moving", sourcePath, ex);
        }
    }

    /// <summary>
    /// Copies a file from the source path to the destination path with error handling.
    /// Logs an error message if the file operation fails.
    /// </summary>
    /// <param name="sourcePath">The path of the source file to be copied.</param>
    /// <param name="destinationPath">The path where the file will be copied to.</param>
    private void CopyFileWithErrorHandling(string sourcePath, string destinationPath)
    {
        try
        {
            File.Copy(sourcePath, destinationPath, false);
        }
        catch (Exception ex)
        {
            LogFileOperationError("copying", sourcePath, ex);
        }
    }

    /// <summary>
    /// Logs an error that occurred during a file operation, such as moving, copying, or reformatting a file.
    /// </summary>
    /// <param name="operation">The type of file operation being performed (e.g., "moving", "copying", "reformatting").</param>
    /// <param name="filePath">The file path associated with the failed operation.</param>
    /// <param name="ex">The exception that was thrown during the file operation.</param>
    private void LogFileOperationError(string operation, string filePath, Exception ex)
    {
        var errorMessage = $"Error {operation} {filePath}: {ex.Message}";
        _logger.LogError(ex, errorMessage);
    }

    /// <summary>
    /// Ensures that the specified directories exist by creating them if they do not already exist.
    /// </summary>
    /// <param name="directories">An array of directory paths to check and create if necessary.</param>
    private void EnsureDirectoriesExist(params string[] directories)
    {
        foreach (var directory in directories) Directory.CreateDirectory(directory);
    }

    /// <summary>
    /// Asynchronously collects crash log files from the specified base directory
    /// and moves them to the given crash logs directory.
    /// </summary>
    /// <param name="baseFolder">The base directory from which crash log files are collected.</param>
    /// <param name="crashLogsDir">The directory where the collected crash log files will be moved to.</param>
    /// <returns>A task that represents the asynchronous operation.</returns>
    private async Task CollectCrashLogFilesFromBaseDirectoryAsync(string baseFolder, string crashLogsDir)
    {
        await MoveFilesAsync(baseFolder, crashLogsDir, CrashLogPattern);
        await MoveFilesAsync(baseFolder, crashLogsDir, CrashAutoscanPattern);
    }

    /// <summary>
    /// Collects crash log files from custom directories and places them in the CrashLogs directory.
    /// </summary>
    /// <param name="crashLogsDir">The directory where crash log files should be collected.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    private async Task CollectCrashLogFilesFromCustomDirectoriesAsync(string crashLogsDir)
    {
        await CollectFromCustomScanFolder(crashLogsDir);
        await CollectFromXseFolder(crashLogsDir);
    }

    /// <summary>
    /// Collects crash log files from a custom scan folder and copies them to the specified crash logs directory.
    /// </summary>
    /// <param name="crashLogsDir">The directory where crash log files should be copied.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    private async Task CollectFromCustomScanFolder(string crashLogsDir)
    {
        var customScanPath = _yamlSettingsCache.GetSetting<string>(YamlStore.Settings, "SCAN Custom Path");
        if (!string.IsNullOrEmpty(customScanPath) && Directory.Exists(customScanPath))
        {
            _logger.LogInformation("Checking custom scan folder: {CustomScanPath}", customScanPath);
            await CopyFilesAsync(customScanPath, crashLogsDir, CrashLogPattern);
        }
    }

    /// <summary>
    /// Collects crash log files from the XSE folder and copies them to the specified crash logs directory.
    /// </summary>
    /// <param name="crashLogsDir">The target directory where crash log files will be collected.</param>
    private async Task CollectFromXseFolder(string crashLogsDir)
    {
        var xseFolderPath = _yamlSettingsCache.GetSetting<string>(YamlStore.GameLocal, "Game_Info.Docs_Folder_XSE");
        if (!string.IsNullOrEmpty(xseFolderPath) && Directory.Exists(xseFolderPath))
        {
            _logger.LogInformation("Checking XSE folder: {XseFolderPath}", xseFolderPath);
            await CopyFilesAsync(xseFolderPath, crashLogsDir, CrashLogPattern);
        }
    }

    /// <summary>
    /// Reformats a single crash log file by processing the lines, removing unwanted patterns,
    /// and making necessary adjustments to the content.
    /// </summary>
    /// <param name="filePath">The full path to the log file to be reformatted.</param>
    /// <param name="removePatterns">A list of patterns to identify lines that should be removed from the log file.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    private async Task ReformatSingleLogFileAsync(string filePath, List<string> removePatterns)
    {
        try
        {
            var lines = await File.ReadAllLinesAsync(filePath);
            var processedLines = new List<string>();
            var inPluginsSection = true;

            // Process lines from bottom to top (reverse order)
            for (var i = lines.Length - 1; i >= 0; i--)
            {
                var line = lines[i];

                if (inPluginsSection && line.StartsWith(PluginsSectionHeader)) inPluginsSection = false;

                // Skip lines that match remove patterns
                if (removePatterns.Any(pattern => line.Contains(pattern, StringComparison.OrdinalIgnoreCase))) continue;

                // Reformat plugin lines
                if (inPluginsSection && line.Contains('[')) line = ReformatPluginLine(line);

                processedLines.Add(line);
            }

            // Reverse the lines back to original order
            processedLines.Reverse();
            await File.WriteAllLinesAsync(filePath, processedLines);
        }
        catch (Exception ex)
        {
            LogFileOperationError("reformatting", filePath, ex);
        }
    }

    /// <summary>
    /// Reformats a plugin line by replacing spaces within square brackets with zeros.
    /// </summary>
    /// <param name="line">The plugin line to reformat.</param>
    /// <returns>
    /// The reformatted line with spaces in brackets replaced by zeros,
    /// or the original line if reformatting is not applicable or fails.
    /// </returns>
    private static string ReformatPluginLine(string line)
    {
        try
        {
            var bracketStart = line.IndexOf('[');
            var bracketEnd = line.IndexOf(']');

            if (bracketStart >= 0 && bracketEnd > bracketStart)
            {
                var prefix = line[..bracketStart];
                var bracket = line.Substring(bracketStart + 1, bracketEnd - bracketStart - 1);
                var suffix = line[(bracketEnd + 1)..];

                var modifiedBracket = bracket.Replace(' ', '0');
                return $"{prefix}[{modifiedBracket}]{suffix}";
            }
        }
        catch
        {
            // If reformatting fails, return original line
        }

        return line;
    }
}