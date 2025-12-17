using System.Text;
using Scanner111.Common.Models.ScanGame;

namespace Scanner111.Common.Services.ScanGame;

/// <summary>
/// Provides functionality for scanning and validating Bethesda BA2 archive files.
/// </summary>
/// <remarks>
/// BA2 archives use a header with the following structure:
/// - Bytes 0-3: Magic signature "BTDX"
/// - Bytes 4-7: Version (uint32, typically 1)
/// - Bytes 8-11: Format type "GNRL" (general) or "DX10" (textures)
/// </remarks>
public sealed class BA2Scanner : IBA2Scanner
{
    private const int HeaderSize = 12;
    private static readonly byte[] MagicSignature = Encoding.ASCII.GetBytes("BTDX");
    private static readonly byte[] GeneralFormat = Encoding.ASCII.GetBytes("GNRL");
    private static readonly byte[] TextureFormat = Encoding.ASCII.GetBytes("DX10");

    // Files to exclude from scanning
    private static readonly HashSet<string> ExcludedFiles = new(StringComparer.OrdinalIgnoreCase)
    {
        "prp - main.ba2" // Pre-combined references pack
    };

    /// <inheritdoc />
    public async Task<BA2ScanResult> ScanAsync(
        string modPath,
        IReadOnlyDictionary<string, string>? xseScriptFolders = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(modPath);

        var ba2Files = await FindBA2FilesAsync(modPath, cancellationToken).ConfigureAwait(false);
        if (ba2Files.Count == 0)
        {
            return new BA2ScanResult { TotalFilesScanned = 0 };
        }

        var formatIssues = new List<BA2FormatIssue>();
        var textureDimensionIssues = new List<TextureDimensionIssue>();
        var textureFormatIssues = new List<TextureFormatIssue>();
        var soundFormatIssues = new List<SoundFormatIssue>();
        var xseFileIssues = new List<XseFileIssue>();

        // Process BA2 files concurrently with reasonable parallelism
        var options = new ParallelOptions
        {
            MaxDegreeOfParallelism = Environment.ProcessorCount,
            CancellationToken = cancellationToken
        };

        // Use a lock object for thread-safe collection access
        var lockObj = new object();

        await Parallel.ForEachAsync(ba2Files, options, async (archivePath, ct) =>
        {
            var result = await ProcessBA2FileAsync(archivePath, xseScriptFolders, ct).ConfigureAwait(false);

            lock (lockObj)
            {
                if (result.FormatIssue is not null)
                    formatIssues.Add(result.FormatIssue);

                textureDimensionIssues.AddRange(result.TextureDimensionIssues);
                textureFormatIssues.AddRange(result.TextureFormatIssues);
                soundFormatIssues.AddRange(result.SoundFormatIssues);

                if (result.XseFileIssue is not null)
                    xseFileIssues.Add(result.XseFileIssue);
            }
        }).ConfigureAwait(false);

        return new BA2ScanResult
        {
            TotalFilesScanned = ba2Files.Count,
            FormatIssues = formatIssues.AsReadOnly(),
            TextureDimensionIssues = textureDimensionIssues.AsReadOnly(),
            TextureFormatIssues = textureFormatIssues.AsReadOnly(),
            SoundFormatIssues = soundFormatIssues.AsReadOnly(),
            XseFileIssues = xseFileIssues.AsReadOnly()
        };
    }

    /// <inheritdoc />
    public Task<IReadOnlyList<string>> FindBA2FilesAsync(
        string modPath,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(modPath);

        return Task.Run(() =>
        {
            var result = new List<string>();

            if (!Directory.Exists(modPath))
            {
                return (IReadOnlyList<string>)result.AsReadOnly();
            }

            try
            {
                var options = new EnumerationOptions
                {
                    IgnoreInaccessible = true,
                    RecurseSubdirectories = true,
                    MatchCasing = MatchCasing.CaseInsensitive
                };

                foreach (var file in Directory.EnumerateFiles(modPath, "*.ba2", options))
                {
                    cancellationToken.ThrowIfCancellationRequested();

                    var fileName = Path.GetFileName(file);
                    if (!ExcludedFiles.Contains(fileName))
                    {
                        result.Add(file);
                    }
                }
            }
            catch (UnauthorizedAccessException)
            {
                // Return what we found so far
            }
            catch (DirectoryNotFoundException)
            {
                // Directory was deleted during enumeration
            }

            return (IReadOnlyList<string>)result.AsReadOnly();
        }, cancellationToken);
    }

    /// <inheritdoc />
    public async Task<BA2HeaderInfo> ReadHeaderAsync(
        string archivePath,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(archivePath);

        try
        {
            var header = new byte[HeaderSize];
            await using var stream = new FileStream(
                archivePath,
                FileMode.Open,
                FileAccess.Read,
                FileShare.Read,
                bufferSize: HeaderSize,
                useAsync: true);

            var bytesRead = await stream.ReadAsync(header.AsMemory(0, HeaderSize), cancellationToken)
                .ConfigureAwait(false);

            if (bytesRead < HeaderSize)
            {
                return new BA2HeaderInfo(false, BA2Format.Unknown, 0);
            }

            return ParseHeader(header);
        }
        catch (IOException)
        {
            return new BA2HeaderInfo(false, BA2Format.Unknown, 0);
        }
        catch (UnauthorizedAccessException)
        {
            return new BA2HeaderInfo(false, BA2Format.Unknown, 0);
        }
    }

    /// <summary>
    /// Parses a BA2 header byte array into header info.
    /// </summary>
    private static BA2HeaderInfo ParseHeader(ReadOnlySpan<byte> header)
    {
        // Check magic signature (bytes 0-3)
        if (!header[..4].SequenceEqual(MagicSignature))
        {
            return new BA2HeaderInfo(false, BA2Format.Unknown, 0);
        }

        // Read version (bytes 4-7)
        var version = BitConverter.ToUInt32(header.Slice(4, 4));

        // Check format type (bytes 8-11)
        var formatBytes = header.Slice(8, 4);
        BA2Format format;

        if (formatBytes.SequenceEqual(GeneralFormat))
        {
            format = BA2Format.General;
        }
        else if (formatBytes.SequenceEqual(TextureFormat))
        {
            format = BA2Format.Texture;
        }
        else
        {
            return new BA2HeaderInfo(false, BA2Format.Unknown, version);
        }

        return new BA2HeaderInfo(true, format, version);
    }

    /// <summary>
    /// Processes a single BA2 file and collects all issues.
    /// </summary>
    private async Task<BA2FileIssues> ProcessBA2FileAsync(
        string archivePath,
        IReadOnlyDictionary<string, string>? xseScriptFolders,
        CancellationToken cancellationToken)
    {
        var fileName = Path.GetFileName(archivePath);
        var result = new BA2FileIssues();

        // Read and validate header
        var headerInfo = await ReadHeaderAsync(archivePath, cancellationToken).ConfigureAwait(false);

        if (!headerInfo.IsValid)
        {
            // Try to get the actual header bytes for diagnostic purposes
            var headerBytes = await TryReadHeaderBytesAsync(archivePath, cancellationToken).ConfigureAwait(false);
            result.FormatIssue = new BA2FormatIssue(archivePath, fileName, headerBytes);
            return result;
        }

        // For now, we only validate headers
        // Full content analysis (texture dimensions, sound formats, XSE files)
        // would require parsing the full BA2 archive structure or using BSArch
        // This can be added in a future enhancement

        return result;
    }

    /// <summary>
    /// Attempts to read header bytes for diagnostic purposes.
    /// </summary>
    private static async Task<string> TryReadHeaderBytesAsync(
        string archivePath,
        CancellationToken cancellationToken)
    {
        try
        {
            var header = new byte[HeaderSize];
            await using var stream = new FileStream(
                archivePath,
                FileMode.Open,
                FileAccess.Read,
                FileShare.Read,
                bufferSize: HeaderSize,
                useAsync: true);

            var bytesRead = await stream.ReadAsync(header.AsMemory(0, HeaderSize), cancellationToken)
                .ConfigureAwait(false);

            if (bytesRead == 0)
            {
                return "<empty file>";
            }

            return BitConverter.ToString(header, 0, bytesRead);
        }
        catch
        {
            return "<unable to read>";
        }
    }

    /// <summary>
    /// Internal record for collecting issues from a single BA2 file.
    /// </summary>
    private sealed record BA2FileIssues
    {
        public BA2FormatIssue? FormatIssue { get; set; }
        public List<TextureDimensionIssue> TextureDimensionIssues { get; } = new();
        public List<TextureFormatIssue> TextureFormatIssues { get; } = new();
        public List<SoundFormatIssue> SoundFormatIssues { get; } = new();
        public XseFileIssue? XseFileIssue { get; set; }
    }
}
