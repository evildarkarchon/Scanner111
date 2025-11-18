using FluentAssertions;
using Scanner111.Common.Services.Parsing;

namespace Scanner111.Common.Tests.Services.Parsing;

/// <summary>
/// Tests for LogParser.
/// </summary>
public class LogParserTests
{
    private readonly LogParser _parser;

    public LogParserTests()
    {
        _parser = new LogParser();
    }

    [Fact]
    public async Task ParseAsync_WithValidLog_ReturnsValidResult()
    {
        // Arrange
        var logContent = @"Fallout 4 v1.10.163.0
Buffout 4 v1.26.2

Unhandled exception ""EXCEPTION_ACCESS_VIOLATION"" at 0x7FF64E9A0000

[Compatibility]
Some compatibility info

SYSTEM SPECS:
OS: Windows 10
CPU: Intel Core i7

MODULES:
module1.dll
module2.dll

PLUGINS:
[00] Fallout4.esm
[01] TestMod.esp
";

        // Act
        var result = await _parser.ParseAsync(logContent);

        // Assert
        result.IsValid.Should().BeTrue();
        result.Header.Should().NotBeNull();
        result.Header.GameVersion.Should().Be("1.10.163.0");
        result.Segments.Should().NotBeEmpty();
    }

    [Fact]
    public async Task ParseAsync_WithIncompleteLog_MarksAsIncomplete()
    {
        // Arrange
        var incompleteLog = @"Fallout 4 v1.10.163.0
Buffout 4 v1.26.2

SYSTEM SPECS:
OS: Windows 10
";

        // Act
        var result = await _parser.ParseAsync(incompleteLog);

        // Assert
        result.IsValid.Should().BeTrue();
        result.ErrorMessage.Should().Contain("incomplete");
    }

    [Fact]
    public void ExtractSegments_WithMultipleSegments_ExtractsCorrectly()
    {
        // Arrange
        var logContent = @"[Compatibility]
Line 1
Line 2

SYSTEM SPECS:
Spec line 1
Spec line 2

MODULES:
Module1
Module2

PLUGINS:
Plugin1
Plugin2
";

        // Act
        var segments = _parser.ExtractSegments(logContent);

        // Assert
        segments.Should().HaveCount(4);
        segments[0].Name.Should().Be("Compatibility");
        segments[1].Name.Should().Be("SYSTEM SPECS");
        segments[2].Name.Should().Be("MODULES");
        segments[3].Name.Should().Be("PLUGINS");
    }

    [Fact]
    public void ExtractSegments_WithBracketedSections_ExtractsName()
    {
        // Arrange
        var logContent = @"[Compatibility]
Compatibility info
[Skyrim Special Edition]
Game info
";

        // Act
        var segments = _parser.ExtractSegments(logContent);

        // Assert
        segments.Should().HaveCount(2);
        segments[0].Name.Should().Be("Compatibility");
        segments[1].Name.Should().Be("Skyrim Special Edition");
    }

    [Fact]
    public void ExtractSegments_WithColonSections_ExtractsName()
    {
        // Arrange
        var logContent = @"SYSTEM SPECS:
Windows 10

PROBABLE CALL STACK:
Stack trace

MODULES:
module.dll
";

        // Act
        var segments = _parser.ExtractSegments(logContent);

        // Assert
        segments.Should().HaveCount(3);
        segments[0].Name.Should().Be("SYSTEM SPECS");
        segments[1].Name.Should().Be("PROBABLE CALL STACK");
        segments[2].Name.Should().Be("MODULES");
    }

    [Fact]
    public void ExtractSegments_EmptyLog_ReturnsEmptyList()
    {
        // Arrange
        var emptyLog = string.Empty;

        // Act
        var segments = _parser.ExtractSegments(emptyLog);

        // Assert
        segments.Should().BeEmpty();
    }

    [Fact]
    public void ExtractSegments_PreservesLineContent()
    {
        // Arrange
        var logContent = @"[TestSection]
Line 1
Line 2
Line 3
";

        // Act
        var segments = _parser.ExtractSegments(logContent);

        // Assert
        segments.Should().HaveCount(1);
        segments[0].Lines.Should().Contain(line => line.Contains("Line 1"));
        segments[0].Lines.Should().Contain(line => line.Contains("Line 2"));
        segments[0].Lines.Should().Contain(line => line.Contains("Line 3"));
    }

    [Fact]
    public void ExtractSegments_SetsCorrectIndices()
    {
        // Arrange
        var logContent = @"[First]
Content 1

[Second]
Content 2
";

        // Act
        var segments = _parser.ExtractSegments(logContent);

        // Assert
        segments[0].StartIndex.Should().Be(0);
        segments[0].EndIndex.Should().BeLessThanOrEqualTo(segments[1].StartIndex);
        segments[1].StartIndex.Should().BeGreaterThan(0);
    }

    [Theory]
    [InlineData("sample_logs/FO4/crash-$123 1.log")]
    [InlineData("sample_logs/FO4/crash-0akensh1eld 1.log")]
    [InlineData("sample_logs/FO4/crash-0DB9300.log")]
    public async Task ParseAsync_WithRealSampleLogs_ParsesWithoutException(string logPath)
    {
        // Skip test if sample log doesn't exist
        if (!File.Exists(logPath))
        {
            return;
        }

        // Arrange
        var content = await File.ReadAllTextAsync(logPath);

        // Act
        var result = await _parser.ParseAsync(content);

        // Assert
        result.Should().NotBeNull();
        // Don't assert IsValid as some sample logs may be intentionally malformed
    }

    [Fact]
    public async Task ParseAsync_WithCancellation_ThrowsOperationCancelledException()
    {
        // Arrange
        var logContent = "Some log content";
        var cts = new CancellationTokenSource();
        cts.Cancel();

        // Act
        var act = () => _parser.ParseAsync(logContent, cts.Token);

        // Assert
        await act.Should().ThrowAsync<OperationCanceledException>();
    }
}
