using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace Scanner111.Services;

/// <summary>
/// Service interface for managing game file backups.
/// </summary>
public interface IBackupService
{
    /// <summary>
    /// Backs up files for the specified category.
    /// </summary>
    Task<BackupResult> BackupAsync(string category, CancellationToken ct = default);

    /// <summary>
    /// Restores files for the specified category from backup.
    /// </summary>
    Task<BackupResult> RestoreAsync(string category, CancellationToken ct = default);

    /// <summary>
    /// Removes files for the specified category from the game folder.
    /// </summary>
    Task<BackupResult> RemoveAsync(string category, CancellationToken ct = default);

    /// <summary>
    /// Gets the backup folder path.
    /// </summary>
    string GetBackupFolderPath();

    /// <summary>
    /// Gets the game folder path. Returns null if not configured.
    /// </summary>
    string? GameFolderPath { get; set; }

    /// <summary>
    /// Checks if a backup exists for the specified category.
    /// </summary>
    bool BackupExists(string category);
}

/// <summary>
/// Result of a backup operation.
/// </summary>
public record BackupResult(bool Success, string Message, int FilesProcessed = 0);

/// <summary>
/// Full implementation of IBackupService for game file backup/restore/remove operations.
/// </summary>
public class BackupService : IBackupService
{
    private readonly string _backupFolderPath;

    /// <summary>
    /// Game folder path. Must be set before backup operations will work.
    /// </summary>
    public string? GameFolderPath { get; set; }

    // File patterns for each backup category
    private static readonly Dictionary<string, string[]> CategoryPatterns = new(StringComparer.OrdinalIgnoreCase)
    {
        ["XSE"] = new[] { "f4se_", "f4sevr_", "f4se_loader", "f4sevr_loader", "f4se_steam_loader", "CustomControlMap" },
        ["RESHADE"] = new[] { "reshade", "dxgi.dll", "d3d11.dll", "ReShade.ini" },
        ["VULKAN"] = new[] { "dxvk", "vulkan" },
        ["ENB"] = new[] { "enb", "d3d11.dll", "d3d9.dll", "enblocal.ini", "enbseries" }
    };

    public BackupService()
    {
        _backupFolderPath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "CLASSIC", "Backups");
    }

    public async Task<BackupResult> BackupAsync(string category, CancellationToken ct = default)
    {
        if (string.IsNullOrEmpty(GameFolderPath) || !Directory.Exists(GameFolderPath))
        {
            return new BackupResult(false, "Game folder not configured or does not exist.");
        }

        if (!CategoryPatterns.TryGetValue(category, out var patterns))
        {
            return new BackupResult(false, $"Unknown backup category: {category}");
        }

        var categoryBackupPath = Path.Combine(_backupFolderPath, $"Backup {category}");
        Directory.CreateDirectory(categoryBackupPath);

        var filesBackedUp = 0;
        var errors = new List<string>();

        await Task.Run(() =>
        {
            try
            {
                var gameFiles = Directory.GetFiles(GameFolderPath);
                var existingBackups = Directory.Exists(categoryBackupPath)
                    ? new HashSet<string>(Directory.GetFiles(categoryBackupPath).Select(Path.GetFileName)!,
                        StringComparer.OrdinalIgnoreCase)
                    : new HashSet<string>(StringComparer.OrdinalIgnoreCase);

                foreach (var file in gameFiles)
                {
                    ct.ThrowIfCancellationRequested();
                    var fileName = Path.GetFileName(file);

                    if (MatchesPattern(fileName, patterns) && !existingBackups.Contains(fileName))
                    {
                        try
                        {
                            var destPath = Path.Combine(categoryBackupPath, fileName);
                            File.Copy(file, destPath, overwrite: true);
                            filesBackedUp++;
                        }
                        catch (Exception ex)
                        {
                            errors.Add($"{fileName}: {ex.Message}");
                        }
                    }
                }
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception ex)
            {
                errors.Add($"Error accessing game folder: {ex.Message}");
            }
        }, ct);

        if (errors.Any())
        {
            return new BackupResult(false, $"Backup completed with errors: {string.Join("; ", errors)}", filesBackedUp);
        }

        return new BackupResult(true, $"Successfully backed up {filesBackedUp} {category} file(s).", filesBackedUp);
    }

    public async Task<BackupResult> RestoreAsync(string category, CancellationToken ct = default)
    {
        if (string.IsNullOrEmpty(GameFolderPath) || !Directory.Exists(GameFolderPath))
        {
            return new BackupResult(false, "Game folder not configured or does not exist.");
        }

        var categoryBackupPath = Path.Combine(_backupFolderPath, $"Backup {category}");
        if (!Directory.Exists(categoryBackupPath))
        {
            return new BackupResult(false, $"No backup found for {category}.");
        }

        var filesRestored = 0;
        var errors = new List<string>();

        await Task.Run(() =>
        {
            try
            {
                var backupFiles = Directory.GetFiles(categoryBackupPath);

                foreach (var file in backupFiles)
                {
                    ct.ThrowIfCancellationRequested();
                    var fileName = Path.GetFileName(file);

                    try
                    {
                        var destPath = Path.Combine(GameFolderPath, fileName);
                        File.Copy(file, destPath, overwrite: true);
                        filesRestored++;
                    }
                    catch (Exception ex)
                    {
                        errors.Add($"{fileName}: {ex.Message}");
                    }
                }
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception ex)
            {
                errors.Add($"Error accessing backup folder: {ex.Message}");
            }
        }, ct);

        if (errors.Any())
        {
            return new BackupResult(false, $"Restore completed with errors: {string.Join("; ", errors)}",
                filesRestored);
        }

        return new BackupResult(true, $"Successfully restored {filesRestored} {category} file(s).", filesRestored);
    }

    public async Task<BackupResult> RemoveAsync(string category, CancellationToken ct = default)
    {
        if (string.IsNullOrEmpty(GameFolderPath) || !Directory.Exists(GameFolderPath))
        {
            return new BackupResult(false, "Game folder not configured or does not exist.");
        }

        if (!CategoryPatterns.TryGetValue(category, out var patterns))
        {
            return new BackupResult(false, $"Unknown backup category: {category}");
        }

        var filesRemoved = 0;
        var errors = new List<string>();

        await Task.Run(() =>
        {
            try
            {
                var gameFiles = Directory.GetFiles(GameFolderPath);

                foreach (var file in gameFiles)
                {
                    ct.ThrowIfCancellationRequested();
                    var fileName = Path.GetFileName(file);

                    if (MatchesPattern(fileName, patterns))
                    {
                        try
                        {
                            File.Delete(file);
                            filesRemoved++;
                        }
                        catch (Exception ex)
                        {
                            errors.Add($"{fileName}: {ex.Message}");
                        }
                    }
                }
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception ex)
            {
                errors.Add($"Error accessing game folder: {ex.Message}");
            }
        }, ct);

        if (errors.Any())
        {
            return new BackupResult(false, $"Remove completed with errors: {string.Join("; ", errors)}", filesRemoved);
        }

        return new BackupResult(true, $"Successfully removed {filesRemoved} {category} file(s) from game folder.",
            filesRemoved);
    }

    public string GetBackupFolderPath()
    {
        if (!Directory.Exists(_backupFolderPath))
        {
            Directory.CreateDirectory(_backupFolderPath);
        }

        return _backupFolderPath;
    }

    public bool BackupExists(string category)
    {
        var categoryPath = Path.Combine(_backupFolderPath, $"Backup {category}");
        return Directory.Exists(categoryPath) && Directory.EnumerateFiles(categoryPath).Any();
    }

    private static bool MatchesPattern(string fileName, string[] patterns)
    {
        return patterns.Any(pattern =>
            fileName.Contains(pattern, StringComparison.OrdinalIgnoreCase));
    }
}

