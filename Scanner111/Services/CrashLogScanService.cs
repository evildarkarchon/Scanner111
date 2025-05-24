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
    ///     Initializes the scan service with settings and file cache.
    /// </summary>
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
    ///     Processes all crash logs and returns results.
    /// </summary>
    public async Task<(List<CrashLogProcessResult> Results, ScanStatistics Statistics)> ProcessAllCrashLogsAsync()
    {
        if (!await CheckAndInitializeAsync())
        {
            logger.LogError("Failed to initialize crash log service");
            return ([], new ScanStatistics());
        }

        logger.LogInformation("Starting crash log processing");
        var stopwatch = Stopwatch.StartNew();

        var logFiles = _crashLogs.GetLogNames();
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
    ///     Processes a single crash log file.
    /// </summary>
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

            foreach (var line in crashData)
            {
                if (TryProcessControlLine(line, crashgenName, ref segments,
                        ref currentState))
                    continue; // Control line processed, or header identified; move to the next line

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
    ///     Gets the current scan statistics.
    /// </summary>
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

    private async Task<bool> CheckAndInitializeAsync()
    {
        if (!_initialized || _yamlData == null || _crashLogs == null) await InitializeAsync();

        return _crashLogs != null && _yamlData != null;
    }

    private async Task<List<CrashLogProcessResult>> ProcessLogsInParallelAsync(IEnumerable<string> logFiles)
    {
        var maxConcurrency = Environment.ProcessorCount;
        var throttler = new SemaphoreSlim(maxConcurrency, maxConcurrency);

        var processingTasks = logFiles.Select(async logFile =>
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

    private void UpdateStatistics(List<CrashLogProcessResult> results)
    {
        _statistics.Scanned = results.Count;
        _statistics.Failed = results.Count(r => r.ScanFailed);
        _statistics.Incomplete = results.Count(r => r.Statistics.GetValueOrDefault("incomplete", 0) > 0);
        _statistics.FailedFiles.AddRange(results.Where(r => r.ScanFailed).Select(r => r.LogFileName));
    }

    private async Task EnsureInitializedAsync()
    {
        if (!_initialized || _yamlData == null || _crashLogs == null)
            await InitializeAsync();

        if (_crashLogs == null)
            throw new InvalidOperationException("Log cache is not initialized");
    }

    private async Task<List<string>> ReadCrashLogDataAsync(string logFileName, CrashReportBuilder reportBuilder)
    {
        var crashData = await _crashLogs!.ReadLogAsync(logFileName);

        if (crashData.Count == 0)
            reportBuilder.AddSection("Error", "ERROR: Empty or inaccessible crash log file")
                .AddStatistic("failed", 1)
                .ScanFailed = true;

        return crashData;
    }

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

    private async Task ProcessFcxModeAsync(CrashReportBuilder reportBuilder)
    {
        var fcxMode = yamlSettings.GetSetting<bool?>(YamlStore.Main, "FCX Mode");
        await CheckFcxModeAsync(fcxMode ?? false);

        if (!string.IsNullOrEmpty(_mainFilesResult))
            reportBuilder.AddSection("FCX Results", _mainFilesResult);

        if (!string.IsNullOrEmpty(_gameFilesResult))
            reportBuilder.AddToSection("FCX Results", _gameFilesResult);
    }

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

    private async Task AnalyzeFormIdsIfEnabledAsync(List<string> callStackSegment,
        Dictionary<string, string> pluginsMap, CrashReportBuilder reportBuilder)
    {
        var showFormIdValues = yamlSettings.GetSetting<bool?>(YamlStore.Main, "Show FormID Values");
        if (showFormIdValues.HasValue && showFormIdValues.Value && databaseService.DatabaseExists())
            await AnalyzeFormIdsAsync(callStackSegment, pluginsMap, reportBuilder);
    }

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

    private async Task AnalyzeCallStackIfAvailableAsync(List<string> callStackSegment,
        Dictionary<string, string> pluginsMap, CrashReportBuilder reportBuilder)
    {
        if (callStackSegment.Count > 0)
        {
            reportBuilder.AddSection("Call Stack", "## Call Stack Analysis", "");
            await AnalyzeCallStackAsync(callStackSegment, pluginsMap, reportBuilder);
        }
    }

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

    private string ExtractLoadOrder(string line)
    {
        var bracketStart = line.IndexOf('[');
        var bracketEnd = line.IndexOf(']');

        if (bracketStart >= 0 && bracketEnd > bracketStart)
            return line.Substring(bracketStart + 1, bracketEnd - bracketStart - 1).Trim();

        return DefaultLoadOrder;
    }

    /// <summary>
    ///     Parses a version string into a Version object.
    /// </summary>
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
    ///     Checks if the current version is the latest.
    /// </summary>
    private static bool IsLatestVersion(Version current, Version latest, Version latestVr)
    {
        return current >= latest || current >= latestVr;
    }

    /// <summary>
    ///     Checks if a suspect stack signal matches the given error and callstack.
    /// </summary>
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
    ///     Determines if a suspect match is valid based on match conditions.
    /// </summary>
    private bool IsSuspectStackMatch(Dictionary<string, bool> matchStatus)
    {
        // If there are required items, they must be found
        if (matchStatus["has_required_item"] && !matchStatus["error_req_found"]) return false;

        // Must have either a stack match or an optional error match
        return matchStatus["stack_found"] || matchStatus["error_opt_found"];
    }

    /// <summary>
    ///     Scans for suspect errors in the callstack.
    /// </summary>
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
    ///     Scans for main errors in the crash log.
    /// </summary>
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
    ///     Checks for FCX mode and performs game file checks.
    /// </summary>
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
    ///     Analyzes FormIDs in crash log callstack for matching.
    /// </summary>
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
    ///     Analyzes call stack for plugins and named records.
    /// </summary>
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
    ///     Analyzes plugins found in the call stack.
    /// </summary>
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
    ///     Analyzes named records in the call stack.
    /// </summary>
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
    ///     Handles moving unsolved logs to a separate folder for further analysis.
    /// </summary>
    private async Task MoveUnsolvedLogsAsync(string logFileName, List<string> autoscanReport)
    {
        if (_yamlData == null) return;

        var moveUnsolved = yamlSettings.GetSetting<bool?>(YamlStore.Main, "Move Unsolved Logs") ?? false;
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