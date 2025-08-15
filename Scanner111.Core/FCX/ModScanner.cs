using System.Runtime.CompilerServices;
using Scanner111.Core.Infrastructure;
using Scanner111.Core.Models;
using Scanner111.Core.Models.Yaml;

namespace Scanner111.Core.FCX;

public class ModScanner : IModScanner
{
    private readonly IApplicationSettingsService _appSettings;
    private readonly IBackupService _backupService;

    private readonly IHashValidationService _fileHashService;

    // Increase concurrency for file operations (2x CPU count for I/O bound operations)
    private readonly SemaphoreSlim _fileLock = new(Environment.ProcessorCount * 2, Environment.ProcessorCount * 2);
    private readonly ILogger<ModScanner> _logger;

    private readonly HashSet<string> _xseScriptFiles = new();
    private readonly IYamlSettingsProvider _yamlSettings;
    private bool _initialized;
    private string _xseAcronym = "XSE"; // Default value

    public ModScanner(
        ILogger<ModScanner> logger,
        IYamlSettingsProvider yamlSettings,
        IApplicationSettingsService appSettings,
        IBackupService backupService,
        IHashValidationService fileHashService)
    {
        _logger = logger;
        _yamlSettings = yamlSettings;
        _appSettings = appSettings;
        _backupService = backupService;
        _fileHashService = fileHashService;
    }

    public async Task<ModScanResult> ScanAllModsAsync(string modPath, IProgress<string>? progress = null,
        CancellationToken ct = default)
    {
        await InitializeAsync().ConfigureAwait(false);
        var stopwatch = Stopwatch.StartNew();
        var result = new ModScanResult();

        progress?.Report("Scanning unpacked mod files...");
        var unpackedResult = await ScanUnpackedModsAsync(modPath, progress, ct).ConfigureAwait(false);
        result.Issues.AddRange(unpackedResult.Issues);
        result.CleanedFiles.AddRange(unpackedResult.CleanedFiles);
        result.TotalFilesScanned = unpackedResult.TotalFilesScanned;

        progress?.Report("Scanning archived mod files...");
        var archivedResult = await ScanArchivedModsAsync(modPath, progress, ct).ConfigureAwait(false);
        result.Issues.AddRange(archivedResult.Issues);
        result.TotalArchivesScanned = archivedResult.TotalArchivesScanned;

        result.ScanDuration = stopwatch.Elapsed;
        return result;
    }

    public async Task<ModScanResult> ScanUnpackedModsAsync(string modPath, IProgress<string>? progress = null,
        CancellationToken ct = default)
    {
        await InitializeAsync().ConfigureAwait(false);
        var result = new ModScanResult();
        var stopwatch = Stopwatch.StartNew();

        if (!Directory.Exists(modPath))
        {
            _logger.LogWarning("Mod path does not exist: {Path}", modPath);
            return result;
        }

        var settings = await _appSettings.LoadSettingsAsync().ConfigureAwait(false);
        var backupPath = string.IsNullOrEmpty(settings.BackupDirectory)
            ? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Scanner 111",
                "Backup", "Cleaned Files")
            : Path.Combine(settings.BackupDirectory, "Cleaned Files");

        // First pass: cleanup
        progress?.Report("Performing initial mod files cleanup...");
        await CleanupModFilesAsync(modPath, backupPath, result, ct).ConfigureAwait(false);

        // Second pass: analyze files
        progress?.Report("Analyzing unpacked mod files...");
        await AnalyzeUnpackedFilesAsync(modPath, result, progress, ct).ConfigureAwait(false);

        result.ScanDuration = stopwatch.Elapsed;
        return result;
    }

    public async Task<ModScanResult> ScanArchivedModsAsync(string modPath, IProgress<string>? progress = null,
        CancellationToken ct = default)
    {
        await InitializeAsync().ConfigureAwait(false);
        var result = new ModScanResult();
        var stopwatch = Stopwatch.StartNew();

        if (!Directory.Exists(modPath))
        {
            _logger.LogWarning("Mod path does not exist: {Path}", modPath);
            return result;
        }

        var settings = await _appSettings.LoadSettingsAsync().ConfigureAwait(false);
        var bsarchPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Data", "BSArch.exe");

        if (!File.Exists(bsarchPath))
        {
            _logger.LogError("BSArch.exe not found at: {Path}", bsarchPath);
            return result;
        }

        // Scan BA2 archives
        await AnalyzeArchivedFilesAsync(modPath, bsarchPath, result, progress, ct).ConfigureAwait(false);

        result.ScanDuration = stopwatch.Elapsed;
        return result;
    }

    private async Task InitializeAsync()
    {
        if (_initialized) return;

        var settings = await _appSettings.LoadSettingsAsync().ConfigureAwait(false);

        // Detect game type from path
        var gameTypeString = CrashLogDirectoryManager.DetectGameType(settings.DefaultGamePath);
        var gameType = DetectGameTypeFromString(gameTypeString);

        // Load game info from YAML
        var yamlFile = gameType == GameType.Fallout4 ? "CLASSIC Fallout4.yaml" : "CLASSIC Skyrim.yaml";
        var yamlData = _yamlSettings.LoadYaml<ClassicFallout4YamlV2>(yamlFile);

        if (yamlData != null)
        {
            _xseAcronym = yamlData.GameInfo?.XseAcronym ?? "XSE";

            // Load XSE script files from all game versions
            if (yamlData.GameInfo?.Versions != null)
                foreach (var versionEntry in yamlData.GameInfo.Versions)
                {
                    var versionInfo = versionEntry.Value;
                    if (versionInfo.XseScripts != null)
                        foreach (var script in versionInfo.XseScripts.Keys)
                            _xseScriptFiles.Add(script.ToLowerInvariant());
                }
        }

        _initialized = true;
    }

    private GameType DetectGameTypeFromString(string gameTypeString)
    {
        return gameTypeString?.ToLower() switch
        {
            "fallout4" => GameType.Fallout4,
            "fallout4vr" => GameType.Fallout4, // Treat VR as regular Fallout 4
            "skyrimse" => GameType.SkyrimSE,
            "skyrim" => GameType.Skyrim,
            _ => GameType.Fallout4 // Default to Fallout 4
        };
    }

    private async Task CleanupModFilesAsync(string modPath, string backupPath, ModScanResult result,
        CancellationToken ct)
    {
        var filterNames = new[] { "readme", "changes", "changelog", "change log" };
        var settings = await _appSettings.LoadSettingsAsync().ConfigureAwait(false);

        await foreach (var dirInfo in EnumerateDirectoriesAsync(modPath, ct).ConfigureAwait(false))
        {
            ct.ThrowIfCancellationRequested();

            // Check for FOMod folders
            if (dirInfo.Name.Equals("fomod", StringComparison.OrdinalIgnoreCase))
            {
                var relativePath = Path.GetRelativePath(modPath, dirInfo.FullName);

                // TODO: Implement backup functionality when settings.EnableBackups is added
                // For now, just delete without backup

                try
                {
                    Directory.Delete(dirInfo.FullName, true);
                    result.CleanedFiles.Add(relativePath);
                    result.Issues.Add(new ModIssue
                    {
                        Type = ModIssueType.CleanupFile,
                        FilePath = relativePath,
                        Description = "FOMod folder moved to backup"
                    });
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Failed to remove FOMod folder: {Path}", dirInfo.FullName);
                }
            }
        }

        // Cleanup text files
        await foreach (var fileInfo in EnumerateFilesAsync(modPath, "*.txt", ct).ConfigureAwait(false))
        {
            ct.ThrowIfCancellationRequested();

            var fileName = fileInfo.Name.ToLowerInvariant();
            if (filterNames.Any(filter => fileName.Contains(filter)))
            {
                var relativePath = Path.GetRelativePath(modPath, fileInfo.FullName);

                // TODO: Implement backup functionality when settings.EnableBackups is added
                // For now, just delete without backup

                try
                {
                    File.Delete(fileInfo.FullName);
                    result.CleanedFiles.Add(relativePath);
                    result.Issues.Add(new ModIssue
                    {
                        Type = ModIssueType.CleanupFile,
                        FilePath = relativePath,
                        Description = "Documentation file moved to backup"
                    });
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Failed to remove file: {Path}", fileInfo.FullName);
                }
            }
        }
    }

    private async Task AnalyzeUnpackedFilesAsync(string modPath, ModScanResult result, IProgress<string>? progress,
        CancellationToken ct)
    {
        var processedDirs = new HashSet<string>();
        var fileCount = 0;

        await foreach (var fileInfo in EnumerateFilesAsync(modPath, "*.*", ct).ConfigureAwait(false))
        {
            ct.ThrowIfCancellationRequested();
            fileCount++;

            if (fileCount % 100 == 0) progress?.Report($"Analyzed {fileCount} files...");

            var relativePath = Path.GetRelativePath(modPath, fileInfo.FullName);
            var dirPath = Path.GetDirectoryName(relativePath) ?? "";
            var ext = fileInfo.Extension.ToLowerInvariant();

            // Check for animation data
            if (!processedDirs.Contains(dirPath) &&
                dirPath.Contains("animationfiledata", StringComparison.OrdinalIgnoreCase))
            {
                processedDirs.Add(dirPath);
                result.Issues.Add(new ModIssue
                {
                    Type = ModIssueType.AnimationData,
                    FilePath = dirPath,
                    Description = "Contains custom animation file data"
                });
            }

            // Check file-specific issues
            switch (ext)
            {
                case ".dds":
                    await CheckDdsFile(fileInfo.FullName, relativePath, result, ct).ConfigureAwait(false);
                    break;

                case ".tga":
                case ".png":
                    if (!fileInfo.FullName.Contains("BodySlide", StringComparison.OrdinalIgnoreCase))
                        result.Issues.Add(new ModIssue
                        {
                            Type = ModIssueType.TextureFormatIncorrect,
                            FilePath = relativePath,
                            Description = $"Texture file should be DDS format, not {ext.ToUpper()}"
                        });
                    break;

                case ".mp3":
                case ".m4a":
                    result.Issues.Add(new ModIssue
                    {
                        Type = ModIssueType.SoundFormatIncorrect,
                        FilePath = relativePath,
                        Description = $"Sound file should be XWM or WAV format, not {ext.ToUpper()}"
                    });
                    break;
            }

            // Check for XSE script files
            var fileName = fileInfo.Name.ToLowerInvariant();
            if (_xseScriptFiles.Contains(fileName) &&
                fileInfo.FullName.Contains("Scripts", StringComparison.OrdinalIgnoreCase) &&
                !fileInfo.FullName.Contains("workshop framework", StringComparison.OrdinalIgnoreCase))
                if (!processedDirs.Contains(dirPath + "_xse"))
                {
                    processedDirs.Add(dirPath + "_xse");
                    result.Issues.Add(new ModIssue
                    {
                        Type = ModIssueType.XseScriptFile,
                        FilePath = dirPath,
                        Description = $"Contains copies of {_xseAcronym} script files"
                    });
                }

            // Check for previs files
            if (fileName.EndsWith(".uvd") || fileName.EndsWith("_oc.nif"))
                if (!processedDirs.Contains(dirPath + "_previs"))
                {
                    processedDirs.Add(dirPath + "_previs");
                    result.Issues.Add(new ModIssue
                    {
                        Type = ModIssueType.PrevisFile,
                        FilePath = dirPath,
                        Description = "Contains loose precombine/previs files"
                    });
                }
        }

        result.TotalFilesScanned = fileCount;
    }

    private async Task AnalyzeArchivedFilesAsync(string modPath, string bsarchPath, ModScanResult result,
        IProgress<string>? progress, CancellationToken ct)
    {
        var archiveCount = 0;

        await foreach (var fileInfo in EnumerateFilesAsync(modPath, "*.ba2", ct).ConfigureAwait(false))
        {
            ct.ThrowIfCancellationRequested();
            archiveCount++;

            var fileName = fileInfo.Name;
            if (fileName.Equals("prp - main.ba2", StringComparison.OrdinalIgnoreCase))
                continue;

            progress?.Report($"Analyzing archive: {fileName}");

            // Read BA2 header
            byte[] header;
            try
            {
                await _fileLock.WaitAsync(ct).ConfigureAwait(false);
                try
                {
                    using var fs = new FileStream(fileInfo.FullName, FileMode.Open, FileAccess.Read, FileShare.Read,
                        4096, true);
                    header = new byte[12];
                    await fs.ReadAsync(header, 0, 12, ct).ConfigureAwait(false);
                }
                finally
                {
                    _fileLock.Release();
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to read BA2 header: {Path}", fileInfo.FullName);
                continue;
            }

            // Validate BA2 format
            var headerStr = Encoding.ASCII.GetString(header, 0, 4);
            var formatStr = Encoding.ASCII.GetString(header, 8, 4);

            if (headerStr != "BTDX" || (formatStr != "DX10" && formatStr != "GNRL"))
            {
                result.Issues.Add(new ModIssue
                {
                    Type = ModIssueType.ArchiveFormatIncorrect,
                    FilePath = fileName,
                    Description = "BA2 archive has incorrect format",
                    AdditionalInfo = $"Header: {headerStr}, Format: {formatStr}"
                });
                continue;
            }

            // Analyze archive contents
            if (formatStr == "DX10")
                await AnalyzeTextureArchive(fileInfo.FullName, fileName, bsarchPath, result, ct).ConfigureAwait(false);
            else
                await AnalyzeGeneralArchive(fileInfo.FullName, fileName, bsarchPath, result, ct).ConfigureAwait(false);
        }

        result.TotalArchivesScanned = archiveCount;
    }

    private async Task CheckDdsFile(string filePath, string relativePath, ModScanResult result, CancellationToken ct)
    {
        try
        {
            await _fileLock.WaitAsync(ct).ConfigureAwait(false);
            try
            {
                using var fs = new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.Read, 4096, true);
                var header = new byte[20];
                await fs.ReadAsync(header, 0, 20, ct).ConfigureAwait(false);

                // Check DDS signature
                if (header[0] == 'D' && header[1] == 'D' && header[2] == 'S' && header[3] == ' ')
                {
                    // Read dimensions
                    var width = BitConverter.ToUInt32(header, 16);
                    var height = BitConverter.ToUInt32(header, 12);

                    if (width % 2 != 0 || height % 2 != 0)
                        result.Issues.Add(new ModIssue
                        {
                            Type = ModIssueType.TextureDimensionsInvalid,
                            FilePath = relativePath,
                            Description = "DDS texture dimensions not divisible by 2",
                            AdditionalInfo = $"{width}x{height}"
                        });
                }
            }
            finally
            {
                _fileLock.Release();
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to check DDS file: {Path}", filePath);
        }
    }

    private async Task AnalyzeTextureArchive(string archivePath, string fileName, string bsarchPath,
        ModScanResult result, CancellationToken ct)
    {
        try
        {
            var process = new Process
            {
                StartInfo = new ProcessStartInfo
                {
                    FileName = bsarchPath,
                    Arguments = $"\"{archivePath}\" -dump",
                    UseShellExecute = false,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    CreateNoWindow = true
                }
            };

            process.Start();
            var output = await process.StandardOutput.ReadToEndAsync().ConfigureAwait(false);
            await process.WaitForExitAsync(ct).ConfigureAwait(false);

            if (process.ExitCode != 0)
            {
                _logger.LogError("BSArch failed for {Archive}: {Error}", fileName,
                    await process.StandardError.ReadToEndAsync().ConfigureAwait(false));
                return;
            }

            // Parse BSArch output
            var blocks = output.Split(new[] { "\n\n" }, StringSplitOptions.RemoveEmptyEntries);
            foreach (var block in blocks.Skip(4)) // Skip header info
            {
                if (string.IsNullOrWhiteSpace(block))
                    continue;

                var lines = block.Split('\n');
                if (lines.Length < 3)
                    continue;

                var fileLine = lines[0];
                var extLine = lines[1];
                var dimLine = lines[2];

                // Check texture format
                if (!extLine.Contains("Ext: dds"))
                {
                    var ext = extLine.Replace("Ext: ", "").Trim().ToUpper();
                    result.Issues.Add(new ModIssue
                    {
                        Type = ModIssueType.TextureFormatIncorrect,
                        FilePath = $"{fileName} > {fileLine}",
                        Description = $"Texture file should be DDS format, not {ext}"
                    });
                    continue;
                }

                // Check dimensions
                var dimParts = dimLine.Split(' ', StringSplitOptions.RemoveEmptyEntries);
                if (dimParts.Length >= 4 &&
                    uint.TryParse(dimParts[1], out var width) &&
                    uint.TryParse(dimParts[3], out var height))
                    if (width % 2 != 0 || height % 2 != 0)
                        result.Issues.Add(new ModIssue
                        {
                            Type = ModIssueType.TextureDimensionsInvalid,
                            FilePath = $"{fileName} > {fileLine}",
                            Description = "DDS texture dimensions not divisible by 2",
                            AdditionalInfo = $"{width}x{height}"
                        });
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to analyze texture archive: {Archive}", fileName);
        }
    }

    private async Task AnalyzeGeneralArchive(string archivePath, string fileName, string bsarchPath,
        ModScanResult result, CancellationToken ct)
    {
        try
        {
            var process = new Process
            {
                StartInfo = new ProcessStartInfo
                {
                    FileName = bsarchPath,
                    Arguments = $"\"{archivePath}\" -list",
                    UseShellExecute = false,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    CreateNoWindow = true
                }
            };

            process.Start();
            var output = await process.StandardOutput.ReadToEndAsync().ConfigureAwait(false);
            await process.WaitForExitAsync(ct).ConfigureAwait(false);

            if (process.ExitCode != 0)
            {
                _logger.LogError("BSArch failed for {Archive}: {Error}", fileName,
                    await process.StandardError.ReadToEndAsync().ConfigureAwait(false));
                return;
            }

            // Parse file list
            var lines = output.ToLowerInvariant().Split('\n', StringSplitOptions.RemoveEmptyEntries);
            var hasPrevis = false;
            var hasAnimData = false;
            var hasXseFiles = false;

            foreach (var line in lines.Skip(15)) // Skip header info
            {
                // Check sound formats
                if (line.EndsWith(".mp3") || line.EndsWith(".m4a"))
                {
                    var ext = Path.GetExtension(line).ToUpper().TrimStart('.');
                    result.Issues.Add(new ModIssue
                    {
                        Type = ModIssueType.SoundFormatIncorrect,
                        FilePath = $"{fileName} > {line}",
                        Description = $"Sound file should be XWM or WAV format, not {ext}"
                    });
                }

                // Check animation data
                if (!hasAnimData && line.Contains("animationfiledata"))
                {
                    hasAnimData = true;
                    result.Issues.Add(new ModIssue
                    {
                        Type = ModIssueType.AnimationData,
                        FilePath = fileName,
                        Description = "Archive contains custom animation file data"
                    });
                }

                // Check XSE files
                if (!hasXseFiles && line.Contains("scripts\\") &&
                    !archivePath.Contains("workshop framework", StringComparison.OrdinalIgnoreCase))
                {
                    var scriptName = Path.GetFileName(line);
                    if (_xseScriptFiles.Contains(scriptName))
                    {
                        hasXseFiles = true;
                        result.Issues.Add(new ModIssue
                        {
                            Type = ModIssueType.XseScriptFile,
                            FilePath = fileName,
                            Description = $"Archive contains copies of {_xseAcronym} script files"
                        });
                    }
                }

                // Check previs files
                if (!hasPrevis && (line.EndsWith(".uvd") || line.EndsWith("_oc.nif")))
                {
                    hasPrevis = true;
                    result.Issues.Add(new ModIssue
                    {
                        Type = ModIssueType.PrevisFile,
                        FilePath = fileName,
                        Description = "Archive contains precombine/previs files"
                    });
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to analyze general archive: {Archive}", fileName);
        }
    }

    private async IAsyncEnumerable<DirectoryInfo> EnumerateDirectoriesAsync(string path,
        [EnumeratorCancellation] CancellationToken ct)
    {
        await Task.Yield();

        var dirs = Directory.EnumerateDirectories(path, "*", SearchOption.AllDirectories);
        foreach (var dir in dirs)
        {
            ct.ThrowIfCancellationRequested();
            yield return new DirectoryInfo(dir);
        }
    }

    private async IAsyncEnumerable<FileInfo> EnumerateFilesAsync(string path, string pattern,
        [EnumeratorCancellation] CancellationToken ct)
    {
        await Task.Yield();

        var files = Directory.EnumerateFiles(path, pattern, SearchOption.AllDirectories);
        foreach (var file in files)
        {
            ct.ThrowIfCancellationRequested();
            yield return new FileInfo(file);
        }
    }
}