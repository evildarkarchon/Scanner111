using FluentAssertions;
using Scanner111.Common.Services.Parsing;

namespace Scanner111.Common.Tests.Services.Parsing;

/// <summary>
/// Tests for LogReformatter.
/// </summary>
public class LogReformatterTests
{
    [Fact]
    public void ReformatBuffout4LoadOrder_WithExcessiveSpaces_NormalizesSpacing()
    {
        // Arrange
        var logContent = @"PLUGINS:
  253   253    FD Unmanaged.esp
  254   254    FE AnotherMod.esp";

        // Act
        var reformatted = LogReformatter.ReformatBuffout4LoadOrder(logContent);

        // Assert
        reformatted.Should().Contain("253 253 FD Unmanaged.esp");
        reformatted.Should().Contain("254 254 FE AnotherMod.esp");
    }

    [Fact]
    public void ReformatBuffout4LoadOrder_WithVariousSpacing_NormalizesAll()
    {
        // Arrange
        var logContent = @"  0     0      00 Fallout4.esm
  1     1      01 DLCRobot.esm
  253   253    FD StartMeUp.esp";

        // Act
        var reformatted = LogReformatter.ReformatBuffout4LoadOrder(logContent);

        // Assert
        reformatted.Should().Contain("0 0 00 Fallout4.esm");
        reformatted.Should().Contain("1 1 01 DLCRobot.esm");
        reformatted.Should().Contain("253 253 FD StartMeUp.esp");
    }

    [Fact]
    public void ReformatBuffout4LoadOrder_WithLightPlugins_HandlesFEPrefix()
    {
        // Arrange
        var logContent = @"  254   254    FE:000 PPF.esm
  255   255    FE:001 Resources.esl";

        // Act
        var reformatted = LogReformatter.ReformatBuffout4LoadOrder(logContent);

        // Assert
        reformatted.Should().Contain("254 254 FE:000 PPF.esm");
        reformatted.Should().Contain("255 255 FE:001 Resources.esl");
    }

    [Fact]
    public void ReformatBuffout4LoadOrder_WithEmptyString_ReturnsEmpty()
    {
        // Arrange
        var logContent = string.Empty;

        // Act
        var reformatted = LogReformatter.ReformatBuffout4LoadOrder(logContent);

        // Assert
        reformatted.Should().BeEmpty();
    }

    [Fact]
    public void ReformatBuffout4LoadOrder_WithNullString_ReturnsNull()
    {
        // Arrange
        string? logContent = null;

        // Act
        var reformatted = LogReformatter.ReformatBuffout4LoadOrder(logContent!);

        // Assert
        reformatted.Should().BeNull();
    }

    [Fact]
    public void ReformatBuffout4LoadOrder_WithNoLoadOrderLines_ReturnsUnchanged()
    {
        // Arrange
        var logContent = @"SYSTEM SPECS:
OS: Windows 10
CPU: Intel Core i7";

        // Act
        var reformatted = LogReformatter.ReformatBuffout4LoadOrder(logContent);

        // Assert
        reformatted.Should().Be(logContent);
    }

    [Fact]
    public void ReformatBuffout4LoadOrder_WithMixedContent_OnlyReformatsLoadOrder()
    {
        // Arrange
        var logContent = @"SYSTEM SPECS:
OS: Windows 10

PLUGINS:
  0     0      00 Fallout4.esm
  1     1      01 TestMod.esp

MODULES:
module.dll";

        // Act
        var reformatted = LogReformatter.ReformatBuffout4LoadOrder(logContent);

        // Assert
        reformatted.Should().Contain("0 0 00 Fallout4.esm");
        reformatted.Should().Contain("1 1 01 TestMod.esp");
        reformatted.Should().Contain("SYSTEM SPECS:");
        reformatted.Should().Contain("module.dll");
    }

    [Fact]
    public void ReformatBuffout4LoadOrder_PreservesPluginNamesWithSpaces()
    {
        // Arrange
        var logContent = @"  100   100    64 My Cool Mod.esp
  101   101    65 Another Mod With Spaces.esp";

        // Act
        var reformatted = LogReformatter.ReformatBuffout4LoadOrder(logContent);

        // Assert
        reformatted.Should().Contain("100 100 64 My Cool Mod.esp");
        reformatted.Should().Contain("101 101 65 Another Mod With Spaces.esp");
    }

    [Fact]
    public void ReformatBuffout4LoadOrder_WithHexFormIds_HandlesCorrectly()
    {
        // Arrange
        var logContent = @"  0     0      00 Fallout4.esm
  15    15     0F TestMod.esp
  255   255    FF AnotherMod.esp";

        // Act
        var reformatted = LogReformatter.ReformatBuffout4LoadOrder(logContent);

        // Assert
        reformatted.Should().Contain("0 0 00 Fallout4.esm");
        reformatted.Should().Contain("15 15 0F TestMod.esp");
        reformatted.Should().Contain("255 255 FF AnotherMod.esp");
    }

    [Fact]
    public void ReformatBuffout4LoadOrder_WithCompleteLog_ReformatsCorrectly()
    {
        // Arrange
        var logContent = @"Fallout 4 v1.10.163.0
Buffout 4 v1.26.2

PLUGINS:
  0     0      00 Fallout4.esm
  253   253    FD StartMeUp.esp
  254   254    FE:000 PPF.esm

MODULES:
f4se_1_10_163.dll";

        // Act
        var reformatted = LogReformatter.ReformatBuffout4LoadOrder(logContent);

        // Assert
        reformatted.Should().Contain("0 0 00 Fallout4.esm");
        reformatted.Should().Contain("253 253 FD StartMeUp.esp");
        reformatted.Should().Contain("254 254 FE:000 PPF.esm");
        reformatted.Should().Contain("Fallout 4 v1.10.163.0");
        reformatted.Should().Contain("f4se_1_10_163.dll");
    }
}
