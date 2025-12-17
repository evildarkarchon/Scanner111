using System.Text;
using FluentAssertions;
using Scanner111.Common.Services.ScanGame;

namespace Scanner111.Common.Tests.Services.ScanGame;

/// <summary>
/// Tests for the IniParser class.
/// </summary>
public class IniParserTests : IDisposable
{
    private readonly IniParser _parser;
    private readonly string _tempDirectory;

    public IniParserTests()
    {
        _parser = new IniParser();
        _tempDirectory = Path.Combine(Path.GetTempPath(), $"IniParserTests_{Guid.NewGuid():N}");
        Directory.CreateDirectory(_tempDirectory);
    }

    public void Dispose()
    {
        try
        {
            if (Directory.Exists(_tempDirectory))
            {
                Directory.Delete(_tempDirectory, recursive: true);
            }
        }
        catch
        {
            // Ignore cleanup errors in tests
        }
        GC.SuppressFinalize(this);
    }

    #region Basic Parsing Tests

    [Fact]
    public void Parse_WithSimpleSection_ReturnsCorrectValues()
    {
        // Arrange
        const string content = """
            [General]
            Name=TestValue
            Count=42
            """;

        // Act
        var result = _parser.Parse(content);

        // Assert
        result.Should().ContainKey("General");
        result["General"].Should().ContainKey("Name");
        result["General"]["Name"].Should().Be("TestValue");
        result["General"]["Count"].Should().Be("42");
    }

    [Fact]
    public void Parse_WithMultipleSections_ReturnsAllSections()
    {
        // Arrange
        const string content = """
            [Section1]
            Key1=Value1

            [Section2]
            Key2=Value2
            """;

        // Act
        var result = _parser.Parse(content);

        // Assert
        result.Should().ContainKey("Section1");
        result.Should().ContainKey("Section2");
        result["Section1"]["Key1"].Should().Be("Value1");
        result["Section2"]["Key2"].Should().Be("Value2");
    }

    [Fact]
    public void Parse_WithGlobalSettings_ReturnsEmptySectionName()
    {
        // Arrange
        const string content = """
            GlobalKey=GlobalValue

            [Section]
            Key=Value
            """;

        // Act
        var result = _parser.Parse(content);

        // Assert
        result.Should().ContainKey("");
        result[""]["GlobalKey"].Should().Be("GlobalValue");
    }

    [Fact]
    public void Parse_IsCaseInsensitive_ForSectionLookup()
    {
        // Arrange
        const string content = """
            [General]
            Name=TestValue
            """;

        // Act
        var result = _parser.Parse(content);

        // Assert
        result.Should().ContainKey("general");
        result.Should().ContainKey("GENERAL");
        result.Should().ContainKey("General");
    }

    [Fact]
    public void Parse_IsCaseInsensitive_ForKeyLookup()
    {
        // Arrange
        const string content = """
            [Section]
            MyKey=MyValue
            """;

        // Act
        var result = _parser.Parse(content);

        // Assert
        result["Section"].Should().ContainKey("mykey");
        result["Section"].Should().ContainKey("MYKEY");
        result["Section"].Should().ContainKey("MyKey");
    }

    #endregion

    #region Comment Handling Tests

    [Fact]
    public void Parse_IgnoresSemicolonComments()
    {
        // Arrange
        const string content = """
            [Section]
            ; This is a comment
            Key=Value
            """;

        // Act
        var result = _parser.Parse(content);

        // Assert
        result["Section"].Should().HaveCount(1);
        result["Section"]["Key"].Should().Be("Value");
    }

    [Fact]
    public void Parse_IgnoresHashComments()
    {
        // Arrange
        const string content = """
            [Section]
            # This is a comment
            Key=Value
            """;

        // Act
        var result = _parser.Parse(content);

        // Assert
        result["Section"].Should().HaveCount(1);
    }

    [Fact]
    public void Parse_IgnoresDoubleSlashComments()
    {
        // Arrange
        const string content = """
            [Section]
            // This is a comment
            Key=Value
            """;

        // Act
        var result = _parser.Parse(content);

        // Assert
        result["Section"].Should().HaveCount(1);
    }

    [Fact]
    public void Parse_HandlesInlineComments()
    {
        // Arrange
        const string content = """
            [Section]
            Key=Value ; inline comment
            """;

        // Act
        var result = _parser.Parse(content);

        // Assert
        result["Section"]["Key"].Should().Be("Value");
    }

    #endregion

    #region Value Formatting Tests

    [Fact]
    public void Parse_TrimsWhitespace_FromValues()
    {
        // Arrange
        const string content = """
            [Section]
            Key =   Value With Spaces
            """;

        // Act
        var result = _parser.Parse(content);

        // Assert
        result["Section"]["Key"].Should().Be("Value With Spaces");
    }

    [Fact]
    public void Parse_RemovesDoubleQuotes_FromValues()
    {
        // Arrange
        const string content = """
            [Section]
            Key="Quoted Value"
            """;

        // Act
        var result = _parser.Parse(content);

        // Assert
        result["Section"]["Key"].Should().Be("Quoted Value");
    }

    [Fact]
    public void Parse_RemovesSingleQuotes_FromValues()
    {
        // Arrange
        const string content = """
            [Section]
            Key='Quoted Value'
            """;

        // Act
        var result = _parser.Parse(content);

        // Assert
        result["Section"]["Key"].Should().Be("Quoted Value");
    }

    [Fact]
    public void Parse_HandlesColonSeparator()
    {
        // Arrange - Some INI files use : instead of =
        const string content = """
            [Section]
            Key:Value
            """;

        // Act
        var result = _parser.Parse(content);

        // Assert
        result["Section"]["Key"].Should().Be("Value");
    }

    [Fact]
    public void Parse_HandlesEmptyValues()
    {
        // Arrange
        const string content = """
            [Section]
            Key=
            """;

        // Act
        var result = _parser.Parse(content);

        // Assert
        result["Section"]["Key"].Should().Be("");
    }

    [Fact]
    public void Parse_HandlesValuesWithEquals()
    {
        // Arrange
        const string content = """
            [Section]
            Equation=x=1+2
            """;

        // Act
        var result = _parser.Parse(content);

        // Assert
        result["Section"]["Equation"].Should().Be("x=1+2");
    }

    #endregion

    #region File Reading Tests

    [Fact]
    public async Task ParseFileAsync_WithUtf8File_ReturnsCorrectValues()
    {
        // Arrange
        var filePath = Path.Combine(_tempDirectory, "utf8.ini");
        await File.WriteAllTextAsync(filePath, "[Section]\nKey=Value", Encoding.UTF8);

        // Act
        var result = await _parser.ParseFileAsync(filePath);

        // Assert
        result["Section"]["Key"].Should().Be("Value");
    }

    [Fact]
    public async Task ParseFileAsync_WithUtf8BomFile_ReturnsCorrectValues()
    {
        // Arrange
        var filePath = Path.Combine(_tempDirectory, "utf8bom.ini");
        var encoding = new UTF8Encoding(encoderShouldEmitUTF8Identifier: true);
        await File.WriteAllTextAsync(filePath, "[Section]\nKey=Value", encoding);

        // Act
        var result = await _parser.ParseFileAsync(filePath);

        // Assert
        result["Section"]["Key"].Should().Be("Value");
    }

    [Fact]
    public async Task ParseFileAsync_WithUtf16File_ReturnsCorrectValues()
    {
        // Arrange
        var filePath = Path.Combine(_tempDirectory, "utf16.ini");
        await File.WriteAllTextAsync(filePath, "[Section]\nKey=Value", Encoding.Unicode);

        // Act
        var result = await _parser.ParseFileAsync(filePath);

        // Assert
        result["Section"]["Key"].Should().Be("Value");
    }

    [Fact]
    public async Task ParseFileAsync_WithNonExistentFile_ThrowsException()
    {
        // Arrange
        var filePath = Path.Combine(_tempDirectory, "nonexistent.ini");

        // Act & Assert
        await Assert.ThrowsAsync<FileNotFoundException>(() => _parser.ParseFileAsync(filePath));
    }

    #endregion

    #region Static Helper Tests

    [Fact]
    public void GetValue_ReturnsCorrectValue()
    {
        // Arrange
        var sections = _parser.Parse("""
            [Section]
            Key=Value
            """);

        // Act
        var value = IniParser.GetValue(sections, "Section", "Key");

        // Assert
        value.Should().Be("Value");
    }

    [Fact]
    public void GetValue_ReturnsNull_ForMissingSection()
    {
        // Arrange
        var sections = _parser.Parse("""
            [Section]
            Key=Value
            """);

        // Act
        var value = IniParser.GetValue(sections, "MissingSection", "Key");

        // Assert
        value.Should().BeNull();
    }

    [Fact]
    public void GetValue_ReturnsNull_ForMissingKey()
    {
        // Arrange
        var sections = _parser.Parse("""
            [Section]
            Key=Value
            """);

        // Act
        var value = IniParser.GetValue(sections, "Section", "MissingKey");

        // Assert
        value.Should().BeNull();
    }

    [Fact]
    public void HasSetting_ReturnsTrue_ForExistingSetting()
    {
        // Arrange
        var sections = _parser.Parse("""
            [Section]
            Key=Value
            """);

        // Act
        var exists = IniParser.HasSetting(sections, "Section", "Key");

        // Assert
        exists.Should().BeTrue();
    }

    [Fact]
    public void HasSetting_ReturnsFalse_ForMissingSetting()
    {
        // Arrange
        var sections = _parser.Parse("""
            [Section]
            Key=Value
            """);

        // Act
        var exists = IniParser.HasSetting(sections, "Section", "MissingKey");

        // Assert
        exists.Should().BeFalse();
    }

    #endregion

    #region Edge Case Tests

    [Fact]
    public void Parse_HandlesEmptyContent()
    {
        // Arrange
        const string content = "";

        // Act
        var result = _parser.Parse(content);

        // Assert
        result.Should().BeEmpty();
    }

    [Fact]
    public void Parse_HandlesWhitespaceOnlyContent()
    {
        // Arrange
        const string content = "   \n   \n   ";

        // Act
        var result = _parser.Parse(content);

        // Assert
        result.Should().BeEmpty();
    }

    [Fact]
    public void Parse_HandlesSectionWithNoValues()
    {
        // Arrange
        const string content = """
            [EmptySection]

            [AnotherSection]
            Key=Value
            """;

        // Act
        var result = _parser.Parse(content);

        // Assert
        result.Should().ContainKey("EmptySection");
        result["EmptySection"].Should().BeEmpty();
    }

    [Fact]
    public void Parse_HandlesDuplicateKeys_LastWins()
    {
        // Arrange
        const string content = """
            [Section]
            Key=First
            Key=Second
            """;

        // Act
        var result = _parser.Parse(content);

        // Assert
        result["Section"]["Key"].Should().Be("Second");
    }

    [Fact]
    public void Parse_HandlesSectionWithSpaces()
    {
        // Arrange
        const string content = """
            [Section With Spaces]
            Key=Value
            """;

        // Act
        var result = _parser.Parse(content);

        // Assert
        result.Should().ContainKey("Section With Spaces");
    }

    #endregion
}
