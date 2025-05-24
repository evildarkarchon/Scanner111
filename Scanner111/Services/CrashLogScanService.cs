using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Scanner111.Models;
using Scanner111.Services.Interfaces;

namespace Scanner111.Services;

/// <summary>
///     Implementation of crash log scanning service.
/// </summary>
public partial class CrashLogScanService(
    ILogger<CrashLogScanService> logger,
    IYamlSettingsCache yamlSettings,
    IGameContextService gameContext,
    ICrashLogFileService fileService,
    IDatabaseService databaseService,
    IModDetectionService modDetectionService,
    IReportWriterService reportWriter)
    : ICrashLogScanService
{
// Constants for segment headers and other identifiable strings
    private const string SystemSpecsHeader = "SYSTEM SPECS:";
    private const string ProbableCallStackHeader = "PROBABLE CALL STACK:";
    private const string ModulesHeader = "MODULES:";
    private const string PluginsHeader = "PLUGINS:";
    private const string Fallout4Prefix = "Fallout 4:";
    private const string SkyrimPrefix = "Skyrim:";
    private const string ExceptionIndicator = "EXCEPTION_";
    private const string F4SeIndicator = "f4se";
    private const string SkseIndicator = "skse";

    /// <summary>
    ///     Extracts plugins from plugin segment lines.
    /// </summary>
    private const string DefaultLoadOrder = "FF";

    private readonly SemaphoreSlim _initializationSemaphore = new(1, 1);

    private readonly Regex? _pluginSearchRegex = InitializePluginSearchRegex();

    private ILogCache? _crashLogs;
    private bool _disposed;
    private string _gameFilesResult = string.Empty;
    private bool _initialized;
    private string _mainFilesResult = string.Empty;
    private ScanStatistics _statistics = new();
    private ScanLogInfo? _yamlData;

    // Initialize regex pattern for plugin search

    /// <summary>
    /// Asynchronously initializes the crash log scan service by loading settings, retrieving and reformatting
    /// crash log files, and populating the log cache.
    /// </summary>
    /// <returns>
    /// A task representing the asynchronous initialization operation.
    /// </returns>
    public async Task InitializeAsync()
    {
        await _initializationSemaphore.WaitAsync();
        try
        {
            if (_initialized) return;

            logger.LogInformation("Initializing crash log scan service");

            // Load scan log info from settings
            _yamlData = ScanLogInfo.LoadFromSettings(yamlSettings, gameContext);

            // Get crash log files
            var crashLogFiles = await fileService.GetCrashLogFilesAsync(); // Get remove list for log reformatting
            var removeList = yamlSettings.GetSetting<List<string>>(YamlStore.Main, "exclude_log_records") ?? [];

            // Reformat crash logs
            await fileService.ReformatCrashLogsAsync(crashLogFiles, removeList);

            // Initialize log cache
            _crashLogs = new ThreadSafeLogCache(crashLogFiles);

            logger.LogInformation("Initialized log cache with {Count} log files", crashLogFiles.Count);

            _initialized = true;
            logger.LogInformation("Scan service initialization complete. Found {LogCount} crash logs",
                crashLogFiles.Count);
        }
        finally
        {
            _initializationSemaphore.Release();
        }
    }

    /// <summary>
    /// Processes all available crash logs asynchronously and gathers the corresponding
    /// processing results alongside statistical summaries detailing the scan process.
    /// </summary>
    /// <returns>
    /// A task that resolves to a tuple containing a list of crash log processing results
    /// and an object representing the aggregated scan statistics.
    /// </returns>
    public async Task<(List<CrashLogProcessResult> Results, ScanStatistics Statistics)> ProcessAllCrashLogsAsync()
    {
        if (!await CheckAndInitializeAsync())
        {
            logger.LogError("Failed to initialize crash log service");
            return ([], new ScanStatistics());
        }

        logger.LogInformation("Starting crash log processing");
        var stopwatch = Stopwatch.StartNew();

        var logFiles = _crashLogs?.GetLogNames();
        _statistics = new ScanStatistics();

        // Process all logs in parallel
        var results = await ProcessLogsInParallelAsync(logFiles);

        // Update statistics based on results
        UpdateStatistics(results);

        stopwatch.Stop();
        logger.LogInformation("Completed crash log processing in {Time} seconds", stopwatch.Elapsed.TotalSeconds);

        // Write combined results
        await reportWriter.WriteCombinedResultsAsync(results, _statistics);

        return (results, _statistics);
    }

    /// <summary>
    /// Processes a single crash log file and analyzes its content to generate a result.
    /// </summary>
    /// <param name="logFileName">
    /// The file path of the crash log to be processed.
    /// </param>
    /// <returns>
    /// A <see cref="CrashLogProcessResult"/> containing the results of the crash log analysis.
    /// </returns>
    public async Task<CrashLogProcessResult> ProcessSingleCrashLogAsync(string logFileName)
    {
        try
        {
            await EnsureInitializedAsync();
            logger.LogDebug("Processing crash log: {LogFile}", logFileName);

            // Create report builder and read crash data
            var reportBuilder = new CrashReportBuilder { LogFileName = logFileName };
            reportBuilder.AddStatistic("scanned", 1);

            var crashData = await ReadCrashLogDataAsync(logFileName, reportBuilder);
            if (reportBuilder.ScanFailed)
                return reportBuilder.BuildResult();

            // Add header to report
            AddReportHeader(reportBuilder, logFileName);

            // Process crash log content
            var segments = await FindSegmentsAsync(crashData, _yamlData!.CrashgenName);

            // Validate log completeness
            ValidateLogCompleteness(reportBuilder, segments, crashData);

            // FCX mode processing
            await ProcessFcxModeAsync(reportBuilder);

            // Extract plugins from segments
            var pluginsMap = ExtractPluginsFromSegment(segments.PluginsSegment);

            // Process errors and detect suspects
            ProcessErrorsAndDetectSuspects(reportBuilder, segments);

            // Analyze FormIDs if needed
            await AnalyzeFormIdsIfEnabledAsync(segments.CallStackSegment, pluginsMap, reportBuilder);

            // Detect problematic mods
            DetectProblematicMods(reportBuilder, pluginsMap, segments, _yamlData);

            // Add version information
            AddVersionInformation(reportBuilder, segments);

            // Analyze call stack
            await AnalyzeCallStackIfAvailableAsync(segments.CallStackSegment, pluginsMap, reportBuilder);

            // Move unsolved logs and add summary
            await FinalizeScanAsync(logFileName, reportBuilder);

            var result = reportBuilder.BuildResult();
            await reportWriter.WriteReportToFileAsync(logFileName, result.Report, false);
            return result;
        }
        catch (Exception ex)
        {
            return await HandleProcessingExceptionAsync(logFileName, ex);
        }
    }

    /// <summary>
    /// Analyzes crash log data and identifies specific segments based on the provided crash generation name.
    /// </summary>
    /// <param name="crashData">A list of strings representing the lines of the crash log data.</param>
    /// <param name="crashgenName">The name of the crash generation section to aid in segmentation.</param>
    /// <returns>A task representing the asynchronous operation, with a result of <see cref="CrashLogSegments"/> containing the parsed segments of the crash log.</returns>
    public Task<CrashLogSegments> FindSegmentsAsync(List<string> crashData, string crashgenName)
    {
        return Task.Run(() =>
        {
            var segments = new CrashLogSegments();
            var currentState = SegmentParserState.None;

            var crashgenSegment = new List<string>();
            var systemSegment = new List<string>();
            var callStackSegment = new List<string>();
            var allModulesSegment = new List<string>();
            var xseModulesSegment = new List<string>();
            var pluginsSegment = new List<string>();

            foreach (var line in crashData.Where(line => !TryProcessControlLine(line, crashgenName, ref segments,
                         ref currentState)))
            {
                // If not a control line, add it to the current segment's content list
                switch (currentState)
                {
                    case SegmentParserState.Crashgen:
                        crashgenSegment.Add(line);
                        break;
                    case SegmentParserState.System:
                        systemSegment.Add(line);
                        break;
                    case SegmentParserState.CallStack:
                        callStackSegment.Add(line);
                        break;
                    case SegmentParserState.Modules:
                        allModulesSegment.Add(line);
                        if (line.Contains(F4SeIndicator) || line.Contains(SkseIndicator)) xseModulesSegment.Add(line);
                        break;
                    case SegmentParserState.Plugins:
                        pluginsSegment.Add(line);
                        break;
                    // case SegmentParserState.None:
                    // Lines encountered before any segment is identified (and not control lines) are ignored.
                    // This matches the original logic where boolean flags would all be false.
                }
            }

            // Populate the final segments object
            return segments with
            {
                CrashgenSegment = crashgenSegment,
                SystemSegment = systemSegment,
                CallStackSegment = callStackSegment,
                AllModulesSegment = allModulesSegment,
                XseModulesSegment = xseModulesSegment,
                PluginsSegment = pluginsSegment
            };
        });
    }

    /// <summary>
    /// Retrieves the current scan statistics, providing insights into the ongoing or completed crash log scanning process.
    /// </summary>
    /// <returns>
    /// The current scan statistics as a <see cref="ScanStatistics"/> object.
    /// </returns>
    public ScanStatistics GetStatistics()
    {
        return _statistics;
    }

    /// <summary>
    ///     Disposes managed resources.
    /// </summary>
    public void Dispose()
    {
        if (!_disposed)
        {
            _crashLogs?.Dispose();
            _initializationSemaphore.Dispose();
            _disposed = true;
        }

        GC.SuppressFinalize(this);
    }

    /// <summary>
    /// Asynchronously checks the initialization state of the crash log scanning service
    /// and performs initialization if it has not been completed.
    /// </summary>
    /// <returns>
    /// A task representing the asynchronous check and initialization operation, returning a boolean value
    /// indicating whether the service is successfully initialized.
    /// </returns>
    private async Task<bool> CheckAndInitializeAsync()
    {
        if (!_initialized || _yamlData == null || _crashLogs == null) await InitializeAsync();

        return _crashLogs != null && _yamlData != null;
    }

    /// <summary>
    /// Processes a collection of crash log files in parallel, allowing for a maximum concurrency level
    /// based on the number of available processors, and returns the results for each processed log.
    /// </summary>
    /// <param name="logFiles">
    /// A collection of names of crash log files to be processed.
    /// </param>
    /// <returns>
    /// A task representing the asynchronous operation, containing a list of <see cref="CrashLogProcessResult"/>
    /// for each processed log file.
    /// </returns>
    private async Task<List<CrashLogProcessResult>> ProcessLogsInParallelAsync(IEnumerable<string>? logFiles)
    {
        var maxConcurrency = Environment.ProcessorCount;
        var throttler = new SemaphoreSlim(maxConcurrency, maxConcurrency);

        // ReSharper disable once UseCollectionExpression
        var processingTasks = (logFiles ?? Array.Empty<string>()).Select(async logFile =>
        {
            await throttler.WaitAsync();
            try
            {
                return await ProcessSingleCrashLogAsync(logFile);
            }
            finally
            {
                throttler.Release();
            }
        });

        var results = await Task.WhenAll(processingTasks);
        return results.ToList();
    }

    /// <summary>
    /// Updates the scan statistics based on the results of crash log processing.
    /// </summary>
    /// <param name="results">
    /// A list of <see cref="CrashLogProcessResult"/> objects representing the results
    /// of crash log processing.
    /// </param>
    private void UpdateStatistics(List<CrashLogProcessResult> results)
    {
        _statistics.Scanned = results.Count;
        _statistics.Failed = results.Count(r => r.ScanFailed);
        _statistics.Incomplete = results.Count(r => r.Statistics.GetValueOrDefault("incomplete", 0) > 0);
        _statistics.FailedFiles.AddRange(results.Where(r => r.ScanFailed).Select(r => r.LogFileName));
    }

    /// <summary>
    /// Ensures that the crash log scan service is initialized by verifying the internal state
    /// and invoking the initialization process if needed.
    /// </summary>
    /// <returns>
    /// A task representing the completion of the initialization verification operation.
    /// </returns>
    private async Task EnsureInitializedAsync()
    {
        if (!_initialized || _yamlData == null || _crashLogs == null)
            await InitializeAsync();

        if (_crashLogs == null)
            throw new InvalidOperationException("Log cache is not initialized");
    }

    /// <summary>
    /// Reads the crash log data from the specified log file and updates the provided report builder
    /// with error information if the log file is empty or inaccessible.
    /// </summary>
    /// <param name="logFileName">The name of the crash log file to be read.</param>
    /// <param name="reportBuilder">The report builder used to log scan results and statistics.</param>
    /// <returns>
    /// A task representing the asynchronous operation, containing the list of strings
    /// representing the content of the crash log file.
    /// </returns>
    private async Task<List<string>> ReadCrashLogDataAsync(string logFileName, CrashReportBuilder reportBuilder)
    {
        var crashData = await _crashLogs!.ReadLogAsync(logFileName);

        if (crashData.Count == 0)
            reportBuilder.AddSection("Error", "ERROR: Empty or inaccessible crash log file")
                .AddStatistic("failed", 1)
                .ScanFailed = true;

        return crashData;
    }

    /// <summary>
    /// Adds a header section to the crash report, including the log file name, autoscan report message, and supplementary information.
    /// </summary>
    /// <param name="reportBuilder">The instance of <see cref="CrashReportBuilder"/> used to build the crash report.</param>
    /// <param name="logFileName">The name of the crash log file being processed.</param>
    private void AddReportHeader(CrashReportBuilder reportBuilder, string logFileName)
    {
        var headerText =
            $"{logFileName} -> AUTOSCAN REPORT {(_yamlData != null ? $"GENERATED BY {_yamlData.ClassicVersion}" : "")}";

        reportBuilder.AddSection("Header",
            headerText,
            "",
            "# FOR BEST VIEWING EXPERIENCE OPEN THIS FILE IN NOTEPAD++ OR SIMILAR #",
            "# PLEASE READ EVERYTHING CAREFULLY AND BEWARE OF FALSE POSITIVES #",
            "====================================================");
    }

    /// <summary>
    /// Validates the completeness of the crash log by checking for the presence of essential segments
    /// and the overall length of the log data. Updates the report builder with warnings and statistics
    /// if the crash log lacks necessary information.
    /// </summary>
    /// <param name="reportBuilder">
    /// The report builder instance used for aggregating results and adding warnings or statistics
    /// about the completeness of the crash log.
    /// </param>
    /// <param name="segments">
    /// The extracted segments of the crash log, containing information such as plugins and call stack segments.
    /// </param>
    /// <param name="crashData">
    /// The raw crash log content represented as a list of strings.
    /// </param>
    private void ValidateLogCompleteness(CrashReportBuilder reportBuilder, CrashLogSegments segments,
        List<string> crashData)
    {
        if (segments.PluginsSegment.Count == 0)
        {
            reportBuilder.AddSection("Warnings", "⚠️ NO PLUGINS FOUND IN CRASH LOG ⚠️");
            reportBuilder.AddStatistic("incomplete", 1);
        }

        if (crashData.Count >= 20) return;
        reportBuilder.AddToSection("Warnings", "⚠️ CRASH LOG APPEARS TO BE INCOMPLETE ⚠️");
        reportBuilder.AddStatistic("incomplete", 1);
    }

    /// <summary>
    /// Asynchronously processes the FCX (Fault Context eXtraction) mode by checking the settings,
    /// performing necessary validations, and updating the crash report with relevant FCX results.
    /// </summary>
    /// <param name="reportBuilder">
    /// The crash report builder instance used to update report sections with FCX processing results.
    /// </param>
    /// <returns>
    /// A task representing the asynchronous FCX mode processing operation.
    /// </returns>
    private async Task ProcessFcxModeAsync(CrashReportBuilder reportBuilder)
    {
        var fcxMode = yamlSettings.GetSetting<bool?>(YamlStore.Main, "FCX Mode");
        await CheckFcxModeAsync(fcxMode ?? false);

        if (!string.IsNullOrEmpty(_mainFilesResult))
            reportBuilder.AddSection("FCX Results", _mainFilesResult);

        if (!string.IsNullOrEmpty(_gameFilesResult))
            reportBuilder.AddToSection("FCX Results", _gameFilesResult);
    }

    /// <summary>
    /// Processes errors within the crash log, identifies potential suspects based on error patterns
    /// and call stacks, and updates the crash report with findings.
    /// </summary>
    /// <param name="reportBuilder">
    /// An instance of CrashReportBuilder used to record findings and generate the crash report.
    /// </param>
    /// <param name="segments">
    /// An instance of CrashLogSegments containing parsed segments of the crash log, such as main errors and call stack data.
    /// </param>
    private void ProcessErrorsAndDetectSuspects(CrashReportBuilder reportBuilder, CrashLogSegments segments)
    {
        var suspectFound = false;

        if (!string.IsNullOrEmpty(segments.MainError))
            suspectFound = ScanSuspectMainError(reportBuilder, segments.MainError);

        var suspectStackFound =
            ScanSuspectStack(segments.MainError, string.Join("\n", segments.CallStackSegment), reportBuilder);
        suspectFound = suspectFound || suspectStackFound;

        if (!suspectFound)
            reportBuilder.AddSection("Suspects",
                "# ℹ️ No suspect patterns found in main error or call stack # \n-----\n");
    }

    /// <summary>
    /// Asynchronously analyzes FormIDs in the call stack segment if enabled in the settings
    /// and the database exists.
    /// </summary>
    /// <param name="callStackSegment">The list of strings representing the call stack segment to be analyzed.</param>
    /// <param name="pluginsMap">A dictionary mapping plugin names to their corresponding master files or identifiers.</param>
    /// <param name="reportBuilder">The crash report builder used to record analysis results and report data.</param>
    /// <returns>
    /// A task representing the asynchronous FormID analysis operation.
    /// </returns>
    private async Task AnalyzeFormIdsIfEnabledAsync(List<string> callStackSegment,
        Dictionary<string, string> pluginsMap, CrashReportBuilder reportBuilder)
    {
        var showFormIdValues = yamlSettings.GetSetting<bool?>(YamlStore.Main, "Show FormID Values");
        if (showFormIdValues.HasValue && showFormIdValues.Value && databaseService.DatabaseExists())
            await AnalyzeFormIdsAsync(callStackSegment, pluginsMap, reportBuilder);
    }

    /// <summary>
    /// Detects known problematic mods, conflicting mod combinations, and important mods
    /// based on provided data and adds the findings to the crash report.
    /// </summary>
    /// <param name="reportBuilder">The report builder used for constructing the crash report.</param>
    /// <param name="pluginsMap">A dictionary mapping plugin names to their respective file paths.</param>
    /// <param name="segments">Segments extracted from the crash log, containing various system and crash-related details.</param>
    /// <param name="yamlData">Data loaded from the YAML settings cache, specifying known mods and configurations.</param>
    private void DetectProblematicMods(CrashReportBuilder reportBuilder, Dictionary<string, string> pluginsMap,
        CrashLogSegments segments, ScanLogInfo yamlData)
    {
        if (pluginsMap.Count <= 0) return;

        reportBuilder.AddSection("Problematic Mods", "# CHECKING FOR KNOWN PROBLEMATIC MODS #");

        // Detect single problematic mods
        var singleModsFound = modDetectionService.DetectModsSingle(yamlData.GameModsFreq, pluginsMap, reportBuilder);

        // Detect conflicting mod combinations
        var conflictingModsFound =
            modDetectionService.DetectModsDouble(yamlData.GameModsConf, pluginsMap, reportBuilder);

        // Extract GPU info and detect important mods
        var gpuRival = ExtractGpuInfo(segments.SystemSegment);
        reportBuilder.AddSection("Important Mods", "# CHECKING FOR IMPORTANT MODS #");
        modDetectionService.DetectModsImportant(yamlData.GameModsCore, pluginsMap, reportBuilder, gpuRival);

        if (!singleModsFound && !conflictingModsFound)
            reportBuilder.AddToSection("Problematic Mods", "# ℹ️ No known problematic mods detected #");
    }

    /// <summary>
    /// Extracts GPU information from the provided system segment data.
    /// </summary>
    /// <param name="systemSegment">The list of system-related information extracted from the crash log.</param>
    /// <returns>
    /// A string indicating the detected GPU type ("nvidia", "amd", or an empty string if no matching GPU information was found).
    /// </returns>
    private string ExtractGpuInfo(List<string> systemSegment)
    {
        var gpuInfo = systemSegment.FirstOrDefault(line =>
            line.Contains("GPU:", StringComparison.OrdinalIgnoreCase)) ?? string.Empty;

        if (gpuInfo.Contains("NVIDIA", StringComparison.OrdinalIgnoreCase))
            return "nvidia";

        if (gpuInfo.Contains("AMD", StringComparison.OrdinalIgnoreCase) ||
            gpuInfo.Contains("Radeon", StringComparison.OrdinalIgnoreCase))
            return "amd";

        return string.Empty;
    }

    /// <summary>
    /// Adds version information to the crash report based on the detected crash log metadata and the predefined settings.
    /// </summary>
    /// <param name="reportBuilder">
    /// The builder instance used to construct the crash report.
    /// </param>
    /// <param name="segments">
    /// The parsed segments of the crash log containing metadata such as the detected version and main error.
    /// </param>
    private void AddVersionInformation(CrashReportBuilder reportBuilder, CrashLogSegments segments)
    {
        var versionCurrent = ParseVersion(segments.Crashgen);
        var versionLatest = ParseVersion(_yamlData!.CrashgenLatestOg);
        var versionLatestVr = ParseVersion(_yamlData.CrashgenLatestVr);

        var versionStatus = IsLatestVersion(versionCurrent, versionLatest, versionLatestVr)
            ? $"* You have the latest version of {_yamlData.CrashgenName}! *"
            : $"{_yamlData.WarnOutdated}";

        reportBuilder.AddSection("Version Info",
            $"Main Error: {segments.MainError}",
            $"Detected {_yamlData.CrashgenName} Version: {segments.Crashgen}",
            versionStatus);
    }

    /// <summary>
    /// Analyzes the call stack segment if available and appends the analysis results to the crash report.
    /// </summary>
    /// <param name="callStackSegment">A list of strings representing the call stack segment of the crash log.</param>
    /// <param name="pluginsMap">A dictionary mapping plugin names to their versions or sources.</param>
    /// <param name="reportBuilder">The builder responsible for constructing the crash report.</param>
    /// <returns>
    /// A task that represents the asynchronous operation of analyzing the call stack,
    /// which updates the provided report builder with analysis results, if applicable.
    /// </returns>
    private async Task AnalyzeCallStackIfAvailableAsync(List<string> callStackSegment,
        Dictionary<string, string> pluginsMap, CrashReportBuilder reportBuilder)
    {
        if (callStackSegment.Count > 0)
        {
            reportBuilder.AddSection("Call Stack", "## Call Stack Analysis", "");
            await AnalyzeCallStackAsync(callStackSegment, pluginsMap, reportBuilder);
        }
    }

    /// <summary>
    /// Finalizes the scanning process for a given crash log by performing final steps such as moving unsolved logs
    /// and appending a summary report section. Updates scan statistics as well.
    /// </summary>
    /// <param name="logFileName">The name of the crash log file being processed.</param>
    /// <param name="reportBuilder">The report builder containing information and results of the scan process.</param>
    /// <returns>
    /// A task that represents the asynchronous operation of finalizing the scanning process.
    /// </returns>
    private async Task FinalizeScanAsync(string logFileName, CrashReportBuilder reportBuilder)
    {
        try
        {
            await MoveUnsolvedLogsAsync(logFileName, reportBuilder.Build());

            reportBuilder.AddSection("Summary",
                "## SCAN SUMMARY ##",
                "",
                "This analysis provides possible crash causes based on known patterns and mod detection.",
                "Verify suggested solutions and consult the community for help if needed.",
                "",
                $"Analysis completed on: {DateTime.Now:yyyy-MM-dd HH:mm:ss}");

            reportBuilder.AddStatistic("success", 1);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error during final analysis steps for {LogFile}", logFileName);
            reportBuilder.AddSection("Error", $"⚠️ ERROR DURING FINAL ANALYSIS: {ex.Message}");
        }
    }

    /// <summary>
    /// Handles exceptions that occur during the processing of a crash log, logs them, and generates a failure report.
    /// </summary>
    /// <param name="logFileName">The name of the log file being processed when the exception occurred.</param>
    /// <param name="ex">The exception encountered during the crash log processing.</param>
    /// <returns>
    /// A task that represents the asynchronous operation. The task result contains a <see cref="CrashLogProcessResult"/>
    /// with details of the processed crash log, including the failure information.
    /// </returns>
    private async Task<CrashLogProcessResult> HandleProcessingExceptionAsync(string logFileName, Exception ex)
    {
        logger.LogError(ex, "Error processing crash log {LogFile}", logFileName);

        var reportBuilder = new CrashReportBuilder
        {
            LogFileName = logFileName,
            ScanFailed = true
        };

        reportBuilder.AddSection("Error", $"Error processing {logFileName}: {ex.Message}")
            .AddStatistic("failed", 1);

        var result = reportBuilder.BuildResult();
        await reportWriter.WriteReportToFileAsync(logFileName, result.Report, true);

        return result;
    }

    /// <summary>
    /// Attempts to process a control line from the crash log to determine if it matches specific known headers or
    /// markers, updating the current parsing state and crash log segments accordingly.
    /// </summary>
    /// <param name="line">The current line being processed from the crash log.</param>
    /// <param name="crashgenName">The identifier indicating the crash generation section of the log.</param>
    /// <param name="segments">
    /// A reference to the crash log segments object, which is updated with extracted data if the line is successfully processed.
    /// </param>
    /// <param name="currentState">
    /// A reference to the current parser state, which is updated based on the processing of the control line.
    /// </param>
    /// <returns>
    /// A boolean value indicating whether the line was identified and processed as a control line.
    /// Returns true if the line was consumed as a valid control line, otherwise false.
    /// </returns>
    private bool TryProcessControlLine(
        string line,
        string crashgenName,
        ref CrashLogSegments segments, // Assuming CrashLogSegments is a struct or uses 'with' on a class
        ref SegmentParserState currentState)
    {
        if (line.StartsWith(crashgenName))
        {
            segments = segments with { Crashgen = line };
            currentState = SegmentParserState.Crashgen;
            return true; // Line consumed, continue to next line
        }

        if (line.StartsWith(Fallout4Prefix) || line.StartsWith(SkyrimPrefix))
        {
            segments = segments with { GameVersion = line };
            return true; // Line consumed
        }

        if (string.IsNullOrEmpty(segments.MainError) && line.Contains(ExceptionIndicator))
        {
            segments = segments with { MainError = line };
            return true; // Line consumed
        }

        // Check for segment headers
        if (line.Contains(SystemSpecsHeader))
        {
            currentState = SegmentParserState.System;
            return true; // Line consumed (it's a header)
        }

        if (line.Contains(ProbableCallStackHeader))
        {
            currentState = SegmentParserState.CallStack;
            return true; // Line consumed
        }

        if (line.Contains(ModulesHeader))
        {
            currentState = SegmentParserState.Modules;
            return true; // Line consumed
        }

        if (!line.Contains(PluginsHeader))
            return false; // Line is not a control/header line, should be processed as content
        currentState = SegmentParserState.Plugins;
        return true; // Line consumed
    }

    /// <summary>
    /// Extracts plugin names and their corresponding load orders from a specified segment of crash log data.
    /// </summary>
    /// <param name="pluginSegment">
    /// A list of strings representing the segment of the crash log that contains plugin information.
    /// </param>
    /// <returns>
    /// A dictionary where the keys are plugin names and the values are their respective load orders.
    /// </returns>
    private Dictionary<string, string> ExtractPluginsFromSegment(List<string> pluginSegment)
    {
        var result = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        if (_pluginSearchRegex == null) return result;

        foreach (var line in pluginSegment)
        {
            var match = _pluginSearchRegex.Match(line);
            if (!match.Success || match.Groups.Count <= 1) continue;

            var pluginName = match.Groups[1].Value.Trim();
            if (string.IsNullOrEmpty(pluginName)) continue;

            var loadOrder = ExtractLoadOrder(line);
            result[pluginName] = loadOrder;
        }

        return result;
    }

    /// <summary>
    /// Extracts the load order value enclosed in square brackets from the provided log line.
    /// If no valid load order is found, returns a default value.
    /// </summary>
    /// <param name="line">The log line from which to extract the load order value.</param>
    /// <returns>The extracted load order as a string, or a default value if none is found.</returns>
    private string ExtractLoadOrder(string line)
    {
        var bracketStart = line.IndexOf('[');
        var bracketEnd = line.IndexOf(']');

        if (bracketStart >= 0 && bracketEnd > bracketStart)
            return line.Substring(bracketStart + 1, bracketEnd - bracketStart - 1).Trim();

        return DefaultLoadOrder;
    }

    /// <summary>
    /// Parses a version string into a Version object.
    /// </summary>
    /// <param name="versionString">
    /// The version string to parse, which should follow a pattern like "v1.2.3.4".
    /// </param>
    /// <returns>
    /// A Version object representing the parsed version. If the input string is null, empty,
    /// or does not match the expected pattern, a default version (0.0.0.0) is returned.
    /// </returns>
    private static Version ParseVersion(string versionString)
    {
        if (string.IsNullOrEmpty(versionString))
            return new Version(0, 0, 0, 0);

        // Extract version from string (look for pattern like "v1.2.3.4")
        var match = ParseVersionString().Match(versionString);
        if (match.Success && Version.TryParse(match.Groups[1].Value, out var version)) return version;

        return new Version(0, 0, 0, 0);
    }

    /// <summary>
    /// Determines if the current version is the latest available version, considering multiple latest version criteria.
    /// </summary>
    /// <param name="current">The current version being checked.</param>
    /// <param name="latest">The main latest version to compare against.</param>
    /// <param name="latestVr">An alternative latest version to compare against.</param>
    /// <returns>
    /// A boolean value indicating whether the current version is the latest.
    /// Returns true if the current version is greater than or equal to either the main or alternative latest versions; otherwise, false.
    /// </returns>
    private static bool IsLatestVersion(Version current, Version latest, Version latestVr)
    {
        return current >= latest || current >= latestVr;
    }

    /// <summary>
    /// Processes a suspect stack signal to determine whether it matches specific error and call stack information.
    /// Updates the match status with relevant details about the processing result.
    /// </summary>
    /// <param name="signal">The suspect signal to process, which may include modifiers.</param>
    /// <param name="crashlogMainError">The main error from the crash log being analyzed.</param>
    /// <param name="callStack">The call stack from the crash log being analyzed.</param>
    /// <param name="matchStatus">A dictionary containing flags that indicate matching results for different processing criteria.</param>
    /// <returns>
    /// A boolean value indicating whether the signal processing should skip the current error.
    /// </returns>
    private bool ProcessSuspectStackSignals(string signal, string crashlogMainError, string callStack,
        Dictionary<string, bool> matchStatus)
    {
        const string mainErrorRequired = "ME-REQ";
        const string mainErrorOptional = "ME-OPT";
        const string callStackNegative = "NOT";

        if (!signal.Contains('|'))
        {
            // Simple string match in callstack
            if (callStack.Contains(signal, StringComparison.OrdinalIgnoreCase)) matchStatus["stack_found"] = true;

            return false;
        }

        var parts = signal.Split('|', 2);
        var signalModifier = parts[0];
        var signalString = parts[1];

        // Handle different types of signals
        if (signalModifier.Equals(mainErrorRequired, StringComparison.OrdinalIgnoreCase))
        {
            matchStatus["has_required_item"] = true;
            if (crashlogMainError.Contains(signalString, StringComparison.OrdinalIgnoreCase))
                matchStatus["error_req_found"] = true;
        }
        else if (signalModifier.Equals(mainErrorOptional, StringComparison.OrdinalIgnoreCase))
        {
            if (crashlogMainError.Contains(signalString, StringComparison.OrdinalIgnoreCase))
                matchStatus["error_opt_found"] = true;
        }
        else if (signalModifier.Equals(callStackNegative, StringComparison.OrdinalIgnoreCase))
        {
            // If NOT condition is found, we should skip this error
            if (callStack.Contains(signalString,
                    StringComparison.OrdinalIgnoreCase)) return true; // Signal to skip this error
        }
        else if (int.TryParse(signalModifier, out var count))
        {
            // Count the occurrences of signal in callstack
            var occurrences = Regex.Matches(callStack, Regex.Escape(signalString), RegexOptions.IgnoreCase).Count;
            if (occurrences >= count) matchStatus["stack_found"] = true;
        }

        return false;
    }

    /// <summary>
    /// Determines whether the suspect stack matches the defined conditions based on match status flags.
    /// </summary>
    /// <param name="matchStatus">A dictionary containing boolean values for various conditions such as presence of required items, stack match, and optional error match.</param>
    /// <returns>
    /// True if the match conditions are met; otherwise, false.
    /// </returns>
    private bool IsSuspectStackMatch(Dictionary<string, bool> matchStatus)
    {
        // If there are required items, they must be found
        if (matchStatus["has_required_item"] && !matchStatus["error_req_found"]) return false;

        // Must have either a stack match or an optional error match
        return matchStatus["stack_found"] || matchStatus["error_opt_found"];
    }

    /// <summary>
    /// Scans the call stack for suspect errors potentially linked to a crash.
    /// </summary>
    /// <param name="crashlogMainError">
    /// The main error message extracted from the crash log, used to identify potential issues.
    /// </param>
    /// <param name="callStack">
    /// The call stack extracted from the crash log, which is analyzed for suspect patterns.
    /// </param>
    /// <param name="reportBuilder">
    /// The <see cref="CrashReportBuilder"/> used to append findings about any detected suspects.
    /// </param>
    /// <returns>
    /// A boolean indicating whether any suspect errors were found in the call stack.
    /// </returns>
    private bool ScanSuspectStack(string crashlogMainError, string callStack, CrashReportBuilder reportBuilder)
    {
        if (_yamlData == null || string.IsNullOrEmpty(crashlogMainError) || string.IsNullOrEmpty(callStack))
            return false;

        var anySuspectFound = false;
        const int maxWarnLength = 40;

        foreach (var (errorKey, signalList) in _yamlData.SuspectsStackList)
        {
            var parts = errorKey.Split(" | ", 2);
            if (parts.Length != 2) continue;

            var errorSeverity = parts[0];
            var errorName = parts[1];

            // Initialize match status
            var matchStatus = new Dictionary<string, bool>
            {
                ["has_required_item"] = false,
                ["error_req_found"] = false,
                ["error_opt_found"] = false,
                ["stack_found"] = false
            };

            // Process each signal in the list
            var skipError = signalList.Any(signal =>
                ProcessSuspectStackSignals(signal, crashlogMainError, callStack, matchStatus));

            if (skipError) continue;

            // Check if we have a match
            if (!IsSuspectStackMatch(matchStatus)) continue;

            // Format warning
            var formattedErrorName = errorName.PadRight(maxWarnLength, '.');
            reportBuilder.AddToSection("Suspects",
                $"# Checking for {formattedErrorName} SUSPECT FOUND! > Severity : {errorSeverity} # ",
                "-----");
            anySuspectFound = true;
        }

        return anySuspectFound;
    }

    /// <summary>
    /// Scans the crash log's main error for suspect patterns and updates the report builder accordingly.
    /// </summary>
    /// <param name="reportBuilder">
    /// The instance of <see cref="CrashReportBuilder"/> used to document the discovered suspects.
    /// </param>
    /// <param name="crashlogMainError">
    /// The main error string extracted from the crash log for analysis.
    /// </param>
    /// <returns>
    /// A boolean indicating whether any suspect patterns were found in the main error string.
    /// </returns>
    private bool ScanSuspectMainError(CrashReportBuilder reportBuilder, string crashlogMainError)
    {
        if (_yamlData == null || string.IsNullOrEmpty(crashlogMainError)) return false;

        var foundSuspect = false;
        const int maxWarnLength = 40;

        foreach (var (errorKey, signal) in _yamlData.SuspectsErrorList)
        {
            if (!crashlogMainError.Contains(signal, StringComparison.OrdinalIgnoreCase)) continue;

            var parts = errorKey.Split(" | ", 2);
            if (parts.Length != 2) continue;

            var errorSeverity = parts[0];
            var errorName = parts[1];

            // Format the error name for report
            var formattedErrorName = errorName.PadRight(maxWarnLength, '.');

            // Add the error to the report
            reportBuilder.AddSection("Suspects",
                $"# Checking for {formattedErrorName} SUSPECT FOUND! > Severity : {errorSeverity} # ",
                "-----");

            foundSuspect = true;
        }

        return foundSuspect;
    }

    /// <summary>
    /// Asynchronously checks if FCX mode is enabled and performs related game file checks. Results of the checks
    /// are stored for further processing or reporting purposes.
    /// </summary>
    /// <param name="fcxMode">A boolean indicating whether FCX mode is enabled.</param>
    /// <returns>A task representing the asynchronous operation of checking FCX mode and performing game file checks.</returns>
    private async Task CheckFcxModeAsync(bool fcxMode)
    {
        // Use a lock to ensure thread safety
        await _initializationSemaphore.WaitAsync();
        try
        {
            if (fcxMode)
            {
                if (string.IsNullOrEmpty(_mainFilesResult) && string.IsNullOrEmpty(_gameFilesResult))
                {
                    // Run checks and store results
                    // In a real implementation, these would call the equivalent of Python's main_combined_result() and game_combined_result()
                    _mainFilesResult = await Task.FromResult("Main files check result would go here");
                    _gameFilesResult = await Task.FromResult("Game files check result would go here");
                }
            }
            else
            {
                _mainFilesResult = "❌ FCX Mode is disabled, skipping game files check... \n-----\n";
                _gameFilesResult = string.Empty;
            }
        }
        finally
        {
            _initializationSemaphore.Release();
        }
    }

    /// <summary>
    /// Asynchronously analyzes FormIDs found in a crash log's call stack for matches against the database,
    /// identifying their associated plugins and supplementing the crash report with the results.
    /// </summary>
    /// <param name="callStackSegment">The segment of the crash log call stack containing potential FormIDs.</param>
    /// <param name="pluginsMap">A dictionary mapping plugin names to their respective identifiers.</param>
    /// <param name="reportBuilder">An instance of <see cref="CrashReportBuilder"/> used to augment the crash report with FormID analysis data.</param>
    /// <returns>
    /// A task representing the asynchronous FormID analysis operation.
    /// </returns>
    private async Task AnalyzeFormIdsAsync(List<string> callStackSegment, Dictionary<string, string> pluginsMap,
        CrashReportBuilder reportBuilder)
    {
        if (callStackSegment.Count == 0 || pluginsMap.Count == 0 || !databaseService.DatabaseExists()) return;

        // FormID pattern in callstack: typically in format like "[formID]" or similar
        var formIdRegex = InitializeFormIdRegex();
        var formIdsFound = false;

        reportBuilder.AddSection("FormID Analysis", "# MATCHING FORMIDS FROM CALL STACK #");

        foreach (var formId in from line in callStackSegment
                 select formIdRegex.Matches(line)
                 into matches
                 from Match match in matches
                 where match.Success && match.Groups.Count >= 2
                 select match.Groups[1].Value
                 into formId
                 where !string.IsNullOrEmpty(formId)
                 select formId)
            // Try to find which plugin this FormID belongs to
        foreach (var (plugin, _) in pluginsMap)
        {
            var entry = await databaseService.GetEntryAsync(formId, plugin);
            if (string.IsNullOrEmpty(entry)) continue;
            reportBuilder.AddToSection("FormID Analysis", $"🔍 FormID [{formId}] matched to {plugin}: {entry}");
            formIdsFound = true;
            break; // Found a match, no need to check other plugins
        }

        if (!formIdsFound)
            reportBuilder.AddToSection("FormID Analysis",
                "❓ No FormIDs from call stack matched to plugins in database");
    }

    /// <summary>
    /// Asynchronously analyzes the call stack for relevant plugins and named records,
    /// and adds the results to the specified crash report builder.
    /// </summary>
    /// <param name="callStackSegment">A list of call stack entries to be analyzed.</param>
    /// <param name="pluginsMap">A dictionary mapping plugin identifiers to their associated metadata.</param>
    /// <param name="reportBuilder">The report builder instance used to document the analysis results.</param>
    /// <returns>
    /// A task representing the asynchronous call stack analysis operation.
    /// </returns>
    private async Task AnalyzeCallStackAsync(List<string> callStackSegment, Dictionary<string, string> pluginsMap,
        CrashReportBuilder reportBuilder)
    {
        if (callStackSegment.Count == 0)
            return;

        // 1. Plugin matching
        await AnalyzePluginsInCallStackAsync(callStackSegment, pluginsMap, reportBuilder);

        // 2. Named records matching
        await AnalyzeNamedRecordsAsync(callStackSegment, reportBuilder);
    }

    /// <summary>
    /// Analyzes the plugins referenced within a given call stack segment and updates the crash report
    /// with insights about potential plugin-related issues that could contribute to crashes.
    /// </summary>
    /// <param name="callStackSegment">A list of strings representing individual lines of a call stack segment.</param>
    /// <param name="pluginsMap">A dictionary mapping plugin names to associated metadata.</param>
    /// <param name="reportBuilder">An instance of CrashReportBuilder used to update the crash report with findings.</param>
    /// <returns>
    /// A task representing the asynchronous plugin analysis operation within the call stack.
    /// </returns>
    private Task AnalyzePluginsInCallStackAsync(List<string> callStackSegment, Dictionary<string, string> pluginsMap,
        CrashReportBuilder reportBuilder)
    {
        if (callStackSegment.Count == 0 || pluginsMap.Count == 0 || _yamlData == null)
            return Task.CompletedTask;

        // Get plugins ignore list from settings
        var pluginsIgnoreList = yamlSettings.GetSetting<List<string>>(YamlStore.Main, "game_ignore_plugins") ?? [];

        // Lowercase versions for comparison
        var ignorePluginsLower = pluginsIgnoreList.Select(p => p.ToLower()).ToHashSet();
        var pluginsMatchCount = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);

        // Lowercase all callstack lines for case-insensitive matching
        var callStackLower = callStackSegment.Select(line => line.ToLower()).ToList();

        // Check each plugin against each line in the call stack
        foreach (var plugin in pluginsMap.Keys)
        {
            // Skip plugins in the ignore list
            if (ignorePluginsLower.Contains(plugin.ToLower()))
                continue;

            var pluginLower = plugin.ToLower();
            var count = callStackLower.Count(line => !line.Contains("modified by:") && line.Contains(pluginLower));

            if (count > 0)
                pluginsMatchCount[plugin] = count;
        }

        // Report findings
        if (pluginsMatchCount.Count > 0)
        {
            reportBuilder.AddSection("Plugin Analysis", "The following PLUGINS were found in the CRASH STACK:");

            // Sort by occurrence count (descending), then by name for consistent output
            foreach (var (plugin, count) in pluginsMatchCount.OrderByDescending(p => p.Value).ThenBy(p => p.Key))
                reportBuilder.AddToSection("Plugin Analysis", $"- {plugin} | {count}");

            reportBuilder.AddToSection("Plugin Analysis",
                "\n[Last number counts how many times each Plugin Suspect shows up in the crash log.]",
                $"These Plugins were caught by {_yamlData.CrashgenName} and some of them might be responsible for this crash.",
                "You can try disabling these plugins and check if the game still crashes, though this method can be unreliable.\n");
        }
        else
        {
            reportBuilder.AddSection("Plugin Analysis", "* COULDN'T FIND ANY PLUGIN SUSPECTS *\n");
        }

        return Task.CompletedTask;
    }

    /// <summary>
    /// Analyzes named records within the provided call stack segment and appends findings to the crash report.
    /// </summary>
    /// <param name="callStackSegment">A list of strings representing the call stack segment to analyze.</param>
    /// <param name="reportBuilder">The crash report builder used to record the analysis results.</param>
    /// <returns>
    /// A task representing the asynchronous operation of analyzing named records.
    /// </returns>
    private Task AnalyzeNamedRecordsAsync(List<string> callStackSegment, CrashReportBuilder reportBuilder)
    {
        if (callStackSegment.Count == 0 || _yamlData == null)
            return Task.CompletedTask;

        // Get records list and ignore list from settings
        var recordsList = yamlSettings.GetSetting<List<string>>(YamlStore.Main, "classic_records_list") ?? [];
        var ignoreList = yamlSettings.GetSetting<List<string>>(YamlStore.Main, "game_ignore_records") ?? [];

        // Lowercase versions for case-insensitive comparison
        var recordsLower = recordsList.Select(r => r.ToLower()).ToHashSet();
        var ignoreLower = ignoreList.Select(r => r.ToLower()).ToHashSet();

        const string rspMarker = "[RSP+";
        const int rspOffset = 30;

        bool IsRelevantLogLine(string logLine)
        {
            var lineLower = logLine.ToLower();
            return recordsLower.Any(lineLower.Contains) && !ignoreLower.Any(lineLower.Contains);
        }

        string FormatMatchedLine(string matchedLine)
        {
            return matchedLine.Contains(rspMarker)
                ? matchedLine.Substring(Math.Min(matchedLine.Length, rspOffset)).Trim()
                : matchedLine.Trim();
        }

        var recordsMatches = callStackSegment
            .Where(IsRelevantLogLine)
            .Select(FormatMatchedLine)
            .ToList();

        // Find matching records in the call stack

        // Report findings
        if (recordsMatches.Count > 0)
        {
            reportBuilder.AddSection("Named Records Analysis", "# LIST OF DETECTED (NAMED) RECORDS #");

            // Count occurrences and sort them
            var recordsCount = recordsMatches
                .GroupBy(r => r)
                .ToDictionary(g => g.Key, g => g.Count())
                .OrderBy(kvp => kvp.Key);

            foreach (var (record, count) in recordsCount)
                reportBuilder.AddToSection("Named Records Analysis", $"- {record} | {count}");

            reportBuilder.AddToSection("Named Records Analysis",
                "\n[Last number counts how many times each Named Record shows up in the crash log.]",
                $"These records were caught by {_yamlData.CrashgenName} and some of them might be related to this crash.",
                "Named records should give extra info on involved game objects, record types or mod files.\n");
        }
        else
        {
            reportBuilder.AddSection("Named Records Analysis", "* COULDN'T FIND ANY NAMED RECORDS *\n");
        }

        return Task.CompletedTask;
    }

    /// <summary>
    /// Asynchronously moves unsolved crash logs to a designated folder for further review and analysis.
    /// </summary>
    /// <param name="logFileName">The full path to the crash log file to be moved.</param>
    /// <param name="autoscanReport">The list of analysis results for the crash log used to determine whether it is solved.</param>
    /// <returns>
    /// A task representing the asynchronous operation of moving the unsolved logs.
    /// </returns>
    private async Task MoveUnsolvedLogsAsync(string logFileName, List<string> autoscanReport)
    {
        if (_yamlData == null) return;

        var moveUnsolved = yamlSettings.GetSetting<bool?>(YamlStore.Settings, "Move Unsolved Logs") ?? false;
        if (!moveUnsolved) return;

        // Determine if this log is "unsolved" based on specific keywords or patterns
        var isSolved = autoscanReport.Any(line => line.Contains("SUSPECT FOUND", StringComparison.OrdinalIgnoreCase) ||
                                                  line.Contains("PROBLEMATIC MOD", StringComparison.OrdinalIgnoreCase));

        if (!isSolved)
        {
            // Create unsolved logs directory if it doesn't exist
            var unsolvedDir = Path.Combine(Path.GetDirectoryName(logFileName) ?? string.Empty, "Unsolved Logs");
            Directory.CreateDirectory(unsolvedDir);

            // Copy the log file to the unsolved directory
            var destFile = Path.Combine(unsolvedDir, Path.GetFileName(logFileName));
            try
            {
                if (!File.Exists(destFile))
                {
                    await fileService.CopyFilesAsync(
                        Path.GetDirectoryName(logFileName) ?? string.Empty,
                        unsolvedDir,
                        Path.GetFileName(logFileName));

                    logger.LogInformation("Moved unsolved log {LogFile} to unsolved directory", logFileName);
                    autoscanReport.Add("\n# Log has been copied to 'Unsolved Logs' folder for further analysis #\n");
                }
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Error moving unsolved log {LogFile}", logFileName);
                autoscanReport.Add($"\n# Error moving log to 'Unsolved Logs' folder: {ex.Message} #\n");
            }
        }
    }

    [GeneratedRegex(@"^(?:\[[\dA-Fa-f ]{2,8}\])?(?:[\d]+\))?[ ]*([^:]+?)(?:\.es[lpm])?:?",
        RegexOptions.IgnoreCase | RegexOptions.Compiled, "en-US")]
    private static partial Regex InitializePluginSearchRegex();

    [GeneratedRegex(@"\[([0-9A-Fa-f]{8})\]", RegexOptions.Compiled)]
    private static partial Regex InitializeFormIdRegex();

    [GeneratedRegex(@"v?(\d+\.\d+\.\d+(?:\.\d+)?)")]
    private static partial Regex ParseVersionString();

    /// <summary>
    ///     Finds and extracts segments from crash log data.
    /// </summary>
    // Assuming CrashLogSegments is an existing class/record, its definition is not included here.
    // It is expected to have properties like Crashgen, GameVersion, MainError, 
    // and List<string> properties for each segment (e.g., CrashgenSegment, SystemSegment).
    private enum SegmentParserState
    {
        None,
        Crashgen,
        System,
        CallStack,
        Modules,
        Plugins
    }
}