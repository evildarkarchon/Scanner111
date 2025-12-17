using System.Collections.Concurrent;
using Scanner111.Common.Models.ScanGame;

namespace Scanner111.Common.Services.ScanGame;

/// <summary>
/// Provides functionality for scanning unpacked (loose) mod files in a directory.
/// </summary>
/// <remarks>
/// This scanner performs parallel directory traversal to efficiently detect
/// issues with unpacked mod files. It can optionally analyze DDS textures
/// for dimension issues using the <see cref="IDDSAnalyzer"/> service.
/// </remarks>
public sealed class UnpackedModsScanner : IUnpackedModsScanner
{
    /// <summary>
    /// File name patterns that indicate readme/changelog files to be cleaned up.
    /// </summary>
    private static readonly string[] CleanupFilePatterns =
    [
        "readme",
        "changes",
        "changelog",
        "change log"
    ];

    /// <summary>
    /// Texture file extensions that should be converted to DDS.
    /// </summary>
    private static readonly HashSet<string> InvalidTextureExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".tga",
        ".png"
    };

    /// <summary>
    /// Sound file extensions that should be converted to XWM/WAV.
    /// </summary>
    private static readonly HashSet<string> InvalidSoundExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".mp3",
        ".m4a"
    };

    /// <summary>
    /// File suffixes that indicate previs/precombine files.
    /// </summary>
    private static readonly string[] PrevisFileSuffixes =
    [
        ".uvd",
        "_oc.nif"
    ];

    /// <summary>
    /// Directories to skip when checking for texture format issues.
    /// </summary>
    private const string BodySlideDirectoryName = "BodySlide";

    /// <summary>
    /// Directory name that indicates animation data.
    /// </summary>
    private const string AnimationFileDataDirectoryName = "animationfiledata";

    /// <summary>
    /// Directory name for FOMOD installer folders.
    /// </summary>
    private const string FomodDirectoryName = "fomod";

    /// <summary>
    /// Directory name for script files.
    /// </summary>
    private const string ScriptsDirectoryName = "scripts";

    /// <summary>
    /// Workshop Framework mod name (XSE scripts from this mod are allowed).
    /// </summary>
    private const string WorkshopFrameworkName = "workshop framework";

    private readonly IDDSAnalyzer? _ddsAnalyzer;

    /// <summary>
    /// Initializes a new instance of the <see cref="UnpackedModsScanner"/> class.
    /// </summary>
    public UnpackedModsScanner() : this(null)
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="UnpackedModsScanner"/> class with DDS analysis support.
    /// </summary>
    /// <param name="ddsAnalyzer">Optional DDS analyzer for texture dimension validation.</param>
    public UnpackedModsScanner(IDDSAnalyzer? ddsAnalyzer)
    {
        _ddsAnalyzer = ddsAnalyzer;
    }

    /// <inheritdoc />
    public Task<UnpackedScanResult> ScanAsync(
        string modPath,
        IReadOnlyDictionary<string, string>? xseScriptFiles = null,
        bool analyzeDdsTextures = true,
        CancellationToken cancellationToken = default)
    {
        return ScanWithProgressAsync(modPath, xseScriptFiles, analyzeDdsTextures, null, cancellationToken);
    }

    /// <inheritdoc />
    public async Task<UnpackedScanResult> ScanWithProgressAsync(
        string modPath,
        IReadOnlyDictionary<string, string>? xseScriptFiles,
        bool analyzeDdsTextures,
        IProgress<UnpackedScanProgress>? progress,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(modPath);

        if (!Directory.Exists(modPath))
        {
            return new UnpackedScanResult { TotalDirectoriesScanned = 0, TotalFilesScanned = 0 };
        }

        // Collect all directories with their contents
        var directories = await CollectDirectoriesAsync(modPath, cancellationToken).ConfigureAwait(false);

        if (directories.Count == 0)
        {
            return new UnpackedScanResult { TotalDirectoriesScanned = 0, TotalFilesScanned = 0 };
        }

        // Thread-safe collections for issues
        var cleanupIssues = new ConcurrentBag<CleanupIssue>();
        var animationDataIssues = new ConcurrentBag<AnimationDataIssue>();
        var textureFormatIssues = new ConcurrentBag<UnpackedTextureFormatIssue>();
        var textureDimensionIssues = new ConcurrentBag<UnpackedTextureDimensionIssue>();
        var soundFormatIssues = new ConcurrentBag<UnpackedSoundFormatIssue>();
        var xseFileIssues = new ConcurrentBag<UnpackedXseFileIssue>();
        var previsFileIssues = new ConcurrentBag<PrevisFileIssue>();

        // Track directories that have already reported certain issues
        var reportedAnimationDirs = new ConcurrentDictionary<string, byte>();
        var reportedXseDirs = new ConcurrentDictionary<string, byte>();
        var reportedPrevisDirs = new ConcurrentDictionary<string, byte>();

        // Track progress
        int directoriesScanned = 0;
        int filesScanned = 0;

        // Process directories in parallel batches
        var options = new ParallelOptions
        {
            MaxDegreeOfParallelism = Environment.ProcessorCount,
            CancellationToken = cancellationToken
        };

        await Parallel.ForEachAsync(directories, options, async (dirInfo, ct) =>
        {
            await ProcessDirectoryAsync(
                dirInfo,
                modPath,
                xseScriptFiles,
                analyzeDdsTextures,
                cleanupIssues,
                animationDataIssues,
                textureFormatIssues,
                textureDimensionIssues,
                soundFormatIssues,
                xseFileIssues,
                previsFileIssues,
                reportedAnimationDirs,
                reportedXseDirs,
                reportedPrevisDirs,
                ct).ConfigureAwait(false);

            var currentDirs = Interlocked.Increment(ref directoriesScanned);
            var currentFiles = Interlocked.Add(ref filesScanned, dirInfo.Files.Count);

            progress?.Report(new UnpackedScanProgress(
                dirInfo.Path,
                currentDirs,
                currentFiles,
                cleanupIssues.Count + animationDataIssues.Count + textureFormatIssues.Count +
                textureDimensionIssues.Count + soundFormatIssues.Count + xseFileIssues.Count +
                previsFileIssues.Count));
        }).ConfigureAwait(false);

        return new UnpackedScanResult
        {
            TotalDirectoriesScanned = directories.Count,
            TotalFilesScanned = directories.Sum(d => d.Files.Count),
            CleanupIssues = cleanupIssues.ToList().AsReadOnly(),
            AnimationDataIssues = animationDataIssues.ToList().AsReadOnly(),
            TextureFormatIssues = textureFormatIssues.ToList().AsReadOnly(),
            TextureDimensionIssues = textureDimensionIssues.ToList().AsReadOnly(),
            SoundFormatIssues = soundFormatIssues.ToList().AsReadOnly(),
            XseFileIssues = xseFileIssues.ToList().AsReadOnly(),
            PrevisFileIssues = previsFileIssues.ToList().AsReadOnly()
        };
    }

    /// <summary>
    /// Collects all directories and their contents in the given path.
    /// </summary>
    private static Task<IReadOnlyList<DirectoryInfo>> CollectDirectoriesAsync(
        string rootPath,
        CancellationToken cancellationToken)
    {
        return Task.Run(() =>
        {
            var result = new List<DirectoryInfo>();
            var enumOptions = new EnumerationOptions
            {
                IgnoreInaccessible = true,
                RecurseSubdirectories = false,
                MatchCasing = MatchCasing.CaseInsensitive
            };

            try
            {
                // Process root directory
                var rootDir = new System.IO.DirectoryInfo(rootPath);
                if (rootDir.Exists)
                {
                    var rootSubdirs = rootDir.GetDirectories();
                    var rootFiles = rootDir.GetFiles();
                    result.Add(new DirectoryInfo(
                        rootPath,
                        rootSubdirs.Select(d => d.Name).ToList(),
                        rootFiles.Select(f => f.Name).ToList()));

                    // Process all subdirectories
                    var queue = new Queue<System.IO.DirectoryInfo>(rootSubdirs);
                    while (queue.Count > 0)
                    {
                        cancellationToken.ThrowIfCancellationRequested();

                        var current = queue.Dequeue();
                        try
                        {
                            var subdirs = current.GetDirectories();
                            var files = current.GetFiles();

                            result.Add(new DirectoryInfo(
                                current.FullName,
                                subdirs.Select(d => d.Name).ToList(),
                                files.Select(f => f.Name).ToList()));

                            foreach (var subdir in subdirs)
                            {
                                queue.Enqueue(subdir);
                            }
                        }
                        catch (UnauthorizedAccessException)
                        {
                            // Skip inaccessible directories
                        }
                        catch (DirectoryNotFoundException)
                        {
                            // Directory was deleted during enumeration
                        }
                    }
                }
            }
            catch (UnauthorizedAccessException)
            {
                // Return what we found so far
            }
            catch (DirectoryNotFoundException)
            {
                // Root directory doesn't exist
            }

            return (IReadOnlyList<DirectoryInfo>)result.AsReadOnly();
        }, cancellationToken);
    }

    /// <summary>
    /// Processes a single directory for issues.
    /// </summary>
    private async Task ProcessDirectoryAsync(
        DirectoryInfo dirInfo,
        string modPath,
        IReadOnlyDictionary<string, string>? xseScriptFiles,
        bool analyzeDdsTextures,
        ConcurrentBag<CleanupIssue> cleanupIssues,
        ConcurrentBag<AnimationDataIssue> animationDataIssues,
        ConcurrentBag<UnpackedTextureFormatIssue> textureFormatIssues,
        ConcurrentBag<UnpackedTextureDimensionIssue> textureDimensionIssues,
        ConcurrentBag<UnpackedSoundFormatIssue> soundFormatIssues,
        ConcurrentBag<UnpackedXseFileIssue> xseFileIssues,
        ConcurrentBag<PrevisFileIssue> previsFileIssues,
        ConcurrentDictionary<string, byte> reportedAnimationDirs,
        ConcurrentDictionary<string, byte> reportedXseDirs,
        ConcurrentDictionary<string, byte> reportedPrevisDirs,
        CancellationToken cancellationToken)
    {
        var relativePath = GetRelativePath(dirInfo.Path, modPath);
        var parentRelativePath = GetParentRelativePath(relativePath);

        // Check subdirectories for FOMOD folders and animation data
        foreach (var subdir in dirInfo.Subdirectories)
        {
            var subdirLower = subdir.ToLowerInvariant();

            // Check for FOMOD folders
            if (subdirLower == FomodDirectoryName)
            {
                var fomodPath = Path.Combine(dirInfo.Path, subdir);
                var fomodRelative = Path.Combine(relativePath, subdir);
                cleanupIssues.Add(new CleanupIssue(fomodPath, fomodRelative, CleanupItemType.FomodFolder));
            }

            // Check for animation data directories
            if (subdirLower == AnimationFileDataDirectoryName)
            {
                if (reportedAnimationDirs.TryAdd(parentRelativePath, 0))
                {
                    animationDataIssues.Add(new AnimationDataIssue(dirInfo.Path, parentRelativePath));
                }
            }
        }

        // Determine if we're inside a BodySlide directory (skip texture format checks)
        var isInBodySlideDir = dirInfo.Path.Contains(BodySlideDirectoryName, StringComparison.OrdinalIgnoreCase);

        // Determine if this is a scripts directory and not Workshop Framework
        var currentDirName = Path.GetFileName(dirInfo.Path);
        var isScriptsDir = currentDirName.Equals(ScriptsDirectoryName, StringComparison.OrdinalIgnoreCase);
        var isWorkshopFramework = dirInfo.Path.Contains(WorkshopFrameworkName, StringComparison.OrdinalIgnoreCase);

        // Check files
        var ddsFilesToAnalyze = new List<string>();

        foreach (var fileName in dirInfo.Files)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var filePath = Path.Combine(dirInfo.Path, fileName);
            var fileRelative = Path.Combine(relativePath, fileName);
            var fileExt = Path.GetExtension(fileName);
            var fileNameLower = fileName.ToLowerInvariant();

            // Check for readme/changelog files
            if (fileExt.Equals(".txt", StringComparison.OrdinalIgnoreCase))
            {
                if (CleanupFilePatterns.Any(pattern => fileNameLower.Contains(pattern)))
                {
                    cleanupIssues.Add(new CleanupIssue(filePath, fileRelative, CleanupItemType.ReadmeFile));
                    continue;
                }
            }

            // Check for texture format issues (TGA/PNG outside BodySlide)
            if (!isInBodySlideDir && InvalidTextureExtensions.Contains(fileExt))
            {
                textureFormatIssues.Add(new UnpackedTextureFormatIssue(
                    filePath,
                    fileRelative,
                    fileExt.TrimStart('.').ToUpperInvariant()));
                continue;
            }

            // Collect DDS files for dimension analysis
            if (fileExt.Equals(".dds", StringComparison.OrdinalIgnoreCase))
            {
                ddsFilesToAnalyze.Add(filePath);
                continue;
            }

            // Check for sound format issues
            if (InvalidSoundExtensions.Contains(fileExt))
            {
                soundFormatIssues.Add(new UnpackedSoundFormatIssue(
                    filePath,
                    fileRelative,
                    fileExt.TrimStart('.').ToUpperInvariant()));
                continue;
            }

            // Check for XSE script files (only in scripts directory, not Workshop Framework)
            if (isScriptsDir && !isWorkshopFramework && xseScriptFiles is not null)
            {
                if (xseScriptFiles.Keys.Any(key => key.Equals(fileName, StringComparison.OrdinalIgnoreCase)))
                {
                    if (reportedXseDirs.TryAdd(parentRelativePath, 0))
                    {
                        xseFileIssues.Add(new UnpackedXseFileIssue(dirInfo.Path, parentRelativePath));
                    }
                    continue;
                }
            }

            // Check for previs/precombine files
            if (PrevisFileSuffixes.Any(suffix => fileNameLower.EndsWith(suffix, StringComparison.OrdinalIgnoreCase)))
            {
                if (reportedPrevisDirs.TryAdd(parentRelativePath, 0))
                {
                    previsFileIssues.Add(new PrevisFileIssue(dirInfo.Path, parentRelativePath));
                }
            }
        }

        // Analyze DDS files if analyzer is available and enabled
        if (analyzeDdsTextures && _ddsAnalyzer is not null && ddsFilesToAnalyze.Count > 0)
        {
            await AnalyzeDdsFilesAsync(
                ddsFilesToAnalyze,
                modPath,
                textureDimensionIssues,
                cancellationToken).ConfigureAwait(false);
        }
    }

    /// <summary>
    /// Analyzes DDS files for dimension issues.
    /// </summary>
    private async Task AnalyzeDdsFilesAsync(
        List<string> ddsFiles,
        string modPath,
        ConcurrentBag<UnpackedTextureDimensionIssue> textureDimensionIssues,
        CancellationToken cancellationToken)
    {
        foreach (var filePath in ddsFiles)
        {
            cancellationToken.ThrowIfCancellationRequested();

            try
            {
                var info = await _ddsAnalyzer!.AnalyzeAsync(filePath, cancellationToken).ConfigureAwait(false);
                if (info is null)
                {
                    continue;
                }

                var fileRelative = GetRelativePath(filePath, modPath);
                var issues = new List<string>();

                // Check for odd dimensions
                if (info.Width % 2 != 0 || info.Height % 2 != 0)
                {
                    issues.Add($"Odd dimensions ({info.Width}x{info.Height})");
                }

                // Check for BC format compatibility
                if (info.IsCompressed && !_ddsAnalyzer.IsValidBCDimensions(info.Width, info.Height))
                {
                    issues.Add($"BC compressed but not multiple of 4 ({info.Width}x{info.Height})");
                }

                if (issues.Count > 0)
                {
                    textureDimensionIssues.Add(new UnpackedTextureDimensionIssue(
                        filePath,
                        fileRelative,
                        info.Width,
                        info.Height,
                        string.Join("; ", issues)));
                }
            }
            catch (Exception)
            {
                // Skip files that can't be analyzed
            }
        }
    }

    /// <summary>
    /// Gets the relative path from a full path and a base path.
    /// </summary>
    private static string GetRelativePath(string fullPath, string basePath)
    {
        if (fullPath.StartsWith(basePath, StringComparison.OrdinalIgnoreCase))
        {
            var relative = fullPath.Substring(basePath.Length);
            return relative.TrimStart(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        }

        return fullPath;
    }

    /// <summary>
    /// Gets the parent directory from a relative path.
    /// </summary>
    private static string GetParentRelativePath(string relativePath)
    {
        var parts = relativePath.Split(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        return parts.Length > 0 ? parts[0] : relativePath;
    }

    /// <summary>
    /// Represents information about a directory during scanning.
    /// </summary>
    private sealed record DirectoryInfo(
        string Path,
        IReadOnlyList<string> Subdirectories,
        IReadOnlyList<string> Files);
}
