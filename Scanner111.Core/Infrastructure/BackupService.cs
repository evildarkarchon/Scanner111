using Scanner111.Core.Abstractions;
using Scanner111.Core.Models;

namespace Scanner111.Core.Infrastructure;

/// <summary>
///     Service for backing up game files
/// </summary>
public class BackupService : IBackupService
{
    private readonly string _defaultBackupDirectory;
    private readonly ILogger<BackupService> _logger;
    private readonly IApplicationSettingsService _settingsService;
    private readonly IFileSystem _fileSystem;
    private readonly IPathService _pathService;
    private readonly IEnvironmentPathProvider _environment;
    private readonly IZipService _zipService;

    public BackupService(
        ILogger<BackupService> logger,
        IApplicationSettingsService settingsService,
        IFileSystem fileSystem,
        IPathService pathService,
        IEnvironmentPathProvider environment,
        IZipService zipService)
    {
        _logger = logger;
        _settingsService = settingsService;
        _fileSystem = fileSystem;
        _pathService = pathService;
        _environment = environment;
        _zipService = zipService;

        // Default backup directory in user's Documents
        _defaultBackupDirectory = _pathService.Combine(
            _environment.GetFolderPath(Environment.SpecialFolder.MyDocuments),
            "Scanner111",
            "Backups");
    }

    /// <summary>
    ///     Create a backup of specified files
    /// </summary>
    public async Task<BackupResult> CreateBackupAsync(
        string gamePath,
        IEnumerable<string> filesToBackup,
        IProgress<BackupProgress>? progress = null,
        CancellationToken cancellationToken = default)
    {
        var result = new BackupResult();

        try
        {
            if (!_fileSystem.DirectoryExists(gamePath))
            {
                result.Success = false;
                result.ErrorMessage = "Game path does not exist";
                return result;
            }

            // Ensure backup directory exists
            var backupDir = await GetBackupDirectoryAsync().ConfigureAwait(false);
            _fileSystem.CreateDirectory(backupDir);

            // Create backup filename with timestamp including milliseconds
            var timestamp = DateTime.Now.ToString("yyyy-MM-dd_HH-mm-ss-fff");
            var backupName = $"Fallout4_Backup_{timestamp}.zip";
            var backupPath = _pathService.Combine(backupDir, backupName);

            result.BackupPath = backupPath;
            result.Timestamp = DateTime.UtcNow;

            var fileList = filesToBackup.ToList();
            var totalFiles = fileList.Count;
            var processedFiles = 0;

            // Build dictionary of files to add to zip
            var filesToAdd = new Dictionary<string, string>();
            
            foreach (var file in fileList)
            {
                cancellationToken.ThrowIfCancellationRequested();

                var sourceFile = _pathService.Combine(gamePath, file);

                progress?.Report(new BackupProgress
                {
                    CurrentFile = file,
                    TotalFiles = totalFiles,
                    ProcessedFiles = processedFiles
                });

                if (_fileSystem.FileExists(sourceFile))
                {
                    // Add file to collection with relative path
                    var entryName = file.Replace('\\', '/');
                    filesToAdd[sourceFile] = entryName;
                    result.BackedUpFiles.Add(file);
                    var fileSize = _fileSystem.GetFileSize(sourceFile);
                    result.TotalSize += fileSize;
                    _logger.LogDebug("Preparing to backup file: {File}", file);
                }
                else
                {
                    _logger.LogWarning("File not found for backup: {File}", sourceFile);
                }

                processedFiles++;
            }

            // Create the zip with all files
            if (filesToAdd.Any())
            {
                var success = await _zipService.CreateZipAsync(backupPath, filesToAdd, cancellationToken)
                    .ConfigureAwait(false);
                
                if (!success)
                {
                    result.Success = false;
                    result.ErrorMessage = "Failed to create backup archive";
                    return result;
                }
            }

            result.Success = result.BackedUpFiles.Count > 0;

            if (result.Success)
            {
                _logger.LogInformation("Backup created successfully: {BackupPath} ({FileCount} files, {Size:N0} bytes)",
                    backupPath, result.BackedUpFiles.Count, result.TotalSize);
            }
            else
            {
                // Delete empty backup file
                if (_fileSystem.FileExists(backupPath)) _fileSystem.DeleteFile(backupPath);
                result.ErrorMessage = "No files were backed up";
            }
        }
        catch (OperationCanceledException)
        {
            // Clean up partial backup
            if (_fileSystem.FileExists(result.BackupPath))
                try
                {
                    _fileSystem.DeleteFile(result.BackupPath);
                }
                catch
                {
                }

            throw; // Re-throw to let the caller handle cancellation
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating backup");
            result.Success = false;
            result.ErrorMessage = ex.Message;
        }

        return result;
    }

    /// <summary>
    ///     Create a full backup of critical game files
    /// </summary>
    public async Task<BackupResult> CreateFullBackupAsync(
        string gamePath,
        IProgress<BackupProgress>? progress = null,
        CancellationToken cancellationToken = default)
    {
        // Define critical files to backup
        var criticalFiles = new List<string>
        {
            "Fallout4.exe",
            "Fallout4Launcher.exe",
            "f4se_loader.exe",
            "f4se_1_10_163.dll",
            "f4se_1_10_984.dll",
            "Fallout4.cdx",
            "Fallout4.ini",
            "Fallout4Prefs.ini",
            "Fallout4Custom.ini",
            @"Data\Fallout4.esm",
            @"Data\Fallout4 - Animations.ba2",
            @"Data\Fallout4 - Interface.ba2",
            @"Data\Fallout4 - Materials.ba2",
            @"Data\Fallout4 - Meshes.ba2",
            @"Data\Fallout4 - MeshesExtra.ba2",
            @"Data\Fallout4 - Misc.ba2",
            @"Data\Fallout4 - Shaders.ba2",
            @"Data\Fallout4 - Sounds.ba2",
            @"Data\Fallout4 - Startup.ba2",
            @"Data\Fallout4 - Textures1.ba2",
            @"Data\Fallout4 - Textures2.ba2",
            @"Data\Fallout4 - Textures3.ba2"
        };

        // Also backup F4SE plugins directory if it exists
        var f4sePluginsPath = _pathService.Combine(gamePath, "Data", "F4SE", "Plugins");
        if (_fileSystem.DirectoryExists(f4sePluginsPath))
        {
            var pluginFiles = _fileSystem.GetFiles(f4sePluginsPath, "*.*", SearchOption.TopDirectoryOnly)
                .Select(f => _pathService.NormalizePath(f.Replace(gamePath + "\\", "").Replace(gamePath + "/", "")))
                .ToList();
            criticalFiles.AddRange(pluginFiles);
        }

        return await CreateBackupAsync(gamePath, criticalFiles, progress, cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    ///     Restore files from a backup
    /// </summary>
    public async Task<bool> RestoreBackupAsync(
        string backupPath,
        string gamePath,
        IEnumerable<string>? filesToRestore = null,
        IProgress<BackupProgress>? progress = null,
        CancellationToken cancellationToken = default)
    {
        try
        {
            if (!_fileSystem.FileExists(backupPath))
            {
                _logger.LogError("Backup file not found: {BackupPath}", backupPath);
                return false;
            }

            if (!_fileSystem.DirectoryExists(gamePath))
            {
                _logger.LogError("Game path not found: {GamePath}", gamePath);
                return false;
            }

            // Get list of entries in the zip
            var allEntries = await _zipService.ListZipEntriesAsync(backupPath).ConfigureAwait(false);
            
            var entriesToRestore = filesToRestore != null
                ? allEntries.Where(e => filesToRestore.Contains(e.Replace('/', '\\')))
                : allEntries;

            var entryList = entriesToRestore.ToList();
            var totalFiles = entryList.Count;
            var processedFiles = 0;

            foreach (var entry in entryList)
            {
                cancellationToken.ThrowIfCancellationRequested();

                var targetPath = _pathService.Combine(gamePath, entry.Replace('/', '\\'));

                progress?.Report(new BackupProgress
                {
                    CurrentFile = entry,
                    TotalFiles = totalFiles,
                    ProcessedFiles = processedFiles
                });

                try
                {
                    // Ensure target directory exists
                    var targetDir = _pathService.GetDirectoryName(targetPath);
                    if (!string.IsNullOrEmpty(targetDir)) _fileSystem.CreateDirectory(targetDir);

                    // Create backup of existing file if it exists
                    if (_fileSystem.FileExists(targetPath))
                    {
                        var backupFile = targetPath + ".bak";
                        _fileSystem.MoveFile(targetPath, backupFile);
                    }

                    // Extract file
                    var extracted = await _zipService.ExtractFileFromZipAsync(
                        backupPath, entry, targetPath, true, cancellationToken)
                        .ConfigureAwait(false);

                    if (!extracted)
                    {
                        _logger.LogError("Failed to extract file: {File}", entry);
                        return false;
                    }

                    _logger.LogDebug("Restored file: {File}", entry);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Failed to restore file: {File}", entry);
                    return false;
                }

                processedFiles++;
            }

            _logger.LogInformation("Backup restored successfully from: {BackupPath}", backupPath);
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error restoring backup");
            return false;
        }
    }

    /// <summary>
    ///     List available backups
    /// </summary>
    public async Task<IEnumerable<BackupInfo>> ListBackupsAsync(string? backupDirectory = null)
    {
        var backups = new List<BackupInfo>();

        try
        {
            var backupDir = backupDirectory ?? await GetBackupDirectoryAsync().ConfigureAwait(false);

            if (!_fileSystem.DirectoryExists(backupDir)) return backups;

            var backupFiles = _fileSystem.GetFiles(backupDir, "*.zip", SearchOption.TopDirectoryOnly)
                .OrderByDescending(f => _fileSystem.GetLastWriteTime(f));

            foreach (var backupFile in backupFiles)
                try
                {
                    var backupInfo = new BackupInfo
                    {
                        BackupPath = backupFile,
                        Name = _pathService.GetFileNameWithoutExtension(backupFile),
                        CreatedDate = _fileSystem.GetLastWriteTime(backupFile),
                        Size = _fileSystem.GetFileSize(backupFile)
                    };

                    // Try to get file count from zip
                    try
                    {
                        backupInfo.FileCount = await _zipService.GetZipEntryCountAsync(backupFile)
                            .ConfigureAwait(false);
                    }
                    catch
                    {
                        // Ignore errors reading zip
                    }

                    backups.Add(backupInfo);
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Error reading backup info for: {File}", backupFile);
                }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error listing backups");
        }

        return backups;
    }

    /// <summary>
    ///     Delete a backup
    /// </summary>
    public async Task<bool> DeleteBackupAsync(string backupPath)
    {
        try
        {
            if (_fileSystem.FileExists(backupPath))
            {
                await Task.Run(() => _fileSystem.DeleteFile(backupPath)).ConfigureAwait(false);
                _logger.LogInformation("Deleted backup: {BackupPath}", backupPath);
                return true;
            }

            _logger.LogWarning("Backup not found: {BackupPath}", backupPath);
            return false;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error deleting backup: {BackupPath}", backupPath);
            return false;
        }
    }

    private async Task<string> GetBackupDirectoryAsync()
    {
        var settings = await _settingsService.LoadSettingsAsync().ConfigureAwait(false);
        return !string.IsNullOrEmpty(settings.BackupDirectory)
            ? settings.BackupDirectory
            : _defaultBackupDirectory;
    }
}