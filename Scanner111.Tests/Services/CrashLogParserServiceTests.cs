using Scanner111.Services;

namespace Scanner111.Tests.Services;

public class CrashLogParserServiceTests
{
    private readonly CrashLogParserService _service;
    private readonly string _tempPath;

    public CrashLogParserServiceTests()
    {
        _service = new CrashLogParserService();
        _tempPath = Path.GetTempPath();
    }

    [Fact]
    public async Task ParseCrashLogContentAsync_WithNonExistentFile_ReturnsEmptyParsedLog()
    {
        // Arrange
        var nonExistentPath = Path.Combine(_tempPath, $"non-existent-{Guid.NewGuid()}.log");

        // Act
        var result = await _service.ParseCrashLogContentAsync(nonExistentPath);

        // Assert
        Assert.Equal(nonExistentPath, result.FilePath);
        Assert.Empty(result.Lines);
        Assert.Empty(result.MainErrorSegment);
        Assert.Empty(result.CallStack);
        Assert.Empty(result.LoadedPlugins);
        Assert.Empty(result.OtherSegments);
    }

    [Fact]
    public async Task ParseCrashLogContentAsync_WithEmptyFile_ReturnsEmptySegments()
    {
        // Arrange
        var emptyFilePath = Path.Combine(_tempPath, $"empty-crash-{Guid.NewGuid()}.log");
        await File.WriteAllTextAsync(emptyFilePath, string.Empty);

        try
        {
            // Act
            var result = await _service.ParseCrashLogContentAsync(emptyFilePath);

            // Assert
            Assert.Equal(emptyFilePath, result.FilePath);
            Assert.Empty(result.Lines);
            Assert.Empty(result.MainErrorSegment);
            Assert.Empty(result.CallStack);
            Assert.Empty(result.LoadedPlugins);
            Assert.Empty(result.OtherSegments);
        }
        finally
        {
            // Cleanup
            if (File.Exists(emptyFilePath)) File.Delete(emptyFilePath);
        }
    }

    [Fact]
    public async Task ParseCrashLogContentAsync_WithValidCrashLog_ParsesCorrectly()
    {
        // Arrange
        var crashLogPath = Path.Combine(_tempPath, $"valid-crash-{Guid.NewGuid()}.log");
        var content = @"Fallout 4 v1.10.163
Buffout 4 v1.26.2

Unhandled exception ""EXCEPTION_ACCESS_VIOLATION"" at 0x7FF7B5A6DDDD Fallout4.exe+0x16DDDD

PROBABLE CALL STACK:
[0] 0x7FF7B5A6DDDD Fallout4.exe+0x16DDDD
[1] 0x7FF7B5A6EEEE Fallout4.exe+0x16EEEE

PLUGINS:
[00] Fallout4.esm
[01] DLCRobot.esm
[FE:000] SomePlugin.esp

MODULES:
0x000000400000 Fallout4.exe
0x7FFCB8E70000 ntdll.dll";

        await File.WriteAllTextAsync(crashLogPath, content);

        try
        {
            // Act
            var result = await _service.ParseCrashLogContentAsync(crashLogPath);

            // Assert
            Assert.Equal(crashLogPath, result.FilePath);
            Assert.Equal(13, result.Lines.Count);
            Assert.Equal("1.10.163", result.GameVersion);
            Assert.Equal("Buffout 4 v1.26.2", result.CrashGeneratorName);

            // Check main error segment
            Assert.Single(result.MainErrorSegment);
            Assert.Contains("Unhandled exception", result.MainErrorSegment[0]);

            // Check call stack
            Assert.Equal(2, result.CallStack.Count);
            Assert.Contains("0x7FF7B5A6DDDD", result.CallStack[0]);
            Assert.Contains("0x7FF7B5A6EEEE", result.CallStack[1]);

            // Check loaded plugins
            Assert.Equal(3, result.LoadedPlugins.Count);
            Assert.True(result.LoadedPlugins.ContainsKey("Fallout4.esm"));
            Assert.Equal("00", result.LoadedPlugins["Fallout4.esm"]);
            Assert.True(result.LoadedPlugins.ContainsKey("DLCRobot.esm"));
            Assert.Equal("01", result.LoadedPlugins["DLCRobot.esm"]);
            Assert.True(result.LoadedPlugins.ContainsKey("SomePlugin.esp"));
            Assert.Equal("FE:000", result.LoadedPlugins["SomePlugin.esp"]);

            // Check other segments
            Assert.Single(result.OtherSegments);
            Assert.True(result.OtherSegments.ContainsKey("MODULES:"));
            Assert.Equal(2, result.OtherSegments["MODULES:"].Count);
        }
        finally
        {
            // Cleanup
            if (File.Exists(crashLogPath)) File.Delete(crashLogPath);
        }
    }

    [Fact]
    public async Task ParseCrashLogContentAsync_WithComplexCrashLog_HandlesAllSegments()
    {
        // Arrange
        var crashLogPath = Path.Combine(_tempPath, $"complex-crash-{Guid.NewGuid()}.log");
        var content = @"Fallout 4 v1.10.163
Buffout 4 v1.26.2

Unhandled exception ""EXCEPTION_ACCESS_VIOLATION"" at 0x7FF7B5A6DDDD Fallout4.exe+0x16DDDD

SYSTEM SPECS:
    OS: Windows 10 Pro 64-bit
    CPU: Intel Core i7-9700K
    GPU: NVIDIA GeForce RTX 3080
    RAM: 32 GB

PROBABLE CALL STACK:
[0] 0x7FF7B5A6DDDD Fallout4.exe+0x16DDDD
[1] 0x7FF7B5A6EEEE Fallout4.exe+0x16EEEE

REGISTERS:
RAX 0x0                (NULL)
RCX 0x7FF7B5A6DDDD     (Fallout4.exe+0x16DDDD)

STACK:
RSP+00 0x7FF7B5A6DDDD  (Fallout4.exe+0x16DDDD)
RSP+08 0x7FF7B5A6EEEE  (Fallout4.exe+0x16EEEE)

MODULES:
0x000000400000 Fallout4.exe
0x7FFCB8E70000 ntdll.dll

F4SE PLUGINS:
    f4se_1_10_163.dll
    HighFPSPhysicsFix.dll v1.4

PLUGINS:
[00] Fallout4.esm
[01] DLCRobot.esm
[FE:000] SomePlugin.esp";

        await File.WriteAllTextAsync(crashLogPath, content);

        try
        {
            // Act
            var result = await _service.ParseCrashLogContentAsync(crashLogPath);

            // Assert
            Assert.Equal(crashLogPath, result.FilePath);
            Assert.Equal("1.10.163", result.GameVersion);
            Assert.Equal("Buffout 4 v1.26.2", result.CrashGeneratorName);

            // Check main error segment
            Assert.Single(result.MainErrorSegment);
            Assert.Contains("Unhandled exception", result.MainErrorSegment[0]);

            // Check call stack
            Assert.Equal(2, result.CallStack.Count);

            // Check loaded plugins
            Assert.Equal(3, result.LoadedPlugins.Count);

            // Check other segments
            Assert.Equal(5, result.OtherSegments.Count);
            Assert.True(result.OtherSegments.ContainsKey("SYSTEM SPECS:"));
            Assert.True(result.OtherSegments.ContainsKey("REGISTERS:"));
            Assert.True(result.OtherSegments.ContainsKey("STACK:"));
            Assert.True(result.OtherSegments.ContainsKey("MODULES:"));
            Assert.True(result.OtherSegments.ContainsKey("F4SE PLUGINS:"));

            // Check specific segment content
            Assert.Equal(4, result.OtherSegments["SYSTEM SPECS:"].Count);
            Assert.Equal(2, result.OtherSegments["REGISTERS:"].Count);
            Assert.Equal(2, result.OtherSegments["STACK:"].Count);
            Assert.Equal(2, result.OtherSegments["MODULES:"].Count);
            Assert.Equal(2, result.OtherSegments["F4SE PLUGINS:"].Count);
        }
        finally
        {
            // Cleanup
            if (File.Exists(crashLogPath)) File.Delete(crashLogPath);
        }
    }

    [Fact]
    public async Task ParseCrashLogContentAsync_WithMalformedPluginEntries_HandlesGracefully()
    {
        // Arrange
        var crashLogPath = Path.Combine(_tempPath, $"malformed-plugins-{Guid.NewGuid()}.log");
        var content = @"Fallout 4 v1.10.163

PLUGINS:
[00] Fallout4.esm
[01 DLCRobot.esm  (Missing closing bracket)
[FE:000 SomePlugin.esp  (Missing closing bracket)
[XX] InvalidIndex.esp
[] EmptyIndex.esp
Not a plugin line at all";

        await File.WriteAllTextAsync(crashLogPath, content);

        try
        {
            // Act
            var result = await _service.ParseCrashLogContentAsync(crashLogPath);

            // Assert
            Assert.Equal(crashLogPath, result.FilePath);
            Assert.Equal("1.10.163", result.GameVersion);

            // Check loaded plugins - only valid entries should be parsed
            Assert.Single(result.LoadedPlugins);
            Assert.True(result.LoadedPlugins.ContainsKey("Fallout4.esm"));
            Assert.Equal("00", result.LoadedPlugins["Fallout4.esm"]);

            // Invalid entries should be ignored
            Assert.False(result.LoadedPlugins.ContainsKey("DLCRobot.esm"));
            Assert.False(result.LoadedPlugins.ContainsKey("SomePlugin.esp"));
            Assert.False(result.LoadedPlugins.ContainsKey("InvalidIndex.esp"));
            Assert.False(result.LoadedPlugins.ContainsKey("EmptyIndex.esp"));
        }
        finally
        {
            // Cleanup
            if (File.Exists(crashLogPath)) File.Delete(crashLogPath);
        }
    }

    [Fact]
    public async Task ParseCrashLogContentAsync_WithEmptySegments_HandlesCorrectly()
    {
        // Arrange
        var crashLogPath = Path.Combine(_tempPath, $"empty-segments-{Guid.NewGuid()}.log");
        var content = @"Fallout 4 v1.10.163

PROBABLE CALL STACK:

PLUGINS:

MODULES:
";

        await File.WriteAllTextAsync(crashLogPath, content);

        try
        {
            // Act
            var result = await _service.ParseCrashLogContentAsync(crashLogPath);

            // Assert
            Assert.Equal(crashLogPath, result.FilePath);
            Assert.Equal("1.10.163", result.GameVersion);

            // Check segments - they should be empty but initialized
            Assert.Empty(result.CallStack);
            Assert.Empty(result.LoadedPlugins);
            Assert.Single(result.OtherSegments);
            Assert.True(result.OtherSegments.ContainsKey("MODULES:"));
            Assert.Empty(result.OtherSegments["MODULES:"]);
        }
        finally
        {
            // Cleanup
            if (File.Exists(crashLogPath)) File.Delete(crashLogPath);
        }
    }

    [Fact]
    public async Task ParseCrashLogContentAsync_WithGenericCrashGenerator_ParsesCorrectly()
    {
        // Arrange
        var crashLogPath = Path.Combine(_tempPath, $"generic-crashgen-{Guid.NewGuid()}.log");
        var content = @"Fallout 4 v1.10.163
CrashLogger v1.5.0

Unhandled exception at 0x7FF7B5A6DDDD

PLUGINS:
[00] Fallout4.esm";

        await File.WriteAllTextAsync(crashLogPath, content);

        try
        {
            // Act
            var result = await _service.ParseCrashLogContentAsync(crashLogPath);

            // Assert
            Assert.Equal(crashLogPath, result.FilePath);
            Assert.Equal("1.10.163", result.GameVersion);
            Assert.Equal("CrashLogger v1.5.0", result.CrashGeneratorName);

            // Check main error segment
            Assert.Single(result.MainErrorSegment);

            // Check loaded plugins
            Assert.Single(result.LoadedPlugins);
            Assert.True(result.LoadedPlugins.ContainsKey("Fallout4.esm"));
        }
        finally
        {
            // Cleanup
            if (File.Exists(crashLogPath)) File.Delete(crashLogPath);
        }
    }
}