using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Scanner111.Models;

namespace Scanner111.Services
{
    /// <summary>
    /// Service for managing game files (backup, restore, remove)
    /// </summary>
    public class GameFileManagementService : IGameFileManagementService
    {
        private readonly IYamlSettingsCacheService _yamlSettingsCache;
        private readonly ILogErrorCheckService _logErrorCheckService;
        private readonly IModScanningService _modScanningService;
        private readonly ICheckCrashgenSettingsService _checkCrashgenSettingsService;
        private readonly ICheckXsePluginsService _checkXsePluginsService;
        private readonly IScanModInisService _scanModInisService;
        private readonly IScanWryeCheckService _scanWryeCheckService;
        private readonly bool _testMode;

        public GameFileManagementService(
            IYamlSettingsCacheService yamlSettingsCache,
            ILogErrorCheckService logErrorCheckService,
            IModScanningService modScanningService,
            ICheckCrashgenSettingsService checkCrashgenSettingsService,
            ICheckXsePluginsService checkXsePluginsService,
            IScanModInisService scanModInisService,
            IScanWryeCheckService scanWryeCheckService,
            bool testMode = false)
        {
            _yamlSettingsCache = yamlSettingsCache ?? throw new ArgumentNullException(nameof(yamlSettingsCache));
            _logErrorCheckService =
                logErrorCheckService ?? throw new ArgumentNullException(nameof(logErrorCheckService));
            _modScanningService = modScanningService ?? throw new ArgumentNullException(nameof(modScanningService));
            _checkCrashgenSettingsService = checkCrashgenSettingsService ??
                                            throw new ArgumentNullException(nameof(checkCrashgenSettingsService));
            _checkXsePluginsService =
                checkXsePluginsService ?? throw new ArgumentNullException(nameof(checkXsePluginsService));
            _scanModInisService = scanModInisService ?? throw new ArgumentNullException(nameof(scanModInisService));
            _scanWryeCheckService =
                scanWryeCheckService ?? throw new ArgumentNullException(nameof(scanWryeCheckService));
            _testMode = testMode;
        }

        /// <summary>
        /// Manages game files by performing backup, restore, or removal operations.
        /// </summary>
        /// <param name="classicList">The name of the list specifying which files need to be managed.</param>
        /// <param name="mode">The operation mode to be performed on the files (BACKUP, RESTORE, REMOVE).</param>
        /// <returns>A task representing the asynchronous operation.</returns>
        public async Task<string> GameFilesManageAsync(string classicList, string mode = "BACKUP")
        {
            const string BACKUP_DIR = "CLASSIC Backup/Game Files";
            const string SUCCESS_PREFIX = "✔️ SUCCESSFULLY";
            const string ERROR_PREFIX = "❌ ERROR :";
            const string ADMIN_SUGGESTION = "    TRY RUNNING THE APP IN ADMIN MODE TO RESOLVE THIS PROBLEM.\n";

            // Use Task.Run for file I/O operations to make the method properly async
            await Task.Run(() =>
            {
                if (!_testMode && !Directory.Exists(BACKUP_DIR))
                {
                    Directory.CreateDirectory(BACKUP_DIR);
                }
            });

            var gameDir = GetSetting<string>(YAML.Game, "game_dir") ?? "";
            if (string.IsNullOrEmpty(gameDir))
            {
                return $"{ERROR_PREFIX} Game directory is not set in settings";
            }

            var fileList = GetSettingAsList<string>(YAML.Main, classicList);
            if (fileList.Count == 0)
            {
                return $"{ERROR_PREFIX} No files found in list: {classicList}";
            }

            var results = new StringBuilder();
            int successCount = 0;
            int errorCount = 0;

            await Task.Run(() =>
            {
                foreach (var file in fileList)
                {
                    try
                    {
                        var sourcePath = Path.Combine(gameDir, file);
                        var backupPath = Path.Combine(BACKUP_DIR, file);

                        var backupDir = Path.GetDirectoryName(backupPath);
                        if (backupDir != null && !_testMode && !Directory.Exists(backupDir))
                        {
                            Directory.CreateDirectory(backupDir);
                        }

                        switch (mode)
                        {
                            case "BACKUP":
                                if (File.Exists(sourcePath) && !_testMode)
                                {
                                    File.Copy(sourcePath, backupPath, true);
                                }

                                successCount++;
                                break;

                            case "RESTORE":
                                if (File.Exists(backupPath) && !_testMode)
                                {
                                    File.Copy(backupPath, sourcePath, true);
                                }

                                successCount++;
                                break;

                            case "REMOVE":
                                if (File.Exists(sourcePath) && !_testMode)
                                {
                                    File.Delete(sourcePath);
                                }

                                successCount++;
                                break;

                            default:
                                results.AppendLine($"{ERROR_PREFIX} Invalid mode: {mode}");
                                errorCount++;
                                break;
                        }
                    }
                    catch (Exception ex)
                    {
                        results.AppendLine($"{ERROR_PREFIX} {ex.Message} - {file}");
                        results.AppendLine(ADMIN_SUGGESTION);
                        errorCount++;
                    }
                }
            });

            results.AppendLine($"{SUCCESS_PREFIX} processed {successCount} files in {mode} mode");
            if (errorCount > 0)
            {
                results.AppendLine($"{ERROR_PREFIX} encountered {errorCount} errors");
            }

            return results.ToString();
        }

        /// <summary>
        /// Generates a combined result summarizing game-related checks and scans.
        /// </summary>
        /// <returns>A string summarizing the results of all performed checks and scans.</returns>
        public async Task<string> GetGameCombinedResultAsync()
        {
            var results = new StringBuilder();

            results.AppendLine("======================== GAME SCAN ========================\n");

            // Log scan
            var logResults = await _logErrorCheckService.ScanGameLogsAsync();
            results.AppendLine(logResults);

            // Add crashgen/buffout settings check
            var crashgenResults = await _checkCrashgenSettingsService.CheckCrashgenSettingsAsync();
            results.AppendLine(crashgenResults);

            // Add XSE plugins check
            var xseResults = await _checkXsePluginsService.CheckXsePluginsAsync();
            results.AppendLine(xseResults);

            // Add INI scan
            var iniResults = await _scanModInisService.ScanModInisAsync();
            results.AppendLine(iniResults);

            // Add Wrye Bash check
            var wryeResults = await _scanWryeCheckService.ScanWryeCheckAsync();
            results.AppendLine(wryeResults);

            return results.ToString();
        }

        /// <summary>
        /// Writes combined results of game and mods into a markdown report file.
        /// </summary>
        /// <returns>A task representing the asynchronous operation.</returns>
        public async Task WriteCombinedResultsAsync()
        {
            const string REPORT_DIR = "CLASSIC Reports";
            const string REPORT_FILE = "CLASSIC_Report.md";

            // Create directory if needed
            if (!_testMode && !Directory.Exists(REPORT_DIR))
            {
                await Task.Run(() => Directory.CreateDirectory(REPORT_DIR));
            }

            // Get results
            var gameResults = await GetGameCombinedResultAsync();
            var unpackedModResults = await _modScanningService.ScanModsUnpackedAsync();
            var archivedModResults = await _modScanningService.ScanModsArchivedAsync();

            // Build report
            var report = new StringBuilder();
            report.AppendLine("# CLASSIC Report");
            report.AppendLine($"## Generated on {DateTime.Now}\n");

            report.AppendLine("## Game Scan Results");
            report.AppendLine("```");
            report.AppendLine(gameResults);
            report.AppendLine("```\n");

            report.AppendLine("## Mod Scan Results (Unpacked)");
            report.AppendLine("```");
            report.AppendLine(unpackedModResults);
            report.AppendLine("```\n");

            report.AppendLine("## Mod Scan Results (Archives)");
            report.AppendLine("```");
            report.AppendLine(archivedModResults);
            report.AppendLine("```\n");

            // Write file
            if (!_testMode)
            {
                var reportPath = Path.Combine(REPORT_DIR, REPORT_FILE);
                await File.WriteAllTextAsync(reportPath, report.ToString());
            }
        }

        /// <summary>
        /// Gets a setting from the YAML settings cache
        /// </summary>
        private T? GetSetting<T>(YAML yamlType, string key) where T : class
        {
            try
            {
                return _yamlSettingsCache.GetSetting<T>(yamlType, key);
            }
            catch
            {
                return null;
            }
        }

        /// <summary>
        /// Gets a list setting from the YAML settings cache
        /// </summary>
        private List<T> GetSettingAsList<T>(YAML yamlType, string key)
        {
            try
            {
                var setting = _yamlSettingsCache.GetSetting<List<T>>(yamlType, key);
                return setting ?? new List<T>();
            }
            catch
            {
                return new List<T>();
            }
        }
    }
}

