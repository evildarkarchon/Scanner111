using System.Text;
using Microsoft.Extensions.Logging;
using Scanner111.Core.Analysis.SignalProcessing;
using Scanner111.Core.Configuration;
using Scanner111.Core.Reporting;

namespace Scanner111.Core.Analysis.Analyzers;

/// <summary>
///     Analyzes crash logs for known suspect patterns and errors.
///     Thread-safe analyzer that scans main errors, call stacks, and DLL-related crashes.
///     Enhanced with advanced signal processing, severity calculation, and call stack analysis.
/// </summary>
public sealed class SuspectScannerAnalyzer : AnalyzerBase
{
    private const int DefaultMaxWarnLength = 80;
    private readonly IAsyncYamlSettingsCore _yamlCore;
    private readonly SignalProcessor _signalProcessor;
    private readonly SeverityCalculator _severityCalculator;
    private readonly CallStackAnalyzer _callStackAnalyzer;

    public SuspectScannerAnalyzer(
        IAsyncYamlSettingsCore yamlCore, 
        ILogger<SuspectScannerAnalyzer> logger,
        SignalProcessor? signalProcessor = null,
        SeverityCalculator? severityCalculator = null,
        CallStackAnalyzer? callStackAnalyzer = null)
        : base(logger)
    {
        _yamlCore = yamlCore ?? throw new ArgumentNullException(nameof(yamlCore));
        
        // Create default instances if not provided (for backward compatibility)
        _signalProcessor = signalProcessor ?? new SignalProcessor(
            new Logger<SignalProcessor>(new LoggerFactory()));
        _severityCalculator = severityCalculator ?? new SeverityCalculator(
            new Logger<SeverityCalculator>(new LoggerFactory()));
        _callStackAnalyzer = callStackAnalyzer ?? new CallStackAnalyzer(
            new Logger<CallStackAnalyzer>(new LoggerFactory()));
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

        // Check for cancellation early
        cancellationToken.ThrowIfCancellationRequested();

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
        var suspectErrorList = await _yamlCore.GetSettingAsync<Dictionary<string, string>>(
                YamlStore.Game, "Crashlog_Error_Check", null, cancellationToken)
            .ConfigureAwait(false) ?? new Dictionary<string, string>();

        var suspectStackList = await _yamlCore.GetSettingAsync<Dictionary<string, List<string>>>(
                YamlStore.Game, "Crashlog_Stack_Check", null, cancellationToken)
            .ConfigureAwait(false) ?? new Dictionary<string, List<string>>();

        LogDebug("Loaded {ErrorCount} error patterns and {StackCount} stack patterns",
            suspectErrorList.Count, suspectStackList.Count);

        var fragments = new List<ReportFragment>();
        var foundAnySupect = false;
        var severity = AnalysisSeverity.Info;

        // Check DLL crashes first (highest priority)
        var dllFragment = await CheckDllCrashAsync(mainError, cancellationToken).ConfigureAwait(false);
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
        var severityAssessments = new List<SeverityAssessment>();
        if (!string.IsNullOrWhiteSpace(callStack))
        {
            var (stackFragment, stackFound, stackAssessments) =
                ScanCallStack(mainError, callStack, suspectStackList, DefaultMaxWarnLength);
            if (stackFragment != null && stackFound)
            {
                fragments.Add(stackFragment);
                foundAnySupect = true;
                severityAssessments.AddRange(stackAssessments);
            }
        }

        // Calculate combined severity if we have multiple assessments
        if (severityAssessments.Count > 0)
        {
            var combinedSeverity = _severityCalculator.CalculateCombinedSeverity(severityAssessments);
            severity = combinedSeverity.FinalLevel;
            
            // Add combined severity information to metadata
            if (combinedSeverity.Explanations.Count > 0)
            {
                fragments.Add(ReportFragment.CreateInfo(
                    "Severity Assessment",
                    string.Join("\n", combinedSeverity.Explanations),
                    90));
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
            
            // Add confidence metadata if available
            if (severityAssessments.Count > 0)
            {
                var avgConfidence = severityAssessments.Average(a => a.Score);
                result.AddMetadata("AverageConfidence", $"{avgConfidence:P0}");
            }
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
            contentBuilder.AppendLine(
                $"- **Checking for {formattedErrorName} SUSPECT FOUND! > Severity : {errorSeverity}**");
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

    private (ReportFragment? fragment, bool foundSuspect, List<SeverityAssessment> assessments) ScanCallStack(
        string crashLogMainError,
        string segmentCallStackIntact,
        Dictionary<string, List<string>> suspectStackList,
        int maxWarnLength)
    {
        if (suspectStackList == null || suspectStackList.Count == 0)
            return (null, false, new List<SeverityAssessment>());

        var contentBuilder = new StringBuilder();
        var anySuspectFound = false;
        var severityAssessments = new List<SeverityAssessment>();
        
        // Perform advanced call stack analysis
        var stackAnalysis = _callStackAnalyzer.AnalyzeCallStack(segmentCallStackIntact);
        
        foreach (var stackEntry in suspectStackList)
        {
            // Parse error key format: "Severity | Error Name"
            var parts = stackEntry.Key.Split(" | ", 2);
            if (parts.Length != 2)
            {
                LogWarning("Invalid stack key format: {Key}", stackEntry.Key);
                continue;
            }

            var errorSeverity = int.TryParse(parts[0], out var severity) ? severity : 3;
            var errorName = parts[1];

            // Use advanced signal processing
            var matchResult = _signalProcessor.ProcessSignals(
                stackEntry.Value, crashLogMainError, segmentCallStackIntact);

            if (!matchResult.IsMatch)
            {
                LogDebug("No match for {ErrorName}: {Reason}", errorName, matchResult.SkipReason);
                continue;
            }

            // Calculate dynamic severity
            var severityFactors = new SeverityFactors
            {
                HasMultipleIndicators = matchResult.MatchedSignals.Count > 2,
                IsRecurring = stackAnalysis.RecursionDetected,
                AffectsGameStability = errorName.Contains("crash", StringComparison.OrdinalIgnoreCase)
            };

            var severityAssessment = _severityCalculator.CalculateSeverity(
                errorSeverity, matchResult, severityFactors);
            severityAssessments.Add(severityAssessment);

            // Format output with enhanced information
            var formattedErrorName = errorName.PadRight(maxWarnLength, '.');
            contentBuilder.AppendLine(
                $"- **Checking for {formattedErrorName} SUSPECT FOUND!**");
            contentBuilder.AppendLine($"  - **Severity**: {severityAssessment.FinalLevel} (Score: {severityAssessment.Score:P0})");
            contentBuilder.AppendLine($"  - **Confidence**: {matchResult.Confidence:P0}");
            
            if (matchResult.MatchedSignals.Count > 0)
            {
                contentBuilder.AppendLine($"  - **Matched Signals**: {matchResult.MatchedSignals.Count}");
            }
            
            if (severityAssessment.Explanations.Count > 0)
            {
                contentBuilder.AppendLine("  - **Details**: " + string.Join(", ", severityAssessment.Explanations));
            }
            
            contentBuilder.AppendLine();
            contentBuilder.AppendLine("-----");
            
            anySuspectFound = true;
            LogDebug("Found stack suspect: {ErrorName} (Severity: {Severity}, Confidence: {Confidence:P})",
                errorName, severityAssessment.FinalLevel, matchResult.Confidence);
        }

        // Add call stack analysis insights if problems detected
        if (stackAnalysis.ProblemIndicators.Count > 0)
        {
            contentBuilder.AppendLine("**Call Stack Analysis Warnings:**");
            foreach (var indicator in stackAnalysis.ProblemIndicators)
            {
                contentBuilder.AppendLine($"  - {indicator}");
            }
            contentBuilder.AppendLine();
            contentBuilder.AppendLine("-----");
        }

        if (!anySuspectFound)
            return (null, false, new List<SeverityAssessment>());

        var fragment = ReportFragment.CreateWarning(
            "Call Stack Suspects",
            contentBuilder.ToString(),
            25);

        return (fragment, true, severityAssessments);
    }

    private async Task<ReportFragment?> CheckDllCrashAsync(string crashLogMainError, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(crashLogMainError))
            return null;

        var crashLogLower = crashLogMainError.ToLowerInvariant();

        // Load DLL exclusion list from configuration
        var dllExclusions = await _yamlCore.GetSettingAsync<List<string>>(
            YamlStore.Game, "DLL_Exclusions", null, cancellationToken)
            .ConfigureAwait(false) ?? new List<string> { "tbbmalloc", "kernel32", "ntdll", "user32" };

        // Check if any DLL is mentioned
        if (!crashLogLower.Contains(".dll"))
            return null;

        // Check if it's an excluded DLL
        foreach (var exclusion in dllExclusions)
        {
            if (crashLogLower.Contains(exclusion.ToLowerInvariant()))
            {
                LogDebug("DLL {DLL} is in exclusion list, skipping", exclusion);
                return null;
            }
        }

        // Try to extract DLL name
        var dllName = ExtractDllName(crashLogMainError);
        var dllType = ClassifyDll(dllName);

        var content = new StringBuilder();
        content.AppendLine("* NOTICE : MAIN ERROR REPORTS THAT A DLL FILE WAS INVOLVED IN THIS CRASH! *");
        
        if (!string.IsNullOrEmpty(dllName))
        {
            content.AppendLine($"  - **DLL Name**: {dllName}");
            content.AppendLine($"  - **DLL Type**: {dllType}");
        }

        switch (dllType)
        {
            case DllType.Mod:
                content.AppendLine("This DLL belongs to a mod and is a prime suspect for the crash.");
                content.AppendLine("Consider disabling or updating the mod that provides this DLL.");
                break;
            case DllType.Driver:
                content.AppendLine("This appears to be a driver-related DLL (graphics/audio).");
                content.AppendLine("Consider updating your drivers to the latest version.");
                break;
            case DllType.Game:
                content.AppendLine("This is a game-related DLL. Verify game files integrity.");
                break;
            default:
                content.AppendLine("If this DLL belongs to a mod, that mod is a prime suspect for the crash.");
                break;
        }
        
        content.AppendLine();
        content.AppendLine("-----");

        return ReportFragment.CreateWarning(
            "DLL Crash Detected",
            content.ToString(),
            15);
    }

    private static string ExtractDllName(string crashLogMainError)
    {
        // Try to extract DLL name using regex
        var match = System.Text.RegularExpressions.Regex.Match(
            crashLogMainError, 
            @"(\w+\.dll)", 
            System.Text.RegularExpressions.RegexOptions.IgnoreCase);
        
        return match.Success ? match.Groups[1].Value : string.Empty;
    }

    private static DllType ClassifyDll(string dllName)
    {
        if (string.IsNullOrEmpty(dllName))
            return DllType.Unknown;

        var lowerName = dllName.ToLowerInvariant();

        // Known mod-related DLLs
        if (lowerName.Contains("f4se") || lowerName.Contains("skse") || 
            lowerName.Contains("mod") || lowerName.Contains("plugin") ||
            lowerName.Contains("cbp") || lowerName.Contains("flexrelease"))
            return DllType.Mod;

        // Driver-related DLLs
        if (lowerName.Contains("nvwgf") || lowerName.Contains("atio") || 
            lowerName.Contains("d3d") || lowerName.Contains("xaudio") ||
            lowerName.Contains("vulkan") || lowerName.Contains("dxvk"))
            return DllType.Driver;

        // Game DLLs
        if (lowerName.Contains("fallout") || lowerName.Contains("skyrim") || 
            lowerName.Contains("bethesda"))
            return DllType.Game;

        return DllType.Unknown;
    }

    private enum DllType
    {
        Unknown,
        Mod,
        Driver,
        Game,
        System
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
                matchStatus.StackFound = true;
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
                    matchStatus.ErrorReqFound = true;
                break;

            case MainErrorOptional:
                if (crashLogMainError.Contains(signalString, StringComparison.OrdinalIgnoreCase))
                    matchStatus.ErrorOptFound = true;
                break;

            case CallStackNegative:
                // Return true to signal that we should skip this error
                return segmentCallStackIntact.Contains(signalString, StringComparison.OrdinalIgnoreCase);

            default:
                // Check if modifier is numeric (minimum occurrence count)
                if (int.TryParse(signalModifier, out var minOccurrences))
                {
                    var count = CountOccurrences(segmentCallStackIntact, signalString);
                    if (count >= minOccurrences) matchStatus.StackFound = true;
                }

                break;
        }

        return false;
    }

    private static bool IsSuspectMatch(MatchStatus matchStatus)
    {
        // If we have required items, they must be found
        if (matchStatus.HasRequiredItem) return matchStatus.ErrorReqFound;

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
    ///     Tracks matching status for signal processing.
    /// </summary>
    private sealed class MatchStatus
    {
        public bool HasRequiredItem { get; set; }
        public bool ErrorReqFound { get; set; }
        public bool ErrorOptFound { get; set; }
        public bool StackFound { get; set; }
    }
}