using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using Scanner111.Services.Interfaces;

namespace Scanner111.Services;

/// <summary>
/// Service for scanning and managing game files and mods, while providing utility functions
/// for analyzing log files and generating consolidated results. Implements the IGameScanService interface.
/// </summary>
public class GameScanService(
    IYamlSettingsCache settingsCache,
    IGameContextService gameContextService,
    IFileService fileService)
    : IGameScanService
{
    // Constants
    private const string BackupDir = "CLASSIC Backup/Game Files";
    private const string CleanupFilesDir = "CLASSIC Backup/Cleaned Files";
    private const bool TestMode = false; // Set to true for testing

    /// <summary>
    /// Inspects log files located within a specified directory for any recorded errors.
    /// </summary>
    /// <param name="folderPath">The path to the directory containing log files to be analyzed for errors.</param>
    /// <returns>A string with a report of all detected errors found in the relevant log files, or an empty string if none are found.</returns>
    public string CheckLogErrors(string folderPath)
    {
        if (string.IsNullOrEmpty(folderPath) || !Directory.Exists(folderPath))
            return string.Empty;

        // Get YAML settings
        var catchErrors = NormalizeList(settingsCache.GetSetting<List<string>>(YamlStore.Main, "catch_log_errors"));
        var ignoreFiles = NormalizeList(settingsCache.GetSetting<List<string>>(YamlStore.Main, "exclude_log_files"));
        var ignoreErrors = NormalizeList(settingsCache.GetSetting<List<string>>(YamlStore.Main, "exclude_log_errors"));

        var errorReport = new StringBuilder();

        // Find valid log files (excluding crash logs)
        var validLogFiles = Directory.GetFiles(folderPath, "*.log")
            .Where(file => !Path.GetFileName(file).Contains("crash-"))
            .Select(file => new FileInfo(file));

        foreach (var logFile in validLogFiles)
        {
            // Skip files that should be ignored
            if (ignoreFiles.Any(part => logFile.FullName.Contains(part, StringComparison.InvariantCultureIgnoreCase)))
                continue;

            try
            {
                var logLines = File.ReadAllLines(logFile.FullName);

                // Filter for relevant errors
                var detectedErrors = logLines
                    .Where(line =>
                        catchErrors.Any(error => line.Contains(error, StringComparison.InvariantCultureIgnoreCase)) &&
                        ignoreErrors.All(ignore => !line.Contains(ignore, StringComparison.InvariantCultureIgnoreCase)))
                    .Select(line => $"ERROR > {line}");

                var errorsList = detectedErrors.ToList();
                if (errorsList.Count > 0)
                {
                    // Format the error report
                    errorReport.AppendLine("[!] CAUTION : THE FOLLOWING LOG FILE REPORTS ONE OR MORE ERRORS!");
                    errorReport.AppendLine("[ Errors do not necessarily mean that the mod is not working. ]");
                    errorReport.AppendLine($"\nLOG PATH > {logFile.FullName}");

                    foreach (var error in errorsList) errorReport.AppendLine(error);

                    errorReport.AppendLine($"\n* TOTAL NUMBER OF DETECTED LOG ERRORS * : {errorsList.Count}\n");
                }
            }
            catch (IOException)
            {
                var errorMessage = $"❌ ERROR : Unable to scan this log file :\n  {logFile.FullName}";
                errorReport.AppendLine(errorMessage);
                Console.WriteLine($"> ! > DETECT LOG ERRORS > UNABLE TO SCAN : {logFile.FullName}");
            }
        }

        return errorReport.ToString();
    }

    /// <summary>
    ///     Scans loose mod files for issues and moves redundant files to backup location.
    /// </summary>
    /// <returns>Detailed report of scan results.</returns>
    public string? ScanModsUnpacked()
    {
        // Initialize report strings
        var messageList = new List<string>
        {
            "=================== MOD FILES SCAN ====================\n",
            "========= RESULTS FROM UNPACKED / LOOSE FILES =========\n"
        };

        // Initialize sets for collecting different issue types
        var issueCollections = new Dictionary<string, HashSet<string>>
        {
            ["cleanup"] = [],
            ["animdata"] = [],
            ["tex_dims"] = [],
            ["tex_frmt"] = [],
            ["snd_frmt"] = [],
            ["xse_file"] = [],
            ["previs"] = []
        };

        // Get settings
        var vr = gameContextService.GetGameVr();
        var xseAcronym = settingsCache.GetSetting<string>(YamlStore.Game, $"Game{vr}_Info.XSE_Acronym") ?? "XSE";
        var xseScriptFiles = settingsCache.GetSetting<Dictionary<string, string>>(
            YamlStore.Game, $"Game{vr}_Info.XSE_HashedScripts") ?? new Dictionary<string, string>();

        // Setup paths
        var backupPath = new DirectoryInfo(CleanupFilesDir);
        if (!TestMode) backupPath.Create();

        var modPath = settingsCache.GetSetting<string>(YamlStore.Settings, "CLASSIC_Settings.MODS Folder Path");
        if (string.IsNullOrEmpty(modPath))
            return settingsCache.GetSetting<string>(YamlStore.Main, "Mods_Warn.Mods_Path_Missing");

        if (!Directory.Exists(modPath))
            return settingsCache.GetSetting<string>(YamlStore.Main, "Mods_Warn.Mods_Path_Invalid");

        Console.WriteLine("✔️ MODS FOLDER PATH FOUND! PERFORMING INITIAL MOD FILES CLEANUP...");

        // First pass: cleanup and detect animation data
        var filterNames = new[] { "readme", "changes", "changelog", "change log" };
        foreach (var directory in Directory.GetDirectories(modPath, "*", SearchOption.AllDirectories))
        {
            var rootDir = new DirectoryInfo(directory);
            var rootMain = GetRelativePath(rootDir.Parent!, new DirectoryInfo(modPath));
            var hasAnimData = false;

            // Process directories
            foreach (var subDir in rootDir.GetDirectories())
            {
                var dirnameLower = subDir.Name.ToLowerInvariant();
                if (!hasAnimData && dirnameLower == "animationfiledata")
                {
                    hasAnimData = true;
                    issueCollections["animdata"].Add($"  - {rootMain}\n");
                }
                else if (dirnameLower == "fomod")
                {
                    var fomodFolderPath = subDir.FullName;
                    var relativePath = fomodFolderPath.Substring(modPath.Length).TrimStart(Path.DirectorySeparatorChar);
                    var newFolderPath = Path.Combine(backupPath.FullName, relativePath);

                    if (!TestMode)
                    {
                        // Create directory if it doesn't exist
                        Directory.CreateDirectory(Path.GetDirectoryName(newFolderPath)!);
                        MoveDirectory(fomodFolderPath, newFolderPath);
                    }

                    issueCollections["cleanup"].Add($"  - {relativePath}\n");
                }
            }

            // Process files for cleanup
            foreach (var file in rootDir.GetFiles("*.txt"))
            {
                var filenameLower = file.Name.ToLowerInvariant();
                if (!filterNames.Any(name => filenameLower.Contains(name))) continue;
                var relativePath = file.FullName.Substring(modPath.Length).TrimStart(Path.DirectorySeparatorChar);
                var newFilePath = Path.Combine(backupPath.FullName, relativePath);

                if (!TestMode)
                {
                    // Create directory if it doesn't exist
                    Directory.CreateDirectory(Path.GetDirectoryName(newFilePath)!);
                    File.Move(file.FullName, newFilePath, true);
                }

                issueCollections["cleanup"].Add($"  - {relativePath}\n");
            }
        }

        Console.WriteLine("✔️ CLEANUP COMPLETE! NOW ANALYZING ALL UNPACKED/LOOSE MOD FILES...");

        // Second pass: analyze files for issues
        foreach (var file in Directory.GetFiles(modPath, "*", SearchOption.AllDirectories))
        {
            var fileInfo = new FileInfo(file);
            var rootMain = GetRelativePath(fileInfo.Directory!.Parent!, new DirectoryInfo(modPath));
            var filenameLower = fileInfo.Name.ToLowerInvariant();
            var relativePath = file.Substring(modPath.Length).TrimStart(Path.DirectorySeparatorChar);
            var fileExt = fileInfo.Extension.ToLowerInvariant();

            switch (fileExt)
            {
                // Check DDS dimensions
                case ".dds":
                    try
                    {
                        using var ddsFile = File.OpenRead(file);
                        var ddsData = new byte[20];
                        ddsFile.ReadExactly(ddsData, 0, 20);

                        if (ddsData[0] == 'D' && ddsData[1] == 'D' && ddsData[2] == 'S' && ddsData[3] == ' ')
                        {
                            var width = BitConverter.ToInt32(ddsData, 12);
                            var height = BitConverter.ToInt32(ddsData, 16);

                            if (width % 2 != 0 || height % 2 != 0)
                                issueCollections["tex_dims"].Add($"  - {relativePath} ({width}x{height})");
                        }
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"Error reading DDS file {file}: {ex.Message}");
                    }

                    break;
                // Check for invalid texture formats
                case ".tga" or ".png" when !file.Contains("BodySlide"):
                    issueCollections["tex_frmt"]
                        .Add($"  - {fileExt.Substring(1).ToUpperInvariant()} : {relativePath}\n");
                    break;
                // Check for invalid sound formats
                case ".mp3":
                case ".m4a":
                    issueCollections["snd_frmt"]
                        .Add($"  - {fileExt.Substring(1).ToUpperInvariant()} : {relativePath}\n");
                    break;
                // Check for XSE files
                default:
                {
                    if (xseScriptFiles.Keys.Any(key =>
                            filenameLower.Equals(key, StringComparison.InvariantCultureIgnoreCase)) &&
                        !file.Contains("workshop framework", StringComparison.InvariantCultureIgnoreCase) &&
                        file.Contains($"Scripts{Path.DirectorySeparatorChar}{fileInfo.Name}"))
                        issueCollections["xse_file"].Add($"  - {rootMain}\n");

                    // Check for previs files
                    else if (filenameLower.EndsWith(".uvd") || filenameLower.EndsWith("_oc.nif"))
                        issueCollections["previs"].Add($"  - {rootMain}\n");
                    break;
                }
            }
        }

        // Build the report
        var issueMessages = new Dictionary<string, List<string>>
        {
            ["xse_file"] =
            [
                $"\n# ⚠️ FOLDERS CONTAIN COPIES OF *{xseAcronym}* SCRIPT FILES ⚠️\n",
                "▶️ Any mods with copies of original Script Extender files\n",
                "  may cause script related problems or crashes.\n\n"
            ],
            ["previs"] =
            [
                "\n# ⚠️ FOLDERS CONTAIN LOOSE PRECOMBINE / PREVIS FILES ⚠️\n",
                "▶️ Any mods that contain custom precombine/previs files\n",
                "  should load after the PRP.esp plugin from Previs Repair Pack (PRP).\n",
                "  Otherwise, see if there is a PRP patch available for these mods.\n\n"
            ],
            ["tex_dims"] =
            [
                "\n# ⚠️ DDS DIMENSIONS ARE NOT DIVISIBLE BY 2 ⚠️\n",
                "▶️ Any mods that have texture files with incorrect dimensions\n",
                "  are very likely to cause a *Texture (DDS) Crash*. For further details,\n",
                "  read the *How To Read Crash Logs.pdf* included with the CLASSIC exe.\n\n"
            ],
            ["tex_frmt"] =
            [
                "\n# ❓ TEXTURE FILES HAVE INCORRECT FORMAT, SHOULD BE DDS ❓\n",
                "▶️ Any files with an incorrect file format will not work.\n",
                "  Mod authors should convert these files to their proper game format.\n",
                "  If possible, notify the original mod authors about these problems.\n\n"
            ],
            ["snd_frmt"] =
            [
                "\n# ❓ SOUND FILES HAVE INCORRECT FORMAT, SHOULD BE XWM OR WAV ❓\n",
                "▶️ Any files with an incorrect file format will not work.\n",
                "  Mod authors should convert these files to their proper game format.\n",
                "  If possible, notify the original mod authors about these problems.\n\n"
            ],
            ["animdata"] =
            [
                "\n# ❓ FOLDERS CONTAIN CUSTOM ANIMATION FILE DATA ❓\n",
                "▶️ Any mods that have their own custom Animation File Data\n",
                "  may rarely cause an *Animation Corruption Crash*. For further details,\n",
                "  read the *How To Read Crash Logs.pdf* included with the CLASSIC exe.\n\n"
            ],
            ["cleanup"] = ["\n# 📄 DOCUMENTATION FILES MOVED TO 'CLASSIC Backup\\Cleaned Files' 📄\n"]
        };

        // Add found issues to message list
        foreach (var (issueType, items) in issueCollections)
            if (items.Count > 0)
            {
                messageList.AddRange(issueMessages[issueType]);
                messageList.AddRange(items.OrderBy(x => x));
            }

        return string.Join("", messageList);
    }

    /// <summary>
    ///     Scans archived BA2 mod files for issues.
    /// </summary>
    /// <returns>Detailed report of scan results.</returns>
    public string? ScanModsArchived()
    {
        var messageList = new List<string>
        {
            "\n========== RESULTS FROM ARCHIVED / BA2 FILES ==========\n"
        };

        // Initialize collections for different issue types
        var issueCollections = new Dictionary<string, HashSet<string>>
        {
            ["ba2_frmt"] = [],
            ["animdata"] = [],
            ["tex_dims"] = [],
            ["tex_frmt"] = [],
            ["snd_frmt"] = [],
            ["xse_file"] = [],
            ["previs"] = []
        };

        // Get settings
        var vr = gameContextService.GetGameVr();
        var xseAcronym = settingsCache.GetSetting<string>(YamlStore.Game, $"Game{vr}_Info.XSE_Acronym") ?? "";
        var xseScriptFiles = settingsCache.GetSetting<Dictionary<string, string>>(
            YamlStore.Game, $"Game{vr}_Info.XSE_HashedScripts") ?? new Dictionary<string, string>();

        // Setup paths
        var bsarchPath = Path.Combine(Directory.GetCurrentDirectory(), "CLASSIC Data", "BSArch.exe");
        var modPath = settingsCache.GetSetting<string>(YamlStore.Settings, "CLASSIC_Settings.MODS Folder Path");

        // Validate paths
        if (string.IsNullOrEmpty(modPath))
            return settingsCache.GetSetting<string>(YamlStore.Main, "Mods_Warn.Mods_Path_Missing");

        if (!Directory.Exists(modPath))
            return settingsCache.GetSetting<string>(YamlStore.Main, "Mods_Warn.Mods_Path_Invalid");

        if (!File.Exists(bsarchPath))
            return settingsCache.GetSetting<string>(YamlStore.Main, "Mods_Warn.Mods_BSArch_Missing");

        Console.WriteLine("✔️ ALL REQUIREMENTS SATISFIED! NOW ANALYZING ALL BA2 MOD ARCHIVES...");

        // Process BA2 files
        foreach (var filePath in Directory.GetFiles(modPath, "*.ba2", SearchOption.AllDirectories))
        {
            var fileName = Path.GetFileName(filePath);
            var fileNameLower = fileName.ToLowerInvariant();

            if (fileNameLower == "prp - main.ba2") continue;

            // Read BA2 header
            try
            {
                using var fileStream = new FileStream(filePath, FileMode.Open, FileAccess.Read);
                var header = new byte[12];
                fileStream.ReadExactly(header, 0, 12);

                // Check BA2 format
                if (header[0] != 'B' || header[1] != 'T' || header[2] != 'D' || header[3] != 'X' ||
                    ((header[8] != 'D' || header[9] != 'X' || header[10] != '1' || header[11] != '0') &&
                     (header[8] != 'G' || header[9] != 'N' || header[10] != 'R' || header[11] != 'L')))
                {
                    issueCollections["ba2_frmt"].Add($"  - {fileName} : {Encoding.ASCII.GetString(header)}\n");
                    continue;
                }

                if (header[8] == 'D' && header[9] == 'X' && header[10] == '1' && header[11] == '0')
                {
                    // Process texture-format BA2
                    var startInfo = new ProcessStartInfo
                    {
                        FileName = bsarchPath,
                        Arguments = $"\"{filePath}\" -dump",
                        UseShellExecute = false,
                        RedirectStandardOutput = true,
                        RedirectStandardError = true,
                        CreateNoWindow = true
                    };

                    var process = Process.Start(startInfo);
                    var output = process!.StandardOutput.ReadToEnd();
                    var error = process.StandardError.ReadToEnd();
                    process.WaitForExit();

                    if (process.ExitCode != 0)
                    {
                        Console.WriteLine($"BSArch command failed: {process.ExitCode} {error}");
                        continue;
                    }

                    var outputSplit = output.Split("\n\n", StringSplitOptions.RemoveEmptyEntries);
                    if (outputSplit.Length > 0 && outputSplit[^1].StartsWith("Error:"))
                    {
                        Console.WriteLine($"BSArch command failed: {outputSplit[^1]} {error}");
                        continue;
                    }

                    // Process texture information
                    foreach (var fileBlock in outputSplit.Skip(4))
                    {
                        if (string.IsNullOrWhiteSpace(fileBlock))
                            continue;

                        var blockSplit = fileBlock.Split('\n', 4);

                        // Check texture format
                        if (!blockSplit[1].Contains("Ext: dds"))
                        {
                            var ext = blockSplit[0].Split('.').Last().ToUpperInvariant();
                            issueCollections["tex_frmt"].Add($"  - {ext} : {fileName} > {blockSplit[0]}\n");
                            continue;
                        }

                        // Check texture dimensions
                        var parts = blockSplit[2].Split([' '], StringSplitOptions.RemoveEmptyEntries);
                        if (parts.Length < 5) continue;
                        var width = parts[1];
                        var height = parts[3];

                        if ((int.TryParse(width, out var w) && w % 2 != 0) ||
                            (int.TryParse(height, out var h) && h % 2 != 0))
                            issueCollections["tex_dims"]
                                .Add($"  - {width}x{height} : {fileName} > {blockSplit[0]}");
                    }
                }
                else
                {
                    // Process general-format BA2
                    var startInfo = new ProcessStartInfo
                    {
                        FileName = bsarchPath,
                        Arguments = $"\"{filePath}\" -list",
                        UseShellExecute = false,
                        RedirectStandardOutput = true,
                        RedirectStandardError = true,
                        CreateNoWindow = true
                    };

                    var process = Process.Start(startInfo);
                    var output = process!.StandardOutput.ReadToEnd().ToLowerInvariant();
                    var error = process.StandardError.ReadToEnd();
                    process.WaitForExit();

                    if (process.ExitCode != 0)
                    {
                        Console.WriteLine($"BSArch command failed: {process.ExitCode} {error}");
                        continue;
                    }

                    // Process file list
                    var outputSplit = output.Split('\n');
                    var hasPrevisFiles = false;
                    var hasAnimData = false;
                    var hasXseFiles = false;

                    foreach (var file in outputSplit.Skip(15))
                        // Check sound formats
                        if (file.EndsWith(".mp3") || file.EndsWith(".m4a"))
                        {
                            var ext = file.Split('.').Last().ToUpperInvariant();
                            issueCollections["snd_frmt"].Add($"  - {ext} : {fileName} > {file}\n");
                        }

                        // Check animation data
                        else if (!hasAnimData && file.Contains("animationfiledata"))
                        {
                            hasAnimData = true;
                            issueCollections["animdata"].Add($"  - {fileName}\n");
                        }

                        // Check XSE files
                        else if (!hasXseFiles &&
                                 xseScriptFiles.Keys.Any(key => file.Contains($"scripts\\{key.ToLowerInvariant()}")) &&
                                 !filePath.Contains("workshop framework", StringComparison.InvariantCultureIgnoreCase))
                        {
                            hasXseFiles = true;
                            issueCollections["xse_file"].Add($"  - {fileName}\n");
                        }

                        // Check previs files
                        else if (!hasPrevisFiles && (file.EndsWith(".uvd") || file.EndsWith("_oc.nif")))
                        {
                            hasPrevisFiles = true;
                            issueCollections["previs"].Add($"  - {fileName}\n");
                        }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Failed to process file {fileName}: {ex.Message}");
            }
        }

        // Build the report
        var issueMessages = new Dictionary<string, List<string>>
        {
            ["xse_file"] =
            [
                $"\n# ⚠️ BA2 ARCHIVES CONTAIN COPIES OF *{xseAcronym}* SCRIPT FILES ⚠️\n",
                "▶️ Any mods with copies of original Script Extender files\n",
                "  may cause script related problems or crashes.\n\n"
            ],
            ["previs"] =
            [
                "\n# ⚠️ BA2 ARCHIVES CONTAIN CUSTOM PRECOMBINE / PREVIS FILES ⚠️\n",
                "▶️ Any mods that contain custom precombine/previs files\n",
                "  should load after the PRP.esp plugin from Previs Repair Pack (PRP).\n",
                "  Otherwise, see if there is a PRP patch available for these mods.\n\n"
            ],
            ["tex_dims"] =
            [
                "\n# ⚠️ DDS DIMENSIONS ARE NOT DIVISIBLE BY 2 ⚠️\n",
                "▶️ Any mods that have texture files with incorrect dimensions\n",
                "  are very likely to cause a *Texture (DDS) Crash*. For further details,\n",
                "  read the *How To Read Crash Logs.pdf* included with the CLASSIC exe.\n\n"
            ],
            ["tex_frmt"] =
            [
                "\n# ❓ TEXTURE FILES HAVE INCORRECT FORMAT, SHOULD BE DDS ❓\n",
                "▶️ Any files with an incorrect file format will not work.\n",
                "  Mod authors should convert these files to their proper game format.\n",
                "  If possible, notify the original mod authors about these problems.\n\n"
            ],
            ["snd_frmt"] =
            [
                "\n# ❓ SOUND FILES HAVE INCORRECT FORMAT, SHOULD BE XWM OR WAV ❓\n",
                "▶️ Any files with an incorrect file format will not work.\n",
                "  Mod authors should convert these files to their proper game format.\n",
                "  If possible, notify the original mod authors about these problems.\n\n"
            ],
            ["animdata"] =
            [
                "\n# ❓ BA2 ARCHIVES CONTAIN CUSTOM ANIMATION FILE DATA ❓\n",
                "▶️ Any mods that have their own custom Animation File Data\n",
                "  may rarely cause an *Animation Corruption Crash*. For further details,\n",
                "  read the *How To Read Crash Logs.pdf* included with the CLASSIC exe.\n\n"
            ],
            ["ba2_frmt"] =
            [
                "\n# ❓ BA2 ARCHIVES HAVE INCORRECT FORMAT, SHOULD BE BTDX-GNRL OR BTDX-DX10 ❓\n",
                "▶️ Any files with an incorrect file format will not work.\n",
                "  Mod authors should convert these files to their proper game format.\n",
                "  If possible, notify the original mod authors about these problems.\n\n"
            ]
        };

        // Add found issues to message list
        foreach (var (issueType, items) in issueCollections)
            if (items.Count > 0)
            {
                messageList.AddRange(issueMessages[issueType]);
                messageList.AddRange(items.OrderBy(x => x));
            }

        return string.Join("", messageList);
    }

    /// <summary>
    ///     Manages game files by performing backup, restore, or removal operations.
    /// </summary>
    /// <param name="classicList">The name of the list specifying which files need to be managed.</param>
    /// <param name="mode">The operation mode to be performed on the files.</param>
    public void GameFilesManage(string classicList, string mode = "BACKUP")
    {
        // Constants
        const string backupDir = "CLASSIC Backup/Game Files";
        const string successPrefix = "✔️ SUCCESSFULLY";
        const string errorPrefix = "❌ ERROR :";
        const string adminSuggestion = "    TRY RUNNING CLASSIC.EXE IN ADMIN MODE TO RESOLVE THIS PROBLEM.\n";

        var vr = gameContextService.GetGameVr();

        // Get paths and settings
        var gamePath = settingsCache.GetSetting<string>(YamlStore.GameLocal, $"Game{vr}_Info.Root_Folder_Game");
        var manageList = settingsCache.GetSetting<List<string>>(YamlStore.Game, classicList) ?? [];

        // Validate game path
        if (string.IsNullOrEmpty(gamePath) || !Directory.Exists(gamePath))
            throw new DirectoryNotFoundException("Game folder not found or is not a valid directory");

        // Set up backup path
        var backupPath = Path.Combine(backupDir, classicList);
        Directory.CreateDirectory(backupPath);

        // Extract list name for display purposes
        var listName = classicList.Split([' '], 2).Length > 1
            ? classicList.Split([' '], 2)[1]
            : classicList;

        bool MatchesManagedFile(string fileName)
        {
            return manageList.Any(item => fileName.Contains(item, StringComparison.InvariantCultureIgnoreCase));
        }

        void HandlePermissionError(string operation)
        {
            Console.WriteLine($"{errorPrefix} UNABLE TO {operation} {listName} FILES DUE TO FILE PERMISSIONS!");
            Console.WriteLine(adminSuggestion);
        }

        try
        {
            switch (mode)
            {
                case "BACKUP":
                    Console.WriteLine($"CREATING A BACKUP OF {listName} FILES, PLEASE WAIT...");
                    foreach (var file in Directory.GetFileSystemEntries(gamePath))
                    {
                        var fileName = Path.GetFileName(file);
                        if (!MatchesManagedFile(fileName)) continue;
                        var destPath = Path.Combine(backupPath, fileName);

                        if (File.Exists(file))
                        {
                            File.Copy(file, destPath, true);
                        }
                        else if (Directory.Exists(file))
                        {
                            if (Directory.Exists(destPath))
                                Directory.Delete(destPath, true);
                            else if (File.Exists(destPath)) File.Delete(destPath);
                            fileService.CopyDirectory(file, destPath);
                        }
                    }

                    Console.WriteLine($"{successPrefix} CREATED A BACKUP OF {listName} FILES\n");
                    break;

                case "RESTORE":
                    Console.WriteLine($"RESTORING {listName} FILES FROM A BACKUP, PLEASE WAIT...");
                    foreach (var file in Directory.GetFileSystemEntries(gamePath))
                    {
                        var fileName = Path.GetFileName(file);
                        if (!MatchesManagedFile(fileName)) continue;
                        var sourcePath = Path.Combine(backupPath, fileName);
                        if (!File.Exists(sourcePath) && !Directory.Exists(sourcePath)) continue;
                        if (File.Exists(file))
                            File.Delete(file);
                        else if (Directory.Exists(file)) Directory.Delete(file, true);

                        if (File.Exists(sourcePath))
                            File.Copy(sourcePath, file, true);
                        else if (Directory.Exists(sourcePath)) fileService.CopyDirectory(sourcePath, file);
                    }

                    Console.WriteLine($"{successPrefix} RESTORED {listName} FILES TO THE GAME FOLDER\n");
                    break;

                case "REMOVE":
                    Console.WriteLine($"REMOVING {listName} FILES FROM YOUR GAME FOLDER, PLEASE WAIT...");
                    foreach (var file in Directory.GetFileSystemEntries(gamePath))
                    {
                        var fileName = Path.GetFileName(file);
                        if (!MatchesManagedFile(fileName)) continue;
                        if (File.Exists(file))
                            File.Delete(file);
                        else if (Directory.Exists(file)) Directory.Delete(file, true);
                    }

                    Console.WriteLine($"{successPrefix} REMOVED {listName} FILES FROM THE GAME FOLDER\n");
                    break;
            }
        }
        catch (UnauthorizedAccessException)
        {
            HandlePermissionError(mode);
        }
        catch (IOException ex)
        {
            Console.WriteLine($"{errorPrefix} {ex.Message}");
            HandlePermissionError(mode);
        }
    }

    /// <summary>
    ///     Generates a combined result summarizing game-related checks and scans.
    /// </summary>
    /// <returns>A string summarizing the results of all performed checks and scans.</returns>
    public string GameCombinedResult()
    {
        var vr = gameContextService.GetGameVr();
        var docsPath = settingsCache.GetSetting<string>(YamlStore.GameLocal, $"Game{vr}_Info.Root_Folder_Docs");
        var gamePath = settingsCache.GetSetting<string>(YamlStore.GameLocal, $"Game{vr}_Info.Root_Folder_Game");

        if (string.IsNullOrEmpty(gamePath) || string.IsNullOrEmpty(docsPath)) return string.Empty;

        var checkXsePlugins = gameContextService.CheckXsePlugins();
        var checkCrashgenSettings = gameContextService.CheckCrashgenSettings();
        var checkLogErrorsDocs = CheckLogErrors(docsPath);
        var checkLogErrorsGame = CheckLogErrors(gamePath);
        var scanWryecheck = gameContextService.ScanWryeCheck();
        var scanModInis = gameContextService.ScanModInis();

        return string.Concat(
            checkXsePlugins,
            checkCrashgenSettings,
            checkLogErrorsDocs,
            checkLogErrorsGame,
            scanWryecheck,
            scanModInis
        );
    }

    /// <summary>
    ///     Combines the results of scanning unpacked and archived mods.
    /// </summary>
    /// <returns>The combined results of the unpacked and archived mods scans.</returns>
    public string? ModsCombinedResult()
    {
        var unpacked = ScanModsUnpacked();
        if (unpacked!.StartsWith("❌ MODS FOLDER PATH NOT PROVIDED")) return unpacked;
        return unpacked + ScanModsArchived();
    }

    /// <summary>
    ///     Writes combined results of game and mods into a markdown report file.
    /// </summary>
    public void WriteCombinedResults()
    {
        var gameResult = GameCombinedResult();
        var modsResult = ModsCombinedResult();
        var reportPath = Path.Combine(Directory.GetCurrentDirectory(), "CLASSIC GFS Report.md");

        File.WriteAllText(reportPath, gameResult + modsResult, Encoding.UTF8);
    }

    #region Helper Methods

    private List<string> NormalizeList(List<string>? items)
    {
        return items?.Select(item => item.ToLowerInvariant()).ToList() ?? [];
    }

    private string GetRelativePath(DirectoryInfo path, DirectoryInfo basePath)
    {
        var pathString = path.FullName;
        var basePathString = basePath.FullName;

        if (!pathString.StartsWith(basePathString)) return path.Name;
        var relative = pathString[basePathString.Length..];
        return relative.TrimStart('\\', '/');
    }

    private void MoveDirectory(string sourceDir, string destDir)
    {
        // Create the destination directory
        Directory.CreateDirectory(destDir);

        // Get the files in the source directory and copy to the destination directory
        foreach (var file in Directory.GetFiles(sourceDir))
        {
            var fileName = Path.GetFileName(file);
            var destFile = Path.Combine(destDir, fileName);
            File.Copy(file, destFile, true);
        }

        // Get the subdirectories in the source directory and copy to the destination directory
        foreach (var directory in Directory.GetDirectories(sourceDir))
        {
            var dirName = Path.GetFileName(directory);
            var destSubDir = Path.Combine(destDir, dirName);
            MoveDirectory(directory, destSubDir);
        }

        // Delete the source directory
        Directory.Delete(sourceDir, true);
    }

    #endregion
}