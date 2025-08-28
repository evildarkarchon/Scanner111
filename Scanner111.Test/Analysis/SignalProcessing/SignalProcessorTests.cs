using System;
using System.Collections.Generic;
using System.Linq;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using NSubstitute;
using Scanner111.Core.Analysis.SignalProcessing;
using Xunit;

namespace Scanner111.Test.Analysis.SignalProcessing;

/// <summary>
/// Comprehensive tests for SignalProcessor covering all signal types (ME-REQ, ME-OPT, NOT, stack),
/// occurrence thresholds, confidence calculations, and priority ordering.
/// </summary>
public sealed class SignalProcessorTests
{
    private readonly ILogger<SignalProcessor> _logger;
    private readonly SignalProcessor _processor;

    public SignalProcessorTests()
    {
        _logger = Substitute.For<ILogger<SignalProcessor>>();
        _processor = new SignalProcessor(_logger);
    }

    #region Constructor Tests

    [Fact]
    public void Constructor_WithNullLogger_ThrowsArgumentNullException()
    {
        // Arrange & Act
        var act = () => new SignalProcessor(null!);

        // Assert
        act.Should().Throw<ArgumentNullException>()
            .WithParameterName("logger");
    }

    [Fact]
    public void Constructor_WithValidLogger_CreatesInstance()
    {
        // Arrange & Act
        var processor = new SignalProcessor(_logger);

        // Assert
        processor.Should().NotBeNull();
    }

    #endregion

    #region Basic Signal Processing Tests

    [Fact]
    public void ProcessSignals_WithNullSignals_ReturnsNoMatch()
    {
        // Arrange
        string mainError = "Test error";
        string callStack = "Test stack";

        // Act
        var result = _processor.ProcessSignals(null!, mainError, callStack);

        // Assert
        result.IsMatch.Should().BeFalse();
        result.Confidence.Should().Be(0.0);
    }

    [Fact]
    public void ProcessSignals_WithEmptySignals_ReturnsNoMatch()
    {
        // Arrange
        var signals = new List<string>();
        string mainError = "Test error";
        string callStack = "Test stack";

        // Act
        var result = _processor.ProcessSignals(signals, mainError, callStack);

        // Assert
        result.IsMatch.Should().BeFalse();
    }

    [Fact]
    public void ProcessSignals_WithNullInputs_HandlesGracefully()
    {
        // Arrange
        var signals = new List<string> { "ME-REQ|TestPattern" };

        // Act
        var result = _processor.ProcessSignals(signals, null!, null!);

        // Assert
        result.IsMatch.Should().BeFalse();
        result.SkipReason.Should().Be("Required signals not met");
    }

    #endregion

    #region Required Signal (ME-REQ) Tests

    [Fact]
    public void ProcessSignals_WithMatchingRequiredSignal_ReturnsMatch()
    {
        // Arrange
        var signals = new List<string> { "ME-REQ|ACCESS_VIOLATION" };
        string mainError = "Exception: ACCESS_VIOLATION at 0x12345678";
        string callStack = "Stack trace here";

        // Act
        var result = _processor.ProcessSignals(signals, mainError, callStack);

        // Assert
        result.IsMatch.Should().BeTrue();
        result.RequiredMatches.Should().Be(1);
        result.RequiredTotal.Should().Be(1);
        result.MatchedSignals.Should().HaveCount(1);
        result.MatchedSignals[0].Type.Should().Be(SignalType.Required);
        result.MatchedSignals[0].Location.Should().Be(SignalLocation.MainError);
    }

    [Fact]
    public void ProcessSignals_WithNonMatchingRequiredSignal_ReturnsNoMatch()
    {
        // Arrange
        var signals = new List<string> { "ME-REQ|MISSING_PATTERN" };
        string mainError = "Exception: ACCESS_VIOLATION";
        string callStack = "Stack trace";

        // Act
        var result = _processor.ProcessSignals(signals, mainError, callStack);

        // Assert
        result.IsMatch.Should().BeFalse();
        result.RequiredMatches.Should().Be(0);
        result.SkipReason.Should().Be("Required signals not met");
    }

    [Fact]
    public void ProcessSignals_WithMultipleRequiredSignals_AllMustMatch()
    {
        // Arrange
        var signals = new List<string>
        {
            "ME-REQ|ACCESS_VIOLATION",
            "ME-REQ|0x12345678"
        };
        string mainError = "Exception: ACCESS_VIOLATION at 0x12345678";
        string callStack = "Stack trace";

        // Act
        var result = _processor.ProcessSignals(signals, mainError, callStack);

        // Assert
        result.IsMatch.Should().BeTrue();
        result.RequiredMatches.Should().Be(2);
        result.RequiredTotal.Should().Be(2);
    }

    [Fact]
    public void ProcessSignals_WithPartialRequiredMatches_ReturnsNoMatch()
    {
        // Arrange
        var signals = new List<string>
        {
            "ME-REQ|ACCESS_VIOLATION",
            "ME-REQ|MISSING"
        };
        string mainError = "Exception: ACCESS_VIOLATION at 0x12345678";
        string callStack = "Stack trace";

        // Act
        var result = _processor.ProcessSignals(signals, mainError, callStack);

        // Assert
        result.IsMatch.Should().BeFalse();
        result.RequiredMatches.Should().Be(1);
        result.RequiredTotal.Should().Be(2);
        result.SkipReason.Should().Be("Required signals not met");
    }

    #endregion

    #region Optional Signal (ME-OPT) Tests

    [Fact]
    public void ProcessSignals_WithMatchingOptionalSignal_ReturnsMatch()
    {
        // Arrange
        var signals = new List<string> { "ME-OPT|WARNING" };
        string mainError = "WARNING: Memory corruption detected";
        string callStack = "Stack trace";

        // Act
        var result = _processor.ProcessSignals(signals, mainError, callStack);

        // Assert
        result.IsMatch.Should().BeTrue();
        result.OptionalMatches.Should().Be(1);
        result.OptionalTotal.Should().Be(1);
    }

    [Fact]
    public void ProcessSignals_WithMultipleOptionalSignals_AnyMatchCounts()
    {
        // Arrange
        var signals = new List<string>
        {
            "ME-OPT|WARNING",
            "ME-OPT|ERROR",
            "ME-OPT|CRITICAL"
        };
        string mainError = "ERROR: Something went wrong";
        string callStack = "Stack trace";

        // Act
        var result = _processor.ProcessSignals(signals, mainError, callStack);

        // Assert
        result.IsMatch.Should().BeTrue();
        result.OptionalMatches.Should().Be(1);
        result.OptionalTotal.Should().Be(3);
    }

    [Fact]
    public void ProcessSignals_WithNoOptionalMatches_ReturnsNoMatch()
    {
        // Arrange
        var signals = new List<string> { "ME-OPT|NOTFOUND" };
        string mainError = "Simple error message";
        string callStack = "Stack trace";

        // Act
        var result = _processor.ProcessSignals(signals, mainError, callStack);

        // Assert
        result.IsMatch.Should().BeFalse();
        result.OptionalMatches.Should().Be(0);
    }

    #endregion

    #region Negative Signal (NOT) Tests

    [Fact]
    public void ProcessSignals_WithMatchingNegativeSignal_ReturnsNoMatch()
    {
        // Arrange
        var signals = new List<string>
        {
            "ME-REQ|ACCESS_VIOLATION",
            "NOT|HANDLED"
        };
        string mainError = "ACCESS_VIOLATION - HANDLED by exception handler";
        string callStack = "Stack trace";

        // Act
        var result = _processor.ProcessSignals(signals, mainError, callStack);

        // Assert
        result.IsMatch.Should().BeFalse();
        result.SkipReason.Should().Be("Negative condition met");
    }

    [Fact]
    public void ProcessSignals_WithNegativeSignalInCallStack_ReturnsNoMatch()
    {
        // Arrange
        var signals = new List<string>
        {
            "ME-REQ|ERROR",
            "NOT|SafeMode"
        };
        string mainError = "ERROR occurred";
        string callStack = "Running in SafeMode";

        // Act
        var result = _processor.ProcessSignals(signals, mainError, callStack);

        // Assert
        result.IsMatch.Should().BeFalse();
        result.SkipReason.Should().Be("Negative condition met");
    }

    [Fact]
    public void ProcessSignals_WithNonMatchingNegativeSignal_ContinuesProcessing()
    {
        // Arrange
        var signals = new List<string>
        {
            "ME-REQ|ERROR",
            "NOT|HANDLED"
        };
        string mainError = "ERROR occurred";
        string callStack = "Stack trace";

        // Act
        var result = _processor.ProcessSignals(signals, mainError, callStack);

        // Assert
        result.IsMatch.Should().BeTrue();
        result.RequiredMatches.Should().Be(1);
    }

    #endregion

    #region Stack Signal Tests with Occurrence Thresholds

    [Fact]
    public void ProcessSignals_WithSimpleStackSignal_MatchesInCallStack()
    {
        // Arrange
        var signals = new List<string> { "ModFunction" };
        string mainError = "Error occurred";
        string callStack = "at ModFunction() line 42";

        // Act
        var result = _processor.ProcessSignals(signals, mainError, callStack);

        // Assert
        result.IsMatch.Should().BeTrue();
        result.StackMatches.Should().Be(1);
        result.MatchedSignals[0].Location.Should().Be(SignalLocation.CallStack);
    }

    [Fact]
    public void ProcessSignals_WithMinOccurrenceThreshold_RequiresMinimumMatches()
    {
        // Arrange
        var signals = new List<string> { "3|RecursiveCall" };
        string mainError = "Stack overflow";
        string callStack = @"
            at RecursiveCall() line 1
            at RecursiveCall() line 1
            at RecursiveCall() line 1
            at Main()";

        // Act
        var result = _processor.ProcessSignals(signals, mainError, callStack);

        // Assert
        result.IsMatch.Should().BeTrue();
        result.StackMatches.Should().Be(1);
        result.MatchedSignals[0].Occurrences.Should().Be(3);
        result.MatchedSignals[0].MinOccurrences.Should().Be(3);
    }

    [Fact]
    public void ProcessSignals_WithBelowMinOccurrence_ReturnsNoMatch()
    {
        // Arrange
        var signals = new List<string> { "5|RecursiveCall" };
        string mainError = "Stack overflow";
        string callStack = @"
            at RecursiveCall() line 1
            at RecursiveCall() line 1
            at Main()";

        // Act
        var result = _processor.ProcessSignals(signals, mainError, callStack);

        // Assert
        result.IsMatch.Should().BeFalse();
        result.StackMatches.Should().Be(0);
    }

    [Fact]
    public void ProcessSignals_WithRangeThreshold_RequiresWithinRange()
    {
        // Arrange
        var signals = new List<string> { "2-4|Pattern" };
        string mainError = "Error";
        string callStack = @"
            at Pattern() line 1
            at Pattern() line 2
            at Pattern() line 3
            at Main()";

        // Act
        var result = _processor.ProcessSignals(signals, mainError, callStack);

        // Assert
        result.IsMatch.Should().BeTrue();
        result.StackMatches.Should().Be(1);
        result.MatchedSignals[0].Occurrences.Should().Be(3);
        result.MatchedSignals[0].MinOccurrences.Should().Be(2);
        result.MatchedSignals[0].MaxOccurrences.Should().Be(4);
    }

    [Fact]
    public void ProcessSignals_WithAboveMaxOccurrence_ReturnsNoMatch()
    {
        // Arrange
        var signals = new List<string> { "1-2|Pattern" };
        string mainError = "Error";
        string callStack = @"
            at Pattern() line 1
            at Pattern() line 2
            at Pattern() line 3
            at Pattern() line 4
            at Main()";

        // Act
        var result = _processor.ProcessSignals(signals, mainError, callStack);

        // Assert
        result.IsMatch.Should().BeFalse();
        result.StackMatches.Should().Be(0);
    }

    #endregion

    #region Signal Priority and Ordering Tests

    [Theory]
    [InlineData("NOT|SKIP", "ME-REQ|ERROR", "ME-OPT|WARNING")]
    [InlineData("NOT|EXCLUDE", "3|Stack", "ME-REQ|REQUIRED")]
    public void ProcessSignals_WithNegativeSignalFirst_ShortCircuits(string notSignal, string signal2, string signal3)
    {
        // Arrange
        var signals = new List<string> { signal2, notSignal, signal3 }; // Order in list shouldn't matter
        string mainError = "ERROR REQUIRED WARNING SKIP EXCLUDE";
        string callStack = "Stack Stack Stack";

        // Act
        var result = _processor.ProcessSignals(signals, mainError, callStack);

        // Assert
        result.IsMatch.Should().BeFalse();
        result.SkipReason.Should().Be("Negative condition met");
        // Should not process other signals after negative match
        result.RequiredMatches.Should().Be(0);
        result.OptionalMatches.Should().Be(0);
    }

    #endregion

    #region Confidence Calculation Tests

    [Fact]
    public void ProcessSignals_WithAllRequiredMatches_HasHighConfidence()
    {
        // Arrange
        var signals = new List<string>
        {
            "ME-REQ|ERROR",
            "ME-REQ|CRITICAL"
        };
        string mainError = "ERROR: CRITICAL failure";
        string callStack = "Stack";

        // Act
        var result = _processor.ProcessSignals(signals, mainError, callStack);

        // Assert
        result.IsMatch.Should().BeTrue();
        result.Confidence.Should().BeApproximately(1.0, 0.01); // 100% of required signals matched
    }

    [Fact]
    public void ProcessSignals_WithMixedSignalTypes_CalculatesWeightedConfidence()
    {
        // Arrange
        var signals = new List<string>
        {
            "ME-REQ|ERROR",      // Weight: 0.5
            "ME-OPT|WARNING",    // Weight: 0.3
            "StackTrace"         // Weight: 0.2
        };
        string mainError = "ERROR with WARNING";
        string callStack = "at StackTrace()";

        // Act
        var result = _processor.ProcessSignals(signals, mainError, callStack);

        // Assert
        result.IsMatch.Should().BeTrue();
        result.Confidence.Should().BeApproximately(1.0, 0.01); // All signals matched
    }

    [Fact]
    public void ProcessSignals_WithPartialOptionalMatches_HasLowerConfidence()
    {
        // Arrange
        var signals = new List<string>
        {
            "ME-OPT|WARNING",
            "ME-OPT|ERROR",
            "ME-OPT|CRITICAL",
            "ME-OPT|FATAL"
        };
        string mainError = "WARNING occurred";
        string callStack = "Stack";

        // Act
        var result = _processor.ProcessSignals(signals, mainError, callStack);

        // Assert
        result.IsMatch.Should().BeTrue();
        result.Confidence.Should().BeLessThan(1.0); // Only 1 of 4 optional signals matched
        result.Confidence.Should().BeApproximately(0.25, 0.01); // 1/4 = 0.25
    }

    #endregion

    #region Case Sensitivity Tests

    [Fact]
    public void ProcessSignals_WithDifferentCase_MatchesIgnoringCase()
    {
        // Arrange
        var signals = new List<string> { "ME-REQ|access_violation" };
        string mainError = "Exception: ACCESS_VIOLATION at memory";
        string callStack = "Stack";

        // Act
        var result = _processor.ProcessSignals(signals, mainError, callStack);

        // Assert
        result.IsMatch.Should().BeTrue();
        result.RequiredMatches.Should().Be(1);
    }

    #endregion

    #region Occurrence Counting Tests

    [Fact]
    public void ProcessSignals_WithMultipleOccurrences_CountsCorrectly()
    {
        // Arrange
        var signals = new List<string> { "Pattern" };
        string mainError = "Error";
        string callStack = "Pattern at line 1, Pattern at line 2, Pattern at line 3";

        // Act
        var result = _processor.ProcessSignals(signals, mainError, callStack);

        // Assert
        result.IsMatch.Should().BeTrue();
        result.MatchedSignals[0].Occurrences.Should().Be(3);
    }

    [Fact]
    public void ProcessSignals_WithOverlappingPattern_CountsNonOverlapping()
    {
        // Arrange
        var signals = new List<string> { "AAA" };
        string mainError = "Error";
        string callStack = "AAAAAAA"; // Should count as 2 non-overlapping occurrences

        // Act
        var result = _processor.ProcessSignals(signals, mainError, callStack);

        // Assert
        result.IsMatch.Should().BeTrue();
        result.MatchedSignals[0].Occurrences.Should().Be(2); // Non-overlapping count
    }

    #endregion

    #region Complex Scenario Tests

    [Fact]
    public void ProcessSignals_WithComplexRealWorldScenario_ProcessesCorrectly()
    {
        // Arrange
        var signals = new List<string>
        {
            "ME-REQ|ACCESS_VIOLATION",
            "ME-OPT|0xC0000005",
            "NOT|Handled",
            "3|MyMod.dll",
            "1-2|Fallout4.exe"
        };
        string mainError = "EXCEPTION: ACCESS_VIOLATION (0xC0000005) at 0x7FFF1234";
        string callStack = @"
            at MyMod.dll+0x1234
            at MyMod.dll+0x5678
            at MyMod.dll+0x9ABC
            at Fallout4.exe+0xDEF0
            at kernel32.dll";

        // Act
        var result = _processor.ProcessSignals(signals, mainError, callStack);

        // Assert
        result.IsMatch.Should().BeTrue();
        result.RequiredMatches.Should().Be(1);
        result.OptionalMatches.Should().Be(1);
        result.StackMatches.Should().Be(2); // Both stack patterns match
        result.Confidence.Should().BeGreaterThan(0.8);
    }

    [Fact]
    public void ProcessSignals_WithEmptyPatternAfterPrefix_HandlesGracefully()
    {
        // Arrange
        var signals = new List<string>
        {
            "ME-REQ|", // Empty pattern
            "ME-OPT|ValidPattern"
        };
        string mainError = "ValidPattern found";
        string callStack = "Stack";

        // Act
        var result = _processor.ProcessSignals(signals, mainError, callStack);

        // Assert
        result.IsMatch.Should().BeTrue(); // Should match based on optional signal
        result.OptionalMatches.Should().Be(1);
    }

    #endregion

    #region Logging Tests

    [Fact]
    public void ProcessSignals_WithDebugLogging_LogsProcessingSteps()
    {
        // Arrange
        var signals = new List<string> { "ME-REQ|ERROR" };
        string mainError = "ERROR occurred";
        string callStack = "Stack";

        // Act
        var result = _processor.ProcessSignals(signals, mainError, callStack);

        // Assert
        _logger.Received().Log(
            LogLevel.Debug,
            Arg.Any<EventId>(),
            Arg.Is<object>(o => o.ToString()!.Contains("Signal processing complete")),
            null,
            Arg.Any<Func<object, Exception?, string>>());
    }

    [Fact]
    public void ProcessSignals_WithNegativeMatch_LogsSkipReason()
    {
        // Arrange
        var signals = new List<string> { "NOT|SKIP" };
        string mainError = "Error with SKIP";
        string callStack = "Stack";

        // Act
        var result = _processor.ProcessSignals(signals, mainError, callStack);

        // Assert
        _logger.Received().Log(
            LogLevel.Debug,
            Arg.Any<EventId>(),
            Arg.Is<object>(o => o.ToString()!.Contains("Negative signal matched")),
            null,
            Arg.Any<Func<object, Exception?, string>>());
    }

    #endregion
}