using System.Diagnostics;
using Microsoft.Extensions.Logging;
using Scanner111.Common.Models.GameIntegrity;
using Scanner111.Common.Models.GamePath;
using Scanner111.Common.Models.Reporting;
using Scanner111.Common.Models.ScanGame;
using Scanner111.Common.Services.FileIO;
using Scanner111.Common.Services.GameIntegrity;
using Scanner111.Common.Services.Reporting;
using Scanner111.Common.Services.ScanGame;

namespace Scanner111.Common.Services.Orchestration;

/// <summary>
/// Orchestrates comprehensive game and mod installation scanning operations.
/// </summary>
/// <remarks>
/// <para>
/// This orchestrator coordinates multiple independent scanners in parallel,
/// continuing execution even when individual scanners fail. All errors are
/// captured and reported for diagnostic purposes.
/// </para>
/// <para>
/// The implementation follows a fail-safe pattern: scanner failures do not
/// prevent other scanners from completing, and the orchestrator always
/// produces a result (even if empty due to errors).
/// </para>
/// </remarks>
public sealed class ScanGameOrchestrator : IScanGameOrchestrator
{
    private readonly ILogger<ScanGameOrchestrator> _logger;
    private readonly IUnpackedModsScanner _unpackedScanner;
    private readonly IBA2Scanner _ba2Scanner;
    private readonly IIniValidator _iniValidator;
    private readonly ITomlValidator _tomlValidator;
    private readonly IXseChecker _xseChecker;
    private readonly IGameIntegrityChecker _integrityChecker;
    private readonly IScanGameReportBuilder _reportBuilder;
    private readonly IFileIOService _fileIO;

    /// <summary>
    /// Initializes a new instance of the <see cref="ScanGameOrchestrator"/> class.
    /// </summary>
    /// <param name="logger">The logger instance.</param>
    /// <param name="unpackedScanner">The unpacked mod files scanner.</param>
    /// <param name="ba2Scanner">The BA2 archive scanner.</param>
    /// <param name="iniValidator">The INI configuration validator.</param>
    /// <param name="tomlValidator">The TOML crash generator config validator.</param>
    /// <param name="xseChecker">The XSE installation integrity checker.</param>
    /// <param name="integrityChecker">The game installation integrity checker.</param>
    /// <param name="reportBuilder">The report builder for generating markdown reports.</param>
    /// <param name="fileIO">The file I/O service for writing reports.</param>
    public ScanGameOrchestrator(
        ILogger<ScanGameOrchestrator> logger,
        IUnpackedModsScanner unpackedScanner,
        IBA2Scanner ba2Scanner,
        IIniValidator iniValidator,
        ITomlValidator tomlValidator,
        IXseChecker xseChecker,
        IGameIntegrityChecker integrityChecker,
        IScanGameReportBuilder reportBuilder,
        IFileIOService fileIO)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _unpackedScanner = unpackedScanner ?? throw new ArgumentNullException(nameof(unpackedScanner));
        _ba2Scanner = ba2Scanner ?? throw new ArgumentNullException(nameof(ba2Scanner));
        _iniValidator = iniValidator ?? throw new ArgumentNullException(nameof(iniValidator));
        _tomlValidator = tomlValidator ?? throw new ArgumentNullException(nameof(tomlValidator));
        _xseChecker = xseChecker ?? throw new ArgumentNullException(nameof(xseChecker));
        _integrityChecker = integrityChecker ?? throw new ArgumentNullException(nameof(integrityChecker));
        _reportBuilder = reportBuilder ?? throw new ArgumentNullException(nameof(reportBuilder));
        _fileIO = fileIO ?? throw new ArgumentNullException(nameof(fileIO));
    }

    /// <inheritdoc/>
    public async Task<ScanGameResult> ScanAsync(
        ScanGameConfiguration configuration,
        IProgress<ScanGameProgress>? progress = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(configuration);

        _logger.LogInformation("Starting game scan for {GameName}", configuration.GameDisplayName);

        var stopwatch = Stopwatch.StartNew();
        var errors = new List<ScannerError>();

        // Determine which scanners will run
        var scannerTasks = BuildScannerTasks(configuration, cancellationToken);
        var totalScanners = scannerTasks.Count;

        _logger.LogDebug("Enabled scanners ({Count}): {Scanners}", totalScanners, string.Join(", ", scannerTasks.Keys));

        progress?.Report(ScanGameProgress.Starting(totalScanners));

        // Run all scanners concurrently
        var results = await ExecuteScannersAsync(
            scannerTasks,
            errors,
            progress,
            totalScanners,
            cancellationToken).ConfigureAwait(false);

        stopwatch.Stop();

        // Build the report
        var report = BuildReport(configuration, results);
        var generatedReport = _reportBuilder.BuildCombinedReport(report);

        progress?.Report(ScanGameProgress.Completed(totalScanners));

        if (errors.Count > 0)
        {
            _logger.LogWarning("Game scan completed with {ErrorCount} errors in {Duration:F2}s", errors.Count, stopwatch.Elapsed.TotalSeconds);
        }
        else
        {
            _logger.LogInformation("Game scan completed successfully in {Duration:F2}s", stopwatch.Elapsed.TotalSeconds);
        }

        return new ScanGameResult
        {
            Report = report,
            GeneratedReport = generatedReport,
            Duration = stopwatch.Elapsed,
            Errors = errors
        };
    }

    /// <inheritdoc/>
    public async Task<ScanGameResult> ScanAndWriteReportAsync(
        ScanGameConfiguration configuration,
        string reportPath,
        IProgress<ScanGameProgress>? progress = null,
        CancellationToken cancellationToken = default)
    {
        var result = await ScanAsync(configuration, progress, cancellationToken).ConfigureAwait(false);

        if (result.GeneratedReport != null && result.GeneratedReport.HasContent)
        {
            var reportContent = string.Join(Environment.NewLine, result.GeneratedReport.Lines);
            await _fileIO.WriteFileAsync(reportPath, reportContent, cancellationToken).ConfigureAwait(false);
        }

        return result;
    }

    private Dictionary<string, Func<Task<object?>>> BuildScannerTasks(
        ScanGameConfiguration config,
        CancellationToken ct)
    {
        var tasks = new Dictionary<string, Func<Task<object?>>>();

        // Unpacked mod files scanner
        if (config.ScanUnpacked && !string.IsNullOrEmpty(config.ModPath))
        {
            tasks["Unpacked"] = async () => await _unpackedScanner
                .ScanAsync(config.ModPath, config.XseScriptFiles, config.AnalyzeDdsTextures, ct)
                .ConfigureAwait(false);
        }

        // BA2 archive scanner
        if (config.ScanArchives && !string.IsNullOrEmpty(config.ModPath))
        {
            tasks["Archives"] = async () => await _ba2Scanner
                .ScanAsync(config.ModPath, config.XseScriptFolders, ct)
                .ConfigureAwait(false);
        }

        // INI configuration validator
        if (config.ValidateIni && !string.IsNullOrEmpty(config.GameRootPath))
        {
            var gameName = config.GameType.GetRegistryKeyName();
            tasks["INI"] = async () => await _iniValidator
                .ScanAsync(config.GameRootPath, gameName, ct)
                .ConfigureAwait(false);
        }

        // TOML crash generator config validator
        if (config.ValidateToml &&
            !string.IsNullOrEmpty(config.XsePluginsPath) &&
            !string.IsNullOrEmpty(config.CrashGenName))
        {
            var gameName = config.GameType.GetRegistryKeyName();
            tasks["TOML"] = async () => await _tomlValidator
                .ValidateAsync(config.XsePluginsPath, config.CrashGenName, gameName, ct)
                .ConfigureAwait(false);
        }

        // XSE installation integrity checker
        if (config.CheckXse && config.XseConfiguration != null)
        {
            tasks["XSE"] = async () => await _xseChecker
                .CheckIntegrityAsync(config.XseConfiguration, ct)
                .ConfigureAwait(false);
        }

        // Game installation integrity checker
        if (config.CheckGameIntegrity && config.GameIntegrityConfiguration != null)
        {
            tasks["GameIntegrity"] = async () => await _integrityChecker
                .CheckIntegrityAsync(config.GameIntegrityConfiguration, ct)
                .ConfigureAwait(false);
        }

        return tasks;
    }

    private async Task<Dictionary<string, object?>> ExecuteScannersAsync(
        Dictionary<string, Func<Task<object?>>> scannerTasks,
        List<ScannerError> errors,
        IProgress<ScanGameProgress>? progress,
        int totalScanners,
        CancellationToken ct)
    {
        var results = new Dictionary<string, object?>();

        if (scannerTasks.Count == 0)
        {
            _logger.LogDebug("No scanners enabled, skipping execution");
            return results;
        }

        var completedCount = 0;

        // Create tasks that capture their scanner name
        var runningTasks = scannerTasks.Select(kvp =>
            RunScannerAsync(kvp.Key, kvp.Value, ct)).ToList();

        // Process as they complete
        while (runningTasks.Count > 0)
        {
            var completedTask = await Task.WhenAny(runningTasks).ConfigureAwait(false);
            runningTasks.Remove(completedTask);

            var (name, result, error) = await completedTask.ConfigureAwait(false);

            if (error != null)
            {
                errors.Add(error);
                _logger.LogError(error.Exception, "Scanner '{ScannerName}' failed: {ErrorMessage}", name, error.ErrorMessage);
            }
            else
            {
                results[name] = result;
                _logger.LogDebug("Scanner '{ScannerName}' completed successfully", name);
            }

            completedCount++;
            var percentComplete = totalScanners > 0
                ? (int)((completedCount / (double)totalScanners) * 100)
                : 100;

            progress?.Report(new ScanGameProgress(
                $"Completed {name} scan",
                percentComplete,
                completedCount,
                totalScanners));
        }

        return results;
    }

    private static async Task<(string Name, object? Result, ScannerError? Error)> RunScannerAsync(
        string name,
        Func<Task<object?>> scanner,
        CancellationToken ct)
    {
        try
        {
            var result = await scanner().ConfigureAwait(false);
            return (name, result, null);
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            return (name, null, new ScannerError(name, "Operation was cancelled"));
        }
        catch (Exception ex)
        {
            return (name, null, new ScannerError(name, ex.Message, ex));
        }
    }

    private static ScanGameReport BuildReport(
        ScanGameConfiguration config,
        Dictionary<string, object?> results)
    {
        return new ScanGameReport
        {
            XseAcronym = config.XseAcronym,
            GameName = config.GameDisplayName,
            ScanTimestamp = DateTimeOffset.Now,
            UnpackedResult = results.TryGetValue("Unpacked", out var unpacked)
                ? unpacked as UnpackedScanResult
                : null,
            ArchivedResult = results.TryGetValue("Archives", out var archived)
                ? archived as BA2ScanResult
                : null,
            IniResult = results.TryGetValue("INI", out var ini)
                ? ini as IniScanResult
                : null,
            TomlResult = results.TryGetValue("TOML", out var toml)
                ? toml as TomlScanResult
                : null,
            XseResult = results.TryGetValue("XSE", out var xse)
                ? xse as XseScanResult
                : null,
            IntegrityResult = results.TryGetValue("GameIntegrity", out var integrity)
                ? integrity as GameIntegrityResult
                : null
        };
    }
}
