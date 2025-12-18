using FluentAssertions;
using Scanner111.Common.Models.Analysis;
using Scanner111.Common.Services.Analysis;

namespace Scanner111.Common.Tests.Services.Analysis;

/// <summary>
/// Tests for RecordScanner.
/// </summary>
public class RecordScannerTests
{
    private readonly RecordScanner _scanner;

    public RecordScannerTests()
    {
        _scanner = new RecordScanner();
    }

    [Fact]
    public async Task ScanAsync_WithNullSegment_ReturnsEmpty()
    {
        // Arrange
        _scanner.Configuration = new RecordScannerConfiguration
        {
            TargetRecords = new[] { "WEAP", "ARMO", "NPC_" }
        };

        // Act
        var result = await _scanner.ScanAsync(null);

        // Assert
        result.Should().Be(RecordScanResult.Empty);
        result.HasRecords.Should().BeFalse();
    }

    [Fact]
    public async Task ScanAsync_WithEmptySegment_ReturnsEmpty()
    {
        // Arrange
        _scanner.Configuration = new RecordScannerConfiguration
        {
            TargetRecords = new[] { "WEAP", "ARMO" }
        };

        var segment = new LogSegment
        {
            Name = "STACK",
            Lines = Array.Empty<string>()
        };

        // Act
        var result = await _scanner.ScanAsync(segment);

        // Assert
        result.HasRecords.Should().BeFalse();
    }

    [Fact]
    public async Task ScanAsync_WithNoTargetRecords_ReturnsEmpty()
    {
        // Arrange
        _scanner.Configuration = RecordScannerConfiguration.Empty;

        var segment = new LogSegment
        {
            Name = "STACK",
            Lines = new[]
            {
                "[RSP+0  ] 0x12345 (WEAP) SomeWeapon"
            }
        };

        // Act
        var result = await _scanner.ScanAsync(segment);

        // Assert
        result.HasRecords.Should().BeFalse();
    }

    [Fact]
    public async Task ScanAsync_WithMatchingRecords_ReturnsMatches()
    {
        // Arrange
        _scanner.Configuration = new RecordScannerConfiguration
        {
            TargetRecords = new[] { "WEAP", "ARMO" }
        };

        var segment = new LogSegment
        {
            Name = "STACK",
            Lines = new[]
            {
                "[RSP+0  ] 0x12345678901234567890 (WEAP) SomeWeapon",
                "[RSP+8  ] 0x12345678901234567890 Nothing here",
                "[RSP+10 ] 0x12345678901234567890 (ARMO) SomeArmor"
            }
        };

        // Act
        var result = await _scanner.ScanAsync(segment);

        // Assert
        result.HasRecords.Should().BeTrue();
        result.TotalMatches.Should().Be(2);
        result.UniqueRecordCount.Should().Be(2);
    }

    [Fact]
    public async Task ScanAsync_WithDuplicateRecords_CountsCorrectly()
    {
        // Arrange
        _scanner.Configuration = new RecordScannerConfiguration
        {
            TargetRecords = new[] { "WEAP" }
        };

        // Use simple lines without RSP format for testing duplicate counting
        var segment = new LogSegment
        {
            Name = "STACK",
            Lines = new[]
            {
                "(WEAP) Weapon1",
                "(WEAP) Weapon1",
                "(WEAP) Weapon2"
            }
        };

        // Act
        var result = await _scanner.ScanAsync(segment);

        // Assert
        result.TotalMatches.Should().Be(3);
        result.UniqueRecordCount.Should().Be(2);
        result.RecordCounts["(WEAP) Weapon1"].Should().Be(2);
        result.RecordCounts["(WEAP) Weapon2"].Should().Be(1);
    }

    [Fact]
    public async Task ScanAsync_WithIgnoredRecords_FiltersThemOut()
    {
        // Arrange
        _scanner.Configuration = new RecordScannerConfiguration
        {
            TargetRecords = new[] { "WEAP", "ARMO" },
            IgnoreRecords = new[] { "IgnoreThis" }
        };

        var segment = new LogSegment
        {
            Name = "STACK",
            Lines = new[]
            {
                "(WEAP) GoodWeapon",
                "(WEAP) IgnoreThis",
                "(ARMO) GoodArmor"
            }
        };

        // Act
        var result = await _scanner.ScanAsync(segment);

        // Assert
        result.TotalMatches.Should().Be(2);
        result.MatchedRecords.Should().NotContain(r => r.Contains("IgnoreThis"));
    }

    [Fact]
    public async Task ScanAsync_IsCaseInsensitive()
    {
        // Arrange
        _scanner.Configuration = new RecordScannerConfiguration
        {
            TargetRecords = new[] { "weap" }
        };

        var segment = new LogSegment
        {
            Name = "STACK",
            Lines = new[]
            {
                "(WEAP) Weapon1",
                "(Weap) Weapon2"
            }
        };

        // Act
        var result = await _scanner.ScanAsync(segment);

        // Assert
        result.TotalMatches.Should().Be(2);
    }

    [Fact]
    public async Task ScanAsync_WithLinesWithoutRspMarker_StillMatches()
    {
        // Arrange
        _scanner.Configuration = new RecordScannerConfiguration
        {
            TargetRecords = new[] { "WEAP" }
        };

        var segment = new LogSegment
        {
            Name = "STACK",
            Lines = new[]
            {
                "Some line with WEAP record",
                "Another WEAP line here"
            }
        };

        // Act
        var result = await _scanner.ScanAsync(segment);

        // Assert
        result.TotalMatches.Should().Be(2);
    }

    [Fact]
    public async Task ScanFromSegmentsAsync_FindsStackSegment()
    {
        // Arrange
        _scanner.Configuration = new RecordScannerConfiguration
        {
            TargetRecords = new[] { "WEAP" }
        };

        var segments = new[]
        {
            new LogSegment
            {
                Name = "PROBABLE CALL STACK",
                Lines = new[] { "Not the right segment" }
            },
            new LogSegment
            {
                Name = "STACK",
                Lines = new[] { "(WEAP) SomeWeapon" }
            },
            new LogSegment
            {
                Name = "REGISTERS",
                Lines = new[] { "RAX 0x0" }
            }
        };

        // Act
        var result = await _scanner.ScanFromSegmentsAsync(segments);

        // Assert
        result.HasRecords.Should().BeTrue();
        result.TotalMatches.Should().Be(1);
    }

    [Fact]
    public async Task ScanFromSegmentsAsync_WithNoStackSegment_ReturnsEmpty()
    {
        // Arrange
        _scanner.Configuration = new RecordScannerConfiguration
        {
            TargetRecords = new[] { "WEAP" }
        };

        var segments = new[]
        {
            new LogSegment
            {
                Name = "PROBABLE CALL STACK",
                Lines = new[] { "[0] 0x7FF..." }
            }
        };

        // Act
        var result = await _scanner.ScanFromSegmentsAsync(segments);

        // Assert
        result.HasRecords.Should().BeFalse();
    }

    [Fact]
    public void CreateReportFragment_WithNoRecords_ReturnsNotFoundMessage()
    {
        // Arrange
        _scanner.Configuration = new RecordScannerConfiguration
        {
            CrashGeneratorName = "Buffout4"
        };

        var result = RecordScanResult.Empty;

        // Act
        var fragment = _scanner.CreateReportFragment(result);

        // Assert
        fragment.HasContent.Should().BeTrue();
        var content = string.Join("\n", fragment.Lines);
        content.Should().Contain("COULDN'T FIND ANY NAMED RECORDS");
    }

    [Fact]
    public void CreateReportFragment_WithRecords_FormatsWithCounts()
    {
        // Arrange
        _scanner.Configuration = new RecordScannerConfiguration
        {
            CrashGeneratorName = "Buffout4"
        };

        var result = new RecordScanResult
        {
            MatchedRecords = new[] { "Record1", "Record1", "Record2" },
            RecordCounts = new Dictionary<string, int>
            {
                { "Record1", 2 },
                { "Record2", 1 }
            }
        };

        // Act
        var fragment = _scanner.CreateReportFragment(result);

        // Assert
        var content = string.Join("\n", fragment.Lines);
        content.Should().Contain("Named Records Found");
        content.Should().Contain("Record1 | 2");
        content.Should().Contain("Record2 | 1");
        content.Should().Contain("Buffout4");
    }

    [Fact]
    public void CreateReportFragment_ContainsExplanatoryNotes()
    {
        // Arrange
        _scanner.Configuration = new RecordScannerConfiguration
        {
            CrashGeneratorName = "TestGen"
        };

        var result = new RecordScanResult
        {
            MatchedRecords = new[] { "Record1" },
            RecordCounts = new Dictionary<string, int> { { "Record1", 1 } }
        };

        // Act
        var fragment = _scanner.CreateReportFragment(result);

        // Assert
        var content = string.Join("\n", fragment.Lines);
        content.Should().Contain("how many times");
        content.Should().Contain("TestGen");
        content.Should().Contain("game objects");
    }

    [Fact]
    public async Task ScanAsync_ExtractsTextAfterRspOffset()
    {
        // Arrange
        _scanner.Configuration = new RecordScannerConfiguration
        {
            TargetRecords = new[] { "WEAP" }
        };

        // Line format: "[RSP+XX ] " is followed by address, then content
        // The RSP offset (30) should skip past the marker and address
        var segment = new LogSegment
        {
            Name = "STACK",
            Lines = new[]
            {
                "[RSP+0  ] 0x12345678901234567890 (WEAP) TestWeapon"
            }
        };

        // Act
        var result = await _scanner.ScanAsync(segment);

        // Assert
        result.HasRecords.Should().BeTrue();
        result.MatchedRecords[0].Should().Contain("WEAP");
    }

    [Fact]
    public void RecordScanResult_Empty_HasCorrectDefaults()
    {
        // Assert
        RecordScanResult.Empty.HasRecords.Should().BeFalse();
        RecordScanResult.Empty.TotalMatches.Should().Be(0);
        RecordScanResult.Empty.UniqueRecordCount.Should().Be(0);
        RecordScanResult.Empty.MatchedRecords.Should().BeEmpty();
        RecordScanResult.Empty.RecordCounts.Should().BeEmpty();
    }

    [Fact]
    public void RecordScannerConfiguration_Empty_HasCorrectDefaults()
    {
        // Assert
        RecordScannerConfiguration.Empty.TargetRecords.Should().BeEmpty();
        RecordScannerConfiguration.Empty.IgnoreRecords.Should().BeEmpty();
        RecordScannerConfiguration.Empty.CrashGeneratorName.Should().Be("Crash Generator");
    }
}
