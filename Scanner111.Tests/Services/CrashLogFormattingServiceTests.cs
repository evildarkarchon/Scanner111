using Scanner111.Models;
using Scanner111.Services;

namespace Scanner111.Tests.Services;

public class CrashLogFormattingServiceTests
{
    private readonly AppSettings _appSettings;
    private readonly CrashLogFormattingService _service;
    private readonly string _tempPath;

    public CrashLogFormattingServiceTests()
    {
        _appSettings = new AppSettings
        {
            SimplifyLogs = true,
            SimplifyRemoveStrings = new List<string> { "ntdll.dll", "Steam.dll" }
        };

        _service = new CrashLogFormattingService(_appSettings);
        _tempPath = Path.GetTempPath();
    }

    [Fact]
    public async Task ReformatCrashLogsAsync_ShouldReformatPluginLoadOrder()
    {
        // Arrange
        var crashLogPath = Path.Combine(_tempPath, $"test-crash-{Guid.NewGuid()}.log");
        var content = @"PLUGINS:
[ 1] DLCRobot.esm
[FE:  0] RedRocketsGlareII.esl
[FE: 1] Some Plugin.esp
[23] Another Plugin.esp
MODULES:
ntdll.dll";
        await File.WriteAllTextAsync(crashLogPath, content);

        try
        {
            // Act
            var result =
                await _service.ReformatCrashLogsAsync(new[] { crashLogPath }, _appSettings.SimplifyRemoveStrings);
            var reformattedContent = await File.ReadAllTextAsync(crashLogPath);

            // Assert
            Assert.Equal(1, result); // One file processed
            Assert.Contains("[01]", reformattedContent); // Space replaced with zero
            Assert.Contains("[FE:000]", reformattedContent); // Spaces replaced with zeros
            Assert.Contains("[FE:001]", reformattedContent); // Spaces replaced with zeros
            Assert.Contains("[23]", reformattedContent); // No spaces, left unchanged
            Assert.DoesNotContain("ntdll.dll", reformattedContent); // Line with ntdll.dll was removed
        }
        finally
        {
            // Cleanup
            if (File.Exists(crashLogPath)) File.Delete(crashLogPath);
        }
    }

    [Fact]
    public void FormatCrashLogContent_ShouldReformatContent()
    {
        // Arrange
        var originalContent = @"PLUGINS:
[ 1] DLCRobot.esm
[FE:  0] RedRocketsGlareII.esl
[FE: 1] Some Plugin.esp
MODULES:
ntdll.dll
PROBABLE CALL STACK:
Steam.dll+0x12345
Other Line";

        // Act
        var result = _service.FormatCrashLogContent(
            originalContent,
            _appSettings.SimplifyRemoveStrings,
            _appSettings.SimplifyLogs
        );

        // Assert
        Assert.Contains("[01] DLCRobot.esm", result);
        Assert.Contains("[FE:000] RedRocketsGlareII.esl", result);
        Assert.Contains("[FE:001] Some Plugin.esp", result);
        Assert.DoesNotContain("ntdll.dll", result);
        Assert.DoesNotContain("Steam.dll", result);
        Assert.Contains("Other Line", result);
    }

    [Fact]
    public void FormatCrashLogContent_WithSimplifyLogsFalse_ShouldNotRemoveLines()
    {
        // Arrange
        var originalContent = @"PLUGINS:
[ 1] DLCRobot.esm
MODULES:
ntdll.dll";

        // Act
        var result = _service.FormatCrashLogContent(
            originalContent,
            _appSettings.SimplifyRemoveStrings,
            false // Disable simplify logs
        );

        // Assert
        Assert.Contains("[01] DLCRobot.esm", result); // Still reformats plugin lines
        Assert.Contains("ntdll.dll", result); // But doesn't remove the excluded DLLs
    }

    [Fact]
    public void FormatCrashLogContent_WithMalformedPluginLines_ShouldHandleGracefully()
    {
        // Arrange
        var originalContent = @"PLUGINS:
[ 1] DLCRobot.esm
[Bad Line With No Closing Bracket
[FE: 1 Also Bad
[ ] Empty
Normal Line";

        // Act
        var result = _service.FormatCrashLogContent(
            originalContent,
            _appSettings.SimplifyRemoveStrings,
            _appSettings.SimplifyLogs
        );

        // Assert
        Assert.Contains("[01] DLCRobot.esm", result); // Correctly formatted
        Assert.Contains("[Bad Line With No Closing Bracket", result); // Preserved as-is
        Assert.Contains("[FE: 1 Also Bad", result); // Preserved as-is
        Assert.Contains("[ ] Empty", result); // Preserved as-is
        Assert.Contains("Normal Line", result); // Preserved as-is
    }

    [Fact]
    public void FormatCrashLogContent_WithNullContent_ReturnsNull()
    {
        // Note: Since C# is now making strings non-nullable by default,
        // we'll test with empty string instead

        // Arrange
        var originalContent = string.Empty;

        // Act
        var result = _service.FormatCrashLogContent(
            originalContent,
            _appSettings.SimplifyRemoveStrings,
            _appSettings.SimplifyLogs
        );

        // Assert
        Assert.Equal(originalContent, result);
    }

    [Fact]
    public void FormatCrashLogContent_WithEmptyContent_ReturnsEmpty()
    {
        // Arrange
        var originalContent = string.Empty;

        // Act
        var result = _service.FormatCrashLogContent(
            originalContent,
            _appSettings.SimplifyRemoveStrings,
            _appSettings.SimplifyLogs
        );

        // Assert
        Assert.Equal(originalContent, result);
    }

    [Fact]
    public void FormatCrashLogContent_WithEmptyRemoveStrings_ProcessesContentNormally()
    {
        // Arrange
        var originalContent = @"PLUGINS:
[FE:  2] SomePlugin.esp";
        var removeStrings = new List<string>();

        // Act
        var result = _service.FormatCrashLogContent(
            originalContent,
            removeStrings,
            true
        );

        // Assert
        Assert.Contains("[FE:002]", result);
    }

    [Fact]
    public async Task ReformatCrashLogsAsync_WithNoFiles_ReturnsZero()
    {
        // Act
        var result = await _service.ReformatCrashLogsAsync(
            new List<string>(),
            _appSettings.SimplifyRemoveStrings
        );

        // Assert
        Assert.Equal(0, result);
    }

    [Fact]
    public async Task ReformatCrashLogsAsync_WithNonexistentFile_ShouldSkipAndContinue()
    {
        // Arrange
        var validCrashLogPath = Path.Combine(_tempPath, $"test-crash-valid-{Guid.NewGuid()}.log");
        var nonExistingPath = Path.Combine(_tempPath, $"does-not-exist-{Guid.NewGuid()}.log");

        var content = @"PLUGINS:
[ 1] DLCRobot.esm";
        await File.WriteAllTextAsync(validCrashLogPath, content);

        try
        {
            // Act
            var result = await _service.ReformatCrashLogsAsync(
                new[] { validCrashLogPath, nonExistingPath },
                _appSettings.SimplifyRemoveStrings
            );

            // Assert
            Assert.Equal(1, result); // Only the valid file was processed
        }
        finally
        {
            // Cleanup
            if (File.Exists(validCrashLogPath)) File.Delete(validCrashLogPath);
        }
    }

    [Fact]
    public async Task IsCrashLogAsync_WithValidCrashLog_ReturnsTrue()
    {
        // Arrange
        var crashLogPath = Path.Combine(_tempPath, $"valid-crash-{Guid.NewGuid()}.log");
        var content = @"Unhandled exception occurred
PLUGINS:
[00] Fallout4.esm";
        await File.WriteAllTextAsync(crashLogPath, content);

        try
        {
            // Act
            var result = await _service.IsCrashLogAsync(crashLogPath);

            // Assert
            Assert.True(result);
        }
        finally
        {
            // Cleanup
            if (File.Exists(crashLogPath)) File.Delete(crashLogPath);
        }
    }

    [Fact]
    public async Task IsCrashLogAsync_WithNonCrashLog_ReturnsFalse()
    {
        // Arrange
        var logPath = Path.Combine(_tempPath, $"not-crash-{Guid.NewGuid()}.log");
        var content = @"Regular log file
Just some random text
No crash indicators here";
        await File.WriteAllTextAsync(logPath, content);

        try
        {
            // Act
            var result = await _service.IsCrashLogAsync(logPath);

            // Assert
            Assert.False(result);
        }
        finally
        {
            // Cleanup
            if (File.Exists(logPath)) File.Delete(logPath);
        }
    }

    [Fact]
    public async Task IsCrashLogAsync_WithNonexistentFile_ReturnsFalse()
    {
        // Arrange
        var nonExistingPath = Path.Combine(_tempPath, $"does-not-exist-{Guid.NewGuid()}.log");

        // Act
        var result = await _service.IsCrashLogAsync(nonExistingPath);

        // Assert
        Assert.False(result);
    }

    [Fact]
    public void FormatCrashLogContent_WithComplexRealWorldLog_FormatsCorrectly()
    {
        // Arrange - Complex realistic crash log with mixed plugin formats and DLLs to remove
        var originalContent = @"Fallout 4 v1.10.163
Buffout 4 v1.26.2

Unhandled exception ""EXCEPTION_ACCESS_VIOLATION"" at 0x7FF7C3F48000

STACK:
0x7FF7C3F48000 - Fallout4.exe+1AB8000
0x7FF7C3F4A000 - Fallout4.exe+1ABA000
0x7FF7C3C12F0F - Fallout4.exe+1782F0F

PLUGINS:
[00:  0] Fallout4.esm
[01:  1] DLCRobot.esm
[02:  2] DLCworkshop01.esm
[03:  3] DLCCoast.esm
[FE:  0] ccBGSFO4001-PipBoy(Black).esl
[FE:  1] ccBGSFO4002-PipBoy(Blue).esl
[FE:  2] ccBGSFO4003-PipBoy(Camo01).esl
[FE:  3] ccBGSFO4004-PipBoy(Camo02).esl
[FE:  4] ccBGSFO4006-PipBoy(Chrome).esl
[FE: 25] WeaponMod.esp
[FE: 26] ArmorMod.esp
[FE:102] GameplayOverhaul.esp

SYSTEM SPECS:
    OS: Microsoft Windows 10 Home v10.0.19041
    CPU: AuthenticAMD AMD Ryzen 7 3700X 8-Core Processor 
    GPU #1: Nvidia TU104 [GeForce RTX 2070 SUPER]
    GPU #2: Microsoft Basic Render Driver
    PHYSICAL MEMORY: 15.90 GB/31.94 GB

PROBABLE CALL STACK:
	[ 0] Fallout4.exe+1AB8000	->	N/A
	[ 1] Fallout4.exe+1ABA000	->	N/A
	[ 2] Fallout4.exe+1782F0F	->	N/A
	[ 3] Fallout4.exe+1749FD3	->	N/A

MODULES:
	nvwgf2umx.dll
	Steam.dll
	KERNELBASE.dll
	ntdll.dll";

        // Act
        var result = _service.FormatCrashLogContent(
            originalContent,
            _appSettings.SimplifyRemoveStrings, // Includes ntdll.dll and Steam.dll
            _appSettings.SimplifyLogs
        );

        // Assert
        // Check that plugin entries are properly formatted
        Assert.Contains("[00:000] Fallout4.esm", result);
        Assert.Contains("[01:001] DLCRobot.esm", result);
        Assert.Contains("[FE:000] ccBGSFO4001-PipBoy(Black).esl", result);
        Assert.Contains("[FE:025] WeaponMod.esp", result);
        Assert.Contains("[FE:102] GameplayOverhaul.esp", result);

        // Check that specified DLLs are removed
        Assert.DoesNotContain("Steam.dll", result);
        Assert.DoesNotContain("ntdll.dll", result);

        // But other DLLs are still present because they're not in the removal list
        Assert.Contains("nvwgf2umx.dll", result);
        Assert.Contains("KERNELBASE.dll", result);

        // Check that other crash log content is preserved
        Assert.Contains("Unhandled exception", result);
        Assert.Contains("STACK:", result);
        Assert.Contains("SYSTEM SPECS:", result);
    }
}