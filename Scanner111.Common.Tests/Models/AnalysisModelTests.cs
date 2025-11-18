using FluentAssertions;
using Scanner111.Common.Models.Analysis;

namespace Scanner111.Common.Tests.Models;

/// <summary>
/// Tests for analysis model classes (LogSegment, CrashHeader, PluginInfo, ModuleInfo).
/// </summary>
public class AnalysisModelTests
{
    [Fact]
    public void LogSegment_DefaultValues_AreCorrect()
    {
        // Arrange & Act
        var segment = new LogSegment();

        // Assert
        segment.Name.Should().BeEmpty();
        segment.Lines.Should().BeEmpty();
        segment.StartIndex.Should().Be(0);
        segment.EndIndex.Should().Be(0);
    }

    [Fact]
    public void LogSegment_WithData_StoresCorrectly()
    {
        // Arrange
        var lines = new List<string> { "Line 1", "Line 2", "Line 3" };

        // Act
        var segment = new LogSegment
        {
            Name = "SYSTEM SPECS",
            Lines = lines,
            StartIndex = 100,
            EndIndex = 500
        };

        // Assert
        segment.Name.Should().Be("SYSTEM SPECS");
        segment.Lines.Should().HaveCount(3);
        segment.Lines[0].Should().Be("Line 1");
        segment.StartIndex.Should().Be(100);
        segment.EndIndex.Should().Be(500);
    }

    [Fact]
    public void LogSegment_IsImmutable()
    {
        // Arrange
        var segment1 = new LogSegment { Name = "MODULES" };

        // Act
        var segment2 = segment1 with { StartIndex = 200 };

        // Assert
        segment1.StartIndex.Should().Be(0);
        segment2.StartIndex.Should().Be(200);
        segment1.Name.Should().Be(segment2.Name);
    }

    [Fact]
    public void CrashHeader_DefaultValues_AreCorrect()
    {
        // Arrange & Act
        var header = new CrashHeader();

        // Assert
        header.GameVersion.Should().BeEmpty();
        header.CrashGeneratorVersion.Should().BeEmpty();
        header.MainError.Should().BeEmpty();
        header.CrashTimestamp.Should().BeNull();
    }

    [Fact]
    public void CrashHeader_WithData_StoresCorrectly()
    {
        // Arrange
        var timestamp = new DateTime(2025, 11, 17, 14, 30, 0);

        // Act
        var header = new CrashHeader
        {
            GameVersion = "1.10.163.0",
            CrashGeneratorVersion = "Buffout 4 v1.26.2",
            MainError = "Unhandled exception \"EXCEPTION_ACCESS_VIOLATION\"",
            CrashTimestamp = timestamp
        };

        // Assert
        header.GameVersion.Should().Be("1.10.163.0");
        header.CrashGeneratorVersion.Should().Be("Buffout 4 v1.26.2");
        header.MainError.Should().Be("Unhandled exception \"EXCEPTION_ACCESS_VIOLATION\"");
        header.CrashTimestamp.Should().Be(timestamp);
    }

    [Fact]
    public void PluginInfo_RegularPlugin_IsNotLight()
    {
        // Arrange & Act
        var plugin = new PluginInfo
        {
            FormIdPrefix = "E7",
            PluginName = "StartMeUp.esp"
        };

        // Assert
        plugin.IsLightPlugin.Should().BeFalse();
    }

    [Fact]
    public void PluginInfo_LightPlugin_IsLight()
    {
        // Arrange & Act
        var plugin = new PluginInfo
        {
            FormIdPrefix = "FE:000",
            PluginName = "PPF.esm"
        };

        // Assert
        plugin.IsLightPlugin.Should().BeTrue();
    }

    [Theory]
    [InlineData("FE:000", true)]
    [InlineData("FE:001", true)]
    [InlineData("fe:000", true)] // Case insensitive
    [InlineData("E7", false)]
    [InlineData("00", false)]
    [InlineData("FF", false)]
    public void PluginInfo_IsLightPlugin_DetectsCorrectly(string formIdPrefix, bool expectedIsLight)
    {
        // Arrange & Act
        var plugin = new PluginInfo
        {
            FormIdPrefix = formIdPrefix,
            PluginName = "TestPlugin.esp"
        };

        // Assert
        plugin.IsLightPlugin.Should().Be(expectedIsLight);
    }

    [Fact]
    public void PluginInfo_IsImmutable()
    {
        // Arrange
        var plugin1 = new PluginInfo
        {
            FormIdPrefix = "E7",
            PluginName = "Original.esp"
        };

        // Act
        var plugin2 = plugin1 with { PluginName = "Modified.esp" };

        // Assert
        plugin1.PluginName.Should().Be("Original.esp");
        plugin2.PluginName.Should().Be("Modified.esp");
        plugin1.FormIdPrefix.Should().Be(plugin2.FormIdPrefix);
    }

    [Fact]
    public void ModuleInfo_DefaultValues_AreCorrect()
    {
        // Arrange & Act
        var module = new ModuleInfo();

        // Assert
        module.Name.Should().BeEmpty();
        module.Version.Should().BeNull();
        module.Path.Should().BeNull();
    }

    [Fact]
    public void ModuleInfo_WithData_StoresCorrectly()
    {
        // Arrange & Act
        var module = new ModuleInfo
        {
            Name = "f4se_1_10_163.dll",
            Version = "1.10.163.0",
            Path = @"C:\Games\Fallout4\f4se_1_10_163.dll"
        };

        // Assert
        module.Name.Should().Be("f4se_1_10_163.dll");
        module.Version.Should().Be("1.10.163.0");
        module.Path.Should().Be(@"C:\Games\Fallout4\f4se_1_10_163.dll");
    }

    [Fact]
    public void ModuleInfo_WithPartialData_AllowsNullValues()
    {
        // Arrange & Act
        var module = new ModuleInfo
        {
            Name = "UnknownModule.dll"
            // Version and Path are null
        };

        // Assert
        module.Name.Should().Be("UnknownModule.dll");
        module.Version.Should().BeNull();
        module.Path.Should().BeNull();
    }

    [Fact]
    public void ModuleInfo_IsImmutable()
    {
        // Arrange
        var module1 = new ModuleInfo { Name = "module1.dll" };

        // Act
        var module2 = module1 with { Version = "1.0.0" };

        // Assert
        module1.Version.Should().BeNull();
        module2.Version.Should().Be("1.0.0");
        module1.Name.Should().Be(module2.Name);
    }
}
