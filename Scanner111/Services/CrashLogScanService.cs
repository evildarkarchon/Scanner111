using System;
using System.Collections.Generic;
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
        if (!_initialized || _yamlData == null || _crashLogs == null)
        {
            await InitializeAsync();

            // Double-check initialization was successful
            if (_crashLogs == null || _yamlData == null)
            {
                logger.LogError("Failed to initialize crash log service");
                return ([], new ScanStatistics());
            }
        }

        logger.LogInformation("Starting crash log processing");

        var startTime = DateTime.Now;
        var results = new List<CrashLogProcessResult>();
        var logFiles = _crashLogs.GetLogNames();

        _statistics = new ScanStatistics();

        // Process logs in parallel
        var semaphore = new SemaphoreSlim(Environment.ProcessorCount, Environment.ProcessorCount);
        var tasks = logFiles.Select(async logFile =>
        {
            await semaphore.WaitAsync();
            try
            {
                return await ProcessSingleCrashLogAsync(logFile);
            }
            finally
            {
                semaphore.Release();
            }
        });

        results.AddRange(await Task.WhenAll(tasks)); // Update statistics
        _statistics.Scanned = results.Count;
        _statistics.Failed = results.Count(r => r.ScanFailed);
        _statistics.Incomplete = results.Count(r => r.Statistics.GetValueOrDefault("incomplete", 0) > 0);
        _statistics.FailedFiles.AddRange(results.Where(r => r.ScanFailed).Select(r => r.LogFileName));

        var processingTime = DateTime.Now - startTime;
        logger.LogInformation("Completed crash log processing in {Time} seconds", processingTime.TotalSeconds);

        // Write combined results
        await reportWriter.WriteCombinedResultsAsync(results, _statistics);

        return (results, _statistics);
    }

    /// <summary>
    ///     Processes a single crash log file.
    /// </summary>
    public async Task<CrashLogProcessResult> ProcessSingleCrashLogAsync(string logFileName)
    {
        if (!_initialized || _yamlData == null || _crashLogs == null) await InitializeAsync();

        logger.LogDebug("Processing crash log: {LogFile}", logFileName);
        try
        {
            // Read the crash log content
            if (_crashLogs == null) throw new InvalidOperationException("Log cache is not initialized");

            var crashData = await _crashLogs.ReadLogAsync(logFileName);
            var reportBuilder = new CrashReportBuilder { LogFileName = logFileName };
            reportBuilder.AddStatistic("scanned", 1);

            if (crashData.Count == 0)
            {
                reportBuilder.AddSection("Error", "ERROR: Empty or inaccessible crash log file")
                    .AddStatistic("failed", 1)
                    .ScanFailed = true;

                return reportBuilder.BuildResult();
            }

            // Add header to report
            if (_yamlData != null)
                reportBuilder.AddSection("Header",
                    $"{logFileName} -> AUTOSCAN REPORT GENERATED BY {_yamlData.ClassicVersion}",
                    "",
                    "# FOR BEST VIEWING EXPERIENCE OPEN THIS FILE IN NOTEPAD++ OR SIMILAR #",
                    "# PLEASE READ EVERYTHING CAREFULLY AND BEWARE OF FALSE POSITIVES #",
                    "====================================================");
            else
                reportBuilder.AddSection("Header",
                    $"{logFileName} -> AUTOSCAN REPORT",
                    "",
                    "# FOR BEST VIEWING EXPERIENCE OPEN THIS FILE IN NOTEPAD++ OR SIMILAR #",
                    "# PLEASE READ EVERYTHING CAREFULLY AND BEWARE OF FALSE POSITIVES #",
                    "====================================================");

            // Find segments in the crash log
            var segments = await FindSegmentsAsync(crashData, _yamlData!.CrashgenName);

            // Check for basic validity
            if (segments.PluginsSegment.Count == 0)
            {
                reportBuilder.AddSection("Warnings", "⚠️ NO PLUGINS FOUND IN CRASH LOG ⚠️");
                reportBuilder.AddStatistic("incomplete", 1);
            }

            if (crashData.Count < 20)
            {
                reportBuilder.AddToSection("Warnings", "⚠️ CRASH LOG APPEARS TO BE INCOMPLETE ⚠️");
                reportBuilder.AddStatistic("incomplete", 1);
            }

            // Check for FCX mode
            var fcxMode = yamlSettings.GetSetting<bool?>(YamlStore.Main, "FCX Mode");
            await CheckFcxModeAsync(fcxMode ?? false);

            // Add FCX check results to report
            if (!string.IsNullOrEmpty(_mainFilesResult))
                reportBuilder.AddSection("FCX Results", _mainFilesResult);

            if (!string.IsNullOrEmpty(_gameFilesResult))
                reportBuilder.AddToSection("FCX Results", _gameFilesResult);

            // Extract plugins from the segments
            var pluginsMap = ExtractPluginsFromSegment(segments.PluginsSegment);

            // Process main error for suspect detection
            var suspectFound = false;
            if (!string.IsNullOrEmpty(segments.MainError))
                suspectFound = ScanSuspectMainError(reportBuilder, segments.MainError);

            // Process call stack for suspect detection
            var callStackText = string.Join("\n", segments.CallStackSegment);
            var suspectStackFound = ScanSuspectStack(segments.MainError, callStackText, reportBuilder);
            suspectFound = suspectFound || suspectStackFound;

            // If no suspects found, note that in the report
            if (!suspectFound)
                reportBuilder.AddSection("Suspects",
                    "# ℹ️ No suspect patterns found in main error or call stack # \n-----\n");

            // Analyze FormIDs if database exists and show FormID values is enabled
            var showFormIdValues = yamlSettings.GetSetting<bool?>(YamlStore.Main, "Show FormID Values");
            if (showFormIdValues.HasValue && showFormIdValues.Value && databaseService.DatabaseExists())
                await AnalyzeFormIdsAsync(segments.CallStackSegment, pluginsMap, reportBuilder);

            // Detect mods from plugins
            if (pluginsMap.Count > 0)
            {
                reportBuilder.AddSection("Problematic Mods", "# CHECKING FOR KNOWN PROBLEMATIC MODS #");

                // Detect single problematic mods
                var singleModsFound =
                    modDetectionService.DetectModsSingle(_yamlData.GameModsFreq, pluginsMap, reportBuilder);

                // Detect conflicting mod combinations
                var conflictingModsFound =
                    modDetectionService.DetectModsDouble(_yamlData.GameModsConf, pluginsMap, reportBuilder);

                // Detect important mods and check compatibility
                // Extract GPU info from system segment for compatibility checking
                var gpuInfo =
                    segments.SystemSegment.FirstOrDefault(line =>
                        line.Contains("GPU:", StringComparison.OrdinalIgnoreCase)) ?? string.Empty;
                var gpuRival = string.Empty;

                if (gpuInfo.Contains("NVIDIA", StringComparison.OrdinalIgnoreCase))
                    gpuRival = "nvidia";
                else if (gpuInfo.Contains("AMD", StringComparison.OrdinalIgnoreCase) ||
                         gpuInfo.Contains("Radeon", StringComparison.OrdinalIgnoreCase))
                    gpuRival = "amd";

                reportBuilder.AddSection("Important Mods", "# CHECKING FOR IMPORTANT MODS #");
                modDetectionService.DetectModsImportant(_yamlData.GameModsCore, pluginsMap, reportBuilder, gpuRival);

                if (!singleModsFound && !conflictingModsFound)
                    reportBuilder.AddToSection("Problematic Mods", "# ℹ️ No known problematic mods detected #");
            }

            // Add version information
            var versionCurrent = ParseVersion(segments.Crashgen);
            var versionLatest = ParseVersion(_yamlData.CrashgenLatestOg);
            var versionLatestVr = ParseVersion(_yamlData.CrashgenLatestVr);

            reportBuilder.AddSection("Version Info",
                $"Main Error: {segments.MainError}",
                $"Detected {_yamlData.CrashgenName} Version: {segments.Crashgen}",
                IsLatestVersion(versionCurrent, versionLatest, versionLatestVr)
                    ? $"* You have the latest version of {_yamlData.CrashgenName}! *"
                    : $"{_yamlData.WarnOutdated}");

            // Extract and analyze plugins
            var crashlogPlugins = ExtractPluginsFromSegment(segments.PluginsSegment);
            if (crashlogPlugins.Count > 0)
                reportBuilder.AddSection("Plugins", $"## Found {crashlogPlugins.Count} plugins");

            // Analyze call stack if available
            if (segments.CallStackSegment.Count > 0)
            {
                reportBuilder.AddSection("Call Stack", "## Call Stack Analysis", "");
                // TODO: Implement stack analysis
            }

            try
            {
                // Move unsolved logs if necessary
                await MoveUnsolvedLogsAsync(logFileName, reportBuilder.Build());

                // Add summary information
                reportBuilder.AddSection("Summary",
                    "## SCAN SUMMARY ##",
                    "",
                    "This analysis provides possible crash causes based on known patterns and mod detection.",
                    "Verify suggested solutions and consult the community for help if needed.",
                    "",
                    $"Analysis completed on: {DateTime.Now:yyyy-MM-dd HH:mm:ss}");

                // Mark success
                reportBuilder.AddStatistic("success", 1);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Error during final analysis steps for {LogFile}", logFileName);
                reportBuilder.AddSection("Error", $"⚠️ ERROR DURING FINAL ANALYSIS: {ex.Message}");
            }

            var result = reportBuilder.BuildResult();

            // Write report to file
            await reportWriter.WriteReportToFileAsync(logFileName, result.Report, false);

            return result;
        }
        catch (Exception ex)
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
    }

    /// <summary>
    ///     Finds and extracts segments from crash log data.
    /// </summary>
    public Task<CrashLogSegments> FindSegmentsAsync(List<string> crashData, string crashgenName)
    {
        return Task.Run(() =>
        {
            var segments = new CrashLogSegments();

            // Process data to extract segments
            var inCrashgen = false;
            var inSystem = false;
            var inCallStack = false;
            var inModules = false;
            var inPlugins = false;

            var crashgenSegment = new List<string>();
            var systemSegment = new List<string>();
            var callStackSegment = new List<string>();
            var allModulesSegment = new List<string>();
            var xseModulesSegment = new List<string>();
            var pluginsSegment = new List<string>();

            foreach (var line in crashData)
            {
                // Extract Crashgen and version
                if (line.StartsWith(crashgenName))
                {
                    segments = segments with { Crashgen = line };
                    inCrashgen = true;
                    continue;
                }

                if (line.StartsWith("Fallout 4:") || line.StartsWith("Skyrim:"))
                {
                    segments = segments with { GameVersion = line };
                    continue;
                }

                // Main error is often at the top
                if (string.IsNullOrEmpty(segments.MainError) && line.Contains("EXCEPTION_"))
                {
                    segments = segments with { MainError = line };
                    continue;
                }

                // Identify segments
                if (line.Contains("SYSTEM SPECS:"))
                {
                    inCrashgen = false;
                    inSystem = true;
                    continue;
                }

                if (line.Contains("PROBABLE CALL STACK:"))
                {
                    inSystem = false;
                    inCallStack = true;
                    continue;
                }

                if (line.Contains("MODULES:"))
                {
                    inCallStack = false;
                    inModules = true;
                    continue;
                }

                if (line.Contains("PLUGINS:"))
                {
                    inModules = false;
                    inPlugins = true;
                    continue;
                }

                // Collect lines for each segment
                if (inCrashgen)
                {
                    crashgenSegment.Add(line);
                }
                else if (inSystem)
                {
                    systemSegment.Add(line);
                }
                else if (inCallStack)
                {
                    callStackSegment.Add(line);
                }
                else if (inModules)
                {
                    allModulesSegment.Add(line);
                    if (line.Contains("f4se") || line.Contains("skse")) xseModulesSegment.Add(line);
                }
                else if (inPlugins)
                {
                    pluginsSegment.Add(line);
                }
            }

            // Create a new segments object with all the data
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

    /// <summary>
    ///     Extracts plugins from plugin segment lines.
    /// </summary>
    private Dictionary<string, string> ExtractPluginsFromSegment(List<string> pluginSegment)
    {
        var result = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        if (_pluginSearchRegex == null) return result;

        foreach (var line in pluginSegment)
        {
            var match = _pluginSearchRegex.Match(line);
            if (match is not { Success: true, Groups.Count: > 1 }) continue;
            var pluginName = match.Groups[1].Value.Trim();
            if (string.IsNullOrEmpty(pluginName)) continue;
            // Extract the load order if available
            var loadOrder = "FF";
            var bracketStart = line.IndexOf('[');
            var bracketEnd = line.IndexOf(']');

            if (bracketStart >= 0 && bracketEnd > bracketStart)
                loadOrder = line.Substring(bracketStart + 1, bracketEnd - bracketStart - 1).Trim();

            result[pluginName] = loadOrder;
        }

        return result;
    }

    /// <summary>
    ///     Parses a version string into a Version object.
    /// </summary>
    private static Version ParseVersion(string versionString)
    {
        if (string.IsNullOrEmpty(versionString))
            return new Version(0, 0, 0, 0);

        // Extract version from string (look for pattern like "v1.2.3.4")
        var match = Regex.Match(versionString, @"v?(\d+\.\d+\.\d+(?:\.\d+)?)");
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
        {
            // Try to find which plugin this FormID belongs to
            foreach (var (plugin, _) in pluginsMap)
            {
                var entry = await databaseService.GetEntryAsync(formId, plugin);
                if (string.IsNullOrEmpty(entry)) continue;
                reportBuilder.AddToSection("FormID Analysis", $"🔍 FormID [{formId}] matched to {plugin}: {entry}");
                formIdsFound = true;
                break; // Found a match, no need to check other plugins
            }
        }

        if (!formIdsFound)
            reportBuilder.AddToSection("FormID Analysis",
                "❓ No FormIDs from call stack matched to plugins in database");
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
}