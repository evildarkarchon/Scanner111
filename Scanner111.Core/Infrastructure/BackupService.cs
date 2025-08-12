using System.IO.Compression;
using Microsoft.Extensions.Logging;
using Scanner111.Core.Models;

namespace Scanner111.Core.Infrastructure;

/// <summary>
///     Service for backing up game files
/// </summary>
public class BackupService : IBackupService
{
    private readonly ILogger<BackupService> _logger;
    private readonly IApplicationSettingsService _settingsService;
    private readonly string _defaultBackupDirectory;

    public BackupService(ILogger<BackupService> logger, IApplicationSettingsService settingsService)
    {
        _logger = logger;
        _settingsService = settingsService;
        
        // Default backup directory in user's Documents
        _defaultBackupDirectory = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments),
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
            if (!Directory.Exists(gamePath))
            {
                result.Success = false;
                result.ErrorMessage = "Game path does not exist";
                return result;
            }

            // Ensure backup directory exists
            var backupDir = await GetBackupDirectoryAsync().ConfigureAwait(false);
            Directory.CreateDirectory(backupDir);

            // Create backup filename with timestamp including milliseconds
            var timestamp = DateTime.Now.ToString("yyyy-MM-dd_HH-mm-ss-fff");
            var backupName = $"Fallout4_Backup_{timestamp}.zip";
            var backupPath = Path.Combine(backupDir, backupName);

            result.BackupPath = backupPath;
            result.Timestamp = DateTime.UtcNow;

            var fileList = filesToBackup.ToList();
            var totalFiles = fileList.Count;
            var processedFiles = 0;

            using (var zipArchive = ZipFile.Open(backupPath, ZipArchiveMode.Create))
            {
                foreach (var file in fileList)
                {
                    cancellationToken.ThrowIfCancellationRequested();

                    var sourceFile = Path.Combine(gamePath, file);
                    
                    progress?.Report(new BackupProgress
                    {
                        CurrentFile = file,
                        TotalFiles = totalFiles,
                        ProcessedFiles = processedFiles
                    });

                    if (File.Exists(sourceFile))
                    {
                        try
                        {
                            // Add file to zip with relative path
                            var entryName = file.Replace(Path.DirectorySeparatorChar, '/');
                            await Task.Run(() => zipArchive.CreateEntryFromFile(sourceFile, entryName), cancellationToken).ConfigureAwait(false);
                            
                            result.BackedUpFiles.Add(file);
                            var fileInfo = new FileInfo(sourceFile);
                            result.TotalSize += fileInfo.Length;
                            
                            _logger.LogDebug("Backed up file: {File}", file);
                        }
                        catch (Exception ex)
                        {
                            _logger.LogWarning(ex, "Failed to backup file: {File}", file);
                        }
                    }
                    else
                    {
                        _logger.LogWarning("File not found for backup: {File}", sourceFile);
                    }

                    processedFiles++;
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
                if (File.Exists(backupPath))
                {
                    File.Delete(backupPath);
                }
                result.ErrorMessage = "No files were backed up";
            }
        }
        catch (OperationCanceledException)
        {
            // Clean up partial backup
            if (File.Exists(result.BackupPath))
            {
                try { File.Delete(result.BackupPath); } catch { }
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
        var f4sePluginsPath = Path.Combine(gamePath, @"Data\F4SE\Plugins");
        if (Directory.Exists(f4sePluginsPath))
        {
            var pluginFiles = Directory.GetFiles(f4sePluginsPath, "*.*", SearchOption.TopDirectoryOnly)
                .Select(f => Path.GetRelativePath(gamePath, f))
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
            if (!File.Exists(backupPath))
            {
                _logger.LogError("Backup file not found: {BackupPath}", backupPath);
                return false;
            }

            if (!Directory.Exists(gamePath))
            {
                _logger.LogError("Game path not found: {GamePath}", gamePath);
                return false;
            }

            using (var zipArchive = ZipFile.OpenRead(backupPath))
            {
                var entries = filesToRestore != null 
                    ? zipArchive.Entries.Where(e => filesToRestore.Contains(e.FullName.Replace('/', Path.DirectorySeparatorChar)))
                    : zipArchive.Entries;

                var entryList = entries.ToList();
                var totalFiles = entryList.Count;
                var processedFiles = 0;

                foreach (var entry in entryList)
                {
                    cancellationToken.ThrowIfCancellationRequested();

                    var targetPath = Path.Combine(gamePath, entry.FullName.Replace('/', Path.DirectorySeparatorChar));
                    
                    progress?.Report(new BackupProgress
                    {
                        CurrentFile = entry.FullName,
                        TotalFiles = totalFiles,
                        ProcessedFiles = processedFiles
                    });

                    try
                    {
                        // Ensure target directory exists
                        var targetDir = Path.GetDirectoryName(targetPath);
                        if (!string.IsNullOrEmpty(targetDir))
                        {
                            Directory.CreateDirectory(targetDir);
                        }

                        // Create backup of existing file if it exists
                        if (File.Exists(targetPath))
                        {
                            var backupFile = targetPath + ".bak";
                            File.Move(targetPath, backupFile, true);
                        }

                        // Extract file
                        await Task.Run(() => entry.ExtractToFile(targetPath, true), cancellationToken).ConfigureAwait(false);
                        
                        _logger.LogDebug("Restored file: {File}", entry.FullName);
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "Failed to restore file: {File}", entry.FullName);
                        return false;
                    }

                    processedFiles++;
                }
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
            
            if (!Directory.Exists(backupDir))
            {
                return backups;
            }

            var backupFiles = Directory.GetFiles(backupDir, "*.zip", SearchOption.TopDirectoryOnly)
                .OrderByDescending(f => File.GetCreationTime(f));

            foreach (var backupFile in backupFiles)
            {
                try
                {
                    var fileInfo = new FileInfo(backupFile);
                    var backupInfo = new BackupInfo
                    {
                        BackupPath = backupFile,
                        Name = Path.GetFileNameWithoutExtension(backupFile),
                        CreatedDate = fileInfo.CreationTime,
                        Size = fileInfo.Length
                    };

                    // Try to get file count from zip
                    try
                    {
                        using (var zip = ZipFile.OpenRead(backupFile))
                        {
                            backupInfo.FileCount = zip.Entries.Count;
                        }
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
            if (File.Exists(backupPath))
            {
                await Task.Run(() => File.Delete(backupPath)).ConfigureAwait(false);
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