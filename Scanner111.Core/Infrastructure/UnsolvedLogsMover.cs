using Microsoft.Extensions.Logging;
using Scanner111.Core.Models;

namespace Scanner111.Core.Infrastructure;

/// <summary>
///     Service to move unsolved/incomplete crash logs to a backup location
/// </summary>
public class UnsolvedLogsMover : IUnsolvedLogsMover
{
    private readonly IApplicationSettingsService _applicationSettings;
    private readonly ILogger<UnsolvedLogsMover> _logger;

    public UnsolvedLogsMover(ILogger<UnsolvedLogsMover> logger, IApplicationSettingsService applicationSettings)
    {
        _logger = logger;
        _applicationSettings = applicationSettings;
    }

    public async Task<bool> MoveUnsolvedLogAsync(string crashLogPath, ApplicationSettings? settings = null, CancellationToken cancellationToken = default)
    {
        try
        {
            // Use provided settings if available, otherwise load from settings service
            settings ??= await _applicationSettings.LoadSettingsAsync();
            if (!settings.MoveUnsolvedLogs)
            {
                _logger.LogDebug("Move unsolved logs is disabled");
                return false;
            }

            if (!File.Exists(crashLogPath))
            {
                _logger.LogWarning("Crash log file not found: {Path}", crashLogPath);
                return false;
            }

            // Get backup directory - use BackupDirectory if set, otherwise use default
            var backupPath = !string.IsNullOrEmpty(settings.BackupDirectory)
                ? Path.Combine(settings.BackupDirectory, "Unsolved Logs")
                : Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), 
                    "Scanner 111", "Unsolved Logs");

            // Create backup directory if it doesn't exist
            Directory.CreateDirectory(backupPath);

            // Get file names
            var crashLogFileName = Path.GetFileName(crashLogPath);
            var autoscanFileName = Path.GetFileNameWithoutExtension(crashLogPath) + "-AUTOSCAN.md";
            var autoscanPath = Path.Combine(Path.GetDirectoryName(crashLogPath)!, autoscanFileName);

            // Move crash log
            var backupCrashLogPath = Path.Combine(backupPath, crashLogFileName);
            try
            {
                // If file already exists in backup, add a timestamp
                if (File.Exists(backupCrashLogPath))
                {
                    var timestamp = DateTime.Now.ToString("yyyyMMdd_HHmmss");
                    var nameWithoutExt = Path.GetFileNameWithoutExtension(crashLogFileName);
                    var ext = Path.GetExtension(crashLogFileName);
                    backupCrashLogPath = Path.Combine(backupPath, $"{nameWithoutExt}_{timestamp}{ext}");
                }

                File.Move(crashLogPath, backupCrashLogPath);
                _logger.LogInformation("Moved unsolved crash log to: {Path}", backupCrashLogPath);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to move crash log {Path} to backup", crashLogPath);
                return false;
            }

            // Move autoscan report if it exists
            if (File.Exists(autoscanPath))
            {
                var backupAutoscanPath = Path.Combine(backupPath, autoscanFileName);
                try
                {
                    // If file already exists in backup, add a timestamp
                    if (File.Exists(backupAutoscanPath))
                    {
                        var timestamp = DateTime.Now.ToString("yyyyMMdd_HHmmss");
                        var nameWithoutExt = Path.GetFileNameWithoutExtension(autoscanFileName);
                        var ext = Path.GetExtension(autoscanFileName);
                        backupAutoscanPath = Path.Combine(backupPath, $"{nameWithoutExt}_{timestamp}{ext}");
                    }

                    File.Move(autoscanPath, backupAutoscanPath);
                    _logger.LogInformation("Moved autoscan report to: {Path}", backupAutoscanPath);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Failed to move autoscan report {Path} to backup", autoscanPath);
                    // Don't fail the operation if we can't move the autoscan report
                }
            }

            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unexpected error moving unsolved log: {Path}", crashLogPath);
            return false;
        }
    }
}

/// <summary>
///     Interface for moving unsolved logs
/// </summary>
public interface IUnsolvedLogsMover
{
    /// <summary>
    ///     Move an unsolved/incomplete crash log and its autoscan report to backup directory
    /// </summary>
    /// <param name="crashLogPath">Path to the crash log file</param>
    /// <param name="settings">Application settings to use for the operation</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>True if successfully moved, false otherwise</returns>
    Task<bool> MoveUnsolvedLogAsync(string crashLogPath, ApplicationSettings? settings = null, CancellationToken cancellationToken = default);
}