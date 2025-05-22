using Moq;
using Scanner111.Models;
using Scanner111.Services;

namespace Scanner111.Tests.Services;

public class CrashStackAnalysisTests
{
    private readonly AppSettings _appSettings;
    private readonly Mock<IYamlSettingsCacheService> _mockYamlSettingsCache;
    private readonly CrashStackAnalysis _service;

    public CrashStackAnalysisTests()
    {
        _appSettings = new AppSettings
        {
            CrashXseAcronym = "F4SE",
            GameRootName = "Fallout 4"
        };
        _mockYamlSettingsCache = new Mock<IYamlSettingsCacheService>();

        _service = new CrashStackAnalysis(_appSettings, _mockYamlSettingsCache.Object);
    }

    [Fact]
    public void FindSegments_WithValidCrashData_ExtractsCorrectSegments()
    {
        // Arrange
        var crashData = new List<string>
        {
            "Fallout 4 v1.10.163",
            "Buffout 4 v1.26.2",
            "",
            "Unhandled exception \"EXCEPTION_ACCESS_VIOLATION\" at 0x7FF7B5A6DDDD Fallout4.exe+0x16DDDD",
            "",
            "SYSTEM SPECS:",
            "    OS: Windows 10 Pro 64-bit",
            "    CPU: Intel Core i7-9700K",
            "",
            "PROBABLE CALL STACK:",
            "[0] 0x7FF7B5A6DDDD Fallout4.exe+0x16DDDD",
            "[1] 0x7FF7B5A6EEEE Fallout4.exe+0x16EEEE",
            "",
            "MODULES:",
            "0x000000400000 Fallout4.exe",
            "0x7FFCB8E70000 ntdll.dll",
            "",
            "F4SE PLUGINS:",
            "    f4se_1_10_163.dll",
            "    HighFPSPhysicsFix.dll v1.4",
            "",
            "PLUGINS:",
            "[00] Fallout4.esm",
            "[01] DLCRobot.esm"
        };

        // Act
        var result = _service.FindSegments(crashData, "Buffout 4");

        // Assert
        Assert.Equal("Fallout 4 v1.10.163", result.GameVersion);
        Assert.Equal("Buffout 4 v1.26.2", result.CrashgenVersion);
        Assert.Equal("Unhandled exception \"EXCEPTION_ACCESS_VIOLATION\" at 0x7FF7B5A6DDDD Fallout4.exe+0x16DDDD",
            result.MainError);

        // Check segments
        Assert.Equal(6, result.Segments.Count);

        // Check system specs segment
        Assert.Equal(2, result.Segments[1].Count);
        Assert.Equal("OS: Windows 10 Pro 64-bit", result.Segments[1][0]);
        Assert.Equal("CPU: Intel Core i7-9700K", result.Segments[1][1]);

        // Check call stack segment
        Assert.Equal(2, result.Segments[2].Count);
        Assert.Equal("[0] 0x7FF7B5A6DDDD Fallout4.exe+0x16DDDD", result.Segments[2][0]);
        Assert.Equal("[1] 0x7FF7B5A6EEEE Fallout4.exe+0x16EEEE", result.Segments[2][1]);

        // Check modules segment
        Assert.Equal(2, result.Segments[3].Count);
        Assert.Equal("0x000000400000 Fallout4.exe", result.Segments[3][0]);
        Assert.Equal("0x7FFCB8E70000 ntdll.dll", result.Segments[3][1]);

        // Check F4SE plugins segment
        Assert.Equal(2, result.Segments[4].Count);
        Assert.Equal("f4se_1_10_163.dll", result.Segments[4][0]);
        Assert.Equal("HighFPSPhysicsFix.dll v1.4", result.Segments[4][1]);

        // Check plugins segment
        Assert.Equal(2, result.Segments[5].Count);
        Assert.Equal("[00] Fallout4.esm", result.Segments[5][0]);
        Assert.Equal("[01] DLCRobot.esm", result.Segments[5][1]);
    }

    [Fact]
    public void FindSegments_WithMissingSegments_AddsEmptyLists()
    {
        // Arrange
        var crashData = new List<string>
        {
            "Fallout 4 v1.10.163",
            "Buffout 4 v1.26.2",
            "",
            "Unhandled exception \"EXCEPTION_ACCESS_VIOLATION\" at 0x7FF7B5A6DDDD Fallout4.exe+0x16DDDD",
            "",
            "SYSTEM SPECS:",
            "    OS: Windows 10 Pro 64-bit",
            "",
            "PLUGINS:",
            "[00] Fallout4.esm"
        };

        // Act
        var result = _service.FindSegments(crashData, "Buffout 4");

        // Assert
        Assert.Equal(6, result.Segments.Count);

        // Check that missing segments are empty lists
        Assert.Empty(result.Segments[2]); // PROBABLE CALL STACK
        Assert.Empty(result.Segments[3]); // MODULES
        Assert.Empty(result.Segments[4]); // F4SE PLUGINS
    }

    [Fact]
    public void FindSegments_WithEmptyCrashData_ReturnsEmptySegments()
    {
        // Arrange
        var crashData = new List<string>();

        // Act
        var result = _service.FindSegments(crashData, "Buffout 4");

        // Assert
        Assert.Equal("UNKNOWN", result.GameVersion);
        Assert.Equal("UNKNOWN", result.CrashgenVersion);
        Assert.Equal("UNKNOWN", result.MainError);
        Assert.Equal(6, result.Segments.Count);
        Assert.All(result.Segments, segment => Assert.Empty(segment));
    }

    [Fact]
    public void FindSegments_WithNullCrashgenName_StillExtractsGameVersion()
    {
        // Arrange
        var crashData = new List<string>
        {
            "Fallout 4 v1.10.163",
            "",
            "Unhandled exception \"EXCEPTION_ACCESS_VIOLATION\""
        };

        // Act
        var result = _service.FindSegments(crashData, null);

        // Assert
        Assert.Equal("Fallout 4 v1.10.163", result.GameVersion);
        Assert.Equal("UNKNOWN", result.CrashgenVersion);
        Assert.Equal("Unhandled exception \"EXCEPTION_ACCESS_VIOLATION\"", result.MainError);
    }

    [Fact]
    public void FindSegments_WithDifferentXseAcronym_AdjustsSegmentBoundaries()
    {
        // Arrange
        _appSettings.CrashXseAcronym = "SKSE";

        var crashData = new List<string>
        {
            "Skyrim v1.5.97",
            "Crash Logger v1.0.0",
            "",
            "Unhandled exception",
            "",
            "SYSTEM SPECS:",
            "    OS: Windows 10",
            "",
            "PROBABLE CALL STACK:",
            "[0] 0x12345678",
            "",
            "MODULES:",
            "0x00000000 SkyrimSE.exe",
            "",
            "SKSE PLUGINS:",
            "    skse64_1_5_97.dll",
            "",
            "PLUGINS:",
            "[00] Skyrim.esm"
        };

        // Act
        var result = _service.FindSegments(crashData, "Crash Logger");

        // Assert
        Assert.Equal("Skyrim v1.5.97", result.GameVersion);
        Assert.Equal("Crash Logger v1.0.0", result.CrashgenVersion);

        // Check SKSE plugins segment
        Assert.Single(result.Segments[4]);
        Assert.Equal("skse64_1_5_97.dll", result.Segments[4][0]);
    }

    [Fact]
    public void FindSegments_WithMultilineError_HandlesCorrectly()
    {
        // Arrange
        var crashData = new List<string>
        {
            "Fallout 4 v1.10.163",
            "Buffout 4 v1.26.2",
            "",
            "Unhandled exception \"EXCEPTION_ACCESS_VIOLATION\"|at 0x7FF7B5A6DDDD|Fallout4.exe+0x16DDDD",
            "",
            "SYSTEM SPECS:",
            "    OS: Windows 10"
        };

        // Act
        var result = _service.FindSegments(crashData, "Buffout 4");

        // Assert
        Assert.Contains(Environment.NewLine, result.MainError);
    }

    [Fact]
    public void FindSegments_WithNullLines_HandlesGracefully()
    {
        // Arrange
        var crashData = new List<string>
        {
            "Fallout 4 v1.10.163",
            null,
            "SYSTEM SPECS:",
            null,
            "PROBABLE CALL STACK:",
            null
        };

        // Act
        var result = _service.FindSegments(crashData, "Buffout 4");

        // Assert
        Assert.Equal("Fallout 4 v1.10.163", result.GameVersion);
        Assert.Equal(6, result.Segments.Count);
        // Null lines should be converted to empty strings
        Assert.All(result.Segments, segment =>
            Assert.All(segment, line => Assert.NotNull(line)));
    }
}