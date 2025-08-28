using System.Text.RegularExpressions;
using Microsoft.Extensions.Logging;

namespace Scanner111.Core.Analysis.SignalProcessing;

/// <summary>
///     Advanced call stack analyzer for pattern detection and analysis.
///     Handles pattern sequences, depth analysis, clustering, and statistical analysis.
///     Thread-safe for concurrent analysis operations.
/// </summary>
public sealed class CallStackAnalyzer
{
    private readonly ILogger<CallStackAnalyzer> _logger;
    
    // Regular expression for parsing call stack lines
    private static readonly Regex StackLineRegex = new(
        @"^\s*\[(\d+)\]\s+(0x[0-9A-Fa-f]+)\s+(.+?)(?:\+([0-9A-Fa-f]+))?\s*(?:->(.+))?$",
        RegexOptions.Compiled | RegexOptions.IgnoreCase);

    public CallStackAnalyzer(ILogger<CallStackAnalyzer> logger)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <summary>
    ///     Analyze a call stack for advanced patterns and characteristics.
    /// </summary>
    /// <param name="callStack">Raw call stack string</param>
    /// <param name="patterns">Optional patterns to search for</param>
    /// <returns>Detailed call stack analysis</returns>
    public CallStackAnalysis AnalyzeCallStack(string callStack, List<string>? patterns = null)
    {
        if (string.IsNullOrWhiteSpace(callStack))
        {
            return new CallStackAnalysis { IsValid = false };
        }

        var analysis = new CallStackAnalysis { IsValid = true };
        var stackFrames = ParseCallStack(callStack);
        
        analysis.TotalFrames = stackFrames.Count;
        analysis.Frames = stackFrames;

        // Perform various analyses
        analysis.ModuleCounts = AnalyzeModules(stackFrames);
        analysis.RecursionDetected = DetectRecursion(stackFrames);
        analysis.PatternClusters = FindPatternClusters(stackFrames);
        analysis.DepthStatistics = CalculateDepthStatistics(stackFrames);

        // Check for specific patterns if provided
        if (patterns != null && patterns.Count > 0)
        {
            analysis.PatternMatches = FindPatternSequences(stackFrames, patterns);
            analysis.PatternDepths = AnalyzePatternDepths(stackFrames, patterns);
        }

        // Identify potential problem areas
        analysis.ProblemIndicators = IdentifyProblemIndicators(analysis);

        _logger.LogDebug(
            "Call stack analyzed: Frames={Frames}, Modules={Modules}, Problems={Problems}",
            analysis.TotalFrames, analysis.ModuleCounts.Count, analysis.ProblemIndicators.Count);

        return analysis;
    }

    /// <summary>
    ///     Find sequences of patterns that appear in order in the call stack.
    /// </summary>
    /// <param name="frames">Parsed stack frames</param>
    /// <param name="patternSequence">Ordered list of patterns to find</param>
    /// <returns>True if the sequence is found in order</returns>
    public bool FindOrderedSequence(List<StackFrame> frames, List<string> patternSequence)
    {
        if (frames.Count == 0 || patternSequence.Count == 0)
            return false;

        var sequenceIndex = 0;
        
        foreach (var frame in frames)
        {
            if (FrameMatchesPattern(frame, patternSequence[sequenceIndex]))
            {
                sequenceIndex++;
                if (sequenceIndex >= patternSequence.Count)
                {
                    _logger.LogDebug("Found complete pattern sequence");
                    return true;
                }
            }
        }

        return false;
    }

    /// <summary>
    ///     Perform statistical analysis on pattern occurrences.
    /// </summary>
    /// <param name="frames">Parsed stack frames</param>
    /// <param name="pattern">Pattern to analyze</param>
    /// <returns>Statistical information about the pattern</returns>
    public PatternStatistics AnalyzePatternStatistics(List<StackFrame> frames, string pattern)
    {
        var stats = new PatternStatistics { Pattern = pattern };
        var occurrences = new List<int>();

        for (int i = 0; i < frames.Count; i++)
        {
            if (FrameMatchesPattern(frames[i], pattern))
            {
                occurrences.Add(i);
                stats.TotalOccurrences++;
            }
        }

        if (stats.TotalOccurrences > 0)
        {
            stats.FirstOccurrenceDepth = occurrences.First();
            stats.LastOccurrenceDepth = occurrences.Last();
            stats.AverageDepth = occurrences.Average();
            
            // Calculate clustering coefficient (how close occurrences are)
            if (occurrences.Count > 1)
            {
                var distances = new List<int>();
                for (int i = 1; i < occurrences.Count; i++)
                {
                    distances.Add(occurrences[i] - occurrences[i - 1]);
                }
                stats.ClusteringCoefficient = 1.0 / (1.0 + distances.Average());
            }
        }

        return stats;
    }

    private List<StackFrame> ParseCallStack(string callStack)
    {
        var frames = new List<StackFrame>();
        var lines = callStack.Split('\n', StringSplitOptions.RemoveEmptyEntries);

        foreach (var line in lines)
        {
            var match = StackLineRegex.Match(line);
            if (match.Success)
            {
                var frame = new StackFrame
                {
                    Index = int.Parse(match.Groups[1].Value),
                    Address = match.Groups[2].Value,
                    Module = ExtractModuleName(match.Groups[3].Value),
                    Function = match.Groups[5].Success ? match.Groups[5].Value : match.Groups[3].Value,
                    Offset = match.Groups[4].Success ? match.Groups[4].Value : null,
                    RawLine = line
                };
                frames.Add(frame);
            }
            else if (line.Contains("0x", StringComparison.OrdinalIgnoreCase))
            {
                // Fallback for non-standard formats
                var frame = new StackFrame
                {
                    Index = frames.Count,
                    RawLine = line,
                    Module = ExtractModuleFromLine(line),
                    Function = ExtractFunctionFromLine(line)
                };
                frames.Add(frame);
            }
        }

        return frames;
    }

    private Dictionary<string, int> AnalyzeModules(List<StackFrame> frames)
    {
        var moduleCounts = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        
        foreach (var frame in frames)
        {
            if (!string.IsNullOrEmpty(frame.Module))
            {
                moduleCounts.TryGetValue(frame.Module, out var count);
                moduleCounts[frame.Module] = count + 1;
            }
        }

        return moduleCounts;
    }

    private bool DetectRecursion(List<StackFrame> frames)
    {
        var functionCounts = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        
        foreach (var frame in frames)
        {
            if (!string.IsNullOrEmpty(frame.Function))
            {
                functionCounts.TryGetValue(frame.Function, out var count);
                functionCounts[frame.Function] = count + 1;
                
                // If same function appears more than 3 times, likely recursion
                if (count + 1 > 3)
                {
                    _logger.LogDebug("Recursion detected for function: {Function}", frame.Function);
                    return true;
                }
            }
        }

        // Also check for direct recursion (same function in consecutive frames)
        for (int i = 1; i < frames.Count; i++)
        {
            if (!string.IsNullOrEmpty(frames[i].Function) &&
                frames[i].Function == frames[i - 1].Function)
            {
                _logger.LogDebug("Direct recursion detected: {Function}", frames[i].Function);
                return true;
            }
        }

        return false;
    }

    private List<PatternCluster> FindPatternClusters(List<StackFrame> frames)
    {
        var clusters = new List<PatternCluster>();
        var moduleGroups = new Dictionary<string, List<int>>();

        // Group frame indices by module
        for (int i = 0; i < frames.Count; i++)
        {
            if (!string.IsNullOrEmpty(frames[i].Module))
            {
                if (!moduleGroups.ContainsKey(frames[i].Module))
                    moduleGroups[frames[i].Module] = new List<int>();
                moduleGroups[frames[i].Module].Add(i);
            }
        }

        // Find clusters (consecutive frames from same module)
        foreach (var kvp in moduleGroups)
        {
            if (kvp.Value.Count < 2)
                continue;

            var currentCluster = new List<int> { kvp.Value[0] };
            
            for (int i = 1; i < kvp.Value.Count; i++)
            {
                if (kvp.Value[i] - kvp.Value[i - 1] <= 2) // Allow gap of 1 frame
                {
                    currentCluster.Add(kvp.Value[i]);
                }
                else
                {
                    if (currentCluster.Count >= 2)
                    {
                        clusters.Add(new PatternCluster
                        {
                            Module = kvp.Key,
                            FrameIndices = new List<int>(currentCluster),
                            Size = currentCluster.Count
                        });
                    }
                    currentCluster = new List<int> { kvp.Value[i] };
                }
            }
            
            if (currentCluster.Count >= 2)
            {
                clusters.Add(new PatternCluster
                {
                    Module = kvp.Key,
                    FrameIndices = currentCluster,
                    Size = currentCluster.Count
                });
            }
        }

        return clusters;
    }

    private DepthStatistics CalculateDepthStatistics(List<StackFrame> frames)
    {
        var stats = new DepthStatistics
        {
            MaxDepth = frames.Count,
            AverageModuleDepth = new Dictionary<string, double>()
        };

        // Calculate average depth for each module
        var moduleDepths = new Dictionary<string, List<int>>();
        
        for (int i = 0; i < frames.Count; i++)
        {
            if (!string.IsNullOrEmpty(frames[i].Module))
            {
                if (!moduleDepths.ContainsKey(frames[i].Module))
                    moduleDepths[frames[i].Module] = new List<int>();
                moduleDepths[frames[i].Module].Add(i);
            }
        }

        foreach (var kvp in moduleDepths)
        {
            stats.AverageModuleDepth[kvp.Key] = kvp.Value.Average();
        }

        // Find critical depth (where most crashes occur)
        if (frames.Count > 0)
        {
            // Heuristic: Critical issues often occur in top 30% of stack
            stats.CriticalDepth = (int)(frames.Count * 0.3);
        }

        return stats;
    }

    private List<PatternMatch> FindPatternSequences(List<StackFrame> frames, List<string> patterns)
    {
        var matches = new List<PatternMatch>();
        
        foreach (var pattern in patterns)
        {
            for (int i = 0; i < frames.Count; i++)
            {
                if (FrameMatchesPattern(frames[i], pattern))
                {
                    matches.Add(new PatternMatch
                    {
                        Pattern = pattern,
                        FrameIndex = i,
                        Module = frames[i].Module,
                        Function = frames[i].Function
                    });
                }
            }
        }

        return matches;
    }

    private Dictionary<string, List<int>> AnalyzePatternDepths(List<StackFrame> frames, List<string> patterns)
    {
        var depths = new Dictionary<string, List<int>>();
        
        foreach (var pattern in patterns)
        {
            var patternDepths = new List<int>();
            
            for (int i = 0; i < frames.Count; i++)
            {
                if (FrameMatchesPattern(frames[i], pattern))
                {
                    patternDepths.Add(i);
                }
            }
            
            if (patternDepths.Count > 0)
            {
                depths[pattern] = patternDepths;
            }
        }

        return depths;
    }

    private List<string> IdentifyProblemIndicators(CallStackAnalysis analysis)
    {
        var indicators = new List<string>();
        
        if (analysis.RecursionDetected)
        {
            indicators.Add("Possible infinite recursion detected");
        }
        
        // Check for deep call stacks (potential stack overflow)
        if (analysis.TotalFrames > 100)
        {
            indicators.Add($"Very deep call stack ({analysis.TotalFrames} frames)");
        }
        
        // Check for clustering in error-prone modules
        foreach (var cluster in analysis.PatternClusters)
        {
            if (IsKnownProblemModule(cluster.Module) && cluster.Size > 3)
            {
                indicators.Add($"Multiple calls in problematic module: {cluster.Module}");
            }
        }
        
        // Check for unbalanced module distribution
        if (analysis.ModuleCounts.Count > 0)
        {
            var maxCount = analysis.ModuleCounts.Values.Max();
            var totalCount = analysis.ModuleCounts.Values.Sum();
            
            if (maxCount > totalCount * 0.6) // One module has >60% of frames
            {
                var dominantModule = analysis.ModuleCounts.First(kvp => kvp.Value == maxCount).Key;
                indicators.Add($"Call stack dominated by {dominantModule}");
            }
        }

        return indicators;
    }

    private bool FrameMatchesPattern(StackFrame frame, string pattern)
    {
        if (string.IsNullOrEmpty(pattern))
            return false;

        var searchText = $"{frame.Module} {frame.Function} {frame.RawLine}";
        return searchText.Contains(pattern, StringComparison.OrdinalIgnoreCase);
    }

    private string ExtractModuleName(string text)
    {
        // Extract module name from formats like "Module.dll+offset" or "Module.exe+offset"
        var match = Regex.Match(text, @"^([^+]+\.(?:dll|exe))", RegexOptions.IgnoreCase);
        return match.Success ? match.Groups[1].Value : text.Split('+')[0].Trim();
    }

    private string ExtractModuleFromLine(string line)
    {
        var match = Regex.Match(line, @"(\w+\.(?:dll|exe))", RegexOptions.IgnoreCase);
        return match.Success ? match.Groups[1].Value : "Unknown";
    }

    private string ExtractFunctionFromLine(string line)
    {
        // Try to extract function name from various formats
        var match = Regex.Match(line, @"->\s*(\w+)", RegexOptions.IgnoreCase);
        if (match.Success)
            return match.Groups[1].Value;

        // Fallback to extracting any word after module name
        match = Regex.Match(line, @"\.(?:dll|exe)[^a-zA-Z]*(\w+)", RegexOptions.IgnoreCase);
        return match.Success ? match.Groups[1].Value : "Unknown";
    }

    private bool IsKnownProblemModule(string module)
    {
        // List of modules known to cause issues
        var problemModules = new[]
        {
            "nvwgf2umx.dll", // Nvidia drivers
            "atio6axx.dll",  // AMD drivers
            "d3d11.dll",     // DirectX issues
            "kernelbase.dll" // System crashes
        };

        return problemModules.Any(pm => 
            module.Contains(pm, StringComparison.OrdinalIgnoreCase));
    }
}

/// <summary>
///     Represents a parsed stack frame.
/// </summary>
public sealed class StackFrame
{
    public int Index { get; init; }
    public string? Address { get; init; }
    public string? Module { get; init; }
    public string? Function { get; init; }
    public string? Offset { get; init; }
    public required string RawLine { get; init; }
}

/// <summary>
///     Results of call stack analysis.
/// </summary>
public sealed class CallStackAnalysis
{
    public bool IsValid { get; set; }
    public int TotalFrames { get; set; }
    public List<StackFrame> Frames { get; set; } = new();
    public Dictionary<string, int> ModuleCounts { get; set; } = new();
    public bool RecursionDetected { get; set; }
    public List<PatternCluster> PatternClusters { get; set; } = new();
    public DepthStatistics DepthStatistics { get; set; } = new();
    public List<PatternMatch> PatternMatches { get; set; } = new();
    public Dictionary<string, List<int>> PatternDepths { get; set; } = new();
    public List<string> ProblemIndicators { get; set; } = new();
}

/// <summary>
///     Represents a cluster of related patterns in the call stack.
/// </summary>
public sealed class PatternCluster
{
    public required string Module { get; init; }
    public required List<int> FrameIndices { get; init; }
    public int Size { get; init; }
}

/// <summary>
///     Statistics about call stack depth.
/// </summary>
public sealed class DepthStatistics
{
    public int MaxDepth { get; set; }
    public int CriticalDepth { get; set; }
    public Dictionary<string, double> AverageModuleDepth { get; set; } = new();
}

/// <summary>
///     Represents a pattern match in the call stack.
/// </summary>
public sealed class PatternMatch
{
    public required string Pattern { get; init; }
    public int FrameIndex { get; init; }
    public string? Module { get; init; }
    public string? Function { get; init; }
}

/// <summary>
///     Statistical information about a pattern.
/// </summary>
public sealed class PatternStatistics
{
    public required string Pattern { get; init; }
    public int TotalOccurrences { get; set; }
    public int FirstOccurrenceDepth { get; set; }
    public int LastOccurrenceDepth { get; set; }
    public double AverageDepth { get; set; }
    public double ClusteringCoefficient { get; set; }
}