using Microsoft.Extensions.Logging;
using Scanner111.Core.Infrastructure;
using Scanner111.Core.Models;
using Scanner111.Core.Models.Yaml;

namespace Scanner111.Core.Analyzers;

/// <summary>
/// Represents an analyzer that identifies known crash patterns and suspects from crash logs.
/// </summary>
/// <remarks>
/// Implements the IAnalyzer interface. Utilizes YAML settings for configuration and supports asynchronous analysis.
/// </remarks>
public class SuspectScanner : IAnalyzer
{
    private readonly IYamlSettingsProvider _yamlSettings;
    private readonly ILogger<SuspectScanner> _logger;

    /// <summary>
    ///     Initialize the suspect scanner
    /// </summary>
    /// <param name="yamlSettings">YAML settings provider for configuration</param>
    /// <param name="logger">Logger for internal diagnostics</param>
    public SuspectScanner(IYamlSettingsProvider yamlSettings, ILogger<SuspectScanner> logger)
    {
        _yamlSettings = yamlSettings;
        _logger = logger;
    }

    /// <summary>
    ///     Name of the analyzer
    /// </summary>
    public string Name => "Suspect Scanner";

    /// <summary>
    ///     Priority of the analyzer (lower values run first)
    /// </summary>
    public int Priority => 2;

    /// <summary>
    ///     Whether this analyzer can be run in parallel with others
    /// </summary>
    public bool CanRunInParallel => true;

    /// <summary>
    /// Analyze a crash log for suspect patterns
    /// </summary>
    /// <param name="crashLog">Crash log to analyze</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Suspect analysis result</returns>
    public async Task<AnalysisResult> AnalyzeAsync(CrashLog crashLog, CancellationToken cancellationToken = default)
    {
        await Task.CompletedTask.ConfigureAwait(false); // Make it async-ready

        var reportLines = new List<string>();
        var errorMatches = new List<string>();
        var stackMatches = new List<string>();
        var matchDescriptions = new List<string>();

        const int maxWarnLength = 25;

        // Check for DLL crashes first
        CheckDllCrash(crashLog.MainError, reportLines);

        // Scan main error for suspects
        var mainErrorFound = SuspectScanMainError(reportLines, crashLog.MainError, maxWarnLength);
        if (mainErrorFound) errorMatches.Add(crashLog.MainError);

        // Scan call stack for suspects
        var callStackIntact = string.Join("\n", crashLog.CallStack);
        _logger.LogDebug(
            "Call stack has {CallStackCount} lines, joined length: {CallStackLength}", 
            crashLog.CallStack.Count, callStackIntact.Length);
        var stackFound = SuspectScanStack(crashLog.MainError, callStackIntact, reportLines, maxWarnLength);
        if (stackFound) stackMatches.Add(callStackIntact);

        return new SuspectAnalysisResult
        {
            AnalyzerName = Name,
            ErrorMatches = errorMatches,
            StackMatches = stackMatches,
            MatchDescriptions = matchDescriptions,
            ReportLines = reportLines,
            HasFindings = mainErrorFound || stackFound
        };
    }

    /// <summary>
    /// Scans the crash log for errors listed in a predefined suspect error list.
    /// Direct port of Python suspect_scan_mainerror method.
    /// </summary>
    /// <param name="autoscanReport">
    /// A list to store formatted strings of identified suspect errors and their associated details.
    /// </param>
    /// <param name="crashlogMainError">
    /// The main error output from a crash log to scan for suspect errors.
    /// </param>
    /// <param name="maxWarnLength">
    /// The maximum length for formatting the error name in the autoscan report.
    /// </param>
    /// <returns>
    /// A boolean indicating whether any suspect errors were found in the crash log.
    /// </returns>
    private bool SuspectScanMainError(List<string> autoscanReport, string crashlogMainError, int maxWarnLength)
    {
        var foundSuspect = false;

        // Load the strongly-typed YAML model
        var fallout4Yaml = _yamlSettings.LoadYaml<ClassicFallout4YamlV2>("CLASSIC Fallout4");
        if (fallout4Yaml?.CrashlogErrorCheck == null)
        {
            _logger.LogDebug("Could not load YAML or CrashlogErrorCheck section not found");
            return false;
        }

        // Parse the special format: "5 | Stack Overflow Crash: EXCEPTION_STACK_OVERFLOW"
        var suspectsErrorList = new Dictionary<string, (string severity, string criteria)>();
        foreach (var (key, value) in fallout4Yaml.CrashlogErrorCheck)
        {
            // Parse the key format: "5 | Stack Overflow Crash"
            var parts = key.Split(" | ", 2);
            if (parts.Length != 2) continue;
            var severity = parts[0].Trim();
            var description = parts[1].Trim();

            // Use description as key and store both severity and criteria
            suspectsErrorList[description] = (severity, value);
        }

        _logger.LogDebug("Loaded {SuspectCount} suspects from YAML", suspectsErrorList.Count);
        foreach (var (errorName, (severity, signal)) in suspectsErrorList)
        {
            // Skip checking if signal not in crash log
            if (!crashlogMainError.Contains(signal)) continue;

            // Format the error name for report
            var formattedErrorName = errorName.PadRight(maxWarnLength, '.');

            // Add the error to the report
            var reportEntry = $"# Checking for {formattedErrorName} SUSPECT FOUND! > Severity : {severity} # \n-----\n";
            autoscanReport.Add(reportEntry);

            // Update suspect found status
            foundSuspect = true;
        }

        return foundSuspect;
    }

    /// <summary>
    /// Analyzes the call stack of a crash log to identify potential suspects based on predefined patterns.
    /// </summary>
    /// <param name="crashlogMainError">The main error string extracted from the crash log.</param>
    /// <param name="segmentCallstackIntact">The complete concatenated call stack extracted from the crash log.</param>
    /// <param name="autoscanReport">A list to store the auto-scan report lines generated during the analysis.</param>
    /// <param name="maxWarnLength">The maximum length of warning messages to include in the report.</param>
    /// <returns>
    /// A boolean value indicating whether any suspect patterns were found in the call stack.
    /// </returns>
    private bool SuspectScanStack(string crashlogMainError, string segmentCallstackIntact, List<string> autoscanReport,
        int maxWarnLength)
    {
        var anySuspectFound = false;

        // Load the strongly-typed YAML model
        var fallout4Yaml = _yamlSettings.LoadYaml<ClassicFallout4YamlV2>("CLASSIC Fallout4");
        if (fallout4Yaml?.CrashlogStackCheck == null) return false;

        // Use the already properly typed CrashlogStackCheck
        var suspectsStackList = fallout4Yaml.CrashlogStackCheck;

        _logger.LogDebug("Found {StackPatternCount} stack patterns to check", suspectsStackList.Count);
        _logger.LogDebug(
            "Call stack preview: '{CallStackPreview}'", 
            segmentCallstackIntact.Substring(0, Math.Min(200, segmentCallstackIntact.Length)));
        foreach (var (errorKey, signalList) in suspectsStackList)
        {
            // Parse error information from the key format: "6 | BA2 Limit Crash"
            var keyStr = errorKey;
            _logger.LogDebug("Processing stack pattern: '{PatternKey}' with {SignalCount} signals", keyStr, signalList.Count);
            var parts = keyStr.Split(" | ", 2);
            if (parts.Length < 2)
            {
                _logger.LogDebug("Skipping malformed stack pattern key: '{PatternKey}'", keyStr);
                continue;
            }

            var errorSeverity = parts[0];
            var errorName = parts[1];

            // Initialize match status tracking dictionary
            var matchStatus = new Dictionary<string, bool>
            {
                { "has_required_item", false },
                { "error_req_found", false },
                { "error_opt_found", false },
                { "stack_found", false }
            };

            // Process each signal in the list
            var shouldSkipError = false;
            foreach (var signal in signalList)
            {
                _logger.LogDebug("Processing signal '{Signal}' for pattern '{ErrorName}'", signal, errorName);
                // Process the signal and update match_status accordingly
                if (!ProcessSignal(signal, crashlogMainError, segmentCallstackIntact, matchStatus)) continue;
                _logger.LogDebug("Signal '{Signal}' triggered skip (NOT condition met)", signal);
                shouldSkipError = true;
                break;
            }

            // Skip this error if a condition indicates we should
            if (shouldSkipError) continue;

            // Determine if we have a match based on the processed signals
            if (!IsSuspectMatch(matchStatus)) continue;
            // Add the suspect to the report and update the found status
            AddSuspectToReport(errorName, errorSeverity, maxWarnLength, autoscanReport);
            anySuspectFound = true;
        }

        return anySuspectFound;
    }


    /// <summary>
    /// Processes a signal to analyze matching conditions in crash logs and updates the match status accordingly.
    /// </summary>
    /// <param name="signal">The signal string to be processed, which may include a modifier and a target string separated by '|'.</param>
    /// <param name="crashlogMainError">The main error section of the crash log to be checked against the signal.</param>
    /// <param name="segmentCallstackIntact">The callstack segment of the crash log to be examined for signal matches.</param>
    /// <param name="matchStatus">A dictionary for tracking various match conditions and their statuses.</param>
    /// <returns>
    /// A boolean value indicating whether a "NOT" condition is met in the signal, triggering a skip in further processing.
    /// </returns>
    private bool ProcessSignal(string signal, string crashlogMainError, string segmentCallstackIntact,
        Dictionary<string, bool> matchStatus)
    {
        // Constants for signal modifiers
        const string mainErrorRequired = "ME-REQ";
        const string mainErrorOptional = "ME-OPT";
        const string callstackNegative = "NOT";

        if (!signal.Contains("|"))
        {
            // Simple case: direct string match in callstack
            _logger.LogDebug("Simple signal check: '{Signal}' in call stack", signal);
            if (segmentCallstackIntact.Contains(signal))
            {
                _logger.LogDebug("FOUND simple signal '{Signal}' in call stack!", signal);
                matchStatus["stack_found"] = true;
            }
            else
            {
                _logger.LogDebug("Simple signal '{Signal}' NOT found in call stack", signal);
            }

            return false;
        }

        var signalParts = signal.Split('|', 2);
        var signalModifier = signalParts[0];
        var signalString = signalParts[1];

        switch (signalModifier)
        {
            // Process based on signal modifier
            case mainErrorRequired:
            {
                matchStatus["has_required_item"] = true;
                if (crashlogMainError.Contains(signalString)) matchStatus["error_req_found"] = true;
                break;
            }
            case mainErrorOptional:
            {
                if (crashlogMainError.Contains(signalString)) matchStatus["error_opt_found"] = true;
                break;
            }
            case callstackNegative:
                // Return True to break out of the loop if NOT condition is met
                return segmentCallstackIntact.Contains(signalString);
            default:
            {
                if (int.TryParse(signalModifier, out var minOccurrences))
                {
                    // Check for minimum occurrences
                    var occurrences = CountOccurrences(segmentCallstackIntact, signalString);
                    if (occurrences >= minOccurrences) matchStatus["stack_found"] = true;
                }

                break;
            }
        }

        return false;
    }

    /// <summary>
    /// Counts the occurrences of a specific substring within a given text.
    /// </summary>
    /// <param name="text">The text in which to search for the substring.</param>
    /// <param name="pattern">The substring to count within the text.</param>
    /// <returns>The total number of occurrences of the substring in the text.</returns>
    private static int CountOccurrences(string text, string pattern)
    {
        if (string.IsNullOrEmpty(pattern))
            return 0;

        var count = 0;
        var index = 0;
        while ((index = text.IndexOf(pattern, index, StringComparison.Ordinal)) != -1)
        {
            count++;
            index += pattern.Length;
        }

        return count;
    }


    /// <summary>
    /// Determines if a suspect matches based on the provided match status.
    /// </summary>
    /// <param name="matchStatus">A dictionary containing the match status of various conditions.</param>
    /// <returns>
    /// Returns <c>true</c> if the suspect match criteria are satisfied; otherwise, returns <c>false</c>.
    /// </returns>
    private static bool IsSuspectMatch(Dictionary<string, bool> matchStatus)
    {
        if (matchStatus["has_required_item"]) return matchStatus["error_req_found"];
        return matchStatus["error_opt_found"] || matchStatus["stack_found"];
    }


    /// <summary>
    /// Adds a suspect to the report based on the provided error details.
    /// </summary>
    /// <param name="errorName">The name of the error or suspect identified.</param>
    /// <param name="errorSeverity">The severity level of the identified error.</param>
    /// <param name="maxWarnLength">The maximum allowed length for formatting warnings.</param>
    /// <param name="autoscanReport">The report to which the formatted suspect details should be added.</param>
    private static void AddSuspectToReport(string errorName, string errorSeverity, int maxWarnLength,
        List<string> autoscanReport)
    {
        var formattedErrorName = errorName.PadRight(maxWarnLength, '.');
        var message = $"# Checking for {formattedErrorName} SUSPECT FOUND! > Severity : {errorSeverity} # \n-----\n";
        autoscanReport.Add(message);
    }


    /// <summary>
    /// Checks if the main error of the crash log indicates involvement of a DLL file and updates the report accordingly.
    /// </summary>
    /// <param name="crashlogMainError">The main error message from the crash log.</param>
    /// <param name="autoscanReport">A list for appending the report lines generated during the check.</param>
    private static void CheckDllCrash(string crashlogMainError, List<string> autoscanReport)
    {
        var crashlogMainErrorLower = crashlogMainError.ToLower();
        if (crashlogMainErrorLower.Contains(".dll") && !crashlogMainErrorLower.Contains("tbbmalloc"))
            autoscanReport.AddRange([
              "* NOTICE : MAIN ERROR REPORTS THAT A DLL FILE WAS INVOLVED IN THIS CRASH! * \n",
                "If that dll file belongs to a mod, that mod is a prime suspect for the crash. \n-----\n"
            ]);
    }
}