using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Scanner111.Models;

namespace Scanner111.Services;

/// <summary>
///     Implementation for checking errors in log files
/// </summary>
public class LogErrorCheckService : ILogErrorCheckService
{
    private readonly IYamlSettingsCacheService _yamlSettingsCache;

    public LogErrorCheckService(IYamlSettingsCacheService yamlSettingsCache)
    {
        _yamlSettingsCache = yamlSettingsCache ?? throw new ArgumentNullException(nameof(yamlSettingsCache));
    }

    /// <summary>
    ///     Scans game logs for errors based on game directory specified in settings.
    /// </summary>
    /// <returns>A detailed report of all detected errors in the game log files, if any.</returns>
    public async Task<string> ScanGameLogsAsync()
    {
        var gameDir = GetSetting<string>(Yaml.Game, "game_dir");
        if (string.IsNullOrEmpty(gameDir)) return "❌ ERROR : Game directory not configured in settings";

        var logsPath = GetSetting<string>(Yaml.Game, "logs_path");
        if (string.IsNullOrEmpty(logsPath)) logsPath = ""; // Default empty path will point to game directory root

        var logsFolderPath = Path.Combine(gameDir, logsPath);
        var logsDirInfo = new DirectoryInfo(logsFolderPath);

        if (!logsDirInfo.Exists) return $"❌ ERROR : Logs directory not found at {logsFolderPath}";

        var results = new StringBuilder();
        results.AppendLine("================== GAME LOGS ANALYSIS ==================\n");

        var logCheckResults = await CheckLogErrorsAsync(logsDirInfo);
        results.AppendLine(logCheckResults);

        return results.ToString();
    }

    /// <summary>
    ///     Inspects log files within a specified folder for recorded errors.
    /// </summary>
    /// <param name="folderPath">Path to the folder containing log files for error inspection.</param>
    /// <returns>A detailed report of all detected errors in the relevant log files, if any.</returns>
    public async Task<string> CheckLogErrorsAsync(DirectoryInfo folderPath)
    {
        if (!folderPath.Exists) return string.Empty;

        // Get YAML settings
        var catchErrors = GetSettingAsList<string>(Yaml.Main, "catch_log_errors").Select(s => s.ToLower()).ToList();
        var ignoreFiles = GetSettingAsList<string>(Yaml.Main, "exclude_log_files").Select(s => s.ToLower())
            .ToList();
        var ignoreErrors = GetSettingAsList<string>(Yaml.Main, "exclude_log_errors").Select(s => s.ToLower())
            .ToList();

        var errorReport = new StringBuilder();

        // Find valid log files (excluding crash logs)
        var validLogFiles = folderPath.GetFiles("*.log")
            .Where(file => !file.Name.Contains("crash-"))
            .ToList();

        foreach (var logFilePath in validLogFiles.Where(logFilePath =>
                     !ignoreFiles.Any(part =>
                         logFilePath.FullName.Contains(part, StringComparison.CurrentCultureIgnoreCase))))
            try
            {
                string[] logLines;

                // Read file with UTF-8 encoding, ignoring encoding errors
                try
                {
                    logLines = await File.ReadAllLinesAsync(logFilePath.FullName, Encoding.UTF8);
                }
                catch (Exception)
                {
                    // Try fallback encoding if UTF-8 fails
                    logLines = await File.ReadAllLinesAsync(logFilePath.FullName, Encoding.Default);
                }

                // List to store detected errors in this log file
                var fileErrors = logLines.Select(line => new { line, lowerLine = line.ToLower() })
                    .Where(t =>
                        catchErrors.Any(t.lowerLine.Contains) &&
                        !ignoreErrors.Any(t.lowerLine.Contains))
                    .Select(t => t.line).ToList();

                // If errors were found in this file, add them to the report
                if (fileErrors.Count <= 0) continue;
                errorReport.AppendLine("[!] CAUTION : THE FOLLOWING LOG FILE REPORTS ONE OR MORE ERRORS!");
                errorReport.AppendLine("[ Errors do not necessarily mean that the mod is not working. ]");
                errorReport.AppendLine($"\nLOG PATH > {logFilePath.FullName}");

                foreach (var error in fileErrors) errorReport.AppendLine(error);

                errorReport.AppendLine($"\n* TOTAL NUMBER OF DETECTED LOG ERRORS * : {fileErrors.Count}\n");
            }
            catch (IOException)
            {
                errorReport.AppendLine($"[!] CANNOT ACCESS LOG FILE: {logFilePath.FullName}");
            }

        return errorReport.ToString();
    }

    /// <summary>
    ///     Gets a setting from the YAML settings cache
    /// </summary>
    private T? GetSetting<T>(Yaml yamlType, string key) where T : class
    {
        try
        {
            return _yamlSettingsCache.GetSetting<T>(yamlType, key);
        }
        catch
        {
            return null; // CS8603
        }
    }

    /// <summary>
    ///     Gets a list setting from the YAML settings cache
    /// </summary>
    private List<T> GetSettingAsList<T>(Yaml yamlType, string key)
    {
        try
        {
            var setting = _yamlSettingsCache.GetSetting<List<T>>(yamlType, key);
            return setting ?? [];
        }
        catch
        {
            return [];
        }
    }
}