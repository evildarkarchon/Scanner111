using Scanner111.Core.Models;

namespace Scanner111.Core.Infrastructure;

/// <summary>
///     Service for backing up game files
/// </summary>
public interface IBackupService
{
    /// <summary>
    ///     Create a backup of specified files
    /// </summary>
    /// <param name="gamePath">Path to game installation</param>
    /// <param name="filesToBackup">List of files to backup (relative to game path)</param>
    /// <param name="progress">Progress reporter</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Backup result with details</returns>
    Task<BackupResult> CreateBackupAsync(
        string gamePath,
        IEnumerable<string> filesToBackup,
        IProgress<BackupProgress>? progress = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    ///     Create a full backup of critical game files
    /// </summary>
    /// <param name="gamePath">Path to game installation</param>
    /// <param name="progress">Progress reporter</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Backup result with details</returns>
    Task<BackupResult> CreateFullBackupAsync(
        string gamePath,
        IProgress<BackupProgress>? progress = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    ///     Restore files from a backup
    /// </summary>
    /// <param name="backupPath">Path to backup archive or directory</param>
    /// <param name="gamePath">Path to game installation</param>
    /// <param name="filesToRestore">Specific files to restore (null for all)</param>
    /// <param name="progress">Progress reporter</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>True if restoration successful</returns>
    Task<bool> RestoreBackupAsync(
        string backupPath,
        string gamePath,
        IEnumerable<string>? filesToRestore = null,
        IProgress<BackupProgress>? progress = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    ///     List available backups
    /// </summary>
    /// <param name="backupDirectory">Directory containing backups</param>
    /// <returns>List of backup information</returns>
    Task<IEnumerable<BackupInfo>> ListBackupsAsync(string? backupDirectory = null);

    /// <summary>
    ///     Delete a backup
    /// </summary>
    /// <param name="backupPath">Path to backup to delete</param>
    /// <returns>True if deletion successful</returns>
    Task<bool> DeleteBackupAsync(string backupPath);
}

/// <summary>
///     Information about a backup
/// </summary>
public class BackupInfo
{
    /// <summary>
    ///     Full path to the backup
    /// </summary>
    public string BackupPath { get; set; } = string.Empty;

    /// <summary>
    ///     Name of the backup
    /// </summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>
    ///     When the backup was created
    /// </summary>
    public DateTime CreatedDate { get; set; }

    /// <summary>
    ///     Size of the backup in bytes
    /// </summary>
    public long Size { get; set; }

    /// <summary>
    ///     Number of files in the backup
    /// </summary>
    public int FileCount { get; set; }

    /// <summary>
    ///     Game version the backup was created from
    /// </summary>
    public string GameVersion { get; set; } = string.Empty;
}