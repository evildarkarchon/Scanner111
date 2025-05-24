using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Scanner111.Services.Interfaces;

namespace Scanner111.Services;

/// <summary>
///     Implementation of crash log file service.
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
    ///     Gets all crash log files from various directories.
    /// </summary>
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
    ///     Reformats crash log files based on settings.
    /// </summary>
    public async Task ReformatCrashLogsAsync(IEnumerable<string> crashLogFiles, IEnumerable<string> removePatterns)
    {
        var removePatternsList = removePatterns.ToList();
        foreach (var file in crashLogFiles) await ReformatSingleLogFileAsync(file, removePatternsList);
    }

    /// <summary>
    ///     Moves files matching pattern from source to target directory.
    /// </summary>
    public async Task MoveFilesAsync(string sourceDir, string targetDir, string pattern)
    {
        await ProcessFilesAsync(sourceDir, targetDir, pattern, MoveFileWithErrorHandling);
    }

    /// <summary>
    ///     Copies files matching pattern from source to target directory.
    /// </summary>
    public async Task CopyFilesAsync(string sourceDir, string targetDir, string pattern)
    {
        await ProcessFilesAsync(sourceDir, targetDir, pattern, CopyFileWithErrorHandling);
    }

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

    private void LogFileOperationError(string operation, string filePath, Exception ex)
    {
        var errorMessage = $"Error {operation} {filePath}: {ex.Message}";
        _logger.LogError(ex, errorMessage);
    }

    private void EnsureDirectoriesExist(params string[] directories)
    {
        foreach (var directory in directories) Directory.CreateDirectory(directory);
    }

    private async Task CollectCrashLogFilesFromBaseDirectoryAsync(string baseFolder, string crashLogsDir)
    {
        await MoveFilesAsync(baseFolder, crashLogsDir, CrashLogPattern);
        await MoveFilesAsync(baseFolder, crashLogsDir, CrashAutoscanPattern);
    }

    private async Task CollectCrashLogFilesFromCustomDirectoriesAsync(string crashLogsDir)
    {
        await CollectFromCustomScanFolder(crashLogsDir);
        await CollectFromXseFolder(crashLogsDir);
    }

    private async Task CollectFromCustomScanFolder(string crashLogsDir)
    {
        var customScanPath = _yamlSettingsCache.GetSetting<string>(YamlStore.Settings, "SCAN Custom Path");
        if (!string.IsNullOrEmpty(customScanPath) && Directory.Exists(customScanPath))
        {
            _logger.LogInformation("Checking custom scan folder: {CustomScanPath}", customScanPath);
            await CopyFilesAsync(customScanPath, crashLogsDir, CrashLogPattern);
        }
    }

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
    ///     Reformats a single log file.
    /// </summary>
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
    ///     Reformats a plugin line by replacing spaces in brackets with zeros.
    /// </summary>
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