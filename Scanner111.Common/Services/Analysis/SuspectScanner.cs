using System.Collections.Concurrent;
using System.Text.RegularExpressions;
using Scanner111.Common.Models.Analysis;

namespace Scanner111.Common.Services.Analysis;

/// <summary>
/// Scans crash logs for known suspect patterns in errors and call stacks.
/// </summary>
public class SuspectScanner : ISuspectScanner
{
    private readonly ConcurrentDictionary<string, Regex> _compiledPatterns;

    /// <summary>
    /// Initializes a new instance of the <see cref="SuspectScanner"/> class.
    /// </summary>
    public SuspectScanner()
    {
        _compiledPatterns = new ConcurrentDictionary<string, Regex>();
    }

    /// <inheritdoc/>
    public async Task<SuspectScanResult> ScanAsync(
        CrashHeader header,
        IReadOnlyList<LogSegment> segments,
        SuspectPatterns patterns,
        CancellationToken cancellationToken = default)
    {
        // Allow async operation to be cancelled
        await Task.Yield();
        cancellationToken.ThrowIfCancellationRequested();

        var errorMatches = new List<string>();
        var stackMatches = new List<string>();
        var recommendations = new List<string>();

        // Scan main error
        if (!string.IsNullOrWhiteSpace(header.MainError))
        {
            var errorResults = ScanMainError(header.MainError, patterns.ErrorPatterns);
            errorMatches.AddRange(errorResults.Matches);
            recommendations.AddRange(errorResults.Recommendations);
        }

        // Scan call stack
        var stackSegment = segments.FirstOrDefault(s =>
            s.Name.Equals("PROBABLE CALL STACK", StringComparison.OrdinalIgnoreCase) ||
            s.Name.Contains("CALL STACK", StringComparison.OrdinalIgnoreCase));

        if (stackSegment != null)
        {
            var stackResults = ScanCallStack(stackSegment, patterns.StackSignatures);
            stackMatches.AddRange(stackResults.Matches);
            recommendations.AddRange(stackResults.Recommendations);
        }

        return new SuspectScanResult
        {
            ErrorMatches = errorMatches,
            StackMatches = stackMatches,
            Recommendations = recommendations.Distinct().ToList()
        };
    }

    private ScanResults ScanMainError(string mainError, IReadOnlyList<SuspectPattern> patterns)
    {
        var matches = new List<string>();
        var recommendations = new List<string>();

        foreach (var pattern in patterns)
        {
            var regex = GetOrCompileRegex(pattern.Pattern);
            if (regex.IsMatch(mainError))
            {
                matches.Add(pattern.Message);
                recommendations.AddRange(pattern.Recommendations);
            }
        }

        return new ScanResults { Matches = matches, Recommendations = recommendations };
    }

    private ScanResults ScanCallStack(LogSegment stackSegment, IReadOnlyList<SuspectPattern> patterns)
    {
        var matches = new List<string>();
        var recommendations = new List<string>();

        // Combine all stack lines into a single string for pattern matching
        var stackContent = string.Join("\n", stackSegment.Lines);

        foreach (var pattern in patterns)
        {
            var regex = GetOrCompileRegex(pattern.Pattern);
            if (regex.IsMatch(stackContent))
            {
                matches.Add(pattern.Message);
                recommendations.AddRange(pattern.Recommendations);
            }
        }

        return new ScanResults { Matches = matches, Recommendations = recommendations };
    }

    private Regex GetOrCompileRegex(string pattern)
    {
        return _compiledPatterns.GetOrAdd(pattern, p =>
            new Regex(p, RegexOptions.Compiled | RegexOptions.IgnoreCase));
    }

    private record ScanResults
    {
        public List<string> Matches { get; init; } = new();
        public List<string> Recommendations { get; init; } = new();
    }
}
