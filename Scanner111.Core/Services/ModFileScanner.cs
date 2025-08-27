using System.Collections.Concurrent;
using System.Diagnostics;
using System.IO.MemoryMappedFiles;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.RegularExpressions;
using Microsoft.Extensions.Logging;
using Scanner111.Core.Configuration;
using Scanner111.Core.Models;

namespace Scanner111.Core.Services;

/// <summary>
///     Service for scanning mod files to detect potential issues with textures, sounds, 
///     scripts, and other mod files. Thread-safe implementation with concurrent processing.
/// </summary>
public sealed class ModFileScanner : IModFileScanner, IAsyncDisposable
{
    private readonly ILogger<ModFileScanner> _logger;
    private readonly IAsyncYamlSettingsCore _yamlCore;
    private readonly SemaphoreSlim _processingSemaphore;
    private readonly SemaphoreSlim _fileOpsSemaphore;
    private readonly SemaphoreSlim _ddsReadSemaphore;
    
    private static readonly string[] DocumentationExtensions = { ".txt", ".md", ".pdf", ".rtf" };
    private static readonly string[] DocumentationKeywords = { "readme", "changes", "changelog", "change log" };
    private static readonly string[] TextureExtensions = { ".dds", ".tga", ".png", ".jpg", ".jpeg" };
    private static readonly string[] SoundExtensions = { ".xwm", ".wav", ".mp3", ".m4a" };
    
    private bool _disposed;

    public ModFileScanner(ILogger<ModFileScanner> logger, IAsyncYamlSettingsCore yamlCore)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _yamlCore = yamlCore ?? throw new ArgumentNullException(nameof(yamlCore));

        // Calculate optimal concurrency limits based on system resources
        var processorCount = Environment.ProcessorCount;
        var memoryGb = GC.GetTotalMemory(false) / (1024.0 * 1024.0 * 1024.0);
        var memoryFactor = Math.Min(memoryGb / 8.0, 2.0); // 8GB baseline

        _processingSemaphore = new SemaphoreSlim(Math.Min((int)(processorCount * memoryFactor), 8));
        _fileOpsSemaphore = new SemaphoreSlim(Math.Min((int)(processorCount * 4 * memoryFactor), 32));
        _ddsReadSemaphore = new SemaphoreSlim(Math.Min((int)(processorCount * 16 * memoryFactor), 128));
    }

    /// <inheritdoc />
    public async Task<string> ScanModsUnpackedAsync(string modPath, CancellationToken cancellationToken = default)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        ArgumentException.ThrowIfNullOrWhiteSpace(modPath);
        cancellationToken.ThrowIfCancellationRequested();

        var stopwatch = Stopwatch.StartNew();
        var issues = new ConcurrentBag<ModScanIssue>();

        try
        {
            _logger.LogInformation("Starting unpacked mod scan at: {ModPath}", modPath);

            if (!Directory.Exists(modPath))
            {
                var errorMsg = $"Mods folder not found: {modPath}";
                _logger.LogWarning(errorMsg);
                return await FormatScanReportAsync("unpacked", issues, errorMsg).ConfigureAwait(false);
            }

            // Get XSE settings for script file detection
            var (xseAcronym, xseScriptFiles) = await GetXseSettingsAsync(cancellationToken).ConfigureAwait(false);

            // Process all directories concurrently in batches
            var allDirs = Directory.EnumerateDirectories(modPath, "*", SearchOption.AllDirectories).ToList();
            var batchSize = 50;

            _logger.LogDebug("Processing {DirectoryCount} directories in batches of {BatchSize}", allDirs.Count, batchSize);

            for (var i = 0; i < allDirs.Count; i += batchSize)
            {
                var batch = allDirs.Skip(i).Take(batchSize).ToList();
                var tasks = batch.Select(dir => ProcessUnpackedDirectoryAsync(dir, modPath, xseAcronym, xseScriptFiles, issues, cancellationToken));
                
                await Task.WhenAll(tasks).ConfigureAwait(false);
                cancellationToken.ThrowIfCancellationRequested();
            }

            stopwatch.Stop();
            _logger.LogInformation("Unpacked mod scan completed in {Duration}ms with {IssueCount} issues",
                stopwatch.ElapsedMilliseconds, issues.Count);

            return await FormatScanReportAsync("unpacked", issues).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to scan unpacked mods at: {ModPath}", modPath);
            return await FormatScanReportAsync("unpacked", issues, ex.Message).ConfigureAwait(false);
        }
    }

    /// <inheritdoc />
    public async Task<string> ScanModsArchivedAsync(string modPath, string bsArchPath, CancellationToken cancellationToken = default)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        ArgumentException.ThrowIfNullOrWhiteSpace(modPath);
        ArgumentException.ThrowIfNullOrWhiteSpace(bsArchPath);
        cancellationToken.ThrowIfCancellationRequested();

        var stopwatch = Stopwatch.StartNew();
        var issues = new ConcurrentBag<ModScanIssue>();

        try
        {
            _logger.LogInformation("Starting archived mod scan at: {ModPath}", modPath);

            if (!Directory.Exists(modPath))
            {
                var errorMsg = $"Mods folder not found: {modPath}";
                _logger.LogWarning(errorMsg);
                return await FormatScanReportAsync("archived", issues, errorMsg).ConfigureAwait(false);
            }

            if (!File.Exists(bsArchPath))
            {
                var errorMsg = $"BSArch.exe not found: {bsArchPath}";
                _logger.LogWarning(errorMsg);
                return await FormatScanReportAsync("archived", issues, errorMsg).ConfigureAwait(false);
            }

            // Find all BA2 files
            var ba2Files = Directory.EnumerateFiles(modPath, "*.ba2", SearchOption.AllDirectories)
                .Where(file => !Path.GetFileName(file).Equals("prp - main.ba2", StringComparison.OrdinalIgnoreCase))
                .ToList();

            _logger.LogDebug("Found {Ba2Count} BA2 files to process", ba2Files.Count);

            // Get XSE settings for script file detection
            var (xseAcronym, xseScriptFiles) = await GetXseSettingsAsync(cancellationToken).ConfigureAwait(false);

            // Process BA2 files in batches to avoid overwhelming the system
            var batchSize = Math.Min(8, ba2Files.Count);
            
            for (var i = 0; i < ba2Files.Count; i += batchSize)
            {
                var batch = ba2Files.Skip(i).Take(batchSize).ToList();
                var tasks = batch.Select(file => ProcessBa2FileAsync(file, bsArchPath, xseAcronym, xseScriptFiles, issues, cancellationToken));
                
                await Task.WhenAll(tasks).ConfigureAwait(false);
                
                // Small delay between batches to prevent system overload
                if (i + batchSize < ba2Files.Count)
                    await Task.Delay(100, cancellationToken).ConfigureAwait(false);
            }

            stopwatch.Stop();
            _logger.LogInformation("Archived mod scan completed in {Duration}ms with {IssueCount} issues",
                stopwatch.ElapsedMilliseconds, issues.Count);

            return await FormatScanReportAsync("archived", issues).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to scan archived mods at: {ModPath}", modPath);
            return await FormatScanReportAsync("archived", issues, ex.Message).ConfigureAwait(false);
        }
    }

    /// <inheritdoc />
    public async Task<string> CheckLogErrorsAsync(string folderPath, CancellationToken cancellationToken = default)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        ArgumentException.ThrowIfNullOrWhiteSpace(folderPath);
        cancellationToken.ThrowIfCancellationRequested();

        var stopwatch = Stopwatch.StartNew();
        var issues = new ConcurrentBag<ModScanIssue>();

        try
        {
            _logger.LogInformation("Checking log errors in: {FolderPath}", folderPath);

            if (!Directory.Exists(folderPath))
            {
                var errorMsg = $"Log folder not found: {folderPath}";
                _logger.LogWarning(errorMsg);
                return await FormatScanReportAsync("log_errors", issues, errorMsg).ConfigureAwait(false);
            }

            // Get error patterns and exclusions from settings
            var (errorPatterns, excludeFiles, excludeErrors) = await GetLogErrorSettingsAsync(cancellationToken).ConfigureAwait(false);

            if (errorPatterns.Count == 0)
            {
                _logger.LogDebug("No error patterns configured for log checking");
                return await FormatScanReportAsync("log_errors", issues).ConfigureAwait(false);
            }

            // Find valid log files (excluding crash logs and ignored files)
            var logFiles = Directory.EnumerateFiles(folderPath, "*.log", SearchOption.TopDirectoryOnly)
                .Where(file => !Path.GetFileName(file).Contains("crash-", StringComparison.OrdinalIgnoreCase))
                .Where(file => !excludeFiles.Any(exclude => Path.GetFileName(file).Contains(exclude, StringComparison.OrdinalIgnoreCase)))
                .ToList();

            _logger.LogDebug("Processing {LogCount} log files for errors", logFiles.Count);

            // Process log files concurrently
            var tasks = logFiles.Select(file => ProcessLogFileAsync(file, errorPatterns, excludeErrors, issues, cancellationToken));
            await Task.WhenAll(tasks).ConfigureAwait(false);

            stopwatch.Stop();
            _logger.LogInformation("Log error check completed in {Duration}ms with {IssueCount} issues",
                stopwatch.ElapsedMilliseconds, issues.Count);

            return await FormatScanReportAsync("log_errors", issues).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to check log errors in: {FolderPath}", folderPath);
            return await FormatScanReportAsync("log_errors", issues, ex.Message).ConfigureAwait(false);
        }
    }

    #region Private Helper Methods

    private async Task<(string XseAcronym, IReadOnlySet<string> XseScriptFiles)> GetXseSettingsAsync(CancellationToken cancellationToken)
    {
        try
        {
            // These would come from YAML settings - using defaults for now
            var xseAcronym = "F4SE"; // Would be retrieved from game-specific settings
            var scriptFiles = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
            {
                "f4se_loader.exe",
                "f4se_steam_loader.dll",
                "f4se.exe"
            }; // Would be retrieved from YAML settings

            await Task.CompletedTask.ConfigureAwait(false);
            return (xseAcronym, scriptFiles);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to get XSE settings, using defaults");
            return ("XSE", new HashSet<string>(StringComparer.OrdinalIgnoreCase));
        }
    }

    private async Task<(IReadOnlySet<string> ErrorPatterns, IReadOnlySet<string> ExcludeFiles, IReadOnlySet<string> ExcludeErrors)> GetLogErrorSettingsAsync(CancellationToken cancellationToken)
    {
        try
        {
            // These would come from YAML settings - using defaults for now
            var errorPatterns = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
            {
                "error", "exception", "failed", "crash"
            };
            var excludeFiles = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
            {
                "temp", "backup"
            };
            var excludeErrors = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
            {
                "expected error", "handled exception"
            };

            await Task.CompletedTask.ConfigureAwait(false);
            return (errorPatterns, excludeFiles, excludeErrors);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to get log error settings, using defaults");
            return (new HashSet<string>(StringComparer.OrdinalIgnoreCase), 
                   new HashSet<string>(StringComparer.OrdinalIgnoreCase),
                   new HashSet<string>(StringComparer.OrdinalIgnoreCase));
        }
    }

    private async Task ProcessUnpackedDirectoryAsync(
        string directoryPath,
        string modBasePath,
        string xseAcronym,
        IReadOnlySet<string> xseScriptFiles,
        ConcurrentBag<ModScanIssue> issues,
        CancellationToken cancellationToken)
    {
        await _fileOpsSemaphore.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            var dirInfo = new DirectoryInfo(directoryPath);
            var relativePath = Path.GetRelativePath(modBasePath, directoryPath);

            // Check for animation data directory
            if (dirInfo.Name.Equals("animationfiledata", StringComparison.OrdinalIgnoreCase))
            {
                var parentMod = Path.GetDirectoryName(relativePath) ?? relativePath;
                issues.Add(ModScanIssue.CreateCustomIssue(
                    ModIssueType.AnimationData,
                    $"Custom animation file data detected",
                    parentMod));
            }

            // Process files in directory
            var files = dirInfo.GetFiles();
            var fileTasks = new List<Task>();

            var ddsFiles = new List<string>();
            
            foreach (var file in files)
            {
                var fileName = file.Name;
                var fileExtension = file.Extension.ToLowerInvariant();
                var fileRelativePath = Path.GetRelativePath(modBasePath, file.FullName);

                // Check for documentation files that should be moved
                if (IsDocumentationFile(fileName))
                {
                    issues.Add(ModScanIssue.CreateCustomIssue(
                        ModIssueType.Cleanup,
                        "Documentation file moved to backup",
                        fileRelativePath));
                    continue;
                }

                // Check texture formats
                if (fileExtension == ".dds")
                {
                    ddsFiles.Add(file.FullName);
                }
                else if ((fileExtension == ".tga" || fileExtension == ".png") && 
                         !directoryPath.Contains("BodySlide", StringComparison.OrdinalIgnoreCase))
                {
                    issues.Add(ModScanIssue.CreateFormatIssue(
                        ModIssueType.TextureFormat,
                        fileRelativePath,
                        "DDS",
                        fileExtension[1..].ToUpperInvariant()));
                }

                // Check sound formats
                else if (fileExtension == ".mp3" || fileExtension == ".m4a")
                {
                    issues.Add(ModScanIssue.CreateFormatIssue(
                        ModIssueType.SoundFormat,
                        fileRelativePath,
                        "XWM or WAV",
                        fileExtension[1..].ToUpperInvariant()));
                }

                // Check for XSE script files
                else if (xseScriptFiles.Contains(fileName) && 
                         directoryPath.Contains("Scripts", StringComparison.OrdinalIgnoreCase) &&
                         !directoryPath.Contains("workshop framework", StringComparison.OrdinalIgnoreCase))
                {
                    var parentMod = Path.GetDirectoryName(relativePath) ?? relativePath;
                    issues.Add(ModScanIssue.CreateCustomIssue(
                        ModIssueType.XseFiles,
                        $"Contains copies of {xseAcronym} script files",
                        parentMod));
                }

                // Check for previs files
                else if (fileExtension == ".uvd" || fileName.EndsWith("_oc.nif", StringComparison.OrdinalIgnoreCase))
                {
                    var parentMod = Path.GetDirectoryName(relativePath) ?? relativePath;
                    issues.Add(ModScanIssue.CreateCustomIssue(
                        ModIssueType.PrevisFiles,
                        "Contains loose precombine/previs files",
                        parentMod));
                }

                cancellationToken.ThrowIfCancellationRequested();
            }

            // Process DDS files for dimension checking
            if (ddsFiles.Count > 0)
            {
                fileTasks.Add(ProcessDdsFilesAsync(ddsFiles, modBasePath, issues, cancellationToken));
            }

            if (fileTasks.Count > 0)
            {
                await Task.WhenAll(fileTasks).ConfigureAwait(false);
            }
        }
        finally
        {
            _fileOpsSemaphore.Release();
        }
    }

    private async Task ProcessDdsFilesAsync(
        IReadOnlyList<string> ddsFiles, 
        string modBasePath,
        ConcurrentBag<ModScanIssue> issues, 
        CancellationToken cancellationToken)
    {
        var tasks = ddsFiles.Select(file => ProcessSingleDdsFileAsync(file, modBasePath, issues, cancellationToken));
        await Task.WhenAll(tasks).ConfigureAwait(false);
    }

    private async Task ProcessSingleDdsFileAsync(
        string ddsFilePath, 
        string modBasePath,
        ConcurrentBag<ModScanIssue> issues, 
        CancellationToken cancellationToken)
    {
        await _ddsReadSemaphore.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            var dimensions = await ReadDdsHeaderAsync(ddsFilePath, cancellationToken).ConfigureAwait(false);
            if (dimensions.HasValue)
            {
                var (width, height) = dimensions.Value;
                if (width % 2 != 0 || height % 2 != 0)
                {
                    var relativePath = Path.GetRelativePath(modBasePath, ddsFilePath);
                    issues.Add(ModScanIssue.CreateTextureDimensionIssue(relativePath, width, height));
                }
            }
        }
        finally
        {
            _ddsReadSemaphore.Release();
        }
    }

    private async Task<(int Width, int Height)?> ReadDdsHeaderAsync(string filePath, CancellationToken cancellationToken)
    {
        try
        {
            using var fileStream = new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.Read, 4096, FileOptions.SequentialScan);
            
            if (fileStream.Length < 20)
                return null;

            var buffer = new byte[20];
            await fileStream.ReadExactlyAsync(buffer, cancellationToken).ConfigureAwait(false);

            // Check DDS signature
            if (buffer[0] != 0x44 || buffer[1] != 0x44 || buffer[2] != 0x53 || buffer[3] != 0x20)
                return null;

            // Read width and height (little-endian)
            var width = BitConverter.ToInt32(buffer, 12);
            var height = BitConverter.ToInt32(buffer, 16);

            return (width, height);
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "Failed to read DDS header from: {FilePath}", filePath);
            return null;
        }
    }

    private async Task ProcessBa2FileAsync(
        string ba2FilePath,
        string bsArchPath,
        string xseAcronym,
        IReadOnlySet<string> xseScriptFiles,
        ConcurrentBag<ModScanIssue> issues,
        CancellationToken cancellationToken)
    {
        await _processingSemaphore.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            var fileName = Path.GetFileName(ba2FilePath);
            
            // Read BA2 header
            var headerValid = await ValidateBa2HeaderAsync(ba2FilePath, cancellationToken).ConfigureAwait(false);
            if (!headerValid.IsValid)
            {
                issues.Add(ModScanIssue.CreateFormatIssue(
                    ModIssueType.Ba2Format,
                    fileName,
                    "BTDX-GNRL or BTDX-DX10",
                    headerValid.ActualFormat ?? "Unknown"));
                return;
            }

            // Process based on BA2 type
            if (headerValid.IsTexture)
            {
                await ProcessTextureBa2Async(ba2FilePath, bsArchPath, fileName, issues, cancellationToken).ConfigureAwait(false);
            }
            else
            {
                await ProcessGeneralBa2Async(ba2FilePath, bsArchPath, fileName, xseAcronym, xseScriptFiles, issues, cancellationToken).ConfigureAwait(false);
            }
        }
        finally
        {
            _processingSemaphore.Release();
        }
    }

    private async Task<(bool IsValid, bool IsTexture, string? ActualFormat)> ValidateBa2HeaderAsync(string filePath, CancellationToken cancellationToken)
    {
        try
        {
            using var fileStream = new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.Read);
            
            if (fileStream.Length < 12)
                return (false, false, "Too small");

            var buffer = new byte[12];
            await fileStream.ReadExactlyAsync(buffer, cancellationToken).ConfigureAwait(false);

            // Check BTDX signature
            if (buffer[0] != 0x42 || buffer[1] != 0x54 || buffer[2] != 0x44 || buffer[3] != 0x58)
                return (false, false, Encoding.ASCII.GetString(buffer, 0, 4));

            // Check format type
            var formatBytes = buffer.AsSpan(8, 4);
            if (formatBytes.SequenceEqual("DX10"u8))
                return (true, true, "BTDX-DX10");
            else if (formatBytes.SequenceEqual("GNRL"u8))
                return (true, false, "BTDX-GNRL");

            return (false, false, Encoding.ASCII.GetString(formatBytes));
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "Failed to validate BA2 header: {FilePath}", filePath);
            return (false, false, "Read Error");
        }
    }

    private async Task ProcessTextureBa2Async(
        string ba2FilePath,
        string bsArchPath,
        string fileName,
        ConcurrentBag<ModScanIssue> issues,
        CancellationToken cancellationToken)
    {
        try
        {
            using var process = new Process();
            process.StartInfo = new ProcessStartInfo
            {
                FileName = bsArchPath,
                Arguments = $"\"{ba2FilePath}\" -dump",
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                CreateNoWindow = true
            };

            process.Start();
            
            var outputTask = process.StandardOutput.ReadToEndAsync(cancellationToken);
            var errorTask = process.StandardError.ReadToEndAsync(cancellationToken);
            
            await process.WaitForExitAsync(cancellationToken).ConfigureAwait(false);
            
            var output = await outputTask.ConfigureAwait(false);
            var error = await errorTask.ConfigureAwait(false);

            if (process.ExitCode != 0)
            {
                _logger.LogWarning("BSArch failed for {FileName}: {Error}", fileName, error);
                return;
            }

            // Process texture information
            ProcessTextureDumpOutput(output, fileName, issues);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to process texture BA2: {FileName}", fileName);
        }
    }

    private void ProcessTextureDumpOutput(string output, string fileName, ConcurrentBag<ModScanIssue> issues)
    {
        var blocks = output.Split("\n\n", StringSplitOptions.RemoveEmptyEntries);
        
        foreach (var block in blocks.Skip(4)) // Skip header blocks
        {
            if (string.IsNullOrWhiteSpace(block))
                continue;

            var lines = block.Split('\n', StringSplitOptions.RemoveEmptyEntries);
            if (lines.Length < 3)
                continue;

            var fileLine = lines[0];
            var extLine = lines[1];
            var sizeLine = lines[2];

            // Check texture format
            if (!extLine.Contains("Ext: dds", StringComparison.OrdinalIgnoreCase))
            {
                var actualExt = Regex.Match(extLine, @"Ext: (\w+)", RegexOptions.IgnoreCase).Groups[1].Value;
                issues.Add(ModScanIssue.CreateFormatIssue(
                    ModIssueType.TextureFormat,
                    $"{fileName} > {fileLine}",
                    "DDS",
                    actualExt.ToUpperInvariant()));
                continue;
            }

            // Check texture dimensions
            var dimensionMatch = Regex.Match(sizeLine, @"(\d+)\s+\w+\s+(\d+)");
            if (dimensionMatch.Success)
            {
                if (int.TryParse(dimensionMatch.Groups[1].Value, out var width) &&
                    int.TryParse(dimensionMatch.Groups[2].Value, out var height))
                {
                    if (width % 2 != 0 || height % 2 != 0)
                    {
                        issues.Add(ModScanIssue.CreateTextureDimensionIssue($"{fileName} > {fileLine}", width, height));
                    }
                }
            }
        }
    }

    private async Task ProcessGeneralBa2Async(
        string ba2FilePath,
        string bsArchPath,
        string fileName,
        string xseAcronym,
        IReadOnlySet<string> xseScriptFiles,
        ConcurrentBag<ModScanIssue> issues,
        CancellationToken cancellationToken)
    {
        try
        {
            using var process = new Process();
            process.StartInfo = new ProcessStartInfo
            {
                FileName = bsArchPath,
                Arguments = $"\"{ba2FilePath}\" -list",
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                CreateNoWindow = true
            };

            process.Start();
            
            var outputTask = process.StandardOutput.ReadToEndAsync(cancellationToken);
            var errorTask = process.StandardError.ReadToEndAsync(cancellationToken);
            
            await process.WaitForExitAsync(cancellationToken).ConfigureAwait(false);
            
            var output = await outputTask.ConfigureAwait(false);
            var error = await errorTask.ConfigureAwait(false);

            if (process.ExitCode != 0)
            {
                _logger.LogWarning("BSArch failed for {FileName}: {Error}", fileName, error);
                return;
            }

            // Process file list
            ProcessGeneralBa2Output(output, fileName, xseAcronym, xseScriptFiles, issues);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to process general BA2: {FileName}", fileName);
        }
    }

    private void ProcessGeneralBa2Output(
        string output, 
        string fileName, 
        string xseAcronym, 
        IReadOnlySet<string> xseScriptFiles,
        ConcurrentBag<ModScanIssue> issues)
    {
        var lines = output.Split('\n', StringSplitOptions.RemoveEmptyEntries).Skip(15); // Skip header lines
        var hasPrevisFiles = false;
        var hasAnimData = false;
        var hasXseFiles = false;

        foreach (var line in lines)
        {
            var filePath = line.Trim().ToLowerInvariant();
            
            // Check sound formats
            if (filePath.EndsWith(".mp3") || filePath.EndsWith(".m4a"))
            {
                var ext = Path.GetExtension(filePath)[1..].ToUpperInvariant();
                issues.Add(ModScanIssue.CreateFormatIssue(
                    ModIssueType.SoundFormat,
                    $"{fileName} > {line.Trim()}",
                    "XWM or WAV",
                    ext));
            }

            // Check animation data
            else if (!hasAnimData && filePath.Contains("animationfiledata"))
            {
                hasAnimData = true;
                issues.Add(ModScanIssue.CreateCustomIssue(
                    ModIssueType.AnimationData,
                    "Contains custom animation file data",
                    fileName));
            }

            // Check XSE files
            else if (!hasXseFiles && 
                     xseScriptFiles.Any(script => filePath.Contains($"scripts\\{script.ToLowerInvariant()}")) &&
                     !filePath.Contains("workshop framework"))
            {
                hasXseFiles = true;
                issues.Add(ModScanIssue.CreateCustomIssue(
                    ModIssueType.XseFiles,
                    $"Contains copies of {xseAcronym} script files",
                    fileName));
            }

            // Check previs files
            else if (!hasPrevisFiles && (filePath.EndsWith(".uvd") || filePath.EndsWith("_oc.nif")))
            {
                hasPrevisFiles = true;
                issues.Add(ModScanIssue.CreateCustomIssue(
                    ModIssueType.PrevisFiles,
                    "Contains custom precombine/previs files",
                    fileName));
            }
        }
    }

    private async Task ProcessLogFileAsync(
        string logFilePath,
        IReadOnlySet<string> errorPatterns,
        IReadOnlySet<string> excludeErrors,
        ConcurrentBag<ModScanIssue> issues,
        CancellationToken cancellationToken)
    {
        try
        {
            using var reader = new StreamReader(logFilePath, Encoding.UTF8);
            var detectedErrors = new List<string>();
            
            string? line;
            while ((line = await reader.ReadLineAsync(cancellationToken).ConfigureAwait(false)) != null)
            {
                var lowerLine = line.ToLowerInvariant();
                
                if (errorPatterns.Any(pattern => lowerLine.Contains(pattern)) &&
                    !excludeErrors.Any(exclude => lowerLine.Contains(exclude)))
                {
                    detectedErrors.Add($"ERROR > {line}");
                }

                cancellationToken.ThrowIfCancellationRequested();
            }

            if (detectedErrors.Count > 0)
            {
                var fileName = Path.GetFileName(logFilePath);
                var errorDetails = string.Join("\n", detectedErrors);
                issues.Add(ModScanIssue.CreateCustomIssue(
                    ModIssueType.Cleanup, // Using Cleanup type for log errors for now
                    $"Log file contains {detectedErrors.Count} errors",
                    fileName,
                    errorDetails));
            }
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "Failed to process log file: {LogFilePath}", logFilePath);
        }
    }

    private static bool IsDocumentationFile(string fileName)
    {
        var lowerFileName = fileName.ToLowerInvariant();
        
        return DocumentationExtensions.Any(ext => lowerFileName.EndsWith(ext)) &&
               DocumentationKeywords.Any(keyword => lowerFileName.Contains(keyword));
    }

    private async Task<string> FormatScanReportAsync(string scanType, ConcurrentBag<ModScanIssue> issues, string? errorMessage = null)
    {
        var report = new StringBuilder();
        
        report.AppendLine($"=================== MOD FILES SCAN ====================");
        report.AppendLine($"========= RESULTS FROM {scanType.ToUpperInvariant()} FILES =========");
        
        if (!string.IsNullOrEmpty(errorMessage))
        {
            report.AppendLine($"❌ ERROR: {errorMessage}");
            return report.ToString();
        }

        var issueGroups = issues.GroupBy(i => i.IssueType).ToDictionary(g => g.Key, g => g.ToList());
        
        await Task.CompletedTask.ConfigureAwait(false);

        // Add issue messages based on type
        await AddIssueMessagesAsync(report, issueGroups, scanType == "unpacked").ConfigureAwait(false);

        if (issues.Count == 0)
        {
            report.AppendLine("✅ No issues detected in mod files.");
        }

        return report.ToString();
    }

    private async Task AddIssueMessagesAsync(StringBuilder report, Dictionary<ModIssueType, List<ModScanIssue>> issueGroups, bool isUnpacked)
    {
        await Task.CompletedTask.ConfigureAwait(false);

        // DDS dimension issues
        if (issueGroups.TryGetValue(ModIssueType.TextureDimensions, out var texDimIssues))
        {
            report.AppendLine("\n# ⚠️ DDS DIMENSIONS ARE NOT DIVISIBLE BY 2 ⚠️");
            report.AppendLine("▶️ Any mods that have texture files with incorrect dimensions");
            report.AppendLine("  are very likely to cause a *Texture (DDS) Crash*.");
            
            foreach (var issue in texDimIssues.Take(10)) // Limit output
            {
                report.AppendLine($"  - {issue.FilePath}: {issue.Details}");
            }
            
            if (texDimIssues.Count > 10)
                report.AppendLine($"  ... and {texDimIssues.Count - 10} more");
        }

        // Texture format issues
        if (issueGroups.TryGetValue(ModIssueType.TextureFormat, out var texFmtIssues))
        {
            report.AppendLine("\n# ❓ TEXTURE FILES HAVE INCORRECT FORMAT, SHOULD BE DDS ❓");
            report.AppendLine("▶️ Any files with an incorrect file format will not work.");
            
            foreach (var issue in texFmtIssues.Take(10))
            {
                report.AppendLine($"  - {issue.Details}: {issue.FilePath}");
            }
        }

        // Sound format issues
        if (issueGroups.TryGetValue(ModIssueType.SoundFormat, out var sndFmtIssues))
        {
            report.AppendLine("\n# ❓ SOUND FILES HAVE INCORRECT FORMAT, SHOULD BE XWM OR WAV ❓");
            report.AppendLine("▶️ Any files with an incorrect file format will not work.");
            
            foreach (var issue in sndFmtIssues.Take(10))
            {
                report.AppendLine($"  - {issue.Details}: {issue.FilePath}");
            }
        }

        // XSE file issues
        if (issueGroups.TryGetValue(ModIssueType.XseFiles, out var xseIssues))
        {
            var containerType = isUnpacked ? "FOLDERS" : "BA2 ARCHIVES";
            report.AppendLine($"\n# ⚠️ {containerType} CONTAIN COPIES OF SCRIPT EXTENDER FILES ⚠️");
            report.AppendLine("▶️ Any mods with copies of original Script Extender files");
            report.AppendLine("  may cause script related problems or crashes.");
            
            foreach (var issue in xseIssues)
            {
                report.AppendLine($"  - {issue.FilePath}");
            }
        }

        // Previs issues
        if (issueGroups.TryGetValue(ModIssueType.PrevisFiles, out var previsIssues))
        {
            var containerType = isUnpacked ? "FOLDERS" : "BA2 ARCHIVES";
            report.AppendLine($"\n# ⚠️ {containerType} CONTAIN LOOSE PRECOMBINE / PREVIS FILES ⚠️");
            report.AppendLine("▶️ Any mods that contain custom precombine/previs files");
            report.AppendLine("  should load after the PRP.esp plugin from Previs Repair Pack (PRP).");
            
            foreach (var issue in previsIssues)
            {
                report.AppendLine($"  - {issue.FilePath}");
            }
        }

        // Animation data issues
        if (issueGroups.TryGetValue(ModIssueType.AnimationData, out var animIssues))
        {
            var containerType = isUnpacked ? "FOLDERS" : "BA2 ARCHIVES";
            report.AppendLine($"\n# ❓ {containerType} CONTAIN CUSTOM ANIMATION FILE DATA ❓");
            report.AppendLine("▶️ Any mods that have their own custom Animation File Data");
            report.AppendLine("  may rarely cause an *Animation Corruption Crash*.");
            
            foreach (var issue in animIssues)
            {
                report.AppendLine($"  - {issue.FilePath}");
            }
        }

        // BA2 format issues
        if (issueGroups.TryGetValue(ModIssueType.Ba2Format, out var ba2Issues))
        {
            report.AppendLine("\n# ❓ BA2 ARCHIVES HAVE INCORRECT FORMAT, SHOULD BE BTDX-GNRL OR BTDX-DX10 ❓");
            report.AppendLine("▶️ Any files with an incorrect file format will not work.");
            
            foreach (var issue in ba2Issues)
            {
                report.AppendLine($"  - {issue.FilePath}: {issue.Details}");
            }
        }

        // Cleanup issues
        if (issueGroups.TryGetValue(ModIssueType.Cleanup, out var cleanupIssues))
        {
            report.AppendLine("\n# 📄 DOCUMENTATION FILES MOVED TO 'CLASSIC Backup\\Cleaned Files' 📄");
            
            foreach (var issue in cleanupIssues.Take(20)) // Limit cleanup output
            {
                report.AppendLine($"  - {issue.FilePath}");
            }
        }
    }

    #endregion

    public async ValueTask DisposeAsync()
    {
        if (_disposed) return;

        _processingSemaphore?.Dispose();
        _fileOpsSemaphore?.Dispose();
        _ddsReadSemaphore?.Dispose();

        _disposed = true;
        await Task.CompletedTask.ConfigureAwait(false);
    }
}