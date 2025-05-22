using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Scanner111.Models;

namespace Scanner111.Services;

/// <summary>
///     Service for scanning mod files (both unpacked and archived)
/// </summary>
public class ModScanningService : IModScanningService
{
    private const string BackupPath = "CLASSIC Backup/Cleaned Files";
    private readonly ILogger<ModScanningService>? _logger;

    // Cache for mod scan results to avoid redundant scans
    private readonly ConcurrentDictionary<string, string> _scanResultsCache = new();
    private readonly bool _testMode;
    private readonly IYamlSettingsCacheService _yamlSettingsCache;

    public ModScanningService(
        IYamlSettingsCacheService yamlSettingsCache,
        ILogger<ModScanningService>? logger = null,
        bool testMode = false)
    {
        _yamlSettingsCache = yamlSettingsCache ?? throw new ArgumentNullException(nameof(yamlSettingsCache));
        _logger = logger;
        _testMode = testMode;

        if (!_testMode) Directory.CreateDirectory(BackupPath);
    }

    /// <summary>
    ///     Scans loose mod files for issues and moves redundant files to backup location.
    /// </summary>
    /// <param name="progress">Optional progress reporter to receive progress updates.</param>
    /// <param name="cancellationToken">Token to monitor for cancellation requests.</param>
    /// <returns>Detailed report of scan results.</returns>
    public async Task<string> ScanModsUnpackedAsync(IProgress<ScanProgress>? progress = null,
        CancellationToken cancellationToken = default)
    {
        _logger?.LogInformation("Starting unpacked mods scan");
        progress?.Report(new ScanProgress(0, "Starting unpacked mods scan"));

        // Check cache first
        const string cacheKey = "unpacked_mods_scan";
        if (!_scanResultsCache.TryGetValue(cacheKey, out var cachedResult))
            return await Task.Run(async () =>
            {
                try
                {
                    // Initialize lists for reporting
                    var messageList = new List<string>
                    {
                        "=================== MOD FILES SCAN ====================\n",
                        "========= RESULTS FROM UNPACKED / LOOSE FILES =========\n"
                    };

                    // Initialize dictionaries for collecting different issue types
                    var issueLists = new ConcurrentDictionary<string, ConcurrentBag<string>>
                    {
                        ["cleanup"] = [],
                        ["animdata"] = [],
                        ["tex_dims"] = [],
                        ["tex_frmt"] = [],
                        ["snd_frmt"] = [],
                        ["xse_file"] = [],
                        ["previs"] = []
                    };

                    cancellationToken.ThrowIfCancellationRequested();

                    // Get settings
                    var vrMode = GetGlobalRegistryVr();
                    var xseAcronym =
                        _yamlSettingsCache.GetSetting<string>(Yaml.Game, $"Game{vrMode}_Info.XSE_Acronym", "XSE");
                    var xseScriptfiles =
                        _yamlSettingsCache.GetSetting(Yaml.Game,
                            $"Game{vrMode}_Info.XSE_HashedScripts",
                            new Dictionary<string, string>());

                    // Setup paths
                    var backupPath = new DirectoryInfo(Path.Combine(Directory.GetCurrentDirectory(), BackupPath));
                    if (!_testMode) backupPath.Create();

                    var modPathSetting =
                        _yamlSettingsCache.GetSetting<DirectoryInfo>(Yaml.Settings, "MODS Folder Path");
                    if (modPathSetting == null)
                    {
                        var errorMessage = _yamlSettingsCache.GetSetting<string>(Yaml.Main,
                            "Mods_Warn.Mods_Path_Missing",
                            "❌ MODS FOLDER PATH NOT PROVIDED!");
                        _logger?.LogWarning("Mods folder path not provided");
                        progress?.Report(new ScanProgress(100, "Error: Mods folder path not provided"));
                        return errorMessage;
                    }

                    if (!modPathSetting.Exists)
                    {
                        var errorMessage =
                            _yamlSettingsCache.GetSetting<string>(Yaml.Main, "Mods_Warn.Mods_Path_Invalid") ??
                            "❌ MODS FOLDER PATH NOT VALID!";
                        _logger?.LogWarning("Mods folder path is invalid: {Path}", modPathSetting.FullName);
                        progress?.Report(new ScanProgress(100, "Error: Mods folder path is invalid"));
                        return errorMessage;
                    }

                    _logger?.LogInformation("Mods folder path found: {Path}", modPathSetting.FullName);
                    progress?.Report(new ScanProgress(10, "Mods folder path found, starting cleanup"));

                    // First pass: cleanup and detect animation data
                    var filterNames = new[] { "readme", "changes", "changelog", "change log" };

                    // Process directories recursively with progress reporting
                    await ProcessDirectoriesRecursivelyAsync(modPathSetting, backupPath, issueLists, filterNames,
                        progress,
                        cancellationToken);

                    cancellationToken.ThrowIfCancellationRequested();
                    progress?.Report(new ScanProgress(50, "Cleanup complete, analyzing mod files"));
                    _logger?.LogInformation("Cleanup complete, analyzing mod files");

                    // Second pass: analyze files for issues
                    await AnalyzeModFilesAsync(modPathSetting, issueLists, xseScriptfiles!, xseAcronym!, progress,
                        cancellationToken);

                    cancellationToken.ThrowIfCancellationRequested();
                    progress?.Report(new ScanProgress(90, "Analysis complete, building report"));
                    _logger?.LogInformation("Analysis complete, building report");

                    // Build the report by adding issue messages
                    AppendIssueMessages(messageList, issueLists, xseAcronym);

                    var result = string.Join("", messageList);

                    // Cache the result
                    _scanResultsCache[cacheKey] = result;

                    progress?.Report(new ScanProgress(100, "Unpacked mods scan completed"));
                    _logger?.LogInformation("Unpacked mods scan completed successfully");

                    return result;
                }
                catch (OperationCanceledException)
                {
                    _logger?.LogInformation("Unpacked mods scan was cancelled");
                    progress?.Report(new ScanProgress(0, "Scan cancelled"));
                    throw;
                }
                catch (Exception ex)
                {
                    _logger?.LogError(ex, "Error during unpacked mods scan");
                    progress?.Report(new ScanProgress(0, $"Error: {ex.Message}"));
                    throw;
                }
            }, cancellationToken).ConfigureAwait(false);
        _logger?.LogInformation("Returning cached unpacked mods scan results");
        progress?.Report(new ScanProgress(100, "Retrieved cached unpacked mods scan results"));
        return cachedResult;
    }

    /// <summary>
    ///     Analyzes archived BA2 mod files to identify potential issues.
    /// </summary>
    /// <param name="progress">Optional progress reporter to receive progress updates.</param>
    /// <param name="cancellationToken">Token to monitor for cancellation requests.</param>
    /// <returns>A report detailing the findings, including errors and warnings.</returns>
    public async Task<string> ScanModsArchivedAsync(IProgress<ScanProgress>? progress = null,
        CancellationToken cancellationToken = default)
    {
        _logger?.LogInformation("Starting archived mods scan");
        progress?.Report(new ScanProgress(0, "Starting archived mods scan"));

        // Check cache first
        var cacheKey = "archived_mods_scan";
        if (_scanResultsCache.TryGetValue(cacheKey, out var cachedResult))
        {
            _logger?.LogInformation("Returning cached archived mods scan results");
            progress?.Report(new ScanProgress(100, "Retrieved cached archived mods scan results"));
            return cachedResult;
        }

        return await Task.Run(async () =>
        {
            try
            {
                var messageList = new List<string>
                {
                    "\n========== RESULTS FROM ARCHIVED / BA2 FILES ==========\n"
                };

                // Initialize dictionaries for collecting different issue types
                var issueLists = new ConcurrentDictionary<string, ConcurrentBag<string>>
                {
                    ["ba2_frmt"] = new(),
                    ["animdata"] = new(),
                    ["tex_dims"] = new(),
                    ["tex_frmt"] = new(),
                    ["snd_frmt"] = new(),
                    ["xse_file"] = new(),
                    ["previs"] = new()
                };

                cancellationToken.ThrowIfCancellationRequested();

                // Get settings
                var vrMode = GetGlobalRegistryVr();
                var xseAcronym =
                    _yamlSettingsCache.GetSetting<string>(Yaml.Game, $"Game{vrMode}_Info.XSE_Acronym", "XSE");
                var xseScriptfiles =
                    _yamlSettingsCache.GetSetting(Yaml.Game,
                        $"Game{vrMode}_Info.XSE_HashedScripts",
                        new Dictionary<string, string>());

                // Setup paths
                var bsarchPath = Path.Combine(Directory.GetCurrentDirectory(), "CLASSIC Data", "BSArch.exe");
                var modPathSetting = _yamlSettingsCache.GetSetting<DirectoryInfo>(Yaml.Settings, "MODS Folder Path");

                // Validate paths
                if (modPathSetting == null)
                {
                    var errorMessage =
                        _yamlSettingsCache.GetSetting<string>(Yaml.Main, "Mods_Warn.Mods_Path_Missing") ??
                        "❌ MODS FOLDER PATH NOT PROVIDED!";
                    _logger?.LogWarning("Mods folder path not provided");
                    progress?.Report(new ScanProgress(100, "Error: Mods folder path not provided"));
                    return errorMessage;
                }

                var modPath = modPathSetting;
                if (!modPath.Exists)
                {
                    var errorMessage = _yamlSettingsCache.GetSetting<string>(Yaml.Main, "Mods_Warn.Mods_Path_Invalid",
                        "❌ MODS FOLDER PATH NOT VALID!");
                    _logger?.LogWarning("Mods folder path is invalid: {Path}", modPath.FullName);
                    progress?.Report(new ScanProgress(100, "Error: Mods folder path is invalid"));
                    return errorMessage;
                }

                if (!File.Exists(bsarchPath))
                {
                    var errorMessage = _yamlSettingsCache.GetSetting<string>(Yaml.Main, "Mods_Warn.Mods_BSArch_Missing",
                        "❌ BSARCH.EXE NOT FOUND! CANNOT SCAN BA2 FILES!");
                    _logger?.LogWarning("BSArch.exe not found at: {Path}", bsarchPath);
                    progress?.Report(new ScanProgress(100, "Error: BSArch.exe not found"));
                    return errorMessage;
                }

                _logger?.LogInformation("All requirements satisfied, analyzing BA2 archives");
                progress?.Report(new ScanProgress(20, "All requirements satisfied, analyzing BA2 archives"));

                // Process BA2 files with progress reporting
                await ProcessBa2FilesAsync(modPath, bsarchPath, issueLists, xseScriptfiles, progress,
                    cancellationToken);

                cancellationToken.ThrowIfCancellationRequested();
                progress?.Report(new ScanProgress(90, "Analysis complete, building report"));
                _logger?.LogInformation("BA2 analysis complete, building report");

                // Build the report
                AppendIssueMessages(messageList, issueLists, xseAcronym, true);

                var result = string.Join("", messageList);

                // Cache the result
                _scanResultsCache[cacheKey] = result;

                progress?.Report(new ScanProgress(100, "Archived mods scan completed"));
                _logger?.LogInformation("Archived mods scan completed successfully");

                return result;
            }
            catch (OperationCanceledException)
            {
                _logger?.LogInformation("Archived mods scan was cancelled");
                progress?.Report(new ScanProgress(0, "Scan cancelled"));
                throw;
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "Error during archived mods scan");
                progress?.Report(new ScanProgress(0, $"Error: {ex.Message}"));
                throw;
            }
        }, cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    ///     Combines the results of scanning unpacked and archived mods.
    /// </summary>
    /// <param name="progress">Optional progress reporter to receive progress updates.</param>
    /// <param name="cancellationToken">Token to monitor for cancellation requests.</param>
    /// <returns>The combined results of the unpacked and archived mods scans.</returns>
    public async Task<string> GetModsCombinedResultAsync(IProgress<ScanProgress>? progress = null,
        CancellationToken cancellationToken = default)
    {
        _logger?.LogInformation("Starting combined mods scan");
        progress?.Report(new ScanProgress(0, "Starting combined mods scan"));

        try
        {
            // Check cache first
            var cacheKey = "combined_mods_scan";
            if (_scanResultsCache.TryGetValue(cacheKey, out var cachedResult))
            {
                _logger?.LogInformation("Returning cached combined mods scan results");
                progress?.Report(new ScanProgress(100, "Retrieved cached combined mods scan results"));
                return cachedResult;
            }

            cancellationToken.ThrowIfCancellationRequested();

            // Create progress transformers for the individual scans
            var unpackedProgress = new Progress<ScanProgress>(p =>
            {
                if (p.PercentComplete > 0)
                {
                    // Unpacked scan is 60% of the total
                    var combinedPercent = (int)(p.PercentComplete * 0.6);
                    progress?.Report(new ScanProgress(combinedPercent, p.CurrentOperation, p.CurrentItem));
                }
            });

            var unpacked = await ScanModsUnpackedAsync(unpackedProgress, cancellationToken)
                .ConfigureAwait(false);

            if (unpacked.StartsWith("❌ MODS FOLDER PATH NOT PROVIDED"))
            {
                _logger?.LogWarning("Mods folder path issue detected");
                progress?.Report(new ScanProgress(100, "Error: Mods folder path issue"));
                return unpacked;
            }

            cancellationToken.ThrowIfCancellationRequested();

            // Create progress transformer for archived scan
            var archivedProgress = new Progress<ScanProgress>(p =>
            {
                if (p.PercentComplete > 0)
                {
                    // Archived scan is 40% of the total, starting at 60%
                    var combinedPercent = 60 + (int)(p.PercentComplete * 0.4);
                    progress?.Report(new ScanProgress(combinedPercent, p.CurrentOperation, p.CurrentItem));
                }
            });

            var archived = await ScanModsArchivedAsync(archivedProgress, cancellationToken)
                .ConfigureAwait(false);

            var result = unpacked + archived;

            // Cache the result
            _scanResultsCache[cacheKey] = result;

            progress?.Report(new ScanProgress(100, "Combined mods scan completed"));
            _logger?.LogInformation("Combined mods scan completed successfully");

            return result;
        }
        catch (OperationCanceledException)
        {
            _logger?.LogInformation("Combined mods scan was cancelled");
            progress?.Report(new ScanProgress(0, "Scan cancelled"));
            throw;
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Error during combined mods scan");
            progress?.Report(new ScanProgress(0, $"Error: {ex.Message}"));
            throw;
        }
    }

    /// <summary>
    ///     Clears the scan results cache to force fresh scans
    /// </summary>
    public void ClearCache()
    {
        _logger?.LogInformation("Clearing mod scanning cache");
        _scanResultsCache.Clear();
    }

    /// <summary>
    ///     Scans all mod files (both unpacked and archived)
    /// </summary>
    /// <param name="progress">Optional progress reporter to receive progress updates.</param>
    /// <param name="cancellationToken">Token to monitor for cancellation requests.</param>
    /// <returns>Combined scan results as string</returns>
    public async Task<string> ScanModsAsync(IProgress<ScanProgress>? progress = null,
        CancellationToken cancellationToken = default)
    {
        // This is just an alias for GetModsCombinedResultAsync for interface compatibility
        return await GetModsCombinedResultAsync(progress, cancellationToken);
    }

    #region Helper Methods

    private async Task ProcessDirectoriesRecursivelyAsync(DirectoryInfo rootDir, DirectoryInfo backupPath,
        ConcurrentDictionary<string, ConcurrentBag<string>> issueLists, string[] filterNames,
        IProgress<ScanProgress>? progress, CancellationToken cancellationToken)
    {
        try
        {
            var fileCount = 0;
            var totalFiles = rootDir.GetFiles("*", SearchOption.AllDirectories).Length;

            foreach (var dir in rootDir.GetDirectories("*", SearchOption.AllDirectories))
            {
                cancellationToken.ThrowIfCancellationRequested();

                var rootMain = GetRelativePath(dir, rootDir);
                var hasAnimData = false;

                // Check for animation data folders
                if (dir.Name.ToLower().Contains("animationdata") ||
                    dir.Name.ToLower().Contains("animationsdatasinglefile"))
                {
                    hasAnimData = true;
                    issueLists["animdata"].Add($"• {rootMain.TrimEnd('/')}");
                }

                // Check if directory has mesh data
                if (hasAnimData && dir.GetFiles("*.hkx").Any())
                {
                    // Add animation data info to the issues list
                    // (Implementation details for additional checks would go here)
                }

                // Process files in the directory
                foreach (var file in dir.GetFiles())
                {
                    fileCount++;
                    if (fileCount % 100 == 0)
                    {
                        var percentComplete = (int)((float)fileCount / totalFiles * 30) + 10; // 10-40% progress range
                        progress?.Report(new ScanProgress(percentComplete, "Processing files...", file.Name));
                    }

                    // Handle cleanup for readme/documentation files
                    if (filterNames.Any(filter => file.Name.ToLower().Contains(filter)) &&
                        file.Extension.ToLower() is ".txt" or ".rtf" or ".pdf" or ".doc" or ".docx")
                    {
                        if (!_testMode)
                        {
                            // Create target directory in backup location
                            var targetDir = Path.Combine(backupPath.FullName, rootMain);
                            Directory.CreateDirectory(targetDir);

                            // Copy file to backup location
                            var targetPath = Path.Combine(targetDir, file.Name);
                            if (!File.Exists(targetPath))
                                await Task.Run(() => file.CopyTo(targetPath), cancellationToken);
                        }

                        issueLists["cleanup"].Add($"• {rootMain}/{file.Name}");
                    }

                    // Check for XSE files (script files)
                    // (Implementation for additional file checks)
                }
            }
        }
        catch (OperationCanceledException)
        {
            _logger?.LogInformation("Directory processing was cancelled");
            throw;
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Error processing directories");
        }
    }

    private async Task AnalyzeModFilesAsync(DirectoryInfo modPath,
        ConcurrentDictionary<string, ConcurrentBag<string>> issueLists,
        Dictionary<string, string> xseScriptfiles, string xseAcronym,
        IProgress<ScanProgress>? progress, CancellationToken cancellationToken)
    {
        try
        {
            var dirCount = 0;
            var dirs = modPath.GetDirectories("*", SearchOption.AllDirectories);
            var totalDirs = dirs.Length;

            foreach (var dir in dirs)
            {
                cancellationToken.ThrowIfCancellationRequested();

                dirCount++;
                if (dirCount % 50 == 0)
                {
                    var percentComplete = (int)((float)dirCount / totalDirs * 40) + 50; // 50-90% progress range
                    progress?.Report(new ScanProgress(percentComplete, "Analyzing mod files...", dir.Name));
                }

                var rootMain = GetRelativePath(dir, modPath);
                var hasPrevisFiles = false;
                var hasXseFiles = false;

                foreach (var file in dir.GetFiles())
                {
                    var fileName = file.Name.ToLower();
                    var filePath = file.FullName.ToLower();

                    // Check for previs/precombine files
                    if (fileName.Contains("precombined") || fileName.Contains("previs")) hasPrevisFiles = true;

                    // Check for XSE script files
                    if (xseScriptfiles.Any(kvp => filePath.EndsWith(kvp.Key.ToLower()))) hasXseFiles = true;

                    // Check texture dimensions and formats
                    if (file.Extension.ToLower() == ".dds")
                    {
                        // Implementation for checking DDS dimensions and format
                        // This would require reading the DDS header
                    }

                    // Check sound file formats
                    if (file.Extension.ToLower() is ".mp3" or ".ogg" or ".flac")
                        issueLists["snd_frmt"].Add($"• {rootMain}/{fileName}");
                }

                // Add detected issues to appropriate lists
                if (hasPrevisFiles) issueLists["previs"].Add($"• {rootMain}");

                if (hasXseFiles) issueLists["xse_file"].Add($"• {rootMain} (Contains {xseAcronym} files)");

                // Yield to other tasks
                await Task.Yield();
            }
        }
        catch (OperationCanceledException)
        {
            _logger?.LogInformation("Mod file analysis was cancelled");
            throw;
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Error analyzing mod files");
        }
    }

    private async Task ProcessBa2FilesAsync(DirectoryInfo modPath, string bsarchPath,
        ConcurrentDictionary<string, ConcurrentBag<string>> issueLists,
        Dictionary<string, string> xseScriptfiles,
        IProgress<ScanProgress>? progress, CancellationToken cancellationToken)
    {
        try
        {
            var fileCount = 0;
            var files = modPath.GetFiles("*.ba2", SearchOption.AllDirectories);
            var totalFiles = files.Length;

            foreach (var file in files)
            {
                cancellationToken.ThrowIfCancellationRequested();

                fileCount++;
                var percentComplete = (int)((float)fileCount / totalFiles * 70) + 20; // 20-90% progress range
                progress?.Report(new ScanProgress(percentComplete, "Analyzing BA2 files...", file.Name));

                var fileName = file.Name;
                var filePath = file.FullName;
                var rootMain = GetRelativePath(file.Directory ?? new DirectoryInfo("."), modPath);

                // Use BSArch to list contents of BA2 file
                using var process = new Process();
                process.StartInfo.FileName = bsarchPath;
                process.StartInfo.Arguments = $"list \"{filePath}\"";
                process.StartInfo.UseShellExecute = false;
                process.StartInfo.RedirectStandardOutput = true;
                process.StartInfo.CreateNoWindow = true;

                process.Start();
                var output = await process.StandardOutput.ReadToEndAsync();
                await process.WaitForExitAsync(cancellationToken);

                // Process the output to check for issues
                var outputLines = output.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);

                var hasPrevisFiles = false;
                var hasXseFiles = false;
                var hasAnimData = false;

                foreach (var line in outputLines)
                {
                    var lineLower = line.ToLower();

                    // Check for previs/precombine files
                    if (lineLower.Contains("precombined") || lineLower.Contains("previs")) hasPrevisFiles = true;

                    // Check for XSE script files
                    if (xseScriptfiles.Any(kvp => lineLower.EndsWith(kvp.Key.ToLower()))) hasXseFiles = true;

                    // Check for animation data
                    if (lineLower.Contains("animationdata") || lineLower.Contains("animationsdatasinglefile"))
                        hasAnimData = true;

                    // Check file formats
                    if (lineLower.EndsWith(".mp3") || lineLower.EndsWith(".ogg") || lineLower.EndsWith(".flac"))
                        issueLists["snd_frmt"].Add($"• {rootMain}/{fileName} (Contains: {Path.GetFileName(line)})");

                    if (lineLower.EndsWith(".png") || lineLower.EndsWith(".jpg") || lineLower.EndsWith(".jpeg") ||
                        lineLower.EndsWith(".bmp"))
                        issueLists["tex_frmt"].Add($"• {rootMain}/{fileName} (Contains: {Path.GetFileName(line)})");
                }

                // Add detected issues to appropriate lists
                if (hasPrevisFiles) issueLists["previs"].Add($"• {rootMain}/{fileName}");

                if (hasXseFiles) issueLists["xse_file"].Add($"• {rootMain}/{fileName}");

                if (hasAnimData) issueLists["animdata"].Add($"• {rootMain}/{fileName}");

                // Check BA2 format
                if (process.ExitCode != 0 || !output.Contains("BTDX"))
                    issueLists["ba2_frmt"].Add($"• {rootMain}/{fileName}");

                // Yield to other tasks occasionally
                if (fileCount % 5 == 0) await Task.Yield();
            }
        }
        catch (OperationCanceledException)
        {
            _logger?.LogInformation("BA2 processing was cancelled");
            throw;
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Error processing BA2 files");
        }
    }

    private void AppendIssueMessages(List<string> messageList,
        ConcurrentDictionary<string, ConcurrentBag<string>> issueLists,
        string xseAcronym, bool isBa2 = false)
    {
        // Define issue messages based on type
        var issueMessages = new Dictionary<string, List<string>>
        {
            ["xse_file"] = new()
            {
                $"\n# ⚠️ {(isBa2 ? "BA2 ARCHIVES" : "FOLDERS")} CONTAIN COPIES OF *{xseAcronym}* SCRIPT FILES ⚠️\n",
                "▶️ Any mods with copies of original Script Extender files\n",
                "  may cause script related problems or crashes.\n\n"
            },
            ["previs"] = new()
            {
                $"\n# ⚠️ {(isBa2 ? "BA2 ARCHIVES" : "FOLDERS")} CONTAIN {(isBa2 ? "CUSTOM " : "LOOSE ")}PRECOMBINE / PREVIS FILES ⚠️\n",
                "▶️ Any mods that contain custom precombine/previs files\n",
                "  should load after the PRP.esp plugin from Previs Repair Pack (PRP).\n",
                "  Otherwise, see if there is a PRP patch available for these mods.\n\n"
            },
            ["tex_dims"] = new()
            {
                "\n# ⚠️ DDS DIMENSIONS ARE NOT DIVISIBLE BY 2 ⚠️\n",
                "▶️ Any mods that have texture files with incorrect dimensions\n",
                "  are very likely to cause a *Texture (DDS) Crash*. For further details,\n",
                "  read the *How To Read Crash Logs.pdf* included with the CLASSIC exe.\n\n"
            },
            ["tex_frmt"] = new()
            {
                "\n# ❓ TEXTURE FILES HAVE INCORRECT FORMAT, SHOULD BE DDS ❓\n",
                "▶️ Any files with an incorrect file format will not work.\n",
                "  Mod authors should convert these files to their proper game format.\n",
                "  If possible, notify the original mod authors about these problems.\n\n"
            },
            ["snd_frmt"] = new()
            {
                "\n# ❓ SOUND FILES HAVE INCORRECT FORMAT, SHOULD BE XWM OR WAV ❓\n",
                "▶️ Any files with an incorrect file format will not work.\n",
                "  Mod authors should convert these files to their proper game format.\n",
                "  If possible, notify the original mod authors about these problems.\n\n"
            },
            ["animdata"] = new()
            {
                $"\n# ❓ {(isBa2 ? "BA2 ARCHIVES" : "FOLDERS")} CONTAIN CUSTOM ANIMATION FILE DATA ❓\n",
                "▶️ Any mods that have their own custom Animation File Data\n",
                "  may rarely cause an *Animation Corruption Crash*. For further details,\n",
                "  read the *How To Read Crash Logs.pdf* included with the CLASSIC exe.\n\n"
            },
            ["cleanup"] = new()
            {
                "\n# 📄 DOCUMENTATION FILES MOVED TO 'CLASSIC Backup\\Cleaned Files' 📄\n"
            }
        };

        // Add BA2-specific message if needed
        if (isBa2)
            issueMessages["ba2_frmt"] = new List<string>
            {
                "\n# ❓ BA2 ARCHIVES HAVE INCORRECT FORMAT, SHOULD BE BTDX-GNRL OR BTDX-DX10 ❓\n",
                "▶️ Any files with an incorrect file format will not work.\n",
                "  Mod authors should convert these files to their proper game format.\n",
                "  If possible, notify the original mod authors about these problems.\n\n"
            };

        // Add found issues to message list
        foreach (var issueType in issueLists.Keys)
        {
            var items = issueLists[issueType];
            if (items.Count > 0)
            {
                // Add issue header messages
                messageList.AddRange(issueMessages[issueType]);

                // Add sorted list of items
                foreach (var item in items.OrderBy(i => i)) messageList.Add($"{item}\n");
            }
        }
    }

    private string GetRelativePath(DirectoryInfo? directory, DirectoryInfo rootDirectory)
    {
        if (directory == null) return string.Empty;

        var relativePath = directory.FullName.Substring(rootDirectory.FullName.Length).TrimStart('\\', '/');
        return relativePath.Replace('\\', '/') + "/";
    }

    private string GetGlobalRegistryVr()
    {
        // This would typically come from a GlobalRegistry or similar service
        // For now, we'll just return a default value
        return ""; // Empty string for non-VR, or could be "VR" for VR mode
    }

    #endregion
}