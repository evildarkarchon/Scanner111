using System.Text;

namespace Scanner111.Common.Services.FileIO;

/// <summary>
/// Provides file I/O operations with UTF-8 encoding and error handling.
/// Crash logs may contain malformed UTF-8 sequences, so error handling is enabled.
/// </summary>
public class FileIOService : IFileIOService
{
    private static readonly Encoding Utf8WithErrorHandling =
        new UTF8Encoding(encoderShouldEmitUTF8Identifier: false, throwOnInvalidBytes: false);

    /// <inheritdoc/>
    public async Task<string> ReadFileAsync(string path, CancellationToken cancellationToken = default)
    {
        await using var stream = new FileStream(
            path,
            FileMode.Open,
            FileAccess.Read,
            FileShare.Read,
            bufferSize: 4096,
            useAsync: true);

        using var reader = new StreamReader(stream, Utf8WithErrorHandling);
        return await reader.ReadToEndAsync(cancellationToken);
    }

    /// <inheritdoc/>
    public async Task WriteFileAsync(string path, string content, CancellationToken cancellationToken = default)
    {
        await using var stream = new FileStream(
            path,
            FileMode.Create,
            FileAccess.Write,
            FileShare.None,
            bufferSize: 4096,
            useAsync: true);

        await using var writer = new StreamWriter(stream, Utf8WithErrorHandling);
        await writer.WriteAsync(content.AsMemory(), cancellationToken);
    }

    /// <inheritdoc/>
    public Task<bool> FileExistsAsync(string path)
    {
        // File.Exists is synchronous and fast, so we don't need true async here
        return Task.FromResult(File.Exists(path));
    }
}
