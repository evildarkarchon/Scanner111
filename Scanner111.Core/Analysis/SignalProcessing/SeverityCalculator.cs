using Microsoft.Extensions.Logging;
using Scanner111.Core.Analysis.Analyzers;

namespace Scanner111.Core.Analysis.SignalProcessing;

/// <summary>
///     Calculates dynamic severity levels for crash suspects based on multiple factors.
///     Provides weighted scoring and severity escalation rules.
///     Thread-safe for concurrent analysis operations.
/// </summary>
public sealed class SeverityCalculator
{
    private readonly ILogger<SeverityCalculator> _logger;
    
    // Severity thresholds
    private const double CriticalThreshold = 0.8;
    private const double ErrorThreshold = 0.6;
    private const double WarningThreshold = 0.3;
    private const double InfoThreshold = 0.1;

    public SeverityCalculator(ILogger<SeverityCalculator> logger)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <summary>
    ///     Calculate severity for a suspect based on multiple factors.
    /// </summary>
    /// <param name="baseSeverity">Base severity from configuration</param>
    /// <param name="matchResult">Signal match result</param>
    /// <param name="additionalFactors">Additional factors to consider</param>
    /// <returns>Calculated severity assessment</returns>
    public SeverityAssessment CalculateSeverity(
        int baseSeverity,
        SignalMatchResult matchResult,
        SeverityFactors? additionalFactors = null)
    {
        var assessment = new SeverityAssessment
        {
            BaseSeverity = baseSeverity,
            BaseLevel = MapSeverityToLevel(baseSeverity)
        };

        if (matchResult == null || !matchResult.IsMatch)
        {
            assessment.FinalLevel = AnalysisSeverity.None;
            assessment.Score = 0.0;
            return assessment;
        }

        // Calculate base score from severity value (1-6 scale)
        var baseScore = baseSeverity / 6.0;
        
        // Apply confidence modifier
        var confidenceModifier = matchResult.Confidence * 0.3; // Up to 30% boost
        
        // Calculate signal type weights
        var signalWeight = CalculateSignalWeight(matchResult);
        
        // Apply additional factors if provided
        var factorBoost = 0.0;
        if (additionalFactors != null)
        {
            factorBoost = CalculateFactorBoost(additionalFactors);
        }

        // Calculate final score
        assessment.Score = Math.Min(1.0, baseScore + confidenceModifier + signalWeight + factorBoost);
        
        // Determine final severity level
        assessment.FinalLevel = DetermineSeverityLevel(assessment.Score);
        
        // Add explanations
        assessment.Explanations = GenerateExplanations(matchResult, additionalFactors);
        
        // Check for escalation conditions
        if (ShouldEscalate(matchResult, additionalFactors))
        {
            assessment = EscalateSeverity(assessment, matchResult, additionalFactors);
        }

        _logger.LogDebug(
            "Severity calculated: Base={Base}, Final={Final}, Score={Score:P}",
            assessment.BaseLevel, assessment.FinalLevel, assessment.Score);

        return assessment;
    }

    /// <summary>
    ///     Calculate combined severity for multiple suspects.
    /// </summary>
    /// <param name="assessments">Individual severity assessments</param>
    /// <returns>Combined severity assessment</returns>
    public SeverityAssessment CalculateCombinedSeverity(List<SeverityAssessment> assessments)
    {
        if (assessments == null || assessments.Count == 0)
        {
            return new SeverityAssessment
            {
                FinalLevel = AnalysisSeverity.None,
                Score = 0.0
            };
        }

        // Find highest individual severity
        var highest = assessments.OrderByDescending(a => a.Score).First();
        
        // Calculate cumulative effect
        var cumulativeBoost = CalculateCumulativeEffect(assessments);
        
        var combined = new SeverityAssessment
        {
            BaseSeverity = highest.BaseSeverity,
            BaseLevel = highest.BaseLevel,
            Score = Math.Min(1.0, highest.Score + cumulativeBoost),
            Explanations = new List<string> { $"Combined {assessments.Count} suspects" }
        };

        combined.FinalLevel = DetermineSeverityLevel(combined.Score);
        
        // Add cumulative warning if multiple high-severity issues
        var criticalCount = assessments.Count(a => a.FinalLevel == AnalysisSeverity.Critical);
        var errorCount = assessments.Count(a => a.FinalLevel == AnalysisSeverity.Error);
        
        if (criticalCount > 1)
        {
            combined.Explanations.Add($"Multiple critical issues detected ({criticalCount})");
            combined.FinalLevel = AnalysisSeverity.Critical;
        }
        else if (errorCount > 2)
        {
            combined.Explanations.Add($"Multiple error-level issues detected ({errorCount})");
            if (combined.FinalLevel < AnalysisSeverity.Error)
                combined.FinalLevel = AnalysisSeverity.Error;
        }

        return combined;
    }

    private double CalculateSignalWeight(SignalMatchResult matchResult)
    {
        var weight = 0.0;
        
        // Required signals add significant weight
        if (matchResult.RequiredTotal > 0)
        {
            weight += 0.15 * ((double)matchResult.RequiredMatches / matchResult.RequiredTotal);
        }
        
        // Multiple matches increase severity
        var totalMatches = matchResult.MatchedSignals.Count;
        if (totalMatches > 3)
        {
            weight += 0.05 * Math.Min(3, totalMatches - 3); // Up to 15% for many matches
        }
        
        // High occurrence counts increase severity
        var highOccurrenceSignals = matchResult.MatchedSignals
            .Count(s => s.Occurrences > 5);
        if (highOccurrenceSignals > 0)
        {
            weight += 0.03 * highOccurrenceSignals; // 3% per high-occurrence signal
        }

        return weight;
    }

    private double CalculateFactorBoost(SeverityFactors factors)
    {
        var boost = 0.0;
        
        if (factors.IsDllCrash)
            boost += 0.1; // DLL crashes are more serious
            
        if (factors.IsRecurring)
            boost += 0.15; // Recurring crashes need attention
            
        if (factors.HasMultipleIndicators)
            boost += 0.1; // Multiple indicators suggest serious issue
            
        if (factors.IsKnownCriticalPattern)
            boost += 0.2; // Known critical patterns get highest boost
            
        if (factors.AffectsGameStability)
            boost += 0.15; // Stability issues are serious
            
        return boost;
    }

    private bool ShouldEscalate(SignalMatchResult matchResult, SeverityFactors? factors)
    {
        // Escalate if all required signals matched with high confidence
        if (matchResult.RequiredTotal > 0 && 
            matchResult.RequiredMatches == matchResult.RequiredTotal &&
            matchResult.Confidence > 0.9)
        {
            return true;
        }
        
        // Escalate for known critical patterns
        if (factors?.IsKnownCriticalPattern == true)
        {
            return true;
        }
        
        // Escalate for recurring stability issues
        if (factors?.IsRecurring == true && factors?.AffectsGameStability == true)
        {
            return true;
        }

        return false;
    }

    private SeverityAssessment EscalateSeverity(
        SeverityAssessment assessment,
        SignalMatchResult matchResult,
        SeverityFactors? factors)
    {
        // Escalate by one level, but not beyond Critical
        var newLevel = assessment.FinalLevel switch
        {
            AnalysisSeverity.None => AnalysisSeverity.Info,
            AnalysisSeverity.Info => AnalysisSeverity.Warning,
            AnalysisSeverity.Warning => AnalysisSeverity.Error,
            AnalysisSeverity.Error => AnalysisSeverity.Critical,
            _ => assessment.FinalLevel
        };

        if (newLevel != assessment.FinalLevel)
        {
            assessment.FinalLevel = newLevel;
            assessment.WasEscalated = true;
            assessment.Explanations.Add("Severity escalated due to critical indicators");
            
            _logger.LogDebug("Severity escalated from {Original} to {New}",
                assessment.FinalLevel, newLevel);
        }

        return assessment;
    }

    private double CalculateCumulativeEffect(List<SeverityAssessment> assessments)
    {
        if (assessments.Count <= 1)
            return 0.0;
            
        // Each additional suspect adds diminishing boost
        var boost = 0.0;
        for (int i = 1; i < Math.Min(5, assessments.Count); i++)
        {
            boost += 0.05 / i; // 5%, 2.5%, 1.67%, 1.25%...
        }
        
        return boost;
    }

    private List<string> GenerateExplanations(SignalMatchResult matchResult, SeverityFactors? factors)
    {
        var explanations = new List<string>();
        
        if (matchResult.RequiredMatches > 0)
        {
            explanations.Add($"All {matchResult.RequiredMatches} required conditions met");
        }
        
        if (matchResult.Confidence > 0.8)
        {
            explanations.Add($"High confidence match ({matchResult.Confidence:P0})");
        }
        
        if (factors?.IsDllCrash == true)
        {
            explanations.Add("DLL crash detected");
        }
        
        if (factors?.IsRecurring == true)
        {
            explanations.Add("Recurring crash pattern");
        }
        
        if (factors?.IsKnownCriticalPattern == true)
        {
            explanations.Add("Known critical crash pattern");
        }

        return explanations;
    }

    private AnalysisSeverity MapSeverityToLevel(int severity)
    {
        return severity switch
        {
            >= 5 => AnalysisSeverity.Critical,
            >= 4 => AnalysisSeverity.Error,
            >= 3 => AnalysisSeverity.Warning,
            >= 1 => AnalysisSeverity.Info,
            _ => AnalysisSeverity.None
        };
    }

    private AnalysisSeverity DetermineSeverityLevel(double score)
    {
        return score switch
        {
            >= CriticalThreshold => AnalysisSeverity.Critical,
            >= ErrorThreshold => AnalysisSeverity.Error,
            >= WarningThreshold => AnalysisSeverity.Warning,
            >= InfoThreshold => AnalysisSeverity.Info,
            _ => AnalysisSeverity.None
        };
    }
}

/// <summary>
///     Additional factors that can influence severity calculation.
/// </summary>
public sealed class SeverityFactors
{
    public bool IsDllCrash { get; init; }
    public bool IsRecurring { get; init; }
    public bool HasMultipleIndicators { get; init; }
    public bool IsKnownCriticalPattern { get; init; }
    public bool AffectsGameStability { get; init; }
    public int CrashFrequency { get; init; }
    public List<string>? RelatedMods { get; init; }
}

/// <summary>
///     Result of severity assessment with detailed information.
/// </summary>
public sealed class SeverityAssessment
{
    public int BaseSeverity { get; init; }
    public AnalysisSeverity BaseLevel { get; init; }
    public AnalysisSeverity FinalLevel { get; set; }
    public double Score { get; set; }
    public bool WasEscalated { get; set; }
    public List<string> Explanations { get; set; } = new();
}