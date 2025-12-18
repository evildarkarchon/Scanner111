using System.Diagnostics;
using Microsoft.Extensions.Logging;
using Scanner111.Common.Models.ScanGame;

namespace Scanner111.Common.Services.ScanGame;

/// <summary>
/// Provides functionality to interact with BSArch.exe for BA2 archive operations.
/// </summary>
public sealed class BSArchService : IBSArchService
{
    private readonly ILogger<BSArchService> _logger;
    private const int DefaultTimeoutMs = 30000; // 30 seconds
    private const int MaxOutputBuffer = 1024 * 1024; // 1MB

    // Common locations where BSArch might be found
    private static readonly string[] CommonBSArchLocations =
    [
        "BSArch.exe",
        "Tools/BSArch.exe",
        "tools/BSArch.exe",
        "BSArch/BSArch.exe"
    ];

    private string? _bsarchPath;

    /// <summary>
    /// Initializes a new instance of the <see cref="BSArchService"/> class.
    /// </summary>
    /// <param name="logger">The logger instance.</param>
    public BSArchService(ILogger<BSArchService> logger)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <inheritdoc />
    public string? BSArchPath
    {
        get => _bsarchPath;
        set => _bsarchPath = value;
    }

    /// <inheritdoc />
    public bool IsAvailable => !string.IsNullOrEmpty(_bsarchPath) && File.Exists(_bsarchPath);

    /// <inheritdoc />
    public Task<bool> TryLocateBSArchAsync()
    {
        return Task.Run(() =>
        {
            // Check if already configured and valid
            if (IsAvailable)
            {
                return true;
            }

            // Check PATH environment variable
            var pathEnv = Environment.GetEnvironmentVariable("PATH") ?? string.Empty;
            var pathDirs = pathEnv.Split(Path.PathSeparator, StringSplitOptions.RemoveEmptyEntries);

            foreach (var dir in pathDirs)
            {
                var bsarchPath = Path.Combine(dir, "BSArch.exe");
                if (File.Exists(bsarchPath))
                {
                    _bsarchPath = bsarchPath;
                    return true;
                }
            }

            // Check common locations relative to current directory
            var baseDir = AppContext.BaseDirectory;
            foreach (var location in CommonBSArchLocations)
            {
                var fullPath = Path.Combine(baseDir, location);
                if (File.Exists(fullPath))
                {
                    _bsarchPath = fullPath;
                    return true;
                }
            }

            return false;
        });
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<string>> ListArchiveContentsAsync(
        string archivePath,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(archivePath);

        if (!IsAvailable)
        {
            return Array.Empty<string>();
        }

        var (exitCode, stdout, _) = await RunBSArchAsync(
            archivePath,
            "-list",
            cancellationToken).ConfigureAwait(false);

        if (exitCode != 0 || string.IsNullOrEmpty(stdout))
        {
            return Array.Empty<string>();
        }

        // Parse the output - skip header lines, extract file paths
        var files = new List<string>();
        var lines = stdout.Split('\n', StringSplitOptions.RemoveEmptyEntries);

        // BSArch -list output format has header lines, then file paths
        // Skip first ~15 lines of header info
        var startIndex = Math.Min(15, lines.Length);
        for (int i = startIndex; i < lines.Length; i++)
        {
            var line = lines[i].Trim();
            if (!string.IsNullOrEmpty(line) && !line.StartsWith("Error:", StringComparison.OrdinalIgnoreCase))
            {
                files.Add(line);
            }
        }

        return files.AsReadOnly();
    }

    /// <inheritdoc />
    public async Task<ArchiveTextureAnalysisResult> DumpTextureInfoAsync(
        string archivePath,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(archivePath);

        var archiveName = Path.GetFileName(archivePath);

        if (!IsAvailable)
        {
            return new ArchiveTextureAnalysisResult
            {
                ArchivePath = archivePath,
                ArchiveName = archiveName
            };
        }

        var (exitCode, stdout, stderr) = await RunBSArchAsync(
            archivePath,
            "-dump",
            cancellationToken).ConfigureAwait(false);

        if (exitCode != 0 || string.IsNullOrEmpty(stdout))
        {
            return new ArchiveTextureAnalysisResult
            {
                ArchivePath = archivePath,
                ArchiveName = archiveName
            };
        }

        // Check for errors in output
        if (stdout.Contains("Error:", StringComparison.OrdinalIgnoreCase))
        {
            return new ArchiveTextureAnalysisResult
            {
                ArchivePath = archivePath,
                ArchiveName = archiveName
            };
        }

        return ParseTextureDumpOutput(stdout, archivePath, archiveName);
    }

    /// <summary>
    /// Parses BSArch -dump output for texture information.
    /// </summary>
    /// <remarks>
    /// BSArch -dump output format for texture BA2s:
    /// <code>
    /// [texture path]
    /// Ext: dds
    /// W: 1024 H: 1024 ...
    /// ...
    ///
    /// [next texture]
    /// </code>
    /// </remarks>
    private static ArchiveTextureAnalysisResult ParseTextureDumpOutput(
        string output,
        string archivePath,
        string archiveName)
    {
        var dimensionIssues = new List<TextureDimensionIssue>();
        var formatIssues = new List<TextureFormatIssue>();
        var textureCount = 0;

        // Split output into blocks separated by double newlines
        var blocks = output.Split(new[] { "\n\n", "\r\n\r\n" }, StringSplitOptions.RemoveEmptyEntries);

        // Skip header blocks (first ~4 blocks contain archive info)
        var startIndex = Math.Min(4, blocks.Length);

        for (int i = startIndex; i < blocks.Length; i++)
        {
            var block = blocks[i].Trim();
            if (string.IsNullOrEmpty(block))
            {
                continue;
            }

            var textureInfo = ParseTextureBlock(block, archiveName);
            if (textureInfo is null)
            {
                continue;
            }

            textureCount++;

            // Check for non-DDS format
            if (!textureInfo.Extension.Equals("dds", StringComparison.OrdinalIgnoreCase))
            {
                formatIssues.Add(new TextureFormatIssue(
                    archiveName,
                    textureInfo.TexturePath,
                    textureInfo.Extension.ToUpperInvariant()));
            }
            // Check for odd dimensions (only for DDS textures)
            else if (textureInfo.Width % 2 != 0 || textureInfo.Height % 2 != 0)
            {
                dimensionIssues.Add(new TextureDimensionIssue(
                    archiveName,
                    textureInfo.TexturePath,
                    textureInfo.Width,
                    textureInfo.Height));
            }
        }

        return new ArchiveTextureAnalysisResult
        {
            ArchivePath = archivePath,
            ArchiveName = archiveName,
            TotalTextures = textureCount,
            DimensionIssues = dimensionIssues.AsReadOnly(),
            FormatIssues = formatIssues.AsReadOnly()
        };
    }

    /// <summary>
    /// Parses a single texture block from BSArch -dump output.
    /// </summary>
    private static ArchiveTextureInfo? ParseTextureBlock(string block, string archiveName)
    {
        var lines = block.Split('\n', StringSplitOptions.RemoveEmptyEntries);
        if (lines.Length < 3)
        {
            return null;
        }

        // First line is the texture path
        var texturePath = lines[0].Trim();
        if (string.IsNullOrEmpty(texturePath))
        {
            return null;
        }

        // Second line contains "Ext: xxx"
        var extLine = lines[1].Trim();
        string extension = "unknown";
        if (extLine.StartsWith("Ext:", StringComparison.OrdinalIgnoreCase))
        {
            extension = extLine[4..].Trim();
        }

        // Third line contains dimensions "W: xxx H: xxx ..."
        int width = 0, height = 0;
        var dimLine = lines[2].Trim();
        var parts = dimLine.Split(' ', StringSplitOptions.RemoveEmptyEntries);

        for (int i = 0; i < parts.Length - 1; i++)
        {
            if (parts[i].Equals("W:", StringComparison.OrdinalIgnoreCase) &&
                int.TryParse(parts[i + 1], out var w))
            {
                width = w;
            }
            else if (parts[i].Equals("H:", StringComparison.OrdinalIgnoreCase) &&
                     int.TryParse(parts[i + 1], out var h))
            {
                height = h;
            }
        }

        return new ArchiveTextureInfo(texturePath, extension, width, height);
    }

    /// <summary>
    /// Runs BSArch with the specified arguments and returns the output.
    /// </summary>
    private async Task<(int ExitCode, string Stdout, string Stderr)> RunBSArchAsync(
        string archivePath,
        string command,
        CancellationToken cancellationToken)
    {
        if (!IsAvailable)
        {
            return (-1, string.Empty, "BSArch not available");
        }

        var startInfo = new ProcessStartInfo
        {
            FileName = _bsarchPath!,
            Arguments = $"\"{archivePath}\" {command}",
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            CreateNoWindow = true,
            StandardOutputEncoding = System.Text.Encoding.UTF8,
            StandardErrorEncoding = System.Text.Encoding.UTF8
        };

        using var process = new Process { StartInfo = startInfo };
        var stdoutBuilder = new System.Text.StringBuilder(MaxOutputBuffer);
        var stderrBuilder = new System.Text.StringBuilder(MaxOutputBuffer);

        process.OutputDataReceived += (_, e) =>
        {
            if (e.Data is not null && stdoutBuilder.Length < MaxOutputBuffer)
            {
                stdoutBuilder.AppendLine(e.Data);
            }
        };

        process.ErrorDataReceived += (_, e) =>
        {
            if (e.Data is not null && stderrBuilder.Length < MaxOutputBuffer)
            {
                stderrBuilder.AppendLine(e.Data);
            }
        };

        try
        {
            process.Start();
            process.BeginOutputReadLine();
            process.BeginErrorReadLine();

            using var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            cts.CancelAfter(DefaultTimeoutMs);

            await process.WaitForExitAsync(cts.Token).ConfigureAwait(false);

            return (process.ExitCode, stdoutBuilder.ToString(), stderrBuilder.ToString());
        }
        catch (OperationCanceledException)
        {
            _logger.LogWarning("BSArch process timed out or was cancelled: {ArchivePath} {Command}", archivePath, command);
            try
            {
                process.Kill(entireProcessTree: true);
            }
            catch
            {
                // Ignore kill errors
            }

            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to execute BSArch: {ArchivePath} {Command}", archivePath, command);
            return (-1, string.Empty, ex.Message);
        }
    }
}
