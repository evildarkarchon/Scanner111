using Scanner111.Core.Models;
using Scanner111.Core.Infrastructure;

namespace Scanner111.Core.Analyzers;

/// <summary>
/// Handles scanning for known crash patterns and suspects, direct port of Python SuspectScanner
/// </summary>
public class SuspectScanner : IAnalyzer
{
    private readonly IYamlSettingsProvider _yamlSettings;

    /// <summary>
    /// Name of the analyzer
    /// </summary>
    public string Name => "Suspect Scanner";
    
    /// <summary>
    /// Priority of the analyzer (lower values run first)
    /// </summary>
    public int Priority => 2;
    
    /// <summary>
    /// Whether this analyzer can be run in parallel with others
    /// </summary>
    public bool CanRunInParallel => true;

    /// <summary>
    /// Initialize the suspect scanner
    /// </summary>
    /// <param name="yamlSettings">YAML settings provider for configuration</param>
    public SuspectScanner(IYamlSettingsProvider yamlSettings)
    {
        _yamlSettings = yamlSettings;
    }

    /// <summary>
    /// Analyze a crash log for suspect patterns
    /// </summary>
    /// <param name="crashLog">Crash log to analyze</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Suspect analysis result</returns>
    public async Task<AnalysisResult> AnalyzeAsync(CrashLog crashLog, CancellationToken cancellationToken = default)
    {
        await Task.CompletedTask; // Make it async-ready

        var reportLines = new List<string>();
        var errorMatches = new List<string>();
        var stackMatches = new List<string>();
        var matchDescriptions = new List<string>();

        const int maxWarnLength = 40;

        // Check for DLL crashes first
        CheckDllCrash(crashLog.MainError, reportLines);

        // Scan main error for suspects
        var mainErrorFound = SuspectScanMainError(reportLines, crashLog.MainError, maxWarnLength);
        if (mainErrorFound)
        {
            errorMatches.Add(crashLog.MainError);
        }

        // Scan call stack for suspects
        var callStackIntact = string.Join("\n", crashLog.CallStack);
        MessageHandler.MsgDebug($"Call stack has {crashLog.CallStack.Count} lines, joined length: {callStackIntact.Length}");
        var stackFound = SuspectScanStack(crashLog.MainError, callStackIntact, reportLines, maxWarnLength);
        if (stackFound)
        {
            stackMatches.Add(callStackIntact);
        }

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
    /// <param name="autoscanReport">A list to store formatted strings of identified suspect errors and their associated details</param>
    /// <param name="crashlogMainError">The main error output from a crash log to scan for suspect errors</param>
    /// <param name="maxWarnLength">The maximum length for formatting the error name in the autoscan report</param>
    /// <returns>A boolean indicating whether any suspect errors were found in the crash log</returns>
    private bool SuspectScanMainError(List<string> autoscanReport, string crashlogMainError, int maxWarnLength)
    {
        var foundSuspect = false;

        // Load the entire YAML file and access the section directly
        var fullData = _yamlSettings.LoadYaml<Dictionary<string, object>>("CLASSIC Fallout4");
        if (fullData == null || !fullData.ContainsKey("Crashlog_Error_Check"))
        {
            MessageHandler.MsgDebug("Could not load YAML or Crashlog_Error_Check section not found");
            return false;
        }
        
        var rawSuspectsData = fullData["Crashlog_Error_Check"] as Dictionary<object, object>;
        if (rawSuspectsData == null)
        {
            MessageHandler.MsgDebug("Crashlog_Error_Check section is not a dictionary");
            return false;
        }
        
        // Parse the special format: "5 | Stack Overflow Crash: EXCEPTION_STACK_OVERFLOW"
        var suspectsErrorList = new Dictionary<string, (string severity, string criteria)>();
        foreach (var (key, value) in rawSuspectsData)
        {
            var keyStr = key?.ToString() ?? "";
            var criteria = value?.ToString() ?? "";
            
            // Parse the key format: "5 | Stack Overflow Crash"
            var parts = keyStr.Split(" | ", 2);
            if (parts.Length == 2)
            {
                var severity = parts[0].Trim();
                var description = parts[1].Trim();
                
                // Use description as key and store both severity and criteria
                suspectsErrorList[description] = (severity, criteria);
            }
        }
        
        MessageHandler.MsgDebug($"Loaded {suspectsErrorList.Count} suspects from YAML");
        foreach (var (errorName, (severity, signal)) in suspectsErrorList)
        {
            // Skip checking if signal not in crash log
            if (!crashlogMainError.Contains(signal))
            {
                continue;
            }

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
    /// Analyzes a crash report and call stack information to identify potential suspect errors.
    /// Direct port of Python suspect_scan_stack method.
    /// </summary>
    /// <param name="crashlogMainError">The main error extracted from the crash log</param>
    /// <param name="segmentCallstackIntact">The intact segment of the call stack relevant to the analysis</param>
    /// <param name="autoscanReport">A mutable report list where detected suspects are appended</param>
    /// <param name="maxWarnLength">Maximum allowed length for warnings included in the report</param>
    /// <returns>Indicates whether any suspect has been identified and added to the autoscan report</returns>
    private bool SuspectScanStack(string crashlogMainError, string segmentCallstackIntact, List<string> autoscanReport, int maxWarnLength)
    {
        var anySuspectFound = false;

        // Load the entire YAML file and access the section directly
        var fullData = _yamlSettings.LoadYaml<Dictionary<string, object>>("CLASSIC Fallout4");
        if (fullData == null || !fullData.ContainsKey("Crashlog_Stack_Check"))
        {
            return false;
        }
        
        var rawStackData = fullData["Crashlog_Stack_Check"] as Dictionary<object, object>;
        
        // Parse the special format similar to error check
        var suspectsStackList = new Dictionary<string, List<string>>();
        if (rawStackData != null)
        {
            foreach (var (key, value) in rawStackData)
            {
                List<string> stringList;
                
                // Handle both single string and list formats
                if (value is List<object> listValue)
                {
                    stringList = listValue.Select(item => item?.ToString() ?? "").ToList();
                }
                else if (value is string stringValue)
                {
                    // Single string value, treat as single-item list
                    stringList = new List<string> { stringValue };
                }
                else
                {
                    // Try to convert to string as fallback
                    stringList = new List<string> { value?.ToString() ?? "" };
                }
                
                suspectsStackList[key?.ToString() ?? ""] = stringList;
            }
        }
        MessageHandler.MsgDebug($"Found {suspectsStackList.Count} stack patterns to check");
        MessageHandler.MsgDebug($"Call stack preview: '{segmentCallstackIntact.Substring(0, Math.Min(200, segmentCallstackIntact.Length))}'");
        foreach (var (errorKey, signalList) in suspectsStackList)
        {
            // Parse error information from the key format: "6 | BA2 Limit Crash"
            var keyStr = errorKey?.ToString() ?? "";
            MessageHandler.MsgDebug($"Processing stack pattern: '{keyStr}' with {signalList.Count} signals");
            var parts = keyStr.Split(" | ", 2, StringSplitOptions.None);
            if (parts.Length < 2) 
            {
                MessageHandler.MsgDebug($"Skipping malformed stack pattern key: '{keyStr}'");
                continue;
            }

            var errorSeverity = parts[0];
            var errorName = parts[1];

            // Initialize match status tracking dictionary
            var matchStatus = new Dictionary<string, bool>
            {
                {"has_required_item", false},
                {"error_req_found", false},
                {"error_opt_found", false},
                {"stack_found", false}
            };

            // Process each signal in the list
            var shouldSkipError = false;
            foreach (var signal in signalList)
            {
                MessageHandler.MsgDebug($"Processing signal '{signal}' for pattern '{errorName}'");
                // Process the signal and update match_status accordingly
                if (ProcessSignal(signal, crashlogMainError, segmentCallstackIntact, matchStatus))
                {
                    MessageHandler.MsgDebug($"Signal '{signal}' triggered skip (NOT condition met)");
                    shouldSkipError = true;
                    break;
                }
            }

            // Skip this error if a condition indicates we should
            if (shouldSkipError)
            {
                continue;
            }

            // Determine if we have a match based on the processed signals
            if (IsSuspectMatch(matchStatus))
            {
                // Add the suspect to the report and update the found status
                AddSuspectToReport(errorName, errorSeverity, maxWarnLength, autoscanReport);
                anySuspectFound = true;
            }
        }

        return anySuspectFound;
    }

    /// <summary>
    /// Process an individual signal and update match status.
    /// Direct port of Python _process_signal method.
    /// </summary>
    /// <param name="signal">Signal to process</param>
    /// <param name="crashlogMainError">Main error from crash log</param>
    /// <param name="segmentCallstackIntact">Intact call stack segment</param>
    /// <param name="matchStatus">Match status dictionary to update</param>
    /// <returns>True if processing should stop (NOT condition met)</returns>
    private static bool ProcessSignal(string signal, string crashlogMainError, string segmentCallstackIntact, Dictionary<string, bool> matchStatus)
    {
        // Constants for signal modifiers
        const string mainErrorRequired = "ME-REQ";
        const string mainErrorOptional = "ME-OPT";
        const string callstackNegative = "NOT";

        if (!signal.Contains("|"))
        {
            // Simple case: direct string match in callstack
            MessageHandler.MsgDebug($"Simple signal check: '{signal}' in call stack");
            if (segmentCallstackIntact.Contains(signal))
            {
                MessageHandler.MsgDebug($"FOUND simple signal '{signal}' in call stack!");
                matchStatus["stack_found"] = true;
            }
            else
            {
                MessageHandler.MsgDebug($"Simple signal '{signal}' NOT found in call stack");
            }
            return false;
        }

        var signalParts = signal.Split('|', 2, StringSplitOptions.None);
        var signalModifier = signalParts[0];
        var signalString = signalParts[1];

        // Process based on signal modifier
        if (signalModifier == mainErrorRequired)
        {
            matchStatus["has_required_item"] = true;
            if (crashlogMainError.Contains(signalString))
            {
                matchStatus["error_req_found"] = true;
            }
        }
        else if (signalModifier == mainErrorOptional)
        {
            if (crashlogMainError.Contains(signalString))
            {
                matchStatus["error_opt_found"] = true;
            }
        }
        else if (signalModifier == callstackNegative)
        {
            // Return True to break out of the loop if NOT condition is met
            return segmentCallstackIntact.Contains(signalString);
        }
        else if (int.TryParse(signalModifier, out var minOccurrences))
        {
            // Check for minimum occurrences
            var occurrences = CountOccurrences(segmentCallstackIntact, signalString);
            if (occurrences >= minOccurrences)
            {
                matchStatus["stack_found"] = true;
            }
        }

        return false;
    }

    /// <summary>
    /// Count occurrences of a substring in a string
    /// </summary>
    /// <param name="text">Text to search in</param>
    /// <param name="pattern">Pattern to search for</param>
    /// <returns>Number of occurrences</returns>
    private static int CountOccurrences(string text, string pattern)
    {
        if (string.IsNullOrEmpty(pattern))
            return 0;

        int count = 0;
        int index = 0;
        while ((index = text.IndexOf(pattern, index, StringComparison.Ordinal)) != -1)
        {
            count++;
            index += pattern.Length;
        }
        return count;
    }

    /// <summary>
    /// Determine if current error conditions constitute a suspect match.
    /// Direct port of Python _is_suspect_match method.
    /// </summary>
    /// <param name="matchStatus">Match status dictionary</param>
    /// <returns>True if conditions constitute a suspect match</returns>
    private static bool IsSuspectMatch(Dictionary<string, bool> matchStatus)
    {
        if (matchStatus["has_required_item"])
        {
            return matchStatus["error_req_found"];
        }
        return matchStatus["error_opt_found"] || matchStatus["stack_found"];
    }

    /// <summary>
    /// Add a found suspect to the report with proper formatting.
    /// Direct port of Python _add_suspect_to_report method.
    /// </summary>
    /// <param name="errorName">Name of the error</param>
    /// <param name="errorSeverity">Severity of the error</param>
    /// <param name="maxWarnLength">Maximum warning length</param>
    /// <param name="autoscanReport">Report to append to</param>
    private static void AddSuspectToReport(string errorName, string errorSeverity, int maxWarnLength, List<string> autoscanReport)
    {
        var formattedErrorName = errorName.PadRight(maxWarnLength, '.');
        var message = $"# Checking for {formattedErrorName} SUSPECT FOUND! > Severity : {errorSeverity} # \n-----\n";
        autoscanReport.Add(message);
    }

    /// <summary>
    /// Analyze a crash log and identify if a DLL file is implicated in the crash.
    /// Direct port of Python check_dll_crash method.
    /// </summary>
    /// <param name="crashlogMainError">The main error message extracted from the crash log</param>
    /// <param name="autoscanReport">A reference to a list where any relevant findings or alerts about the crash will be appended</param>
    private static void CheckDllCrash(string crashlogMainError, List<string> autoscanReport)
    {
        var crashlogMainErrorLower = crashlogMainError.ToLower();
        if (crashlogMainErrorLower.Contains(".dll") && !crashlogMainErrorLower.Contains("tbbmalloc"))
        {
            autoscanReport.AddRange(new[]
            {
                "* NOTICE : MAIN ERROR REPORTS THAT A DLL FILE WAS INVOLVED IN THIS CRASH! * \n",
                "If that dll file belongs to a mod, that mod is a prime suspect for the crash. \n-----\n"
            });
        }
    }
}