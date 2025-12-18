using Scanner111.Common.Models.Orchestration;

namespace Scanner111.Common.Services.Orchestration;

/// <summary>
/// Collects and organizes crash logs based on analysis results.
/// Moves unsolved logs to backup directories for later review.
/// </summary>
public class LogCollector : ILogCollector
{
    private LogCollectorConfiguration _configuration = LogCollectorConfiguration.Empty;

    /// <inheritdoc/>
    public LogCollectorConfiguration Configuration
    {
        get => _configuration;
        set => _configuration = value;
    }

    /// <inheritdoc/>
    public async Task<LogCollectionResult> MoveUnsolvedLogAsync(
        string crashLogPath,
        CancellationToken cancellationToken = default)
    {
        await Task.Yield();
        cancellationToken.ThrowIfCancellationRequested();

        if (string.IsNullOrWhiteSpace(crashLogPath))
        {
            return LogCollectionResult.Empty;
        }

        if (string.IsNullOrWhiteSpace(_configuration.BackupBasePath))
        {
            return new LogCollectionResult
            {
                Errors = new[]
                {
                    new LogCollectionError(crashLogPath, "Backup path not configured")
                }
            };
        }

        var movedCrashLogs = new List<string>();
        var movedReports = new List<string>();
        var errors = new List<LogCollectionError>();

        // Ensure backup directory exists
        var backupDir = GetBackupDirectory();
        if (_configuration.CreateDirectoryIfNotExists && !Directory.Exists(backupDir))
        {
            try
            {
                Directory.CreateDirectory(backupDir);
            }
            catch (Exception ex)
            {
                return new LogCollectionResult
                {
                    Errors = new[]
                    {
                        new LogCollectionError(crashLogPath, $"Failed to create backup directory: {ex.Message}")
                    }
                };
            }
        }

        // Move crash log
        if (File.Exists(crashLogPath))
        {
            var backupPath = GetBackupPath(crashLogPath);
            var moveResult = await MoveFileAsync(crashLogPath, backupPath, cancellationToken).ConfigureAwait(false);

            if (moveResult.Success)
            {
                movedCrashLogs.Add(Path.GetFileName(crashLogPath));
            }
            else
            {
                errors.Add(new LogCollectionError(crashLogPath, moveResult.ErrorMessage ?? "Unknown error"));
            }
        }

        // Move associated report
        var reportPath = GetReportPath(crashLogPath);
        if (File.Exists(reportPath))
        {
            var reportBackupPath = GetBackupPath(reportPath);
            var moveResult = await MoveFileAsync(reportPath, reportBackupPath, cancellationToken).ConfigureAwait(false);

            if (moveResult.Success)
            {
                movedReports.Add(Path.GetFileName(reportPath));
            }
            else
            {
                errors.Add(new LogCollectionError(reportPath, moveResult.ErrorMessage ?? "Unknown error"));
            }
        }

        return new LogCollectionResult
        {
            MovedCrashLogs = movedCrashLogs,
            MovedReports = movedReports,
            Errors = errors
        };
    }

    /// <inheritdoc/>
    public async Task<LogCollectionResult> MoveUnsolvedLogsAsync(
        IEnumerable<string> crashLogPaths,
        CancellationToken cancellationToken = default)
    {
        var allMovedCrashLogs = new List<string>();
        var allMovedReports = new List<string>();
        var allErrors = new List<LogCollectionError>();

        foreach (var crashLogPath in crashLogPaths)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var result = await MoveUnsolvedLogAsync(crashLogPath, cancellationToken).ConfigureAwait(false);

            allMovedCrashLogs.AddRange(result.MovedCrashLogs);
            allMovedReports.AddRange(result.MovedReports);
            allErrors.AddRange(result.Errors);
        }

        return new LogCollectionResult
        {
            MovedCrashLogs = allMovedCrashLogs,
            MovedReports = allMovedReports,
            Errors = allErrors
        };
    }

    /// <inheritdoc/>
    public string GetBackupPath(string crashLogPath)
    {
        var fileName = Path.GetFileName(crashLogPath);
        var backupDir = GetBackupDirectory();
        return Path.Combine(backupDir, fileName);
    }

    /// <inheritdoc/>
    public string GetReportPath(string crashLogPath)
    {
        var directory = Path.GetDirectoryName(crashLogPath) ?? string.Empty;
        var fileNameWithoutExtension = Path.GetFileNameWithoutExtension(crashLogPath);
        var reportFileName = fileNameWithoutExtension + _configuration.ReportFileSuffix;
        return Path.Combine(directory, reportFileName);
    }

    private string GetBackupDirectory()
    {
        return Path.Combine(_configuration.BackupBasePath, _configuration.UnsolvedLogsSubdirectory);
    }

    private async Task<MoveResult> MoveFileAsync(
        string sourcePath,
        string destinationPath,
        CancellationToken cancellationToken)
    {
        await Task.Yield();
        cancellationToken.ThrowIfCancellationRequested();

        try
        {
            // Check if destination exists
            if (File.Exists(destinationPath))
            {
                if (!_configuration.OverwriteExisting)
                {
                    return new MoveResult(false, "Destination file already exists");
                }

                File.Delete(destinationPath);
            }

            // Move the file
            File.Move(sourcePath, destinationPath);
            return new MoveResult(true, null);
        }
        catch (IOException ex)
        {
            return new MoveResult(false, $"IO error: {ex.Message}");
        }
        catch (UnauthorizedAccessException ex)
        {
            return new MoveResult(false, $"Access denied: {ex.Message}");
        }
    }

    private record MoveResult(bool Success, string? ErrorMessage);
}
