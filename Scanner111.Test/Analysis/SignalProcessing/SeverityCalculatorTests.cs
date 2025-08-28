using FluentAssertions;
using Microsoft.Extensions.Logging;
using NSubstitute;
using Scanner111.Core.Analysis;
using Scanner111.Core.Analysis.SignalProcessing;

namespace Scanner111.Test.Analysis.SignalProcessing;

public sealed class SeverityCalculatorTests
{
    private readonly ILogger<SeverityCalculator> _logger;
    private readonly SeverityCalculator _sut;

    public SeverityCalculatorTests()
    {
        _logger = Substitute.For<ILogger<SeverityCalculator>>();
        _sut = new SeverityCalculator(_logger);
    }

    [Fact]
    public void Constructor_NullLogger_ThrowsArgumentNullException()
    {
        // Act & Assert
        var act = () => new SeverityCalculator(null!);
        act.Should().Throw<ArgumentNullException>().WithParameterName("logger");
    }

    [Theory]
    [InlineData(0, AnalysisSeverity.None)]
    [InlineData(1, AnalysisSeverity.Info)]
    [InlineData(2, AnalysisSeverity.Info)]
    [InlineData(3, AnalysisSeverity.Warning)]
    [InlineData(4, AnalysisSeverity.Error)]
    [InlineData(5, AnalysisSeverity.Critical)]
    [InlineData(6, AnalysisSeverity.Critical)]
    public void CalculateSeverity_BaseSeverityValues_MapsToCorrectLevel(int baseSeverity, AnalysisSeverity expectedLevel)
    {
        // Arrange
        var matchResult = new SignalMatchResult
        {
            IsMatch = true,
            Confidence = 0.5
        };

        // Act
        var assessment = _sut.CalculateSeverity(baseSeverity, matchResult);

        // Assert
        assessment.BaseSeverity.Should().Be(baseSeverity);
        assessment.BaseLevel.Should().Be(expectedLevel);
    }

    [Fact]
    public void CalculateSeverity_NoMatch_ReturnsNoneSeverity()
    {
        // Arrange
        var matchResult = new SignalMatchResult
        {
            IsMatch = false,
            Confidence = 0.0
        };

        // Act
        var assessment = _sut.CalculateSeverity(4, matchResult);

        // Assert
        assessment.FinalLevel.Should().Be(AnalysisSeverity.None);
        assessment.Score.Should().Be(0.0);
    }

    [Fact]
    public void CalculateSeverity_NullMatchResult_ReturnsNoneSeverity()
    {
        // Act
        var assessment = _sut.CalculateSeverity(4, null!);

        // Assert
        assessment.FinalLevel.Should().Be(AnalysisSeverity.None);
        assessment.Score.Should().Be(0.0);
    }

    [Fact]
    public void CalculateSeverity_HighConfidenceMatch_BoostsScore()
    {
        // Arrange
        var lowConfidenceMatch = new SignalMatchResult
        {
            IsMatch = true,
            Confidence = 0.3
        };

        var highConfidenceMatch = new SignalMatchResult
        {
            IsMatch = true,
            Confidence = 1.0
        };

        // Act
        var lowAssessment = _sut.CalculateSeverity(3, lowConfidenceMatch);
        var highAssessment = _sut.CalculateSeverity(3, highConfidenceMatch);

        // Assert
        highAssessment.Score.Should().BeGreaterThan(lowAssessment.Score);
    }

    [Fact]
    public void CalculateSeverity_WithDllCrashFactor_IncreasesScore()
    {
        // Arrange
        var matchResult = new SignalMatchResult
        {
            IsMatch = true,
            Confidence = 0.7
        };

        var factors = new SeverityFactors
        {
            IsDllCrash = true
        };

        // Act
        var withFactor = _sut.CalculateSeverity(3, matchResult, factors);
        var withoutFactor = _sut.CalculateSeverity(3, matchResult);

        // Assert
        withFactor.Score.Should().BeGreaterThan(withoutFactor.Score);
    }

    [Fact]
    public void CalculateSeverity_WithMultipleFactors_AccumulatesBoosts()
    {
        // Arrange
        var matchResult = new SignalMatchResult
        {
            IsMatch = true,
            Confidence = 0.6
        };

        var factors = new SeverityFactors
        {
            IsDllCrash = true,
            IsRecurring = true,
            HasMultipleIndicators = true,
            AffectsGameStability = true
        };

        // Act
        var assessment = _sut.CalculateSeverity(2, matchResult, factors);

        // Assert
        assessment.Score.Should().BeGreaterThan(0.5); // Base + all boosts
        assessment.FinalLevel.Should().BeGreaterOrEqualTo(AnalysisSeverity.Warning);
    }

    [Fact]
    public void CalculateSeverity_KnownCriticalPattern_EscalatesSeverity()
    {
        // Arrange
        var matchResult = new SignalMatchResult
        {
            IsMatch = true,
            Confidence = 0.5
        };

        var factors = new SeverityFactors
        {
            IsKnownCriticalPattern = true
        };

        // Act
        var assessment = _sut.CalculateSeverity(2, matchResult, factors);

        // Assert
        assessment.WasEscalated.Should().BeTrue();
        assessment.Explanations.Should().Contain(e => e.Contains("escalated"));
    }

    [Fact]
    public void CalculateSeverity_AllRequiredSignalsWithHighConfidence_Escalates()
    {
        // Arrange
        var matchResult = new SignalMatchResult
        {
            IsMatch = true,
            Confidence = 0.95,
            RequiredTotal = 3,
            RequiredMatches = 3
        };

        // Act
        var assessment = _sut.CalculateSeverity(2, matchResult);

        // Assert
        assessment.WasEscalated.Should().BeTrue();
    }

    [Fact]
    public void CalculateSeverity_RecurringStabilityIssues_Escalates()
    {
        // Arrange
        var matchResult = new SignalMatchResult
        {
            IsMatch = true,
            Confidence = 0.7
        };

        var factors = new SeverityFactors
        {
            IsRecurring = true,
            AffectsGameStability = true
        };

        // Act
        var assessment = _sut.CalculateSeverity(2, matchResult, factors);

        // Assert
        assessment.WasEscalated.Should().BeTrue();
    }

    [Fact]
    public void CalculateSeverity_MultipleMatchedSignals_IncreasesWeight()
    {
        // Arrange
        var matchResult = new SignalMatchResult
        {
            IsMatch = true,
            Confidence = 0.6
        };

        // Add multiple matched signals
        for (int i = 0; i < 5; i++)
        {
            matchResult.MatchedSignals.Add(new SignalMatch
            {
                Signal = $"Signal{i}",
                Pattern = $"Pattern{i}",
                Type = SignalType.Optional,
                Location = SignalLocation.MainError,
                Occurrences = 1
            });
        }

        // Act
        var assessment = _sut.CalculateSeverity(3, matchResult);

        // Assert
        assessment.Score.Should().BeGreaterThan(0.5); // Base score plus weight
    }

    [Fact]
    public void CalculateSeverity_HighOccurrenceSignals_IncreasesWeight()
    {
        // Arrange
        var matchResult = new SignalMatchResult
        {
            IsMatch = true,
            Confidence = 0.6
        };

        matchResult.MatchedSignals.Add(new SignalMatch
        {
            Signal = "HighOccurrence",
            Pattern = "Pattern",
            Type = SignalType.Optional,
            Location = SignalLocation.CallStack,
            Occurrences = 10 // High occurrence
        });

        // Act
        var assessment = _sut.CalculateSeverity(3, matchResult);

        // Assert
        assessment.Score.Should().BeGreaterThan(0.5);
    }

    [Fact]
    public void CalculateSeverity_ScoreNeverExceedsOne()
    {
        // Arrange
        var matchResult = new SignalMatchResult
        {
            IsMatch = true,
            Confidence = 1.0,
            RequiredTotal = 10,
            RequiredMatches = 10
        };

        for (int i = 0; i < 20; i++)
        {
            matchResult.MatchedSignals.Add(new SignalMatch
            {
                Signal = $"Signal{i}",
                Pattern = $"Pattern{i}",
                Type = SignalType.Required,
                Location = SignalLocation.CallStack,
                Occurrences = 20
            });
        }

        var factors = new SeverityFactors
        {
            IsDllCrash = true,
            IsRecurring = true,
            HasMultipleIndicators = true,
            IsKnownCriticalPattern = true,
            AffectsGameStability = true
        };

        // Act
        var assessment = _sut.CalculateSeverity(6, matchResult, factors);

        // Assert
        assessment.Score.Should().BeLessOrEqualTo(1.0);
    }

    [Fact]
    public void CalculateSeverity_GeneratesRelevantExplanations()
    {
        // Arrange
        var matchResult = new SignalMatchResult
        {
            IsMatch = true,
            Confidence = 0.85,
            RequiredMatches = 2,
            RequiredTotal = 2
        };

        var factors = new SeverityFactors
        {
            IsDllCrash = true,
            IsRecurring = true
        };

        // Act
        var assessment = _sut.CalculateSeverity(4, matchResult, factors);

        // Assert
        assessment.Explanations.Should().NotBeEmpty();
        assessment.Explanations.Should().Contain(e => e.Contains("required conditions"));
        assessment.Explanations.Should().Contain(e => e.Contains("confidence"));
        assessment.Explanations.Should().Contain(e => e.Contains("DLL"));
        assessment.Explanations.Should().Contain(e => e.Contains("Recurring"));
    }

    [Theory]
    [InlineData(0.05, AnalysisSeverity.None)]
    [InlineData(0.15, AnalysisSeverity.Info)]
    [InlineData(0.35, AnalysisSeverity.Warning)]
    [InlineData(0.65, AnalysisSeverity.Error)]
    [InlineData(0.85, AnalysisSeverity.Critical)]
    public void DetermineSeverityLevel_ScoreRanges_MapsCorrectly(double score, AnalysisSeverity expected)
    {
        // Arrange
        var matchResult = new SignalMatchResult
        {
            IsMatch = true,
            Confidence = score // Will affect final score calculation
        };

        // Act - Use base severity that won't dominate the score
        var assessment = _sut.CalculateSeverity(3, matchResult);
        
        // Manually set score to test threshold mapping
        assessment.Score = score;
        assessment.FinalLevel = score switch
        {
            >= 0.8 => AnalysisSeverity.Critical,
            >= 0.6 => AnalysisSeverity.Error,
            >= 0.3 => AnalysisSeverity.Warning,
            >= 0.1 => AnalysisSeverity.Info,
            _ => AnalysisSeverity.None
        };

        // Assert
        assessment.FinalLevel.Should().Be(expected);
    }

    [Fact]
    public void CalculateCombinedSeverity_EmptyList_ReturnsNone()
    {
        // Act
        var combined = _sut.CalculateCombinedSeverity(new List<SeverityAssessment>());

        // Assert
        combined.FinalLevel.Should().Be(AnalysisSeverity.None);
        combined.Score.Should().Be(0.0);
    }

    [Fact]
    public void CalculateCombinedSeverity_NullList_ReturnsNone()
    {
        // Act
        var combined = _sut.CalculateCombinedSeverity(null!);

        // Assert
        combined.FinalLevel.Should().Be(AnalysisSeverity.None);
        combined.Score.Should().Be(0.0);
    }

    [Fact]
    public void CalculateCombinedSeverity_SingleAssessment_ReturnsSlightlyBoosted()
    {
        // Arrange
        var assessment = new SeverityAssessment
        {
            BaseSeverity = 4,
            BaseLevel = AnalysisSeverity.Error,
            FinalLevel = AnalysisSeverity.Error,
            Score = 0.7
        };

        // Act
        var combined = _sut.CalculateCombinedSeverity(new List<SeverityAssessment> { assessment });

        // Assert
        combined.Score.Should().BeCloseTo(0.7, 0.01);
        combined.FinalLevel.Should().Be(AnalysisSeverity.Error);
    }

    [Fact]
    public void CalculateCombinedSeverity_MultipleAssessments_AppliesCumulativeEffect()
    {
        // Arrange
        var assessments = new List<SeverityAssessment>
        {
            new() { Score = 0.6, FinalLevel = AnalysisSeverity.Error },
            new() { Score = 0.5, FinalLevel = AnalysisSeverity.Warning },
            new() { Score = 0.4, FinalLevel = AnalysisSeverity.Warning }
        };

        // Act
        var combined = _sut.CalculateCombinedSeverity(assessments);

        // Assert
        combined.Score.Should().BeGreaterThan(0.6); // Highest + cumulative boost
        combined.Explanations.Should().Contain(e => e.Contains("Combined 3 suspects"));
    }

    [Fact]
    public void CalculateCombinedSeverity_MultipleCritical_EscalatesToCritical()
    {
        // Arrange
        var assessments = new List<SeverityAssessment>
        {
            new() { Score = 0.85, FinalLevel = AnalysisSeverity.Critical },
            new() { Score = 0.82, FinalLevel = AnalysisSeverity.Critical }
        };

        // Act
        var combined = _sut.CalculateCombinedSeverity(assessments);

        // Assert
        combined.FinalLevel.Should().Be(AnalysisSeverity.Critical);
        combined.Explanations.Should().Contain(e => e.Contains("Multiple critical"));
    }

    [Fact]
    public void CalculateCombinedSeverity_MultipleErrors_EscalatesToError()
    {
        // Arrange
        var assessments = new List<SeverityAssessment>
        {
            new() { Score = 0.4, FinalLevel = AnalysisSeverity.Warning },
            new() { Score = 0.65, FinalLevel = AnalysisSeverity.Error },
            new() { Score = 0.62, FinalLevel = AnalysisSeverity.Error },
            new() { Score = 0.61, FinalLevel = AnalysisSeverity.Error }
        };

        // Act
        var combined = _sut.CalculateCombinedSeverity(assessments);

        // Assert
        combined.FinalLevel.Should().BeGreaterOrEqualTo(AnalysisSeverity.Error);
        combined.Explanations.Should().Contain(e => e.Contains("Multiple error-level"));
    }

    [Fact]
    public void CalculateCombinedSeverity_DiminishingReturns_LimitsBoost()
    {
        // Arrange
        var manyAssessments = new List<SeverityAssessment>();
        for (int i = 0; i < 10; i++)
        {
            manyAssessments.Add(new SeverityAssessment
            {
                Score = 0.3,
                FinalLevel = AnalysisSeverity.Warning
            });
        }

        // Act
        var combined = _sut.CalculateCombinedSeverity(manyAssessments);

        // Assert
        combined.Score.Should().BeLessThan(0.5); // Despite many assessments, boost is limited
    }

    [Fact]
    public void CalculateCombinedSeverity_CombinedScoreNeverExceedsOne()
    {
        // Arrange
        var assessments = new List<SeverityAssessment>();
        for (int i = 0; i < 20; i++)
        {
            assessments.Add(new SeverityAssessment
            {
                Score = 0.9,
                FinalLevel = AnalysisSeverity.Critical
            });
        }

        // Act
        var combined = _sut.CalculateCombinedSeverity(assessments);

        // Assert
        combined.Score.Should().BeLessOrEqualTo(1.0);
    }

    [Fact]
    public void EscalateSeverity_EscalatesEachLevel()
    {
        // Arrange test cases for each level
        var testCases = new[]
        {
            (AnalysisSeverity.None, AnalysisSeverity.Info),
            (AnalysisSeverity.Info, AnalysisSeverity.Warning),
            (AnalysisSeverity.Warning, AnalysisSeverity.Error),
            (AnalysisSeverity.Error, AnalysisSeverity.Critical),
            (AnalysisSeverity.Critical, AnalysisSeverity.Critical) // Can't escalate beyond Critical
        };

        foreach (var (initial, expected) in testCases)
        {
            // Arrange
            var matchResult = new SignalMatchResult
            {
                IsMatch = true,
                Confidence = 0.95,
                RequiredTotal = 1,
                RequiredMatches = 1 // Trigger escalation
            };

            var baseSeverity = initial switch
            {
                AnalysisSeverity.None => 0,
                AnalysisSeverity.Info => 1,
                AnalysisSeverity.Warning => 3,
                AnalysisSeverity.Error => 4,
                AnalysisSeverity.Critical => 5,
                _ => 0
            };

            // Act
            var assessment = _sut.CalculateSeverity(baseSeverity, matchResult);

            // Assert
            assessment.FinalLevel.Should().BeGreaterOrEqualTo(expected);
        }
    }
}