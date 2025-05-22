using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Scanner111.Models;

namespace Scanner111.Services;

/// <summary>
///     Service for managing game files and mods
/// </summary>
/// <summary>
///     Service for managing game files (backup, restore, remove)
/// </summary>
public class GameFileManagementService : IGameFileManagementService
{
    private readonly ICheckCrashgenSettingsService _checkCrashgenSettingsService;
    private readonly ICheckXsePluginsService _checkXsePluginsService;
    private readonly ILogErrorCheckService _logErrorCheckService;
    private readonly ILogger<GameFileManagementService>? _logger;
    private readonly IModScanningService _modScanningService;
    private readonly IScanModInisService _scanModInisService;
    private readonly IScanWryeCheckService _scanWryeCheckService;
    private readonly bool _testMode;
    private readonly IYamlSettingsCacheService _yamlSettingsCache;

    public GameFileManagementService(
        IYamlSettingsCacheService yamlSettingsCache,
        ILogErrorCheckService logErrorCheckService,
        IModScanningService modScanningService,
        ICheckCrashgenSettingsService checkCrashgenSettingsService,
        ICheckXsePluginsService checkXsePluginsService,
        IScanModInisService scanModInisService,
        IScanWryeCheckService scanWryeCheckService,
        ILogger<GameFileManagementService>? logger = null,
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
        _logger = logger;
        _testMode = testMode;
    }

    /// <summary>
    ///     Manages game files by performing backup, restore, or removal operations.
    /// </summary>
    /// <param name="classicList">The name of the list specifying which files need to be managed.</param>
    /// <param name="mode">The operation mode to be performed on the files (BACKUP, RESTORE, REMOVE).</param>
    /// <param name="progress">Optional progress reporter to receive progress updates.</param>
    /// <param name="cancellationToken">Token to monitor for cancellation requests.</param>
    /// <returns>A task representing the asynchronous operation with a string result.</returns>
    public async Task<string> GameFilesManageAsync(string classicList, string mode = "BACKUP",
        IProgress<ScanProgress>? progress = null, CancellationToken cancellationToken = default)
    {
        _logger?.LogInformation("Starting game files management. Mode: {Mode}, List: {List}", mode, classicList);
        progress?.Report(new ScanProgress(0, $"Starting {mode.ToLower()} operation", classicList));

        const string backupDir = "CLASSIC Backup/Game Files";
        const string successPrefix = "✔️ SUCCESSFULLY";
        const string errorPrefix = "❌ ERROR :";
        const string adminSuggestion = "    TRY RUNNING THE APP IN ADMIN MODE TO RESOLVE THIS PROBLEM.\n";

        try
        {
            cancellationToken.ThrowIfCancellationRequested();

            // Use Task.Run for file I/O operations to make the method properly async
            await Task.Run(() =>
            {
                if (!_testMode && !Directory.Exists(backupDir)) Directory.CreateDirectory(backupDir);
            }, cancellationToken).ConfigureAwait(false);

            progress?.Report(new ScanProgress(10, $"Preparing for {mode.ToLower()} operation", classicList));

            var gameDir = GetSetting<string>(Yaml.Game, "game_dir") ?? "";
            if (string.IsNullOrEmpty(gameDir))
            {
                var errorMessage = $"{errorPrefix} Game directory is not set in settings";
                _logger?.LogWarning("Game directory not set in settings");
                progress?.Report(new ScanProgress(100, "Error: Game directory not set"));
                return errorMessage;
            }

            var fileList = GetSettingAsList<string>(Yaml.Main, classicList);
            if (fileList.Count == 0)
            {
                var errorMessage = $"{errorPrefix} No files found in list: {classicList}";
                _logger?.LogWarning("No files found in list: {List}", classicList);
                progress?.Report(new ScanProgress(100, $"Error: No files found in list: {classicList}"));
                return errorMessage;
            }

            progress?.Report(new ScanProgress(20, $"Found {fileList.Count} files to process"));
            _logger?.LogInformation("Found {Count} files to process for {Mode} operation", fileList.Count, mode);

            var results = new StringBuilder();
            var successCount = 0;
            var errorCount = 0;
            var totalFiles = fileList.Count;

            await Task.Run(() =>
            {
                for (var i = 0; i < fileList.Count; i++)
                {
                    cancellationToken.ThrowIfCancellationRequested();

                    var file = fileList[i];
                    var percentComplete = 20 + (int)((float)(i + 1) / totalFiles * 70);
                    progress?.Report(
                        new ScanProgress(percentComplete, $"Processing file {i + 1} of {totalFiles}", file));

                    try
                    {
                        var sourcePath = Path.Combine(gameDir, file);
                        var backupPath = Path.Combine(backupDir, file);

                        var backupDirPath = Path.GetDirectoryName(backupPath);
                        if (backupDirPath != null && !_testMode && !Directory.Exists(backupDirPath))
                            Directory.CreateDirectory(backupDirPath);

                        switch (mode)
                        {
                            case "BACKUP":
                                if (File.Exists(sourcePath) && !_testMode)
                                {
                                    File.Copy(sourcePath, backupPath, true);
                                    _logger?.LogDebug("Backed up file: {File}", file);
                                }

                                successCount++;
                                break;

                            case "RESTORE":
                                if (File.Exists(backupPath) && !_testMode)
                                {
                                    File.Copy(backupPath, sourcePath, true);
                                    _logger?.LogDebug("Restored file: {File}", file);
                                }

                                successCount++;
                                break;

                            case "REMOVE":
                                if (File.Exists(sourcePath) && !_testMode)
                                {
                                    File.Delete(sourcePath);
                                    _logger?.LogDebug("Removed file: {File}", file);
                                }

                                successCount++;
                                break;

                            default:
                                results.AppendLine($"{errorPrefix} Invalid mode: {mode}");
                                _logger?.LogWarning("Invalid mode: {Mode}", mode);
                                errorCount++;
                                break;
                        }
                    }
                    catch (Exception ex)
                    {
                        results.AppendLine($"{errorPrefix} {ex.Message} - {file}");
                        results.AppendLine(adminSuggestion);
                        _logger?.LogError(ex, "Error processing file {File}", file);
                        errorCount++;
                    }
                }
            }, cancellationToken).ConfigureAwait(false);

            results.AppendLine($"{successPrefix} processed {successCount} files in {mode} mode");
            if (errorCount > 0) results.AppendLine($"{errorPrefix} encountered {errorCount} errors");

            progress?.Report(new ScanProgress(100, $"Completed {mode.ToLower()} operation",
                $"Processed {successCount} files, {errorCount} errors"));

            _logger?.LogInformation("Completed {Mode} operation. Success: {Success}, Errors: {Errors}",
                mode, successCount, errorCount);

            return results.ToString();
        }
        catch (OperationCanceledException)
        {
            _logger?.LogInformation("Game files management was cancelled");
            progress?.Report(new ScanProgress(0, "Operation cancelled"));
            throw;
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Error during game files management");
            progress?.Report(new ScanProgress(0, $"Error: {ex.Message}"));
            throw;
        }
    }

    /// <summary>
    ///     Generates a combined result summarizing game-related checks and scans.
    /// </summary>
    /// <param name="progress">Optional progress reporter to receive progress updates.</param>
    /// <param name="cancellationToken">Token to monitor for cancellation requests.</param>
    /// <returns>A string summarizing the results of all performed checks and scans.</returns>
    public async Task<string> GetGameCombinedResultAsync(IProgress<ScanProgress>? progress = null,
        CancellationToken cancellationToken = default)
    {
        _logger?.LogInformation("Starting combined game scan");
        progress?.Report(new ScanProgress(0, "Starting game scan"));

        try
        {
            cancellationToken.ThrowIfCancellationRequested();

            var results = new StringBuilder();
            results.AppendLine("======================== GAME SCAN ========================\n");

            // Log scan (20% of progress)
            progress?.Report(new ScanProgress(5, "Scanning game logs"));
            var logResults = await Task
                .Run(async () => await _logErrorCheckService.ScanGameLogsAsync().ConfigureAwait(false),
                    cancellationToken).ConfigureAwait(false);
            results.AppendLine(logResults);

            cancellationToken.ThrowIfCancellationRequested();

            // Add crashgen/buffout settings check (20% of progress)
            progress?.Report(new ScanProgress(25, "Checking crashgen settings"));
            var crashgenResults = await Task
                .Run(async () => await _checkCrashgenSettingsService.CheckCrashgenSettingsAsync().ConfigureAwait(false),
                    cancellationToken).ConfigureAwait(false);
            results.AppendLine(crashgenResults);

            cancellationToken.ThrowIfCancellationRequested();

            // Add XSE plugins check (20% of progress)
            progress?.Report(new ScanProgress(45, "Checking XSE plugins"));
            var xseResults = await Task
                .Run(async () => await _checkXsePluginsService.CheckXsePluginsAsync().ConfigureAwait(false),
                    cancellationToken).ConfigureAwait(false);
            results.AppendLine(xseResults);

            cancellationToken.ThrowIfCancellationRequested();

            // Add INI scan (20% of progress)
            progress?.Report(new ScanProgress(65, "Scanning mod INIs"));
            var iniResults = await Task
                .Run(async () => await _scanModInisService.ScanModInisAsync().ConfigureAwait(false), cancellationToken)
                .ConfigureAwait(false);
            results.AppendLine(iniResults);

            cancellationToken.ThrowIfCancellationRequested();

            // Add Wrye Bash check (20% of progress)
            progress?.Report(new ScanProgress(85, "Checking Wrye Bash"));
            var wryeResults = await Task
                .Run(async () => await _scanWryeCheckService.ScanWryeCheckAsync().ConfigureAwait(false),
                    cancellationToken).ConfigureAwait(false);
            results.AppendLine(wryeResults);

            progress?.Report(new ScanProgress(100, "Game scan completed"));
            _logger?.LogInformation("Combined game scan completed successfully");

            return results.ToString();
        }
        catch (OperationCanceledException)
        {
            _logger?.LogInformation("Combined game scan was cancelled");
            progress?.Report(new ScanProgress(0, "Scan cancelled"));
            throw;
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Error during combined game scan");
            progress?.Report(new ScanProgress(0, $"Error: {ex.Message}"));
            throw;
        }
    }

    /// <summary>
    ///     Writes combined results of game and mods into a markdown report file.
    /// </summary>
    /// <param name="progress">Optional progress reporter to receive progress updates.</param>
    /// <param name="cancellationToken">Token to monitor for cancellation requests.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    public async Task WriteCombinedResultsAsync(IProgress<ScanProgress>? progress = null,
        CancellationToken cancellationToken = default)
    {
        _logger?.LogInformation("Starting to write combined report");
        progress?.Report(new ScanProgress(0, "Starting to write report"));

        try
        {
            cancellationToken.ThrowIfCancellationRequested();

            const string reportDir = "CLASSIC Reports";
            const string reportFile = "CLASSIC_Report.md";

            // Create directory if needed
            if (!_testMode && !Directory.Exists(reportDir))
                await Task.Run(() => Directory.CreateDirectory(reportDir), cancellationToken)
                    .ConfigureAwait(false);

            progress?.Report(new ScanProgress(10, "Getting game scan results"));

            // Get game results
            var gameResults = await GetGameCombinedResultAsync(
                new Progress<ScanProgress>(p =>
                {
                    if (p.PercentComplete > 0)
                    {
                        // Map game scan progress (0-100) to report progress (10-60)
                        var reportProgress = 10 + (int)(p.PercentComplete * 0.5);
                        progress?.Report(new ScanProgress(reportProgress, p.CurrentOperation, p.CurrentItem));
                    }
                }),
                cancellationToken).ConfigureAwait(false);

            cancellationToken.ThrowIfCancellationRequested();
            progress?.Report(new ScanProgress(60, "Getting mod scan results"));

            // Get mod results
            var modResults = await _modScanningService.GetModsCombinedResultAsync(
                new Progress<ScanProgress>(p =>
                {
                    if (p.PercentComplete > 0)
                    {
                        // Map mod scan progress (0-100) to report progress (60-90)
                        var reportProgress = 60 + (int)(p.PercentComplete * 0.3);
                        progress?.Report(new ScanProgress(reportProgress, p.CurrentOperation, p.CurrentItem));
                    }
                }),
                cancellationToken).ConfigureAwait(false);

            cancellationToken.ThrowIfCancellationRequested();
            progress?.Report(new ScanProgress(90, "Writing report to file"));

            // Combine results
            var combinedResults = gameResults + modResults;

            // Write to file
            if (!_testMode)
                await Task.Run(() =>
                {
                    var reportPath = Path.Combine(reportDir, reportFile);
                    File.WriteAllText(reportPath, combinedResults);
                }, cancellationToken).ConfigureAwait(false);

            progress?.Report(new ScanProgress(100, "Report written successfully"));
            _logger?.LogInformation("Combined report written successfully");
        }
        catch (OperationCanceledException)
        {
            _logger?.LogInformation("Writing combined report was cancelled");
            progress?.Report(new ScanProgress(0, "Operation cancelled"));
            throw;
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Error writing combined report");
            progress?.Report(new ScanProgress(0, $"Error: {ex.Message}"));
            throw;
        }
    }

    private T? GetSetting<T>(Yaml section, string key)
    {
        return _yamlSettingsCache.GetSetting<T>(section, key);
    }

    private List<T> GetSettingAsList<T>(Yaml section, string key)
    {
        var result = _yamlSettingsCache.GetSetting<List<T>>(section, key);
        return result ?? [];
    }
}