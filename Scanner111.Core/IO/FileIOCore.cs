using System.Text;
using Microsoft.Extensions.Logging;

namespace Scanner111.Core.IO;

/// <summary>
///     Provides async-first file I/O operations with automatic encoding detection.
///     Thread-safe implementation suitable for concurrent operations.
/// </summary>
public class FileIoCore : IFileIoCore
{
    private readonly Encoding _defaultEncoding;
    private readonly ILogger<FileIoCore> _logger;

    static FileIoCore()
    {
        // Register code page provider for legacy encodings
        Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
    }

    public FileIoCore(ILogger<FileIoCore> logger)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _defaultEncoding = Encoding.UTF8;
    }

    /// <inheritdoc />
    public async Task<string> ReadFileAsync(string path, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);

        try
        {
            // Try to detect encoding first
            var encoding = await DetectEncodingAsync(path, cancellationToken).ConfigureAwait(false);

            _logger.LogDebug("Reading file {Path} with encoding {Encoding}", path, encoding.EncodingName);

            return await File.ReadAllTextAsync(path, encoding, cancellationToken).ConfigureAwait(false);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _logger.LogError(ex, "Failed to read file {Path}", path);
            throw;
        }
    }

    /// <inheritdoc />
    public async Task<string[]> ReadLinesAsync(string path, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);

        try
        {
            var encoding = await DetectEncodingAsync(path, cancellationToken).ConfigureAwait(false);

            _logger.LogDebug("Reading lines from {Path} with encoding {Encoding}", path, encoding.EncodingName);

            return await File.ReadAllLinesAsync(path, encoding, cancellationToken).ConfigureAwait(false);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _logger.LogError(ex, "Failed to read lines from file {Path}", path);
            throw;
        }
    }

    /// <inheritdoc />
    public async Task WriteFileAsync(string path, string content, Encoding? encoding = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        ArgumentNullException.ThrowIfNull(content);

        encoding ??= _defaultEncoding;

        try
        {
            // Ensure directory exists
            var directory = Path.GetDirectoryName(path);
            if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
            {
                Directory.CreateDirectory(directory);
                _logger.LogDebug("Created directory {Directory}", directory);
            }

            _logger.LogDebug("Writing file {Path} with encoding {Encoding}", path, encoding.EncodingName);

            await File.WriteAllTextAsync(path, content, encoding, cancellationToken).ConfigureAwait(false);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _logger.LogError(ex, "Failed to write file {Path}", path);
            throw;
        }
    }

    /// <inheritdoc />
    public async Task<bool> FileExistsAsync(string path, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);

        // File.Exists is synchronous but very fast, so we run it in a task
        return await Task.Run(() => File.Exists(path), cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc />
    public async Task<DateTime?> GetLastWriteTimeAsync(string path, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);

        try
        {
            var fileInfo = await Task.Run(() => new FileInfo(path), cancellationToken).ConfigureAwait(false);

            if (!fileInfo.Exists) return null;

            return fileInfo.LastWriteTimeUtc;
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _logger.LogError(ex, "Failed to get last write time for {Path}", path);
            return null;
        }
    }

    /// <inheritdoc />
    public async Task CreateDirectoryAsync(string path, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);

        try
        {
            await Task.Run(() =>
            {
                if (!Directory.Exists(path))
                {
                    Directory.CreateDirectory(path);
                    _logger.LogDebug("Created directory {Path}", path);
                }
            }, cancellationToken).ConfigureAwait(false);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _logger.LogError(ex, "Failed to create directory {Path}", path);
            throw;
        }
    }

    /// <inheritdoc />
    public async Task<bool> DeleteFileAsync(string path, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);

        try
        {
            return await Task.Run(() =>
            {
                if (File.Exists(path))
                {
                    File.Delete(path);
                    _logger.LogDebug("Deleted file {Path}", path);
                    return true;
                }

                return false;
            }, cancellationToken).ConfigureAwait(false);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _logger.LogError(ex, "Failed to delete file {Path}", path);
            throw;
        }
    }

    /// <inheritdoc />
    public async Task CopyFileAsync(string sourcePath, string destinationPath, bool overwrite = false,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(sourcePath);
        ArgumentException.ThrowIfNullOrWhiteSpace(destinationPath);

        try
        {
            // Ensure destination directory exists
            var destDir = Path.GetDirectoryName(destinationPath);
            if (!string.IsNullOrEmpty(destDir))
                await CreateDirectoryAsync(destDir, cancellationToken).ConfigureAwait(false);

            await Task.Run(() =>
            {
                File.Copy(sourcePath, destinationPath, overwrite);
                _logger.LogDebug("Copied file from {Source} to {Destination}", sourcePath, destinationPath);
            }, cancellationToken).ConfigureAwait(false);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _logger.LogError(ex, "Failed to copy file from {Source} to {Destination}",
                sourcePath, destinationPath);
            throw;
        }
    }

    /// <summary>
    ///     Detects the encoding of a file by reading its BOM or analyzing content.
    /// </summary>
    private async Task<Encoding> DetectEncodingAsync(string path, CancellationToken cancellationToken)
    {
        try
        {
            // Read first few bytes to detect BOM
            var buffer = new byte[4];
            await using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read);
            var bytesRead = await stream.ReadAsync(buffer.AsMemory(0, 4), cancellationToken).ConfigureAwait(false);

            if (bytesRead >= 3)
            {
                // Check for UTF-8 BOM
                if (buffer[0] == 0xEF && buffer[1] == 0xBB && buffer[2] == 0xBF) return new UTF8Encoding(true);

                // Check for UTF-16 LE BOM
                if (buffer[0] == 0xFF && buffer[1] == 0xFE) return Encoding.Unicode;

                // Check for UTF-16 BE BOM
                if (buffer[0] == 0xFE && buffer[1] == 0xFF) return Encoding.BigEndianUnicode;

                // Check for UTF-32 LE BOM
                if (bytesRead >= 4 && buffer[0] == 0xFF && buffer[1] == 0xFE
                    && buffer[2] == 0x00 && buffer[3] == 0x00)
                    return Encoding.UTF32;
            }

            // No BOM found, default to UTF-8 without BOM
            return new UTF8Encoding(false);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to detect encoding for {Path}, using default UTF-8", path);
            return new UTF8Encoding(false);
        }
    }
}