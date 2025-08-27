using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Scanner111.Core.Configuration;
using Scanner111.Core.Reporting;

namespace Scanner111.Core.Analysis.Analyzers;

/// <summary>
/// Analyzes crash logs for known suspect patterns and errors.
/// Thread-safe analyzer that scans main errors, call stacks, and DLL-related crashes.
/// </summary>
public sealed class SuspectScannerAnalyzer : AnalyzerBase
{
    private readonly IYamlSettingsCache _yamlCache;
    private const int DefaultMaxWarnLength = 80;
    
    public SuspectScannerAnalyzer(IYamlSettingsCache yamlCache, ILogger<SuspectScannerAnalyzer> logger)
        : base(logger)
    {
        _yamlCache = yamlCache ?? throw new ArgumentNullException(nameof(yamlCache));
    }
    
    /// <inheritdoc />
    public override string Name => "SuspectScanner";
    
    /// <inheritdoc />
    public override string DisplayName => "Suspect Pattern Scanner";
    
    /// <inheritdoc />
    public override int Priority => 20; // Run early to identify critical issues
    
    /// <inheritdoc />
    protected override async Task<AnalysisResult> PerformAnalysisAsync(
        AnalysisContext context,
        CancellationToken cancellationToken)
    {
        if (context == null)
            throw new ArgumentNullException(nameof(context));
        
        // Extract crash log data from context
        context.TryGetSharedData<string>("MainError", out var mainError);
        mainError = mainError ?? string.Empty;
            
        context.TryGetSharedData<string>("CallStack", out var callStack);
        callStack = callStack ?? string.Empty;
        
        if (string.IsNullOrWhiteSpace(mainError) && string.IsNullOrWhiteSpace(callStack))
        {
            LogDebug("No crash data available for suspect scanning");
            return AnalysisResult.CreateSkipped(Name, "No crash data available");
        }
        
        // Load suspect patterns from YAML configuration
        var suspectErrorList = await Task.Run(() => 
            _yamlCache.GetSetting<Dictionary<string, string>>(
                YamlStore.Game, "Crashlog_Error_Check") ?? new Dictionary<string, string>(),
            cancellationToken).ConfigureAwait(false);
            
        var suspectStackList = await Task.Run(() =>
            _yamlCache.GetSetting<Dictionary<string, List<string>>>(
                YamlStore.Game, "Crashlog_Stack_Check") ?? new Dictionary<string, List<string>>(),
            cancellationToken).ConfigureAwait(false);
        
        LogDebug("Loaded {ErrorCount} error patterns and {StackCount} stack patterns",
            suspectErrorList.Count, suspectStackList.Count);
        
        var fragments = new List<ReportFragment>();
        var foundAnySupect = false;
        var severity = AnalysisSeverity.Info;
        
        // Check DLL crashes first (highest priority)
        var dllFragment = CheckDllCrash(mainError);
        if (dllFragment != null)
        {
            fragments.Add(dllFragment);
            severity = AnalysisSeverity.Warning;
        }
        
        // Scan main error for known patterns
        if (!string.IsNullOrWhiteSpace(mainError))
        {
            var (errorFragment, errorFound) = ScanMainError(mainError, suspectErrorList, DefaultMaxWarnLength);
            if (errorFragment != null && errorFound)
            {
                fragments.Add(errorFragment);
                foundAnySupect = true;
                severity = AnalysisSeverity.Error;
            }
        }
        
        // Scan call stack for complex patterns
        if (!string.IsNullOrWhiteSpace(callStack))
        {
            var (stackFragment, stackFound) = ScanCallStack(mainError, callStack, suspectStackList, DefaultMaxWarnLength);
            if (stackFragment != null && stackFound)
            {
                fragments.Add(stackFragment);
                foundAnySupect = true;
                if (severity != AnalysisSeverity.Error)
                    severity = AnalysisSeverity.Warning;
            }
        }
        
        // Create result based on findings
        if (fragments.Count == 0)
        {
            var noSuspectsFragment = ReportFragment.CreateInfo(
                "Suspect Analysis",
                "No known crash suspects detected in the log.",
                100);
            var noSuspectResult = new AnalysisResult(Name)
            {
                Success = true,
                Fragment = noSuspectsFragment,
                Severity = AnalysisSeverity.None
            };
            return noSuspectResult;
        }
        
        var combinedFragment = ReportFragment.CreateWithChildren(
            "Crash Suspect Analysis",
            fragments,
            10); // High priority in report
        
        var result = new AnalysisResult(Name)
        {
            Success = true,
            Fragment = combinedFragment,
            Severity = severity
        };
        
        if (foundAnySupect)
        {
            result.AddMetadata("SuspectCount", fragments.Count.ToString());
            result.AddWarning($"Found {fragments.Count} crash suspect(s)");
        }
        
        LogInformation("Suspect scan completed: Found={Found}, Severity={Severity}",
            foundAnySupect, severity);
        
        return result;
    }
    
    private (ReportFragment? fragment, bool foundSuspect) ScanMainError(
        string crashLogMainError,
        Dictionary<string, string> suspectErrorList,
        int maxWarnLength)
    {
        if (suspectErrorList == null || suspectErrorList.Count == 0)
            return (null, false);
        
        var contentBuilder = new StringBuilder();
        var foundSuspect = false;
        
        foreach (var errorEntry in suspectErrorList)
        {
            var signal = errorEntry.Value;
            
            // Skip if signal not found in crash log
            if (!crashLogMainError.Contains(signal, StringComparison.OrdinalIgnoreCase))
                continue;
            
            // Parse error key format: "Severity | Error Name"
            var parts = errorEntry.Key.Split(" | ", 2);
            if (parts.Length != 2)
            {
                LogWarning("Invalid error key format: {Key}", errorEntry.Key);
                continue;
            }
            
            var errorSeverity = parts[0];
            var errorName = parts[1];
            
            // Format the error name with padding
            var formattedErrorName = errorName.PadRight(maxWarnLength, '.');
            
            // Add to report
            contentBuilder.AppendLine($"- **Checking for {formattedErrorName} SUSPECT FOUND! > Severity : {errorSeverity}**");
            contentBuilder.AppendLine();
            contentBuilder.AppendLine("-----");
            
            foundSuspect = true;
            LogDebug("Found main error suspect: {ErrorName} (Severity: {Severity})",
                errorName, errorSeverity);
        }
        
        if (!foundSuspect)
            return (null, false);
        
        var fragment = ReportFragment.CreateWarning(
            "Main Error Suspects",
            contentBuilder.ToString(),
            20);
        
        return (fragment, true);
    }
    
    private (ReportFragment? fragment, bool foundSuspect) ScanCallStack(
        string crashLogMainError,
        string segmentCallStackIntact,
        Dictionary<string, List<string>> suspectStackList,
        int maxWarnLength)
    {
        if (suspectStackList == null || suspectStackList.Count == 0)
            return (null, false);
        
        var contentBuilder = new StringBuilder();
        var anySuspectFound = false;
        
        foreach (var stackEntry in suspectStackList)
        {
            // Parse error key format: "Severity | Error Name"
            var parts = stackEntry.Key.Split(" | ", 2);
            if (parts.Length != 2)
            {
                LogWarning("Invalid stack key format: {Key}", stackEntry.Key);
                continue;
            }
            
            var errorSeverity = parts[0];
            var errorName = parts[1];
            
            // Initialize match status tracking
            var matchStatus = new MatchStatus();
            
            // Process each signal in the list
            var shouldSkipError = false;
            foreach (var signal in stackEntry.Value)
            {
                if (ProcessSignal(signal, crashLogMainError, segmentCallStackIntact, matchStatus))
                {
                    shouldSkipError = true;
                    break; // NOT condition met, skip this error
                }
            }
            
            if (shouldSkipError)
            {
                LogDebug("Skipping {ErrorName} due to NOT condition", errorName);
                continue;
            }
            
            // Check if we have a match
            if (IsSuspectMatch(matchStatus))
            {
                var formattedErrorName = errorName.PadRight(maxWarnLength, '.');
                contentBuilder.AppendLine($"- **Checking for {formattedErrorName} SUSPECT FOUND! > Severity : {errorSeverity}**");
                contentBuilder.AppendLine();
                contentBuilder.AppendLine("-----");
                anySuspectFound = true;
                LogDebug("Found stack suspect: {ErrorName} (Severity: {Severity})",
                    errorName, errorSeverity);
            }
        }
        
        if (!anySuspectFound)
            return (null, false);
        
        var fragment = ReportFragment.CreateWarning(
            "Call Stack Suspects",
            contentBuilder.ToString(),
            25);
        
        return (fragment, true);
    }
    
    private static ReportFragment? CheckDllCrash(string crashLogMainError)
    {
        if (string.IsNullOrWhiteSpace(crashLogMainError))
            return null;
        
        var crashLogLower = crashLogMainError.ToLowerInvariant();
        
        // Check for DLL involvement, excluding tbbmalloc
        if (crashLogLower.Contains(".dll") && !crashLogLower.Contains("tbbmalloc"))
        {
            var content = new StringBuilder();
            content.AppendLine("* NOTICE : MAIN ERROR REPORTS THAT A DLL FILE WAS INVOLVED IN THIS CRASH! *");
            content.AppendLine("If that dll file belongs to a mod, that mod is a prime suspect for the crash.");
            content.AppendLine();
            content.AppendLine("-----");
            
            return ReportFragment.CreateWarning(
                "DLL Crash Detected",
                content.ToString(),
                15);
        }
        
        return null;
    }
    
    private static bool ProcessSignal(
        string signal,
        string crashLogMainError,
        string segmentCallStackIntact,
        MatchStatus matchStatus)
    {
        // Signal modifiers
        const string MainErrorRequired = "ME-REQ";
        const string MainErrorOptional = "ME-OPT";
        const string CallStackNegative = "NOT";
        
        // Simple case: no modifier, direct string match
        if (!signal.Contains('|'))
        {
            if (segmentCallStackIntact.Contains(signal, StringComparison.OrdinalIgnoreCase))
            {
                matchStatus.StackFound = true;
            }
            return false;
        }
        
        // Parse signal with modifier
        var separatorIndex = signal.IndexOf('|');
        if (separatorIndex <= 0 || separatorIndex == signal.Length - 1)
            return false;
        
        var signalModifier = signal.Substring(0, separatorIndex);
        var signalString = signal.Substring(separatorIndex + 1);
        
        // Process based on modifier type
        switch (signalModifier)
        {
            case MainErrorRequired:
                matchStatus.HasRequiredItem = true;
                if (crashLogMainError.Contains(signalString, StringComparison.OrdinalIgnoreCase))
                {
                    matchStatus.ErrorReqFound = true;
                }
                break;
                
            case MainErrorOptional:
                if (crashLogMainError.Contains(signalString, StringComparison.OrdinalIgnoreCase))
                {
                    matchStatus.ErrorOptFound = true;
                }
                break;
                
            case CallStackNegative:
                // Return true to signal that we should skip this error
                return segmentCallStackIntact.Contains(signalString, StringComparison.OrdinalIgnoreCase);
                
            default:
                // Check if modifier is numeric (minimum occurrence count)
                if (int.TryParse(signalModifier, out var minOccurrences))
                {
                    var count = CountOccurrences(segmentCallStackIntact, signalString);
                    if (count >= minOccurrences)
                    {
                        matchStatus.StackFound = true;
                    }
                }
                break;
        }
        
        return false;
    }
    
    private static bool IsSuspectMatch(MatchStatus matchStatus)
    {
        // If we have required items, they must be found
        if (matchStatus.HasRequiredItem)
        {
            return matchStatus.ErrorReqFound;
        }
        
        // Otherwise, any optional error or stack match counts
        return matchStatus.ErrorOptFound || matchStatus.StackFound;
    }
    
    private static int CountOccurrences(string text, string pattern)
    {
        if (string.IsNullOrEmpty(text) || string.IsNullOrEmpty(pattern))
            return 0;
        
        var count = 0;
        var index = 0;
        
        while ((index = text.IndexOf(pattern, index, StringComparison.OrdinalIgnoreCase)) != -1)
        {
            count++;
            index += pattern.Length;
        }
        
        return count;
    }
    
    /// <summary>
    /// Tracks matching status for signal processing.
    /// </summary>
    private sealed class MatchStatus
    {
        public bool HasRequiredItem { get; set; }
        public bool ErrorReqFound { get; set; }
        public bool ErrorOptFound { get; set; }
        public bool StackFound { get; set; }
    }
}