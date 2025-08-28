using System.Text.RegularExpressions;
using Microsoft.Extensions.Logging;

namespace Scanner111.Core.Analysis.SignalProcessing;

/// <summary>
///     Advanced signal processor for crash log pattern matching.
///     Handles complex signal combinations, priority ordering, and occurrence thresholds.
///     Thread-safe for concurrent analysis operations.
/// </summary>
public sealed class SignalProcessor
{
    private readonly ILogger<SignalProcessor> _logger;

    public SignalProcessor(ILogger<SignalProcessor> logger)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <summary>
    ///     Process a list of signals against crash log data.
    /// </summary>
    /// <param name="signals">List of signals to process</param>
    /// <param name="mainError">Main error from crash log</param>
    /// <param name="callStack">Call stack from crash log</param>
    /// <returns>Signal match result with detailed information</returns>
    public SignalMatchResult ProcessSignals(
        List<string> signals,
        string mainError,
        string callStack)
    {
        if (signals == null || signals.Count == 0)
            return new SignalMatchResult { IsMatch = false };

        mainError = mainError ?? string.Empty;
        callStack = callStack ?? string.Empty;

        var result = new SignalMatchResult();
        var signalGroups = GroupSignalsByType(signals);

        // Process signals by priority: Required -> Negative -> Optional -> Stack
        // If any NOT condition is met, immediately return no match
        if (ProcessNegativeSignals(signalGroups.NegativeSignals, mainError, callStack))
        {
            _logger.LogDebug("Negative signal matched, skipping suspect");
            result.IsMatch = false;
            result.SkipReason = "Negative condition met";
            return result;
        }

        // Process required signals - all must match
        if (signalGroups.RequiredSignals.Count > 0)
        {
            var requiredMatches = ProcessRequiredSignals(signalGroups.RequiredSignals, mainError);
            result.RequiredMatches = requiredMatches.Count;
            result.RequiredTotal = signalGroups.RequiredSignals.Count;

            if (requiredMatches.Count != signalGroups.RequiredSignals.Count)
            {
                _logger.LogDebug("Not all required signals matched: {Matched}/{Total}",
                    requiredMatches.Count, signalGroups.RequiredSignals.Count);
                result.IsMatch = false;
                result.SkipReason = "Required signals not met";
                return result;
            }

            result.MatchedSignals.AddRange(requiredMatches);
        }

        // Process optional signals - any match counts
        if (signalGroups.OptionalSignals.Count > 0)
        {
            var optionalMatches = ProcessOptionalSignals(signalGroups.OptionalSignals, mainError);
            result.OptionalMatches = optionalMatches.Count;
            result.OptionalTotal = signalGroups.OptionalSignals.Count;
            result.MatchedSignals.AddRange(optionalMatches);
        }

        // Process stack signals with occurrence thresholds
        if (signalGroups.StackSignals.Count > 0)
        {
            var stackMatches = ProcessStackSignals(signalGroups.StackSignals, callStack);
            result.StackMatches = stackMatches.Count;
            result.StackTotal = signalGroups.StackSignals.Count;
            result.MatchedSignals.AddRange(stackMatches);
        }

        // Determine if we have a match
        result.IsMatch = DetermineMatch(result);
        result.Confidence = CalculateConfidence(result);

        _logger.LogDebug("Signal processing complete: Match={IsMatch}, Confidence={Confidence:P}",
            result.IsMatch, result.Confidence);

        return result;
    }

    private SignalGroups GroupSignalsByType(List<string> signals)
    {
        var groups = new SignalGroups();

        foreach (var signal in signals)
        {
            if (signal.StartsWith("NOT|"))
            {
                groups.NegativeSignals.Add(signal);
            }
            else if (signal.StartsWith("ME-REQ|"))
            {
                groups.RequiredSignals.Add(signal);
            }
            else if (signal.StartsWith("ME-OPT|"))
            {
                groups.OptionalSignals.Add(signal);
            }
            else
            {
                groups.StackSignals.Add(signal);
            }
        }

        return groups;
    }

    private bool ProcessNegativeSignals(List<string> signals, string mainError, string callStack)
    {
        foreach (var signal in signals)
        {
            var pattern = ExtractSignalPattern(signal);
            if (string.IsNullOrEmpty(pattern))
                continue;

            // Check both main error and call stack for negative patterns
            if (mainError.Contains(pattern, StringComparison.OrdinalIgnoreCase) ||
                callStack.Contains(pattern, StringComparison.OrdinalIgnoreCase))
            {
                _logger.LogDebug("Negative signal matched: {Pattern}", pattern);
                return true;
            }
        }

        return false;
    }

    private List<SignalMatch> ProcessRequiredSignals(List<string> signals, string mainError)
    {
        var matches = new List<SignalMatch>();

        foreach (var signal in signals)
        {
            var pattern = ExtractSignalPattern(signal);
            if (string.IsNullOrEmpty(pattern))
                continue;

            if (mainError.Contains(pattern, StringComparison.OrdinalIgnoreCase))
            {
                matches.Add(new SignalMatch
                {
                    Signal = signal,
                    Pattern = pattern,
                    Type = SignalType.Required,
                    Location = SignalLocation.MainError,
                    Occurrences = CountOccurrences(mainError, pattern)
                });
            }
        }

        return matches;
    }

    private List<SignalMatch> ProcessOptionalSignals(List<string> signals, string mainError)
    {
        var matches = new List<SignalMatch>();

        foreach (var signal in signals)
        {
            var pattern = ExtractSignalPattern(signal);
            if (string.IsNullOrEmpty(pattern))
                continue;

            if (mainError.Contains(pattern, StringComparison.OrdinalIgnoreCase))
            {
                matches.Add(new SignalMatch
                {
                    Signal = signal,
                    Pattern = pattern,
                    Type = SignalType.Optional,
                    Location = SignalLocation.MainError,
                    Occurrences = CountOccurrences(mainError, pattern)
                });
            }
        }

        return matches;
    }

    private List<SignalMatch> ProcessStackSignals(List<string> signals, string callStack)
    {
        var matches = new List<SignalMatch>();

        foreach (var signal in signals)
        {
            var (pattern, minOccurrences, maxOccurrences) = ParseStackSignal(signal);
            if (string.IsNullOrEmpty(pattern))
                continue;

            var occurrences = CountOccurrences(callStack, pattern);

            // Check if occurrences meet the threshold
            var meetsThreshold = minOccurrences.HasValue
                ? occurrences >= minOccurrences.Value &&
                  (!maxOccurrences.HasValue || occurrences <= maxOccurrences.Value)
                : occurrences > 0;

            if (meetsThreshold)
            {
                matches.Add(new SignalMatch
                {
                    Signal = signal,
                    Pattern = pattern,
                    Type = SignalType.Stack,
                    Location = SignalLocation.CallStack,
                    Occurrences = occurrences,
                    MinOccurrences = minOccurrences,
                    MaxOccurrences = maxOccurrences
                });
            }
        }

        return matches;
    }

    private (string pattern, int? min, int? max) ParseStackSignal(string signal)
    {
        // Handle range format: "3-5|pattern"
        var rangeMatch = Regex.Match(signal, @"^(\d+)-(\d+)\|(.+)$");
        if (rangeMatch.Success)
        {
            return (
                rangeMatch.Groups[3].Value,
                int.Parse(rangeMatch.Groups[1].Value),
                int.Parse(rangeMatch.Groups[2].Value)
            );
        }

        // Handle minimum format: "3|pattern"
        var minMatch = Regex.Match(signal, @"^(\d+)\|(.+)$");
        if (minMatch.Success)
        {
            return (
                minMatch.Groups[2].Value,
                int.Parse(minMatch.Groups[1].Value),
                null
            );
        }

        // Simple pattern without occurrence requirement
        return (signal, null, null);
    }

    private string ExtractSignalPattern(string signal)
    {
        var separatorIndex = signal.IndexOf('|');
        return separatorIndex > 0 && separatorIndex < signal.Length - 1
            ? signal.Substring(separatorIndex + 1)
            : signal;
    }

    private int CountOccurrences(string text, string pattern)
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

    private bool DetermineMatch(SignalMatchResult result)
    {
        // If we have required signals, all must match
        if (result.RequiredTotal > 0)
            return result.RequiredMatches == result.RequiredTotal;

        // Otherwise, any optional or stack match counts
        return result.OptionalMatches > 0 || result.StackMatches > 0;
    }

    private double CalculateConfidence(SignalMatchResult result)
    {
        if (!result.IsMatch)
            return 0.0;

        var confidence = 0.0;
        var totalWeight = 0.0;

        // Required signals have highest weight
        if (result.RequiredTotal > 0)
        {
            confidence += (double)result.RequiredMatches / result.RequiredTotal * 0.5;
            totalWeight += 0.5;
        }

        // Optional signals have medium weight
        if (result.OptionalTotal > 0)
        {
            confidence += (double)result.OptionalMatches / result.OptionalTotal * 0.3;
            totalWeight += 0.3;
        }

        // Stack signals have lower weight
        if (result.StackTotal > 0)
        {
            confidence += (double)result.StackMatches / result.StackTotal * 0.2;
            totalWeight += 0.2;
        }

        return totalWeight > 0 ? confidence / totalWeight : 0.0;
    }

    /// <summary>
    ///     Groups signals by their type for processing.
    /// </summary>
    private sealed class SignalGroups
    {
        public List<string> RequiredSignals { get; } = new();
        public List<string> OptionalSignals { get; } = new();
        public List<string> StackSignals { get; } = new();
        public List<string> NegativeSignals { get; } = new();
    }
}

/// <summary>
///     Result of signal processing with detailed match information.
/// </summary>
public sealed class SignalMatchResult
{
    public bool IsMatch { get; set; }
    public double Confidence { get; set; }
    public string? SkipReason { get; set; }

    public int RequiredMatches { get; set; }
    public int RequiredTotal { get; set; }
    public int OptionalMatches { get; set; }
    public int OptionalTotal { get; set; }
    public int StackMatches { get; set; }
    public int StackTotal { get; set; }

    public List<SignalMatch> MatchedSignals { get; } = new();
}

/// <summary>
///     Represents a single signal match.
/// </summary>
public sealed class SignalMatch
{
    public required string Signal { get; init; }
    public required string Pattern { get; init; }
    public required SignalType Type { get; init; }
    public required SignalLocation Location { get; init; }
    public int Occurrences { get; init; }
    public int? MinOccurrences { get; init; }
    public int? MaxOccurrences { get; init; }
}

/// <summary>
///     Type of signal for processing priority.
/// </summary>
public enum SignalType
{
    Required,
    Optional,
    Stack,
    Negative
}

/// <summary>
///     Location where signal was found.
/// </summary>
public enum SignalLocation
{
    MainError,
    CallStack,
    Both
}