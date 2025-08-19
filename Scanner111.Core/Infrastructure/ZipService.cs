using System.IO.Compression;
using Scanner111.Core.Abstractions;

namespace Scanner111.Core.Infrastructure;

/// <summary>
/// Implementation of zip file operations using System.IO.Compression
/// </summary>
public class ZipService : IZipService
{
    private readonly IFileSystem _fileSystem;
    private readonly IPathService _pathService;
    private readonly ILogger<ZipService> _logger;

    public ZipService(IFileSystem fileSystem, IPathService pathService, ILogger<ZipService> logger)
    {
        _fileSystem = fileSystem;
        _pathService = pathService;
        _logger = logger;
    }

    public async Task<bool> CreateZipAsync(string zipPath, Dictionary<string, string> files, CancellationToken cancellationToken = default)
    {
        try
        {
            // Ensure the directory exists
            var directory = _pathService.GetDirectoryName(zipPath);
            if (!string.IsNullOrEmpty(directory))
                _fileSystem.CreateDirectory(directory);

            using (var zipArchive = ZipFile.Open(zipPath, ZipArchiveMode.Create))
            {
                foreach (var file in files)
                {
                    cancellationToken.ThrowIfCancellationRequested();

                    if (_fileSystem.FileExists(file.Key))
                    {
                        await Task.Run(() => zipArchive.CreateEntryFromFile(file.Key, file.Value), cancellationToken)
                            .ConfigureAwait(false);
                    }
                    else
                    {
                        _logger.LogWarning("File not found for zip: {File}", file.Key);
                    }
                }
            }

            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating zip: {ZipPath}", zipPath);
            return false;
        }
    }

    public async Task<bool> AddFileToZipAsync(string zipPath, string sourceFile, string entryName, CancellationToken cancellationToken = default)
    {
        try
        {
            if (!_fileSystem.FileExists(sourceFile))
            {
                _logger.LogWarning("Source file not found: {SourceFile}", sourceFile);
                return false;
            }

            using (var zipArchive = ZipFile.Open(zipPath, ZipArchiveMode.Update))
            {
                await Task.Run(() => zipArchive.CreateEntryFromFile(sourceFile, entryName), cancellationToken)
                    .ConfigureAwait(false);
            }

            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error adding file to zip: {SourceFile} -> {ZipPath}", sourceFile, zipPath);
            return false;
        }
    }

    public async Task<IEnumerable<string>> ExtractZipAsync(string zipPath, string targetDirectory, 
        IEnumerable<string>? filesToExtract = null, bool overwrite = true, CancellationToken cancellationToken = default)
    {
        var extractedFiles = new List<string>();

        try
        {
            if (!_fileSystem.FileExists(zipPath))
            {
                _logger.LogError("Zip file not found: {ZipPath}", zipPath);
                return extractedFiles;
            }

            _fileSystem.CreateDirectory(targetDirectory);

            using (var zipArchive = ZipFile.OpenRead(zipPath))
            {
                var entries = filesToExtract != null
                    ? zipArchive.Entries.Where(e => filesToExtract.Contains(e.FullName.Replace('/', '\\')))
                    : zipArchive.Entries;

                foreach (var entry in entries)
                {
                    cancellationToken.ThrowIfCancellationRequested();

                    var targetPath = _pathService.Combine(targetDirectory, entry.FullName.Replace('/', '\\'));
                    
                    // Ensure target directory exists
                    var targetDir = _pathService.GetDirectoryName(targetPath);
                    if (!string.IsNullOrEmpty(targetDir))
                        _fileSystem.CreateDirectory(targetDir);

                    await Task.Run(() => entry.ExtractToFile(targetPath, overwrite), cancellationToken)
                        .ConfigureAwait(false);

                    extractedFiles.Add(targetPath);
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error extracting zip: {ZipPath}", zipPath);
        }

        return extractedFiles;
    }

    public async Task<bool> ExtractFileFromZipAsync(string zipPath, string entryName, string targetPath, 
        bool overwrite = true, CancellationToken cancellationToken = default)
    {
        try
        {
            if (!_fileSystem.FileExists(zipPath))
            {
                _logger.LogError("Zip file not found: {ZipPath}", zipPath);
                return false;
            }

            using (var zipArchive = ZipFile.OpenRead(zipPath))
            {
                var entry = zipArchive.GetEntry(entryName.Replace('\\', '/'));
                if (entry == null)
                {
                    _logger.LogWarning("Entry not found in zip: {EntryName}", entryName);
                    return false;
                }

                // Ensure target directory exists
                var targetDir = _pathService.GetDirectoryName(targetPath);
                if (!string.IsNullOrEmpty(targetDir))
                    _fileSystem.CreateDirectory(targetDir);

                await Task.Run(() => entry.ExtractToFile(targetPath, overwrite), cancellationToken)
                    .ConfigureAwait(false);
            }

            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error extracting file from zip: {EntryName} from {ZipPath}", entryName, zipPath);
            return false;
        }
    }

    public async Task<IEnumerable<string>> ListZipEntriesAsync(string zipPath)
    {
        var entries = new List<string>();

        try
        {
            if (!_fileSystem.FileExists(zipPath))
            {
                _logger.LogError("Zip file not found: {ZipPath}", zipPath);
                return entries;
            }

            await Task.Run(() =>
            {
                using (var zipArchive = ZipFile.OpenRead(zipPath))
                {
                    entries.AddRange(zipArchive.Entries.Select(e => e.FullName));
                }
            }).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error listing zip entries: {ZipPath}", zipPath);
        }

        return entries;
    }

    public async Task<int> GetZipEntryCountAsync(string zipPath)
    {
        try
        {
            if (!_fileSystem.FileExists(zipPath))
            {
                _logger.LogError("Zip file not found: {ZipPath}", zipPath);
                return 0;
            }

            return await Task.Run(() =>
            {
                using (var zipArchive = ZipFile.OpenRead(zipPath))
                {
                    return zipArchive.Entries.Count;
                }
            }).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting zip entry count: {ZipPath}", zipPath);
            return 0;
        }
    }

    public async Task<bool> IsValidZipAsync(string zipPath)
    {
        try
        {
            if (!_fileSystem.FileExists(zipPath))
                return false;

            return await Task.Run(() =>
            {
                try
                {
                    using (var zipArchive = ZipFile.OpenRead(zipPath))
                    {
                        // If we can open it and access entries, it's valid
                        var _ = zipArchive.Entries.Count;
                        return true;
                    }
                }
                catch
                {
                    return false;
                }
            }).ConfigureAwait(false);
        }
        catch
        {
            return false;
        }
    }
}