using FluentAssertions;
using Scanner111.Common.Services.Parsing;

namespace Scanner111.Common.Tests.Services.Parsing;

/// <summary>
/// Tests for CrashHeaderParser.
/// </summary>
public class CrashHeaderParserTests
{
    private readonly CrashHeaderParser _parser;

    public CrashHeaderParserTests()
    {
        _parser = new CrashHeaderParser();
    }

    [Fact]
    public void Parse_WithFallout4Version_ExtractsCorrectly()
    {
        // Arrange
        var logContent = @"Fallout 4 v1.10.163.0
Some other content";

        // Act
        var header = _parser.Parse(logContent);

        // Assert
        header.Should().NotBeNull();
        header!.GameVersion.Should().Be("1.10.163.0");
    }

    [Fact]
    public void Parse_WithSkyrimVersion_ExtractsCorrectly()
    {
        // Arrange
        var logContent = @"Skyrim SE v1.5.97.0
Some other content";

        // Act
        var header = _parser.Parse(logContent);

        // Assert
        header.Should().NotBeNull();
        header!.GameVersion.Should().Be("1.5.97.0");
    }

    [Fact]
    public void Parse_WithBuffout4Version_ExtractsCorrectly()
    {
        // Arrange
        var logContent = @"Fallout 4 v1.10.163.0
Buffout 4 v1.26.2";

        // Act
        var header = _parser.Parse(logContent);

        // Assert
        header.Should().NotBeNull();
        header!.CrashGeneratorVersion.Should().Be("Buffout 4 v1.26.2");
    }

    [Fact]
    public void Parse_WithCrashLogger_ExtractsCorrectly()
    {
        // Arrange
        var logContent = @"Skyrim SE v1.5.97.0
Crash Logger v1.0.0";

        // Act
        var header = _parser.Parse(logContent);

        // Assert
        header.Should().NotBeNull();
        header!.CrashGeneratorVersion.Should().Be("Crash Logger v1.0.0");
    }

    [Fact]
    public void Parse_WithMainError_ExtractsCorrectly()
    {
        // Arrange
        var logContent = @"Fallout 4 v1.10.163.0
Unhandled exception ""EXCEPTION_ACCESS_VIOLATION"" at 0x7FF64E9A0000";

        // Act
        var header = _parser.Parse(logContent);

        // Assert
        header.Should().NotBeNull();
        header!.MainError.Should().Be("EXCEPTION_ACCESS_VIOLATION");
    }

    [Fact]
    public void Parse_WithTimestamp_ExtractsCorrectly()
    {
        // Arrange
        var logContent = @"Fallout 4 v1.10.163.0
Crash log generated at 2023-12-07 02:24:27";

        // Act
        var header = _parser.Parse(logContent);

        // Assert
        header.Should().NotBeNull();
        header!.CrashTimestamp.Should().NotBeNull();
        header.CrashTimestamp!.Value.Year.Should().Be(2023);
        header.CrashTimestamp.Value.Month.Should().Be(12);
        header.CrashTimestamp.Value.Day.Should().Be(7);
        header.CrashTimestamp.Value.Hour.Should().Be(2);
        header.CrashTimestamp.Value.Minute.Should().Be(24);
        header.CrashTimestamp.Value.Second.Should().Be(27);
    }

    [Fact]
    public void Parse_WithAllFields_ExtractsAll()
    {
        // Arrange
        var logContent = @"Fallout 4 v1.10.163.0
Buffout 4 v1.26.2

Unhandled exception ""EXCEPTION_ACCESS_VIOLATION"" at 0x7FF64E9A0000

Crash log generated at 2023-12-07 02:24:27

[Compatibility]
More content...";

        // Act
        var header = _parser.Parse(logContent);

        // Assert
        header.Should().NotBeNull();
        header!.GameVersion.Should().Be("1.10.163.0");
        header.CrashGeneratorVersion.Should().Be("Buffout 4 v1.26.2");
        header.MainError.Should().Be("EXCEPTION_ACCESS_VIOLATION");
        header.CrashTimestamp.Should().NotBeNull();
    }

    [Fact]
    public void Parse_WithEmptyString_ReturnsNull()
    {
        // Arrange
        var logContent = string.Empty;

        // Act
        var header = _parser.Parse(logContent);

        // Assert
        header.Should().BeNull();
    }

    [Fact]
    public void Parse_WithNoRecognizableContent_ReturnsNull()
    {
        // Arrange
        var logContent = @"This is not a valid crash log
No version information
No crash data";

        // Act
        var header = _parser.Parse(logContent);

        // Assert
        header.Should().BeNull();
    }

    [Fact]
    public void Parse_WithPartialData_ReturnsHeaderWithAvailableFields()
    {
        // Arrange
        var logContent = @"Fallout 4 v1.10.163.0
Some random content without other metadata";

        // Act
        var header = _parser.Parse(logContent);

        // Assert
        header.Should().NotBeNull();
        header!.GameVersion.Should().Be("1.10.163.0");
        header.CrashGeneratorVersion.Should().BeEmpty();
        header.MainError.Should().BeEmpty();
        header.CrashTimestamp.Should().BeNull();
    }

    [Theory]
    [InlineData("Fallout 4 v1.10.163.0", "1.10.163.0")]
    [InlineData("fallout 4 v2.0.0.1", "2.0.0.1")]
    [InlineData("Fallout 4 V1.9.4.0", "1.9.4.0")]
    public void Parse_WithVariousFalloutVersions_ExtractsCorrectly(string versionLine, string expectedVersion)
    {
        // Arrange
        var logContent = versionLine;

        // Act
        var header = _parser.Parse(logContent);

        // Assert
        header.Should().NotBeNull();
        header!.GameVersion.Should().Be(expectedVersion);
    }

    [Theory]
    [InlineData("Skyrim SE v1.5.97.0", "1.5.97.0")]
    [InlineData("Skyrim Special Edition v1.6.1170.0", "1.6.1170.0")]
    [InlineData("SKYRIM SE V1.5.39.0", "1.5.39.0")]
    public void Parse_WithVariousSkyrimVersions_ExtractsCorrectly(string versionLine, string expectedVersion)
    {
        // Arrange
        var logContent = versionLine;

        // Act
        var header = _parser.Parse(logContent);

        // Assert
        header.Should().NotBeNull();
        header!.GameVersion.Should().Be(expectedVersion);
    }

    [Fact]
    public void Parse_WithInvalidTimestamp_ReturnsNullTimestamp()
    {
        // Arrange
        var logContent = @"Fallout 4 v1.10.163.0
Crash log at 2023-99-99 99:99:99";

        // Act
        var header = _parser.Parse(logContent);

        // Assert
        header.Should().NotBeNull();
        header!.CrashTimestamp.Should().BeNull();
    }

    [Fact]
    public void Parse_OnlyParsesFirst2000Characters_ForPerformance()
    {
        // Arrange
        var longContent = new string('X', 3000);
        var logContent = $"Fallout 4 v1.10.163.0\n{longContent}\nBuffout 4 v1.0.0";

        // Act
        var header = _parser.Parse(logContent);

        // Assert
        header.Should().NotBeNull();
        header!.GameVersion.Should().Be("1.10.163.0");
        // Buffout version should not be found since it's beyond 2000 chars
        header.CrashGeneratorVersion.Should().BeEmpty();
    }
}
