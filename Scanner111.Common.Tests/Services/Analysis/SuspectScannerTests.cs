using FluentAssertions;
using Scanner111.Common.Models.Analysis;
using Scanner111.Common.Services.Analysis;

namespace Scanner111.Common.Tests.Services.Analysis;

/// <summary>
/// Tests for SuspectScanner.
/// </summary>
public class SuspectScannerTests
{
    private readonly SuspectScanner _scanner;

    public SuspectScannerTests()
    {
        _scanner = new SuspectScanner();
    }

    [Fact]
    public async Task ScanAsync_WithMatchingErrorPattern_DetectsIt()
    {
        // Arrange
        var header = new CrashHeader
        {
            MainError = "EXCEPTION_ACCESS_VIOLATION"
        };
        var segments = Array.Empty<LogSegment>();
        var patterns = new SuspectPatterns
        {
            ErrorPatterns = new[]
            {
                new SuspectPattern
                {
                    Pattern = "ACCESS_VIOLATION",
                    Message = "Memory access violation detected",
                    Recommendations = new[] { "Check for conflicting mods" }
                }
            }
        };

        // Act
        var result = await _scanner.ScanAsync(header, segments, patterns);

        // Assert
        result.ErrorMatches.Should().Contain("Memory access violation detected");
        result.Recommendations.Should().Contain("Check for conflicting mods");
    }

    [Fact]
    public async Task ScanAsync_WithMatchingStackPattern_DetectsIt()
    {
        // Arrange
        var header = new CrashHeader { MainError = "" };
        var segments = new[]
        {
            new LogSegment
            {
                Name = "PROBABLE CALL STACK",
                Lines = new[]
                {
                    "ntdll.dll",
                    "KERNELBASE.dll",
                    "Fallout4.exe+0x1234567"
                }
            }
        };
        var patterns = new SuspectPatterns
        {
            StackSignatures = new[]
            {
                new SuspectPattern
                {
                    Pattern = "KERNELBASE",
                    Message = "System-level crash detected",
                    Recommendations = new[] { "Update Windows" }
                }
            }
        };

        // Act
        var result = await _scanner.ScanAsync(header, segments, patterns);

        // Assert
        result.StackMatches.Should().Contain("System-level crash detected");
        result.Recommendations.Should().Contain("Update Windows");
    }

    [Fact]
    public async Task ScanAsync_WithNoMatches_ReturnsEmpty()
    {
        // Arrange
        var header = new CrashHeader { MainError = "UNKNOWN_ERROR" };
        var segments = Array.Empty<LogSegment>();
        var patterns = new SuspectPatterns
        {
            ErrorPatterns = new[]
            {
                new SuspectPattern { Pattern = "DIFFERENT_ERROR" }
            }
        };

        // Act
        var result = await _scanner.ScanAsync(header, segments, patterns);

        // Assert
        result.ErrorMatches.Should().BeEmpty();
        result.StackMatches.Should().BeEmpty();
    }

    [Fact]
    public async Task ScanAsync_WithMultipleMatches_ReturnAll()
    {
        // Arrange
        var header = new CrashHeader { MainError = "ACCESS_VIOLATION at 0x0" };
        var segments = Array.Empty<LogSegment>();
        var patterns = new SuspectPatterns
        {
            ErrorPatterns = new[]
            {
                new SuspectPattern { Pattern = "ACCESS_VIOLATION", Message = "Match 1" },
                new SuspectPattern { Pattern = "at 0x0", Message = "Match 2" }
            }
        };

        // Act
        var result = await _scanner.ScanAsync(header, segments, patterns);

        // Assert
        result.ErrorMatches.Should().HaveCount(2);
    }

    [Fact]
    public async Task ScanAsync_WithEmptyHeader_SkipsErrorScan()
    {
        // Arrange
        var header = new CrashHeader { MainError = "" };
        var segments = Array.Empty<LogSegment>();
        var patterns = new SuspectPatterns
        {
            ErrorPatterns = new[]
            {
                new SuspectPattern { Pattern = ".*", Message = "Should not match" }
            }
        };

        // Act
        var result = await _scanner.ScanAsync(header, segments, patterns);

        // Assert
        result.ErrorMatches.Should().BeEmpty();
    }

    [Fact]
    public async Task ScanAsync_WithNoCallStackSegment_SkipsStackScan()
    {
        // Arrange
        var header = new CrashHeader { MainError = "ERROR" };
        var segments = new[]
        {
            new LogSegment { Name = "SYSTEM SPECS" }
        };
        var patterns = new SuspectPatterns
        {
            StackSignatures = new[]
            {
                new SuspectPattern { Pattern = ".*", Message = "Should not match" }
            }
        };

        // Act
        var result = await _scanner.ScanAsync(header, segments, patterns);

        // Assert
        result.StackMatches.Should().BeEmpty();
    }

    [Fact]
    public async Task ScanAsync_WithDuplicateRecommendations_ReturnsUnique()
    {
        // Arrange
        var header = new CrashHeader { MainError = "ERROR" };
        var segments = Array.Empty<LogSegment>();
        var patterns = new SuspectPatterns
        {
            ErrorPatterns = new[]
            {
                new SuspectPattern
                {
                    Pattern = "ERROR",
                    Message = "Match 1",
                    Recommendations = new[] { "Same recommendation" }
                },
                new SuspectPattern
                {
                    Pattern = "ERROR",
                    Message = "Match 2",
                    Recommendations = new[] { "Same recommendation" }
                }
            }
        };

        // Act
        var result = await _scanner.ScanAsync(header, segments, patterns);

        // Assert
        result.Recommendations.Should().HaveCount(1);
        result.Recommendations.Should().Contain("Same recommendation");
    }

    [Fact]
    public async Task ScanAsync_IsCaseInsensitive()
    {
        // Arrange
        var header = new CrashHeader { MainError = "ACCESS_VIOLATION" };
        var segments = Array.Empty<LogSegment>();
        var patterns = new SuspectPatterns
        {
            ErrorPatterns = new[]
            {
                new SuspectPattern
                {
                    Pattern = "access_violation",
                    Message = "Should match despite case difference"
                }
            }
        };

        // Act
        var result = await _scanner.ScanAsync(header, segments, patterns);

        // Assert
        result.ErrorMatches.Should().Contain("Should match despite case difference");
    }
}
